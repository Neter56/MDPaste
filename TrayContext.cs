using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace MDPaste
{
    internal sealed class TrayContext : ApplicationContext
    {
        private readonly HotkeyWindow hotkey = new HotkeyWindow();
        private readonly NotifyIcon tray;
        private readonly Icon icon;
        private readonly ContextMenuStrip menu;
        private readonly Timer pasteTimer = new Timer { Interval = 25 };
        private Shortcut shortcut = new Shortcut();
        private SettingsForm? settingsForm;
        private ConvertedContent? pendingContent;
        private IntPtr pasteTarget;
        private uint clipboardVersion;
        private readonly Stopwatch releaseWait = new Stopwatch();
        private int clipboardAttempts;

        [DllImport("user32.dll")]
        private static extern uint GetClipboardSequenceNumber();

        internal TrayContext()
        {
            icon = CreateIcon();
            menu = new ContextMenuStrip();
            menu.Items.Add("Settings...", null, (_, __) => OpenSettings());
            menu.Items.Add("Convert clipboard only", null, (_, __) => ConvertOnly());
            menu.Items.Add("Exit", null, (_, __) => ExitThread());
            tray = new NotifyIcon { Icon = icon, Visible = true, ContextMenuStrip = menu };
            tray.DoubleClick += (_, __) => OpenSettings();
            hotkey.Pressed += (_, __) => BeginPaste();
            pasteTimer.Tick += (_, __) => ContinuePaste();
            Exception? startupError = null;
            try { shortcut = Settings.Load(); }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException ||
                                      e is SerializationException || e is System.Xml.XmlException)
            {
                startupError = e;
            }
            try { hotkey.Register(shortcut); }
            catch (System.ComponentModel.Win32Exception e) { startupError = e; }
            UpdateTooltip();
            if (startupError != null)
            {
                Program.ReportError(startupError);
                OpenSettings();
            }
            else
                tray.ShowBalloonTip(3000, "MDPaste is ready", "Copy Markdown, then press " + shortcut +
                    ". Right-click this icon for Settings.", ToolTipIcon.Info);
        }

        private void UpdateTooltip() => tray.Text = "MDPaste - " + (hotkey.IsRegistered ? shortcut.ToString() : "shortcut unavailable");

        private static Icon CreateIcon()
        {
            using (var bitmap = new Bitmap(32, 32))
            using (var graphics = Graphics.FromImage(bitmap))
            using (var font = new Font("Segoe UI", 17, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var background = new SolidBrush(Color.FromArgb(35, 93, 145)))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillRectangle(background, 1, 1, 30, 30);
                graphics.DrawString("M", font, Brushes.White, 5, 5);
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (var temporary = Icon.FromHandle(handle))
                        return (Icon)temporary.Clone();
                }
                finally { DestroyIcon(handle); }
            }
        }

        [DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr icon);

        private void OpenSettings()
        {
            CancelPending();
            if (settingsForm != null && !settingsForm.IsDisposed)
            {
                settingsForm.Activate();
                return;
            }
            try
            {
                settingsForm = new SettingsForm(shortcut, selected =>
            {
                if (hotkey.IsRegistered && selected.Modifiers == shortcut.Modifiers && selected.Key == shortcut.Key)
                {
                    Settings.Save(selected);
                    return;
                }
                var previous = shortcut;
                hotkey.Register(selected);
                try { Settings.Save(selected); }
                catch (Exception saveError)
                {
                    try { hotkey.Register(previous); }
                    catch (System.ComponentModel.Win32Exception rollbackError)
                    {
                        shortcut = selected;
                        UpdateTooltip();
                        throw new AggregateException("Settings could not be saved, and the old shortcut could not be restored. " +
                            "The new shortcut remains active for this session.", saveError, rollbackError);
                    }
                    throw;
                }
                shortcut = selected;
                UpdateTooltip();
                });
                settingsForm.Show();
            }
            catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException ||
                                      e is System.Security.SecurityException)
            {
                Program.ReportError(e);
            }
        }

        private void BeginPaste()
        {
            if (pasteTimer.Enabled || (settingsForm != null && settingsForm.ContainsFocus))
                return;
            pasteTarget = Native.GetForegroundWindow();
            clipboardVersion = GetClipboardSequenceNumber();
            clipboardAttempts = 0;
            releaseWait.Restart();
            pasteTimer.Start();
        }

        private void ContinuePaste()
        {
            try
            {
                if (Native.GetForegroundWindow() != pasteTarget || pasteTarget == IntPtr.Zero)
                    throw new InvalidOperationException("Paste cancelled because the active window changed. Try the shortcut again.");
                if (GetClipboardSequenceNumber() != clipboardVersion)
                    throw new InvalidOperationException("Paste cancelled because another app changed the clipboard. Copy the Markdown again.");
                if (!Native.ShortcutReleased(shortcut.Key))
                {
                    if (releaseWait.ElapsedMilliseconds > 3000)
                        throw new InvalidOperationException("Paste cancelled: release the shortcut keys within three seconds.");
                    return;
                }
                if (pendingContent == null)
                    pendingContent = MarkdownConverter.Convert(ReadClipboardText());
                // Rendering can take time. Check focus and clipboard again before committing.
                if (Native.GetForegroundWindow() != pasteTarget || GetClipboardSequenceNumber() != clipboardVersion)
                    throw new InvalidOperationException("Paste cancelled because the window or clipboard changed during conversion.");
                Clipboard.SetDataObject(pendingContent.ToClipboardData(), true, 3, 25);
                if (Native.GetForegroundWindow() != pasteTarget)
                    throw new InvalidOperationException("Clipboard converted, but focus changed. Paste manually with Ctrl+V.");
                pasteTimer.Stop();
                Native.Paste();
                CancelPending();
            }
            catch (ExternalException e)
            {
                if (++clipboardAttempts < 10)
                    return;
                Fail(e);
            }
            catch (Exception e) { Fail(e); }
        }

        private static string ReadClipboardText()
        {
            if (!Clipboard.ContainsText(TextDataFormat.UnicodeText))
                throw new InvalidOperationException("Copy Markdown text first. The clipboard does not contain text.");
            return Clipboard.GetText(TextDataFormat.UnicodeText);
        }

        private void ConvertOnly()
        {
            CancelPending();
            try
            {
                var content = MarkdownConverter.Convert(ReadClipboardText());
                Clipboard.SetDataObject(content.ToClipboardData(), true, 10, 50);
                tray.ShowBalloonTip(2500, "Clipboard converted", "Switch to your app and paste with Ctrl+V.", ToolTipIcon.Info);
            }
            catch (Exception e) { Fail(e); }
        }

        private void Fail(Exception e)
        {
            CancelPending();
            Program.ReportError(e);
        }

        private void CancelPending()
        {
            pasteTimer.Stop();
            pendingContent = null;
            releaseWait.Reset();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                pasteTimer.Dispose();
                settingsForm?.Dispose();
                hotkey.Dispose();
                tray.Visible = false;
                tray.Dispose();
                menu.Dispose();
                icon.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}

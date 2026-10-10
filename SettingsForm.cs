using System;
using System.Drawing;
using System.Windows.Forms;

namespace MDPaste
{
    internal sealed class SettingsForm : Form
    {
        internal SettingsForm(Shortcut current, Action<Shortcut> apply)
        {
            Text = "MDPaste - Settings";
            ClientSize = new Size(460, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.Dpi;
            Font = new Font("Segoe UI", 9);
            var title = new Label { Text = "Paste Markdown as rich text", AutoSize = true, Location = new Point(20, 20),
                Font = new Font(Font, FontStyle.Bold) };
            var explanation = new Label { Text = "Copy Markdown in any app, then press your shortcut.\n" +
                "Formatting support depends on the app receiving the paste.", AutoSize = true, Location = new Point(20, 52) };
            var ctrl = new CheckBox { Text = "Ctrl", Checked = current.Control, Location = new Point(20, 106), Width = 65 };
            var alt = new CheckBox { Text = "Alt", Checked = current.Alt, Location = new Point(90, 106), Width = 65 };
            var shift = new CheckBox { Text = "Shift", Checked = current.Shift, Location = new Point(160, 106), Width = 70 };
            var win = new CheckBox { Text = "Win", Checked = current.Windows, Location = new Point(235, 106), Width = 65 };
            var key = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(320, 104), Width = 105 };
            for (Keys value = Keys.A; value <= Keys.Z; value++) key.Items.Add(value);
            for (Keys value = Keys.D0; value <= Keys.D9; value++) key.Items.Add(value);
            for (Keys value = Keys.F1; value <= Keys.F12; value++) key.Items.Add(value);
            key.Items.Add(Keys.Insert);
            key.SelectedItem = current.Key;
            var note = new Label { Text = "The tray menu also has \"Convert clipboard only\".\n" +
                "Use it if the shortcut is unavailable or automatic paste is blocked.\n" +
                "Your original Markdown stays available as plain-text clipboard data.",
                AutoSize = true, Location = new Point(20, 151) };
            var startup = new CheckBox { Text = "Start MDPaste when I sign in to Windows",
                Checked = WindowsStartup.IsEnabled(), AutoSize = true, Location = new Point(20, 215) };
            var startupNote = new Label { Text = "Keep the executable in its permanent location before enabling.",
                AutoSize = true, Location = new Point(20, 242) };
            var save = new Button { Text = "Save", Location = new Point(260, 280), Width = 80 };
            var cancel = new Button { Text = "Cancel", Location = new Point(350, 280), Width = 80, DialogResult = DialogResult.Cancel };
            save.Click += (_, __) =>
            {
                try
                {
                    if (!(key.SelectedItem is Keys selectedKey))
                        throw new InvalidOperationException("Choose a shortcut key.");
                    var selected = new Shortcut { Control = ctrl.Checked, Alt = alt.Checked, Shift = shift.Checked,
                        Windows = win.Checked, Key = selectedKey };
                    selected.Validate();
                    apply(selected);
                    try { WindowsStartup.SetEnabled(startup.Checked); }
                    catch (Exception e) when (e is System.IO.IOException || e is UnauthorizedAccessException ||
                                              e is System.Security.SecurityException || e is ArgumentException)
                    {
                        throw new InvalidOperationException("Your shortcut was saved, but Windows startup could not be changed. " +
                            "Check registry permissions and try again.", e);
                    }
                    Close();
                }
                catch (Exception e) { Program.ReportError(e); }
            };
            Controls.AddRange(new Control[] { title, explanation, ctrl, alt, shift, win, key, note,
                startup, startupNote, save, cancel });
            AcceptButton = save;
            CancelButton = cancel;
        }
    }
}

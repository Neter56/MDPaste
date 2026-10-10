using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;

namespace MDPaste
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            AppDomain.CurrentDomain.AssemblyResolve += ResolveEmbeddedAssembly;
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            if (args.Length == 1 && args[0] == "--smoke-test")
                return SmokeTest();

            using (var mutex = new Mutex(true, @"Local\MDPaste.Tray", out bool firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show("MDPaste is already running. Look for its icon in the system tray.",
                        "MDPaste", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += (_, e) => ReportError(e.Exception);
                try
                {
                    using (var context = new TrayContext())
                        Application.Run(context);
                    return 0;
                }
                catch (Exception e)
                {
                    ReportError(e);
                    return 1;
                }
            }
        }

        private static Assembly? ResolveEmbeddedAssembly(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name + ".dll";
            using (Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("Dependencies." + name))
            {
                if (stream == null)
                    return null;
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    return Assembly.Load(memory.ToArray());
                }
            }
        }

        private static int SmokeTest()
        {
            try
            {
                using (var hotkey = new HotkeyWindow())
                {
                    hotkey.Register(new Shortcut { Control = true, Alt = true, Shift = true, Key = Keys.F12 });
                    var content = MarkdownConverter.Convert("**Smoke test**\n\n| A | B |\n|---|---|\n| 1 | 2 |");
                    if (!content.Html.Contains("<strong>") || !content.Rtf.Contains("\\trowd"))
                        throw new InvalidOperationException("Rendering smoke test failed.");
                }
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-test.txt"),
                    "PASS: embedded dependencies, Markdown rendering, HTML/RTF tables, and global hotkey registration.");
                return 0;
            }
            catch (Exception e)
            {
                File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "smoke-test.txt"), e.ToString());
                return 1;
            }
        }

        internal static void ReportError(Exception error)
        {
            string message = error.Message;
            try
            {
                Directory.CreateDirectory(Settings.DirectoryPath);
                File.AppendAllText(Path.Combine(Settings.DirectoryPath, "errors.log"),
                    DateTimeOffset.Now.ToString("o") + " " + error + Environment.NewLine);
            }
            catch (IOException logError)
            {
                message += "\n\nCould not write the error log: " + logError.Message;
            }
            catch (UnauthorizedAccessException logError)
            {
                message += "\n\nCould not write the error log: " + logError.Message;
            }
            MessageBox.Show(message, "MDPaste", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}

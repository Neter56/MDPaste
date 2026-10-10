using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MDPaste
{
    internal static class WindowsStartup
    {
        private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "MDPaste";

        internal static bool IsEnabled()
        {
            using (var key = Registry.CurrentUser.OpenSubKey(RunKey))
                return key != null && IsEnabled(key, ValueName, Application.ExecutablePath);
        }

        internal static void SetEnabled(bool enabled)
        {
            if (enabled)
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RunKey, true))
                {
                    if (key == null)
                        throw new IOException("Could not open the Windows startup registry key.");
                    SetEnabled(key, ValueName, true, Application.ExecutablePath);
                }
            }
            else
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RunKey, true))
                    if (key != null)
                        SetEnabled(key, ValueName, false, Application.ExecutablePath);
            }
        }

        internal static bool IsEnabled(RegistryKey key, string name, string executablePath) =>
            string.Equals(key.GetValue(name) as string, Command(executablePath), StringComparison.OrdinalIgnoreCase);

        internal static void SetEnabled(RegistryKey key, string name, bool enabled, string executablePath)
        {
            if (enabled)
                key.SetValue(name, Command(executablePath), RegistryValueKind.String);
            else
                key.DeleteValue(name, false);
        }

        private static string Command(string path)
        {
            if (!Path.IsPathRooted(path) || path.Contains("\"") || path.Contains("\r") || path.Contains("\n"))
                throw new ArgumentException("Windows startup requires a valid absolute executable path.");
            return "\"" + path + "\"";
        }
    }
}

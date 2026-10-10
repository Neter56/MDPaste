using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Windows.Forms;

namespace MDPaste
{
    [DataContract]
    internal sealed class Shortcut
    {
        [DataMember] public bool Control { get; set; } = true;
        [DataMember] public bool Shift { get; set; } = true;
        [DataMember] public bool Alt { get; set; }
        [DataMember] public bool Windows { get; set; }
        [DataMember] public Keys Key { get; set; } = Keys.V;

        public uint Modifiers => (Alt ? 1u : 0u) | (Control ? 2u : 0u) |
                                 (Shift ? 4u : 0u) | (Windows ? 8u : 0u) | 0x4000u;

        public void Validate()
        {
            if (!Control && !Alt && !Windows)
                throw new InvalidDataException("Choose at least Ctrl, Alt, or Win to avoid intercepting normal typing.");
            bool supported = (Key >= Keys.A && Key <= Keys.Z) || (Key >= Keys.D0 && Key <= Keys.D9) ||
                             (Key >= Keys.F1 && Key <= Keys.F12) || Key == Keys.Insert;
            if (!supported)
                throw new InvalidDataException("Choose a letter, number, F1-F12, or Insert.");
            if (Control && !Alt && !Shift && !Windows &&
                (Key == Keys.V || Key == Keys.C || Key == Keys.X || Key == Keys.Z || Key == Keys.A))
                throw new InvalidDataException("This shortcut would replace a standard editing command. Add another modifier.");
        }

        public override string ToString() =>
            (Control ? "Ctrl+" : "") + (Alt ? "Alt+" : "") + (Shift ? "Shift+" : "") +
            (Windows ? "Win+" : "") + (Key >= Keys.D0 && Key <= Keys.D9 ? ((int)Key - (int)Keys.D0).ToString() : Key.ToString());
    }

    internal static class Settings
    {
        internal static readonly string DirectoryPath =
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MDPaste");
        private static readonly string FilePath = Path.Combine(DirectoryPath, "settings.json");

        internal static Shortcut Load()
        {
            string path = FilePath;
            if (!File.Exists(path))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "MarkdownPaste", "settings.json");
            if (!File.Exists(path))
                return new Shortcut();
            using (var file = File.OpenRead(path))
            {
                var shortcut = (Shortcut?)new DataContractJsonSerializer(typeof(Shortcut)).ReadObject(file);
                if (shortcut == null)
                    throw new InvalidDataException("The shortcut settings file is empty.");
                shortcut.Validate();
                return shortcut;
            }
        }

        internal static void Save(Shortcut shortcut)
        {
            shortcut.Validate();
            Directory.CreateDirectory(DirectoryPath);
            string temporary = FilePath + ".tmp";
            using (var file = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                new DataContractJsonSerializer(typeof(Shortcut)).WriteObject(file, shortcut);
            if (File.Exists(FilePath))
                File.Replace(temporary, FilePath, null);
            else
                File.Move(temporary, FilePath);
        }
    }
}

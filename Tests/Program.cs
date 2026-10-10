using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MDPaste
{
    internal static class Tests
    {
        [STAThread]
        private static int Main()
        {
            var cases = new Dictionary<string, Action>
            {
                ["bold and italic"] = () => Check("**Bold** and *italic*", "<strong>Bold</strong>", @"\b ", @"\i "),
                ["headings"] = () => Check("# Heading\n\n###### Small", "<h1", @"\fs48", @"\fs22"),
                ["tables"] = () => Check("| A | B |\n|---|---|\n| 1 | **2** |", "<table", @"\trowd", @"\cellx4680", @"\cellx9360", @"\row"),
                ["ordered and nested lists"] = () => Check("3. Third\n4. Fourth\n   - Nested", "<ol start=\"3\">", "3. ", "4. ", @"\u8226?", @"\li720"),
                ["task lists"] = () => Check("- [x] Done\n- [ ] Todo", "&#9745;", @"\u9745?", @"\u9744?"),
                ["strike, subscript, superscript"] = () => Check("~~gone~~ H~2~O x^2^", "<del>gone</del>", @"\strike", @"\sub", @"\super"),
                ["quotes and code"] = () => Check("> Quote\n\n```cs\nvar x = \"{\\\\}\";\n```", "<blockquote", @"\f1", @"\{", @"\\"),
                ["links and images"] = () => Check("[Site](https://example.com) ![Alt](https://example.com/a.png)", "href=\"https://example.com\"", "HYPERLINK", "[Image: Alt]"),
                ["unicode CF_HTML byte offsets"] = TestHtmlOffsets,
                ["raw HTML and dangerous links"] = TestUnsafeContent,
                ["clipboard formats"] = TestFormats,
                ["native clipboard UTF-8 serialization"] = TestNativeClipboard,
                ["RTF parses and preserves unicode"] = TestRtf,
                ["loose lists and soft line breaks"] = TestListAndBreaks,
                ["table alignment"] = () => Check("| Left | Right |\n|:---|---:|\n| x | y |", "text-align: right", @"\qr"),
                ["footnotes and definition lists"] = () => Check("Term\n:   Definition\n\nNote[^1]\n\n[^1]: Footnote text", "<dl>", "Footnote text", "Definition"),
                ["horizontal rules and hard breaks"] = () => Check("first  \nsecond\n\n---", "<br", @"\line", @"\brdrb"),
                ["input bounds"] = () => { Throws(() => MarkdownConverter.Convert(" ")); Throws(() => MarkdownConverter.Convert(new string('x', MarkdownConverter.MaximumCharacters + 1))); },
                ["shortcut validation"] = () => { new Shortcut().Validate(); Throws(() => new Shortcut { Control = false, Shift = true }.Validate()); Throws(() => new Shortcut { Shift = false }.Validate()); },
                ["global hotkey conflict preserves registration"] = TestHotkeys,
                ["Windows startup enable, update, disable"] = TestStartup
            };
            int failed = 0;
            foreach (var test in cases)
            {
                try { test.Value(); Console.WriteLine("PASS " + test.Key); }
                catch (Exception e) { failed++; Console.WriteLine("FAIL " + test.Key + ": " + e); }
            }
            Console.WriteLine($"{cases.Count - failed}/{cases.Count} passed.");
            return failed == 0 ? 0 : 1;
        }

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Check(string markdown, string html, params string[] rtf)
        {
            var result = MarkdownConverter.Convert(markdown);
            Assert(result.Html.Contains(html), "HTML missing " + html + "\n" + result.Html);
            foreach (var expected in rtf)
                Assert(result.Rtf.Contains(expected), "RTF missing " + expected + "\n" + result.Rtf);
        }

        private static void TestHtmlOffsets()
        {
            string fragment = MarkdownConverter.Convert("# Caf\u00e9 \u4e16\u754c \ud83d\ude00\n\n**bold**").Html;
            string payload = MarkdownConverter.BuildClipboardHtml(fragment);
            byte[] bytes = Encoding.UTF8.GetBytes(payload);
            int Offset(string key) => int.Parse(payload.Split(new[] { "\r\n" }, StringSplitOptions.None)
                .First(line => line.StartsWith(key + ":", StringComparison.Ordinal)).Substring(key.Length + 1), CultureInfo.InvariantCulture);
            int start = Offset("StartFragment"), end = Offset("EndFragment");
            Assert(Encoding.UTF8.GetString(bytes, start, end - start) == fragment, "Fragment offsets must be UTF-8 byte offsets.");
            Assert(Offset("EndHTML") == bytes.Length, "EndHTML mismatch.");
            Assert(Encoding.UTF8.GetString(bytes, Offset("StartHTML"), 15) == "<!DOCTYPE html>", "StartHTML mismatch.");
        }

        private static void TestUnsafeContent()
        {
            var result = MarkdownConverter.Convert("<script>alert(1)</script>\n\n[x](javascript:alert%281%29)\n\n![secret](file:///C:/secret.txt)");
            Assert(!result.Html.Contains("<script>"), "Raw HTML must not be executable.");
            Assert(!result.Html.Contains("href=\"javascript:"), "Unsafe hyperlink emitted.");
            Assert(!result.Html.Contains("src=\"file:"), "Local image emitted.");
        }

        private static void TestFormats()
        {
            var result = MarkdownConverter.Convert("**Clipboard**");
            var data = result.ToClipboardData();
            foreach (string format in new[] { DataFormats.UnicodeText, DataFormats.Html, DataFormats.Rtf })
                Assert(data.GetDataPresent(format, false), "Missing clipboard format " + format);
            Assert((string)data.GetData(DataFormats.UnicodeText, false) == "**Clipboard**", "Original text not preserved.");
        }

        private static void TestRtf()
        {
            var result = MarkdownConverter.Convert("# Title\n\n**Bold** \u00e9 \u4e16\u754c \ud83d\ude00\n\n| A | B |\n|---|---|\n| 1 | 2 |\n\n```txt\n{a}\\b\n```");
            using (var box = new RichTextBox())
            {
                box.Rtf = result.Rtf;
                Assert(box.Text.Contains("Bold \u00e9 \u4e16\u754c \ud83d\ude00"), "Unicode did not round-trip through RTF.");
                Assert(box.Text.Contains("{a}\\b"), "Code escapes did not round-trip.");
                box.Select(box.Text.IndexOf("Bold", StringComparison.Ordinal), 4);
                Assert(box.SelectionFont != null && box.SelectionFont.Bold, "Bold style not recognized by Windows RichEdit.");
            }

        }

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr memory);
        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr memory);
        [DllImport("kernel32.dll")]
        private static extern UIntPtr GlobalSize(IntPtr memory);
        [DllImport("ole32.dll")]
        private static extern void ReleaseStgMedium(ref STGMEDIUM medium);

        private static void TestNativeClipboard()
        {
            var result = MarkdownConverter.Convert("**Caf\u00e9 \u4e16\u754c \ud83d\ude00**");
            var data = result.ToClipboardData();
            var native = (System.Runtime.InteropServices.ComTypes.IDataObject)data;
            var format = new FORMATETC
            {
                cfFormat = unchecked((short)DataFormats.GetFormat(DataFormats.Html).Id),
                dwAspect = DVASPECT.DVASPECT_CONTENT,
                lindex = -1,
                tymed = TYMED.TYMED_HGLOBAL
            };
            native.GetData(ref format, out STGMEDIUM medium);
            try
            {
                IntPtr pointer = GlobalLock(medium.unionmember);
                Assert(pointer != IntPtr.Zero, "Could not lock clipboard data.");
                try
                {
                    int length = checked((int)GlobalSize(medium.unionmember).ToUInt64());
                    byte[] bytes = new byte[length];
                    Marshal.Copy(pointer, bytes, 0, bytes.Length);
                    string actual = Encoding.UTF8.GetString(bytes).TrimEnd('\0');
                    Assert(actual == MarkdownConverter.BuildClipboardHtml(result.Html),
                        "Windows clipboard HTML serialization changed the UTF-8 payload.");
                }
                finally { GlobalUnlock(medium.unionmember); }
            }
            finally { ReleaseStgMedium(ref medium); }
        }

        private static void TestListAndBreaks()
        {
            var result = MarkdownConverter.Convert("1. First\n\n2. Second\n\nsoft\nbreak\n\nhard  \nbreak");
            using (var box = new RichTextBox())
            {
                box.Rtf = result.Rtf;
                Assert(box.Text.Contains("1. First") && box.Text.Contains("2. Second"), "Loose list markers separated from content.");
                Assert(box.Text.Contains("soft break"), "Soft breaks should collapse as in HTML.");
                Assert(box.Text.Contains("hard\nbreak"), "Hard breaks should remain line breaks: " +
                    box.Text.Replace("\r", "\\r").Replace("\n", "\\n").Replace("\v", "\\v") + "\n" + result.Rtf);
            }
        }

        private static void TestHotkeys()
        {
            var shortcut = new Shortcut { Control = true, Alt = true, Shift = true, Key = Keys.F11 };
            using (var first = new HotkeyWindow())
            using (var second = new HotkeyWindow())
            {
                first.Register(shortcut);
                Throws(() => second.Register(shortcut));
                first.Register(new Shortcut { Control = true, Alt = true, Shift = true, Key = Keys.F10 });
                second.Register(shortcut);
            }
        }

        private static void TestStartup()
            {
                string testPath = @"Software\MDPaste.Tests." + Guid.NewGuid().ToString("N");
                try
                {
                    using (var key = Registry.CurrentUser.CreateSubKey(testPath))
                    {
                        const string original = @"C:\Program Files\MDPaste\MDPaste.exe";
                        const string moved = @"C:\Tools\MDPaste.exe";
                        Assert(!WindowsStartup.IsEnabled(key, "MDPaste", original), "Startup must default to disabled.");
                        key.SetValue("UnrelatedApp", "leave this alone");
                        WindowsStartup.SetEnabled(key, "MDPaste", true, original);
                        Assert((string)key.GetValue("MDPaste") == "\"" + original + "\"", "Executable path must be quoted.");
                        Assert(WindowsStartup.IsEnabled(key, "MDPaste", original), "Startup registration missing.");
                        Assert(!WindowsStartup.IsEnabled(key, "MDPaste", moved), "Moved executable must not appear registered.");
                        WindowsStartup.SetEnabled(key, "MDPaste", true, moved);
                        Assert(WindowsStartup.IsEnabled(key, "MDPaste", moved), "Startup path was not updated.");
                        WindowsStartup.SetEnabled(key, "MDPaste", false, moved);
                        WindowsStartup.SetEnabled(key, "MDPaste", false, moved);
                        Assert(key.GetValue("MDPaste") == null, "Startup registration was not removed.");
                        Assert((string)key.GetValue("UnrelatedApp") == "leave this alone", "Unrelated startup entry changed.");
                        Throws(() => WindowsStartup.SetEnabled(key, "MDPaste", true, "relative.exe"));
                    }
                }
                finally { Registry.CurrentUser.DeleteSubKey(testPath, false); }
        }

        private static void Throws(Action action)
        {
            try { action(); }
            catch (ArgumentException) { return; }
            catch (System.IO.InvalidDataException) { return; }
            catch (System.ComponentModel.Win32Exception) { return; }
            throw new InvalidOperationException("Expected explicit rejection.");
        }
    }
}

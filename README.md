# MDPaste

Source repository: https://github.com/Neter56/MDPaste

Portable executable and ZIP are included in `dist`. You can also download the
ZIP from the repository's Releases page when available.

A small Windows tray utility. Copy Markdown, then press **Ctrl+Shift+V** to
paste formatted content into the focused app. No browser engine, telemetry,
background service, or administrator rights.

## Run

1. Run `MDPaste.exe`. Windows may warn because this build is unsigned.
2. Copy Markdown text, for example `**This text**`.
3. Focus an editable field in Word, Teams, Outlook, or another rich-text app.
4. Press **Ctrl+Shift+V**, then release the keys. The app converts the clipboard
   and sends a normal **Ctrl+V**.

Look in the system tray overflow if the blue **M** icon is hidden.
Right-click it for **Settings**, **Convert clipboard only**, or **Exit**.
Double-click it to change the shortcut. Settings are saved per Windows user.
If Windows or another app already owns the shortcut, choose a different one.
Suggested alternative: **Ctrl+Alt+V**.

## Requirements and size

- Windows 10/11 with **.NET Framework 4.8** (included with Windows 11; install it
  separately on older Windows 10 installations if needed).
- The distributed executable contains its Markdown/HTML parsing dependencies.
  No .NET 8 Desktop Runtime, Node.js, Python, or additional DLLs are needed.
- Run as a normal user. Automatic paste into elevated apps is blocked by
  Windows; use **Convert clipboard only**, then paste manually in that app.

## Formatting

CommonMark formatting, headings 1-6, bold, italic, nested ordered/bullet lists,
block quotes, fenced/indented code, links, images, horizontal rules, line breaks,
pipe/grid tables, task lists, strikethrough, subscript, superscript, footnotes,
definition lists, and automatic links are supported.

The clipboard offers **HTML**, **RTF**, and the **original Markdown as plain
text**. The receiving application chooses a format and may simplify or strip
styles. Word and Outlook typically retain more formatting than Teams. A plain
text-only field receives the original Markdown. This app cannot force an app
to accept formatting.

Markdown images are URL references in HTML, not downloaded or embedded by this
utility. The receiving app may load remote images. RTF uses image-alt-text
placeholders. Relative/local image paths are not loaded. RTF represents lists
as indented text and numbers rather than editable Word list objects. HTML is
preferred for the most complete structure; complex nested tables and merged
cells have limited RTF fidelity. Math/LaTeX, Mermaid diagrams, embedded HTML,
and app-specific Markdown extensions are not rendered.

Raw HTML is disabled. Only HTTP/HTTPS image URLs and HTTP/HTTPS/mailto links
(plus document-local anchors) are permitted. Rendering is local and offline.
There is a 2,097,152-character input safety limit.

## Clipboard and shortcuts

- The clipboard is intentionally replaced with converted content, retaining
  the original Markdown in its plain-text representation. It is not restored
  after paste, so repeated normal pastes work.
- Conversion only happens on the shortcut or the explicit tray-menu action.
  There is no continuous clipboard polling.
- Release the shortcut within three seconds. If focus or clipboard contents
  change while conversion is pending, automatic paste is cancelled.
- The app registers a global shortcut. Some apps use Ctrl+Shift+V themselves
  for plain-text paste; choose a different shortcut if you need that behavior.
- Run only one instance per interactive session. Exit from the tray menu.
- To start automatically at sign-in, open **Settings**, select **Start MDPaste
  when I sign in to Windows**, and click **Save**. This is off by default.
  It registers only this user through the Windows `Run` registry key; no
  administrator rights are needed. Uncheck it and save to remove the entry.
  Keep the executable in a permanent location before enabling this option.
  If you move it, run it from its new location and enable/save startup again.
  Windows Settings or Task Manager can separately disable startup apps;
  re-enable MDPaste there if Windows has disabled it.
- Settings and error logs live in `%LOCALAPPDATA%\MDPaste`.
  Existing shortcut settings from the earlier MarkdownPaste name are read
  automatically until you save settings under the new name.
  Error logs contain exceptions, not the copied text.

## Build and verify

Install the .NET 8 SDK. From this source folder in PowerShell:

```powershell
.\build.ps1
```

This builds the app, runs the tests and standalone smoke test, and packages
the portable executable and notices in `dist\MDPaste-Windows.zip`.
To run the individual steps instead:

```powershell
dotnet build .\MDPaste.csproj -c Release
dotnet build .\Tests\MDPaste.Tests.csproj -c Release
& .\Tests\bin\Release\net48\MDPaste.Tests.exe
```

The application executable is at `bin\Release\net48\MDPaste.exe`.
Its referenced DLLs are embedded at build time. Copy just that executable to
an empty folder to verify it runs without neighboring dependencies:

```powershell
& .\MDPaste.exe --smoke-test
Get-Content .\smoke-test.txt
```

The smoke test does not read or change the clipboard and briefly registers
Ctrl+Alt+Shift+F12. Tests check Markdown output, UTF-8 clipboard offsets, Unicode
RTF parsing with Windows RichEdit, format availability, bounds, unsafe URL
filtering, native OLE clipboard serialization, hotkey conflicts, and startup
registration enable/update/disable without changing real startup entries. Copy the
contents of `SAMPLE.md` for a manual receiving-app test in the specific versions
of Word, Teams, and Outlook you use. Definition-list syntax requires at least
three spaces after the colon, as shown in the sample.

## Dependencies and licensing

Uses Markdig 0.37.0 (BSD-2-Clause) and Html Agility Pack 1.11.71 (MIT), plus their
runtime dependencies. The binary package includes their license notices.

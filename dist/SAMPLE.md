# MDPaste test

Copy this entire file as plain text, focus a rich-text editor, and press your
MDPaste shortcut.

## Inline formatting

**This text** is bold. This is *italic*, ***bold italic***, and ~~struck out~~.
Inline code: `var answer = 42;`. Subscript: H~2~O. Superscript: x^2^.

Unicode: cafe with an accent: caf&eacute;.

[Microsoft Learn](https://learn.microsoft.com/)

## Lists

- First bullet
- **Second bullet**
  - Nested bullet
  - Another nested bullet

1. First numbered item
2. Second numbered item
3. Third numbered item

- [x] Completed task
- [ ] Pending task

## Table

| Feature | Supported | Notes |
|:--------|:---------:|------:|
| **Bold** | Yes | HTML + RTF |
| Headings | Yes | Six levels |
| Tables | Yes | Receiver-dependent |

## Quote and code

> A block quote with **bold text**.
>
> A second paragraph.

```csharp
var greeting = "Hello, world!";
Console.WriteLine(greeting);
```

## Breaks

The next line is a hard break.  
Here is the next line.

---

## Extended Markdown

Markdown
:   Plain text with formatting markers.

Rich text
:   Formatted content that a compatible destination can paste.

A sentence with a footnote.[^note]

[^note]: This is the footnote text.

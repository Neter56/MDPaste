using System;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Windows.Forms;
using HtmlAgilityPack;
using Markdig;

namespace MDPaste
{
    internal sealed class ConvertedContent
    {
        internal string PlainText { get; }
        internal string Html { get; }
        internal string Rtf { get; }
        internal ConvertedContent(string plainText, string html, string rtf)
        {
            PlainText = plainText;
            Html = html;
            Rtf = rtf;
        }

        internal DataObject ToClipboardData()
        {
            var data = new DataObject();
            data.SetData(DataFormats.UnicodeText, false, PlainText);
            data.SetData(DataFormats.Html, false, MarkdownConverter.BuildClipboardHtml(Html));
            data.SetData(DataFormats.Rtf, false, Rtf);
            return data;
        }
    }

    internal static class MarkdownConverter
    {
        internal const int MaximumCharacters = 2 * 1024 * 1024;
        private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
            .UsePipeTables().UseGridTables().UseTaskLists().UseAutoLinks().UseEmphasisExtras()
            .UseFootnotes().UseDefinitionLists().DisableHtml().Build();

        internal static ConvertedContent Convert(string markdown)
        {
            if (string.IsNullOrWhiteSpace(markdown))
                throw new ArgumentException("The clipboard has no Markdown text to paste.");
            if (markdown.Length > MaximumCharacters)
                throw new ArgumentException("The clipboard text exceeds the 2-million-character safety limit.");
            var document = new HtmlAgilityPack.HtmlDocument();
            document.LoadHtml(Markdown.ToHtml(markdown, Pipeline));
            foreach (var node in document.DocumentNode.Descendants().ToArray())
            {
                if (node.Name == "a" || node.Name == "img")
                {
                    string attribute = node.Name == "a" ? "href" : "src";
                    string url = HtmlEntity.DeEntitize(node.GetAttributeValue(attribute, ""));
                    bool anchor = node.Name == "a" && url.StartsWith("#", StringComparison.Ordinal);
                    bool valid = Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) &&
                        (uri.Scheme == "http" || uri.Scheme == "https" || (node.Name == "a" && uri.Scheme == "mailto"));
                    if (!anchor && !valid)
                    {
                        if (node.Name == "img")
                            node.ParentNode.ReplaceChild(document.CreateTextNode(
                                WebUtility.HtmlEncode(node.GetAttributeValue("alt", "[image]"))), node);
                        else
                            node.Attributes.Remove(attribute);
                    }
                }
                if (node.Name == "input")
                {
                    string check = node.Attributes.Contains("checked") ? "&#9745; " : "&#9744; ";
                    node.ParentNode.ReplaceChild(document.CreateTextNode(check), node);
                }
                string? style = StyleFor(node.Name);
                if (style != null)
                    node.SetAttributeValue("style", style + node.GetAttributeValue("style", ""));
            }
            string html = document.DocumentNode.InnerHtml;
            return new ConvertedContent(markdown, html, RtfConverter.Convert(document.DocumentNode));
        }

        private static string? StyleFor(string tag)
        {
            switch (tag)
            {
                case "table": return "border-collapse:collapse;margin:8px 0;";
                case "th": return "border:1px solid #b8b8b8;padding:6px 10px;background-color:#f1f1f1;text-align:left;";
                case "td": return "border:1px solid #b8b8b8;padding:6px 10px;";
                case "blockquote": return "border-left:3px solid #b8b8b8;margin:8px 0;padding-left:12px;color:#555555;";
                case "pre": return "font-family:Consolas,monospace;background-color:#f3f3f3;padding:10px;white-space:pre-wrap;";
                case "code": return "font-family:Consolas,monospace;background-color:#f3f3f3;";
                case "p": return "margin:0 0 8px 0;";
                case "h1": return "font-size:24pt;font-weight:bold;margin:12px 0 6px 0;";
                case "h2": return "font-size:20pt;font-weight:bold;margin:12px 0 6px 0;";
                case "h3": return "font-size:16pt;font-weight:bold;margin:10px 0 6px 0;";
                case "h4": return "font-size:14pt;font-weight:bold;margin:8px 0 4px 0;";
                case "h5": return "font-size:12pt;font-weight:bold;margin:8px 0 4px 0;";
                case "h6": return "font-size:11pt;font-weight:bold;margin:8px 0 4px 0;";
                default: return null;
            }
        }

        internal static string BuildClipboardHtml(string fragment)
        {
            const string template = "Version:1.0\r\nStartHTML:{0:D10}\r\nEndHTML:{1:D10}\r\nStartFragment:{2:D10}\r\nEndFragment:{3:D10}\r\n";
            const string prefix = "<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body><!--StartFragment-->";
            const string suffix = "<!--EndFragment--></body></html>";
            int startHtml = Encoding.UTF8.GetByteCount(string.Format(CultureInfo.InvariantCulture, template, 0, 0, 0, 0));
            int startFragment = startHtml + Encoding.UTF8.GetByteCount(prefix);
            int endFragment = startFragment + Encoding.UTF8.GetByteCount(fragment);
            int endHtml = endFragment + Encoding.UTF8.GetByteCount(suffix);
            return string.Format(CultureInfo.InvariantCulture, template, startHtml, endHtml, startFragment, endFragment) +
                   prefix + fragment + suffix;
        }
    }
}

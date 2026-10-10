using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace MDPaste
{
    internal sealed class RtfConverter
    {
        private readonly StringBuilder output = new StringBuilder();
        private int listDepth;
        private int quoteDepth;
        private bool inCell;

        internal static string Convert(HtmlNode root)
        {
            var renderer = new RtfConverter();
            renderer.output.Append(@"{\rtf1\ansi\ansicpg1252\deff0\uc1{\fonttbl{\f0 Calibri;}{\f1 Consolas;}}");
            renderer.output.Append(@"{\colortbl;\red0\green0\blue0;\red85\green85\blue85;\red0\green102\blue204;}\viewkind4\pard\f0\fs22 ");
            renderer.Children(root);
            renderer.output.Append('}');
            return renderer.output.ToString();
        }

        private void Children(HtmlNode node)
        {
            foreach (var child in node.ChildNodes)
                Render(child);
        }

        private void Render(HtmlNode node)
        {
            if (node.NodeType == HtmlNodeType.Text)
            {
                string text = HtmlEntity.DeEntitize(node.InnerText);
                // Ignore structural whitespace between HTML blocks, not spaces inside inline content.
                if (!string.IsNullOrWhiteSpace(text) || (node.ParentNode.Name != "#document" &&
                    node.ParentNode.Name != "ul" && node.ParentNode.Name != "ol" &&
                    node.ParentNode.Name != "table" && node.ParentNode.Name != "thead" &&
                    node.ParentNode.Name != "tbody" && node.ParentNode.Name != "tr"))
                {
                    string normalized = node.Ancestors("pre").Any() ? text : Regex.Replace(text, @"\s+", " ");
                    if (node.PreviousSibling?.Name == "br")
                        normalized = normalized.TrimStart();
                    Text(normalized);
                }
                return;
            }
            if (node.NodeType != HtmlNodeType.Element && node.NodeType != HtmlNodeType.Document)
                return;
            switch (node.Name)
            {
                case "strong": case "b": Group(@"\b ", node); break;
                case "em": case "i": Group(@"\i ", node); break;
                case "del": case "s": Group(@"\strike ", node); break;
                case "u": case "ins": Group(@"\ul ", node); break;
                case "sub": Group(@"\sub ", node); break;
                case "sup": Group(@"\super ", node); break;
                case "code": Group(@"\f1 ", node); break;
                case "pre":
                    Paragraph(); Group(@"\f1\fs20 ", node); output.Append(@"\par "); break;
                case "p":
                    Paragraph(); Children(node); output.Append(@"\par "); break;
                case "h1": case "h2": case "h3": case "h4": case "h5": case "h6":
                    Paragraph();
                    int[] sizes = { 48, 40, 32, 28, 24, 22 };
                    Group(@"\b\fs" + sizes[node.Name[1] - '1'] + " ", node);
                    output.Append(@"\par "); break;
                case "br": output.Append(@"\line "); break;
                case "hr":
                    Paragraph(); output.Append(@"\brdrb\brdrs\brdrw10\brsp20 \par "); break;
                case "blockquote":
                    quoteDepth++;
                    output.Append(@"{\cf2 "); Children(node); output.Append('}');
                    quoteDepth--; break;
                case "ul": case "ol":
                    listDepth++; Children(node); listDepth--; break;
                case "li":
                    Paragraph();
                    output.Append(@"\li" + ((listDepth + quoteDepth) * 360) + @"\fi-240 ");
                    if (node.ParentNode.Name == "ol")
                    {
                        int start = node.ParentNode.GetAttributeValue("start", 1);
                        int index = node.ParentNode.Elements("li").TakeWhile(n => n != node).Count();
                        Text((start + index).ToString(CultureInfo.InvariantCulture) + ". ");
                    }
                    else
                        Text("\u2022 ");
                    bool firstContent = true;
                    foreach (var child in node.ChildNodes)
                    {
                        if (firstContent && child.Name == "p")
                            Children(child);
                        else
                            Render(child);
                        if (child.NodeType == HtmlNodeType.Element || !string.IsNullOrWhiteSpace(child.InnerText))
                            firstContent = false;
                    }
                    output.Append(@"\par "); break;
                case "a":
                    string url = HtmlEntity.DeEntitize(node.GetAttributeValue("href", ""));
                    if (url.Length == 0)
                        Children(node);
                    else
                    {
                        output.Append(@"{\field{\*\fldinst HYPERLINK ");
                        output.Append('"');
                        Text(url.Replace("\"", "%22"));
                        output.Append("\"}{\\fldrslt{\\ul\\cf3 ");
                        Children(node); output.Append("}}}");
                    }
                    break;
                case "img":
                    Text("[Image: " + HtmlEntity.DeEntitize(node.GetAttributeValue("alt", "image")) + "]"); break;
                case "table": Table(node); break;
                case "dt":
                    Paragraph(); Group(@"\b ", node); output.Append(@"\par "); break;
                case "dd":
                    Paragraph(); output.Append(@"\li360 "); Children(node); output.Append(@"\par "); break;
                default: Children(node); break;
            }
        }

        private void Paragraph()
        {
            output.Append(@"\pard");
            if (inCell)
                output.Append(@"\intbl");
            if (quoteDepth + listDepth > 0)
                output.Append(@"\li").Append((quoteDepth + listDepth) * 360);
            output.Append(@"\sa100 ");
        }

        private void Group(string controls, HtmlNode node)
        {
            output.Append('{').Append(controls);
            Children(node);
            output.Append('}');
        }

        private void Table(HtmlNode table)
        {
            if (inCell)
            {
                Text(HtmlEntity.DeEntitize(table.InnerText));
                return;
            }
            foreach (var row in table.Descendants("tr").Where(n => n.Ancestors("table").First() == table))
            {
                var cells = row.ChildNodes.Where(n => n.Name == "td" || n.Name == "th").ToArray();
                if (cells.Length == 0)
                    continue;
                output.Append(@"\trowd\trgaph100\trleft0");
                if (cells.All(n => n.Name == "th"))
                    output.Append(@"\trhdr");
                for (int i = 0; i < cells.Length; i++)
                {
                    output.Append(@"\clbrdrt\brdrs\brdrw10\clbrdrl\brdrs\brdrw10\clbrdrb\brdrs\brdrw10\clbrdrr\brdrs\brdrw10");
                    output.Append(@"\cellx").Append((i + 1) * 9360 / cells.Length);
                }
                output.Append(' ');
                inCell = true;
                foreach (var cell in cells)
                {
                    Paragraph();
                    string style = cell.GetAttributeValue("style", "");
                    if (Regex.IsMatch(style, @"text-align:\s*right", RegexOptions.IgnoreCase))
                        output.Append(@"\qr ");
                    else if (Regex.IsMatch(style, @"text-align:\s*center", RegexOptions.IgnoreCase))
                        output.Append(@"\qc ");
                    if (cell.Name == "th")
                        Group(@"\b ", cell);
                    else
                        Children(cell);
                    output.Append(@"\cell ");
                }
                inCell = false;
                output.Append(@"\row\pard ");
            }
            output.Append(@"\par ");
        }

        private void Text(string text)
        {
            foreach (char character in text)
            {
                switch (character)
                {
                    case '\\': case '{': case '}': output.Append('\\').Append(character); break;
                    case '\r': break;
                    case '\n': output.Append(@"\line "); break;
                    case '\t': output.Append(@"\tab "); break;
                    default:
                        if (character >= 32 && character <= 126)
                            output.Append(character);
                        else
                            output.Append(@"\u").Append(unchecked((short)character).ToString(CultureInfo.InvariantCulture)).Append('?');
                        break;
                }
            }
        }
    }
}

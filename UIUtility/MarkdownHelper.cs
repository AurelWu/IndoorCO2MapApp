using System.Text;
using System.Text.RegularExpressions;

namespace IndoorCO2MapAppV2.UIUtility
{
    /// <summary>
    /// Converts a small subset of Markdown to a styled HTML document.
    /// Supports: # headings, **bold**, *italic*, `code`, - bullet lists, blank-line paragraphs.
    /// </summary>
    public static partial class MarkdownHelper
    {
        [GeneratedRegex(@"\*\*(.+?)\*\*")]
        private static partial Regex BoldRegex();

        [GeneratedRegex(@"\*(.+?)\*")]
        private static partial Regex ItalicRegex();

        [GeneratedRegex(@"`(.+?)`")]
        private static partial Regex CodeRegex();

        [GeneratedRegex(@"!\[([^\]]*)\]\(([^)]+)\)")]
        private static partial Regex ImageRegex();

        [GeneratedRegex(@"\[([^\]]+)\]\(([^)]+)\)")]
        private static partial Regex LinkRegex();

        public static string ToHtml(string markdown)
        {
            var lines = markdown.ReplaceLineEndings("\n").Split('\n');
            var body = new StringBuilder();
            bool inList = false;

            foreach (var rawLine in lines)
            {
                var line = rawLine.TrimEnd();

                if (line.StartsWith("### "))
                {
                    CloseList(body, ref inList);
                    body.AppendLine($"<h3>{Inline(line[4..])}</h3>");
                }
                else if (line.StartsWith("## "))
                {
                    CloseList(body, ref inList);
                    body.AppendLine($"<h2>{Inline(line[3..])}</h2>");
                }
                else if (line.StartsWith("# "))
                {
                    CloseList(body, ref inList);
                    body.AppendLine($"<h1>{Inline(line[2..])}</h1>");
                }
                else if (line.StartsWith("- ") || line.StartsWith("* "))
                {
                    if (!inList) { body.AppendLine("<ul>"); inList = true; }
                    body.AppendLine($"<li>{Inline(line[2..])}</li>");
                }
                else if (line == "---" || line == "***" || line == "___")
                {
                    CloseList(body, ref inList);
                    body.AppendLine("<hr/>");
                }
                else if (string.IsNullOrWhiteSpace(line))
                {
                    CloseList(body, ref inList);
                }
                else
                {
                    CloseList(body, ref inList);
                    body.AppendLine($"<p>{Inline(line)}</p>");
                }
            }

            CloseList(body, ref inList);
            return Wrap(body.ToString());
        }

        private static void CloseList(StringBuilder sb, ref bool inList)
        {
            if (inList) { sb.AppendLine("</ul>"); inList = false; }
        }

        private static string Inline(string text)
        {
            text = ImageRegex().Replace(text, "<img src=\"$2\" alt=\"$1\" style=\"max-width:100%;height:auto;\">");
            text = LinkRegex().Replace(text, "<a href=\"$2\">$1</a>");
            text = BoldRegex().Replace(text, "<strong>$1</strong>");
            text = ItalicRegex().Replace(text, "<em>$1</em>");
            text = CodeRegex().Replace(text, "<code>$1</code>");
            return text;
        }

        private static string Wrap(string body) => $$"""
            <!DOCTYPE html>
            <html>
            <head>
              <meta charset="UTF-8">
              <meta name="viewport" content="width=device-width, initial-scale=1.0">
              <style>
                body {
                  font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;
                  padding: 0 4px;
                  margin: 0;
                  font-size: 15px;
                  line-height: 1.6;
                  color: #1a1a1a;
                }
                h1 { font-size: 22px; margin: 14px 0 6px; }
                h2 { font-size: 18px; margin: 14px 0 4px; }
                h3 { font-size: 15px; margin: 10px 0 2px; font-weight: 600; }
                p  { margin: 4px 0 8px; }
                ul { padding-left: 20px; margin: 4px 0 8px; }
                li { margin: 3px 0; }
                code {
                  background: #f0f0f0;
                  padding: 1px 5px;
                  border-radius: 3px;
                  font-size: 13px;
                  font-family: monospace;
                }
                strong { font-weight: 600; }
                hr { border: none; border-top: 1px solid #ccc; margin: 16px 0; }
                a { color: #512BD4; }
                img { display: block; margin: 8px 0; border-radius: 6px; }
                @media (prefers-color-scheme: dark) {
                  body  { color: #e8e8e8; background: #1a1a1a; }
                  code  { background: #2a2a2a; }
                  a     { color: #ac99ea; }
                  hr    { border-top-color: #444; }
                }
              </style>
            </head>
            <body>{{body}}</body>
            </html>
            """;
    }
}

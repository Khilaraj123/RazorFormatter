using System;
using System.Collections.Generic;
using System.Text;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Core.Formatting
{
    /// <summary>
    /// Formats embedded CSS (&lt;style&gt;) and JavaScript (&lt;script&gt;) blocks with Razor preservation.
    /// </summary>
    public sealed class EmbeddedLanguageFormatter
    {
        public static string FormatCss(
            string cssText,
            int baseIndentLevel,
            RazorFormatterOptions options,
            string eol)
        {
            if (string.IsNullOrWhiteSpace(cssText)) return cssText;

            var startsWithNewline = cssText.StartsWith("\r\n", StringComparison.Ordinal) ||
                                   cssText.StartsWith("\n", StringComparison.Ordinal);

            var lines = cssText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            if (startsWithNewline)
            {
                sb.Append(eol);
            }

            var blockDepth = 0;
            var contentLines = new List<string>();

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length == 0)
                {
                    if (contentLines.Count > 0)
                        contentLines.Add(string.Empty);
                    continue;
                }

                var isCloser = trimmed.StartsWith("}", StringComparison.Ordinal);
                var currentLevel = baseIndentLevel + (isCloser ? Math.Max(0, blockDepth - 1) : blockDepth);
                var indent = options.BuildIndentString(currentLevel);

                contentLines.Add(indent + trimmed);

                blockDepth += CountNetBracesInCss(trimmed);
                if (blockDepth < 0) blockDepth = 0;
            }

            while (contentLines.Count > 0 && contentLines[^1].Length == 0)
                contentLines.RemoveAt(contentLines.Count - 1);

            for (var i = 0; i < contentLines.Count; i++)
            {
                sb.Append(contentLines[i]);
                sb.Append(eol);
            }

            return sb.ToString();
        }

        public static string FormatJavaScript(
            string jsText,
            int baseIndentLevel,
            RazorFormatterOptions options,
            string eol)
        {
            if (string.IsNullOrWhiteSpace(jsText)) return jsText;

            var startsWithNewline = jsText.StartsWith("\r\n", StringComparison.Ordinal) ||
                                   jsText.StartsWith("\n", StringComparison.Ordinal);

            var lines = jsText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var sb = new StringBuilder();
            if (startsWithNewline)
            {
                sb.Append(eol);
            }

            var blockDepth = 0;
            var contentLines = new List<string>();

            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.Length == 0)
                {
                    if (contentLines.Count > 0)
                        contentLines.Add(string.Empty);
                    continue;
                }

                var isCloser = trimmed.StartsWith("}", StringComparison.Ordinal) || trimmed.StartsWith("]", StringComparison.Ordinal);
                var currentLevel = baseIndentLevel + (isCloser ? Math.Max(0, blockDepth - 1) : blockDepth);
                var indent = options.BuildIndentString(currentLevel);

                contentLines.Add(indent + trimmed);

                blockDepth += CountNetBracesInJs(trimmed);
                if (blockDepth < 0) blockDepth = 0;
            }

            while (contentLines.Count > 0 && contentLines[^1].Length == 0)
                contentLines.RemoveAt(contentLines.Count - 1);

            for (var i = 0; i < contentLines.Count; i++)
            {
                sb.Append(contentLines[i]);
                sb.Append(eol);
            }

            return sb.ToString();
        }

        private static int CountNetBracesInCss(string line)
        {
            var net = 0;
            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];
                if (ch == '{') net++;
                else if (ch == '}') net--;
            }
            return net;
        }

        private static int CountNetBracesInJs(string line)
        {
            var net = 0;
            var inSingle = false;
            var inDouble = false;
            var inTemplate = false;

            for (var i = 0; i < line.Length; i++)
            {
                var ch = line[i];

                if (ch == '\\' && (inSingle || inDouble || inTemplate))
                {
                    i++; // skip escaped char
                    continue;
                }

                if (ch == '\'' && !inDouble && !inTemplate)
                {
                    inSingle = !inSingle;
                    continue;
                }
                if (ch == '"' && !inSingle && !inTemplate)
                {
                    inDouble = !inDouble;
                    continue;
                }
                if (ch == '`' && !inSingle && !inDouble)
                {
                    inTemplate = !inTemplate;
                    continue;
                }

                if (inSingle || inDouble) continue;

                // Single-line comment starts
                if (!inTemplate && ch == '/' && i + 1 < line.Length && line[i + 1] == '/')
                    break;

                if (ch == '{') net++;
                else if (ch == '}') net--;
            }
            return net;
        }
    }
}

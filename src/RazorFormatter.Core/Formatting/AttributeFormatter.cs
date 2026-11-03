using System;
using System.Collections.Generic;
using System.Text;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.internals;

namespace RazorFormatter.Core.Formatting
{
    public sealed class ParsedAttribute
    {
        public string Name { get; }
        public string? Value { get; }
        public string RawText { get; }

        public ParsedAttribute(string name, string? value, string rawText)
        {
            Name = name;
            Value = value;
            RawText = rawText;
        }

        public override string ToString() => RawText;
    }

    /// <summary>
    /// Formats HTML and TagHelper attributes according to configured layout strategies.
    /// </summary>
    public sealed class AttributeFormatter
    {
        public static string FormatTag(
            string tagText,
            int tagIndentLevel,
            RazorFormatterOptions options,
            string eol)
        {
            if (string.IsNullOrEmpty(tagText)) return tagText;

            // In Preserve mode, single-line tags are preserved untouched unless void slash policy needs to be applied
            var wasMultiline = tagText.Contains('\n');
            if (options.AttributeLayout == AttributeLayout.Preserve && !wasMultiline && options.VoidElementSlash == VoidElementSlashPolicy.Preserve)
                return tagText;

            // Closing tags and comments have no attributes
            var trimmed = tagText.TrimStart();
            if (trimmed.StartsWith("</", StringComparison.Ordinal) || trimmed.StartsWith("<!--", StringComparison.Ordinal))
                return tagText;

            if (!TryParseTag(tagText, out var tagPrefix, out var attributes, out var suffix))
                return tagText;

            var tagName = tagPrefix.TrimStart('<').Trim();
            var isVoid = HtmlVoidElements.IsVoid(tagName);
            if (isVoid)
            {
                if (options.VoidElementSlash == VoidElementSlashPolicy.Slash)
                    suffix = "/>";
                else if (options.VoidElementSlash == VoidElementSlashPolicy.NoSlash)
                    suffix = ">";
            }

            if (options.AttributeLayout == AttributeLayout.Preserve && !wasMultiline)
            {
                var originalSuffix = tagText.TrimEnd().EndsWith("/>", StringComparison.Ordinal) ? "/>" : ">";
                if (suffix == originalSuffix)
                    return tagText;
            }

            if (attributes.Count == 0)
            {
                return suffix == "/>" ? $"{tagPrefix} />" : $"{tagPrefix}>";
            }

            var layout = options.AttributeLayout;

            // Determine if attributes should be formatted multiline
            var shouldBeMultiline = layout switch
            {
                AttributeLayout.OnePerLine => attributes.Count > 1 || wasMultiline,
                AttributeLayout.SingleLine => false,
                AttributeLayout.Preserve => wasMultiline,
                _ => wasMultiline,
            };

            if (!shouldBeMultiline)
            {
                // Single-line format
                var sb = new StringBuilder();
                sb.Append(tagPrefix);
                for (var i = 0; i < attributes.Count; i++)
                {
                    sb.Append(' ');
                    sb.Append(attributes[i].RawText);
                }
                if (suffix == "/>")
                    sb.Append(" />");
                else
                    sb.Append('>');
                return sb.ToString();
            }

            // Multiline format (one attribute per line, indented +1 from tag)
            var attrIndent = options.BuildIndentString(tagIndentLevel + 1);
            var sbMulti = new StringBuilder();
            sbMulti.Append(tagPrefix);
            sbMulti.Append(eol);

            for (var i = 0; i < attributes.Count; i++)
            {
                sbMulti.Append(attrIndent);
                sbMulti.Append(attributes[i].RawText);

                if (i == attributes.Count - 1)
                {
                    // Last attribute: append tag suffix on the same line
                    if (suffix == "/>")
                        sbMulti.Append(" />");
                    else
                        sbMulti.Append('>');
                }
                else
                {
                    sbMulti.Append(eol);
                }
            }

            return sbMulti.ToString();
        }

        public static bool TryParseTag(
            string tagText,
            out string tagPrefix,
            out List<ParsedAttribute> attributes,
            out string suffix)
        {
            tagPrefix = string.Empty;
            attributes = new List<ParsedAttribute>();
            suffix = ">";

            if (string.IsNullOrWhiteSpace(tagText) || tagText[0] != '<')
                return false;

            var trimmed = tagText.Trim();
            if (trimmed.StartsWith("</", StringComparison.Ordinal) || trimmed.StartsWith("<!--", StringComparison.Ordinal))
                return false;

            var isSelfClosing = trimmed.EndsWith("/>", StringComparison.Ordinal);
            suffix = isSelfClosing ? "/>" : ">";

            // Tag prefix: from start of tag to end of tag name
            var p = 1;
            if (p < tagText.Length && tagText[p] == '!') p++;
            while (p < tagText.Length && (char.IsLetterOrDigit(tagText[p]) || tagText[p] == '-' || tagText[p] == ':' || tagText[p] == '_'))
                p++;

            tagPrefix = tagText.Substring(0, p).TrimStart();

            // Find end of attributes (before final '>' or '/>')
            var end = tagText.Length - 1;
            while (end > p && char.IsWhiteSpace(tagText[end])) end--;
            if (end > p && tagText[end] == '>') end--;
            if (isSelfClosing && end > p && tagText[end] == '/') end--;
            while (end > p && char.IsWhiteSpace(tagText[end])) end--;
            end++; // exclusive end of attributes

            while (p < end)
            {
                while (p < end && char.IsWhiteSpace(tagText[p]))
                    p++;

                if (p >= end) break;

                var attrStart = p;

                // Attribute name: up to '=', whitespace, '>', or '/'
                while (p < end && !char.IsWhiteSpace(tagText[p]) && tagText[p] != '=' && tagText[p] != '>' && tagText[p] != '/')
                    p++;

                var attrName = tagText.Substring(attrStart, p - attrStart);
                string? attrVal = null;

                // Skip whitespace between name and '='
                var save = p;
                while (p < end && char.IsWhiteSpace(tagText[p]))
                    p++;

                if (p < end && tagText[p] == '=')
                {
                    p++; // consume '='
                    while (p < end && char.IsWhiteSpace(tagText[p]))
                        p++;

                    if (p < end)
                    {
                        var valStart = p;
                        var quoteChar = tagText[p];
                        if (quoteChar == '"' || quoteChar == '\'')
                        {
                            p++; // consume opening quote
                            while (p < end)
                            {
                                var ch = tagText[p];
                                if (ch == '\\')
                                {
                                    p = Math.Min(end, p + 2);
                                    continue;
                                }
                                if (ch == '@' && p + 1 < end && tagText[p + 1] == '(')
                                {
                                    p += 2;
                                    SkipParens(tagText, ref p, end);
                                    continue;
                                }
                                if (ch == quoteChar)
                                {
                                    p++; // consume closing quote
                                    break;
                                }
                                p++;
                            }
                        }
                        else if (quoteChar == '@' && p + 1 < end && tagText[p + 1] == '(')
                        {
                            p += 2;
                            SkipParens(tagText, ref p, end);
                        }
                        else
                        {
                            while (p < end && !char.IsWhiteSpace(tagText[p]) && tagText[p] != '>' && tagText[p] != '/')
                                p++;
                        }
                        attrVal = tagText.Substring(valStart, p - valStart);
                    }
                }
                else
                {
                    // Boolean attribute without '='
                    p = save;
                }

                if (p <= attrStart)
                {
                    // Guarantee forward progress
                    p = attrStart + 1;
                }

                var rawAttr = tagText.Substring(attrStart, p - attrStart).Trim();
                if (rawAttr.Length > 0)
                {
                    attributes.Add(new ParsedAttribute(attrName, attrVal, rawAttr));
                }
            }

            return true;
        }

        private static void SkipParens(string text, ref int offset, int limit)
        {
            var depth = 1;
            while (offset < limit && depth > 0)
            {
                var ch = text[offset];
                if (ch == '"' || ch == '\'')
                {
                    var q = ch;
                    offset++;
                    while (offset < limit)
                    {
                        if (text[offset] == '\\') { offset = Math.Min(limit, offset + 2); continue; }
                        if (text[offset] == q) { offset++; break; }
                        offset++;
                    }
                    continue;
                }
                if (ch == '(') depth++;
                else if (ch == ')')
                {
                    depth--;
                    if (depth == 0) { offset++; break; }
                }
                offset++;
            }
        }
    }
}

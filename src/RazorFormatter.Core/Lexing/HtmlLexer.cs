using System;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.internals;

namespace RazorFormatter.Core.Lexing
{
    /// <summary>
    /// Specialized scanner for HTML tags, comments, attributes, and embedded markup.
    /// </summary>
    public sealed class HtmlLexer
    {
        private readonly SourceText _source;
        private readonly string _text;
        private readonly CSharpLexer _csharpLexer;

        public HtmlLexer(SourceText source, CSharpLexer csharpLexer)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _text = source.GetText();
            _csharpLexer = csharpLexer ?? throw new ArgumentNullException(nameof(csharpLexer));
        }

        public bool CanStartTag(int offset)
        {
            if (offset >= _text.Length || _text[offset] != '<') return false;
            if (offset + 1 >= _text.Length) return false;
            var next = _text[offset + 1];
            return next == '!' || next == '/' || next == '?' || char.IsAsciiLetter(next);
        }

        public bool TryScanComment(ref int offset, out int start, out int end)
        {
            start = offset;
            end = offset;
            if (!StartsWith(offset, "<!--")) return false;

            var idx = _text.IndexOf("-->", offset + 4, StringComparison.Ordinal);
            if (idx >= 0)
            {
                end = idx + 3;
            }
            else
            {
                end = _text.Length;
            }
            offset = end;
            return true;
        }

        public bool TryScanTag(
            ref int offset,
            out int start,
            out int end,
            out RazorSyntaxKind kind,
            out HtmlTagInfo info)
        {
            start = offset;
            end = offset;
            kind = RazorSyntaxKind.HtmlTag;
            info = default;

            if (offset >= _text.Length || _text[offset] != '<')
                return false;

            // HTML comment <!-- ... -->
            if (StartsWith(offset, "<!--"))
            {
                TryScanComment(ref offset, out start, out end);
                kind = RazorSyntaxKind.HtmlComment;
                return true;
            }

            offset++; // consume '<'

            var isClosing = offset < _text.Length && _text[offset] == '/';
            if (isClosing) offset++;

            // Tag name (can start with '!' for doctype)
            var nameStart = offset;
            if (offset < _text.Length && _text[offset] == '!') offset++;
            while (offset < _text.Length && (char.IsLetterOrDigit(_text[offset]) || _text[offset] == '-' || _text[offset] == ':' || _text[offset] == '_'))
                offset++;
            var name = _text.Substring(nameStart, offset - nameStart);

            // Scan attributes (respecting quotes, escape characters, and Razor @(...) expressions)
            while (offset < _text.Length)
            {
                var ch = _text[offset];
                if (ch == '>')
                {
                    offset++;
                    break;
                }

                // Quoted attribute value
                if (ch == '\'' || ch == '"')
                {
                    var quote = ch;
                    offset++;
                    while (offset < _text.Length)
                    {
                        var qch = _text[offset];
                        if (qch == '\\')
                        {
                            offset = Math.Min(_text.Length, offset + 2);
                            continue;
                        }
                        if (qch == '@')
                        {
                            if (offset + 1 < _text.Length && _text[offset + 1] == '(')
                            {
                                offset++; // skip '@'
                                _csharpLexer.SkipBalancedParens(ref offset);
                                continue;
                            }
                            if (offset + 1 < _text.Length && _text[offset + 1] == '@')
                            {
                                offset = Math.Min(_text.Length, offset + 2); // skip escaped @@
                                continue;
                            }
                        }
                        if (qch == quote)
                        {
                            offset++;
                            break;
                        }
                        offset++;
                    }
                    continue;
                }

                // Razor expression in unquoted attribute value: @(...)
                if (ch == '@' && offset + 1 < _text.Length && _text[offset + 1] == '(')
                {
                    offset++; // skip '@'
                    _csharpLexer.SkipBalancedParens(ref offset);
                    continue;
                }

                offset++;
            }

            end = Math.Min(_text.Length, offset);

            var voidElement = !isClosing && !string.IsNullOrEmpty(name) && HtmlVoidElements.IsVoid(name);
            var selfClosing = voidElement;
            if (!selfClosing && end >= 2 && end <= _text.Length && _text[end - 1] == '>')
            {
                var p = end - 2;
                while (p >= start && char.IsWhiteSpace(_text[p])) p--;
                if (p >= start && _text[p] == '/')
                    selfClosing = true;
            }

            if (!isClosing && !string.IsNullOrEmpty(name))
            {
                var firstChar = name[0];
                var looksLikeComponent = (firstChar >= 'A' && firstChar <= 'Z') || name.Contains('.') || name.Contains(':');
                if (looksLikeComponent)
                    kind = RazorSyntaxKind.TagHelper;
            }

            info = new HtmlTagInfo(
                isOpening: !isClosing && !selfClosing && !voidElement && !string.IsNullOrEmpty(name),
                isClosing: isClosing,
                isSelfClosing: selfClosing || voidElement,
                isVoidElement: voidElement,
                name: name);

            return true;
        }

        public void ScanEmbeddedUntilClose(ref int offset, string closeTag, out int start, out int end)
        {
            start = offset;
            var idx = _text.IndexOf(closeTag, start, StringComparison.OrdinalIgnoreCase);
            end = idx < 0 ? _text.Length : idx;
            offset = end;
        }

        private bool StartsWith(int offset, string s)
        {
            if (offset + s.Length > _text.Length) return false;
            for (var i = 0; i < s.Length; i++)
            {
                if (_text[offset + i] != s[i]) return false;
            }
            return true;
        }
    }
}


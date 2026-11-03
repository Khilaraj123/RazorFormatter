using System;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Core.Lexing
{
    /// <summary>
    /// Specialized scanner for C# expressions, statements, strings, comments, and delimiters.
    /// </summary>
    public sealed class CSharpLexer
    {
        private readonly SourceText _source;
        private readonly string _text;

        public CSharpLexer(SourceText source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _text = source.GetText();
        }

        public bool TryScanComment(ref int offset, out int start, out int end, out bool isBlockComment)
        {
            start = offset;
            end = offset;
            isBlockComment = false;

            if (offset + 1 >= _text.Length || _text[offset] != '/')
                return false;

            var next = _text[offset + 1];

            // Single line comment // ...
            if (next == '/')
            {
                offset += 2;
                while (offset < _text.Length && _text[offset] != '\r' && _text[offset] != '\n')
                    offset++;
                end = offset;
                return true;
            }

            // Multi-line block comment /* ... */
            if (next == '*')
            {
                isBlockComment = true;
                offset += 2;
                while (offset + 1 < _text.Length && !(_text[offset] == '*' && _text[offset + 1] == '/'))
                    offset++;
                if (offset + 1 < _text.Length) offset += 2;
                else offset = _text.Length;
                end = offset;
                return true;
            }

            return false;
        }

        public bool TryScanStringOrCharLiteral(ref int offset, out int start, out int end)
        {
            start = offset;
            end = offset;

            if (offset >= _text.Length) return false;

            var p = offset;
            var isInterpolated = false;
            var isVerbatim = false;

            if (_text[p] == '$')
            {
                isInterpolated = true;
                p++;
                if (p < _text.Length && _text[p] == '@') { isVerbatim = true; p++; }
            }
            else if (_text[p] == '@')
            {
                isVerbatim = true;
                p++;
                if (p < _text.Length && _text[p] == '$') { isInterpolated = true; p++; }
            }

            if (p >= _text.Length) return false;

            // Character literal '...'
            if (!isInterpolated && !isVerbatim && _text[p] == '\'')
            {
                p++;
                while (p < _text.Length)
                {
                    if (_text[p] == '\\') { p += 2; continue; }
                    if (_text[p] == '\'') { p++; offset = p; end = p; return true; }
                    if (_text[p] == '\r' || _text[p] == '\n') break;
                    p++;
                }
                offset = p;
                end = p;
                return true;
            }

            if (_text[p] != '"') return false;

            // Raw string literal: 3 or more quotes
            var quoteCount = 0;
            while (p + quoteCount < _text.Length && _text[p + quoteCount] == '"')
                quoteCount++;

            if (quoteCount >= 3)
            {
                p += quoteCount;
                var closingQuotes = 0;
                while (p < _text.Length)
                {
                    if (_text[p] == '"')
                    {
                        closingQuotes++;
                        if (closingQuotes == quoteCount)
                        {
                            p++;
                            offset = p;
                            end = p;
                            return true;
                        }
                    }
                    else
                    {
                        closingQuotes = 0;
                    }
                    p++;
                }
                offset = p;
                end = p;
                return true;
            }

            // Verbatim string @"..." or $@"...": ends at unescaped '"' where '""' is an escaped quote
            if (isVerbatim)
            {
                p++; // skip opening '"'
                while (p < _text.Length)
                {
                    if (_text[p] == '"')
                    {
                        if (p + 1 < _text.Length && _text[p + 1] == '"')
                        {
                            p += 2; // consume ""
                            continue;
                        }
                        p++; // consume closing "
                        offset = p;
                        end = p;
                        return true;
                    }
                    p++;
                }
                offset = p;
                end = p;
                return true;
            }

            // Interpolated regular string $"..."
            if (isInterpolated)
            {
                p++; // skip opening '"'
                while (p < _text.Length)
                {
                    var ch = _text[p];
                    if (ch == '\\') { p += 2; continue; }
                    if (ch == '{')
                    {
                        if (p + 1 < _text.Length && _text[p + 1] == '{')
                        {
                            p += 2; // escaped {{
                            continue;
                        }
                        // Interpolation hole
                        p++;
                        var holeDepth = 1;
                        while (p < _text.Length && holeDepth > 0)
                        {
                            if (_text[p] == '{') holeDepth++;
                            else if (_text[p] == '}') { holeDepth--; if (holeDepth == 0) { p++; break; } }
                            else if (_text[p] == '"' || _text[p] == '\'')
                            {
                                var q = _text[p++];
                                while (p < _text.Length && _text[p] != q)
                                {
                                    if (_text[p] == '\\') p += 2;
                                    else p++;
                                }
                                if (p < _text.Length) p++;
                                continue;
                            }
                            p++;
                        }
                        continue;
                    }
                    if (ch == '"') { p++; offset = p; end = p; return true; }
                    if (ch == '\r' || ch == '\n') break;
                    p++;
                }
                offset = p;
                end = p;
                return true;
            }

            // Regular string literal "..."
            p++; // skip opening '"'
            while (p < _text.Length)
            {
                if (_text[p] == '\\') { p += 2; continue; }
                if (_text[p] == '"') { p++; offset = p; end = p; return true; }
                if (_text[p] == '\r' || _text[p] == '\n') break;
                p++;
            }
            offset = p;
            end = p;
            return true;
        }

        public void SkipBalancedParens(ref int offset)
        {
            if (offset >= _text.Length || _text[offset] != '(') return;
            offset++; // consume '('
            var depth = 1;
            while (offset < _text.Length && depth > 0)
            {
                var ch = _text[offset];

                // C# comments
                if (ch == '/' && TryScanComment(ref offset, out _, out _, out _))
                    continue;

                // Razor comment @* ... *@
                if (ch == '@' && offset + 1 < _text.Length && _text[offset + 1] == '*')
                {
                    offset += 2;
                    while (offset + 1 < _text.Length && !(_text[offset] == '*' && _text[offset + 1] == '@'))
                        offset++;
                    if (offset + 1 < _text.Length) offset += 2;
                    else offset = _text.Length;
                    continue;
                }

                // Strings and characters
                if (ch == '"' || ch == '\'' || ch == '$' || (ch == '@' && offset + 1 < _text.Length && (_text[offset + 1] == '"' || _text[offset + 1] == '$')))
                {
                    if (TryScanStringOrCharLiteral(ref offset, out _, out _))
                        continue;
                }

                if (ch == '(') depth++;
                else if (ch == ')')
                {
                    depth--;
                    if (depth == 0)
                    {
                        offset++;
                        break;
                    }
                }
                offset++;
            }
        }

        public void SkipBalancedBrackets(ref int offset)
        {
            if (offset >= _text.Length || _text[offset] != '[') return;
            offset++; // consume '['
            var depth = 1;
            while (offset < _text.Length && depth > 0)
            {
                var ch = _text[offset];
                if (ch == '\'' || ch == '"')
                {
                    if (TryScanStringOrCharLiteral(ref offset, out _, out _))
                        continue;
                }
                if (ch == '[') depth++;
                else if (ch == ']')
                {
                    depth--;
                    if (depth == 0)
                    {
                        offset++;
                        break;
                    }
                }
                offset++;
            }
        }

        public void ScanImplicitExpressionTail(ref int offset)
        {
            while (offset < _text.Length)
            {
                var ch = _text[offset];
                if (ch == '(') { SkipBalancedParens(ref offset); continue; }
                if (ch == '[') { SkipBalancedBrackets(ref offset); continue; }
                if (ch == '.' || ch == '?' || ch == ']' || char.IsLetterOrDigit(ch) || ch == '_')
                {
                    offset++;
                    continue;
                }
                break;
            }
        }
    }
}


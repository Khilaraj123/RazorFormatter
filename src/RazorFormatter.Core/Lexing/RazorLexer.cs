using System;
using System.Collections.Generic;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.internals;

namespace RazorFormatter.Core.Lexing
{
    /// <summary>
    /// Coordinated Razor lexer facade orchestrating transitions between Html, C#, Razor, comments, CSS, and JavaScript.
    /// </summary>
    public sealed class RazorLexer
    {
        private enum PendingBlockType
        {
            None,
            ControlFlow,
            CSharpBlock,
        }

        private readonly SourceText _source;
        private readonly string _text;
        private readonly HtmlLexer _htmlLexer;
        private readonly CSharpLexer _csharpLexer;

        private readonly List<RazorToken> _tokens = new(64);
        private readonly Dictionary<int, HtmlTagInfo> _tagInfoByToken = new();
        private readonly Dictionary<int, RazorBlockInfo> _blockInfoByToken = new();
        private readonly List<RazorDiagnostic> _diagnostics = new();

        private readonly Stack<LexicalContext> _contextStack = new();
        private int _offset;
        private PendingBlockType _pendingBlockType = PendingBlockType.None;
        private bool _lastClosedBlockWasControlFlow;
        private int _lastEmittedLine;

        public RazorLexer(SourceText source)
        {
            _source = source ?? throw new ArgumentNullException(nameof(source));
            _text = source.GetText();
            _csharpLexer = new CSharpLexer(source);
            _htmlLexer = new HtmlLexer(source, _csharpLexer);
            _contextStack.Push(new LexicalContext(LexicalState.Html, 0));
        }

        public IReadOnlyList<RazorDiagnostic> Diagnostics => _diagnostics;

        public bool TryGetTagInfo(int tokenIndex, out HtmlTagInfo info) => _tagInfoByToken.TryGetValue(tokenIndex, out info);

        public bool TryGetBlockInfo(int tokenIndex, out RazorBlockInfo info) => _blockInfoByToken.TryGetValue(tokenIndex, out info);

        public IReadOnlyList<RazorToken> Lex()
        {
            while (_offset < _text.Length)
            {
                var context = _contextStack.Peek();
                switch (context.State)
                {
                    case LexicalState.Html:
                        LexHtmlContext(context);
                        break;

                    case LexicalState.CSharp:
                        LexCSharpContext(context);
                        break;

                    case LexicalState.JavaScript:
                        LexJavaScriptContext(context);
                        break;

                    case LexicalState.Css:
                        LexCssContext(context);
                        break;

                    default:
                        LexHtmlContext(context);
                        break;
                }
            }
            return _tokens;
        }

        private void LexHtmlContext(LexicalContext context)
        {
            var ch = _text[_offset];

            if (ch == '\r' || ch == '\n')
            {
                if (context.IsSingleLineTransition)
                {
                    _contextStack.Pop();
                }
                Emit(RazorSyntaxKind.Newline, 1, LexicalState.Html);
                return;
            }

            if (char.IsWhiteSpace(ch))
            {
                LexWhitespace(LexicalState.Html);
                return;
            }

            if (ch == '@')
            {
                LexAtSign();
                return;
            }

            if (ch == '<' && _htmlLexer.CanStartTag(_offset))
            {
                _lastClosedBlockWasControlFlow = false;
                if (_htmlLexer.TryScanTag(ref _offset, out var tagStart, out var tagEnd, out var kind, out var info))
                {
                    _tagInfoByToken[_tokens.Count] = info;
                    var state = kind == RazorSyntaxKind.HtmlComment ? LexicalState.HtmlComment : LexicalState.Html;
                    Emit(kind, tagStart, tagEnd, state);

                    if (kind == RazorSyntaxKind.HtmlTag && !info.IsClosing && !info.IsSelfClosing && !info.IsVoidElement)
                    {
                        if (string.Equals(info.Name, "script", StringComparison.OrdinalIgnoreCase))
                        {
                            _contextStack.Push(new LexicalContext(LexicalState.JavaScript, _offset, tagName: "script"));
                            return;
                        }
                        if (string.Equals(info.Name, "style", StringComparison.OrdinalIgnoreCase))
                        {
                            _contextStack.Push(new LexicalContext(LexicalState.Css, _offset, tagName: "style"));
                            return;
                        }
                        context.HtmlElementDepth++;
                    }
                    else if (info.IsClosing)
                    {
                        if (context.HtmlElementDepth > 0)
                        {
                            context.HtmlElementDepth--;
                        }

                        // If in inline markup context inside C# code block, and depth returned to 0, pop back to C#
                        if (_contextStack.Count > 1 && context.HtmlElementDepth == 0 &&
                            _contextStack.ToArray()[1].State == LexicalState.CSharp)
                        {
                            _contextStack.Pop();
                        }
                    }
                    return;
                }
            }

            // Parenthesized condition following control flow keyword: @if (...) or @foreach (...)
            if (ch == '(' && _pendingBlockType == PendingBlockType.ControlFlow)
            {
                var condStart = _offset;
                _csharpLexer.SkipBalancedParens(ref _offset);
                Emit(RazorSyntaxKind.Html, condStart, _offset, LexicalState.CSharp);
                return;
            }

            // '{' in HTML context
            if (ch == '{')
            {
                if (_pendingBlockType == PendingBlockType.ControlFlow)
                {
                    _pendingBlockType = PendingBlockType.None;
                    _lastClosedBlockWasControlFlow = false;
                    _blockInfoByToken[_tokens.Count] = new RazorBlockInfo(RazorBlockKind.ControlFlow);
                    Emit(RazorSyntaxKind.RazorBlockOpen, 1, LexicalState.Razor);
                    _contextStack.Push(new LexicalContext(LexicalState.Html, _offset, braceDepth: 1));
                    return;
                }

                if (_pendingBlockType == PendingBlockType.CSharpBlock)
                {
                    _pendingBlockType = PendingBlockType.None;
                    _lastClosedBlockWasControlFlow = false;
                    _blockInfoByToken[_tokens.Count] = new RazorBlockInfo(RazorBlockKind.CSharpCodeBlock);
                    Emit(RazorSyntaxKind.RazorBlockOpen, 1, LexicalState.Razor);
                    _contextStack.Push(new LexicalContext(LexicalState.CSharp, _offset, braceDepth: 1));
                    return;
                }

                // Bare '{' in HTML markup: literal content
                _lastClosedBlockWasControlFlow = false;
                Emit(RazorSyntaxKind.Html, 1, LexicalState.Html);
                return;
            }

            // '}' in HTML context
            if (ch == '}')
            {
                if (context.HtmlElementDepth == 0 && context.BraceDepth > 0)
                {
                    _blockInfoByToken[_tokens.Count] = new RazorBlockInfo(RazorBlockKind.ControlFlow);
                    Emit(RazorSyntaxKind.RazorBlockClose, 1, LexicalState.Razor);
                    _contextStack.Pop();
                    _lastClosedBlockWasControlFlow = true;
                    return;
                }

                // Bare '}' in HTML markup: literal content
                _lastClosedBlockWasControlFlow = false;
                Emit(RazorSyntaxKind.Html, 1, LexicalState.Html);
                return;
            }

            // Control flow continuation without '@': else, catch, finally
            if (_lastClosedBlockWasControlFlow && char.IsLetter(ch))
            {
                var wordStart = _offset;
                while (_offset < _text.Length && char.IsLetterOrDigit(_text[_offset])) _offset++;
                var word = _text.Substring(wordStart, _offset - wordStart);

                if (word is "else" or "catch" or "finally")
                {
                    _lastClosedBlockWasControlFlow = false;
                    Emit(RazorSyntaxKind.RazorControlFlow, wordStart, _offset, LexicalState.Razor);
                    _pendingBlockType = PendingBlockType.ControlFlow;

                    // Check for "else if"
                    if (word == "else")
                    {
                        var saveOffset = _offset;
                        SkipWhitespaceOnly();
                        if (_offset + 2 <= _text.Length && _text.Substring(_offset, 2) == "if" &&
                            (_offset + 2 == _text.Length || !char.IsLetterOrDigit(_text[_offset + 2])))
                        {
                            var ifStart = _offset;
                            _offset += 2;
                            Emit(RazorSyntaxKind.RazorControlFlow, ifStart, _offset, LexicalState.Razor);
                        }
                        else
                        {
                            _offset = saveOffset;
                        }
                    }
                    return;
                }

                _lastClosedBlockWasControlFlow = false;
                Emit(RazorSyntaxKind.Html, wordStart, _offset, LexicalState.Html);
                return;
            }

            _lastClosedBlockWasControlFlow = false;
            Emit(RazorSyntaxKind.Html, 1, LexicalState.Html);
        }

        private void LexCSharpContext(LexicalContext context)
        {
            var ch = _text[_offset];

            if (ch == '\r' || ch == '\n')
            {
                Emit(RazorSyntaxKind.Newline, 1, LexicalState.CSharp);
                return;
            }

            if (char.IsWhiteSpace(ch))
            {
                LexWhitespace(LexicalState.CSharp);
                return;
            }

            // Comments
            if (ch == '/' && _csharpLexer.TryScanComment(ref _offset, out var cStart, out var cEnd, out _))
            {
                Emit(RazorSyntaxKind.CSharp, cStart, cEnd, LexicalState.CSharp);
                return;
            }

            // Razor comment @* ... *@
            if (ch == '@' && _offset + 1 < _text.Length && _text[_offset + 1] == '*')
            {
                var rStart = _offset;
                _offset += 2;
                while (_offset + 1 < _text.Length && !(_text[_offset] == '*' && _text[_offset + 1] == '@'))
                    _offset++;
                if (_offset + 1 < _text.Length) _offset += 2;
                else _offset = _text.Length;
                Emit(RazorSyntaxKind.RazorComment, rStart, _offset, LexicalState.RazorComment);
                return;
            }

            // Single line markup transition @: ...
            if (ch == '@' && _offset + 1 < _text.Length && _text[_offset + 1] == ':')
            {
                var tStart = _offset;
                _offset += 2;
                Emit(RazorSyntaxKind.RazorBlockTransition, tStart, _offset, LexicalState.Razor);
                _contextStack.Push(new LexicalContext(LexicalState.Html, _offset, isSingleLineTransition: true));
                return;
            }

            // Templated Razor delegate transition @<tag>...</tag>
            if (ch == '@' && _offset + 1 < _text.Length && _text[_offset + 1] == '<')
            {
                var tStart = _offset;
                _offset++; // consume '@'
                Emit(RazorSyntaxKind.RazorBlockTransition, tStart, _offset, LexicalState.Razor);
                _contextStack.Push(new LexicalContext(LexicalState.Html, _offset, isSingleLineTransition: false));
                return;
            }

            // C# strings & char literals
            if (ch == '"' || ch == '\'' || ch == '$' || (ch == '@' && _offset + 1 < _text.Length && (_text[_offset + 1] == '"' || _text[_offset + 1] == '$')))
            {
                if (_csharpLexer.TryScanStringOrCharLiteral(ref _offset, out var sStart, out var sEnd))
                {
                    Emit(RazorSyntaxKind.CSharp, sStart, sEnd, LexicalState.CSharp);
                    return;
                }
            }

            // Inline HTML tag transition inside C# code block
            if (ch == '<' && _htmlLexer.CanStartTag(_offset))
            {
                _contextStack.Push(new LexicalContext(LexicalState.Html, _offset));
                if (_htmlLexer.TryScanTag(ref _offset, out var tagStart, out var tagEnd, out var kind, out var info))
                {
                    _tagInfoByToken[_tokens.Count] = info;
                    Emit(kind, tagStart, tagEnd, LexicalState.Html);
                    if (info.IndentsChildren)
                    {
                        _contextStack.Peek().HtmlElementDepth++;
                    }
                }
                return;
            }

            // Nested C# brace open
            if (ch == '{')
            {
                context.BraceDepth++;
                Emit(RazorSyntaxKind.CSharpBlockOpen, 1, LexicalState.CSharp);
                return;
            }

            // C# brace close
            if (ch == '}')
            {
                if (context.BraceDepth > 1)
                {
                    context.BraceDepth--;
                    Emit(RazorSyntaxKind.CSharpBlockClose, 1, LexicalState.CSharp);
                    return;
                }

                // Closes the C# code block
                context.BraceDepth = 0;
                _contextStack.Pop();
                _blockInfoByToken[_tokens.Count] = new RazorBlockInfo(RazorBlockKind.CSharpCodeBlock);
                Emit(RazorSyntaxKind.RazorBlockClose, 1, LexicalState.Razor);
                return;
            }

            Emit(RazorSyntaxKind.CSharp, 1, LexicalState.CSharp);
        }

        private void LexJavaScriptContext(LexicalContext context)
        {
            var start = _offset;
            var end = FindClosingTagOutsideJsLiterals(_text, start, "</script>");
            _offset = end;
            if (end > start)
            {
                Emit(RazorSyntaxKind.EmbeddedJavaScript, start, end, LexicalState.JavaScript);
            }
            _contextStack.Pop();
        }

        private void LexCssContext(LexicalContext context)
        {
            var start = _offset;
            var end = FindClosingTagOutsideCssLiterals(_text, start, "</style>");
            _offset = end;
            if (end > start)
            {
                Emit(RazorSyntaxKind.EmbeddedCss, start, end, LexicalState.Css);
            }
            _contextStack.Pop();
        }

        private static int FindClosingTagOutsideJsLiterals(string text, int start, string closeTag)
        {
            var p = start;
            var len = text.Length;
            var tagLen = closeTag.Length;

            while (p < len)
            {
                if (p + tagLen <= len && string.Compare(text, p, closeTag, 0, tagLen, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return p;
                }

                var ch = text[p];

                // Single line comment // ...
                if (ch == '/' && p + 1 < len && text[p + 1] == '/')
                {
                    p += 2;
                    while (p < len && text[p] != '\n' && text[p] != '\r') p++;
                    continue;
                }

                // Block comment /* ... */
                if (ch == '/' && p + 1 < len && text[p + 1] == '*')
                {
                    p += 2;
                    while (p + 1 < len && !(text[p] == '*' && text[p + 1] == '/')) p++;
                    if (p + 1 < len) p += 2;
                    else p = len;
                    continue;
                }

                // Strings: ", ', `
                if (ch == '"' || ch == '\'' || ch == '`')
                {
                    var q = ch;
                    p++;
                    while (p < len)
                    {
                        if (text[p] == '\\')
                        {
                            p = Math.Min(len, p + 2);
                            continue;
                        }
                        if (text[p] == q)
                        {
                            p++;
                            break;
                        }
                        p++;
                    }
                    continue;
                }

                p++;
            }

            return len;
        }

        private static int FindClosingTagOutsideCssLiterals(string text, int start, string closeTag)
        {
            var p = start;
            var len = text.Length;
            var tagLen = closeTag.Length;

            while (p < len)
            {
                if (p + tagLen <= len && string.Compare(text, p, closeTag, 0, tagLen, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return p;
                }

                var ch = text[p];

                // Block comment /* ... */
                if (ch == '/' && p + 1 < len && text[p + 1] == '*')
                {
                    p += 2;
                    while (p + 1 < len && !(text[p] == '*' && text[p + 1] == '/')) p++;
                    if (p + 1 < len) p += 2;
                    else p = len;
                    continue;
                }

                // Strings: ", '
                if (ch == '"' || ch == '\'')
                {
                    var q = ch;
                    p++;
                    while (p < len)
                    {
                        if (text[p] == '\\')
                        {
                            p = Math.Min(len, p + 2);
                            continue;
                        }
                        if (text[p] == q)
                        {
                            p++;
                            break;
                        }
                        p++;
                    }
                    continue;
                }

                p++;
            }

            return len;
        }

        private void LexWhitespace(LexicalState state)
        {
            var start = _offset;
            while (_offset < _text.Length && char.IsWhiteSpace(_text[_offset]) && _text[_offset] != '\r' && _text[_offset] != '\n')
                _offset++;
            if (_offset > start) Emit(RazorSyntaxKind.Whitespace, start, _offset, state);
        }

        private void SkipWhitespaceOnly()
        {
            while (_offset < _text.Length && char.IsWhiteSpace(_text[_offset]) && _text[_offset] != '\r' && _text[_offset] != '\n')
                _offset++;
        }

        private void LexAtSign()
        {
            var start = _offset;
            _offset++;
            if (_offset >= _text.Length)
            {
                Emit(RazorSyntaxKind.RazorImplicitExpression, start, _offset, LexicalState.Razor);
                return;
            }

            var next = _text[_offset];

            // @@ -> escaped @, emit as literal HTML
            if (next == '@')
            {
                _offset++;
                Emit(RazorSyntaxKind.Html, start, _offset, LexicalState.Html);
                return;
            }

            // @* ... *@ -> Razor comment
            if (next == '*')
            {
                _offset++; // skip '*'
                while (_offset + 1 < _text.Length && !(_text[_offset] == '*' && _text[_offset + 1] == '@'))
                    _offset++;
                if (_offset + 1 < _text.Length) _offset += 2; // consume closing '*@'
                else
                {
                    _offset = _text.Length;
                    _diagnostics.Add(new RazorDiagnostic(RazorDiagnosticSeverity.Warning, "Unclosed Razor comment at end of file.", start));
                }
                Emit(RazorSyntaxKind.RazorComment, start, _offset, LexicalState.RazorComment);
                return;
            }

            // @( ... ) -> explicit expression
            if (next == '(')
            {
                _csharpLexer.SkipBalancedParens(ref _offset);
                Emit(RazorSyntaxKind.RazorExplicitExpression, start, _offset, LexicalState.CSharp);
                return;
            }

            // @{ ... } -> C# code block
            if (next == '{')
            {
                _offset++; // consume '{'
                _contextStack.Push(new LexicalContext(LexicalState.CSharp, start, braceDepth: 1));
                _blockInfoByToken[_tokens.Count] = new RazorBlockInfo(RazorBlockKind.CSharpCodeBlock);
                Emit(RazorSyntaxKind.RazorBlockOpen, start, _offset, LexicalState.Razor);
                return;
            }

            // @: single-line markup transition
            if (next == ':')
            {
                _offset++; // consume ':'
                Emit(RazorSyntaxKind.RazorBlockTransition, start, _offset, LexicalState.Razor);
                _contextStack.Push(new LexicalContext(LexicalState.Html, _offset, isSingleLineTransition: true));
                return;
            }

            // @< templated delegate transition
            if (next == '<')
            {
                Emit(RazorSyntaxKind.RazorBlockTransition, start, _offset, LexicalState.Razor);
                _contextStack.Push(new LexicalContext(LexicalState.Html, _offset, isSingleLineTransition: false));
                return;
            }

            // Keywords: @if, @for, @foreach, @while, @switch, @do, @try, @lock, etc.
            if (char.IsLetter(next))
            {
                var wordStart = _offset;
                while (_offset < _text.Length && (char.IsLetterOrDigit(_text[_offset]) || _text[_offset] == '_')) _offset++;
                var word = _text.Substring(wordStart, _offset - wordStart);

                if (IsControlFlowKeyword(word))
                {
                    Emit(RazorSyntaxKind.RazorControlFlow, start, _offset, LexicalState.Razor);
                    _pendingBlockType = PendingBlockType.ControlFlow;

                    // Handle 'else if'
                    if (word == "else")
                    {
                        var save = _offset;
                        SkipWhitespaceOnly();
                        if (_offset + 2 <= _text.Length && _text.Substring(_offset, 2) == "if" &&
                            (_offset + 2 == _text.Length || !char.IsLetterOrDigit(_text[_offset + 2])))
                        {
                            var ifStart = _offset;
                            _offset += 2;
                            Emit(RazorSyntaxKind.RazorControlFlow, ifStart, _offset, LexicalState.Razor);
                        }
                        else
                        {
                            _offset = save;
                        }
                    }
                    return;
                }

                if (RazorBlockDirectives.IsBlockDirective(word))
                {
                    var isCSharpBlock = word is "code" or "functions";
                    _pendingBlockType = isCSharpBlock ? PendingBlockType.CSharpBlock : PendingBlockType.ControlFlow;

                    // Consume directive arguments up to '{' or newline
                    while (_offset < _text.Length && _text[_offset] != '{' && _text[_offset] != '\r' && _text[_offset] != '\n')
                        _offset++;

                    Emit(RazorSyntaxKind.RazorDirective, start, _offset, LexicalState.Razor);
                    return;
                }

                // Plain directives: @page, @model, @using, @inject, @attribute, @namespace, etc.
                if (IsDirectiveWord(word))
                {
                    while (_offset < _text.Length && _text[_offset] != '\r' && _text[_offset] != '\n')
                        _offset++;
                    Emit(RazorSyntaxKind.RazorDirective, start, _offset, LexicalState.Razor);
                    return;
                }

                // Otherwise: implicit expression @foo.Bar.Baz
                _csharpLexer.ScanImplicitExpressionTail(ref _offset);
                Emit(RazorSyntaxKind.RazorImplicitExpression, start, _offset, LexicalState.CSharp);
                return;
            }

            // Fallback: emit lone '@' as implicit expression
            Emit(RazorSyntaxKind.RazorImplicitExpression, start, _offset, LexicalState.Razor);
        }

        private static bool IsControlFlowKeyword(string word) => word is "if" or "else" or "for" or "foreach" or "while" or "switch" or "do" or "try" or "catch" or "finally" or "lock" or "using";

        private static bool IsDirectiveWord(string word) => word is "page" or "model" or "using" or "inject" or "attribute" or "namespace"
            or "preservewhitespace" or "rendermode" or "type" or "helper" or "layout" or "implements" or "inherits";

        private void Emit(RazorSyntaxKind kind, int length, LexicalState state) => Emit(kind, _offset, _offset + length, state);

        private void Emit(RazorSyntaxKind kind, int start, int end, LexicalState state)
        {
            if (end <= start) return;

            // Coalesce adjacent same-kind and same-state tokens (except structural boundaries)
            if (_tokens.Count > 0)
            {
                var last = _tokens[_tokens.Count - 1];
                if (last.Kind == kind && last.State == state && last.End == start &&
                    kind is not RazorSyntaxKind.HtmlTag and not RazorSyntaxKind.TagHelper
                    and not RazorSyntaxKind.RazorBlockOpen and not RazorSyntaxKind.RazorBlockClose
                    and not RazorSyntaxKind.CSharpBlockOpen and not RazorSyntaxKind.CSharpBlockClose
                    and not RazorSyntaxKind.RazorBlockTransition)
                {
                    _tokens[_tokens.Count - 1] = new RazorToken(kind, last.Start, end, last.Line, last.Column, state);
                    _offset = end;
                    return;
                }
            }
            {
                var (line, col) = _source.GetSequentialLineColumn(start, ref _lastEmittedLine);
                _tokens.Add(new RazorToken(kind, start, end, line, col, state));
                _offset = end;
            }
        }
    }
}

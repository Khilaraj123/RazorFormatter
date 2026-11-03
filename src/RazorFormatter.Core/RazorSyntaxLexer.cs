using System;
using System.Collections.Generic;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.Lexing;

namespace RazorFormatter.Core
{
    /// <summary>
    /// Backward-compatible facade over <see cref="RazorLexer"/>.
    /// </summary>
    public sealed class RazorSyntaxLexer
    {
        private readonly RazorLexer _inner;

        public RazorSyntaxLexer(SourceText source)
        {
            _inner = new RazorLexer(source ?? throw new ArgumentNullException(nameof(source)));
        }

        public IReadOnlyList<RazorDiagnostic> Diagnostics => _inner.Diagnostics;

        public bool TryGetTagInfo(int tokenIndex, out HtmlTagInfo info) => _inner.TryGetTagInfo(tokenIndex, out info);

        public bool TryGetBlockInfo(int tokenIndex, out RazorBlockInfo info) => _inner.TryGetBlockInfo(tokenIndex, out info);

        public IReadOnlyList<RazorToken> Lex() => _inner.Lex();
    }
}

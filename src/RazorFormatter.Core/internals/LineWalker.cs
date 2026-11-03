using System.Collections.Generic;
using RazorFormatter.Core.Formatting;

namespace RazorFormatter.Core.internals
{
    internal sealed class LineWalker
    {
        private readonly SourceText _source;
        private readonly IReadOnlyList<RazorToken> _tokens;
        private readonly RazorSyntaxLexer _lexer;
        private readonly RazorFormatterOptions _options;
        private readonly string _eol;

        public LineWalker(SourceText source, IReadOnlyList<RazorToken> tokens, RazorSyntaxLexer lexer, RazorFormatterOptions options, string eol)
        {
            _source = source;
            _tokens = tokens;
            _lexer = lexer;
            _options = options;
            _eol = eol;
        }

        public void Run(List<TextChange> changes)
        {
            var lineIndents = IndentationEngine.ComputeLineIndents(_source, _tokens, _lexer, _options);
            var preserveIndent = Formatting.FormattingWriter.ComputePreserveIndentLines(_source, _tokens);
            var generated = Formatting.FormattingWriter.GenerateChanges(_source, lineIndents, preserveIndent, _options, _eol);
            changes.AddRange(generated);
        }
    }
}

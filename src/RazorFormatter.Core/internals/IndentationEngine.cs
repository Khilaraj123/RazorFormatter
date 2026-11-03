using System.Collections.Generic;

namespace RazorFormatter.Core.internals
{
    /// <summary>
    /// Backward-compatible internal facade delegating to <see cref="Formatting.IndentationEngine"/>.
    /// </summary>
    internal static class IndentationEngine
    {
        public static int[] ComputeLineIndents(
            SourceText source,
            IReadOnlyList<RazorToken> tokens,
            RazorSyntaxLexer lexer,
            RazorFormatterOptions options)
        {
            return Formatting.IndentationEngine.ComputeLineIndents(source, tokens, lexer, options);
        }
    }
}

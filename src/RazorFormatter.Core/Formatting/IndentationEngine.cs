using System;
using System.Collections.Generic;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Core.Formatting
{
    /// <summary>
    /// Computes structural indentation levels for each line in a Razor document.
    /// </summary>
    public sealed class IndentationEngine
    {
        public static int[] ComputeLineIndents(
            SourceText source,
            IReadOnlyList<RazorToken> tokens,
            RazorSyntaxLexer lexer,
            RazorFormatterOptions options)
        {
            var lineCount = source.LineCount;
            var lineIndents = new int[lineCount];

            // Map tokens to their starting line
            var tokensByLine = new List<int>[lineCount];
            for (var i = 0; i < lineCount; i++)
            {
                tokensByLine[i] = new List<int>();
            }

            for (var i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                var line = tok.Line;
                if (line >= 0 && line < lineCount)
                {
                    tokensByLine[line].Add(i);
                }
            }

            var context = new StructuralContext(options);

            for (var line = 0; line < lineCount; line++)
            {
                var lineTokenIndices = tokensByLine[line];

                // Find the first meaningful token on this line (skipping Whitespace and Newline)
                var firstMeaningfulIdx = -1;
                for (var j = 0; j < lineTokenIndices.Count; j++)
                {
                    var idx = lineTokenIndices[j];
                    var tok = tokens[idx];
                    if (tok.Kind is not RazorSyntaxKind.Whitespace and not RazorSyntaxKind.Newline)
                    {
                        firstMeaningfulIdx = idx;
                        break;
                    }
                }

                if (firstMeaningfulIdx >= 0 &&
                    context.TryGetCloserIndent(tokens[firstMeaningfulIdx], firstMeaningfulIdx, lexer, out var closerIndent))
                {
                    lineIndents[line] = closerIndent;
                }
                else
                {
                    lineIndents[line] = context.GetCurrentDepth();
                }

                // Process all tokens on this line to update structural context for subsequent lines
                for (var j = 0; j < lineTokenIndices.Count; j++)
                {
                    var idx = lineTokenIndices[j];
                    var tok = tokens[idx];
                    context.ApplyTokenEffect(tok, idx, lexer, line);
                }
            }

            return lineIndents;
        }
    }
}


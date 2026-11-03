using System;
using System.Collections.Generic;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.Lexing;

namespace RazorFormatter.Core.Formatting
{
    /// <summary>
    /// Emits text changes for indentation, whitespace trimming, blank line collapse, and line ending normalization.
    /// </summary>
    public sealed class FormattingWriter
    {
        public static bool[] ComputePreserveIndentLines(
            SourceText source,
            IReadOnlyList<RazorToken> tokens)
        {
            var lineCount = source.LineCount;
            var preserve = new bool[lineCount];

            for (var i = 0; i < tokens.Count; i++)
            {
                var tok = tokens[i];
                var (startLine, _) = source.GetLineColumn(tok.Start);
                var endOffset = Math.Max(tok.Start, tok.End - 1);
                var (endLine, _) = source.GetLineColumn(endOffset);

                if (tok.Kind is RazorSyntaxKind.EmbeddedCss or RazorSyntaxKind.EmbeddedJavaScript)
                {
                    for (var l = startLine; l <= endLine; l++)
                    {
                        var lineStart = source.GetOffset(l, 0);
                        if (lineStart >= tok.Start && lineStart < tok.End)
                        {
                            preserve[l] = true;
                        }
                    }
                }
                else if (tok.Kind is RazorSyntaxKind.HtmlTag or RazorSyntaxKind.TagHelper)
                {
                    if (endLine > startLine)
                    {
                        for (var l = startLine + 1; l <= endLine; l++)
                        {
                            preserve[l] = true;
                        }
                    }
                }
                else if (tok.Kind is RazorSyntaxKind.HtmlComment or RazorSyntaxKind.RazorComment)
                {
                    if (endLine > startLine)
                    {
                        for (var l = startLine + 1; l <= endLine; l++)
                        {
                            preserve[l] = true;
                        }
                    }
                }
            }

            return preserve;
        }

        public static List<TextChange> GenerateChanges(
            SourceText source,
            int[] lineIndents,
            RazorFormatterOptions options,
            string eol)
        {
            return GenerateChanges(source, lineIndents, new bool[source.LineCount], options, eol);
        }

        public static List<TextChange> GenerateChanges(
            SourceText source,
            int[] lineIndents,
            bool[] preserveIndent,
            RazorFormatterOptions options,
            string eol)
        {
            var changes = new List<TextChange>(64);
            var lineCount = source.LineCount;
            var blankLinesInARow = 0;

            for (var line = 0; line < lineCount; line++)
            {
                var lineStart = source.GetOffset(line, 0);
                if (lineStart >= source.Length)
                {
                    break;
                }

                var lineEnd = line + 1 < lineCount ? source.GetOffset(line + 1, 0) : source.Length;

                // Find leading horizontal whitespace
                var contentStart = lineStart;
                while (contentStart < lineEnd && IsHorizontalWhitespace(source[contentStart]))
                    contentStart++;

                // Find end of line before newline characters
                var eolStart = lineEnd;
                if (eolStart > lineStart && source[eolStart - 1] == '\n')
                {
                    eolStart--;
                    if (eolStart > lineStart && source[eolStart - 1] == '\r')
                        eolStart--;
                }

                // Find trailing horizontal whitespace before eolStart
                var contentEnd = eolStart;
                while (contentEnd > contentStart && IsHorizontalWhitespace(source[contentEnd - 1]))
                    contentEnd--;

                var currentEol = source.ToString(eolStart, lineEnd - eolStart);
                var isBlankLine = contentStart >= contentEnd;

                if (isBlankLine)
                {
                    blankLinesInARow++;
                    var maxBlank = options.CollapseMultipleBlankLines ? Math.Max(0, options.MaxConsecutiveBlankLines) : int.MaxValue;
                    if (blankLinesInARow > maxBlank)
                    {
                        if (lineEnd > lineStart)
                        {
                            changes.Add(TextChange.Delete(lineStart, lineEnd));
                        }
                    }
                    else
                    {
                        if (options.TrimTrailingWhitespace && contentStart > lineStart)
                        {
                            changes.Add(TextChange.Delete(lineStart, contentStart));
                        }
                        if (currentEol.Length > 0 && currentEol != eol)
                        {
                            changes.Add(TextChange.Replace(eolStart, lineEnd, eol));
                        }
                    }
                    continue;
                }

                blankLinesInARow = 0;

                // 1. Adjust leading indent
                if (line < preserveIndent.Length && !preserveIndent[line])
                {
                    var targetDepth = line < lineIndents.Length ? lineIndents[line] : 0;
                    var desiredIndent = options.BuildIndentString(targetDepth);
                    var currentIndent = source.ToString(lineStart, contentStart - lineStart);

                    if (currentIndent != desiredIndent)
                    {
                        changes.Add(TextChange.Replace(lineStart, contentStart, desiredIndent));
                    }
                }

                // 2. Trim trailing whitespace
                if (options.TrimTrailingWhitespace && eolStart > contentEnd)
                {
                    changes.Add(TextChange.Delete(contentEnd, eolStart));
                }

                // 3. Normalize line ending
                if (currentEol.Length > 0 && currentEol != eol)
                {
                    changes.Add(TextChange.Replace(eolStart, lineEnd, eol));
                }
            }

            // 4. Final newline rule
            if (options.InsertFinalNewline && source.Length > 0)
            {
                var endsWithEol = source.Length >= 1 &&
                    (source[source.Length - 1] == '\n' || source[source.Length - 1] == '\r');

                if (!endsWithEol)
                {
                    changes.Add(TextChange.Insert(source.Length, eol));
                }
            }

            return changes;
        }

        private static bool IsHorizontalWhitespace(char ch) => ch == ' ' || ch == '\t';
    }
}

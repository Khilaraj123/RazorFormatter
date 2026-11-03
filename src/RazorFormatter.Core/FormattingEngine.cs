using System;
using System.Collections.Generic;
using System.Linq;
using RazorFormatter.Core.Abstractions;
using RazorFormatter.Core.Enums;
using RazorFormatter.Core.Formatting;
using RazorFormatter.Core.internals;

namespace RazorFormatter.Core
{
    public sealed class FormattingEngine : IRazorFormatter
    {
        public FormatResult Format(string source, RazorFormatterOptions? options = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            var opts = options != null && !string.IsNullOrEmpty(options.FilePath)
                ? Configuration.ConfigurationResolver.Resolve(options.FilePath, options)
                : (options ?? new RazorFormatterOptions());
            var text = new SourceText(source);

            // If the user passed EndOfLine.Auto, pin it to the detected dominant style now.
            var eol = opts.EndOfLine switch
            {
                EndOfLine.Lf => "\n",
                EndOfLine.Crlf => "\r\n",
                _ => text.DetectLineEnding(),
            };

            var diagnostics = new List<RazorDiagnostic>();

            try
            {
                var currentSource = source;
                var currentText = text;

                // Stage 1: Attribute formatting and embedded language formatting
                var lexer1 = new RazorSyntaxLexer(currentText);
                var tokens1 = lexer1.Lex();
                diagnostics.AddRange(lexer1.Diagnostics);

                if (opts.SkipOnLexerError && diagnostics.Any(d => d.Severity == RazorDiagnosticSeverity.Error))
                {
                    return new FormatResult(source, Array.Empty<TextChange>(), opts, wasSkipped: true, diagnostics);
                }

                var lineIndents1 = Formatting.IndentationEngine.ComputeLineIndents(currentText, tokens1, lexer1, opts);
                var tagAndEmbeddedChanges = new List<TextChange>();

                for (var i = 0; i < tokens1.Count; i++)
                {
                    var tok = tokens1[i];
                    var baseIndent = tok.Line >= 0 && tok.Line < lineIndents1.Length ? lineIndents1[tok.Line] : 0;

                    if (tok.Kind is RazorSyntaxKind.HtmlTag or RazorSyntaxKind.TagHelper)
                    {
                        var tagText = currentText.ToString(tok.Start, tok.Length);
                        var formattedTag = AttributeFormatter.FormatTag(tagText, baseIndent, opts, eol);
                        if (formattedTag != tagText)
                        {
                            tagAndEmbeddedChanges.Add(TextChange.Replace(tok.Start, tok.End, formattedTag));
                        }
                    }
                    else if (opts.FormatEmbeddedLanguages && tok.Kind == RazorSyntaxKind.EmbeddedCss)
                    {
                        var cssText = currentText.ToString(tok.Start, tok.Length);
                        var formattedCss = EmbeddedLanguageFormatter.FormatCss(cssText, baseIndent + 1, opts, eol);
                        if (formattedCss != cssText)
                        {
                            tagAndEmbeddedChanges.Add(TextChange.Replace(tok.Start, tok.End, formattedCss));
                        }
                    }
                    else if (opts.FormatEmbeddedLanguages && tok.Kind == RazorSyntaxKind.EmbeddedJavaScript)
                    {
                        var jsText = currentText.ToString(tok.Start, tok.Length);
                        var formattedJs = EmbeddedLanguageFormatter.FormatJavaScript(jsText, baseIndent + 1, opts, eol);
                        if (formattedJs != jsText)
                        {
                            tagAndEmbeddedChanges.Add(TextChange.Replace(tok.Start, tok.End, formattedJs));
                        }
                    }
                }

                if (tagAndEmbeddedChanges.Count > 0)
                {
                    currentSource = TextChangeApplier.Apply(currentSource, tagAndEmbeddedChanges);
                    currentText = new SourceText(currentSource);
                }

                // Stage 2: Line indentation, trailing whitespace trimming, blank lines, and EOL normalization
                var lexer2 = new RazorSyntaxLexer(currentText);
                var tokens2 = lexer2.Lex();
                var changes2 = new List<TextChange>(64);
                var walker = new LineWalker(currentText, tokens2, lexer2, opts, eol);
                walker.Run(changes2);

                var finalFormatted = TextChangeApplier.Apply(currentSource, changes2);

                // Compute final changes relative to the original source
                IReadOnlyList<TextChange> finalChanges;
                if (tagAndEmbeddedChanges.Count == 0)
                {
                    finalChanges = changes2;
                }
                else if (finalFormatted == source)
                {
                    finalChanges = Array.Empty<TextChange>();
                }
                else
                {
                    finalChanges = ComputeMinimalChange(source, finalFormatted);
                }

                return new FormatResult(finalFormatted, finalChanges, opts, wasSkipped: false, diagnostics);
            }
            catch (RazorSyntaxException ex) when (opts.SkipOnLexerError)
            {
                diagnostics.Add(new RazorDiagnostic(RazorDiagnosticSeverity.Error, ex.Message, ex.Offset, ex.Line, ex.Column));
                return new FormatResult(source, Array.Empty<TextChange>(), opts, wasSkipped: true, diagnostics);
            }
            catch (RazorSyntaxException ex)
            {
                diagnostics.Add(new RazorDiagnostic(RazorDiagnosticSeverity.Error, ex.Message, ex.Offset, ex.Line, ex.Column));
                throw;
            }
            catch (Exception ex)
            {
                throw new RazorFormatterInternalException("An internal error occurred during formatting.", ex);
            }
        }

        public FormatResult FormatFile(string filePath, RazorFormatterOptions? explicitOptions = null)
        {
            if (string.IsNullOrEmpty(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!System.IO.File.Exists(filePath)) throw new System.IO.FileNotFoundException("File not found", filePath);

            var source = System.IO.File.ReadAllText(filePath);
            var resolvedOptions = Configuration.ConfigurationResolver.Resolve(filePath, explicitOptions);
            return Format(source, resolvedOptions);
        }

        public FormatResult FormatRange(string source, int startOffset, int endOffset, RazorFormatterOptions? options = null)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (startOffset < 0) startOffset = 0;
            if (endOffset > source.Length) endOffset = source.Length;
            if (startOffset > endOffset) (startOffset, endOffset) = (endOffset, startOffset);

            var fullResult = Format(source, options);
            if (fullResult.Changes.Count == 0 || fullResult.WasSkipped)
                return fullResult;

            var rangeChanges = fullResult.Changes
                .Where(c => c.Start <= endOffset && c.End >= startOffset)
                .ToList();

            if (rangeChanges.Count == 0)
            {
                return new FormatResult(source, Array.Empty<TextChange>(), fullResult.OptionsUsed, fullResult.WasSkipped, fullResult.Diagnostics);
            }

            var formattedText = TextChangeApplier.Apply(source, rangeChanges);
            return new FormatResult(formattedText, rangeChanges, fullResult.OptionsUsed, fullResult.WasSkipped, fullResult.Diagnostics);
        }

        public string FormatToText(string source, RazorFormatterOptions? options = null)
            => Format(source, options).FormattedText;

        private static IReadOnlyList<TextChange> ComputeMinimalChange(string original, string formatted)
        {
            if (original == formatted) return Array.Empty<TextChange>();

            var p = 0;
            while (p < original.Length && p < formatted.Length && original[p] == formatted[p])
                p++;

            var s = 0;
            while (s < original.Length - p && s < formatted.Length - p &&
                   original[original.Length - 1 - s] == formatted[formatted.Length - 1 - s])
                s++;

            var origSpanLength = original.Length - p - s;
            var newText = formatted.Substring(p, formatted.Length - p - s);

            return new[] { TextChange.Replace(p, p + origSpanLength, newText) };
        }
    }
}

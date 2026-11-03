using System;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Core.Formatting
{
    /// <summary>
    /// Coordinates the element and block trackers, maintaining explicit structural state for indentation.
    /// </summary>
    public sealed class StructuralContext
    {
        public ElementTracker Elements { get; } = new();
        public BlockTracker Blocks { get; } = new();
        public RazorFormatterOptions Options { get; }

        public StructuralContext(RazorFormatterOptions options)
        {
            Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public int GetCurrentDepth()
        {
            return Elements.CalculateDepth(Options) + Blocks.CalculateDepth(Options);
        }

        public bool TryGetCloserIndent(
            RazorToken token,
            int tokenIndex,
            RazorSyntaxLexer lexer,
            out int indent)
        {
            indent = 0;

            switch (token.Kind)
            {
                case RazorSyntaxKind.HtmlTag:
                case RazorSyntaxKind.TagHelper:
                {
                    if (lexer.TryGetTagInfo(tokenIndex, out var info) && info.IsClosing)
                    {
                        if (Elements.TryGetCloserDepth(info.Name, Options, out var elemDepth))
                        {
                            indent = elemDepth + Blocks.CalculateDepth(Options);
                            return true;
                        }
                    }
                    return false;
                }

                case RazorSyntaxKind.RazorBlockClose:
                {
                    if (Blocks.TryGetCloserDepth(isCSharpBrace: false, Options, out var blockDepth))
                    {
                        indent = blockDepth + Elements.CalculateDepth(Options);
                        return true;
                    }
                    return false;
                }

                case RazorSyntaxKind.CSharpBlockClose:
                {
                    if (Blocks.TryGetCloserDepth(isCSharpBrace: true, Options, out var blockDepth))
                    {
                        indent = blockDepth + Elements.CalculateDepth(Options);
                        return true;
                    }
                    return false;
                }

                default:
                    return false;
            }
        }

        public void ApplyTokenEffect(
            RazorToken token,
            int tokenIndex,
            RazorSyntaxLexer lexer,
            int currentLine)
        {
            switch (token.Kind)
            {
                case RazorSyntaxKind.RazorBlockOpen:
                {
                    var kind = StructuralBlockKind.RazorControlFlow;
                    if (lexer.TryGetBlockInfo(tokenIndex, out var info))
                    {
                        if (info.Kind == RazorBlockKind.CSharpCodeBlock)
                            kind = StructuralBlockKind.CSharpCodeBlock;
                    }
                    Blocks.Push(kind, currentLine, token.Start, Elements.Count);
                    break;
                }

                case RazorSyntaxKind.CSharpBlockOpen:
                {
                    Blocks.Push(StructuralBlockKind.CSharpNestedBraces, currentLine, token.Start, Elements.Count);
                    break;
                }

                case RazorSyntaxKind.CSharpBlockClose:
                {
                    Blocks.TryPopBlock(isCSharpBrace: true);
                    break;
                }

                case RazorSyntaxKind.RazorBlockClose:
                {
                    Blocks.TryPopBlock(isCSharpBrace: false);
                    break;
                }

                case RazorSyntaxKind.HtmlTag:
                case RazorSyntaxKind.TagHelper:
                {
                    if (lexer.TryGetTagInfo(tokenIndex, out var info))
                    {
                        if (info.IndentsChildren)
                        {
                            var isComponent = token.Kind == RazorSyntaxKind.TagHelper;
                            Elements.Push(info.Name, isComponent, currentLine, token.Start);
                        }
                        else if (info.IsClosing)
                        {
                            Elements.TryPopMatching(info.Name);
                        }
                    }
                    break;
                }
            }
        }
    }
}


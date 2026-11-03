using System;
using System.Collections.Generic;

namespace RazorFormatter.Core.Formatting
{
    public enum StructuralBlockKind
    {
        RazorControlFlow,
        CSharpCodeBlock,
        CSharpNestedBraces,
        RazorSection,
    }

    public sealed class BlockFrame
    {
        public StructuralBlockKind Kind { get; }
        public int LineOpened { get; }
        public int StartOffset { get; }
        public int ElementDepthAtOpen { get; }

        public BlockFrame(StructuralBlockKind kind, int lineOpened, int startOffset, int elementDepthAtOpen = 0)
        {
            Kind = kind;
            LineOpened = lineOpened;
            StartOffset = startOffset;
            ElementDepthAtOpen = elementDepthAtOpen;
        }

        public bool ContributesToIndent(RazorFormatterOptions options) => Kind switch
        {
            StructuralBlockKind.RazorControlFlow => options.FormatRazorControlFlow,
            StructuralBlockKind.RazorSection => options.FormatRazorControlFlow,
            StructuralBlockKind.CSharpCodeBlock => options.FormatCSharpBlocks,
            StructuralBlockKind.CSharpNestedBraces => options.FormatCSharpBlocks,
            _ => true,
        };

        public override string ToString() => $"{Kind} (Line {LineOpened + 1})";
    }

    /// <summary>
    /// Tracks Razor control-flow, C# code blocks, section blocks, and C# nested braces.
    /// </summary>
    public sealed class BlockTracker
    {
        private readonly List<BlockFrame> _frames = new();

        public IReadOnlyList<BlockFrame> Frames => _frames;
        public int Count => _frames.Count;

        public void Push(StructuralBlockKind kind, int line, int startOffset, int elementDepth = 0)
        {
            _frames.Add(new BlockFrame(kind, line, startOffset, elementDepth));
        }

        public bool TryGetCloserDepth(bool isCSharpBrace, RazorFormatterOptions options, out int depth)
        {
            depth = 0;
            if (isCSharpBrace)
            {
                for (var i = _frames.Count - 1; i >= 0; i--)
                {
                    if (_frames[i].Kind == StructuralBlockKind.CSharpNestedBraces)
                    {
                        depth = CalculateDepthUntil(i, options);
                        return true;
                    }
                }
            }
            else
            {
                for (var i = _frames.Count - 1; i >= 0; i--)
                {
                    if (_frames[i].Kind is StructuralBlockKind.RazorControlFlow or StructuralBlockKind.CSharpCodeBlock or StructuralBlockKind.RazorSection)
                    {
                        depth = CalculateDepthUntil(i, options);
                        return true;
                    }
                }
            }
            depth = Math.Max(0, CalculateDepth(options) - 1);
            return true;
        }

        public bool TryPopBlock(bool isCSharpBrace)
        {
            if (isCSharpBrace)
            {
                for (var i = _frames.Count - 1; i >= 0; i--)
                {
                    if (_frames[i].Kind == StructuralBlockKind.CSharpNestedBraces)
                    {
                        _frames.RemoveRange(i, _frames.Count - i);
                        return true;
                    }
                }
            }
            else
            {
                for (var i = _frames.Count - 1; i >= 0; i--)
                {
                    if (_frames[i].Kind is StructuralBlockKind.RazorControlFlow or StructuralBlockKind.CSharpCodeBlock or StructuralBlockKind.RazorSection)
                    {
                        _frames.RemoveRange(i, _frames.Count - i);
                        return true;
                    }
                }
            }
            return false;
        }

        public int CalculateDepth(RazorFormatterOptions options)
        {
            var d = 0;
            for (var i = 0; i < _frames.Count; i++)
            {
                if (_frames[i].ContributesToIndent(options))
                    d++;
            }
            return d;
        }

        public int CalculateDepthUntil(int count, RazorFormatterOptions options)
        {
            var d = 0;
            var limit = Math.Min(count, _frames.Count);
            for (var i = 0; i < limit; i++)
            {
                if (_frames[i].ContributesToIndent(options))
                    d++;
            }
            return d;
        }
    }
}


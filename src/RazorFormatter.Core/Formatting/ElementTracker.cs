using System;
using System.Collections.Generic;

namespace RazorFormatter.Core.Formatting
{
    public sealed class ElementFrame
    {
        public string TagName { get; }
        public bool IsComponent { get; }
        public int LineOpened { get; }
        public int StartOffset { get; }

        public ElementFrame(string tagName, bool isComponent, int lineOpened, int startOffset)
        {
            TagName = tagName;
            IsComponent = isComponent;
            LineOpened = lineOpened;
            StartOffset = startOffset;
        }

        public bool ContributesToIndent(RazorFormatterOptions options)
            => IsComponent ? options.FormatTagHelpers : options.FormatHtml;

        public override string ToString() => $"<{TagName}> (Line {LineOpened + 1}, Component: {IsComponent})";
    }

    /// <summary>
    /// Tracks HTML element and TagHelper nesting, handling inline tags, void elements, and mismatched closing tag recovery.
    /// </summary>
    public sealed class ElementTracker
    {
        private readonly List<ElementFrame> _frames = new();

        public IReadOnlyList<ElementFrame> Frames => _frames;
        public int Count => _frames.Count;

        public void Push(string tagName, bool isComponent, int line, int startOffset)
        {
            _frames.Add(new ElementFrame(tagName.ToLowerInvariant(), isComponent, line, startOffset));
        }

        public bool TryGetCloserDepth(string tagName, RazorFormatterOptions options, out int depth)
        {
            depth = 0;
            var tag = tagName.ToLowerInvariant();

            // Search from top of stack downwards for matching element
            for (var i = _frames.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_frames[i].TagName, tag, StringComparison.OrdinalIgnoreCase))
                {
                    depth = CalculateDepthUntil(i, options);
                    return true;
                }
            }
            return false;
        }

        public bool TryPopMatching(string tagName)
        {
            var tag = tagName.ToLowerInvariant();
            for (var i = _frames.Count - 1; i >= 0; i--)
            {
                if (string.Equals(_frames[i].TagName, tag, StringComparison.OrdinalIgnoreCase))
                {
                    // Mismatched closing tag recovery: pop all unclosed inner elements as well as matching element
                    _frames.RemoveRange(i, _frames.Count - i);
                    return true;
                }
            }
            // Rogue closing tag: not found, leave stack intact
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


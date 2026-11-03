using System.Text;

namespace RazorFormatter.Core
{
    public static class TextChangeApplier
    {
        public static string Apply(string source, IReadOnlyList<TextChange> changes)
        {
            if (source is null) throw new ArgumentNullException(nameof(source));
            if (changes is null || changes.Count == 0) return source;

            // Sort changes by Start ascending, then End ascending
            var sorted = new List<TextChange>(changes);
            sorted.Sort((a, b) =>
            {
                var cmp = a.Start.CompareTo(b.Start);
                return cmp != 0 ? cmp : a.End.CompareTo(b.End);
            });

            var sb = new StringBuilder(source.Length + 64);
            var cursor = 0;

            foreach (var change in sorted)
            {
                if (change.Start < 0 || change.End > source.Length)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(changes),
                        $"Change span [{change.Start}..{change.End}) is outside source range [0..{source.Length}).");
                }

                if (change.Start < cursor)
                {
                    throw new InvalidOperationException(
                        $"Overlapping or out-of-order edit detected at [{change.Start}..{change.End}) (current cursor={cursor}).");
                }

                if (change.Start > cursor)
                {
                    sb.Append(source, cursor, change.Start - cursor);
                }

                if (!string.IsNullOrEmpty(change.NewText))
                {
                    sb.Append(change.NewText);
                }

                cursor = change.End;
            }

            if (cursor < source.Length)
            {
                sb.Append(source, cursor, source.Length - cursor);
            }

            return sb.ToString();
        }
    }
}

using RazorFormatter.Core.Abstractions;

namespace RazorFormatter.Core
{
    /// <summary>Static factory so callers do not need to know the concrete engine type.</summary>
    public static class RazorFormatterFactory
    {
        public static IRazorFormatter Create() => new FormattingEngine();
    }

}

using System;
using System.Runtime.InteropServices;
using RazorFormatter.Core;
using RazorFormatter.Core.Abstractions;

namespace RazorFormatter.VisualStudio
{
    [Guid("98d57d76-8848-4cb9-9528-6644ec8b9401")]
    public sealed class RazorFormatterPackage
    {
        public const string PackageGuidString = "98d57d76-8848-4cb9-9528-6644ec8b9401";

        private readonly IRazorFormatter _formatter;

        public RazorFormatterPackage()
        {
            _formatter = RazorFormatterFactory.Create();
        }

        public string FormatSnapshot(string text, RazorFormatterOptions? options = null)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return _formatter.FormatToText(text, options);
        }
    }
}


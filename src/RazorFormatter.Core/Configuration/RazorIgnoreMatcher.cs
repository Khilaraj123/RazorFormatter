using System;
using System.Collections.Generic;
using System.IO;

namespace RazorFormatter.Core.Configuration
{
    public sealed class RazorIgnoreMatcher
    {
        private readonly List<string> _patterns = new();

        public IReadOnlyList<string> Patterns => _patterns;

        public RazorIgnoreMatcher()
        {
            // Default built-in ignore patterns
            AddPattern("bin/");
            AddPattern("obj/");
            AddPattern(".git/");
            AddPattern(".vs/");
            AddPattern("node_modules/");
        }

        public void AddPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return;
            var trimmed = pattern.Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal) || trimmed.StartsWith(";", StringComparison.Ordinal))
                return;

            _patterns.Add(trimmed);
        }

        public void LoadFromFile(string filePath)
        {
            if (!File.Exists(filePath)) return;

            foreach (var line in File.ReadAllLines(filePath))
            {
                AddPattern(line);
            }
        }

        public void DiscoverAndLoad(string directory)
        {
            var current = new DirectoryInfo(directory);
            while (current != null && current.Exists)
            {
                var ignoreFile = Path.Combine(current.FullName, ".razorignore");
                if (File.Exists(ignoreFile))
                {
                    LoadFromFile(ignoreFile);
                    break;
                }
                current = current.Parent;
            }
        }

        public bool IsIgnored(string filePath)
        {
            var normalized = filePath.Replace('\\', '/').TrimStart('/');
            var fileName = Path.GetFileName(filePath);

            foreach (var pattern in _patterns)
            {
                var p = pattern.Replace('\\', '/');

                // Directory pattern ending in /: e.g. "bin/" or "obj/"
                if (p.EndsWith("/", StringComparison.Ordinal))
                {
                    var dirName = p.Trim('/');
                    if (normalized.StartsWith(dirName + "/", StringComparison.OrdinalIgnoreCase) ||
                        normalized.Contains("/" + dirName + "/", StringComparison.OrdinalIgnoreCase) ||
                        normalized.EndsWith("/" + dirName, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(normalized, dirName, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }

                if (EditorConfigGlobMatcher.IsMatch(p, normalized) || EditorConfigGlobMatcher.IsMatch(p, fileName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}


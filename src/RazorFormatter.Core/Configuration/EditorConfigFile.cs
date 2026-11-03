using System;
using System.Collections.Generic;

namespace RazorFormatter.Core.Configuration
{
    public sealed class EditorConfigSection
    {
        public string GlobPattern { get; }
        public IReadOnlyDictionary<string, string> Properties { get; }

        public EditorConfigSection(string globPattern, IReadOnlyDictionary<string, string> properties)
        {
            GlobPattern = globPattern ?? throw new ArgumentNullException(nameof(globPattern));
            Properties = properties ?? throw new ArgumentNullException(nameof(properties));
        }
    }

    public sealed class EditorConfigFile
    {
        public string FilePath { get; }
        public string DirectoryPath { get; }
        public bool IsRoot { get; }
        public IReadOnlyList<EditorConfigSection> Sections { get; }

        public EditorConfigFile(string filePath, string directoryPath, bool isRoot, IReadOnlyList<EditorConfigSection> sections)
        {
            FilePath = filePath;
            DirectoryPath = directoryPath;
            IsRoot = isRoot;
            Sections = sections;
        }
    }
}


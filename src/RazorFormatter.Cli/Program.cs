using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using RazorFormatter.Core;
using RazorFormatter.Core.Configuration;
using RazorFormatter.Core.Enums;

namespace RazorFormatter.Cli
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var options = new RazorFormatterOptions();
            var writeInPlace = false;
            var checkOnly = false;
            var changesJson = false;
            var files = new List<string>();
            string? explicitConfigFile = null;
            var ignoreMatcher = new RazorIgnoreMatcher();

            for (var i = 0; i < args.Length; i++)
            {
                var arg = args[i];
                switch (arg)
                {
                    case "--server":
                        return new Server.FormatterServer().Run();
                    case "--changes-json":
                        changesJson = true;
                        break;
                    case "-w":
                    case "--write":
                        writeInPlace = true;
                        break;
                    case "-c":
                    case "--check":
                        checkOnly = true;
                        break;
                    case "--tab":
                        options.IndentStyle = IndentStyle.Tab;
                        break;
                    case "--indent":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out var indent))
                        {
                            options.IndentSize = indent;
                        }
                        break;
                    case "--lf":
                        options.EndOfLine = EndOfLine.Lf;
                        break;
                    case "--crlf":
                        options.EndOfLine = EndOfLine.Crlf;
                        break;
                    case "--attribute-layout":
                        if (i + 1 < args.Length)
                        {
                            var layoutStr = args[++i].ToLowerInvariant().Replace("-", "_");
                            if (layoutStr is "one_per_line" or "oneperline")
                                options.AttributeLayout = AttributeLayout.OnePerLine;
                            else if (layoutStr is "single_line" or "singleline")
                                options.AttributeLayout = AttributeLayout.SingleLine;
                            else if (layoutStr == "preserve")
                                options.AttributeLayout = AttributeLayout.Preserve;
                        }
                        break;
                    case "--config":
                        if (i + 1 < args.Length)
                        {
                            explicitConfigFile = args[++i];
                        }
                        break;
                    case "--ignore":
                        if (i + 1 < args.Length)
                        {
                            ignoreMatcher.AddPattern(args[++i]);
                        }
                        break;
                    case "-h":
                    case "--help":
                        PrintHelp();
                        return 0;
                    default:
                        if (!arg.StartsWith("-"))
                        {
                            files.Add(arg);
                        }
                        break;
                }
            }

            // Load .razorignore from current directory
            ignoreMatcher.DiscoverAndLoad(Directory.GetCurrentDirectory());

            var engine = RazorFormatterFactory.Create();

            // Stdin mode
            if (files.Count == 0 || (files.Count == 1 && files[0] == "-"))
            {
                var input = Console.In.ReadToEnd();
                if (string.IsNullOrEmpty(input))
                {
                    PrintHelp();
                    return 0;
                }

                RazorFormatterOptions effectiveOptions = options;
                if (!string.IsNullOrEmpty(explicitConfigFile) && File.Exists(explicitConfigFile))
                {
                    var parsed = EditorConfigParser.Parse(explicitConfigFile, File.ReadAllText(explicitConfigFile));
                    var props = ConfigurationResolver.ResolveProperties("stdin.cshtml", new[] { parsed });
                    effectiveOptions = options.Clone();
                    ConfigurationResolver.ApplyProperties(effectiveOptions, props);
                }

                var result = engine.Format(input, effectiveOptions);
                if (checkOnly)
                {
                    return result.Changes.Count > 0 ? 1 : 0;
                }

                if (changesJson)
                {
                    var json = JsonSerializer.Serialize(new
                    {
                        wasSkipped = result.WasSkipped,
                        formattedText = result.FormattedText,
                        changes = result.Changes,
                        diagnostics = result.Diagnostics
                    });
                    Console.Out.Write(json);
                    return 0;
                }

                Console.Out.Write(result.FormattedText);
                return 0;
            }

            var hasUnformattedFiles = false;

            foreach (var pattern in files)
            {
                var matchedFiles = Directory.Exists(pattern)
                    ? Directory.GetFiles(pattern, "*.cshtml", SearchOption.AllDirectories)
                    : File.Exists(pattern)
                        ? new[] { pattern }
                        : Array.Empty<string>();

                if (matchedFiles.Length == 0)
                {
                    Console.Error.WriteLine($"File or directory not found: {pattern}");
                    return 1;
                }

                foreach (var filePath in matchedFiles)
                {
                    if (ignoreMatcher.IsIgnored(filePath))
                    {
                        continue;
                    }

                    FormatResult result;
                    if (!string.IsNullOrEmpty(explicitConfigFile) && File.Exists(explicitConfigFile))
                    {
                        var content = File.ReadAllText(filePath);
                        var parsed = EditorConfigParser.Parse(explicitConfigFile, File.ReadAllText(explicitConfigFile));
                        var props = ConfigurationResolver.ResolveProperties(filePath, new[] { parsed });
                        var effectiveOptions = options.Clone();
                        effectiveOptions.FilePath = filePath;
                        ConfigurationResolver.ApplyProperties(effectiveOptions, props);
                        result = engine.Format(content, effectiveOptions);
                    }
                    else
                    {
                        result = engine.FormatFile(filePath, options);
                    }

                    if (checkOnly)
                    {
                        if (result.Changes.Count > 0)
                        {
                            hasUnformattedFiles = true;
                            Console.Error.WriteLine($"Needs formatting: {filePath}");
                        }
                    }
                    else if (writeInPlace)
                    {
                        if (result.Changes.Count > 0)
                        {
                            File.WriteAllText(filePath, result.FormattedText);
                            Console.WriteLine($"Formatted: {filePath}");
                        }
                    }
                    else if (changesJson)
                    {
                        var json = JsonSerializer.Serialize(new
                        {
                            filePath = filePath,
                            wasSkipped = result.WasSkipped,
                            formattedText = result.FormattedText,
                            changes = result.Changes,
                            diagnostics = result.Diagnostics
                        });
                        Console.Out.WriteLine(json);
                    }
                    else
                    {
                        Console.Out.Write(result.FormattedText);
                    }
                }
            }

            if (checkOnly && hasUnformattedFiles)
            {
                return 1;
            }

            return 0;
        }

        private static void PrintHelp()
        {
            Console.WriteLine("RazorFormatter CLI");
            Console.WriteLine("Usage: dotnet run --project src/RazorFormatter.Cli -- [options] <files/folders...>");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --server                    Run as a long-running JSON-RPC formatting daemon");
            Console.WriteLine("  --changes-json              Output minimal text edits and diagnostics as JSON");
            Console.WriteLine("  -w, --write                 Format and overwrite files in-place");
            Console.WriteLine("  -c, --check                 Check if files are formatted (exits with code 1 if not)");
            Console.WriteLine("      --tab                   Use tabs for indentation (default: spaces)");
            Console.WriteLine("      --indent <n>            Number of spaces per indentation level (default: 4)");
            Console.WriteLine("      --lf                    Force LF line endings");
            Console.WriteLine("      --crlf                  Force CRLF line endings");
            Console.WriteLine("      --attribute-layout <l>  Attribute layout: preserve, one-per-line, single-line");
            Console.WriteLine("      --config <path>         Path to explicit .editorconfig file");
            Console.WriteLine("      --ignore <pattern>      Glob pattern to ignore (can be used multiple times)");
            Console.WriteLine("  -h, --help                  Show help");
        }
    }
}

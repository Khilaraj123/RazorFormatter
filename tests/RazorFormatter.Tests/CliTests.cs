using System;
using System.IO;
using RazorFormatter.Cli;
using Xunit;

namespace RazorFormatter.Tests
{
    public class CliTests : IDisposable
    {
        private readonly string _tempDir;

        public CliTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "RazorFormatter_CliTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDir))
            {
                try
                {
                    Directory.Delete(_tempDir, recursive: true);
                }
                catch
                {
                    // Ignore temp directory deletion issues
                }
            }
        }

        [Fact]
        public void Cli_InPlaceFormatting_DefaultBehavior_FormatsFile()
        {
            var filePath = Path.Combine(_tempDir, "Test.cshtml");
            var unformatted = "<div>\n<p>Hello</p>\n</div>\n";
            File.WriteAllText(filePath, unformatted);

            var exitCode = Program.Main(new[] { filePath });

            Assert.Equal(Program.ExitSuccess, exitCode);
            var result = File.ReadAllText(filePath);
            Assert.Contains("    <p>Hello</p>", result);
        }

        [Fact]
        public void Cli_CheckSubcommand_UnformattedFile_ReturnsCode1()
        {
            var filePath = Path.Combine(_tempDir, "Unformatted.cshtml");
            var unformatted = "<div>\n<p>Needs format</p>\n</div>\n";
            File.WriteAllText(filePath, unformatted);

            var exitCode = Program.Main(new[] { "check", filePath });

            Assert.Equal(Program.ExitDifferencesFound, exitCode);
            // File should NOT be modified in check mode
            Assert.Equal(unformatted, File.ReadAllText(filePath));
        }

        [Fact]
        public void Cli_CheckSubcommand_CleanFile_ReturnsCode0()
        {
            var filePath = Path.Combine(_tempDir, "Clean.cshtml");
            var clean = "<div>\n    <p>Clean</p>\n</div>\n";
            File.WriteAllText(filePath, clean);

            var exitCode = Program.Main(new[] { "check", filePath });

            Assert.Equal(Program.ExitSuccess, exitCode);
        }

        [Fact]
        public void Cli_DiffSubcommand_GeneratesUnifiedDiff_ReturnsCode1()
        {
            var filePath = Path.Combine(_tempDir, "Diff.cshtml");
            var unformatted = "<div>\n<p>Diff line</p>\n</div>\n";
            File.WriteAllText(filePath, unformatted);

            var originalOut = Console.Out;
            using var sw = new StringWriter();
            Console.SetOut(sw);

            try
            {
                var exitCode = Program.Main(new[] { "diff", filePath });
                Assert.Equal(Program.ExitDifferencesFound, exitCode);

                var output = sw.ToString();
                Assert.Contains("---", output);
                Assert.Contains("+++", output);
                Assert.Contains("@@", output);
            }
            finally
            {
                Console.SetOut(originalOut);
            }
        }

        [Fact]
        public void Cli_InvalidOption_ReturnsCode2()
        {
            var exitCode = Program.Main(new[] { "--nonexistent-flag" });
            Assert.Equal(Program.ExitInvalidArguments, exitCode);
        }

        [Fact]
        public void Cli_MissingTargetFile_ReturnsCode3()
        {
            var exitCode = Program.Main(new[] { Path.Combine(_tempDir, "does-not-exist.cshtml") });
            Assert.Equal(Program.ExitFileOrFormatError, exitCode);
        }

        [Fact]
        public void DiffHelper_GeneratesAccurateUnifiedDiff()
        {
            var original = "<div>\n<p>Old</p>\n</div>\n";
            var formatted = "<div>\n    <p>Old</p>\n</div>\n";

            var diff = DiffHelper.CreateUnifiedDiff("Test.cshtml", original, formatted);

            Assert.Contains("--- Test.cshtml (original)", diff);
            Assert.Contains("+++ Test.cshtml (formatted)", diff);
            Assert.Contains("- <p>Old</p>", diff);
            Assert.Contains("+     <p>Old</p>", diff);
        }
    }
}

# RazorCodeFormatter (`razorfmt`)

> Fast, deterministic, Prettier-grade code formatter for ASP.NET Core Razor (`.cshtml` and `.razor`) files.

[![NuGet Version](https://img.shields.io/nuget/v/RazorCodeFormatter?color=blue&logo=nuget)](https://www.nuget.org/packages/RazorCodeFormatter/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub](https://img.shields.io/badge/GitHub-Khilaraj123%2FRazorFormatter-black?logo=github)](https://github.com/Khilaraj123/RazorFormatter)

**RazorCodeFormatter** is a .NET Global Tool providing a `gofmt`-style formatting experience for Razor markup. It combines HTML semantic structure, C# control flow, embedded CSS/JavaScript, and Tag Helpers into a unified, 100% idempotent formatting pipeline.

---

## 🚀 Quick Start

### Installation
Install as a global .NET tool from any terminal:

```bash
dotnet tool install --global RazorCodeFormatter
```

To update an existing installation:
```bash
dotnet tool update --global RazorCodeFormatter
```

---

## 💻 CLI Commands & Usage

The executable is named **`razorfmt`**.

### 1. Format in Place (Default)
Format individual files or entire directory trees directly:

```bash
# Format a single Razor file in-place
razorfmt Views/Home/Index.cshtml

# Format an entire directory recursively
razorfmt ./Views

# Format the current project
razorfmt .
```

### 2. CI/CD Check Mode (`check`)
Check if files comply with formatting standards without modifying them. Returns exit code `1` if unformatted files are found, or `0` if all files are clean:

```bash
razorfmt check ./Views
razorfmt check .
```

### 3. Unified Diff Mode (`diff`)
Inspect formatting changes as a unified diff before applying:

```bash
razorfmt diff Views/Home/Index.cshtml
razorfmt diff .
```

### 4. Standard I/O Streaming
Format standard input and pipe to standard output:

```bash
cat Index.cshtml | razorfmt
```

---

## ⚙️ Command-Line Options

| Option | Description |
|:---|:---|
| `check <path>` | Verify formatting without modifying files (exits with code 1 if unformatted) |
| `diff <path>` | Display unified diffs for files needing formatting |
| `-w`, `--write` | Overwrite files in-place (default for file and directory targets) |
| `--stdout` | Print formatted content to stdout instead of overwriting |
| `--config <path>` | Explicit path to an `.editorconfig` file |
| `--ignore <pattern>` | Ignore glob pattern (can be repeated, e.g. `--ignore "**/Generated/**"`) |
| `--indent <n>` | Indentation size in spaces (default: `4`) |
| `--tab` | Indent with tabs instead of spaces |
| `--lf` | Enforce Unix line endings (`\n`) |
| `--crlf` | Enforce Windows line endings (`\r\n`) |
| `--attribute-layout <l>` | Attribute wrapping: `preserve`, `one-per-line`, or `single-line` |
| `--server` | Launch persistent stdio JSON-RPC daemon (for IDE extensions) |
| `--changes-json` | Output minimal `TextChange` diffs as structured JSON |
| `--verbose` | Print detailed file processing logs |
| `-v`, `--version` | Display installed version |
| `-h`, `--help` | Display help and usage information |

### Exit Codes
- `0`: Success; all files are clean and formatted.
- `1`: Unformatted files detected (in `check` or `diff` mode).
- `2`: Invalid command-line arguments or syntax.
- `3`: I/O, file access, or parsing failure.

---

## 🛠️ GitHub Actions CI Example

Enforce consistent Razor formatting on every Pull Request:

```yaml
name: Verify Razor Formatting

on:
  pull_request:
    branches: [ main ]
  push:
    branches: [ main ]

jobs:
  format-check:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET SDK
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'

      - name: Install razorfmt
        run: dotnet tool install -g RazorCodeFormatter

      - name: Check Formatting
        run: razorfmt check .
```

---

## 📝 EditorConfig Support

`razorfmt` automatically resolves `.editorconfig` settings in your project hierarchy:

```ini
[*.{cshtml,razor}]
indent_style = space
indent_size = 4
end_of_line = lf
trim_trailing_whitespace = true
insert_final_newline = true

# Razor-specific rules
razor_wrap_attributes = auto       # auto | preserve | always
razor_format_embedded = true       # format embedded <style> and <script>
razor_collapse_empty_lines = true  # collapse consecutive blank lines
```

Exclusions can also be placed in `.razorignore` files.

---

## 🔗 Ecosystem & Links

- **GitHub Repository**: [Khilaraj123/RazorFormatter](https://github.com/Khilaraj123/RazorFormatter)
- **VS Code Extension**: [Razor Formatter on VS Code Marketplace](https://marketplace.visualstudio.com/items?itemName=razor-formatter.razor-code-formatter)
- **Visual Studio 2022**: [Visual Studio Marketplace](https://marketplace.visualstudio.com/items?itemName=razor-formatter.razorfmt)
- **Issues & Feedback**: [GitHub Issues](https://github.com/Khilaraj123/RazorFormatter/issues)

---

## 📄 License

Licensed under the [MIT License](https://github.com/Khilaraj123/RazorFormatter/blob/main/LICENSE).


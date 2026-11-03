# Razor Formatter for VS Code, Cursor & Google Antigravity

[![Version](https://img.shields.io/visual-studio-marketplace/v/razor-formatter.razor-code-formatter?label=Marketplace&color=blue)](https://marketplace.visualstudio.com/items?itemName=razor-formatter.razor-code-formatter)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![GitHub](https://img.shields.io/badge/GitHub-Khilaraj123%2FRazorFormatter-black?logo=github)](https://github.com/Khilaraj123/RazorFormatter)

A deterministic, Prettier-grade code formatter for ASP.NET Core Razor (`.cshtml` and `.razor`) files. Unifies HTML semantic structure, Razor block directives, embedded CSS/JavaScript, and C# control flow into an idempotent, lightning-fast formatting pipeline.

---

## ✨ Features

- ⚡ **Sub-Millisecond Formatting**: Built-in persistent IPC daemon (`--server`) ensures instant, flicker-free format-on-save.
- 🎯 **100% Idempotent**: Formatting an already formatted document emits **0 changes** — no formatting ping-pong.
- 🧩 **Semantic Mixed-Language Support**: Dedicated lexers for Razor syntax, C# expressions, HTML5 markup, comments, and raw string literals (`"""..."""`).
- 🔄 **C# Control-Flow Directives**: Cleanly indents `@if`, `@else if`, `@else`, `@foreach`, `@for`, `@while`, `@switch`, `@try`, `@catch`, `@finally`, `@using`, `@lock`, `@code { ... }`, and `@{ ... }`.
- 🎨 **Embedded CSS & JavaScript**: Accurately handles inline `<style>` and `<script>` blocks without corrupting surrounding markup.
- 📐 **Attribute Wrapping Policies**: Supports `auto`, `preserve`, and `always` attribute layout modes, keeping Tag Helpers and HTML attributes neatly aligned.
- ⚙️ **EditorConfig & .razorignore Support**: Automatically respects your project's `.editorconfig` hierarchy and `.razorignore` exclusions.
- 🔒 **Security Hardened**: Safe workspace trust enforcement, ReDoS timeouts, and shell-free process execution.

---

## 🚀 Getting Started

### 1. Set as Default Formatter
Add the following to your VS Code / Cursor `settings.json`:

```json
{
  "[aspnetcorerazor]": {
    "editor.defaultFormatter": "razor-formatter.razor-code-formatter",
    "editor.formatOnSave": true
  },
  "[razor]": {
    "editor.defaultFormatter": "razor-formatter.razor-code-formatter",
    "editor.formatOnSave": true
  }
}
```

### 2. Format a File
- **Format Document**: `Shift + Alt + F` (Windows) / `Shift + Option + F` (macOS) / `Ctrl + Shift + I` (Linux).
- **Format on Save**: Automatically runs whenever you save a Razor file.

---

## ⚙️ Configuration Settings

Configure these in VS Code Settings (`Ctrl+,` / `Cmd+,`) under **Razor Formatter**:

| Setting | Default | Description |
|:---|:---:|:---|
| `razorFormatter.serverMode` | `true` | Runs Razor Formatter as a persistent background daemon for sub-millisecond formatting without process restart overhead. |
| `razorFormatter.indentSize` | `4` | Number of spaces per indentation level. |
| `razorFormatter.indentStyle` | `"space"` | Indentation style (`"space"` or `"tab"`). |
| `razorFormatter.wrapAttributes` | `"auto"` | Attribute wrapping policy (`"auto"`, `"preserve"`, or `"always"`). |
| `razorFormatter.formatEmbeddedLanguages` | `true` | Format embedded CSS inside `<style>` and JavaScript inside `<script>` blocks. |
| `razorFormatter.collapseMultipleBlankLines` | `true` | Collapse consecutive blank lines to a single blank line. |
| `razorFormatter.cliPath` | `""` | Optional explicit path to the `razor-format` CLI executable. *(Machine-scoped for security)*. |

---

## 📝 EditorConfig Integration

Razor Formatter automatically detects and prioritizes `.editorconfig` rules in your solution:

```ini
[*.{cshtml,razor}]
indent_style = space
indent_size = 4
end_of_line = lf
trim_trailing_whitespace = true
insert_final_newline = true

# Razor-specific settings
razor_wrap_attributes = auto
razor_format_embedded = true
razor_collapse_empty_lines = true
```

---

## 🔗 Useful Links

- **GitHub Repository**: [Khilaraj123/RazorFormatter](https://github.com/Khilaraj123/RazorFormatter)
- **Report an Issue**: [GitHub Issues](https://github.com/Khilaraj123/RazorFormatter/issues)
- **Visual Studio 2022 Extension**: [Marketplace](https://marketplace.visualstudio.com/items?itemName=razor-formatter.razorformat)
- **.NET Global Tool**: `dotnet tool install -g RazorFormatter`

---

## 📄 License

Licensed under the [MIT License](https://github.com/Khilaraj123/RazorFormatter/blob/main/LICENSE).

# Security Audit & Code Vulnerability Report

**Target**: `RazorFormatter` (Core, CLI, and VS Code Extension)  
**Date**: November 3, 2026  
**Auditor**: Antigravity Security Audit Engine (`/security-audit`)  
**Scope**: All C# source files (`src/RazorFormatter.Core`, `src/RazorFormatter.Cli`), TypeScript extension (`src/RazorFormatter.VSCode`), tests, configurations, and build definitions.

---

## Executive Summary

A comprehensive source-level security audit and bug analysis was performed across the RazorFormatter codebase. The audit identified **2 High-Severity Security Vulnerabilities**, **1 Medium-Severity Security Vulnerability**, and **5 Bugs / Logic Defects**.

The most critical issue involves **untrusted workspace command execution** in the VS Code / Cursor / Antigravity extension, where opening an untrusted repository containing a staged binary in `bin/Debug/` or a custom `cliPath` setting can trigger arbitrary code execution upon document save or formatting. Additional issues include Regular Expression Denial of Service (ReDoS) in EditorConfig pattern matching, an uncaught `IndexOutOfRangeException` crashing on trailing escape characters, and an unhandled `NotSupportedException` on untitled editor buffers.

---

## Findings Overview Table

| ID | Title | Component | Severity | Type |
|---|---|---|---|---|
| **SEC-01** | Untrusted Workspace Arbitrary Binary Execution | `RazorFormatter.VSCode` (`resolver.ts`) | **High** | Remote/Local Code Execution |
| **SEC-02** | Command / Argument Injection via `shell: true` with Workspace Settings | `RazorFormatter.VSCode` (`extension.ts`) | **High** | Command Injection |
| **SEC-03** | Regular Expression Denial of Service (ReDoS) in `GlobToRegex` | `RazorFormatter.Core` (`EditorConfigGlobMatcher.cs`) | **Medium** | Denial of Service (ReDoS) |
| **BUG-01** | Unhandled `IndexOutOfRangeException` on Trailing Backslash in Attribute | `RazorFormatter.Core` (`HtmlLexer.cs`) | **Medium** | Lexer Crash |
| **BUG-02** | Unhandled `NotSupportedException` on Virtual / Untitled Document URIs | `RazorFormatter.Core` (`ConfigurationResolver.cs`) | **Medium** | Engine Crash |
| **BUG-03** | CLI Directory Scanning Ignores `.razor` Files | `RazorFormatter.Cli` (`Program.cs`) | **Low** | Functional Defect |
| **BUG-04** | Monolithic `TextChange` Span Collapses Non-Contiguous File Edits | `RazorFormatter.Core` (`FormattingEngine.cs`) | **Low** | UX / Range Formatting Defect |
| **BUG-05** | Premature `</script>` / `</style>` Delimiter Matching Inside Literals | `RazorFormatter.Core` (`RazorLexer.cs`) | **Low** | Lexer Grammar Defect |

---

## Detailed Findings

### SEC-01: Untrusted Workspace Arbitrary Binary Execution

- **Component**: `src/RazorFormatter.VSCode/src/resolver.ts` (lines 36–64)
- **Severity**: **High**
- **CWE**: CWE-426 (Untrusted Search Path), CWE-829 (Inclusion of Functionality from Untrusted Sphere)

#### Description
In `src/RazorFormatter.VSCode/src/resolver.ts`, the `resolveCli()` function searches workspace directories for executable files:
```typescript
const workspaceFolders = vscode.workspace.workspaceFolders || [];
for (const folder of workspaceFolders) {
    const buildPaths = [
        path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'bin', 'Debug', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli'),
        path.join(folder.uri.fsPath, 'src', 'RazorFormatter.Cli', 'bin', 'Release', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli'),
        path.join(folder.uri.fsPath, 'bin', 'Debug', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli'),
        path.join(folder.uri.fsPath, 'bin', 'Release', 'net10.0', process.platform === 'win32' ? 'RazorFormatter.Cli.exe' : 'RazorFormatter.Cli')
    ];

    for (const bp of buildPaths) {
        if (fs.existsSync(bp)) {
            return { command: bp, args: [], isDirectExecutable: true };
        }
    }
}
```

#### Impact
If an attacker crafts a malicious Git repository containing an executable staged at `bin/Debug/net10.0/RazorFormatter.Cli.exe` (or in `src/RazorFormatter.Cli/...`), cloning and opening this repository in VS Code, Cursor, or Antigravity and editing or saving any Razor file triggers immediate execution of the binary with the user's host credentials. Furthermore, `package.json` does not declare `capabilities.untrustedWorkspaces.supported = false`, allowing execution even in untrusted workspaces.

#### Remediation
1. Remove implicit execution of binaries from workspace directories. Precompiled binaries must only be loaded from the extension bundle directory (`context.extensionPath/bin`) or system PATH.
2. Declare `untrustedWorkspaces` support as `false` in `package.json`:
   ```json
   "capabilities": {
     "untrustedWorkspaces": {
       "supported": false
     }
   }
   ```
3. If workspace binaries are ever desired, prompt the user with explicit Workspace Trust consent.

---

### SEC-02: Command / Argument Injection via `shell: true` with Workspace Settings

- **Component**: `src/RazorFormatter.VSCode/src/extension.ts` (line 203), `resolver.ts` (lines 13–23)
- **Severity**: **High**
- **CWE**: CWE-78 (Improper Neutralization of Special Elements used in an OS Command)

#### Description
In `src/RazorFormatter.VSCode/src/extension.ts`:
```typescript
const child = spawn(resolution.command, args, { shell: !resolution.isDirectExecutable });
```
When `razorFormatter.cliPath` is defined in `.vscode/settings.json` pointing to a DLL (e.g., `tools/app.dll`), `resolution.isDirectExecutable` is set to `false`, causing Node's `child_process.spawn` to run with `shell: true` (which invokes `cmd.exe /c` on Windows).

Because `.vscode/settings.json` is repository-controlled, an attacker can specify a path or parameter containing command chaining operators (`&`, `|`, `;`) or unquoted spaces. When spawned via `shell: true` on Windows, `cmd.exe` interprets metacharacters, leading to arbitrary shell command execution.

#### Remediation
1. Never pass `shell: true` when executing child processes. The `dotnet` runtime executable can always be spawned directly with `shell: false`.
2. Restrict `razorFormatter.cliPath` to Machine/User settings scope (disallowing folder/workspace overrides in `package.json`).

---

### SEC-03: Regular Expression Denial of Service (ReDoS) in `GlobToRegex`

- **Component**: `src/RazorFormatter.Core/Configuration/EditorConfigGlobMatcher.cs` (lines 40–143)
- **Severity**: **Medium**
- **CWE**: CWE-1333 (Inefficient Regular Expression Complexity)

#### Description
`EditorConfigGlobMatcher.GlobToRegex` converts section globs into regular expressions:
```csharp
case '*':
    if (i + 1 < glob.Length && glob[i + 1] == '*')
    {
        sb.Append(".*");
        i += 2;
        ...
    }
    else
    {
        sb.Append("[^/]*");
        i++;
    }
```
And builds the regex:
```csharp
return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
```
No `matchTimeout` is specified. If an `.editorconfig` contains overlapping wildcards (e.g. `[**/*.*]` or `[*{a,b}*]`), matching against crafted long file paths triggers catastrophic exponential backtracking, pinning the CPU at 100% and causing Denial of Service.
Additionally, compiling a new `Regex` instance on every `IsMatch` check causes severe performance degradation and GC pressure.

#### Remediation
1. Enforce a strict regex timeout:
   ```csharp
   return new Regex(sb.ToString(), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(250));
   ```
2. Cache compiled expressions in a `ConcurrentDictionary<string, Regex>`.

---

### BUG-01: Unhandled `IndexOutOfRangeException` on Trailing Backslash in Attribute

- **Component**: `src/RazorFormatter.Core/Lexing/HtmlLexer.cs` (lines 103–107 and 147)
- **Severity**: **Medium**
- **Type**: Crash / Unhandled Exception

#### Description
In `HtmlLexer.TryScanTag`:
```csharp
if (qch == '\\')
{
    offset += 2;
    continue;
}
```
If an unclosed quoted attribute value ends with a trailing backslash at EOF (e.g. `<div class="test\`), `offset += 2` increments `offset` to `_text.Length + 1`.
The scanning loop exits, and `end` becomes `_text.Length + 1`.
Subsequently, line 147 evaluates:
```csharp
if (!selfClosing && end >= 2 && _text[end - 1] == '>')
```
`_text[end - 1]` accesses `_text[_text.Length]`, which throws `System.IndexOutOfRangeException` and crashes the lexer.

#### Remediation
Bound `offset` increment to `_text.Length`:
```csharp
if (qch == '\\')
{
    offset = Math.Min(_text.Length, offset + 2);
    continue;
}
```

---

### BUG-02: Unhandled `NotSupportedException` on Virtual / Untitled Document URIs

- **Component**: `src/RazorFormatter.Core/Configuration/ConfigurationResolver.cs` (line 96)
- **Severity**: **Medium**
- **Type**: Crash / Unhandled Exception

#### Description
In `ConfigurationResolver.ResolveProperties`:
```csharp
var normalizedTarget = Path.GetFullPath(targetFilePath).Replace('\\', '/');
```
While `DiscoverConfigFiles` protects path resolution with `try/catch`, `ResolveProperties` calls `Path.GetFullPath(targetFilePath)` without exception handling.
In VS Code and IDE integrations, formatting an unsaved document buffer provides a virtual URI such as `untitled:Untitled-1`. On Windows, `Path.GetFullPath("untitled:Untitled-1")` throws `System.NotSupportedException: The given path's format is not supported.`, crashing the formatter whenever formatting is triggered on a new/unsaved file.

#### Remediation
Guard `Path.GetFullPath` in `ResolveProperties`:
```csharp
string normalizedTarget;
try
{
    normalizedTarget = Path.GetFullPath(targetFilePath).Replace('\\', '/');
}
catch
{
    normalizedTarget = targetFilePath.Replace('\\', '/');
}
```

---

### BUG-03: CLI Directory Scanning Ignores `.razor` Files

- **Component**: `src/RazorFormatter.Cli/Program.cs` (line 145)
- **Severity**: **Low**
- **Type**: Functional Incompleteness

#### Description
When scanning directories for files to format, `Program.cs` executes:
```csharp
Directory.GetFiles(pattern, "*.cshtml", SearchOption.AllDirectories)
```
This hardcodes `"*.cshtml"` and completely omits Blazor / ASP.NET Core `.razor` components. Invoking the CLI on a directory containing only `.razor` files falsely reports `File or directory not found: <path>`.

#### Remediation
Enumerate both extensions:
```csharp
Directory.EnumerateFiles(pattern, "*.*", SearchOption.AllDirectories)
    .Where(f => f.EndsWith(".cshtml", StringComparison.OrdinalIgnoreCase) || 
                 f.EndsWith(".razor", StringComparison.OrdinalIgnoreCase))
    .ToArray();
```

---

### BUG-04: Monolithic `TextChange` Span Collapses Non-Contiguous File Edits

- **Component**: `src/RazorFormatter.Core/FormattingEngine.cs` (lines 108–111, 168–185)
- **Severity**: **Low**
- **Type**: UX / Edit Granularity Defect

#### Description
When attribute formatting or embedded language formatting occurs (`tagAndEmbeddedChanges.Count > 0`), `FormattingEngine` calls `ComputeMinimalChange(source, finalFormatted)` which computes a single common prefix and suffix.
If a tag on line 5 and a tag on line 500 are modified, `ComputeMinimalChange` returns a single gigantic `TextChange` spanning line 5 to line 500, replacing 495 lines of untouched code. In editor integrations, this damages selection formatting (`FormatRange`), wipes cursor state, and degrades undo history.

#### Remediation
Map two-stage changes through an interval tree or line-by-line diff to emit individual, non-overlapping `TextChange` records.

---

### BUG-05: Premature `</script>` / `</style>` Delimiter Matching Inside Literals

- **Component**: `src/RazorFormatter.Core/Lexing/RazorLexer.cs` (lines 361, 374)
- **Severity**: **Low**
- **Type**: Lexer Grammar Defect

#### Description
`LexJavaScriptContext` and `LexCssContext` scan for closing tags using:
```csharp
var idx = _text.IndexOf("</script>", start, StringComparison.OrdinalIgnoreCase);
```
If a JavaScript string or regular expression literal contains the substring `</script>` (e.g. `const tag = "</script>";`), the lexer prematurely ends the embedded context at that string, corrupting subsequent tokenization for the remainder of the file.

#### Remediation
Scan tokens within `<script>` respecting JS string quotes (`"..."`, `'...'`, `` `...` ``) before checking for the closing `</script>` delimiter.

---

## Remediation Roadmap & Priorities

1. **Immediate (P0)**:
   - Patch `SEC-01`: Disable workspace executable probing in `resolver.ts` and set `untrustedWorkspaces: { supported: false }` in `package.json`.
   - Patch `SEC-02`: Ensure `shell: false` in `extension.ts` child process execution.
2. **Short-Term (P1)**:
   - Patch `SEC-03`: Add a 250ms `matchTimeout` and caching to `EditorConfigGlobMatcher.cs`.
   - Patch `BUG-01`: Safe bounds clamping on `offset` in `HtmlLexer.cs`.
   - Patch `BUG-02`: Safe URI/path fallback in `ConfigurationResolver.cs`.
3. **Quality & Completeness (P2)**:
   - Patch `BUG-03`: Add `.razor` extension support to CLI directory scanning.
   - Patch `BUG-04`: Retain localized `TextChange` spans in two-stage formatting.
   - Patch `BUG-05`: Lex string literals in embedded JavaScript blocks.


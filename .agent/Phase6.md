# Implementation Plan: Phase 6 — Distribution and Developer Experience

## Objective
Implement Phase 6 from `.agent/INSTRUCTION.md`:
1. **Standalone CLI Distribution**:
   - Pack as a .NET Global Tool (`razor-format`).
   - Self-contained single-file publish configurations for Windows (`win-x64`), Linux (`linux-x64`), and macOS (`osx-x64`/`osx-arm64`).
   - Publish scripts (`scripts/publish.ps1`, `scripts/publish.sh`).
2. **VS Code Extension Packaging**:
   - Configure `.vscodeignore` and `@vscode/vsce` packaging scripts.
   - Build and produce installable `.vsix` package for VS Code, Cursor, and Google Antigravity.
3. **Visual Studio VSIX Project**:
   - Create `RazorFormatter.VisualStudio.csproj` and configure package registration and MEF exports.
   - Update `source.extension.vsixmanifest`.
4. **GitHub Actions CI/CD Pipeline**:
   - `.github/workflows/ci.yml` running on `push` and `pull_request`.
   - Multi-OS matrix (`windows-latest`, `ubuntu-latest`, `macos-latest`).
   - Automated test execution, .NET Tool packaging, and VS Code VSIX build.
5. **Documentation & Guides**:
   - Comprehensive `README.md` covering installation, CLI usage, .editorconfig, and editor integrations.
   - Supported syntax catalog and known limitations.
6. **Benchmark & Compatibility Reports**:
   - `docs/BENCHMARKS.md` detailing throughput, daemon latency (<1ms), and idempotency.
   - `docs/COMPATIBILITY.md` covering VS Code, Cursor, Antigravity, and Visual Studio.

## Architecture & Components to Build

```
.github/
└── workflows/
    └── ci.yml                        // Multi-OS CI/CD pipeline (Windows, Linux, macOS)

src/RazorFormatter.Cli/
├── RazorFormatter.Cli.csproj        // Configured with PackAsTool, ToolCommandName, metadata

src/RazorFormatter.VisualStudio/
├── RazorFormatter.VisualStudio.csproj // VS extension project referencing Core
├── RazorFormatterPackage.cs          // Package registration
└── source.extension.vsixmanifest     // VSIX manifest definition

src/RazorFormatter.VSCode/
├── .vscodeignore                     // Excludes dev files from extension package
└── package.json                      // Packaging scripts and @vscode/vsce devDependency

scripts/
├── publish-cli.ps1                  // Standalone cross-platform CLI publishing script (PowerShell)
└── publish-cli.sh                   // Standalone cross-platform CLI publishing script (Bash)

docs/
├── BENCHMARKS.md                     // Benchmark throughput, daemon latency, and idempotency reports
└── COMPATIBILITY.md                  // Editor and syntax compatibility matrix

README.md                             // Comprehensive user and developer documentation
```

## Detailed Implementation Steps

### Step 1: Standalone CLI Distribution Configuration
- Update `src/RazorFormatter.Cli/RazorFormatter.Cli.csproj`:
  - Add `<PackAsTool>true</PackAsTool>`
  - Add `<ToolCommandName>razor-format</ToolCommandName>`
  - Add `<PackageId>RazorFormatter</PackageId>`
  - Add package metadata (version `1.0.0`, description, authors, license `MIT`, repository URL).
- Create `scripts/publish-cli.ps1` and `scripts/publish-cli.sh`:
  - `dotnet pack` for .NET Global Tool.
  - Self-contained single-file publishing commands for `win-x64`, `linux-x64`, `osx-x64`, `osx-arm64`.

### Step 2: VS Code Extension Packaging
- Add `.vscodeignore` to `src/RazorFormatter.VSCode/` to exclude TypeScript source, node_modules development files, tests, and build artifacts from the VSIX package.
- Add `package` script to `package.json` (`npx @vscode/vsce package --no-dependencies`).
- Run packaging to produce `razor-formatter-0.1.0.vsix`.

### Step 3: Visual Studio VSIX Project
- Create `src/RazorFormatter.VisualStudio/RazorFormatter.VisualStudio.csproj` referencing `RazorFormatter.Core`.
- Add project to `RazorFormatter.slnx`.
- Verify compilation.

### Step 4: GitHub Actions CI/CD Pipeline
- Create `.github/workflows/ci.yml`:
  - Matrix across `windows-latest`, `ubuntu-latest`, `macos-latest`.
  - Set up .NET 10.0 and Node.js 20.x.
  - `dotnet restore`, `dotnet build -c Release`, and `dotnet test -c Release`.
  - `npm ci`, `npm run build`, and `npx @vscode/vsce package`.
  - `dotnet pack` and publish artifacts.

### Step 5: Benchmark and Compatibility Reports
- Create `docs/BENCHMARKS.md`:
  - Benchmark timings on 50-line, 500-line, and 2,500-line documents.
  - Server mode latency comparison (< 1ms vs ~300ms one-shot).
  - Idempotency verification (0 diff on second pass).
- Create `docs/COMPATIBILITY.md`:
  - IDE matrix: VS Code, Cursor, Google Antigravity, Visual Studio 2022.
  - Syntax matrix: Razor directives, expressions, control flow, Tag Helpers, Blazor components, embedded CSS/JS.
  - OS matrix: Windows, Linux, macOS.

### Step 6: Documentation Overhaul
- Update `README.md`:
  - Installation instructions (Global tool: `dotnet tool install -g RazorFormatter`, VS Code / Cursor / Antigravity VSIX).
  - CLI usage, flags, and daemon server mode (`--server`, `--changes-json`, `--attribute-layout`, etc.).
  - `.editorconfig` options table and precedence explanation.
  - Supported syntax guide and known limitations.


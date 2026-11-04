# RazorFormatter Technical Documentation

Welcome to the **RazorFormatter** architecture and technical documentation directory.

---

## 📚 Documentation Index

| Document | Description |
|---|---|
| [**Extension Architecture**](./EXTENSION_ARCHITECTURE.md) | In-depth technical architecture of the VS Code / Cursor / Antigravity extension: three-tier design, persistent IPC daemon (`FormatterServer.cs`), line-delimited JSON protocol, minimal diff calculation, executable resolution pipeline, and fault-tolerance safeguards. |
| [**Performance & Benchmarks**](./BENCHMARKS.md) | Throughput metrics (>50,000 lines/sec), memory allocation profiles, sub-millisecond daemon response times, and idempotency guarantees. |
| [**Compatibility Matrix**](./COMPATIBILITY.md) | Syntax coverage across Razor 9/10, Blazor component models, HTML5 semantic tags, embedded CSS/JavaScript, EditorConfig support, and IDE matrix. |

---

## 🏗️ High-Level System Overview

```
RazorFormatter System
├── RazorFormatter.Core/          # Pure formatting engine (lexing, structural tracking, diff generation)
├── RazorFormatter.Cli/           # Command-line interface & persistent stdio daemon server (razorfmt)
├── RazorFormatter.VSCode/        # Language extension for VS Code, Cursor, and Google Antigravity
└── RazorFormatter.VisualStudio/  # VSIX extension for Visual Studio 2022
```

For extension-specific internals, see [**Extension Architecture**](./EXTENSION_ARCHITECTURE.md).

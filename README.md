# RetroC IDE

<p align="center">
  <img src="Assets/logo.png" alt="RetroC IDE Logo" width="180" />
</p>

<p align="center">
  A pixel-perfect, open-source recreation of the classic <strong>Borland Turbo C 3.0 IDE</strong> aesthetic,<br/>
  built with modern WinUI 3 and .NET 8.
</p>

> **⚠️ Disclaimer:** This project is **not affiliated with, endorsed by, or associated with** Borland International, Embarcadero Technologies, or Idera, Inc. "Turbo C" is a trademark of Embarcadero Technologies. This is an independent open-source project inspired by the visual design and UX of the original Turbo C 3.0 IDE (1991).

![Build](https://img.shields.io/badge/build-passing-brightgreen)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)
![Framework](https://img.shields.io/badge/framework-.NET%208-purple)
![License](https://img.shields.io/badge/license-MIT-green)

---

## 📸 Screenshots

<p align="center">
  <img src="Assets/Screenshot-Retro-C-IDE.png" alt="RetroC IDE — Main Editor" width="700" /><br/>
  <em>Main editor with syntax highlighting, multi-tab support, and message panel</em>
</p>

<p align="center">
  <img src="Assets/Screenshot-C-compiler.png" alt="RetroC IDE — Running a Program" width="700" /><br/>
  <em>Compile and run without saving — output appears in a separate console window</em>
</p>

---

## ✨ Features

### **Visual Fidelity**
- 🎨 **Authentic retro color scheme** — Blue editor (`#0000AA`), cyan borders (`#00AAAA`)
- 📦 **Box-drawing borders** — Classic DOS-style window frames with responsive scaling
- ⌨️ **Classic typography** — Consolas monospace for that authentic feel
- 🔘 **Block cursor** — Blinking `█` cursor with 530ms animation cycle

### **Editor Capabilities**
- 📝 **Multi-tab editing** — Open unlimited files, switch with `F6` or `Alt+1..9`
- ✂️ **Edit operations** — Undo/Redo, Cut/Copy/Paste, Clear
- 🔍 **Find & Replace** — Case-sensitive search with Find Again / Find Previous
- 🎯 **Go to Line** — Jump to specific line numbers with `Ctrl+G`
- 🔎 **Find Procedure** — Jump to any function definition by name
- 💾 **Dirty indicator** — Visual `*` in tab title for unsaved changes

### **Compilation**
- 🔨 **Bundled TCC compiler** — Tiny C Compiler (LGPL), auto-downloaded at first run
- ⚡ **Compile without saving** — Run straight from an unsaved buffer via temp file
- ⚡ **One-click build** — `Alt+F9` Compile, `F9` Make, `Ctrl+F9` Run
- 📋 **Error navigation** — Click errors to jump to line; `Alt+F7`/`Alt+F8` cycle through
- ⏱️ **Configurable timeout** — Prevent hung compiler processes (Options > Compiler)

### **Syntax Highlighting**
- 🌈 **Full C language** — 32+ keywords, 40+ standard library functions
- 🔴 **Semantic coloring** — Keywords in red, stdlib in cyan, strings in yellow
- ⚡ **Debounced** — 400ms idle delay prevents UI stalls on large files

### **AI Assistant** (Optional)
- 🤖 **100% local AI** — Runs offline using LLamaSharp + Phi-3 mini (~2.3 GB). No cloud, no API keys.
- 💬 **Ask Mode** — Ask C questions, explain code, explain compiler errors, get code examples
- 🛠️ **Agent Mode** — AI suggests structured edits with diff view; Apply/Reject with one click
- 📎 **Editor context** — Sends your code, cursor position, and errors to the AI for targeted answers
- ⚙️ **Configurable** — Custom GGUF models, GPU offload, temperature, context size
- 📥 **Auto-download** — Model downloads automatically on first use with progress indicator
- 🔒 **Private** — Your code never leaves your machine

> 📖 See [AI-GUIDE.md](AI-GUIDE.md) for detailed usage instructions.

### **Menus — 100% wired**
- All menus fully connected: File, Edit, Search, Run, Compile, Debug (stubs), Project (stubs), Options, Window, Help, System

---

## 🚀 Getting Started

### **Prerequisites**
- Windows 10 build 19041 or later
- .NET 8 SDK

### **Build from Source**
```bash
git clone https://github.com/yourusername/retroc-ide.git
cd retroc-ide/src/C.Compiler

# Build x64
dotnet build -p:Platform=x64

# Run
dotnet run -p:Platform=x64
```

### **Pre-built Binary**
Download the latest release from [Releases](https://github.com/yourusername/retroc-ide/releases), extract, and run `C.Compiler.exe`.

---

## 📖 Keyboard Shortcuts

| Key | Action |
|-----|--------|
| `F2` | Save file |
| `F3` | Open file |
| `F9` | Make (compile + link) |
| `Alt+F9` | Compile to OBJ only |
| `Ctrl+F9` | Run program |
| `Alt+F5` | User screen |
| `F6` | Next editor tab |
| `Alt+1..9` | Switch to tab N |
| `Alt+F3` | Close tab |
| `Alt+F7` / `Alt+F8` | Previous/Next error |
| `Ctrl+G` | Go to line |
| `Ctrl+F` | Find |
| `Ctrl+H` | Find & Replace |
| `Ctrl+Z` / `Ctrl+Y` | Undo / Redo |
| `Alt+X` | Exit |
| `F1` | Help |
| `Ctrl+F1` | Topic Search |
| `Escape` | Close menu / dialog |
| `Ctrl+I` | Toggle AI panel |
| `Ctrl+Enter` | Send AI message |

---

## 🏗️ Architecture

```
src/
├── C.Compiler/                      # Main IDE application
│   ├── Controls/
│   │   ├── EditorControl.xaml(.cs)  # Rich editor with syntax highlighting & block cursor
│   │   └── AIChatPanel.xaml(.cs)    # AI side panel with Ask/Agent modes
│   ├── Models/
│   │   ├── EditorDocument.cs        # File state & selection tracking
│   │   ├── CompilerSettings.cs      # TCC configuration (path, flags, timeout, linker...)
│   │   ├── CompilerError.cs         # Parsed compiler diagnostics
│   │   └── AISettings.cs            # AI model & inference settings
│   ├── Services/
│   │   ├── CompilerService.cs       # TCC/GCC/MSVC process management
│   │   ├── FileService.cs           # File I/O
│   │   ├── ProcessRunner.cs         # Async process execution with timeout
│   │   ├── SettingsService.cs       # JSON persistence (LocalAppData)
│   │   ├── SyntaxHighlighter.cs     # C tokenizer & coloring
│   │   └── TccManager.cs            # TCC auto-download & discovery
│   ├── Dialogs/                     # CompilerOptions, FindReplace, GoToLine, About, AISettings
│   ├── MainWindow.xaml(.cs)         # Shell: menus, tabs, overlays, AI integration
│   └── App.xaml(.cs)                # Entry point, theme
│
└── C.Compiler.AI/                   # AI feature library (conditionally referenced via BUILD_AI)
    ├── Models/
    │   ├── ChatMessage.cs           # ChatRole, ChatMessage, ChatConversation
    │   ├── AIChatMode.cs            # Ask / Agent enum
    │   └── EditorContext.cs         # Code context passed to AI
    └── Services/
        ├── IAIChatService.cs        # AI service interface
        ├── LocalAIChatService.cs    # LLamaSharp-based local inference
        ├── LlamaModelManager.cs     # Model download, discovery & deletion
        ├── CodeContextBuilder.cs    # System prompts & context formatting
        ├── AgentEditParser.cs       # Parses edit/create blocks from Agent responses
        └── MarkdownRenderer.cs      # Renders markdown, code blocks, diff views
```

---

## 🔧 Configuration

Settings are persisted automatically to `%LOCALAPPDATA%\TurboC-IDE\settings.json`.

### **Options > Compiler**
| Field | Description |
|-------|-------------|
| Compiler path | Path to `tcc.exe`, `gcc.exe`, or `cl.exe` (blank = auto-detect) |
| Include dirs | `;`-separated include paths |
| Lib dirs | `;`-separated library search paths |
| Output dir | Where `.obj` / `.exe` are written |
| Flags | Extra compiler flags (e.g. `-Wall -g`) |
| Timeout | Max seconds before killing a hung compile (default: 30) |

### **Options > Linker**
| Field | Description |
|-------|-------------|
| Linker flags | Extra flags passed at link step (e.g. `-lm`) |
| Generate map file | `yes` / `no` |

### **Options > Make**
| Field | Description |
|-------|-------------|
| Primary source file | Override for multi-file projects |
| Warnings as errors | `yes` / `no` |

---

## 📋 Compiler Compatibility

| Feature | Status | Notes |
|---------|--------|-------|
| ANSI C (C89/C90) | ✅ Full | TCC fully supports |
| C99 | ⚠️ Partial | TCC has limited C99 |
| GCC / MinGW | ✅ Supported | Auto-detected on PATH |
| MSVC (`cl.exe`) | ✅ Supported | Auto-detected on PATH |
| Multi-file projects | ⏳ Planned | Currently single-file |
| GDB debugging | ❌ Not planned | Use external GDB |

---

## 🚧 Known Limitations

- **Windows only** — WinUI 3 is a Windows-exclusive framework
- **No integrated debugger** — Debug menu items are stubs pending GDB integration
- **No project files** — `.PRJ` multi-file project format not yet implemented
- **No code completion** — Intentional (retro feel); can be added via LSP
- **AI requires AVX2** — LLamaSharp needs a CPU with AVX2 support (most CPUs from 2013+)
- **AI model is large** — Phi-3 mini Q4 is ~2.3 GB; downloaded once to `%LOCALAPPDATA%\RetroC-IDE\models\`

---

## 🤝 Contributing

Contributions are welcome!

1. Fork the repo
2. Create a branch: `git checkout -b feature/my-feature`
3. Commit: `git commit -m 'Add my feature'`
4. Push: `git push origin feature/my-feature`
5. Open a Pull Request

**Good first issues:**
- GDB integration for Debug menu
- `.PRJ` project file support
- Line number gutter in editor
- Unit tests for `CompilerService` and `SyntaxHighlighter`

---

## 📜 License

MIT License — see [LICENSE](LICENSE) for details.

```
Copyright (c) 2026 Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software...
```

---

## 🙏 Acknowledgments

- **Borland Turbo C 3.0** (1991) — Visual design and UX inspiration
- **TCC (Tiny C Compiler)** — [Fabrice Bellard](https://bellard.org/tcc/) — LGPL, bundled compiler
- **LLamaSharp** — [SciSharp](https://github.com/SciSharp/LLamaSharp) — C# bindings for llama.cpp
- **Phi-3 mini** — [Microsoft](https://huggingface.co/microsoft/Phi-3-mini-4k-instruct-gguf) — Small language model for local AI
- **WinUI 3 / Windows App SDK** — Microsoft's modern Windows UI framework
- **Consolas** — Microsoft's monospace font, perfect for retro IDEs

---

## 📞 Support

- 🐛 **Bugs:** [Open an Issue](https://github.com/yourusername/retroc-ide/issues)
- 💬 **Questions:** [Start a Discussion](https://github.com/yourusername/retroc-ide/discussions)

---

**Last Updated:** April 2026 | **Version:** 1.0.0


![Build](https://img.shields.io/badge/build-passing-brightgreen)
![Platform](https://img.shields.io/badge/platform-Windows%2010%2B-blue)
![Framework](https://img.shields.io/badge/framework-.NET%208-purple)
![License](https://img.shields.io/badge/license-MIT-green)

---

## ✨ Features

### **Visual Fidelity**
- 🎨 **Authentic TC3 color scheme** — Blue editor (`#0000AA`), cyan message panel (`#00AAAA`)
- 📦 **Box-drawing borders** — Classic TC3 window frames with responsive scaling
- ⌨️ **Classic typography** — Consolas monospace, perfect for retro feel
- 🔘 **Block cursor** — Blinking `█` cursor with TC3-style animation (530ms cycle)

### **Editor Capabilities**
- 📝 **Multi-tab editing** — Open unlimited files, switch with `F6` or `Alt+1..9`
- ✂️ **Edit operations** — Undo/Redo, Cut/Copy/Paste, Select All
- 🔍 **Find & Replace** — Case-sensitive search with whole-word matching
- 🎯 **Go to Line** — Jump to specific line numbers with `Ctrl+G`
- 💾 **Auto-save detection** — Visual dirty indicator with `*` in tab

### **Compilation & Debugging**
- 🔨 **Bundled TCC compiler** — Turbo C 3.0 compatible, downloaded at build time
- ⚡ **One-click compilation** — `Alt+F9` Compile, `F9` Make, `Ctrl+F9` Run
- 📋 **Error navigation** — Click errors to jump to file/line; `Alt+F7`/`Alt+F8` cycle
- 💬 **Integrated message panel** — Real-time compiler output and diagnostics

### **Syntax Highlighting**
- 🌈 **C language support** — 32+ keywords, standard library functions
- 🔴 **Semantic coloring** — RED keywords, CYAN stdlib, YELLOW text
- ⚡ **Debounced highlighting** — 400ms idle delay prevents performance lag

---

## 🚀 Getting Started

### **Prerequisites**
- Windows 10 build 19041 or later
- .NET 8 SDK (or .NET Runtime 8.0+)
- [Visual C++ Redistributable](https://support.microsoft.com/en-us/help/2977003) (for WinUI 3)

### **Installation**

#### **Pre-built Binary**
Download the latest release from [Releases](https://github.com/yourusername/Compiler/releases):
```bash
# Extract and run
.\C.Compiler.exe
```

#### **Build from Source**
```bash
git clone https://github.com/yourusername/Compiler.git
cd Compiler/src/C.Compiler

# Build x64 Debug
dotnet build -p:Platform=x64

# Or x86/ARM64
dotnet build -p:Platform=x86
dotnet build -p:Platform=arm64

# Run
dotnet run -p:Platform=x64
```

---

## 📖 Usage

### **Keyboard Shortcuts**

| Key | Action |
|-----|--------|
| `F2` | Save file |
| `F3` | Open file |
| `F9` | Make (compile + link) |
| `Alt+F9` | Compile only |
| `Ctrl+F9` | Run program |
| `Alt+F5` | User screen (show output) |
| `F6` | Next editor window |
| `Alt+F7` / `Alt+F8` | Previous/Next error |
| `Ctrl+G` | Go to line |
| `Ctrl+F` | Find |
| `Ctrl+H` | Find & Replace |
| `Ctrl+Z` | Undo |
| `Ctrl+Y` | Redo |
| `Alt+X` | Exit |
| `F1` | Help |

### **Menu Structure**

**File**
- New, Open, Save, Save As
- Save All, Print
- Program reset

**Edit**
- Undo, Redo, Cut, Copy, Paste, Clear
- Show clipboard, Copy example

**Search**
- Find, Replace, Find procedure, Find error
- Go to line, Match braces

**Run**
- Make, Build all, Run program, User screen
- Program arguments

**Compile**
- Compile file, Primary file selection

**Debug** (stubs)
- Evaluate/modify, Watches, Breakpoints

**Options**
- Compiler path, Include directories
- Linker, Make settings, Environment

**Window**
- Switch tabs, Tile, Cascade
- Output/Watch/Register panels

**Help**
- Contents, Index, Topic search

---

## 🏗️ Architecture

### **Project Structure**
```
src/C.Compiler/
├── Controls/
│   └── EditorControl.xaml(.cs)     # Multi-line editor with syntax highlighting
├── Models/
│   ├── EditorDocument.cs            # File metadata & state
│   ├── CompilerSettings.cs          # TCC configuration
│   └── CompilerResult.cs            # Compilation output
├── Services/
│   ├── CompilerService.cs           # TCC process management
│   ├── FileService.cs               # File I/O (Open, Save, New)
│   ├── ProcessRunner.cs             # Execute EXE in console
│   ├── SyntaxHighlighter.cs         # C tokenization & coloring
│   └── TccManager.cs                # TCC bundling & discovery
├── MainWindow.xaml(.cs)             # Main UI, menu system, tab management
└── App.xaml(.cs)                    # Application entry point
```

### **Key Components**

**EditorControl** — WinUI 3 UserControl
- RichEditBox with custom syntax highlighting
- Blinking block cursor overlay with dynamic sizing
- Responsive top/bottom borders (no hardcoded strings)
- Event forwarding (ContentChanged, CursorMoved, CloseRequested)

**MainWindow** — Multi-tab IDE shell
- TabView for unlimited editors
- Responsive message panel (ListView for errors/output)
- Status bar with line:column position
- 11-menu bar with keyboard accelerators

**CompilerService** — TCC wrapper
- Detects bundled TCC at `AppContext.BaseDirectory\tcc\tcc.exe`
- Async compilation with configurable timeout
- Error parsing with regex (file:line:col format)
- Support for `-I` (include) and `-o` (output) flags

**ProcessRunner** — Console executor
- Spawns `cmd.exe` to run compiled EXE
- Automatic `& pause` to keep console open
- Configurable working directory

---

## 🔧 Configuration

### **Compiler Settings** (Options > Compiler)

**Supported TCC.exe flags:**
```bash
-I <path>     # Include directory (comma-separated)
-o <output>   # Output file path
-E            # Preprocess only
-Wall         # Enable all warnings
```

**Example project setup:**
```
Compiler: C:\path\to\tcc\tcc.exe
Include:  C:\Program Files\tcc\include
Output:   .\bin\
```

### **Environment Variables**

Set before launching to override defaults:
```powershell
$env:TCC_PATH = "C:\Custom\tcc\tcc.exe"
.\C.Compiler.exe
```

---

## 📊 Technical Highlights

- **Zero memory leaks** — Timers disposed on control unload, events unhooked on tab close
- **Null-safe tab management** — Auto-creates editor if all closed; safe indexing
- **Responsive borders** — Grid-based stretching replaces 80+ char hardcoded strings
- **Debounced highlighting** — 400ms idle prevents UI thread stalls on large files
- **DPI-aware** — Block cursor scales with font size; layouts respond to window resize
- **Async/await** — Compilation runs on background thread; UI remains responsive

---

## 📋 Compatibility

| Feature | Status | Notes |
|---------|--------|-------|
| ANSI C (C89/C90) | ✅ Full | TCC fully supports |
| C99 | ⚠️ Partial | TCC has limited C99 support |
| Windows API | ✅ Full | Via `windows.h` includes |
| POSIX | ✅ Partial | Via compatibility mode |
| Multi-file projects | ⚠️ Planned | Currently single-file focus |
| Debugging (GDB) | ❌ Not planned | Output panel only |

---

## 🚧 Known Limitations

- **No project files** — `.PRJ` format not implemented (todo)
- **No integrated debugger** — Use external GDB/Ollydbg
- **Single platform** — Windows only (WinUI 3 constraint)
- **Limited C library** — Common stdlib recognized (strlen, malloc, etc.)
- **No code completion** — Intentional for retro feel; can add via IServiceCollection

---

## 📈 Roadmap

### **Phase 2 (Medium priority)**
- [ ] Complete Debug menu (Watches, Breakpoints stubs → full UI)
- [ ] Complete Options menu (Linker, Make, Environment dialogs)
- [ ] Complete Run menu (Program reset, Trace, Step into)
- [ ] Expand syntax highlighting (100+ C library functions)
- [ ] Find/Replace "Previous" navigation

### **Phase 3 (Low priority)**
- [ ] Project file support (`.PRJ` format)
- [ ] Window tiling/cascading (Size/Move/Zoom)
- [ ] Help system with context search
- [ ] Line number gutter (optional)
- [ ] Code snippets / templates

---

## 🛠️ Development

### **Building**

```bash
# Restore NuGet packages
dotnet restore

# Build for x64 Debug
dotnet build -c Debug -p:Platform=x64

# Build for Release (optimized)
dotnet build -c Release -p:Platform=x64

# Run unit tests (if added)
dotnet test
```

### **Debugging in Visual Studio**

1. Open `C.Compiler.sln`
2. Select "x64 Debug" platform
3. Press `F5` to debug

### **Code Style**

- C# 11+ features encouraged
- async/await for I/O operations
- XAML for all UI (no code-behind for layouts)
- Comments for non-obvious logic

---

## 🤝 Contributing

Contributions welcome! Please:

1. Fork the repo
2. Create a feature branch (`git checkout -b feature/amazing-feature`)
3. Commit changes (`git commit -m 'Add amazing feature'`)
4. Push to branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

### **High-priority contributions needed:**
- Debug menu implementation
- C library function expansion
- Test coverage
- Documentation

---

## 📜 License

MIT License — See [LICENSE](LICENSE) file for details.

```
Copyright (c) 2024 Your Name

Permission is hereby granted, free of charge, to any person obtaining a copy of this software...
```

---

## 🙏 Acknowledgments

- **Turbo C 3.0** — Borland's legendary IDE (1991) — inspiration for visual design
- **TCC (Tiny C Compiler)** — Fabrice Bellard's minimal, fast C compiler
- **WinUI 3** — Microsoft's modern Windows UI framework
- **Consolas font** — Monospace perfection

---

## 📞 Support

**Issues?** Open a [GitHub Issue](https://github.com/yourusername/Compiler/issues)

**Questions?** Start a [Discussion](https://github.com/yourusername/Compiler/discussions)

**Want to chat?** Find me on Twitter/Discord: `@yourhandle`

---

## 🎮 Fun Fact

This IDE replica compiles and runs the **same C source files** as the original Turbo C 3.0 from 1991. No joke — it's retro-compatible by design! 

Try compiling a classic old-school `main.c`:
```c
#include <stdio.h>

int main() {
    printf("Hello, Turbo C!\n");
    return 0;
}
```

Press `F9` and watch the 1991 magic still work in 2024. 🚀

---

**Last Updated:** March 2024 | **Version:** 1.0.0-beta

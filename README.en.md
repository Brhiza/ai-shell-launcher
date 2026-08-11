# AI Shell Launcher

[简体中文](README.md)

AI Shell Launcher manages direct, top-level Windows 10/11 x64 context-menu entries for AI command-line tools. Commands appear directly in the menu for folders, folder backgrounds, and the desktop instead of being nested under an `AI Shell Launcher` submenu.

## Features

- Built-in presets for Codex, Claude Code, Gemini CLI, Grok Build, OpenCode, OpenClaw, and Hermes, plus arbitrary custom commands.
- Normal, Auto, YOLO, and custom argument modes.
- Custom menu labels and icons.
- System default terminal, Windows Terminal, PowerShell, Command Prompt, or a custom terminal.
- Detects the system automatically: Windows 10 uses the classic context menu and Windows 11 uses the modern context menu, with no manual selection.
- Distributed as a single EXE of about 2 MB; no MSIX installer or separate .NET 8 runtime is required.

## Usage

1. Download and run `AiShellLauncher.exe`.
2. Add or enable tools and configure their menu labels, icons, modes, and terminal.
3. Select **Save and apply**. After the initial installation or a menu structure change, the app may ask to restart File Explorer.
4. Right-click a folder, a folder background, or the desktop and select the command directly.

Windows 10 version 2004 (build 19041) or later and Windows 11 are supported. Runtime components are installed for the current user under `%LOCALAPPDATA%\AiShellLauncher`. Context menus are registered internally with sparse packages, but the distributed application remains a single EXE rather than an MSIX installer.

> [!WARNING]
> YOLO modes may bypass confirmation prompts or safety restrictions in the target tool and are disabled by default. Use them only when you understand the command and its effects.

To unregister the context menu while keeping your configuration:

```powershell
.\AiShellLauncher.exe --uninstall --restart-explorer
```

## Building from source

Requirements:

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio 2022 Build Tools with the **Desktop development with C++** workload

Run in PowerShell:

```powershell
.\scripts\build.ps1
```

The script builds the managed solution, runs the core tests, compiles the native shell extension, verifies the bundled resources, and produces:

```text
artifacts\AiShellLauncher.exe
```

## License

The project code is licensed under the [MIT License](LICENSE). Third-party names and icons are used only to identify compatible tools and do not imply affiliation, endorsement, or sponsorship. Trademarks belong to their respective owners. See [Third-party notices](THIRD_PARTY_NOTICES.md) and [assets/builtin/SOURCES.md](assets/builtin/SOURCES.md) for icon sources and licensing.

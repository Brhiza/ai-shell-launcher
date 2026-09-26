# AI Shell Launcher

[简体中文](README.md)

Launch AI command-line tools directly from the Windows 10/11 context menu.

[Download for Windows](https://github.com/Brhiza/ai-shell-launcher/releases)

## Features

- Codex, Claude Code, Antigravity CLI, Grok Build, OpenCode, OpenClaw, Hermes, and custom commands.
- Normal, Auto, YOLO, and custom arguments.
- Custom menu labels, icons, and terminals.
- Automatic classic and modern context-menu support.
- A single EXE of about 2 MB.

## Usage

1. Download and run `AiShellLauncher.exe`.
2. Enable or add the tools you need.
3. Select **Save and apply**.
4. Right-click a folder, folder background, or the desktop and select a tool directly.

If Codex inherits administrator privileges, the launcher adds `--no-daemon` so the shared background server does not block startup. Codex still inherits those privileges.

Windows 10 version 2004 (build 19041) or later and Windows 11 x64 are supported.

> [!WARNING]
> YOLO modes may bypass safety confirmations in the target tool. Use them with care.

## Build

.NET 8 SDK and Visual Studio 2022 Build Tools with the C++ desktop development workload are required:

```powershell
.\scripts\build.ps1
```

## Links

- [LINUX DO](https://linux.do/)

## License

Code is available under the [MIT License](LICENSE). See [Third-party notices](THIRD_PARTY_NOTICES.md) for third-party names, icons, and dependencies.

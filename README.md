# AI Shell Launcher

[English](README.en.md)

在 Windows 10/11 的右键菜单中直接启动 AI 命令行工具。

[下载 Windows 版](https://github.com/Brhiza/ai-shell-launcher/releases)

## 功能

- 支持 Codex、Claude Code、Gemini CLI、Grok Build、OpenCode、OpenClaw、Hermes 和自定义命令。
- 支持普通、Auto、YOLO 和自定义参数。
- 自定义菜单文字、图标和终端。
- 自动适配经典右键菜单和现代右键菜单。
- 单个 EXE，约 2 MB。

## 使用

1. 下载并运行 `AiShellLauncher.exe`。
2. 启用或添加需要的工具。
3. 点击“保存并应用”。
4. 右键文件夹、文件夹空白处或桌面，直接选择工具。

支持 Windows 10 版本 2004（内部版本 19041）及更高版本和 Windows 11 x64。

> [!WARNING]
> YOLO 模式可能跳过目标工具的安全确认，请谨慎使用。

## 源码构建

需要 .NET 8 SDK 和带有 C++ 桌面开发组件的 Visual Studio 2022 Build Tools：

```powershell
.\scripts\build.ps1
```

## 友链

- [LINUX DO](https://linux.do/)

## 许可

代码采用 [MIT License](LICENSE)。第三方名称、图标及依赖的许可信息见 [第三方声明](THIRD_PARTY_NOTICES.md)。

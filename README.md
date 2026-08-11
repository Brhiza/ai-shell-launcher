# AI Shell Launcher

[English](README.en.md)

AI Shell Launcher 是一个面向 Windows 10/11 x64 的右键菜单管理工具。它把常用 AI 命令行工具直接添加到文件夹、文件夹空白处和桌面的首层右键菜单，无需先进入 `AI Shell Launcher` 子菜单。

## 功能

- 内置 Codex、Claude Code、Gemini CLI、Grok Build、OpenCode、OpenClaw 和 Hermes，也可添加任意自定义命令。
- 支持普通、Auto、YOLO 和自定义启动参数。
- 可自定义菜单文字和图标。
- 可使用系统默认终端，或指定 Windows Terminal、PowerShell、命令提示符及自定义终端。
- 自动识别系统：Windows 10 使用经典右键菜单，Windows 11 使用现代右键菜单，无需手动选择。
- 以约 2 MB 的单个 EXE 运行和分发，不需要 MSIX 安装包，也不需要另外安装 .NET 8 运行环境。

## 使用

1. 下载并运行 `AiShellLauncher.exe`。
2. 添加或启用需要的工具，按需设置菜单文字、图标、模式和终端。
3. 点击“保存并应用”。首次安装或菜单结构变化后，程序会询问是否重启资源管理器。
4. 在文件夹、文件夹空白处或桌面上打开右键菜单，直接选择对应命令。

程序支持 Windows 10 版本 2004（内部版本 19041）及更高版本和 Windows 11。运行组件会安装到当前用户的 `%LOCALAPPDATA%\AiShellLauncher`。右键菜单在内部通过稀疏包注册，但用户下载和运行的仍是单个 EXE，不是 MSIX 安装包。

> [!WARNING]
> YOLO 模式可能跳过工具自身的确认或安全限制，默认不启用。请只在理解对应命令参数和影响后使用。

卸载右键菜单并保留配置：

```powershell
.\AiShellLauncher.exe --uninstall --restart-explorer
```

## 从源码构建

需要：

- Windows 10/11 x64
- .NET 8 SDK
- Visual Studio 2022 Build Tools，并安装“使用 C++ 的桌面开发”组件

在 PowerShell 中运行：

```powershell
.\scripts\build.ps1
```

脚本会构建托管项目、运行核心测试、编译原生右键菜单扩展、执行单文件资源自检并生成：

```text
artifacts\AiShellLauncher.exe
```

## 项目结构

- `src`：设置程序、命令启动器和核心逻辑
- `native`：Windows 经典/现代右键菜单扩展
- `assets`：内置工具图标及来源说明
- `package`：稀疏包清单模板
- `scripts`：构建、安装、卸载和图标生成脚本
- `tests`：核心逻辑测试与原生冒烟测试源码

## 许可证

项目代码采用 [MIT License](LICENSE)。内置第三方工具名称和图标仅用于识别对应工具，不代表附属、认可或赞助关系；商标归各自所有者所有。图标来源及许可见 [第三方声明](THIRD_PARTY_NOTICES.md) 和 [assets/builtin/SOURCES.md](assets/builtin/SOURCES.md)。

using AiShellLauncher.Core.Models;
using AiShellLauncher.Core.Services;

var tests = new (string Name, Action Run)[]
{
    ("内置工具目录", TestBuiltinCatalog),
    ("参数模板往返", TestArgumentRoundTrip),
    ("配置读写和菜单索引", TestConfigRoundTrip),
    ("重复 ID 校验", TestDuplicateIds),
    ("Windows 命令解析", TestCommandResolver),
    ("内置图标和自定义文案", TestMenuAppearance),
    ("启动计划", TestLaunchPlan),
    ("真实命令启动", TestCommandStart),
    ("终端打开方式", TestTerminalLaunch),
    ("旧配置迁移", TestLegacyGeminiMigration)
};

var failures = new List<string>();
foreach (var test in tests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures.Add($"FAIL {test.Name}: {exception.Message}");
        Console.Error.WriteLine(failures[failures.Count - 1]);
    }
}

return failures.Count == 0 ? 0 : 1;

static void TestBuiltinCatalog()
{
    var config = BuiltinCatalog.CreateDefault();
    Equal(7, config.Tools.Count, "内置工具数量");
    var codex = config.Tools.Single(tool => tool.Id == "codex");
    True(codex.Modes.Any(mode => mode.Id == "auto" && mode.Arguments.Contains("workspace-write")), "Codex Auto 参数");
    True(codex.Modes.Any(mode => mode.Id == "yolo" && mode.Risk == RiskLevel.Yolo), "Codex YOLO 风险级别");
    True(codex.Modes.Single(mode => mode.Id == "normal").Enabled, "普通模式默认启用");
    True(!codex.Modes.Single(mode => mode.Id == "yolo").Enabled, "YOLO 默认停用");
    var openCode = config.Tools.Single(tool => tool.Id == "opencode");
    True(openCode.Modes.Any(mode => mode.Arguments.Contains("--auto") && mode.Risk == RiskLevel.Yolo), "OpenCode 高风险自动批准");
    var antigravity = config.Tools.Single(tool => tool.Id == "antigravity");
    True(antigravity.Modes.Any(mode => mode.Id == "yolo" && mode.Arguments.Contains("--dangerously-skip-permissions")), "Antigravity YOLO 参数");
}

static void TestArgumentRoundTrip()
{
    var original = new[] { "--model", "name with spaces", "{path}", "quoted\"value", "C:\\trailing\\" };
    var text = ArgumentTemplates.ToDisplayText(original);
    var parsed = ArgumentTemplates.ParseDisplayText(text);
    Equal(original.Length, parsed.Count, "参数数量");
    for (var index = 0; index < original.Length; index++)
    {
        Equal(original[index], parsed[index], $"参数 {index}");
    }

    var expanded = ArgumentTemplates.Expand(parsed, "D:\\Work Folder");
    Equal("D:\\Work Folder", expanded[2], "路径占位符");
}

static void TestConfigRoundTrip()
{
    var root = Path.Combine(Path.GetTempPath(), "AiShellLauncher.Tests", Guid.NewGuid().ToString("N"));
    try
    {
        var service = new ConfigService(root);
        var config = new LauncherConfig
        {
            Tools =
            [
                new ToolDefinition
                {
                    Id = "test",
                    Name = "Test Tool",
                    Command = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    Modes = [new ModeDefinition { Id = "normal", Name = "Normal" }]
                }
            ]
        };
        service.Save(config);
        True(File.Exists(service.ConfigPath), "配置文件存在");
        True(File.Exists(service.MenuIndexPath), "菜单索引存在");
        var loaded = service.LoadOrCreate();
        True(loaded.Warning is null, "配置加载无警告");
        Equal("test", loaded.Config.Tools.Single().Id, "配置内容");
        var menu = File.ReadAllText(service.MenuIndexPath);
        True(menu.IndexOf("C\t0\ttest\tnormal\t在 Test Tool 中打开\tNormal\t", StringComparison.Ordinal) >= 0, "一级菜单记录");
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void TestDuplicateIds()
{
    var config = new LauncherConfig
    {
        Tools =
        [
            new ToolDefinition { Id = "same", Name = "One", Command = "one" },
            new ToolDefinition { Id = "same", Name = "Two", Command = "two" }
        ]
    };

    try
    {
        ConfigService.Validate(config);
        throw new InvalidOperationException("重复 ID 未被拒绝。");
    }
    catch (InvalidDataException)
    {
    }
}

static void TestCommandResolver()
{
    var root = Path.Combine(Path.GetTempPath(), "AiShellLauncher.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var command = Path.Combine(root, "sample-command");
        File.WriteAllText(command, "extensionless shell shim");
        File.WriteAllText(command + ".cmd", "@echo off");

        var resolved = new CommandResolver().Resolve(command);
        Equal(Path.GetFullPath(command + ".cmd"), resolved, "优先选择 Windows 可执行命令");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestMenuAppearance()
{
    var root = Path.Combine(Path.GetTempPath(), "AiShellLauncher.Tests", Guid.NewGuid().ToString("N"));
    try
    {
        var mode = new ModeDefinition
        {
            Id = "normal",
            Name = "Normal",
            MenuTitle = "用 Codex 处理这个目录"
        };
        var config = new LauncherConfig
        {
            Tools =
            [
                new ToolDefinition
                {
                    Id = "codex",
                    Name = "Codex",
                    Command = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                    IsBuiltIn = true,
                    Modes = [mode]
                }
            ]
        };
        var service = new ConfigService(root);
        service.Save(config);
        var menu = File.ReadAllText(service.MenuIndexPath);
        True(menu.IndexOf("用 Codex 处理这个目录", StringComparison.Ordinal) >= 0, "自定义菜单文案");
        True(menu.IndexOf(Path.Combine("Runtime", "Icons", "codex.brand.ico"), StringComparison.OrdinalIgnoreCase) >= 0, "Codex 内置图标");

        mode.IconPath = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        service.Save(config);
        menu = File.ReadAllText(service.MenuIndexPath);
        True(menu.IndexOf(mode.IconPath, StringComparison.OrdinalIgnoreCase) >= 0, "自定义图标覆盖内置图标");
    }
    finally
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }
}

static void TestLaunchPlan()
{
    var workingDirectory = Path.GetTempPath();
    var config = new LauncherConfig
    {
        Tools =
        [
            new ToolDefinition
            {
                Id = "cmd",
                Name = "Command Prompt",
                Command = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Modes =
                [
                    new ModeDefinition
                    {
                        Id = "normal",
                        Name = "Normal",
                        Arguments = ["/c", "cd", "{path}"]
                    }
                ]
            }
        ]
    };
    var plan = new LaunchService().CreatePlan(config, "cmd", "normal", workingDirectory);
    Equal(Path.GetFullPath(workingDirectory), plan.WorkingDirectory, "工作目录");
    Equal(Path.GetFullPath(workingDirectory), plan.Arguments[2], "启动参数路径");
}

static void TestCommandStart()
{
    var root = Path.Combine(Path.GetTempPath(), "AiShellLauncher.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var script = Path.Combine(root, "capture.cmd");
        var output = Path.Combine(root, "working directory.txt");
        File.WriteAllText(script, "@echo off\r\n> \"%~1\" echo %CD%\r\n", System.Text.Encoding.ASCII);
        var config = new LauncherConfig
        {
            Tools =
            [
                new ToolDefinition
                {
                    Id = "capture",
                    Name = "Capture",
                    Command = script,
                    Modes = [new ModeDefinition { Id = "normal", Name = "Normal", Arguments = [output] }]
                }
            ]
        };

        var service = new LaunchService();
        var plan = service.CreatePlan(config, "capture", "normal", root);
        using var process = service.Start(plan);
        True(process.WaitForExit(10000), "命令在超时前结束");
        Equal(0, process.ExitCode, "命令退出码");
        True(File.Exists(output), "命令已运行");
        Equal(Path.GetFullPath(root), File.ReadAllText(output).Trim(), "命令工作目录");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestTerminalLaunch()
{
    var root = Path.Combine(Path.GetTempPath(), "AiShellLauncher.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var runner = Path.Combine(root, "runner.exe");
        File.WriteAllBytes(runner, []);
        var arguments = new[] { "--launch", "codex", "--path", root };
        var service = new TerminalLaunchService();

        var systemDefault = service.CreateStartInfo(new TerminalDefinition(), runner, arguments, root);
        True(systemDefault.UseShellExecute, "系统默认终端使用 Windows 关联");
        Equal(runner, systemDefault.FileName, "系统默认终端启动组件");
        Equal(arguments.Length, ArgumentTemplates.ParseDisplayText(systemDefault.Arguments).Count, "系统默认终端参数");

        var powerShell = service.CreateStartInfo(
            new TerminalDefinition { Kind = TerminalKind.PowerShell },
            runner,
            arguments,
            root);
        True(powerShell.FileName.EndsWith("powershell.exe", StringComparison.OrdinalIgnoreCase), "PowerShell 打开方式");
        True(ArgumentTemplates.ParseDisplayText(powerShell.Arguments).Contains("-Command"), "PowerShell 命令参数");

        var commandPrompt = service.CreateStartInfo(
            new TerminalDefinition { Kind = TerminalKind.CommandPrompt },
            runner,
            arguments,
            root);
        True(commandPrompt.FileName.EndsWith("cmd.exe", StringComparison.OrdinalIgnoreCase), "命令提示符打开方式");
        True(commandPrompt.Arguments.IndexOf(QuoteFragment(runner), StringComparison.Ordinal) >= 0, "命令提示符包含启动组件");

        var custom = service.CreateStartInfo(
            new TerminalDefinition
            {
                Kind = TerminalKind.Custom,
                Command = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                Arguments = ["--cwd", "{path}", "{command}", "{args}"]
            },
            runner,
            arguments,
            root);
        var customArguments = ArgumentTemplates.ParseDisplayText(custom.Arguments);
        Equal(root, customArguments[1], "自定义终端路径占位符");
        Equal(runner, customArguments[2], "自定义终端命令占位符");
        Equal(arguments[0], customArguments[3], "自定义终端参数占位符");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static void TestLegacyGeminiMigration()
{
    var root = Path.Combine(Path.GetTempPath(), "AiShellLauncher.Tests", Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(root);
    try
    {
        var legacyJson = @"{
  ""schemaVersion"": 4,
  ""tools"": [
    {
      ""id"": ""gemini"",
      ""name"": ""Gemini CLI"",
      ""command"": ""gemini"",
      ""enabled"": true,
      ""isBuiltIn"": true,
      ""modes"": [
        {
          ""id"": ""normal"",
          ""name"": ""普通"",
          ""menuTitle"": ""在 Gemini CLI 中打开"",
          ""iconPath"": """",
          ""enabled"": true,
          ""risk"": ""Normal"",
          ""arguments"": [""--approval-mode"", ""default""]
        },
        {
          ""id"": ""yolo"",
          ""name"": ""YOLO"",
          ""menuTitle"": ""把爱留给 Gemini CLI"",
          ""iconPath"": """",
          ""enabled"": true,
          ""risk"": ""Yolo"",
          ""arguments"": [""--yolo""]
        }
      ]
    }
  ]
}";
        File.WriteAllText(Path.Combine(root, "config.json"), legacyJson);
        var service = new ConfigService(root);
        var loaded = service.LoadOrCreate();
        var antigravity = loaded.Config.Tools.Single(t => t.Id == "antigravity");
        Equal("Antigravity CLI", antigravity.Name, "旧工具名称迁移");
        Equal("agy", antigravity.Command, "旧工具命令迁移");
        True(antigravity.Modes.Single(m => m.Id == "yolo").MenuTitle.Contains("Antigravity CLI"), "菜单标题迁移");
        True(antigravity.Modes.Single(m => m.Id == "yolo").Arguments.Contains("--dangerously-skip-permissions"), "YOLO参数迁移");
        True(BuiltinCatalog.GetDefaultIconPath("gemini") != null, "兼容旧工具ID获取图标");
        True(BuiltinCatalog.GetDefaultIconPath("agy") != null, "兼容agy ID获取图标");
    }
    finally
    {
        Directory.Delete(root, true);
    }
}

static string QuoteFragment(string value) => $"\"{value}\"";

static void True(bool condition, string label)
{
    if (!condition)
    {
        throw new InvalidOperationException(label);
    }
}

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}: expected={expected}, actual={actual}");
    }
}

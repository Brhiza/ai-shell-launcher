using AiShellLauncher.Core.Models;

namespace AiShellLauncher.Core.Services;

public static class BuiltinCatalog
{
    private static readonly HashSet<string> BuiltinToolIds = new(StringComparer.OrdinalIgnoreCase)
    {
        "codex", "claude", "gemini", "grok", "opencode", "openclaw", "hermes"
    };

    public static string? GetDefaultIconPath(string toolId)
    {
        if (!BuiltinToolIds.Contains(toolId))
        {
            return null;
        }

        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AiShellLauncher",
            "Runtime",
            "Icons",
            toolId.ToLowerInvariant() + ".brand.ico");
    }

    public static LauncherConfig CreateDefault()
    {
        return new LauncherConfig
        {
            Tools =
            [
                Tool("codex", "Codex", "codex",
                    Mode("normal", "普通", RiskLevel.Normal),
                    Mode("auto", "Auto", RiskLevel.Automatic,
                        "--sandbox", "workspace-write", "--ask-for-approval", "on-request"),
                    Mode("yolo", "YOLO", RiskLevel.Yolo, "--yolo")),

                Tool("claude", "Claude Code", "claude",
                    Mode("normal", "普通", RiskLevel.Normal),
                    Mode("auto", "Auto", RiskLevel.Automatic, "--permission-mode", "auto"),
                    Mode("yolo", "YOLO", RiskLevel.Yolo, "--dangerously-skip-permissions")),

                Tool("gemini", "Gemini CLI", "gemini",
                    Mode("normal", "普通", RiskLevel.Normal, "--approval-mode", "default"),
                    Mode("auto", "Auto", RiskLevel.Automatic, "--approval-mode", "auto_edit"),
                    Mode("yolo", "YOLO", RiskLevel.Yolo, "--yolo")),

                Tool("grok", "Grok Build", "grok",
                    Mode("normal", "普通", RiskLevel.Normal),
                    Mode("auto", "Auto", RiskLevel.Automatic, "--permission-mode", "auto"),
                    Mode("yolo", "YOLO", RiskLevel.Yolo, "--permission-mode", "bypassPermissions")),

                Tool("opencode", "OpenCode", "opencode",
                    Mode("normal", "普通", RiskLevel.Normal),
                    Mode("yolo", "自动批准（高风险）", RiskLevel.Yolo, "--auto")),

                Tool("openclaw", "OpenClaw", "openclaw",
                    Mode("normal", "普通", RiskLevel.Normal)),

                Tool("hermes", "Hermes", "hermes",
                    Mode("normal", "普通", RiskLevel.Normal))
            ]
        };
    }

    private static ToolDefinition Tool(string id, string name, string command, params ModeDefinition[] modes)
    {
        foreach (var mode in modes)
        {
            mode.MenuTitle = mode.Id.Equals("normal", StringComparison.OrdinalIgnoreCase)
                ? $"在 {name} 中打开"
                : $"在 {name} 中打开（{mode.Name}）";
        }

        return new ToolDefinition
        {
            Id = id,
            Name = name,
            Command = command,
            IsBuiltIn = true,
            Modes = [.. modes]
        };
    }

    private static ModeDefinition Mode(string id, string name, RiskLevel risk, params string[] arguments)
    {
        return new ModeDefinition
        {
            Id = id,
            Name = name,
            Enabled = risk == RiskLevel.Normal,
            Risk = risk,
            Arguments = [.. arguments]
        };
    }
}

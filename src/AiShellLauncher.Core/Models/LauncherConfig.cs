using System.Text.Json.Serialization;

namespace AiShellLauncher.Core.Models;

public sealed class LauncherConfig
{
    public int SchemaVersion { get; set; } = 4;

    public List<ToolDefinition> Tools { get; set; } = [];

    public TerminalDefinition Terminal { get; set; } = new();
}

public sealed class TerminalDefinition
{
    public TerminalKind Kind { get; set; } = TerminalKind.SystemDefault;

    public string Command { get; set; } = string.Empty;

    public List<string> Arguments { get; set; } = ["{command}", "{args}"];
}

public sealed class ToolDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Command { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public bool IsBuiltIn { get; set; }

    public List<ModeDefinition> Modes { get; set; } = [];
}

public sealed class ModeDefinition
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string MenuTitle { get; set; } = string.Empty;

    public string IconPath { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public RiskLevel Risk { get; set; } = RiskLevel.Normal;

    public List<string> Arguments { get; set; } = [];
}

[JsonConverter(typeof(JsonStringEnumConverter<RiskLevel>))]
public enum RiskLevel
{
    Normal,
    Automatic,
    Yolo
}

[JsonConverter(typeof(JsonStringEnumConverter<TerminalKind>))]
public enum TerminalKind
{
    SystemDefault,
    WindowsTerminal,
    PowerShell,
    CommandPrompt,
    Custom
}

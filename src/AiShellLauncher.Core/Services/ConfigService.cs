using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using AiShellLauncher.Core.Models;

namespace AiShellLauncher.Core.Services;

public sealed class ConfigService
{
    public const int MenuSlotLimit = 16;

    private readonly CommandResolver _commandResolver;

    public ConfigService(string? configRoot = null, CommandResolver? commandResolver = null)
    {
        ConfigRoot = configRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AiShellLauncher");
        _commandResolver = commandResolver ?? new CommandResolver();
    }

    public string ConfigRoot { get; }

    public string ConfigPath => Path.Combine(ConfigRoot, "config.json");

    public string MenuIndexPath => Path.Combine(ConfigRoot, "menu.tsv");

    public ConfigLoadResult LoadOrCreate()
    {
        Directory.CreateDirectory(ConfigRoot);
        if (!File.Exists(ConfigPath))
        {
            var created = BuiltinCatalog.CreateDefault();
            Save(created);
            return new ConfigLoadResult(created, null);
        }

        try
        {
            var json = File.ReadAllText(ConfigPath, Encoding.UTF8);
            var config = JsonSerializer.Deserialize(json, LauncherJsonContext.Default.LauncherConfig)
                ?? throw new InvalidDataException("配置文件内容为空。");
            if (config.SchemaVersion is >= 1 and <= 3)
            {
                MigrateToVersion4(config);
                Save(config);
                return new ConfigLoadResult(config, null);
            }
            Validate(config);
            WriteMenuIndex(config);
            return new ConfigLoadResult(config, null);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            return new ConfigLoadResult(BuiltinCatalog.CreateDefault(), $"配置文件无效，尚未覆盖原文件：{exception.Message}");
        }
    }

    public void Save(LauncherConfig config)
    {
        Validate(config);
        Directory.CreateDirectory(ConfigRoot);
        var json = JsonSerializer.Serialize(config, LauncherJsonContext.Default.LauncherConfig) + Environment.NewLine;
        WriteAtomically(ConfigPath, json, new UTF8Encoding(false));
        WriteMenuIndex(config);
    }

    public void WriteMenuIndex(LauncherConfig config)
    {
        Directory.CreateDirectory(ConfigRoot);
        var lines = new List<string> { "2" };
        var slot = 0;

        foreach (var tool in config.Tools.Where(tool => tool.Enabled))
        {
            if (_commandResolver.Resolve(tool.Command) is null)
            {
                continue;
            }

            foreach (var mode in tool.Modes.Where(mode => mode.Enabled))
            {
                if (slot >= MenuSlotLimit)
                {
                    throw new InvalidDataException($"右键菜单最多可启用 {MenuSlotLimit} 个选项，请先停用不常用的选项。");
                }

                var defaultTitle = mode.Id.Equals("normal", StringComparison.OrdinalIgnoreCase)
                    ? $"在 {tool.Name} 中打开"
                    : $"在 {tool.Name} 中打开（{mode.Name}）";
                var title = string.IsNullOrWhiteSpace(mode.MenuTitle) ? defaultTitle : mode.MenuTitle;
                var iconPath = string.IsNullOrWhiteSpace(mode.IconPath) && tool.IsBuiltIn
                    ? BuiltinCatalog.GetDefaultIconPath(tool.Id) ?? string.Empty
                    : mode.IconPath;
                lines.Add($"C\t{slot}\t{CleanField(tool.Id)}\t{CleanField(mode.Id)}\t{CleanField(title)}\t{mode.Risk}\t{CleanField(iconPath)}");
                slot++;
            }
        }

        WriteAtomically(MenuIndexPath, string.Join("\n", lines) + "\n", new UTF8Encoding(false));
    }

    public static void Validate(LauncherConfig config)
    {
        if (config.SchemaVersion != 4)
        {
            throw new InvalidDataException($"不支持的配置版本：{config.SchemaVersion}");
        }

        config.Terminal ??= new TerminalDefinition();
        if (config.Terminal.Kind == TerminalKind.Custom)
        {
            if (string.IsNullOrWhiteSpace(config.Terminal.Command))
            {
                throw new InvalidDataException("自定义终端缺少启动程序。");
            }
            if (!config.Terminal.Arguments.Contains("{command}", StringComparer.OrdinalIgnoreCase) ||
                !config.Terminal.Arguments.Contains("{args}", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("自定义终端参数必须包含 {command} 和 {args}。");
            }
        }

        var toolIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var tool in config.Tools)
        {
            ValidateIdentifier(tool.Id, "工具 ID");
            if (!toolIds.Add(tool.Id))
            {
                throw new InvalidDataException($"工具 ID 重复：{tool.Id}");
            }

            if (string.IsNullOrWhiteSpace(tool.Name))
            {
                throw new InvalidDataException($"工具 {tool.Id} 缺少名称。");
            }

            if (string.IsNullOrWhiteSpace(tool.Command))
            {
                throw new InvalidDataException($"工具 {tool.Name} 缺少启动命令。");
            }

            var modeIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var mode in tool.Modes)
            {
                ValidateIdentifier(mode.Id, $"{tool.Name} 的模式 ID");
                if (!modeIds.Add(mode.Id))
                {
                    throw new InvalidDataException($"工具 {tool.Name} 的模式 ID 重复：{mode.Id}");
                }

                if (string.IsNullOrWhiteSpace(mode.Name))
                {
                    throw new InvalidDataException($"工具 {tool.Name} 中存在未命名模式。");
                }
            }
        }
    }

    private static void MigrateToVersion4(LauncherConfig config)
    {
        var disableElevatedModes = config.SchemaVersion == 1;
        config.SchemaVersion = 4;
        config.Terminal ??= new TerminalDefinition();
        foreach (var tool in config.Tools)
        {
            foreach (var mode in tool.Modes)
            {
                if (disableElevatedModes)
                {
                    mode.Enabled = mode.Risk == RiskLevel.Normal;
                }

                if (string.IsNullOrWhiteSpace(mode.MenuTitle))
                {
                    mode.MenuTitle = mode.Id.Equals("normal", StringComparison.OrdinalIgnoreCase)
                        ? $"在 {tool.Name} 中打开"
                        : $"在 {tool.Name} 中打开（{mode.Name}）";
                }
            }
        }
    }

    private static void ValidateIdentifier(string id, string label)
    {
        if (string.IsNullOrWhiteSpace(id) || id.Any(character => !(
            (character >= 'a' && character <= 'z') ||
            (character >= 'A' && character <= 'Z') ||
            (character >= '0' && character <= '9') ||
            character is '-' or '_')))
        {
            throw new InvalidDataException($"{label} 只能包含字母、数字、短横线和下划线：{id}");
        }
    }

    private static string CleanField(string value)
    {
        return value.Replace('\t', ' ').Replace('\r', ' ').Replace('\n', ' ').Trim();
    }

    private static void WriteAtomically(string destination, string content, Encoding encoding)
    {
        var temporary = destination + ".tmp";
        File.WriteAllText(temporary, content, encoding);
        ReplaceFile(temporary, destination);
    }

    private static void ReplaceFile(string source, string destination)
    {
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }
        File.Move(source, destination);
    }
}

public sealed record ConfigLoadResult(LauncherConfig Config, string? Warning);

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(LauncherConfig))]
internal partial class LauncherJsonContext : JsonSerializerContext;

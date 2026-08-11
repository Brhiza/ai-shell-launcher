using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using AiShellLauncher.Core.Models;
using AiShellLauncher.Core.Services;

namespace AiShellLauncher.App;

public sealed class ToolEditorViewModel : NotifyBase
{
    private string _id;
    private string _name;
    private string _command;
    private bool _enabled;
    private string _status = "尚未检测";

    public ToolEditorViewModel(ToolDefinition model)
    {
        _id = model.Id;
        _name = model.Name;
        _command = model.Command;
        _enabled = model.Enabled;
        IsBuiltIn = model.IsBuiltIn;
        Modes = new ObservableCollection<ModeEditorViewModel>(model.Modes.Select(mode => new ModeEditorViewModel(mode)));
    }

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public string Command
    {
        get => _command;
        set => SetField(ref _command, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set
        {
            if (SetField(ref _enabled, value))
            {
                RaisePropertyChanged(nameof(MenuSummary));
            }
        }
    }

    public bool IsBuiltIn { get; }

    public bool CanDelete => !IsBuiltIn;

    public string DisplayIconPath => IsBuiltIn
        ? BuiltinCatalog.GetDefaultIconPath(Id) ?? "pack://application:,,,/Assets/Logo.ico"
        : "pack://application:,,,/Assets/Logo.ico";

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public ObservableCollection<ModeEditorViewModel> Modes { get; }

    public string MenuSummary
    {
        get
        {
            var count = Modes.Count(mode => mode.Enabled);
            if (!Enabled)
            {
                return "已停用";
            }
            return count == 0 ? "没有启用菜单" : $"{count} 个菜单项";
        }
    }

    public ToolDefinition ToModel()
    {
        return new ToolDefinition
        {
            Id = Id.Trim(),
            Name = Name.Trim(),
            Command = Command.Trim(),
            Enabled = Enabled,
            IsBuiltIn = IsBuiltIn,
            Modes = Modes.Select(mode => mode.ToModel()).ToList()
        };
    }
}

public sealed class ModeEditorViewModel : NotifyBase
{
    private string _id;
    private string _name;
    private bool _enabled;
    private RiskLevel _risk;
    private string _menuTitle;
    private string _iconPath;
    private string _argumentsText;

    public ModeEditorViewModel(ModeDefinition model)
    {
        _id = model.Id;
        _name = model.Name;
        _enabled = model.Enabled;
        _risk = model.Risk;
        _menuTitle = model.MenuTitle;
        _iconPath = model.IconPath;
        _argumentsText = ArgumentTemplates.ToDisplayText(model.Arguments);
    }

    public static IReadOnlyList<RiskChoice> RiskChoices { get; } =
    [
        new(RiskLevel.Normal, "普通"),
        new(RiskLevel.Automatic, "自动"),
        new(RiskLevel.Yolo, "YOLO（高风险）")
    ];

    public string Id
    {
        get => _id;
        set => SetField(ref _id, value);
    }

    public string Name
    {
        get => _name;
        set => SetField(ref _name, value);
    }

    public bool Enabled
    {
        get => _enabled;
        set => SetField(ref _enabled, value);
    }

    public RiskLevel Risk
    {
        get => _risk;
        set
        {
            if (SetField(ref _risk, value))
            {
                RaisePropertyChanged(nameof(IsYolo));
            }
        }
    }

    public bool IsYolo => Risk == RiskLevel.Yolo;

    public string MenuTitle
    {
        get => _menuTitle;
        set => SetField(ref _menuTitle, value);
    }

    public string IconPath
    {
        get => _iconPath;
        set
        {
            if (SetField(ref _iconPath, value))
            {
                RaisePropertyChanged(nameof(IconDisplay));
            }
        }
    }

    public string IconDisplay => string.IsNullOrWhiteSpace(IconPath)
        ? "使用软件图标"
        : Path.GetFileName(IconPath);

    public string ArgumentsText
    {
        get => _argumentsText;
        set => SetField(ref _argumentsText, value);
    }

    public ModeDefinition ToModel()
    {
        return new ModeDefinition
        {
            Id = Id.Trim(),
            Name = Name.Trim(),
            Enabled = Enabled,
            Risk = Risk,
            MenuTitle = MenuTitle.Trim(),
            IconPath = IconPath.Trim(),
            Arguments = ArgumentTemplates.ParseDisplayText(ArgumentsText).ToList()
        };
    }
}

public sealed record RiskChoice(RiskLevel Value, string Label);

public sealed class TerminalEditorViewModel : NotifyBase
{
    private TerminalKind _kind;
    private string _command;
    private string _argumentsText;
    private string _status = string.Empty;

    public TerminalEditorViewModel(TerminalDefinition model)
    {
        _kind = model.Kind;
        _command = model.Command;
        _argumentsText = ArgumentTemplates.ToDisplayText(model.Arguments);
    }

    public static IReadOnlyList<TerminalChoice> Choices { get; } =
    [
        new(TerminalKind.SystemDefault, "系统默认"),
        new(TerminalKind.WindowsTerminal, "Windows Terminal"),
        new(TerminalKind.PowerShell, "PowerShell"),
        new(TerminalKind.CommandPrompt, "命令提示符"),
        new(TerminalKind.Custom, "自定义终端")
    ];

    public TerminalKind Kind
    {
        get => _kind;
        set
        {
            if (SetField(ref _kind, value))
            {
                RaisePropertyChanged(nameof(IsCustom));
                RaisePropertyChanged(nameof(Description));
            }
        }
    }

    public bool IsCustom => Kind == TerminalKind.Custom;

    public string Description => Kind switch
    {
        TerminalKind.SystemDefault => "使用 Windows 当前设置的默认终端。",
        TerminalKind.WindowsTerminal => "始终使用 Windows Terminal。",
        TerminalKind.PowerShell => "始终在 Windows PowerShell 窗口中打开。",
        TerminalKind.CommandPrompt => "始终在命令提示符窗口中打开。",
        TerminalKind.Custom => "使用你指定的终端程序。",
        _ => string.Empty
    };

    public string Command
    {
        get => _command;
        set => SetField(ref _command, value);
    }

    public string ArgumentsText
    {
        get => _argumentsText;
        set => SetField(ref _argumentsText, value);
    }

    public string Status
    {
        get => _status;
        set => SetField(ref _status, value);
    }

    public TerminalDefinition ToModel()
    {
        return new TerminalDefinition
        {
            Kind = Kind,
            Command = Command.Trim(),
            Arguments = ArgumentTemplates.ParseDisplayText(ArgumentsText).ToList()
        };
    }

    public static string GetLabel(TerminalKind kind)
    {
        return Choices.First(choice => choice.Value == kind).Label;
    }
}

public sealed record TerminalChoice(TerminalKind Value, string Label);

public abstract class NotifyBase : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

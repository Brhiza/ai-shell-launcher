using System.Windows;
using AiShellLauncher.Core.Models;
using AiShellLauncher.Core.Services;
using Microsoft.Win32;

namespace AiShellLauncher.App;

public partial class ToolEditorDialog : Window
{
    private readonly CommandResolver _commandResolver;

    public ToolEditorDialog(ToolEditorViewModel tool, CommandResolver commandResolver)
    {
        InitializeComponent();
        Tool = tool;
        _commandResolver = commandResolver;
        DataContext = Tool;
        Detect();
    }

    public ToolEditorViewModel Tool { get; }

    private void BrowseCommand_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择启动程序或命令脚本",
            Filter = "程序和命令脚本|*.exe;*.cmd;*.bat;*.com;*.ps1|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            Tool.Command = dialog.FileName;
            Detect();
        }
    }

    private void Detect_Click(object sender, RoutedEventArgs e) => Detect();

    private void Detect()
    {
        var resolved = _commandResolver.Resolve(Tool.Command);
        Tool.Status = resolved is null ? "未找到这个程序，启用后不会显示菜单。" : "程序可用";
    }

    private void AddMode_Click(object sender, RoutedEventArgs e)
    {
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 6);
        Tool.Modes.Add(new ModeEditorViewModel(new ModeDefinition
        {
            Id = $"custom-{suffix}",
            Name = "自定义",
            MenuTitle = $"在 {Tool.Name} 中打开",
            Risk = RiskLevel.Normal
        }));
    }

    private void DeleteMode_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ModeEditorViewModel mode })
        {
            Tool.Modes.Remove(mode);
        }
    }

    private void BrowseIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ModeEditorViewModel mode })
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "选择右键菜单图标",
            Filter = "图标或程序|*.ico;*.exe;*.dll|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            mode.IconPath = dialog.FileName;
        }
    }

    private void ClearIcon_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: ModeEditorViewModel mode })
        {
            mode.IconPath = string.Empty;
        }
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(Tool.Name))
        {
            MessageBox.Show("请填写名称。", "无法完成", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(Tool.Command))
        {
            MessageBox.Show("请选择启动程序或填写命令。", "无法完成", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DialogResult = true;
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

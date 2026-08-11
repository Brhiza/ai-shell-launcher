using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using AiShellLauncher.Core.Models;
using AiShellLauncher.Core.Services;

namespace AiShellLauncher.App;

public partial class MainWindow : Window
{
    private readonly ConfigService _configService = new();
    private readonly CommandResolver _commandResolver = new();
    private TerminalDefinition _terminal = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = this;

        var loadResult = _configService.LoadOrCreate();
        _terminal = CloneTerminal(loadResult.Config.Terminal);
        UpdateTerminalButton();
        Tools = new ObservableCollection<ToolEditorViewModel>(loadResult.Config.Tools.Select(tool => new ToolEditorViewModel(tool)));
        DetectAll();

        if (!string.IsNullOrWhiteSpace(loadResult.Warning))
        {
            MessageBox.Show(loadResult.Warning, "配置未加载", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public ObservableCollection<ToolEditorViewModel> Tools { get; }

    private void AddTool_Click(object sender, RoutedEventArgs e)
    {
        var suffix = Guid.NewGuid().ToString("N").Substring(0, 8);
        var tool = new ToolEditorViewModel(new ToolDefinition
        {
            Id = $"custom-{suffix}",
            Name = "自定义命令",
            Command = "pwsh",
            IsBuiltIn = false,
            Modes =
            [
                new ModeDefinition
                {
                    Id = "normal",
                    Name = "普通",
                    MenuTitle = "在自定义命令中打开"
                }
            ]
        });

        var dialog = new ToolEditorDialog(tool, _commandResolver) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            Detect(tool);
            Tools.Add(tool);
        }
    }

    private void EditTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ToolEditorViewModel original })
        {
            return;
        }

        var edited = new ToolEditorViewModel(original.ToModel());
        var dialog = new ToolEditorDialog(edited, _commandResolver) { Owner = this };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        Detect(edited);
        var index = Tools.IndexOf(original);
        Tools[index] = edited;
    }

    private void Terminal_Click(object sender, RoutedEventArgs e)
    {
        var editor = new TerminalEditorViewModel(CloneTerminal(_terminal));
        var dialog = new TerminalSettingsDialog(editor, _commandResolver) { Owner = this };
        if (dialog.ShowDialog() == true)
        {
            _terminal = editor.ToModel();
            UpdateTerminalButton();
        }
    }

    private void DeleteTool_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ToolEditorViewModel tool } || tool.IsBuiltIn)
        {
            return;
        }

        var result = MessageBox.Show(
            $"确定删除“{tool.Name}”吗？",
            "移除命令",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            return;
        }

        Tools.Remove(tool);
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var config = new LauncherConfig
            {
                Tools = Tools.Select(tool => tool.ToModel()).ToList(),
                Terminal = CloneTerminal(_terminal)
            };
            _configService.Save(config);
            var activeSlotCount = File.ReadLines(_configService.MenuIndexPath).Count(line => line.StartsWith("C\t", StringComparison.Ordinal));
            var installResult = RuntimeInstaller.EnsureInstalled(activeSlotCount);
            DetectAll();
            if (installResult.RegistrationChanged)
            {
                var restart = MessageBox.Show(
                    "已保存。菜单数量发生变化，需要重启一次资源管理器才能完成更新，是否现在重启？\n\n已打开的文件夹窗口会关闭。",
                    "AI Shell Launcher",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (restart == MessageBoxResult.Yes)
                {
                    RuntimeInstaller.RestartExplorer();
                }
            }
            else
            {
                MessageBox.Show("已保存。", "AI Shell Launcher", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException)
        {
            MessageBox.Show(exception.Message, "无法保存", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void DetectAll()
    {
        foreach (var tool in Tools)
        {
            Detect(tool);
        }
    }

    private void Detect(ToolEditorViewModel tool)
    {
        tool.Status = _commandResolver.Resolve(tool.Command) is null ? "未找到程序" : "可以使用";
    }

    private void UpdateTerminalButton()
    {
        TerminalButton.Content = $"终端：{TerminalEditorViewModel.GetLabel(_terminal.Kind)}";
    }

    private static TerminalDefinition CloneTerminal(TerminalDefinition terminal)
    {
        return new TerminalDefinition
        {
            Kind = terminal.Kind,
            Command = terminal.Command,
            Arguments = [.. terminal.Arguments]
        };
    }
}

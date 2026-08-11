using System.IO;
using System.Windows;
using AiShellLauncher.Core.Models;
using AiShellLauncher.Core.Services;
using Microsoft.Win32;

namespace AiShellLauncher.App;

public partial class TerminalSettingsDialog : Window
{
    private readonly CommandResolver _commandResolver;

    public TerminalSettingsDialog(TerminalEditorViewModel editor, CommandResolver commandResolver)
    {
        InitializeComponent();
        Editor = editor;
        _commandResolver = commandResolver;
        DataContext = Editor;
        Detect();
    }

    public TerminalEditorViewModel Editor { get; }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "选择终端程序",
            Filter = "程序|*.exe;*.cmd;*.bat;*.com|所有文件|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog(this) == true)
        {
            Editor.Command = dialog.FileName;
            Detect();
        }
    }

    private void Detect_Click(object sender, RoutedEventArgs e) => Detect();

    private void Detect()
    {
        Editor.Status = string.IsNullOrWhiteSpace(Editor.Command)
            ? "尚未选择终端程序"
            : _commandResolver.Resolve(Editor.Command) is null ? "未找到这个程序" : "程序可用";
    }

    private void Confirm_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var terminal = Editor.ToModel();
            ConfigService.Validate(new LauncherConfig { Terminal = terminal });
            if (terminal.Kind == TerminalKind.Custom && _commandResolver.Resolve(terminal.Command) is null)
            {
                MessageBox.Show("没有找到这个终端程序。", "无法完成", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }
        catch (Exception exception) when (exception is InvalidDataException or FormatException)
        {
            MessageBox.Show(exception.Message, "无法完成", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}

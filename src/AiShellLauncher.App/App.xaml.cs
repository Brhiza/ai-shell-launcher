using System.IO;
using System.Windows;
using AiShellLauncher.Core.Services;

namespace AiShellLauncher.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            if (e.Args.Contains("--run", StringComparer.OrdinalIgnoreCase))
            {
                StartConfiguredTerminal(e.Args);
                Shutdown(0);
                return;
            }

            if (e.Args.Contains("--uninstall", StringComparer.OrdinalIgnoreCase))
            {
                RuntimeInstaller.Unregister();
                if (e.Args.Contains("--restart-explorer", StringComparer.OrdinalIgnoreCase))
                {
                    RuntimeInstaller.RestartExplorer();
                }
                Shutdown(0);
                return;
            }

            var configService = new ConfigService();
            _ = configService.LoadOrCreate();
            var activeSlotCount = File.ReadLines(configService.MenuIndexPath).Count(line => line.StartsWith("C\t", StringComparison.Ordinal));
            var installResult = RuntimeInstaller.EnsureInstalled(
                activeSlotCount,
                e.Args.Contains("--install", StringComparer.OrdinalIgnoreCase));
            if (e.Args.Contains("--install", StringComparer.OrdinalIgnoreCase))
            {
                if (e.Args.Contains("--restart-explorer", StringComparer.OrdinalIgnoreCase))
                {
                    RuntimeInstaller.RestartExplorer();
                }
                Shutdown(0);
                return;
            }

            var window = new MainWindow();
            MainWindow = window;
            window.Show();
            if (installResult.RegistrationChanged)
            {
                var restart = MessageBox.Show(
                    "右键菜单已经安装或更新。需要重启一次资源管理器才能完成加载，是否现在重启？\n\n已打开的文件夹窗口会关闭。",
                    "AI Shell Launcher",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);
                if (restart == MessageBoxResult.Yes)
                {
                    RuntimeInstaller.RestartExplorer();
                }
            }
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"AI Shell Launcher 启动失败。\n\n{exception.Message}",
                "AI Shell Launcher",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    private static void StartConfiguredTerminal(string[] arguments)
    {
        static string ReadValue(string[] values, string option)
        {
            var index = Array.FindIndex(values, value => value.Equals(option, StringComparison.OrdinalIgnoreCase));
            if (index < 0 || index + 1 >= values.Length || string.IsNullOrWhiteSpace(values[index + 1]))
            {
                throw new ArgumentException($"启动参数缺少值：{option}");
            }
            return values[index + 1];
        }

        var toolId = ReadValue(arguments, "--run");
        var modeId = ReadValue(arguments, "--mode");
        var workingDirectory = ReadValue(arguments, "--path");
        var loadResult = new ConfigService().LoadOrCreate();
        if (!string.IsNullOrWhiteSpace(loadResult.Warning))
        {
            throw new InvalidDataException(loadResult.Warning);
        }

        var runner = Path.Combine(RuntimeInstaller.RuntimeRoot, "AiShellLauncher.Runner.exe");
        var runnerArguments = new[] { "--launch", toolId, "--mode", modeId, "--path", workingDirectory };
        using var process = new TerminalLaunchService().Start(
            loadResult.Config.Terminal,
            runner,
            runnerArguments,
            workingDirectory);
    }
}

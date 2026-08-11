using System.Diagnostics;
using System.Text;
using AiShellLauncher.Core.Models;

namespace AiShellLauncher.Core.Services;

public sealed class TerminalLaunchService
{
    private readonly CommandResolver _commandResolver;

    public TerminalLaunchService(CommandResolver? commandResolver = null)
    {
        _commandResolver = commandResolver ?? new CommandResolver();
    }

    public ProcessStartInfo CreateStartInfo(
        TerminalDefinition terminal,
        string runnerPath,
        IReadOnlyList<string> runnerArguments,
        string workingDirectory)
    {
        if (!File.Exists(runnerPath))
        {
            throw new FileNotFoundException("启动组件不存在。", runnerPath);
        }
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"工作目录不存在：{workingDirectory}");
        }

        var directory = Path.GetFullPath(workingDirectory);
        return terminal.Kind switch
        {
            TerminalKind.SystemDefault => CreateSystemDefault(runnerPath, runnerArguments, directory),
            TerminalKind.WindowsTerminal => CreateWindowsTerminal(runnerPath, runnerArguments, directory),
            TerminalKind.PowerShell => CreatePowerShell(runnerPath, runnerArguments, directory),
            TerminalKind.CommandPrompt => CreateCommandPrompt(runnerPath, runnerArguments, directory),
            TerminalKind.Custom => CreateCustom(terminal, runnerPath, runnerArguments, directory),
            _ => throw new InvalidDataException($"不支持的终端类型：{terminal.Kind}")
        };
    }

    public Process Start(
        TerminalDefinition terminal,
        string runnerPath,
        IReadOnlyList<string> runnerArguments,
        string workingDirectory)
    {
        var startInfo = CreateStartInfo(terminal, runnerPath, runnerArguments, workingDirectory);
        return Process.Start(startInfo) ?? throw new InvalidOperationException("无法打开终端。");
    }

    private static ProcessStartInfo CreateSystemDefault(
        string runnerPath,
        IReadOnlyList<string> runnerArguments,
        string workingDirectory)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = runnerPath,
            WorkingDirectory = workingDirectory,
            UseShellExecute = true
        };
        AddArguments(startInfo, runnerArguments);
        return startInfo;
    }

    private ProcessStartInfo CreateWindowsTerminal(
        string runnerPath,
        IReadOnlyList<string> runnerArguments,
        string workingDirectory)
    {
        var executable = _commandResolver.Resolve("wt")
            ?? throw new FileNotFoundException("没有找到 Windows Terminal。可改用“系统默认”。");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-d");
        startInfo.ArgumentList.Add(workingDirectory);
        startInfo.ArgumentList.Add(runnerPath);
        AddArguments(startInfo, runnerArguments);
        return startInfo;
    }

    private static ProcessStartInfo CreatePowerShell(
        string runnerPath,
        IReadOnlyList<string> runnerArguments,
        string workingDirectory)
    {
        var executable = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-Command");
        startInfo.ArgumentList.Add(BuildPowerShellCommand(runnerPath, runnerArguments));
        return startInfo;
    }

    private static ProcessStartInfo CreateCommandPrompt(
        string runnerPath,
        IReadOnlyList<string> runnerArguments,
        string workingDirectory)
    {
        var commandLine = BuildCmdCommandLine(runnerPath, runnerArguments);
        return new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            Arguments = $"/d /s /c \"{commandLine}\""
        };
    }

    private ProcessStartInfo CreateCustom(
        TerminalDefinition terminal,
        string runnerPath,
        IReadOnlyList<string> runnerArguments,
        string workingDirectory)
    {
        var executable = _commandResolver.Resolve(terminal.Command)
            ?? throw new FileNotFoundException($"没有找到自定义终端：{terminal.Command}");
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false
        };

        foreach (var template in terminal.Arguments)
        {
            if (template.Equals("{args}", StringComparison.OrdinalIgnoreCase))
            {
                AddArguments(startInfo, runnerArguments);
                continue;
            }

            startInfo.ArgumentList.Add(template
                .Replace("{command}", runnerPath, StringComparison.OrdinalIgnoreCase)
                .Replace("{path}", workingDirectory, StringComparison.OrdinalIgnoreCase));
        }
        return startInfo;
    }

    private static void AddArguments(ProcessStartInfo startInfo, IEnumerable<string> arguments)
    {
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }
    }

    private static string BuildPowerShellCommand(string runnerPath, IEnumerable<string> arguments)
    {
        return "& " + string.Join(' ', new[] { runnerPath }.Concat(arguments).Select(QuotePowerShellArgument));
    }

    private static string QuotePowerShellArgument(string value)
    {
        return "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
    }

    private static string BuildCmdCommandLine(string runnerPath, IEnumerable<string> arguments)
    {
        return string.Join(' ', new[] { runnerPath }.Concat(arguments).Select(QuoteCmdArgument));
    }

    private static string QuoteCmdArgument(string value)
    {
        var escaped = value.Replace("%", "%%", StringComparison.Ordinal).Replace("\"", "\"\"", StringComparison.Ordinal);
        var result = new StringBuilder(escaped.Length + 2);
        result.Append('"');
        result.Append(escaped);
        result.Append('"');
        return result.ToString();
    }
}

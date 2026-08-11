using System.Diagnostics;
using System.Text;
using AiShellLauncher.Core.Models;

namespace AiShellLauncher.Core.Services;

public sealed class LaunchService
{
    private readonly CommandResolver _commandResolver;

    public LaunchService(CommandResolver? commandResolver = null)
    {
        _commandResolver = commandResolver ?? new CommandResolver();
    }

    public LaunchPlan CreatePlan(LauncherConfig config, string toolId, string modeId, string workingDirectory)
    {
        if (!Directory.Exists(workingDirectory))
        {
            throw new DirectoryNotFoundException($"工作目录不存在：{workingDirectory}");
        }

        var tool = config.Tools.FirstOrDefault(item => item.Id.Equals(toolId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"找不到工具配置：{toolId}");
        if (!tool.Enabled)
        {
            throw new InvalidOperationException($"工具已停用：{tool.Name}");
        }

        var mode = tool.Modes.FirstOrDefault(item => item.Id.Equals(modeId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"找不到运行模式：{tool.Name} / {modeId}");
        if (!mode.Enabled)
        {
            throw new InvalidOperationException($"运行模式已停用：{tool.Name} / {mode.Name}");
        }

        var executable = _commandResolver.Resolve(tool.Command)
            ?? throw new FileNotFoundException($"没有找到 {tool.Name} 的启动命令：{tool.Command}");
        var arguments = ArgumentTemplates.Expand(mode.Arguments, Path.GetFullPath(workingDirectory));
        return new LaunchPlan(tool.Name, mode.Name, executable, arguments, Path.GetFullPath(workingDirectory), mode.Risk);
    }

    public Process Start(LaunchPlan plan)
    {
        var extension = Path.GetExtension(plan.Executable);
        ProcessStartInfo startInfo;
        if (extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false,
                Arguments = $"/d /s /c \"{BuildCmdCommandLine(plan.Executable, plan.Arguments)}\""
            };
        }
        else if (extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase))
        {
            startInfo = new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.SystemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe"),
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false,
                Arguments = WindowsCommandLine.Join(new[]
                {
                    "-NoProfile",
                    "-ExecutionPolicy",
                    "Bypass",
                    "-File",
                    plan.Executable
                }.Concat(plan.Arguments))
            };
        }
        else
        {
            startInfo = new ProcessStartInfo
            {
                FileName = plan.Executable,
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false,
                Arguments = WindowsCommandLine.Join(plan.Arguments)
            };
        }

        return Process.Start(startInfo) ?? throw new InvalidOperationException($"无法启动：{plan.Executable}");
    }

    private static string BuildCmdCommandLine(string executable, IReadOnlyList<string> arguments)
    {
        var parts = new List<string> { QuoteCmdArgument(executable) };
        parts.AddRange(arguments.Select(QuoteCmdArgument));
        return string.Join(" ", parts);
    }

    private static string QuoteCmdArgument(string value)
    {
        var escaped = value.Replace("%", "%%").Replace("\"", "\"\"");
        var result = new StringBuilder(escaped.Length + 2);
        result.Append('"');
        result.Append(escaped);
        result.Append('"');
        return result.ToString();
    }
}

public sealed record LaunchPlan(
    string ToolName,
    string ModeName,
    string Executable,
    IReadOnlyList<string> Arguments,
    string WorkingDirectory,
    RiskLevel Risk);

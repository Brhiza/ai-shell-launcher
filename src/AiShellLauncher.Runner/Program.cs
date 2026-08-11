using System.Text;
using AiShellLauncher.Core.Models;
using AiShellLauncher.Core.Services;

Console.InputEncoding = Encoding.UTF8;
Console.OutputEncoding = Encoding.UTF8;

try
{
    var options = ParseArguments(args);
    var loadResult = new ConfigService().LoadOrCreate();
    if (!string.IsNullOrWhiteSpace(loadResult.Warning))
    {
        Console.Error.WriteLine(loadResult.Warning);
        return 2;
    }

    var plan = new LaunchService().CreatePlan(
        loadResult.Config,
        options.ToolId,
        options.ModeId,
        options.WorkingDirectory);

    Console.Title = $"{plan.ToolName} · {plan.ModeName}";
    Console.WriteLine($"启动 {plan.ToolName} · {plan.ModeName}");
    Console.WriteLine($"目录：{plan.WorkingDirectory}");
    if (plan.Risk == RiskLevel.Yolo)
    {
        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine("警告：当前模式会绕过该工具的安全确认。");
        Console.ResetColor();
    }

    using var process = new LaunchService().Start(plan);
    await process.WaitForExitAsync();
    return process.ExitCode;
}
catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or IOException)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.Error.WriteLine($"启动失败：{exception.Message}");
    Console.ResetColor();
    return 1;
}

static RunnerOptions ParseArguments(string[] arguments)
{
    string? toolId = null;
    string? modeId = null;
    string? path = null;

    for (var index = 0; index < arguments.Length; index++)
    {
        var value = arguments[index];
        string ReadValue()
        {
            if (++index >= arguments.Length)
            {
                throw new ArgumentException($"参数缺少值：{value}");
            }

            return arguments[index];
        }

        switch (value.ToLowerInvariant())
        {
            case "--launch":
                toolId = ReadValue();
                break;
            case "--mode":
                modeId = ReadValue();
                break;
            case "--path":
                path = ReadValue();
                break;
            default:
                throw new ArgumentException($"未知参数：{value}");
        }
    }

    if (string.IsNullOrWhiteSpace(toolId) || string.IsNullOrWhiteSpace(modeId) || string.IsNullOrWhiteSpace(path))
    {
        throw new ArgumentException("用法：AiShellLauncher.Runner --launch <工具 ID> --mode <模式 ID> --path <文件夹>");
    }

    return new RunnerOptions(toolId, modeId, path);
}

internal sealed record RunnerOptions(string ToolId, string ModeId, string WorkingDirectory);

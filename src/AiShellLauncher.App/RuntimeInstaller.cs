using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using AiShellLauncher.Core.Services;

namespace AiShellLauncher.App;

internal sealed record RuntimeInstallResult(bool RegistrationChanged, string InstalledExecutable);

internal static class RuntimeInstaller
{
    private const int MenuSlotCount = 16;
    private const string LegacyPackageName = "AiShellLauncher.Local";
    private const string ManifestResourceName = "Runtime.MenuPackageManifest.xml";

    private static readonly (string ResourceName, string RelativePath)[] RuntimeResources =
    [
        ("Runtime.AiShellLauncher.Runner.exe", "AiShellLauncher.Runner.exe"),
        ("Runtime.AiShellLauncher.ShellExtension.dll", "AiShellLauncher.ShellExtension.dll"),
        ("Runtime.Assets.Logo.ico", Path.Combine("Assets", "Logo.ico")),
        ("Runtime.Assets.Logo.png", Path.Combine("Assets", "Logo.png")),
        ("Runtime.Icons.codex.ico", Path.Combine("Icons", "codex.brand.ico")),
        ("Runtime.Icons.claude.ico", Path.Combine("Icons", "claude.brand.ico")),
        ("Runtime.Icons.gemini.ico", Path.Combine("Icons", "gemini.brand.ico")),
        ("Runtime.Icons.grok.ico", Path.Combine("Icons", "grok.brand.ico")),
        ("Runtime.Icons.opencode.ico", Path.Combine("Icons", "opencode.brand.ico")),
        ("Runtime.Icons.openclaw.ico", Path.Combine("Icons", "openclaw.brand.ico")),
        ("Runtime.Icons.hermes.ico", Path.Combine("Icons", "hermes.brand.ico"))
    ];

    public static string RuntimeRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "AiShellLauncher",
        "Runtime");

    public static void VerifyEmbeddedPayload()
    {
        EnsureSupportedSystem();
        var assembly = Assembly.GetExecutingAssembly();
        foreach (var resource in RuntimeResources)
        {
            using var stream = assembly.GetManifestResourceStream(resource.ResourceName)
                ?? throw new InvalidOperationException($"安装资源缺失：{resource.ResourceName}");
            if (stream.Length == 0)
            {
                throw new InvalidOperationException($"安装资源为空：{resource.ResourceName}");
            }
        }

        var manifest = ReadManifestTemplate();
        if (!manifest.Contains("__SLOT__") || !manifest.Contains("__CLSID__"))
        {
            throw new InvalidDataException("右键菜单清单模板无效。");
        }

        using var notices = assembly.GetManifestResourceStream("Licenses.THIRD_PARTY_NOTICES.md")
            ?? throw new InvalidOperationException("第三方许可声明未打包。");
        if (notices.Length == 0)
        {
            throw new InvalidOperationException("第三方许可声明为空。");
        }
    }

    public static RuntimeInstallResult EnsureInstalled(int activeSlotCount, bool forceRegistration = false)
    {
        EnsureSupportedSystem();
        if (activeSlotCount is < 0 or > MenuSlotCount)
        {
            throw new ArgumentOutOfRangeException(nameof(activeSlotCount));
        }

        string sourceExecutable;
        using (var currentProcess = Process.GetCurrentProcess())
        {
            sourceExecutable = currentProcess.MainModule?.FileName
                ?? throw new InvalidOperationException("无法确定 AI Shell Launcher 的运行路径。");
        }
        Directory.CreateDirectory(RuntimeRoot);

        var installedExecutable = Path.Combine(RuntimeRoot, "AiShellLauncher.exe");
        var changed = CopyIfDifferent(sourceExecutable, installedExecutable);
        foreach (var resource in RuntimeResources)
        {
            changed |= ExtractIfDifferent(resource.ResourceName, Path.Combine(RuntimeRoot, resource.RelativePath));
        }
        if (SlotPayloadNeedsRefresh())
        {
            Unregister();
            changed = true;
        }
        changed |= WriteMenuManifests();

        var markerPath = Path.Combine(RuntimeRoot, ".registered");
        var marker = GetFileHash(installedExecutable) + Environment.NewLine +
            $"slots={activeSlotCount}" + Environment.NewLine +
            $"menu={DetectMenuStyle()}" + Environment.NewLine;
        var registrationChanged = forceRegistration || changed || !File.Exists(markerPath) ||
            !string.Equals(File.ReadAllText(markerPath), marker, StringComparison.Ordinal);
        if (registrationChanged)
        {
            RegisterSparsePackages(activeSlotCount);
            File.WriteAllText(markerPath, marker, new System.Text.UTF8Encoding(false));
        }

        return new RuntimeInstallResult(registrationChanged, installedExecutable);
    }

    public static void Unregister()
    {
        const string command = "$ErrorActionPreference='Stop'; " +
            "$packages=Get-AppxPackage | Where-Object {$_.Name -eq $env:ASL_LEGACY_PACKAGE -or $_.Name -like 'AiShellLauncher.Menu*.Local'}; " +
            "if($packages){$packages | Remove-AppxPackage -ErrorAction Stop}";
        RunWindowsPowerShell(command);
        StopShellSurrogates();
        var markerPath = Path.Combine(RuntimeRoot, ".registered");
        if (File.Exists(markerPath))
        {
            File.Delete(markerPath);
        }
    }

    public static void RestartExplorer()
    {
        foreach (var process in Process.GetProcessesByName("explorer"))
        {
            process.Kill();
            process.Dispose();
        }

        Thread.Sleep(1200);
        Process.Start(new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe"),
            UseShellExecute = true
        });
    }

    private static void RegisterSparsePackages(int activeSlotCount)
    {
        const string command = "$ErrorActionPreference='Stop'; " +
            "$packages=Get-AppxPackage | Where-Object {$_.Name -eq $env:ASL_LEGACY_PACKAGE -or $_.Name -like 'AiShellLauncher.Menu*.Local'}; " +
            "if($packages){$packages | Remove-AppxPackage -ErrorAction Stop}; " +
            "Get-CimInstance Win32_Process -Filter \"Name='dllhost.exe'\" -ErrorAction SilentlyContinue | " +
            "Where-Object {$_.CommandLine -like '*4B7A4183-5B26-46C7-A8BF-01CFE8B133*'} | " +
            "ForEach-Object {Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue}; " +
            "for($slot=0;$slot -lt [int]$env:ASL_ACTIVE_SLOTS;$slot++){" +
            "$manifest=Join-Path $env:ASL_RUNTIME_ROOT ('Manifests\\Slot{0:D2}\\AppxManifest.xml' -f $slot); " +
            "$slotRoot=Split-Path -Parent $manifest; " +
            "Add-AppxPackage -Register $manifest -ExternalLocation $slotRoot -ErrorAction Stop}";
        RunWindowsPowerShell(command, activeSlotCount);
    }

    private static bool WriteMenuManifests()
    {
        var template = ReadManifestTemplate();
        var changed = false;

        for (var slot = 0; slot < MenuSlotCount; slot++)
        {
            var slotText = slot.ToString("D2");
            var classId = $"4B7A4183-5B26-46C7-A8BF-01CFE8B133{slot:X2}";
            var content = BuildManifest(template, slotText, classId);
            var slotRoot = Path.Combine(RuntimeRoot, "Manifests", $"Slot{slotText}");
            var path = Path.Combine(slotRoot, "AppxManifest.xml");
            changed |= WriteIfDifferent(path, content);
            changed |= CopyIfDifferent(
                Path.Combine(RuntimeRoot, "AiShellLauncher.ShellExtension.dll"),
                Path.Combine(slotRoot, "AiShellLauncher.ShellExtension.dll"));
            changed |= CopyIfDifferent(
                Path.Combine(RuntimeRoot, "Assets", "Logo.png"),
                Path.Combine(slotRoot, "Assets", "Logo.png"));
            changed |= CopyIfDifferent(
                Path.Combine(RuntimeRoot, "Assets", "Logo.ico"),
                Path.Combine(slotRoot, "Assets", "Logo.ico"));
        }

        return changed;
    }

    private static bool SlotPayloadNeedsRefresh()
    {
        var template = ReadManifestTemplate();
        var sourceExtension = Path.Combine(RuntimeRoot, "AiShellLauncher.ShellExtension.dll");
        var sourceLogo = Path.Combine(RuntimeRoot, "Assets", "Logo.png");
        var sourceIcon = Path.Combine(RuntimeRoot, "Assets", "Logo.ico");

        for (var slot = 0; slot < MenuSlotCount; slot++)
        {
            var slotText = slot.ToString("D2");
            var slotRoot = Path.Combine(RuntimeRoot, "Manifests", $"Slot{slotText}");
            var extension = Path.Combine(slotRoot, "AiShellLauncher.ShellExtension.dll");
            var logo = Path.Combine(slotRoot, "Assets", "Logo.png");
            var icon = Path.Combine(slotRoot, "Assets", "Logo.ico");
            var manifest = Path.Combine(slotRoot, "AppxManifest.xml");
            if (!File.Exists(extension) || !FilesMatch(sourceExtension, extension) ||
                !File.Exists(logo) || !FilesMatch(sourceLogo, logo) ||
                !File.Exists(icon) || !FilesMatch(sourceIcon, icon))
            {
                return true;
            }

            var classId = $"4B7A4183-5B26-46C7-A8BF-01CFE8B133{slot:X2}";
            var expectedManifest = new System.Text.UTF8Encoding(false).GetBytes(BuildManifest(template, slotText, classId));
            if (!File.Exists(manifest) || !File.ReadAllBytes(manifest).AsSpan().SequenceEqual(expectedManifest))
            {
                return true;
            }
        }

        return false;
    }

    private static string ReadManifestTemplate()
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(ManifestResourceName)
            ?? throw new InvalidOperationException($"安装资源缺失：{ManifestResourceName}");
        using var reader = new StreamReader(resource, System.Text.Encoding.UTF8, true);
        return reader.ReadToEnd();
    }

    private static string BuildManifest(string template, string slotText, string classId)
    {
        return template
            .Replace("__SLOT__", slotText)
            .Replace("__CLSID__", classId);
    }

    private static bool WriteIfDifferent(string destination, string content)
    {
        var bytes = new System.Text.UTF8Encoding(false).GetBytes(content);
        if (File.Exists(destination) && File.ReadAllBytes(destination).AsSpan().SequenceEqual(bytes))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".tmp";
        File.WriteAllBytes(temporary, bytes);
        ReplaceFile(temporary, destination);
        return true;
    }

    private static void StopShellSurrogates()
    {
        const string command = "$ErrorActionPreference='Stop'; " +
            "Get-CimInstance Win32_Process -Filter \"Name='dllhost.exe'\" -ErrorAction SilentlyContinue | " +
            "Where-Object {$_.CommandLine -like '*4B7A4183-5B26-46C7-A8BF-01CFE8B133*'} | " +
            "ForEach-Object {Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue}";
        RunWindowsPowerShell(command);
        Thread.Sleep(600);
    }

    private static void RunWindowsPowerShell(string command, int activeSlotCount = 0)
    {
        var powerShell = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.System),
            "WindowsPowerShell",
            "v1.0",
            "powershell.exe");
        var startInfo = new ProcessStartInfo
        {
            FileName = powerShell,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.EnvironmentVariables["ASL_LEGACY_PACKAGE"] = LegacyPackageName;
        startInfo.EnvironmentVariables["ASL_RUNTIME_ROOT"] = RuntimeRoot;
        startInfo.EnvironmentVariables["ASL_ACTIVE_SLOTS"] = activeSlotCount.ToString(System.Globalization.CultureInfo.InvariantCulture);
        startInfo.Arguments = WindowsCommandLine.Join(new[]
        {
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy",
            "Bypass",
            "-Command",
            command
        });
        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("无法启动 Windows PowerShell 完成右键菜单注册。");
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        Task.WaitAll(outputTask, errorTask);
        if (process.ExitCode != 0)
        {
            var detail = string.IsNullOrWhiteSpace(errorTask.Result) ? outputTask.Result : errorTask.Result;
            throw new InvalidOperationException($"右键菜单注册失败：{detail.Trim()}");
        }
    }

    private static bool CopyIfDifferent(string source, string destination)
    {
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }
        if (File.Exists(destination) && FilesMatch(source, destination))
        {
            return false;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".tmp";
        File.Copy(source, temporary, true);
        ReplaceFile(temporary, destination);
        return true;
    }

    private static bool ExtractIfDifferent(string resourceName, string destination)
    {
        using var resource = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"安装资源缺失：{resourceName}");
        if (File.Exists(destination) && StreamMatchesFile(resource, destination))
        {
            return false;
        }

        resource.Position = 0;
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        var temporary = destination + ".tmp";
        using (var output = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            resource.CopyTo(output);
        }
        ReplaceFile(temporary, destination);
        return true;
    }

    private static bool StreamMatchesFile(Stream source, string destination)
    {
        using var sourceHash = SHA256.Create();
        var expected = sourceHash.ComputeHash(source);
        using var file = File.OpenRead(destination);
        using var destinationHash = SHA256.Create();
        var actual = destinationHash.ComputeHash(file);
        return expected.SequenceEqual(actual);
    }

    private static bool FilesMatch(string first, string second)
    {
        return string.Equals(GetFileHash(first), GetFileHash(second), StringComparison.Ordinal);
    }

    private static string GetFileHash(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha256 = SHA256.Create();
        return BitConverter.ToString(sha256.ComputeHash(stream)).Replace("-", string.Empty);
    }

    private static void ReplaceFile(string source, string destination)
    {
        if (File.Exists(destination))
        {
            File.Delete(destination);
        }
        File.Move(source, destination);
    }

    private static void EnsureSupportedSystem()
    {
        var version = Environment.OSVersion.Version;
        if (version.Major < 10 || (version.Major == 10 && version.Build < 19041))
        {
            throw new PlatformNotSupportedException("需要 Windows 10 版本 2004 或更高版本，或 Windows 11。");
        }
    }

    private static string DetectMenuStyle()
    {
        return Environment.OSVersion.Version.Build >= 22000 ? "modern" : "classic";
    }
}

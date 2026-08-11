namespace AiShellLauncher.Core.Services;

public sealed class CommandResolver
{
    private static readonly string[] WindowsExecutableExtensions = [".exe", ".cmd", ".bat", ".com", ".ps1"];

    public string? Resolve(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var expanded = Environment.ExpandEnvironmentVariables(command.Trim().Trim('"'));
        if (Path.IsPathRooted(expanded) || expanded.Contains(Path.DirectorySeparatorChar) || expanded.Contains(Path.AltDirectorySeparatorChar))
        {
            return ResolveFile(expanded);
        }

        foreach (var directory in GetSearchDirectories())
        {
            foreach (var extension in GetExtensions(expanded))
            {
                var candidate = Path.Combine(directory, expanded + extension);
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }
        }

        return null;
    }

    private static string? ResolveFile(string path)
    {
        if (!string.IsNullOrEmpty(Path.GetExtension(path)))
        {
            return File.Exists(path) ? Path.GetFullPath(path) : null;
        }

        foreach (var extension in WindowsExecutableExtensions)
        {
            if (File.Exists(path + extension))
            {
                return Path.GetFullPath(path + extension);
            }
        }

        if (File.Exists(path))
        {
            return Path.GetFullPath(path);
        }

        return null;
    }

    private static IEnumerable<string> GetSearchDirectories()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var pathValue = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var segment in pathValue.Split(new[] { Path.PathSeparator }, StringSplitOptions.RemoveEmptyEntries).Select(value => value.Trim()))
        {
            var expanded = Environment.ExpandEnvironmentVariables(segment.Trim('"'));
            if (Directory.Exists(expanded) && seen.Add(expanded))
            {
                yield return expanded;
            }
        }

        var npmPrefix = Environment.GetEnvironmentVariable("NPM_CONFIG_PREFIX");
        if (!string.IsNullOrWhiteSpace(npmPrefix) && Directory.Exists(npmPrefix) && seen.Add(npmPrefix))
        {
            yield return npmPrefix;
        }

        var appDataNpm = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "npm");
        if (Directory.Exists(appDataNpm) && seen.Add(appDataNpm))
        {
            yield return appDataNpm;
        }
    }

    private static IEnumerable<string> GetExtensions(string command)
    {
        if (!string.IsNullOrEmpty(Path.GetExtension(command)))
        {
            yield return string.Empty;
            yield break;
        }

        foreach (var extension in WindowsExecutableExtensions)
        {
            yield return extension;
        }
    }
}

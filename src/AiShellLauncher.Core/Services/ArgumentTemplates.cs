using System.Text;

namespace AiShellLauncher.Core.Services;

public static class ArgumentTemplates
{
    public static IReadOnlyList<string> Expand(IEnumerable<string> arguments, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(workingDirectory))
        {
            throw new ArgumentException("工作目录不能为空。", nameof(workingDirectory));
        }

        return arguments.Select(argument => TextCompatibility.ReplaceOrdinalIgnoreCase(argument, "{path}", workingDirectory)).ToArray();
    }

    public static string ToDisplayText(IEnumerable<string> arguments)
    {
        return string.Join(" ", arguments.Select(QuoteForDisplay));
    }

    public static IReadOnlyList<string> ParseDisplayText(string text)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        var backslashCount = 0;

        void FlushBackslashes()
        {
            if (backslashCount > 0)
            {
                current.Append('\\', backslashCount);
                backslashCount = 0;
            }
        }

        foreach (var character in text)
        {
            if (character == '\\')
            {
                backslashCount++;
                continue;
            }

            if (character == '"')
            {
                current.Append('\\', backslashCount / 2);
                if (backslashCount % 2 == 1)
                {
                    current.Append('"');
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                backslashCount = 0;
                continue;
            }

            FlushBackslashes();
            if (char.IsWhiteSpace(character) && !inQuotes)
            {
                if (current.Length > 0)
                {
                    result.Add(current.ToString());
                    current.Clear();
                }
            }
            else
            {
                current.Append(character);
            }
        }

        FlushBackslashes();
        if (inQuotes)
        {
            throw new FormatException("参数中存在未闭合的双引号。");
        }

        if (current.Length > 0)
        {
            result.Add(current.ToString());
        }

        return result;
    }

    private static string QuoteForDisplay(string value)
    {
        if (value.Length > 0 && value.All(character => !char.IsWhiteSpace(character) && character != '"'))
        {
            return value;
        }

        var result = new StringBuilder("\"");
        var backslashes = 0;
        foreach (var character in value)
        {
            if (character == '\\')
            {
                backslashes++;
                continue;
            }

            if (character == '"')
            {
                result.Append('\\', backslashes * 2 + 1);
                result.Append('"');
                backslashes = 0;
                continue;
            }

            result.Append('\\', backslashes);
            backslashes = 0;
            result.Append(character);
        }

        result.Append('\\', backslashes * 2);
        result.Append('"');
        return result.ToString();
    }
}

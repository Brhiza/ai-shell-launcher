using System.Text;

namespace AiShellLauncher.Core.Services;

internal static class TextCompatibility
{
    public static string ReplaceOrdinalIgnoreCase(string value, string oldValue, string newValue)
    {
        var start = 0;
        var match = value.IndexOf(oldValue, StringComparison.OrdinalIgnoreCase);
        if (match < 0)
        {
            return value;
        }

        var result = new StringBuilder(value.Length);
        while (match >= 0)
        {
            result.Append(value, start, match - start);
            result.Append(newValue);
            start = match + oldValue.Length;
            match = value.IndexOf(oldValue, start, StringComparison.OrdinalIgnoreCase);
        }

        result.Append(value, start, value.Length - start);
        return result.ToString();
    }
}

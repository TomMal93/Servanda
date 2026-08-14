using System.Text.RegularExpressions;

namespace Servanda.Domain.Prompts;

/// <summary>
/// Reguły znacznika <c>{{nazwa}}</c> wspólne dla edytora promptu i Prompt Studio.
/// </summary>
public static partial class PromptTemplate
{
    public static IReadOnlyList<string> ExtractPlaceholders(string? content)
    {
        if (string.IsNullOrEmpty(content))
        {
            return [];
        }

        var names = new List<string>();
        foreach (Match match in PlaceholderPattern().Matches(content))
        {
            var name = match.Groups[1].Value;
            if (!names.Contains(name, StringComparer.Ordinal))
            {
                names.Add(name);
            }
        }

        return names;
    }

    public static string Render(string? content, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (string.IsNullOrEmpty(content))
        {
            return string.Empty;
        }

        return PlaceholderPattern().Replace(
            content,
            match => values.TryGetValue(match.Groups[1].Value, out var value) ? value : string.Empty);
    }

    [GeneratedRegex(@"\{\{\s*([A-Za-z_][A-Za-z0-9_-]{0,49})\s*\}\}")]
    private static partial Regex PlaceholderPattern();
}

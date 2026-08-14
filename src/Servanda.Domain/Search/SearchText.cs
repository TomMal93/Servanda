using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Servanda.Domain.Search;

/// <summary>
/// Jedyna reguła normalizacji tekstu indeksowanego i zapytania zgodna z ADR 0003.
/// </summary>
public static partial class SearchText
{
    private const int MinimumQueryLength = 2;

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var lowered = value.ToLowerInvariant();
        var mapped = new StringBuilder(lowered.Length);
        foreach (var character in lowered)
        {
            mapped.Append(MapPolishLetter(character));
        }

        var decomposed = mapped.ToString().Normalize(NormalizationForm.FormKD);
        var stripped = new StringBuilder(decomposed.Length);
        foreach (var character in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            stripped.Append(char.IsLetterOrDigit(character) ? character : ' ');
        }

        return SeparatorPattern().Replace(stripped.ToString(), " ").Trim();
    }

    public static IReadOnlyList<string> Tokenize(string? value)
    {
        var normalized = Normalize(value);
        return normalized.Length == 0
            ? []
            : normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>
    /// Zamienia zwykły tekst użytkownika na koniunkcję prefiksów FTS5.
    /// Zwraca <c>null</c>, gdy zapytanie jest zbyt krótkie i nie powinno być wykonane.
    /// </summary>
    public static string? BuildPrefixQuery(string? value)
    {
        var normalized = Normalize(value);
        if (normalized.Length < MinimumQueryLength)
        {
            return null;
        }

        var tokens = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var token in tokens)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ');
            }

            builder.Append('"').Append(token).Append("\"*");
        }

        return builder.ToString();
    }

    public static bool IsQueryTooShort(string? value) =>
        Normalize(value).Length is > 0 and < MinimumQueryLength;

    private static char MapPolishLetter(char character) => character switch
    {
        'ą' => 'a',
        'ć' => 'c',
        'ę' => 'e',
        'ł' => 'l',
        'ń' => 'n',
        'ó' => 'o',
        'ś' => 's',
        'ź' => 'z',
        'ż' => 'z',
        _ => character,
    };

    [GeneratedRegex(" {2,}")]
    private static partial Regex SeparatorPattern();
}

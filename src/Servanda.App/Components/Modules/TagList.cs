namespace Servanda.App.Components.Modules;

/// <summary>
/// Tagi są wpisywane jako lista rozdzielona przecinkami; nazwy pozostają danymi użytkownika.
/// </summary>
public static class TagList
{
    public static IReadOnlyList<string> Parse(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : [.. value
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.OrdinalIgnoreCase)];

    public static string Format(IReadOnlyList<string> names) => string.Join(", ", names);
}

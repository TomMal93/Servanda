using Servanda.Domain.Search;

namespace Servanda.Infrastructure.Data.Search;

/// <summary>
/// Wyjaśnia dopasowanie po polu niewidocznym na karcie, bez ujawniania prywatnej treści w wynikach.
/// </summary>
internal static class MatchExplanation
{
    public const string InTags = "Dopasowanie w tagach";
    public const string InContent = "Dopasowanie w treści";

    public static string? For(
        IReadOnlyList<string> queryTokens,
        string visibleText,
        string tagText)
    {
        if (queryTokens.Count == 0)
        {
            return null;
        }

        var visibleTokens = SearchText.Tokenize(visibleText);
        if (queryTokens.All(token => HasPrefix(visibleTokens, token)))
        {
            return null;
        }

        var tagTokens = SearchText.Tokenize(tagText);
        return queryTokens.All(token => HasPrefix(visibleTokens, token) || HasPrefix(tagTokens, token))
            ? InTags
            : InContent;
    }

    private static bool HasPrefix(IReadOnlyList<string> tokens, string prefix) =>
        tokens.Any(token => token.StartsWith(prefix, StringComparison.Ordinal));
}

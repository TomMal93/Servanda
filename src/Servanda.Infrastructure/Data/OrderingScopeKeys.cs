namespace Servanda.Infrastructure.Data;

/// <summary>
/// Kanoniczne klucze zakresów kolejności zgodne z ADR 0004.
/// </summary>
internal static class OrderingScopeKeys
{
    public const string Areas = "areas";

    public static string Categories(string areaId, string? parentId) =>
        $"categories:{areaId}:{parentId ?? "root"}";

    public static string RootCategories(string areaId) => Categories(areaId, null);

    public static string Tools(string categoryId, string groupKey) => $"tools:{categoryId}:{groupKey}";

    public static string Prompts(string categoryId) => $"prompts:{categoryId}";
}

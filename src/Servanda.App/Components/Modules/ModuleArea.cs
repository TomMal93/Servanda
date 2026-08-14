using Servanda.Application.Areas;

namespace Servanda.App.Components.Modules;

/// <summary>
/// Odnajduje aktywny obszar modułu; moduł bez obszaru nie może udawać działającej funkcji.
/// </summary>
public static class ModuleArea
{
    public const string ToolsModuleKey = "tools";
    public const string PromptsModuleKey = "prompts";

    public static async Task<AreaListItem?> FindAsync(
        IAreaService areaService,
        string moduleKey,
        CancellationToken cancellationToken = default)
    {
        var areas = await areaService.ListAsync(cancellationToken);
        return areas.FirstOrDefault(area =>
            string.Equals(area.ModuleKey, moduleKey, StringComparison.Ordinal)
            && string.Equals(area.Availability, "active", StringComparison.Ordinal));
    }

    public static string RouteFor(string moduleKey) => moduleKey switch
    {
        ToolsModuleKey => "/narzedzia",
        PromptsModuleKey => "/prompty",
        _ => "/",
    };
}

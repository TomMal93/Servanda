namespace Servanda.App.Components;

internal static class AreaCatalog
{
    public static IReadOnlyList<AreaItem> Items { get; } =
    [
        new("prompts", "Skarbiec promptów", "Przechowywanie, przygotowywanie i ponowne używanie promptów.", "area-tile--accent-0", AreaIconKind.Prompts),
        new("tools", "Przechowalnia narzędzi", "Katalog sprawdzonych stron i aplikacji przydatnych na co dzień.", "area-tile--accent-1", AreaIconKind.Tools),
        new("home", "Dom", "Harmonogram prac porządkowych i innych obowiązków domowych.", "area-tile--accent-2", AreaIconKind.Home),
        new("family", "Rodzina", "Ważne informacje, potrzeby, daty i relacje dotyczące bliskich.", "area-tile--accent-3", AreaIconKind.Family),
        new("vitality", "Witalność", "Zdrowie, biohacking, dieta i trening w jednym uporządkowanym miejscu.", "area-tile--accent-4", AreaIconKind.Vitality),
        new("notes", "Przechowalnia notatek", "Pomysły, obserwacje i informacje zachowane do późniejszego użycia.", "area-tile--accent-5", AreaIconKind.Notes),
        new("budget", "Budżet domowy", "Planowanie miesięcznego budżetu gospodarstwa domowego.", "area-tile--accent-0", AreaIconKind.Budget),
    ];
}

internal sealed record AreaItem(
    string Id,
    string Name,
    string Description,
    string AccentClass,
    AreaIconKind Icon)
{
    public string HeadingId => $"area-{Id}-heading";
}

internal enum AreaIconKind
{
    Prompts,
    Tools,
    Home,
    Family,
    Vitality,
    Notes,
    Budget,
}

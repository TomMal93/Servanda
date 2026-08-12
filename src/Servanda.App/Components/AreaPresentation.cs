using Servanda.Application.Areas;
using Microsoft.AspNetCore.Components;

namespace Servanda.App.Components;

internal static class AreaPresentation
{
    public static string AccentClass(AreaListItem area) => AccentClass(area.AccentKey);

    public static string AccentClass(string accentKey) => accentKey switch
    {
        "accent-0" => "area-tile--accent-0",
        "accent-1" => "area-tile--accent-1",
        "accent-2" => "area-tile--accent-2",
        "accent-3" => "area-tile--accent-3",
        "accent-4" => "area-tile--accent-4",
        "accent-5" => "area-tile--accent-5",
        _ => "area-tile--accent-0",
    };

    public static string HeadingId(AreaListItem area) => $"area-{area.Id}-heading";

    public static RenderFragment Icon(string iconKey) => builder =>
    {
        var markup = iconKey switch
        {
            "prompts" => """<svg viewBox="0 0 24 24" focusable="false"><path d="M5 4h14v12H9l-4 4V4Z"/><path d="M8 8h8M8 12h5"/></svg>""",
            "tools" => """<svg viewBox="0 0 24 24" focusable="false"><path d="m14 6 4-4 4 4-4 4M18 2v8M4 14h8v8H4zM8 14V9a3 3 0 0 1 3-3h7"/></svg>""",
            "home" => """<svg viewBox="0 0 24 24" focusable="false"><path d="m3 11 9-8 9 8M5 10v11h14V10M9 21v-7h6v7"/></svg>""",
            "family" => """<svg viewBox="0 0 24 24" focusable="false"><circle cx="9" cy="8" r="3"/><circle cx="17" cy="9" r="2"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M15 14a5 5 0 0 1 6 5v2"/></svg>""",
            "vitality" => """<svg viewBox="0 0 24 24" focusable="false"><path d="M3 12h4l2-6 4 12 2-6h6M12 21C6 17 3 14 3 9a5 5 0 0 1 9-3 5 5 0 0 1 9 3c0 5-3 8-9 12Z"/></svg>""",
            "notes" => """<svg viewBox="0 0 24 24" focusable="false"><path d="M5 3h14v18H5zM9 7h6M9 11h6M9 15h4"/></svg>""",
            "budget" => """<svg viewBox="0 0 24 24" focusable="false"><path d="M3 7h18v13H3zM3 10h18M16 15h3M6 7V4h12v3"/></svg>""",
            _ => """<svg viewBox="0 0 24 24" focusable="false"><circle cx="12" cy="12" r="8"/><path d="M12 8v8M8 12h8"/></svg>""",
        };

        builder.AddMarkupContent(0, markup);
    };
}

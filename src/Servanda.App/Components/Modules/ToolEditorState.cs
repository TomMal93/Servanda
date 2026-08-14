using Servanda.Application.Tools;

namespace Servanda.App.Components.Modules;

/// <summary>
/// Model wejściowy edytora narzędzia; pola członkostwa listy zmienia osobna komenda przeniesienia.
/// </summary>
public sealed class ToolEditorState
{
    public string? Id { get; init; }

    public string AreaId { get; init; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    public string GroupKey { get; set; } = "regular";

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public long Revision { get; init; }

    public string ContentEpoch { get; init; } = string.Empty;

    public bool IsNew => Id is null;

    public IReadOnlyList<string> TagNames() => TagList.Parse(Tags);

    public static ToolEditorState ForCreate(string areaId, string categoryId, string contentEpoch) =>
        new()
        {
            AreaId = areaId,
            CategoryId = categoryId,
            ContentEpoch = contentEpoch,
        };

    public static ToolEditorState ForEdit(ToolEditorModel model) =>
        new()
        {
            Id = model.Id,
            AreaId = model.AreaId,
            CategoryId = model.CategoryId,
            GroupKey = model.GroupKey,
            Name = model.Name,
            Description = model.Description,
            Url = model.Url,
            Tags = TagList.Format(model.TagNames),
            Revision = model.Revision,
            ContentEpoch = model.ContentEpoch,
        };
}

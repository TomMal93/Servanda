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

    public string OriginalCategoryId { get; init; } = string.Empty;

    public string GroupKey { get; set; } = "regular";

    public string OriginalGroupKey { get; init; } = "regular";

    public string Name { get; set; } = string.Empty;

    public string OriginalName { get; init; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string OriginalDescription { get; init; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string OriginalUrl { get; init; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public string OriginalTags { get; init; } = string.Empty;

    public long Revision { get; init; }

    public string ContentEpoch { get; init; } = string.Empty;

    public bool IsNew => Id is null;

    public IReadOnlyList<string> TagNames() => TagList.Parse(Tags);

    public bool ContentChanged() => IsNew
        || !string.Equals(Name, OriginalName, StringComparison.Ordinal)
        || !string.Equals(Description, OriginalDescription, StringComparison.Ordinal)
        || !string.Equals(Url, OriginalUrl, StringComparison.Ordinal)
        || !string.Equals(Tags, OriginalTags, StringComparison.Ordinal);

    public static ToolEditorState ForCreate(string areaId, string categoryId, string contentEpoch) =>
        new()
        {
            AreaId = areaId,
            CategoryId = categoryId,
            OriginalCategoryId = categoryId,
            ContentEpoch = contentEpoch,
        };

    public static ToolEditorState ForEdit(ToolEditorModel model) =>
        new()
        {
            Id = model.Id,
            AreaId = model.AreaId,
            CategoryId = model.CategoryId,
            OriginalCategoryId = model.CategoryId,
            GroupKey = model.GroupKey,
            OriginalGroupKey = model.GroupKey,
            Name = model.Name,
            OriginalName = model.Name,
            Description = model.Description,
            OriginalDescription = model.Description,
            Url = model.Url,
            OriginalUrl = model.Url,
            Tags = TagList.Format(model.TagNames),
            OriginalTags = TagList.Format(model.TagNames),
            Revision = model.Revision,
            ContentEpoch = model.ContentEpoch,
        };
}

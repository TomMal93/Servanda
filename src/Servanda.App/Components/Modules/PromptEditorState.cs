using Servanda.Application.Prompts;
using Servanda.Domain.Prompts;

namespace Servanda.App.Components.Modules;

/// <summary>
/// Model wejściowy edytora promptu obejmujący warianty i konfigurację zmiennych.
/// </summary>
public sealed class PromptEditorState
{
    public string? Id { get; init; }

    public string AreaId { get; init; } = string.Empty;

    public string CategoryId { get; set; } = string.Empty;

    public string OriginalCategoryId { get; init; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string OriginalTitle { get; init; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string OriginalDescription { get; init; } = string.Empty;

    public string Tags { get; set; } = string.Empty;

    public string OriginalTags { get; init; } = string.Empty;

    public bool AllowUnusedVariables { get; set; }

    public bool OriginalAllowUnusedVariables { get; init; }

    public long Revision { get; init; }

    public string ContentEpoch { get; init; } = string.Empty;

    public List<VariantRow> Variants { get; } = [];

    public List<VariableRow> Variables { get; } = [];

    public bool IsNew => Id is null;

    public IReadOnlyList<string> TagNames() => TagList.Parse(Tags);

    public IReadOnlyList<PromptVariantDraft> VariantDrafts() =>
        [.. Variants.Select(row => new PromptVariantDraft(row.Id, row.Name, row.Target, row.Content))];

    public IReadOnlyList<PromptVariableDraft> VariableDrafts() =>
        [.. Variables.Select(row => new PromptVariableDraft(
            row.Id,
            row.Name,
            row.Label,
            row.DefaultValue,
            row.IsRequired,
            row.IsMultiline))];

    public bool ContentChanged() => IsNew
        || !string.Equals(Title, OriginalTitle, StringComparison.Ordinal)
        || !string.Equals(Description, OriginalDescription, StringComparison.Ordinal)
        || !string.Equals(Tags, OriginalTags, StringComparison.Ordinal)
        || AllowUnusedVariables != OriginalAllowUnusedVariables
        || !VariantDrafts().SequenceEqual(OriginalVariants)
        || !VariableDrafts().SequenceEqual(OriginalVariables);

    private IReadOnlyList<PromptVariantDraft> OriginalVariants { get; init; } = [];

    private IReadOnlyList<PromptVariableDraft> OriginalVariables { get; init; } = [];

    public static PromptEditorState ForCreate(string areaId, string categoryId, string contentEpoch)
    {
        var state = new PromptEditorState
        {
            AreaId = areaId,
            CategoryId = categoryId,
            OriginalCategoryId = categoryId,
            ContentEpoch = contentEpoch,
        };
        state.Variants.Add(new VariantRow { Name = "Podstawowy" });
        return state;
    }

    public static PromptEditorState ForEdit(PromptEditorModel model)
    {
        var state = new PromptEditorState
        {
            Id = model.Id,
            AreaId = model.AreaId,
            CategoryId = model.CategoryId,
            OriginalCategoryId = model.CategoryId,
            Title = model.Title,
            OriginalTitle = model.Title,
            Description = model.Description,
            OriginalDescription = model.Description,
            Tags = TagList.Format(model.TagNames),
            OriginalTags = TagList.Format(model.TagNames),
            OriginalVariants = [.. model.Variants.Select(variant => new PromptVariantDraft(
                variant.Id,
                variant.Name,
                variant.Target,
                variant.Content))],
            OriginalVariables = [.. model.Variables.Select(variable => new PromptVariableDraft(
                variable.Id,
                variable.Name,
                variable.Label,
                variable.DefaultValue,
                variable.IsRequired,
                variable.IsMultiline))],
            Revision = model.Revision,
            ContentEpoch = model.ContentEpoch,
        };
        foreach (var variant in model.Variants)
        {
            state.Variants.Add(new VariantRow
            {
                Id = variant.Id,
                Name = variant.Name,
                Target = variant.Target ?? string.Empty,
                Content = variant.Content,
            });
        }

        foreach (var variable in model.Variables)
        {
            state.Variables.Add(new VariableRow
            {
                Id = variable.Id,
                Name = variable.Name,
                Label = variable.Label,
                DefaultValue = variable.DefaultValue,
                IsRequired = variable.IsRequired,
                IsMultiline = variable.IsMultiline,
            });
        }

        return state;
    }

    public sealed class VariantRow
    {
        public string Key { get; } = Guid.NewGuid().ToString("N");

        public string? Id { get; init; }

        public string Name { get; set; } = string.Empty;

        public string Target { get; set; } = string.Empty;

        public string Content { get; set; } = string.Empty;
    }

    public sealed class VariableRow
    {
        public string Key { get; } = Guid.NewGuid().ToString("N");

        public string? Id { get; init; }

        public string Name { get; set; } = string.Empty;

        public string Label { get; set; } = string.Empty;

        public string DefaultValue { get; set; } = string.Empty;

        public bool IsRequired { get; set; }

        public bool IsMultiline { get; set; }
    }
}

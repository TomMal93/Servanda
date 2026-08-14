using Servanda.Application.Common;
using Servanda.Domain.Prompts;

namespace Servanda.Application.Prompts;

public enum PromptFilter
{
    All,
    Favorites,
    RecentlyUsed,
}

public sealed record PromptCard(
    string Id,
    string CategoryId,
    string CategoryPath,
    string Title,
    string Description,
    bool IsFavorite,
    IReadOnlyList<string> VisibleTags,
    int HiddenTagCount,
    int VariantCount,
    DateTimeOffset? LastUsedAt,
    string? MatchExplanation,
    int SortOrder,
    long Revision);

public sealed record PromptQuery(
    string AreaId,
    PromptFilter Filter = PromptFilter.All,
    string? CategoryId = null,
    string? Text = null,
    int Skip = 0,
    int Take = PromptQuery.PageSize)
{
    public const int PageSize = 50;
}

public sealed record PromptPage(
    IReadOnlyList<PromptCard> Items,
    int TotalCount,
    bool HasMore,
    bool QueryTooShort,
    string ContentEpoch);

public sealed record PromptVariantModel(
    string Id,
    string Name,
    string? Target,
    string Content,
    int SortOrder);

public sealed record PromptVariableModel(
    string Id,
    string Name,
    string Label,
    string DefaultValue,
    bool IsRequired,
    bool IsMultiline,
    int SortOrder);

public sealed record PromptEditorModel(
    string Id,
    string AreaId,
    string CategoryId,
    string Title,
    string Description,
    IReadOnlyList<string> TagNames,
    IReadOnlyList<PromptVariantModel> Variants,
    IReadOnlyList<PromptVariableModel> Variables,
    long Revision,
    string ContentEpoch);

public sealed record PromptVersionItem(
    string Id,
    DateTimeOffset CreatedAt,
    int VariantCount,
    int VariableCount,
    bool IsSupported);

public sealed record PromptUsageItem(
    string Id,
    string? PromptId,
    string PromptTitle,
    string VariantName,
    DateTimeOffset UsedAt);

public sealed record CreatePromptCommand(
    string AreaId,
    string CategoryId,
    string Title,
    string Description,
    IReadOnlyList<string> TagNames,
    IReadOnlyList<PromptVariantDraft> Variants,
    IReadOnlyList<PromptVariableDraft> Variables,
    bool AllowUnusedVariables,
    long ExpectedScopeRevision,
    string ContentEpoch);

public sealed record UpdatePromptCommand(
    string Id,
    string Title,
    string Description,
    IReadOnlyList<string> TagNames,
    IReadOnlyList<PromptVariantDraft> Variants,
    IReadOnlyList<PromptVariableDraft> Variables,
    bool AllowUnusedVariables,
    long ExpectedRevision,
    string ContentEpoch);

public sealed record SetPromptFavoriteCommand(
    string Id,
    bool IsFavorite,
    long ExpectedRevision,
    string ContentEpoch);

public sealed record MovePromptCommand(
    string Id,
    string TargetCategoryId,
    string? BeforePromptId,
    long ExpectedRevision,
    long ExpectedSourceScopeRevision,
    long ExpectedTargetScopeRevision,
    string ContentEpoch);

public sealed record DeletePromptCommand(
    string Id,
    long ExpectedRevision,
    long ExpectedScopeRevision,
    string ContentEpoch);

public sealed record RestorePromptVersionCommand(
    string PromptId,
    string VersionId,
    long ExpectedRevision,
    string ContentEpoch);

public sealed record RecordPromptUsageCommand(string PromptId, string VariantId);

public sealed record PromptResult(
    WriteStatus Status,
    PromptCard? Prompt = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record PromptScopeState(string ScopeKey, long Revision);

public interface IPromptLibraryService
{
    Task<PromptPage> SearchAsync(PromptQuery query, CancellationToken cancellationToken = default);

    Task<PromptEditorModel?> GetForEditAsync(string id, CancellationToken cancellationToken = default);

    Task<PromptScopeState> GetScopeAsync(string categoryId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PromptVersionItem>> ListVersionsAsync(
        string promptId,
        CancellationToken cancellationToken = default);

    /// <summary>Historia użycia jest wspólna dla całej biblioteki i ograniczona do 500 wpisów.</summary>
    Task<IReadOnlyList<PromptUsageItem>> ListUsageAsync(
        int take = 50,
        CancellationToken cancellationToken = default);

    Task<PromptResult> CreateAsync(CreatePromptCommand command, CancellationToken cancellationToken = default);

    Task<PromptResult> UpdateAsync(UpdatePromptCommand command, CancellationToken cancellationToken = default);

    Task<PromptResult> SetFavoriteAsync(
        SetPromptFavoriteCommand command,
        CancellationToken cancellationToken = default);

    Task<PromptResult> MoveAsync(MovePromptCommand command, CancellationToken cancellationToken = default);

    Task<PromptResult> DeleteAsync(DeletePromptCommand command, CancellationToken cancellationToken = default);

    Task<PromptResult> RestoreVersionAsync(
        RestorePromptVersionCommand command,
        CancellationToken cancellationToken = default);

    Task<WriteStatus> RecordUsageAsync(
        RecordPromptUsageCommand command,
        CancellationToken cancellationToken = default);
}

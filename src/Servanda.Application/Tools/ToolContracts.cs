using Servanda.Application.Common;

namespace Servanda.Application.Tools;

public sealed record ToolCard(
    string Id,
    string CategoryId,
    string CategoryPath,
    string GroupKey,
    string Name,
    string Description,
    string Url,
    string Host,
    string Initials,
    IReadOnlyList<string> VisibleTags,
    int HiddenTagCount,
    string? MatchExplanation,
    int SortOrder,
    long Revision);

/// <summary>
/// Zapytanie łączy filtr kategorii z wyszukiwaniem pełnotekstowym i stronicowaniem.
/// </summary>
public sealed record ToolQuery(
    string AreaId,
    string? CategoryId = null,
    string? Text = null,
    int Skip = 0,
    int Take = ToolQuery.PageSize)
{
    public const int PageSize = 50;
}

public sealed record ToolPage(
    IReadOnlyList<ToolCard> Items,
    int TotalCount,
    bool HasMore,
    bool QueryTooShort,
    string ContentEpoch);

public sealed record ToolEditorModel(
    string Id,
    string AreaId,
    string CategoryId,
    string GroupKey,
    string Name,
    string Description,
    string Url,
    IReadOnlyList<string> TagNames,
    long Revision,
    string ContentEpoch);

public sealed record CreateToolCommand(
    string AreaId,
    string CategoryId,
    string GroupKey,
    string Name,
    string Description,
    string Url,
    IReadOnlyList<string> TagNames,
    long ExpectedScopeRevision,
    string ContentEpoch);

public sealed record UpdateToolCommand(
    string Id,
    string Name,
    string Description,
    string Url,
    IReadOnlyList<string> TagNames,
    long ExpectedRevision,
    string ContentEpoch);

public sealed record MoveToolCommand(
    string Id,
    string TargetCategoryId,
    string TargetGroupKey,
    string? BeforeToolId,
    long ExpectedRevision,
    long ExpectedSourceScopeRevision,
    long ExpectedTargetScopeRevision,
    string ContentEpoch);

public sealed record DeleteToolCommand(
    string Id,
    long ExpectedRevision,
    long ExpectedScopeRevision,
    string ContentEpoch);

public sealed record ToolResult(
    WriteStatus Status,
    ToolCard? Tool = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record ToolScopeState(string ScopeKey, long Revision);

public interface IToolCatalogService
{
    Task<ToolPage> SearchAsync(ToolQuery query, CancellationToken cancellationToken = default);

    Task<ToolEditorModel?> GetForEditAsync(string id, CancellationToken cancellationToken = default);

    Task<ToolScopeState> GetScopeAsync(
        string categoryId,
        string groupKey,
        CancellationToken cancellationToken = default);

    Task<ToolResult> CreateAsync(CreateToolCommand command, CancellationToken cancellationToken = default);

    Task<ToolResult> UpdateAsync(UpdateToolCommand command, CancellationToken cancellationToken = default);

    Task<ToolResult> MoveAsync(MoveToolCommand command, CancellationToken cancellationToken = default);

    Task<ToolResult> DeleteAsync(DeleteToolCommand command, CancellationToken cancellationToken = default);
}

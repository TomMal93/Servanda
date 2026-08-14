using Servanda.Application.Common;

namespace Servanda.Application.Catalog;

public sealed record CategoryItem(
    string Id,
    string? ParentId,
    string Name,
    string Description,
    int SortOrder,
    long Revision);

public sealed record CategoryNode(
    CategoryItem Category,
    int DirectItemCount,
    int TotalItemCount,
    IReadOnlyList<CategoryNode> Children);

/// <summary>
/// Drzewo kategorii obszaru wraz z tokenami współbieżności potrzebnymi do zapisu.
/// </summary>
public sealed record CategoryTree(
    string AreaId,
    IReadOnlyList<CategoryNode> Roots,
    int UncategorizedItemCount,
    string ContentEpoch,
    IReadOnlyDictionary<string, long> ScopeRevisions)
{
    public long ScopeRevisionFor(string? parentId) =>
        ScopeRevisions.TryGetValue(ScopeKeyFor(parentId), out var revision) ? revision : 0;

    public string ScopeKeyFor(string? parentId) => $"categories:{AreaId}:{parentId ?? "root"}";
}

public sealed record CreateCategoryCommand(
    string AreaId,
    string? ParentId,
    string Name,
    string Description,
    long ExpectedScopeRevision,
    string ContentEpoch);

public sealed record UpdateCategoryCommand(
    string Id,
    string Name,
    string Description,
    long ExpectedRevision,
    string ContentEpoch);

public sealed record MoveCategoryCommand(
    string Id,
    string? TargetParentId,
    string? BeforeCategoryId,
    long ExpectedRevision,
    long ExpectedSourceScopeRevision,
    long ExpectedTargetScopeRevision,
    string ContentEpoch);

public sealed record DeleteCategoryCommand(
    string Id,
    long ExpectedRevision,
    long ExpectedScopeRevision,
    string ContentEpoch,
    int ExpectedDescendantCategories = 0,
    int ExpectedTools = 0,
    int ExpectedPrompts = 0,
    bool Confirmed = false);

public sealed record CategoryDeletePreview(
    string Id,
    string Name,
    int DescendantCategories,
    int Tools,
    int Prompts,
    long Revision,
    long ParentScopeRevision,
    string ContentEpoch)
{
    public bool RequiresProtectionBackup => DescendantCategories + Tools + Prompts > 0;
}

public sealed record CategoryResult(
    WriteStatus Status,
    CategoryItem? Category = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public sealed record TagItem(string Id, string Name, string NormalizedName, long Revision, int UsageCount);

public sealed record RenameTagCommand(string Id, string Name, long ExpectedRevision, string ContentEpoch);

public sealed record DeleteTagCommand(string Id, long ExpectedRevision, string ContentEpoch);

public sealed record TagResult(
    WriteStatus Status,
    TagItem? Tag = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface ICategoryService
{
    Task<CategoryTree> GetTreeAsync(string areaId, CancellationToken cancellationToken = default);

    Task<CategoryResult> CreateAsync(CreateCategoryCommand command, CancellationToken cancellationToken = default);

    Task<CategoryResult> UpdateAsync(UpdateCategoryCommand command, CancellationToken cancellationToken = default);

    Task<CategoryResult> MoveAsync(MoveCategoryCommand command, CancellationToken cancellationToken = default);

    Task<CategoryDeletePreview?> PreviewDeleteAsync(string id, CancellationToken cancellationToken = default);

    Task<CategoryResult> DeleteAsync(DeleteCategoryCommand command, CancellationToken cancellationToken = default);
}

public interface ITagService
{
    Task<IReadOnlyList<TagItem>> ListAsync(string areaId, CancellationToken cancellationToken = default);

    Task<TagResult> RenameAsync(RenameTagCommand command, CancellationToken cancellationToken = default);

    Task<TagResult> DeleteAsync(DeleteTagCommand command, CancellationToken cancellationToken = default);
}

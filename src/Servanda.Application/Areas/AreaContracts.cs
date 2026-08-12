namespace Servanda.Application.Areas;

public sealed record AreaListItem(
    string Id,
    string Name,
    string Description,
    string IconKey,
    string AccentKey,
    string Availability,
    int SortOrder,
    long Revision,
    string ContentEpoch);

public sealed record UpdateAreaCommand(
    string Id,
    string Name,
    string Description,
    long ExpectedRevision,
    string ContentEpoch);

public enum UpdateAreaStatus
{
    Success,
    ValidationFailed,
    Conflict,
    NotFound,
}

public sealed record UpdateAreaResult(
    UpdateAreaStatus Status,
    AreaListItem? Area = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public interface IAreaService
{
    Task<IReadOnlyList<AreaListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<UpdateAreaResult> UpdateAsync(
        UpdateAreaCommand command,
        CancellationToken cancellationToken = default);
}

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
    string ContentEpoch,
    long OrderingRevision);

public sealed record CreateAreaCommand(
    string Name,
    string Description,
    string IconKey,
    string AccentKey,
    long ExpectedOrderingRevision,
    string ContentEpoch);

public sealed record UpdateAreaCommand(
    string Id,
    string Name,
    string Description,
    long ExpectedRevision,
    string ContentEpoch);

public sealed record MoveAreaCommand(
    string Id,
    string? BeforeAreaId,
    long ExpectedOrderingRevision,
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

public enum CreateAreaStatus
{
    Success,
    ValidationFailed,
    Conflict,
}

public sealed record CreateAreaResult(
    CreateAreaStatus Status,
    AreaListItem? Area = null,
    IReadOnlyDictionary<string, string[]>? Errors = null);

public enum MoveAreaStatus
{
    Success,
    Conflict,
    NotFound,
}

public sealed record MoveAreaResult(
    MoveAreaStatus Status,
    IReadOnlyList<AreaListItem>? Areas = null);

public interface IAreaService
{
    Task<IReadOnlyList<AreaListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<CreateAreaResult> CreateAsync(
        CreateAreaCommand command,
        CancellationToken cancellationToken = default);

    Task<MoveAreaResult> MoveAsync(
        MoveAreaCommand command,
        CancellationToken cancellationToken = default);

    Task<UpdateAreaResult> UpdateAsync(
        UpdateAreaCommand command,
        CancellationToken cancellationToken = default);
}

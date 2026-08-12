namespace Servanda.Application.Areas;

public sealed record AreaListItem(
    string Id,
    string Name,
    string Description,
    string IconKey,
    string AccentKey,
    string Availability,
    bool IsHidden,
    DateTimeOffset? ArchivedAt,
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

public sealed record SetAreaVisibilityCommand(
    string Id,
    bool IsHidden,
    long ExpectedRevision,
    string ContentEpoch);

public sealed record SetAreaArchivedCommand(
    string Id,
    bool IsArchived,
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

public enum SetAreaVisibilityStatus
{
    Success,
    Conflict,
    NotFound,
}

public sealed record SetAreaVisibilityResult(
    SetAreaVisibilityStatus Status,
    AreaListItem? Area = null);

public enum SetAreaArchivedStatus
{
    Success,
    Conflict,
    NotFound,
}

public sealed record SetAreaArchivedResult(
    SetAreaArchivedStatus Status,
    AreaListItem? Area = null);

public interface IAreaService
{
    Task<IReadOnlyList<AreaListItem>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AreaListItem>> ListForManagementAsync(CancellationToken cancellationToken = default);

    Task<CreateAreaResult> CreateAsync(
        CreateAreaCommand command,
        CancellationToken cancellationToken = default);

    Task<MoveAreaResult> MoveAsync(
        MoveAreaCommand command,
        CancellationToken cancellationToken = default);

    Task<SetAreaVisibilityResult> SetVisibilityAsync(
        SetAreaVisibilityCommand command,
        CancellationToken cancellationToken = default);

    Task<SetAreaArchivedResult> SetArchivedAsync(
        SetAreaArchivedCommand command,
        CancellationToken cancellationToken = default);

    Task<UpdateAreaResult> UpdateAsync(
        UpdateAreaCommand command,
        CancellationToken cancellationToken = default);
}

using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.Areas;
using Servanda.Domain.Areas;

namespace Servanda.Infrastructure.Data;

internal sealed class SqliteAreaService(
    IDbContextFactory<ServandaDbContext> contextFactory,
    TimeProvider timeProvider) : IAreaService
{
    public async Task<IReadOnlyList<AreaListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await ListAsync(includeHidden: false, cancellationToken);
    }

    public async Task<IReadOnlyList<AreaListItem>> ListForManagementAsync(
        CancellationToken cancellationToken = default)
    {
        return await ListAsync(includeHidden: true, cancellationToken);
    }

    private async Task<IReadOnlyList<AreaListItem>> ListAsync(
        bool includeHidden,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var epoch = await database.AppState
            .AsNoTracking()
            .Where(item => item.Id == 1)
            .Select(item => item.ContentEpoch)
            .SingleAsync(cancellationToken);
        var orderingRevision = await database.OrderingScopes
            .AsNoTracking()
            .Where(item => item.ScopeKey == "areas")
            .Select(item => item.Revision)
            .SingleAsync(cancellationToken);

        return await database.Areas
            .AsNoTracking()
            .Where(area => area.ArchivedAt == null && (includeHidden || !area.IsHidden))
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.Id)
            .Select(area => new AreaListItem(
                area.Id,
                area.Name,
                area.Description,
                area.IconKey,
                area.AccentKey,
                area.Availability,
                area.IsHidden,
                area.SortOrder,
                area.Revision,
                epoch,
                orderingRevision))
            .ToListAsync(cancellationToken);
    }

    public async Task<CreateAreaResult> CreateAsync(
        CreateAreaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await database.AppState
            .Where(item => item.Id == 1)
            .Select(item => item.ContentEpoch)
            .SingleAsync(cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new CreateAreaResult(CreateAreaStatus.Conflict);
        }

        var nextSortOrder = (await database.Areas.MaxAsync(
            area => (int?)area.SortOrder,
            cancellationToken) ?? -1) + 1;
        var timestamp = timeProvider.GetUtcNow();
        var area = Area.CreatePlanned(
            EntityId.NewUlid(timeProvider),
            command.Name,
            command.Description,
            command.IconKey,
            command.AccentKey,
            nextSortOrder,
            timestamp,
            out var errors);
        if (area is null)
        {
            return new CreateAreaResult(CreateAreaStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            var changedScopes = await database.OrderingScopes
                .Where(scope =>
                    scope.ScopeKey == "areas"
                    && scope.Revision == command.ExpectedOrderingRevision)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(scope => scope.Revision, scope => scope.Revision + 1)
                        .SetProperty(scope => scope.UpdatedAt, timestamp),
                    cancellationToken);
            if (changedScopes == 0)
            {
                return new CreateAreaResult(CreateAreaStatus.Conflict);
            }

            database.Areas.Add(area);
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
        {
            return new CreateAreaResult(CreateAreaStatus.Conflict);
        }

        return new CreateAreaResult(
            CreateAreaStatus.Success,
            ToListItem(area, epoch, command.ExpectedOrderingRevision + 1));
    }

    public async Task<MoveAreaResult> MoveAsync(
        MoveAreaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await database.AppState
            .Where(item => item.Id == 1)
            .Select(item => item.ContentEpoch)
            .SingleAsync(cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new MoveAreaResult(MoveAreaStatus.Conflict);
        }

        var areas = await database.Areas
            .AsNoTracking()
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.Id)
            .ToListAsync(cancellationToken);
        var originalOrder = areas.Select(area => area.Id).ToList();
        if (!originalOrder.Remove(command.Id)
            || (command.BeforeAreaId is not null && !originalOrder.Contains(command.BeforeAreaId)))
        {
            return new MoveAreaResult(MoveAreaStatus.NotFound);
        }

        var targetIndex = command.BeforeAreaId is null
            ? originalOrder.Count
            : originalOrder.IndexOf(command.BeforeAreaId);
        originalOrder.Insert(targetIndex, command.Id);
        var orderingRevision = command.ExpectedOrderingRevision + 1;
        var timestamp = timeProvider.GetUtcNow();

        try
        {
            var changedScopes = await database.OrderingScopes
                .Where(scope =>
                    scope.ScopeKey == "areas"
                    && scope.Revision == command.ExpectedOrderingRevision)
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(scope => scope.Revision, scope => scope.Revision + 1)
                        .SetProperty(scope => scope.UpdatedAt, timestamp),
                    cancellationToken);
            if (changedScopes == 0)
            {
                return new MoveAreaResult(MoveAreaStatus.Conflict);
            }

            var offset = areas.Count;
            await database.Areas.ExecuteUpdateAsync(
                setters => setters.SetProperty(area => area.SortOrder, area => area.SortOrder + offset),
                cancellationToken);
            for (var sortOrder = 0; sortOrder < originalOrder.Count; sortOrder++)
            {
                var areaId = originalOrder[sortOrder];
                var changedAreas = await database.Areas
                    .Where(area => area.Id == areaId)
                    .ExecuteUpdateAsync(
                        setters => setters.SetProperty(area => area.SortOrder, sortOrder),
                        cancellationToken);
                if (changedAreas != 1)
                {
                    throw new InvalidOperationException("Nie udało się atomowo zmienić kolejności obszarów.");
                }
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6)
        {
            return new MoveAreaResult(MoveAreaStatus.Conflict);
        }

        var areasById = areas.ToDictionary(area => area.Id, StringComparer.Ordinal);
        var reordered = originalOrder
            .Select((id, sortOrder) => (Area: areasById[id], SortOrder: sortOrder))
            .Where(item => item.Area.ArchivedAt is null)
            .Select(item => ToListItem(item.Area, epoch, orderingRevision, item.SortOrder))
            .ToList();
        return new MoveAreaResult(MoveAreaStatus.Success, reordered);
    }

    public async Task<SetAreaVisibilityResult> SetVisibilityAsync(
        SetAreaVisibilityCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await database.AppState
            .Where(item => item.Id == 1)
            .Select(item => item.ContentEpoch)
            .SingleAsync(cancellationToken);
        var orderingRevision = await database.OrderingScopes
            .Where(item => item.ScopeKey == "areas")
            .Select(item => item.Revision)
            .SingleAsync(cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new SetAreaVisibilityResult(SetAreaVisibilityStatus.Conflict);
        }

        var area = await database.Areas.SingleOrDefaultAsync(
            item => item.Id == command.Id && item.ArchivedAt == null,
            cancellationToken);
        if (area is null)
        {
            return new SetAreaVisibilityResult(SetAreaVisibilityStatus.NotFound);
        }

        if (area.Revision != command.ExpectedRevision)
        {
            return new SetAreaVisibilityResult(SetAreaVisibilityStatus.Conflict);
        }

        area.SetVisibility(command.IsHidden, timeProvider.GetUtcNow());
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new SetAreaVisibilityResult(SetAreaVisibilityStatus.Conflict);
        }

        return new SetAreaVisibilityResult(
            SetAreaVisibilityStatus.Success,
            ToListItem(area, epoch, orderingRevision));
    }

    public async Task<UpdateAreaResult> UpdateAsync(
        UpdateAreaCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await database.AppState
            .Where(item => item.Id == 1)
            .Select(item => item.ContentEpoch)
            .SingleAsync(cancellationToken);
        var orderingRevision = await database.OrderingScopes
            .Where(item => item.ScopeKey == "areas")
            .Select(item => item.Revision)
            .SingleAsync(cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new UpdateAreaResult(UpdateAreaStatus.Conflict);
        }

        var area = await database.Areas.SingleOrDefaultAsync(
            item => item.Id == command.Id,
            cancellationToken);
        if (area is null)
        {
            return new UpdateAreaResult(UpdateAreaStatus.NotFound);
        }

        if (area.Revision != command.ExpectedRevision)
        {
            return new UpdateAreaResult(UpdateAreaStatus.Conflict);
        }

        var errors = area.UpdateContent(command.Name, command.Description, timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            return new UpdateAreaResult(UpdateAreaStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new UpdateAreaResult(UpdateAreaStatus.Conflict);
        }

        return new UpdateAreaResult(
            UpdateAreaStatus.Success,
            ToListItem(area, epoch, orderingRevision));
    }

    private static AreaListItem ToListItem(
        Area area,
        string epoch,
        long orderingRevision,
        int? sortOrder = null) =>
        new(
            area.Id,
            area.Name,
            area.Description,
            area.IconKey,
            area.AccentKey,
            area.Availability,
            area.IsHidden,
            sortOrder ?? area.SortOrder,
            area.Revision,
            epoch,
            orderingRevision);
}

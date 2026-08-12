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
            .Where(area => area.ArchivedAt == null && !area.IsHidden)
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.Id)
            .Select(area => new AreaListItem(
                area.Id,
                area.Name,
                area.Description,
                area.IconKey,
                area.AccentKey,
                area.Availability,
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

    private static AreaListItem ToListItem(Area area, string epoch, long orderingRevision) =>
        new(
            area.Id,
            area.Name,
            area.Description,
            area.IconKey,
            area.AccentKey,
            area.Availability,
            area.SortOrder,
            area.Revision,
            epoch,
            orderingRevision);
}

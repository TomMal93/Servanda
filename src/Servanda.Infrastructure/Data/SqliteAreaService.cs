using Microsoft.EntityFrameworkCore;
using Servanda.Application.Areas;

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
                epoch))
            .ToListAsync(cancellationToken);
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
            new AreaListItem(
                area.Id,
                area.Name,
                area.Description,
                area.IconKey,
                area.AccentKey,
                area.Availability,
                area.SortOrder,
                area.Revision,
                epoch));
    }
}

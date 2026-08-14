using Microsoft.EntityFrameworkCore;
using Servanda.Application.Catalog;
using Servanda.Application.Common;
using Servanda.Domain.Areas;
using Servanda.Domain.Catalog;
using Servanda.Domain.Prompts;
using Servanda.Domain.Tools;
using Servanda.Infrastructure.Data.Search;

namespace Servanda.Infrastructure.Data;

internal sealed class SqliteTagService(
    IDbContextFactory<ServandaDbContext> contextFactory,
    TimeProvider timeProvider) : ITagService
{
    public async Task<IReadOnlyList<TagItem>> ListAsync(
        string areaId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.Tags
            .AsNoTracking()
            .Where(tag => tag.AreaId == areaId)
            .OrderBy(tag => tag.NormalizedName)
            .Select(tag => new TagItem(
                tag.Id,
                tag.Name,
                tag.NormalizedName,
                tag.Revision,
                database.Set<ToolTag>().Count(link => link.TagId == tag.Id)
                    + database.Set<PromptTag>().Count(link => link.TagId == tag.Id)))
            .ToListAsync(cancellationToken);
    }

    public async Task<TagResult> RenameAsync(
        RenameTagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new TagResult(WriteStatus.Conflict);
        }

        var tag = await database.Tags.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (tag is null)
        {
            return new TagResult(WriteStatus.NotFound);
        }

        if (tag.Revision != command.ExpectedRevision)
        {
            return new TagResult(WriteStatus.Conflict);
        }

        var errors = tag.Rename(command.Name, timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            return new TagResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        var duplicate = await database.Tags.AnyAsync(
            item => item.AreaId == tag.AreaId
                && item.Id != tag.Id
                && item.NormalizedName == tag.NormalizedName,
            cancellationToken);
        if (duplicate)
        {
            return new TagResult(
                WriteStatus.ValidationFailed,
                Errors: new Dictionary<string, string[]>
                {
                    [nameof(Tag.Name)] = ["Tag o takiej nazwie już istnieje w tym obszarze."],
                });
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await SearchIndexWriter.UpdateTagUsagesAsync(database, tag.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new TagResult(WriteStatus.Conflict);
        }

        return new TagResult(WriteStatus.Success, await ReadItemAsync(database, tag, cancellationToken));
    }

    public async Task<TagResult> DeleteAsync(
        DeleteTagCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new TagResult(WriteStatus.Conflict);
        }

        var tag = await database.Tags.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (tag is null)
        {
            return new TagResult(WriteStatus.NotFound);
        }

        if (tag.Revision != command.ExpectedRevision)
        {
            return new TagResult(WriteStatus.Conflict);
        }

        var toolIds = await database.Set<ToolTag>()
            .Where(link => link.TagId == tag.Id)
            .Select(link => link.ToolId)
            .ToListAsync(cancellationToken);
        var promptIds = await database.Set<PromptTag>()
            .Where(link => link.TagId == tag.Id)
            .Select(link => link.PromptId)
            .ToListAsync(cancellationToken);
        var timestamp = timeProvider.GetUtcNow();

        try
        {
            // Odłączenie tagu jest jawną zmianą treści właścicieli, nie kaskadą techniczną.
            await database.Set<ToolTag>()
                .Where(link => link.TagId == tag.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await database.Set<PromptTag>()
                .Where(link => link.TagId == tag.Id)
                .ExecuteDeleteAsync(cancellationToken);
            await database.Tools
                .Where(tool => toolIds.Contains(tool.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(tool => tool.Revision, tool => tool.Revision + 1)
                        .SetProperty(tool => tool.UpdatedAt, timestamp),
                    cancellationToken);
            await database.Prompts
                .Where(prompt => promptIds.Contains(prompt.Id))
                .ExecuteUpdateAsync(
                    setters => setters
                        .SetProperty(prompt => prompt.Revision, prompt => prompt.Revision + 1)
                        .SetProperty(prompt => prompt.UpdatedAt, timestamp),
                    cancellationToken);

            database.Tags.Remove(tag);
            await database.SaveChangesAsync(cancellationToken);
            foreach (var toolId in toolIds)
            {
                await SearchIndexWriter.UpdateToolAsync(database, toolId, cancellationToken);
            }

            foreach (var promptId in promptIds)
            {
                await SearchIndexWriter.UpdatePromptAsync(database, promptId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new TagResult(WriteStatus.Conflict);
        }

        return new TagResult(WriteStatus.Success);
    }

    /// <summary>
    /// Zamienia nazwy tagów podane w edytorze na identyfikatory, tworząc brakujące tagi obszaru.
    /// </summary>
    internal static async Task<IReadOnlyList<string>> ResolveAsync(
        ServandaDbContext database,
        string areaId,
        IReadOnlyList<string> names,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var resolved = new List<string>(names.Count);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in names)
        {
            var normalized = Tag.NormalizeName(name);
            if (normalized.Length == 0 || !seen.Add(normalized))
            {
                continue;
            }

            var existing = await database.Tags
                .Where(tag => tag.AreaId == areaId && tag.NormalizedName == normalized)
                .Select(tag => tag.Id)
                .FirstOrDefaultAsync(cancellationToken);
            if (existing is not null)
            {
                resolved.Add(existing);
                continue;
            }

            var created = Tag.Create(
                EntityId.NewUlid(timeProvider),
                areaId,
                name,
                timeProvider.GetUtcNow(),
                out _);
            if (created is null)
            {
                continue;
            }

            database.Tags.Add(created);
            await database.SaveChangesAsync(cancellationToken);
            resolved.Add(created.Id);
        }

        return resolved;
    }

    private static async Task<TagItem> ReadItemAsync(
        ServandaDbContext database,
        Tag tag,
        CancellationToken cancellationToken)
    {
        var usage = await database.Set<ToolTag>().CountAsync(link => link.TagId == tag.Id, cancellationToken)
            + await database.Set<PromptTag>().CountAsync(link => link.TagId == tag.Id, cancellationToken);
        return new TagItem(tag.Id, tag.Name, tag.NormalizedName, tag.Revision, usage);
    }
}

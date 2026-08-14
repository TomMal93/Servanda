using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.Common;
using Servanda.Application.Tools;
using Servanda.Domain.Areas;
using Servanda.Domain.Search;
using Servanda.Domain.Tools;
using Servanda.Infrastructure.Data.Search;

namespace Servanda.Infrastructure.Data;

internal sealed class SqliteToolCatalogService(
    IDbContextFactory<ServandaDbContext> contextFactory,
    TimeProvider timeProvider) : IToolCatalogService
{
    private const int VisibleTagCount = 3;

    public async Task<ToolPage> SearchAsync(ToolQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        var categoryIds = await ResolveCategoryFilterAsync(database, query.AreaId, query.CategoryId, cancellationToken);
        var matchQuery = SearchText.BuildPrefixQuery(query.Text);
        var take = Math.Clamp(query.Take, 1, ToolQuery.PageSize);
        var skip = Math.Max(query.Skip, 0);

        IReadOnlyList<string> ids;
        int total;
        IReadOnlyList<string> tokens = [];
        if (matchQuery is null)
        {
            (ids, total) = await ListPageAsync(database, query.AreaId, categoryIds, skip, take, cancellationToken);
        }
        else
        {
            tokens = SearchText.Tokenize(query.Text);
            ids = await SearchQueries.RankToolsAsync(
                database,
                matchQuery,
                SearchText.Normalize(query.Text),
                query.AreaId,
                categoryIds,
                skip,
                take,
                cancellationToken);
            total = await SearchQueries.CountToolsAsync(
                database,
                matchQuery,
                query.AreaId,
                categoryIds,
                cancellationToken);
        }

        var cards = await LoadCardsAsync(database, ids, tokens, cancellationToken);
        return new ToolPage(
            cards,
            total,
            skip + cards.Count < total,
            SearchText.IsQueryTooShort(query.Text),
            epoch);
    }

    public async Task<ToolEditorModel?> GetForEditAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        var tool = await database.Tools
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.AreaId,
                item.CategoryId,
                item.GroupKey,
                item.Name,
                item.Description,
                item.Url,
                item.Revision,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            return null;
        }

        var tagNames = await ReadTagNamesAsync(database, [tool.Id], cancellationToken);
        return new ToolEditorModel(
            tool.Id,
            tool.AreaId,
            tool.CategoryId,
            tool.GroupKey,
            tool.Name,
            tool.Description,
            tool.Url,
            tagNames.TryGetValue(tool.Id, out var names) ? names : [],
            tool.Revision,
            epoch);
    }

    public async Task<ToolScopeState> GetScopeAsync(
        string categoryId,
        string groupKey,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var scopeKey = OrderingScopeKeys.Tools(categoryId, groupKey);
        var revision = await CollectionState.ReadScopeRevisionAsync(database, scopeKey, cancellationToken);
        return new ToolScopeState(scopeKey, revision ?? 0);
    }

    public async Task<ToolResult> CreateAsync(
        CreateToolCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        if (!await IsModuleCategoryAsync(database, command.AreaId, command.CategoryId, cancellationToken))
        {
            return new ToolResult(WriteStatus.NotFound);
        }

        var timestamp = timeProvider.GetUtcNow();
        var tagIds = await SqliteTagService.ResolveAsync(
            database,
            command.AreaId,
            command.TagNames,
            timeProvider,
            cancellationToken);
        var sortOrder = await database.Tools.CountAsync(
            tool => tool.CategoryId == command.CategoryId && tool.GroupKey == command.GroupKey,
            cancellationToken);
        var tool = Tool.Create(
            EntityId.NewUlid(timeProvider),
            command.AreaId,
            command.CategoryId,
            command.Name,
            command.Description,
            command.Url,
            command.GroupKey,
            tagIds,
            sortOrder,
            timestamp,
            out var errors);
        if (tool is null)
        {
            return new ToolResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    OrderingScopeKeys.Tools(command.CategoryId, command.GroupKey),
                    command.ExpectedScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new ToolResult(WriteStatus.Conflict);
            }

            database.Tools.Add(tool);
            await database.SaveChangesAsync(cancellationToken);
            await SearchIndexWriter.UpdateToolAsync(database, tool.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 or 19)
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        return new ToolResult(WriteStatus.Success, await ReadCardAsync(database, tool.Id, cancellationToken));
    }

    public async Task<ToolResult> UpdateAsync(
        UpdateToolCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        var tool = await database.Tools
            .Include(item => item.Tags)
            .SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (tool is null)
        {
            return new ToolResult(WriteStatus.NotFound);
        }

        if (tool.Revision != command.ExpectedRevision)
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        var tagIds = await SqliteTagService.ResolveAsync(
            database,
            tool.AreaId,
            command.TagNames,
            timeProvider,
            cancellationToken);
        var errors = tool.UpdateContent(
            command.Name,
            command.Description,
            command.Url,
            tagIds,
            timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            return new ToolResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await SearchIndexWriter.UpdateToolAsync(database, tool.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        return new ToolResult(WriteStatus.Success, await ReadCardAsync(database, tool.Id, cancellationToken));
    }

    public async Task<ToolResult> MoveAsync(MoveToolCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        var tool = await database.Tools.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (tool is null)
        {
            return new ToolResult(WriteStatus.NotFound);
        }

        if (tool.Revision != command.ExpectedRevision)
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        if (!Tool.IsSupportedGroup(command.TargetGroupKey)
            || !await IsModuleCategoryAsync(database, tool.AreaId, command.TargetCategoryId, cancellationToken))
        {
            return new ToolResult(WriteStatus.NotFound);
        }

        var timestamp = timeProvider.GetUtcNow();
        var sourceCategoryId = tool.CategoryId;
        var sourceGroupKey = tool.GroupKey;
        var sourceKey = OrderingScopeKeys.Tools(sourceCategoryId, sourceGroupKey);
        var targetKey = OrderingScopeKeys.Tools(command.TargetCategoryId, command.TargetGroupKey);
        var sameScope = string.Equals(sourceKey, targetKey, StringComparison.Ordinal);

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    sourceKey,
                    command.ExpectedSourceScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new ToolResult(WriteStatus.Conflict);
            }

            if (!sameScope)
            {
                if (!await CollectionState.TryAdvanceScopeAsync(
                        database,
                        targetKey,
                        command.ExpectedTargetScopeRevision,
                        timestamp,
                        cancellationToken))
                {
                    return new ToolResult(WriteStatus.Conflict);
                }

                tool.MoveTo(command.TargetCategoryId, command.TargetGroupKey, timestamp);
                await database.SaveChangesAsync(cancellationToken);
                await RenumberAsync(database, sourceCategoryId, sourceGroupKey, null, null, cancellationToken);
            }

            await RenumberAsync(
                database,
                command.TargetCategoryId,
                command.TargetGroupKey,
                command.Id,
                command.BeforeToolId,
                cancellationToken);
            await SearchIndexWriter.UpdateToolAsync(database, tool.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ToolResult(WriteStatus.Conflict);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 or 19)
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        return new ToolResult(WriteStatus.Success, await ReadCardAsync(database, tool.Id, cancellationToken));
    }

    public async Task<ToolResult> DeleteAsync(
        DeleteToolCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        var tool = await database.Tools.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (tool is null)
        {
            return new ToolResult(WriteStatus.NotFound);
        }

        if (tool.Revision != command.ExpectedRevision)
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        var timestamp = timeProvider.GetUtcNow();
        var categoryId = tool.CategoryId;
        var groupKey = tool.GroupKey;

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    OrderingScopeKeys.Tools(categoryId, groupKey),
                    command.ExpectedScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new ToolResult(WriteStatus.Conflict);
            }

            database.Tools.Remove(tool);
            await database.SaveChangesAsync(cancellationToken);
            await SearchIndexWriter.RemoveToolAsync(database, command.Id, cancellationToken);
            await RenumberAsync(database, categoryId, groupKey, null, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new ToolResult(WriteStatus.Conflict);
        }

        return new ToolResult(WriteStatus.Success);
    }

    private static async Task<(IReadOnlyList<string> Ids, int Total)> ListPageAsync(
        ServandaDbContext database,
        string areaId,
        IReadOnlyList<string>? categoryIds,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var filtered = database.Tools
            .AsNoTracking()
            .Where(tool => tool.AreaId == areaId);
        if (categoryIds is not null)
        {
            filtered = filtered.Where(tool => categoryIds.Contains(tool.CategoryId));
        }

        var total = await filtered.CountAsync(cancellationToken);
        var ids = await filtered
            .OrderBy(tool => tool.GroupKey == Tool.FeaturedGroup ? 0 : 1)
            .ThenBy(tool => tool.CategoryId)
            .ThenBy(tool => tool.SortOrder)
            .ThenBy(tool => tool.Id)
            .Skip(skip)
            .Take(take)
            .Select(tool => tool.Id)
            .ToListAsync(cancellationToken);
        return (ids, total);
    }

    private static async Task<IReadOnlyList<ToolCard>> LoadCardsAsync(
        ServandaDbContext database,
        IReadOnlyList<string> ids,
        IReadOnlyList<string> queryTokens,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var tools = await database.Tools
            .AsNoTracking()
            .Where(tool => ids.Contains(tool.Id))
            .Select(tool => new
            {
                tool.Id,
                tool.CategoryId,
                tool.GroupKey,
                tool.Name,
                tool.Description,
                tool.Url,
                tool.SortOrder,
                tool.Revision,
            })
            .ToListAsync(cancellationToken);
        var tagNames = await ReadTagNamesAsync(database, ids, cancellationToken);
        var categoryPaths = await ReadCategoryPathsAsync(
            database,
            tools.Select(tool => tool.CategoryId).Distinct(StringComparer.Ordinal).ToList(),
            cancellationToken);

        var byId = tools.ToDictionary(tool => tool.Id, StringComparer.Ordinal);
        var cards = new List<ToolCard>(ids.Count);
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var tool))
            {
                continue;
            }

            var tags = tagNames.TryGetValue(id, out var names) ? names : [];
            var visibleTags = tags.Take(VisibleTagCount).ToList();
            var categoryPath = categoryPaths.TryGetValue(tool.CategoryId, out var path) ? path : string.Empty;
            var explanation = MatchExplanation.For(
                queryTokens,
                string.Join(' ', [tool.Name, tool.Description, categoryPath, .. visibleTags]),
                string.Join(' ', tags));
            cards.Add(new ToolCard(
                tool.Id,
                tool.CategoryId,
                categoryPath,
                tool.GroupKey,
                tool.Name,
                tool.Description,
                tool.Url,
                ReadHost(tool.Url),
                CreateInitials(tool.Name),
                visibleTags,
                Math.Max(tags.Count - visibleTags.Count, 0),
                explanation,
                tool.SortOrder,
                tool.Revision));
        }

        return cards;
    }

    private static async Task<ToolCard?> ReadCardAsync(
        ServandaDbContext database,
        string id,
        CancellationToken cancellationToken)
    {
        var cards = await LoadCardsAsync(database, [id], [], cancellationToken);
        return cards.Count == 1 ? cards[0] : null;
    }

    private static async Task<IReadOnlyDictionary<string, List<string>>> ReadTagNamesAsync(
        ServandaDbContext database,
        IReadOnlyList<string> toolIds,
        CancellationToken cancellationToken)
    {
        var links = await (from link in database.Set<ToolTag>().AsNoTracking()
                           join tag in database.Tags.AsNoTracking() on link.TagId equals tag.Id
                           where toolIds.Contains(link.ToolId)
                           orderby tag.NormalizedName
                           select new { link.ToolId, tag.Name })
            .ToListAsync(cancellationToken);
        return links
            .GroupBy(link => link.ToolId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(link => link.Name).ToList(),
                StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyDictionary<string, string>> ReadCategoryPathsAsync(
        ServandaDbContext database,
        IReadOnlyList<string> categoryIds,
        CancellationToken cancellationToken)
    {
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var categoryId in categoryIds)
        {
            var names = await database.Database
                .SqlQueryRaw<string>(
                    """
                    WITH RECURSIVE ancestors(id, parent_id, name, depth) AS (
                        SELECT id, parent_id, name, 0 FROM categories WHERE id = {0}
                        UNION ALL
                        SELECT parent.id, parent.parent_id, parent.name, child.depth + 1
                        FROM categories parent
                        JOIN ancestors child ON parent.id = child.parent_id
                    )
                    SELECT name AS Value FROM ancestors ORDER BY depth DESC
                    """,
                    categoryId)
                .ToListAsync(cancellationToken);
            paths[categoryId] = string.Join(" / ", names);
        }

        return paths;
    }

    /// <summary>
    /// Nieznany albo należący do innego obszaru identyfikator wraca do widoku wszystkich narzędzi.
    /// </summary>
    private static async Task<IReadOnlyList<string>?> ResolveCategoryFilterAsync(
        ServandaDbContext database,
        string areaId,
        string? categoryId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(categoryId))
        {
            return null;
        }

        var belongsToArea = await database.Categories.AnyAsync(
            category => category.Id == categoryId && category.AreaId == areaId,
            cancellationToken);
        if (!belongsToArea)
        {
            return null;
        }

        return await database.Database
            .SqlQueryRaw<string>(
                """
                WITH RECURSIVE subtree(id) AS (
                    SELECT id FROM categories WHERE id = {0}
                    UNION ALL
                    SELECT child.id FROM categories child JOIN subtree ON child.parent_id = subtree.id
                )
                SELECT id AS Value FROM subtree
                """,
                categoryId)
            .ToListAsync(cancellationToken);
    }

    private static async Task<bool> IsModuleCategoryAsync(
        ServandaDbContext database,
        string areaId,
        string categoryId,
        CancellationToken cancellationToken) =>
        await database.Categories.AnyAsync(
            category => category.Id == categoryId && category.AreaId == areaId,
            cancellationToken)
        && await database.Areas.AnyAsync(
            area => area.Id == areaId
                && area.ModuleKey == "tools"
                && area.Availability == Area.ActiveAvailability,
            cancellationToken);

    private static async Task RenumberAsync(
        ServandaDbContext database,
        string categoryId,
        string groupKey,
        string? movedId,
        string? beforeId,
        CancellationToken cancellationToken)
    {
        var members = await database.Tools
            .AsNoTracking()
            .Where(tool => tool.CategoryId == categoryId && tool.GroupKey == groupKey)
            .OrderBy(tool => tool.SortOrder)
            .ThenBy(tool => tool.Id)
            .Select(tool => tool.Id)
            .ToListAsync(cancellationToken);
        if (movedId is not null)
        {
            members.Remove(movedId);
            var index = beforeId is null ? members.Count : members.IndexOf(beforeId);
            members.Insert(index < 0 ? members.Count : index, movedId);
        }

        var offset = members.Count + 1;
        await database.Tools
            .Where(tool => tool.CategoryId == categoryId && tool.GroupKey == groupKey)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(tool => tool.SortOrder, tool => tool.SortOrder + offset),
                cancellationToken);
        for (var sortOrder = 0; sortOrder < members.Count; sortOrder++)
        {
            var id = members[sortOrder];
            var position = sortOrder;
            await database.Tools
                .Where(tool => tool.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(tool => tool.SortOrder, position),
                    cancellationToken);
        }
    }

    private static string ReadHost(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) ? parsed.Host : string.Empty;

    private static string CreateInitials(string name)
    {
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "?",
            1 => words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant(),
            _ => string.Concat(words[0][..1], words[1][..1]).ToUpperInvariant(),
        };
    }
}

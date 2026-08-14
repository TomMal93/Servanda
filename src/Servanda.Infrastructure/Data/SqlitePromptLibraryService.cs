using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.Common;
using Servanda.Application.Prompts;
using Servanda.Domain.Areas;
using Servanda.Domain.Prompts;
using Servanda.Domain.Search;
using Servanda.Infrastructure.Data.Prompts;
using Servanda.Infrastructure.Data.Search;

namespace Servanda.Infrastructure.Data;

internal sealed class SqlitePromptLibraryService(
    IDbContextFactory<ServandaDbContext> contextFactory,
    TimeProvider timeProvider) : IPromptLibraryService
{
    private const int VisibleTagCount = 4;

    public async Task<PromptPage> SearchAsync(PromptQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        var categoryIds = await ResolveCategoryFilterAsync(database, query.AreaId, query.CategoryId, cancellationToken);
        var matchQuery = SearchText.BuildPrefixQuery(query.Text);
        var take = Math.Clamp(query.Take, 1, PromptQuery.PageSize);
        var skip = Math.Max(query.Skip, 0);
        var lastUsed = await ReadLastUsedAsync(database, cancellationToken);

        IReadOnlyList<string> ids;
        IReadOnlyList<string> tokens = [];
        int total;
        if (matchQuery is null)
        {
            (ids, total) = await ListPageAsync(
                database,
                query,
                categoryIds,
                lastUsed,
                skip,
                take,
                cancellationToken);
        }
        else
        {
            tokens = SearchText.Tokenize(query.Text);
            var ranked = await SearchQueries.RankPromptsAsync(
                database,
                matchQuery,
                SearchText.Normalize(query.Text),
                query.AreaId,
                categoryIds,
                0,
                PromptQuery.PageSize * 4,
                cancellationToken);
            var allowed = await FilterIdsAsync(database, ranked, query.Filter, lastUsed, cancellationToken);
            total = allowed.Count;
            ids = allowed.Skip(skip).Take(take).ToList();
        }

        var cards = await LoadCardsAsync(database, ids, tokens, lastUsed, cancellationToken);
        return new PromptPage(
            cards,
            total,
            skip + cards.Count < total,
            SearchText.IsQueryTooShort(query.Text),
            epoch);
    }

    public async Task<PromptEditorModel?> GetForEditAsync(string id, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        var prompt = await database.Prompts
            .AsNoTracking()
            .Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id,
                item.AreaId,
                item.CategoryId,
                item.Title,
                item.Description,
                item.Revision,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (prompt is null)
        {
            return null;
        }

        var tagNames = await ReadTagNamesAsync(database, [id], cancellationToken);
        var variants = await database.Set<PromptVariant>()
            .AsNoTracking()
            .Where(variant => variant.PromptId == id)
            .OrderBy(variant => variant.SortOrder)
            .ThenBy(variant => variant.Id)
            .Select(variant => new PromptVariantModel(
                variant.Id,
                variant.Name,
                variant.Target,
                variant.Content,
                variant.SortOrder))
            .ToListAsync(cancellationToken);
        var variables = await database.Set<PromptVariable>()
            .AsNoTracking()
            .Where(variable => variable.PromptId == id)
            .OrderBy(variable => variable.SortOrder)
            .ThenBy(variable => variable.Id)
            .Select(variable => new PromptVariableModel(
                variable.Id,
                variable.Name,
                variable.Label,
                variable.DefaultValue,
                variable.IsRequired,
                variable.IsMultiline,
                variable.SortOrder))
            .ToListAsync(cancellationToken);

        return new PromptEditorModel(
            prompt.Id,
            prompt.AreaId,
            prompt.CategoryId,
            prompt.Title,
            prompt.Description,
            tagNames.TryGetValue(prompt.Id, out var names) ? names : [],
            variants,
            variables,
            prompt.Revision,
            epoch);
    }

    public async Task<PromptScopeState> GetScopeAsync(
        string categoryId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var scopeKey = OrderingScopeKeys.Prompts(categoryId);
        var revision = await CollectionState.ReadScopeRevisionAsync(database, scopeKey, cancellationToken);
        return new PromptScopeState(scopeKey, revision ?? 0);
    }

    public async Task<IReadOnlyList<PromptVersionItem>> ListVersionsAsync(
        string promptId,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var versions = await database.PromptVersions
            .AsNoTracking()
            .Where(version => version.PromptId == promptId)
            .OrderByDescending(version => version.Id)
            .Select(version => new { version.Id, version.CreatedAt, version.SnapshotJson })
            .ToListAsync(cancellationToken);
        return versions
            .Select(version =>
            {
                var snapshot = PromptSnapshotSerializer.Deserialize(version.SnapshotJson);
                return new PromptVersionItem(
                    version.Id,
                    version.CreatedAt,
                    snapshot?.Variants.Count ?? 0,
                    snapshot?.Variables.Count ?? 0,
                    snapshot is not null);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PromptUsageItem>> ListUsageAsync(
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await database.PromptUsage
            .AsNoTracking()
            .OrderByDescending(entry => entry.Id)
            .Take(Math.Clamp(take, 1, PromptUsageEntry.RetainedEntries))
            .Select(entry => new PromptUsageItem(
                entry.Id,
                entry.PromptId,
                entry.PromptTitle,
                entry.VariantName,
                entry.UsedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<PromptResult> CreateAsync(
        CreatePromptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        if (!await IsModuleCategoryAsync(database, command.AreaId, command.CategoryId, cancellationToken))
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        var timestamp = timeProvider.GetUtcNow();
        var tagIds = await SqliteTagService.ResolveAsync(
            database,
            command.AreaId,
            command.TagNames,
            timeProvider,
            cancellationToken);
        var sortOrder = await database.Prompts.CountAsync(
            prompt => prompt.CategoryId == command.CategoryId,
            cancellationToken);
        var prompt = Prompt.Create(
            EntityId.NewUlid(timeProvider),
            command.AreaId,
            command.CategoryId,
            command.Title,
            command.Description,
            tagIds,
            command.Variants,
            command.Variables,
            command.AllowUnusedVariables,
            sortOrder,
            timestamp,
            () => EntityId.NewUlid(timeProvider),
            out var errors);
        if (prompt is null)
        {
            return new PromptResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    OrderingScopeKeys.Prompts(command.CategoryId),
                    command.ExpectedScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new PromptResult(WriteStatus.Conflict);
            }

            database.Prompts.Add(prompt);
            await database.SaveChangesAsync(cancellationToken);
            await SearchIndexWriter.UpdatePromptAsync(database, prompt.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 or 19)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        return new PromptResult(WriteStatus.Success, await ReadCardAsync(database, prompt.Id, cancellationToken));
    }

    public async Task<PromptResult> UpdateAsync(
        UpdatePromptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var prompt = await LoadAggregateAsync(database, command.Id, cancellationToken);
        if (prompt is null)
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        if (prompt.Revision != command.ExpectedRevision)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var timestamp = timeProvider.GetUtcNow();
        var previous = prompt.BuildSnapshot();
        var tagIds = await SqliteTagService.ResolveAsync(
            database,
            prompt.AreaId,
            command.TagNames,
            timeProvider,
            cancellationToken);
        var errors = prompt.UpdateContent(
            command.Title,
            command.Description,
            tagIds,
            command.Variants,
            command.Variables,
            command.AllowUnusedVariables,
            timestamp,
            () => EntityId.NewUlid(timeProvider));
        if (errors.Count > 0)
        {
            return new PromptResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            await SaveWithVersionAsync(database, prompt, previous, timestamp, cancellationToken);
            await SearchIndexWriter.UpdatePromptAsync(database, prompt.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        return new PromptResult(WriteStatus.Success, await ReadCardAsync(database, prompt.Id, cancellationToken));
    }

    public async Task<PromptResult> SetFavoriteAsync(
        SetPromptFavoriteCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var prompt = await database.Prompts.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (prompt is null)
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        if (prompt.Revision != command.ExpectedRevision)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        prompt.SetFavorite(command.IsFavorite, timeProvider.GetUtcNow());
        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        return new PromptResult(WriteStatus.Success, await ReadCardAsync(database, prompt.Id, cancellationToken));
    }

    public async Task<PromptResult> MoveAsync(
        MovePromptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var prompt = await database.Prompts.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (prompt is null)
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        if (prompt.Revision != command.ExpectedRevision)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        if (!await IsModuleCategoryAsync(database, prompt.AreaId, command.TargetCategoryId, cancellationToken))
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        var timestamp = timeProvider.GetUtcNow();
        var sourceCategoryId = prompt.CategoryId;
        var sourceKey = OrderingScopeKeys.Prompts(sourceCategoryId);
        var targetKey = OrderingScopeKeys.Prompts(command.TargetCategoryId);
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
                return new PromptResult(WriteStatus.Conflict);
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
                    return new PromptResult(WriteStatus.Conflict);
                }

                prompt.MoveTo(command.TargetCategoryId, timestamp);
                await database.SaveChangesAsync(cancellationToken);
                await RenumberAsync(database, sourceCategoryId, null, null, cancellationToken);
            }

            await RenumberAsync(
                database,
                command.TargetCategoryId,
                command.Id,
                command.BeforePromptId,
                cancellationToken);
            await SearchIndexWriter.UpdatePromptAsync(database, prompt.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PromptResult(WriteStatus.Conflict);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 or 19)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        return new PromptResult(WriteStatus.Success, await ReadCardAsync(database, prompt.Id, cancellationToken));
    }

    public async Task<PromptResult> DeleteAsync(
        DeletePromptCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var prompt = await database.Prompts.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (prompt is null)
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        if (prompt.Revision != command.ExpectedRevision)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var timestamp = timeProvider.GetUtcNow();
        var categoryId = prompt.CategoryId;

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    OrderingScopeKeys.Prompts(categoryId),
                    command.ExpectedScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new PromptResult(WriteStatus.Conflict);
            }

            database.Prompts.Remove(prompt);
            await database.SaveChangesAsync(cancellationToken);
            await SearchIndexWriter.RemovePromptAsync(database, command.Id, cancellationToken);
            await RenumberAsync(database, categoryId, null, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        return new PromptResult(WriteStatus.Success);
    }

    public async Task<PromptResult> RestoreVersionAsync(
        RestorePromptVersionCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var prompt = await LoadAggregateAsync(database, command.PromptId, cancellationToken);
        if (prompt is null)
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        if (prompt.Revision != command.ExpectedRevision)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        var version = await database.PromptVersions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == command.VersionId && item.PromptId == command.PromptId,
                cancellationToken);
        if (version is null)
        {
            return new PromptResult(WriteStatus.NotFound);
        }

        var snapshot = PromptSnapshotSerializer.Deserialize(version.SnapshotJson);
        if (snapshot is null)
        {
            return new PromptResult(
                WriteStatus.ValidationFailed,
                Errors: new Dictionary<string, string[]>
                {
                    ["Version"] = ["Zapisana wersja ma nieobsługiwany format i nie może zostać przywrócona."],
                });
        }

        var timestamp = timeProvider.GetUtcNow();
        var previous = prompt.BuildSnapshot();
        var tagIds = prompt.Tags.Select(tag => tag.TagId).ToList();
        var errors = prompt.UpdateContent(
            prompt.Title,
            prompt.Description,
            tagIds,
            [.. snapshot.Variants
                .OrderBy(variant => variant.SortOrder)
                .Select(variant => new PromptVariantDraft(
                    variant.Id,
                    variant.Name,
                    variant.Target,
                    variant.Content))],
            [.. snapshot.Variables.OrderBy(variable => variable.SortOrder).Select(variable => new PromptVariableDraft(
                variable.Id,
                variable.Name,
                variable.Label,
                variable.DefaultValue,
                variable.IsRequired,
                variable.IsMultiline))],
            allowUnusedVariables: true,
            timestamp,
            () => EntityId.NewUlid(timeProvider));
        if (errors.Count > 0)
        {
            return new PromptResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            await SaveWithVersionAsync(database, prompt, previous, timestamp, cancellationToken);
            await SearchIndexWriter.UpdatePromptAsync(database, prompt.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new PromptResult(WriteStatus.Conflict);
        }

        return new PromptResult(WriteStatus.Success, await ReadCardAsync(database, prompt.Id, cancellationToken));
    }

    public async Task<WriteStatus> RecordUsageAsync(
        RecordPromptUsageCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var usage = await (from prompt in database.Prompts.AsNoTracking()
                           join variant in database.Set<PromptVariant>().AsNoTracking()
                               on prompt.Id equals variant.PromptId
                           where prompt.Id == command.PromptId && variant.Id == command.VariantId
                           select new { prompt.Title, VariantName = variant.Name })
            .SingleOrDefaultAsync(cancellationToken);
        if (usage is null)
        {
            return WriteStatus.NotFound;
        }

        database.PromptUsage.Add(new PromptUsageEntry(
            EntityId.NewUlid(timeProvider),
            command.PromptId,
            command.VariantId,
            usage.Title,
            usage.VariantName,
            timeProvider.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);

        // Retencja historii nie zwiększa rewizji promptu.
        var retained = await database.PromptUsage
            .AsNoTracking()
            .OrderByDescending(entry => entry.Id)
            .Take(PromptUsageEntry.RetainedEntries)
            .Select(entry => entry.Id)
            .ToListAsync(cancellationToken);
        await database.PromptUsage
            .Where(entry => !retained.Contains(entry.Id))
            .ExecuteDeleteAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return WriteStatus.Success;
    }

    private static async Task<Prompt?> LoadAggregateAsync(
        ServandaDbContext database,
        string id,
        CancellationToken cancellationToken) =>
        await database.Prompts
            .Include(prompt => prompt.Tags)
            .Include(prompt => prompt.Variants)
            .Include(prompt => prompt.Variables)
            .SingleOrDefaultAsync(prompt => prompt.Id == id, cancellationToken);

    /// <summary>
    /// Zapisuje prompt wraz z wersją poprzedniej treści, jeżeli zmieniły się warianty albo zmienne.
    /// </summary>
    private async Task SaveWithVersionAsync(
        ServandaDbContext database,
        Prompt prompt,
        PromptSnapshot previous,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var previousJson = PromptSnapshotSerializer.Serialize(previous);
        var contentChanged = !string.Equals(
            previousJson,
            PromptSnapshotSerializer.Serialize(prompt.BuildSnapshot()),
            StringComparison.Ordinal);
        if (contentChanged)
        {
            database.PromptVersions.Add(new PromptVersion(
                EntityId.NewUlid(timeProvider),
                prompt.Id,
                previousJson,
                timestamp));
        }

        await database.SaveChangesAsync(cancellationToken);

        if (!contentChanged)
        {
            return;
        }

        var retained = await database.PromptVersions
            .AsNoTracking()
            .Where(version => version.PromptId == prompt.Id)
            .OrderByDescending(version => version.Id)
            .Take(PromptVersion.RetainedVersionsPerPrompt)
            .Select(version => version.Id)
            .ToListAsync(cancellationToken);
        await database.PromptVersions
            .Where(version => version.PromptId == prompt.Id && !retained.Contains(version.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task<(IReadOnlyList<string> Ids, int Total)> ListPageAsync(
        ServandaDbContext database,
        PromptQuery query,
        IReadOnlyList<string>? categoryIds,
        IReadOnlyDictionary<string, DateTimeOffset> lastUsed,
        int skip,
        int take,
        CancellationToken cancellationToken)
    {
        var filtered = database.Prompts
            .AsNoTracking()
            .Where(prompt => prompt.AreaId == query.AreaId);
        if (categoryIds is not null)
        {
            filtered = filtered.Where(prompt => categoryIds.Contains(prompt.CategoryId));
        }

        if (query.Filter == PromptFilter.Favorites)
        {
            filtered = filtered.Where(prompt => prompt.IsFavorite);
        }

        if (query.Filter == PromptFilter.RecentlyUsed)
        {
            var usedIds = lastUsed.Keys.ToList();
            var used = await filtered
                .Where(prompt => usedIds.Contains(prompt.Id))
                .Select(prompt => prompt.Id)
                .ToListAsync(cancellationToken);
            var ordered = used
                .OrderByDescending(id => lastUsed[id])
                .ThenBy(id => id, StringComparer.Ordinal)
                .ToList();
            return (ordered.Skip(skip).Take(take).ToList(), ordered.Count);
        }

        var total = await filtered.CountAsync(cancellationToken);
        var ids = await filtered
            .OrderBy(prompt => prompt.CategoryId)
            .ThenBy(prompt => prompt.SortOrder)
            .ThenBy(prompt => prompt.Id)
            .Skip(skip)
            .Take(take)
            .Select(prompt => prompt.Id)
            .ToListAsync(cancellationToken);
        return (ids, total);
    }

    private static async Task<List<string>> FilterIdsAsync(
        ServandaDbContext database,
        IReadOnlyList<string> ids,
        PromptFilter filter,
        IReadOnlyDictionary<string, DateTimeOffset> lastUsed,
        CancellationToken cancellationToken)
    {
        if (filter == PromptFilter.All || ids.Count == 0)
        {
            return [.. ids];
        }

        if (filter == PromptFilter.RecentlyUsed)
        {
            return [.. ids.Where(lastUsed.ContainsKey)];
        }

        var favorites = await database.Prompts
            .AsNoTracking()
            .Where(prompt => ids.Contains(prompt.Id) && prompt.IsFavorite)
            .Select(prompt => prompt.Id)
            .ToListAsync(cancellationToken);
        var allowed = favorites.ToHashSet(StringComparer.Ordinal);
        return [.. ids.Where(allowed.Contains)];
    }

    private static async Task<IReadOnlyList<PromptCard>> LoadCardsAsync(
        ServandaDbContext database,
        IReadOnlyList<string> ids,
        IReadOnlyList<string> queryTokens,
        IReadOnlyDictionary<string, DateTimeOffset> lastUsed,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        var prompts = await database.Prompts
            .AsNoTracking()
            .Where(prompt => ids.Contains(prompt.Id))
            .Select(prompt => new
            {
                prompt.Id,
                prompt.CategoryId,
                prompt.Title,
                prompt.Description,
                prompt.IsFavorite,
                prompt.SortOrder,
                prompt.Revision,
                VariantCount = database.Set<PromptVariant>().Count(variant => variant.PromptId == prompt.Id),
            })
            .ToListAsync(cancellationToken);
        var tagNames = await ReadTagNamesAsync(database, ids, cancellationToken);
        var categoryPaths = await ReadCategoryPathsAsync(
            database,
            prompts.Select(prompt => prompt.CategoryId).Distinct(StringComparer.Ordinal).ToList(),
            cancellationToken);

        var byId = prompts.ToDictionary(prompt => prompt.Id, StringComparer.Ordinal);
        var cards = new List<PromptCard>(ids.Count);
        foreach (var id in ids)
        {
            if (!byId.TryGetValue(id, out var prompt))
            {
                continue;
            }

            var tags = tagNames.TryGetValue(id, out var names) ? names : [];
            var visibleTags = tags.Take(VisibleTagCount).ToList();
            var categoryPath = categoryPaths.TryGetValue(prompt.CategoryId, out var path) ? path : string.Empty;
            cards.Add(new PromptCard(
                prompt.Id,
                prompt.CategoryId,
                categoryPath,
                prompt.Title,
                prompt.Description,
                prompt.IsFavorite,
                visibleTags,
                Math.Max(tags.Count - visibleTags.Count, 0),
                prompt.VariantCount,
                lastUsed.TryGetValue(prompt.Id, out var usedAt) ? usedAt : null,
                MatchExplanation.For(
                    queryTokens,
                    string.Join(' ', [prompt.Title, prompt.Description, categoryPath, .. visibleTags]),
                    string.Join(' ', tags)),
                prompt.SortOrder,
                prompt.Revision));
        }

        return cards;
    }

    private static async Task<PromptCard?> ReadCardAsync(
        ServandaDbContext database,
        string id,
        CancellationToken cancellationToken)
    {
        var lastUsed = await ReadLastUsedAsync(database, cancellationToken);
        var cards = await LoadCardsAsync(database, [id], [], lastUsed, cancellationToken);
        return cards.Count == 1 ? cards[0] : null;
    }

    private static async Task<IReadOnlyDictionary<string, DateTimeOffset>> ReadLastUsedAsync(
        ServandaDbContext database,
        CancellationToken cancellationToken)
    {
        // Historia jest ograniczona retencją do 500 wpisów, więc agregacja po stronie klienta jest bezpieczna.
        var entries = await database.PromptUsage
            .AsNoTracking()
            .Where(entry => entry.PromptId != null)
            .Select(entry => new { PromptId = entry.PromptId!, entry.UsedAt })
            .ToListAsync(cancellationToken);
        return entries
            .GroupBy(entry => entry.PromptId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Max(entry => entry.UsedAt),
                StringComparer.Ordinal);
    }

    private static async Task<IReadOnlyDictionary<string, List<string>>> ReadTagNamesAsync(
        ServandaDbContext database,
        IReadOnlyList<string> promptIds,
        CancellationToken cancellationToken)
    {
        var links = await (from link in database.Set<PromptTag>().AsNoTracking()
                           join tag in database.Tags.AsNoTracking() on link.TagId equals tag.Id
                           where promptIds.Contains(link.PromptId)
                           orderby tag.NormalizedName
                           select new { link.PromptId, tag.Name })
            .ToListAsync(cancellationToken);
        return links
            .GroupBy(link => link.PromptId, StringComparer.Ordinal)
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
                && area.ModuleKey == "prompts"
                && area.Availability == Area.ActiveAvailability,
            cancellationToken);

    private static async Task RenumberAsync(
        ServandaDbContext database,
        string categoryId,
        string? movedId,
        string? beforeId,
        CancellationToken cancellationToken)
    {
        var members = await database.Prompts
            .AsNoTracking()
            .Where(prompt => prompt.CategoryId == categoryId)
            .OrderBy(prompt => prompt.SortOrder)
            .ThenBy(prompt => prompt.Id)
            .Select(prompt => prompt.Id)
            .ToListAsync(cancellationToken);
        if (movedId is not null)
        {
            members.Remove(movedId);
            var index = beforeId is null ? members.Count : members.IndexOf(beforeId);
            members.Insert(index < 0 ? members.Count : index, movedId);
        }

        var offset = members.Count + 1;
        await database.Prompts
            .Where(prompt => prompt.CategoryId == categoryId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(prompt => prompt.SortOrder, prompt => prompt.SortOrder + offset),
                cancellationToken);
        for (var sortOrder = 0; sortOrder < members.Count; sortOrder++)
        {
            var id = members[sortOrder];
            var position = sortOrder;
            await database.Prompts
                .Where(prompt => prompt.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(prompt => prompt.SortOrder, position),
                    cancellationToken);
        }
    }
}

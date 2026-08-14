using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.Catalog;
using Servanda.Application.Common;
using Servanda.Domain.Areas;
using Servanda.Domain.Catalog;
using Servanda.Infrastructure.Data.Search;

namespace Servanda.Infrastructure.Data;

internal sealed class SqliteCategoryService(
    IDbContextFactory<ServandaDbContext> contextFactory,
    TimeProvider timeProvider) : ICategoryService
{
    public async Task<CategoryTree> GetTreeAsync(string areaId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(areaId);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        var categories = await database.Categories
            .AsNoTracking()
            .Where(category => category.AreaId == areaId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Select(category => new CategoryItem(
                category.Id,
                category.ParentId,
                category.Name,
                category.Description,
                category.SortOrder,
                category.Revision))
            .ToListAsync(cancellationToken);
        var moduleKey = await ReadModuleKeyAsync(database, areaId, cancellationToken);
        var counts = await ReadDirectCountsAsync(database, areaId, moduleKey, cancellationToken);

        var scopeKeys = categories
            .Select(category => OrderingScopeKeys.Categories(areaId, category.Id))
            .Append(OrderingScopeKeys.RootCategories(areaId))
            .ToList();
        var scopeRevisions = await CollectionState.ReadScopeRevisionsAsync(database, scopeKeys, cancellationToken);

        var byParent = categories
            .GroupBy(category => category.ParentId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        var roots = BuildNodes(byParent, string.Empty, counts);
        return new CategoryTree(areaId, roots, 0, epoch, scopeRevisions);
    }

    public async Task<CategoryResult> CreateAsync(
        CreateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        var moduleKey = await ReadModuleKeyAsync(database, command.AreaId, cancellationToken);
        if (moduleKey is null)
        {
            return new CategoryResult(WriteStatus.NotFound);
        }

        if (command.ParentId is not null)
        {
            var depth = await ReadDepthAsync(database, command.AreaId, command.ParentId, cancellationToken);
            if (depth is null)
            {
                return new CategoryResult(WriteStatus.NotFound);
            }

            if (depth + 1 >= Category.MaxDepth)
            {
                return new CategoryResult(
                    WriteStatus.ValidationFailed,
                    Errors: new Dictionary<string, string[]>
                    {
                        [nameof(Category.ParentId)] =
                            [$"Drzewo kategorii może mieć najwyżej {Category.MaxDepth} poziomów."],
                    });
            }
        }

        var timestamp = timeProvider.GetUtcNow();
        var scopeKey = OrderingScopeKeys.Categories(command.AreaId, command.ParentId);
        var nextSortOrder = await database.Categories
            .Where(category => category.AreaId == command.AreaId && category.ParentId == command.ParentId)
            .CountAsync(cancellationToken);
        var category = Category.Create(
            EntityId.NewUlid(timeProvider),
            command.AreaId,
            command.ParentId,
            command.Name,
            command.Description,
            nextSortOrder,
            timestamp,
            out var errors);
        if (category is null)
        {
            return new CategoryResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    scopeKey,
                    command.ExpectedScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new CategoryResult(WriteStatus.Conflict);
            }

            database.Categories.Add(category);
            await database.SaveChangesAsync(cancellationToken);
            await CollectionState.EnsureScopesAsync(
                database,
                ChildScopeKeys(command.AreaId, category.Id, moduleKey),
                timestamp,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 or 19)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        return new CategoryResult(WriteStatus.Success, ToItem(category));
    }

    public async Task<CategoryResult> UpdateAsync(
        UpdateCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        var category = await database.Categories.SingleOrDefaultAsync(
            item => item.Id == command.Id,
            cancellationToken);
        if (category is null)
        {
            return new CategoryResult(WriteStatus.NotFound);
        }

        if (category.Revision != command.ExpectedRevision)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        var errors = category.UpdateContent(command.Name, command.Description, timeProvider.GetUtcNow());
        if (errors.Count > 0)
        {
            return new CategoryResult(WriteStatus.ValidationFailed, Errors: errors);
        }

        try
        {
            await database.SaveChangesAsync(cancellationToken);
            await SearchIndexWriter.UpdateCategorySubtreeAsync(database, category.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        return new CategoryResult(WriteStatus.Success, ToItem(category));
    }

    public async Task<CategoryResult> MoveAsync(
        MoveCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        var category = await database.Categories.SingleOrDefaultAsync(
            item => item.Id == command.Id,
            cancellationToken);
        if (category is null)
        {
            return new CategoryResult(WriteStatus.NotFound);
        }

        if (category.Revision != command.ExpectedRevision)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        var areaId = category.AreaId;
        var sourceParentId = category.ParentId;
        var targetParentId = command.TargetParentId;
        var sameScope = string.Equals(sourceParentId, targetParentId, StringComparison.Ordinal);
        if (!sameScope && targetParentId is not null)
        {
            var subtree = await ReadSubtreeIdsAsync(database, command.Id, cancellationToken);
            if (subtree.Contains(targetParentId, StringComparer.Ordinal))
            {
                return new CategoryResult(
                    WriteStatus.ValidationFailed,
                    Errors: new Dictionary<string, string[]>
                    {
                        [nameof(Category.ParentId)] = ["Kategoria nie może zostać przeniesiona do swojego potomka."],
                    });
            }

            var targetDepth = await ReadDepthAsync(database, areaId, targetParentId, cancellationToken);
            var subtreeDepth = await ReadSubtreeHeightAsync(database, command.Id, cancellationToken);
            if (targetDepth is null)
            {
                return new CategoryResult(WriteStatus.NotFound);
            }

            if (targetDepth + 1 + subtreeDepth >= Category.MaxDepth)
            {
                return new CategoryResult(
                    WriteStatus.ValidationFailed,
                    Errors: new Dictionary<string, string[]>
                    {
                        [nameof(Category.ParentId)] =
                            [$"Drzewo kategorii może mieć najwyżej {Category.MaxDepth} poziomów."],
                    });
            }
        }

        var timestamp = timeProvider.GetUtcNow();
        var sourceKey = OrderingScopeKeys.Categories(areaId, sourceParentId);
        var targetKey = OrderingScopeKeys.Categories(areaId, targetParentId);

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    sourceKey,
                    command.ExpectedSourceScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new CategoryResult(WriteStatus.Conflict);
            }

            if (!sameScope
                && !await CollectionState.TryAdvanceScopeAsync(
                    database,
                    targetKey,
                    command.ExpectedTargetScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new CategoryResult(WriteStatus.Conflict);
            }

            if (!sameScope)
            {
                category.MoveTo(targetParentId, timestamp);
                await database.SaveChangesAsync(cancellationToken);
                await RenumberSiblingsAsync(database, areaId, sourceParentId, null, null, cancellationToken);
            }

            await RenumberSiblingsAsync(
                database,
                areaId,
                targetParentId,
                command.Id,
                command.BeforeCategoryId,
                cancellationToken);
            await SearchIndexWriter.UpdateCategorySubtreeAsync(database, command.Id, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }
        catch (SqliteException exception) when (exception.SqliteErrorCode is 5 or 6 or 19)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        return new CategoryResult(WriteStatus.Success, ToItem(category));
    }

    public async Task<CategoryResult> DeleteAsync(
        DeleteCategoryCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var epoch = await CollectionState.ReadEpochAsync(database, cancellationToken);
        if (!string.Equals(epoch, command.ContentEpoch, StringComparison.Ordinal))
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        var category = await database.Categories.SingleOrDefaultAsync(
            item => item.Id == command.Id,
            cancellationToken);
        if (category is null)
        {
            return new CategoryResult(WriteStatus.NotFound);
        }

        if (category.Revision != command.ExpectedRevision)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        var hasChildren = await database.Categories.AnyAsync(
            item => item.ParentId == command.Id,
            cancellationToken);
        var hasTools = await database.Tools.AnyAsync(item => item.CategoryId == command.Id, cancellationToken);
        var hasPrompts = await database.Prompts.AnyAsync(item => item.CategoryId == command.Id, cancellationToken);
        if (hasChildren || hasTools || hasPrompts)
        {
            return new CategoryResult(
                WriteStatus.ValidationFailed,
                Errors: new Dictionary<string, string[]>
                {
                    [nameof(Category.Name)] =
                        ["Usuń albo przenieś zawartość kategorii, zanim ją usuniesz."],
                });
        }

        var timestamp = timeProvider.GetUtcNow();
        var parentScopeKey = OrderingScopeKeys.Categories(category.AreaId, category.ParentId);
        var moduleKey = await ReadModuleKeyAsync(database, category.AreaId, cancellationToken);

        try
        {
            if (!await CollectionState.TryAdvanceScopeAsync(
                    database,
                    parentScopeKey,
                    command.ExpectedScopeRevision,
                    timestamp,
                    cancellationToken))
            {
                return new CategoryResult(WriteStatus.Conflict);
            }

            database.Categories.Remove(category);
            await database.SaveChangesAsync(cancellationToken);
            await CollectionState.RemoveScopesAsync(
                database,
                ChildScopeKeys(category.AreaId, category.Id, moduleKey),
                cancellationToken);
            await RenumberSiblingsAsync(database, category.AreaId, category.ParentId, null, null, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new CategoryResult(WriteStatus.Conflict);
        }

        return new CategoryResult(WriteStatus.Success);
    }

    private static List<CategoryNode> BuildNodes(
        Dictionary<string, List<CategoryItem>> byParent,
        string parentKey,
        IReadOnlyDictionary<string, int> counts)
    {
        if (!byParent.TryGetValue(parentKey, out var children))
        {
            return [];
        }

        var nodes = new List<CategoryNode>(children.Count);
        foreach (var child in children)
        {
            var childNodes = BuildNodes(byParent, child.Id, counts);
            var directCount = counts.TryGetValue(child.Id, out var count) ? count : 0;
            var totalCount = directCount + childNodes.Sum(node => node.TotalItemCount);
            nodes.Add(new CategoryNode(child, directCount, totalCount, childNodes));
        }

        return nodes;
    }

    private static async Task<string?> ReadModuleKeyAsync(
        ServandaDbContext database,
        string areaId,
        CancellationToken cancellationToken)
    {
        var moduleKeys = await database.Areas
            .AsNoTracking()
            .Where(area => area.Id == areaId && area.Availability == Area.ActiveAvailability)
            .Select(area => area.ModuleKey)
            .ToListAsync(cancellationToken);
        return moduleKeys.Count == 1 ? moduleKeys[0] : null;
    }

    private static async Task<IReadOnlyDictionary<string, int>> ReadDirectCountsAsync(
        ServandaDbContext database,
        string areaId,
        string? moduleKey,
        CancellationToken cancellationToken)
    {
        if (moduleKey == "tools")
        {
            var toolCounts = await database.Tools
                .AsNoTracking()
                .Where(tool => tool.AreaId == areaId)
                .GroupBy(tool => tool.CategoryId)
                .Select(group => new { CategoryId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
            return toolCounts.ToDictionary(item => item.CategoryId, item => item.Count, StringComparer.Ordinal);
        }

        if (moduleKey == "prompts")
        {
            var promptCounts = await database.Prompts
                .AsNoTracking()
                .Where(prompt => prompt.AreaId == areaId)
                .GroupBy(prompt => prompt.CategoryId)
                .Select(group => new { CategoryId = group.Key, Count = group.Count() })
                .ToListAsync(cancellationToken);
            return promptCounts.ToDictionary(item => item.CategoryId, item => item.Count, StringComparer.Ordinal);
        }

        return new Dictionary<string, int>(StringComparer.Ordinal);
    }

    private static List<string> ChildScopeKeys(string areaId, string categoryId, string? moduleKey)
    {
        var keys = new List<string> { OrderingScopeKeys.Categories(areaId, categoryId) };
        if (moduleKey == "tools")
        {
            keys.Add(OrderingScopeKeys.Tools(categoryId, Domain.Tools.Tool.FeaturedGroup));
            keys.Add(OrderingScopeKeys.Tools(categoryId, Domain.Tools.Tool.RegularGroup));
        }
        else if (moduleKey == "prompts")
        {
            keys.Add(OrderingScopeKeys.Prompts(categoryId));
        }

        return keys;
    }

    private static async Task<int?> ReadDepthAsync(
        ServandaDbContext database,
        string areaId,
        string categoryId,
        CancellationToken cancellationToken)
    {
        var depths = await database.Database
            .SqlQueryRaw<int>(
                """
                WITH RECURSIVE ancestors(id, parent_id, depth) AS (
                    SELECT id, parent_id, 0 FROM categories WHERE id = {0} AND area_id = {1}
                    UNION ALL
                    SELECT parent.id, parent.parent_id, child.depth + 1
                    FROM categories parent
                    JOIN ancestors child ON parent.id = child.parent_id
                )
                SELECT COALESCE(MAX(depth), 0) AS Value FROM ancestors
                """,
                categoryId,
                areaId)
            .ToListAsync(cancellationToken);
        var exists = await database.Categories.AnyAsync(
            category => category.Id == categoryId && category.AreaId == areaId,
            cancellationToken);
        return exists ? depths.FirstOrDefault() : null;
    }

    private static async Task<int> ReadSubtreeHeightAsync(
        ServandaDbContext database,
        string categoryId,
        CancellationToken cancellationToken)
    {
        var heights = await database.Database
            .SqlQueryRaw<int>(
                """
                WITH RECURSIVE subtree(id, depth) AS (
                    SELECT id, 0 FROM categories WHERE id = {0}
                    UNION ALL
                    SELECT child.id, parent.depth + 1
                    FROM categories child
                    JOIN subtree parent ON child.parent_id = parent.id
                )
                SELECT COALESCE(MAX(depth), 0) AS Value FROM subtree
                """,
                categoryId)
            .ToListAsync(cancellationToken);
        return heights.FirstOrDefault();
    }

    private static async Task<IReadOnlyList<string>> ReadSubtreeIdsAsync(
        ServandaDbContext database,
        string categoryId,
        CancellationToken cancellationToken) =>
        await database.Database
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

    /// <summary>
    /// Przenumerowuje rodzeństwo gęsto od zera, nie naruszając indeksu unikalnego również przejściowo.
    /// </summary>
    private static async Task RenumberSiblingsAsync(
        ServandaDbContext database,
        string areaId,
        string? parentId,
        string? movedId,
        string? beforeId,
        CancellationToken cancellationToken)
    {
        var siblings = await database.Categories
            .AsNoTracking()
            .Where(category => category.AreaId == areaId && category.ParentId == parentId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Id)
            .Select(category => category.Id)
            .ToListAsync(cancellationToken);

        if (movedId is not null)
        {
            siblings.Remove(movedId);
            var index = beforeId is null ? siblings.Count : siblings.IndexOf(beforeId);
            siblings.Insert(index < 0 ? siblings.Count : index, movedId);
        }

        var offset = siblings.Count + 1;
        await database.Categories
            .Where(category => category.AreaId == areaId && category.ParentId == parentId)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(category => category.SortOrder, category => category.SortOrder + offset),
                cancellationToken);
        for (var sortOrder = 0; sortOrder < siblings.Count; sortOrder++)
        {
            var id = siblings[sortOrder];
            var position = sortOrder;
            await database.Categories
                .Where(category => category.Id == id)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(category => category.SortOrder, position),
                    cancellationToken);
        }
    }

    private static CategoryItem ToItem(Category category) =>
        new(
            category.Id,
            category.ParentId,
            category.Name,
            category.Description,
            category.SortOrder,
            category.Revision);
}

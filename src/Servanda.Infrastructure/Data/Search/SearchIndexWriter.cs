using Microsoft.EntityFrameworkCore;
using Servanda.Domain.Prompts;
using Servanda.Domain.Search;
using Servanda.Domain.Tools;

namespace Servanda.Infrastructure.Data.Search;

/// <summary>
/// Utrzymuje pochodne dokumenty FTS5 w tej samej transakcji co zapis danych domenowych.
/// Wartości wspólnych kategorii i tagów są zawsze pobierane z bieżącej bazy.
/// </summary>
internal static class SearchIndexWriter
{
    private const string CategoryPathSql =
        """
        WITH RECURSIVE ancestors(id, parent_id, name, depth) AS (
            SELECT id, parent_id, name, 0 FROM categories WHERE id = {0}
            UNION ALL
            SELECT parent.id, parent.parent_id, parent.name, child.depth + 1
            FROM categories parent
            JOIN ancestors child ON parent.id = child.parent_id
        )
        SELECT name AS Value FROM ancestors ORDER BY depth DESC
        """;

    public static async Task UpdateToolAsync(
        ServandaDbContext database,
        string toolId,
        CancellationToken cancellationToken)
    {
        var tool = await database.Tools
            .AsNoTracking()
            .Where(item => item.Id == toolId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.Description,
                item.Url,
                item.CategoryId,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (tool is null)
        {
            await RemoveToolAsync(database, toolId, cancellationToken);
            return;
        }

        var tags = await ReadToolTagNamesAsync(database, toolId, cancellationToken);
        var categoryPath = await ReadCategoryPathAsync(database, tool.CategoryId, cancellationToken);

        await database.Database.ExecuteSqlRawAsync(
            "DELETE FROM tool_search WHERE entity_id = {0}",
            [toolId],
            cancellationToken);
        await database.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO tool_search(entity_id, name, tags, category_path, url, description)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5})
            """,
            [
                tool.Id,
                SearchText.Normalize(tool.Name),
                SearchText.Normalize(string.Join(' ', tags)),
                SearchText.Normalize(categoryPath),
                SearchText.Normalize(tool.Url),
                SearchText.Normalize(tool.Description),
            ],
            cancellationToken);
    }

    public static Task RemoveToolAsync(
        ServandaDbContext database,
        string toolId,
        CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlRawAsync(
            "DELETE FROM tool_search WHERE entity_id = {0}",
            [toolId],
            cancellationToken);

    public static async Task UpdatePromptAsync(
        ServandaDbContext database,
        string promptId,
        CancellationToken cancellationToken)
    {
        var prompt = await database.Prompts
            .AsNoTracking()
            .Where(item => item.Id == promptId)
            .Select(item => new
            {
                item.Id,
                item.Title,
                item.Description,
                item.CategoryId,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (prompt is null)
        {
            await RemovePromptAsync(database, promptId, cancellationToken);
            return;
        }

        var tags = await ReadPromptTagNamesAsync(database, promptId, cancellationToken);
        var categoryPath = await ReadCategoryPathAsync(database, prompt.CategoryId, cancellationToken);
        var variants = await database.Set<PromptVariant>()
            .AsNoTracking()
            .Where(variant => variant.PromptId == promptId)
            .OrderBy(variant => variant.SortOrder)
            .Select(variant => new { variant.Name, variant.Target, variant.Content })
            .ToListAsync(cancellationToken);

        await database.Database.ExecuteSqlRawAsync(
            "DELETE FROM prompt_search WHERE entity_id = {0}",
            [promptId],
            cancellationToken);
        await database.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO prompt_search(
                entity_id, title, tags, category_path, variant_names, variant_targets, description, variant_content)
            VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7})
            """,
            [
                prompt.Id,
                SearchText.Normalize(prompt.Title),
                SearchText.Normalize(string.Join(' ', tags)),
                SearchText.Normalize(categoryPath),
                SearchText.Normalize(string.Join(' ', variants.Select(variant => variant.Name))),
                SearchText.Normalize(string.Join(' ', variants.Select(variant => variant.Target))),
                SearchText.Normalize(prompt.Description),
                SearchText.Normalize(string.Join(' ', variants.Select(variant => variant.Content))),
            ],
            cancellationToken);
    }

    public static Task RemovePromptAsync(
        ServandaDbContext database,
        string promptId,
        CancellationToken cancellationToken) =>
        database.Database.ExecuteSqlRawAsync(
            "DELETE FROM prompt_search WHERE entity_id = {0}",
            [promptId],
            cancellationToken);

    /// <summary>
    /// Przebudowuje dokumenty elementów zależnych od zmienionej kategorii wraz z jej poddrzewem.
    /// </summary>
    public static async Task UpdateCategorySubtreeAsync(
        ServandaDbContext database,
        string categoryId,
        CancellationToken cancellationToken)
    {
        var categoryIds = await ReadSubtreeIdsAsync(database, categoryId, cancellationToken);
        var toolIds = await database.Tools
            .AsNoTracking()
            .Where(tool => categoryIds.Contains(tool.CategoryId))
            .Select(tool => tool.Id)
            .ToListAsync(cancellationToken);
        foreach (var toolId in toolIds)
        {
            await UpdateToolAsync(database, toolId, cancellationToken);
        }

        var promptIds = await database.Prompts
            .AsNoTracking()
            .Where(prompt => categoryIds.Contains(prompt.CategoryId))
            .Select(prompt => prompt.Id)
            .ToListAsync(cancellationToken);
        foreach (var promptId in promptIds)
        {
            await UpdatePromptAsync(database, promptId, cancellationToken);
        }
    }

    /// <summary>
    /// Przebudowuje dokumenty wszystkich elementów korzystających ze zmienionego tagu.
    /// </summary>
    public static async Task UpdateTagUsagesAsync(
        ServandaDbContext database,
        string tagId,
        CancellationToken cancellationToken)
    {
        var toolIds = await database.Set<ToolTag>()
            .AsNoTracking()
            .Where(link => link.TagId == tagId)
            .Select(link => link.ToolId)
            .ToListAsync(cancellationToken);
        foreach (var toolId in toolIds)
        {
            await UpdateToolAsync(database, toolId, cancellationToken);
        }

        var promptIds = await database.Set<PromptTag>()
            .AsNoTracking()
            .Where(link => link.TagId == tagId)
            .Select(link => link.PromptId)
            .ToListAsync(cancellationToken);
        foreach (var promptId in promptIds)
        {
            await UpdatePromptAsync(database, promptId, cancellationToken);
        }
    }

    /// <summary>
    /// Pełna odbudowa obu indeksów wyłącznie na podstawie tabel domenowych.
    /// </summary>
    public static async Task RebuildAllAsync(ServandaDbContext database, CancellationToken cancellationToken)
    {
        await database.Database.ExecuteSqlRawAsync("DELETE FROM tool_search", cancellationToken);
        await database.Database.ExecuteSqlRawAsync("DELETE FROM prompt_search", cancellationToken);

        var toolIds = await database.Tools
            .AsNoTracking()
            .Select(tool => tool.Id)
            .ToListAsync(cancellationToken);
        foreach (var toolId in toolIds)
        {
            await UpdateToolAsync(database, toolId, cancellationToken);
        }

        var promptIds = await database.Prompts
            .AsNoTracking()
            .Select(prompt => prompt.Id)
            .ToListAsync(cancellationToken);
        foreach (var promptId in promptIds)
        {
            await UpdatePromptAsync(database, promptId, cancellationToken);
        }
    }

    private static async Task<IReadOnlyList<string>> ReadToolTagNamesAsync(
        ServandaDbContext database,
        string toolId,
        CancellationToken cancellationToken) =>
        await (from link in database.Set<ToolTag>().AsNoTracking()
               join tag in database.Tags.AsNoTracking() on link.TagId equals tag.Id
               where link.ToolId == toolId
               orderby tag.NormalizedName
               select tag.Name)
            .ToListAsync(cancellationToken);

    private static async Task<IReadOnlyList<string>> ReadPromptTagNamesAsync(
        ServandaDbContext database,
        string promptId,
        CancellationToken cancellationToken) =>
        await (from link in database.Set<PromptTag>().AsNoTracking()
               join tag in database.Tags.AsNoTracking() on link.TagId equals tag.Id
               where link.PromptId == promptId
               orderby tag.NormalizedName
               select tag.Name)
            .ToListAsync(cancellationToken);

    private static async Task<string> ReadCategoryPathAsync(
        ServandaDbContext database,
        string categoryId,
        CancellationToken cancellationToken)
    {
        var names = await database.Database
            .SqlQueryRaw<string>(CategoryPathSql, categoryId)
            .ToListAsync(cancellationToken);
        return string.Join(' ', names);
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
}

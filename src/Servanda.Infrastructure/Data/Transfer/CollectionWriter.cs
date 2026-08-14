using Microsoft.EntityFrameworkCore;
using Servanda.Domain.Areas;
using Servanda.Domain.Catalog;
using Servanda.Domain.Prompts;
using Servanda.Domain.Tools;
using Servanda.Infrastructure.Data.Prompts;
using Servanda.Infrastructure.Data.Search;

namespace Servanda.Infrastructure.Data.Transfer;

/// <summary>
/// Zapisuje kompletny dokument kolekcji do bazy: kanonicznej przy imporcie i stagingowej przy walidacji.
/// </summary>
internal static class CollectionWriter
{
    public static async Task ReplaceAsync(
        ServandaDbContext database,
        ExportDocument document,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        await DeleteDomainDataAsync(database, cancellationToken);
        await InsertAsync(database, document, cancellationToken);
        await RebuildOrderingScopesAsync(database, document, timestamp, cancellationToken);
        await SearchIndexWriter.RebuildAllAsync(database, cancellationToken);
    }

    private static async Task DeleteDomainDataAsync(
        ServandaDbContext database,
        CancellationToken cancellationToken)
    {
        await database.PromptUsage.ExecuteDeleteAsync(cancellationToken);
        await database.PromptVersions.ExecuteDeleteAsync(cancellationToken);
        await database.Set<PromptTag>().ExecuteDeleteAsync(cancellationToken);
        await database.Set<PromptVariable>().ExecuteDeleteAsync(cancellationToken);
        await database.Set<PromptVariant>().ExecuteDeleteAsync(cancellationToken);
        await database.Prompts.ExecuteDeleteAsync(cancellationToken);
        await database.Set<ToolTag>().ExecuteDeleteAsync(cancellationToken);
        await database.Tools.ExecuteDeleteAsync(cancellationToken);
        await database.Tags.ExecuteDeleteAsync(cancellationToken);

        // Kategorie mają relację rodzica, więc są usuwane od liści do korzeni.
        while (await database.Categories.AnyAsync(cancellationToken))
        {
            var removed = await database.Categories
                .Where(category => !database.Categories.Any(child => child.ParentId == category.Id))
                .ExecuteDeleteAsync(cancellationToken);
            if (removed == 0)
            {
                throw new InvalidOperationException("Nie udało się usunąć drzewa kategorii przed importem.");
            }
        }

        await database.Areas.ExecuteDeleteAsync(cancellationToken);
    }

    private static async Task InsertAsync(
        ServandaDbContext database,
        ExportDocument document,
        CancellationToken cancellationToken)
    {
        foreach (var area in document.Areas)
        {
            database.Areas.Add(Area.Restore(
                area.Id,
                area.Name,
                area.Description,
                area.IconKey,
                area.AccentKey,
                area.ModuleKey,
                area.Availability,
                area.SortOrder,
                area.IsHidden,
                area.ArchivedAt,
                area.CreatedAt,
                area.UpdatedAt));
        }

        await database.SaveChangesAsync(cancellationToken);

        // Kategorie są wstawiane poziomami, aby relacja rodzica zawsze wskazywała istniejący wiersz.
        var pending = document.Categories.ToList();
        var inserted = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var ready = pending
                .Where(category => category.ParentId is null || inserted.Contains(category.ParentId))
                .ToList();
            if (ready.Count == 0)
            {
                throw new InvalidOperationException("Dokument zawiera cykl w drzewie kategorii.");
            }

            foreach (var category in ready)
            {
                database.Categories.Add(Category.Restore(
                    category.Id,
                    category.AreaId,
                    category.ParentId,
                    category.Name,
                    category.Description,
                    category.SortOrder,
                    category.CreatedAt,
                    category.UpdatedAt));
                inserted.Add(category.Id);
                pending.Remove(category);
            }

            await database.SaveChangesAsync(cancellationToken);
        }

        foreach (var tag in document.Tags)
        {
            database.Tags.Add(Tag.Restore(tag.Id, tag.AreaId, tag.Name, tag.CreatedAt, tag.UpdatedAt));
        }

        await database.SaveChangesAsync(cancellationToken);

        foreach (var tool in document.Tools)
        {
            database.Tools.Add(Tool.Restore(
                tool.Id,
                tool.AreaId,
                tool.CategoryId,
                tool.Name,
                tool.Description,
                tool.Url,
                tool.GroupKey,
                tool.SortOrder,
                tool.TagIds,
                tool.CreatedAt,
                tool.UpdatedAt));
        }

        foreach (var prompt in document.Prompts)
        {
            database.Prompts.Add(Prompt.Restore(
                prompt.Id,
                prompt.AreaId,
                prompt.CategoryId,
                prompt.Title,
                prompt.Description,
                prompt.IsFavorite,
                prompt.SortOrder,
                prompt.TagIds,
                [.. prompt.Variants.Select(variant => new PromptVariantState(
                    variant.Id,
                    variant.Name,
                    variant.Target,
                    variant.Content,
                    variant.SortOrder,
                    variant.CreatedAt,
                    variant.UpdatedAt))],
                [.. prompt.Variables.Select(variable => new PromptVariableState(
                    variable.Id,
                    variable.Name,
                    variable.Label,
                    variable.DefaultValue,
                    variable.IsRequired,
                    variable.IsMultiline,
                    variable.SortOrder,
                    variable.CreatedAt,
                    variable.UpdatedAt))],
                prompt.CreatedAt,
                prompt.UpdatedAt));
        }

        await database.SaveChangesAsync(cancellationToken);

        foreach (var prompt in document.Prompts)
        {
            foreach (var version in prompt.Versions)
            {
                database.PromptVersions.Add(new PromptVersion(
                    version.Id,
                    prompt.Id,
                    PromptSnapshotSerializer.Serialize(new PromptSnapshot(
                        version.Snapshot.SchemaVersion,
                        [.. version.Snapshot.Variants.Select(variant => new PromptVariantSnapshot(
                            variant.Id,
                            variant.Name,
                            variant.Target,
                            variant.Content,
                            variant.SortOrder))],
                        [.. version.Snapshot.Variables.Select(variable => new PromptVariableSnapshot(
                            variable.Id,
                            variable.Name,
                            variable.Label,
                            variable.DefaultValue,
                            variable.IsRequired,
                            variable.IsMultiline,
                            variable.SortOrder))])),
                    version.CreatedAt));
            }
        }

        foreach (var entry in document.PromptUsage)
        {
            database.PromptUsage.Add(new PromptUsageEntry(
                entry.Id,
                entry.PromptId,
                entry.VariantId,
                entry.TitleSnapshot,
                entry.VariantNameSnapshot,
                entry.UsedAt));
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Zakresy kolejności są stanem technicznym: po imporcie każdy istniejący zakres startuje od rewizji 1.
    /// </summary>
    private static async Task RebuildOrderingScopesAsync(
        ServandaDbContext database,
        ExportDocument document,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        await database.OrderingScopes.ExecuteDeleteAsync(cancellationToken);

        var keys = new List<string> { OrderingScopeKeys.Areas };
        var modulesByArea = document.Areas.ToDictionary(
            area => area.Id,
            area => area.ModuleKey,
            StringComparer.Ordinal);
        foreach (var area in document.Areas)
        {
            keys.Add(OrderingScopeKeys.RootCategories(area.Id));
        }

        foreach (var category in document.Categories)
        {
            keys.Add(OrderingScopeKeys.Categories(category.AreaId, category.Id));
            var moduleKey = modulesByArea.TryGetValue(category.AreaId, out var value) ? value : string.Empty;
            if (moduleKey == "tools")
            {
                keys.Add(OrderingScopeKeys.Tools(category.Id, Tool.FeaturedGroup));
                keys.Add(OrderingScopeKeys.Tools(category.Id, Tool.RegularGroup));
            }
            else if (moduleKey == "prompts")
            {
                keys.Add(OrderingScopeKeys.Prompts(category.Id));
            }
        }

        foreach (var key in keys.Distinct(StringComparer.Ordinal))
        {
            database.OrderingScopes.Add(new OrderingScope
            {
                ScopeKey = key,
                Revision = 1,
                UpdatedAt = timestamp,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}

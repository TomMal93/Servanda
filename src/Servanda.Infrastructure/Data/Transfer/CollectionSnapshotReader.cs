using Microsoft.EntityFrameworkCore;
using Servanda.Domain.Prompts;
using Servanda.Domain.Tools;
using Servanda.Infrastructure.Data.Prompts;

namespace Servanda.Infrastructure.Data.Transfer;

/// <summary>
/// Czyta pełny stan danych domenowych v2 na potrzeby eksportu i porównania skutków importu.
/// </summary>
internal static class CollectionSnapshotReader
{
    public static async Task<ExportDocument> ReadAsync(
        ServandaDbContext database,
        string exportId,
        DateTimeOffset exportedAt,
        string applicationVersion,
        CancellationToken cancellationToken)
    {
        var areas = await database.Areas
            .AsNoTracking()
            .OrderBy(area => area.SortOrder)
            .ThenBy(area => area.Id)
            .Select(area => new ExportArea(
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
                area.UpdatedAt))
            .ToListAsync(cancellationToken);

        var categories = await database.Categories
            .AsNoTracking()
            .OrderBy(category => category.Id)
            .Select(category => new ExportCategory(
                category.Id,
                category.AreaId,
                category.ParentId,
                category.Name,
                category.Description,
                category.SortOrder,
                category.CreatedAt,
                category.UpdatedAt))
            .ToListAsync(cancellationToken);

        var tags = await database.Tags
            .AsNoTracking()
            .OrderBy(tag => tag.Id)
            .Select(tag => new ExportTag(
                tag.Id,
                tag.AreaId,
                tag.Name,
                tag.NormalizedName,
                tag.CreatedAt,
                tag.UpdatedAt))
            .ToListAsync(cancellationToken);

        var toolTags = await database.Set<ToolTag>()
            .AsNoTracking()
            .OrderBy(link => link.TagId)
            .ToListAsync(cancellationToken);
        var toolTagsByTool = toolTags
            .GroupBy(link => link.ToolId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(link => link.TagId)],
                StringComparer.Ordinal);

        var tools = await database.Tools
            .AsNoTracking()
            .OrderBy(tool => tool.Id)
            .Select(tool => new
            {
                tool.Id,
                tool.AreaId,
                tool.CategoryId,
                tool.Name,
                tool.Description,
                tool.Url,
                tool.GroupKey,
                tool.SortOrder,
                tool.CreatedAt,
                tool.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var promptTags = await database.Set<PromptTag>()
            .AsNoTracking()
            .OrderBy(link => link.TagId)
            .ToListAsync(cancellationToken);
        var promptTagsByPrompt = promptTags
            .GroupBy(link => link.PromptId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)[.. group.Select(link => link.TagId)],
                StringComparer.Ordinal);

        var variants = await database.Set<PromptVariant>()
            .AsNoTracking()
            .OrderBy(variant => variant.SortOrder)
            .ThenBy(variant => variant.Id)
            .ToListAsync(cancellationToken);
        var variables = await database.Set<PromptVariable>()
            .AsNoTracking()
            .OrderBy(variable => variable.SortOrder)
            .ThenBy(variable => variable.Id)
            .ToListAsync(cancellationToken);
        var versions = await database.PromptVersions
            .AsNoTracking()
            .OrderBy(version => version.Id)
            .ToListAsync(cancellationToken);

        var prompts = await database.Prompts
            .AsNoTracking()
            .OrderBy(prompt => prompt.Id)
            .Select(prompt => new
            {
                prompt.Id,
                prompt.AreaId,
                prompt.CategoryId,
                prompt.Title,
                prompt.Description,
                prompt.IsFavorite,
                prompt.SortOrder,
                prompt.CreatedAt,
                prompt.UpdatedAt,
            })
            .ToListAsync(cancellationToken);

        var usage = await database.PromptUsage
            .AsNoTracking()
            .OrderBy(entry => entry.Id)
            .Select(entry => new ExportPromptUsage(
                entry.Id,
                entry.PromptId,
                entry.VariantId,
                entry.PromptTitle,
                entry.VariantName,
                entry.UsedAt))
            .ToListAsync(cancellationToken);

        return new ExportDocument(
            ExportDocument.CurrentSchemaVersion,
            exportId,
            exportedAt,
            applicationVersion,
            areas,
            categories,
            tags,
            [.. tools.Select(tool => new ExportTool(
                tool.Id,
                tool.AreaId,
                tool.CategoryId,
                tool.Name,
                tool.Description,
                tool.Url,
                tool.GroupKey,
                tool.SortOrder,
                toolTagsByTool.TryGetValue(tool.Id, out var tagIds) ? tagIds : [],
                tool.CreatedAt,
                tool.UpdatedAt))],
            [.. prompts.Select(prompt => new ExportPrompt(
                prompt.Id,
                prompt.AreaId,
                prompt.CategoryId,
                prompt.Title,
                prompt.Description,
                prompt.IsFavorite,
                prompt.SortOrder,
                promptTagsByPrompt.TryGetValue(prompt.Id, out var tagIds) ? tagIds : [],
                [.. variants
                    .Where(variant => variant.PromptId == prompt.Id)
                    .Select(variant => new ExportPromptVariant(
                        variant.Id,
                        variant.Name,
                        variant.Target,
                        variant.Content,
                        variant.SortOrder,
                        variant.CreatedAt,
                        variant.UpdatedAt))],
                [.. variables
                    .Where(variable => variable.PromptId == prompt.Id)
                    .Select(variable => new ExportPromptVariable(
                        variable.Id,
                        variable.Name,
                        variable.Label,
                        variable.DefaultValue,
                        variable.IsRequired,
                        variable.IsMultiline,
                        variable.SortOrder,
                        variable.CreatedAt,
                        variable.UpdatedAt))],
                [.. versions
                    .Where(version => version.PromptId == prompt.Id)
                    .Select(ToExportVersion)
                    .OfType<ExportPromptVersion>()],
                prompt.CreatedAt,
                prompt.UpdatedAt))],
            usage);
    }

    private static ExportPromptVersion? ToExportVersion(PromptVersion version)
    {
        var snapshot = PromptSnapshotSerializer.Deserialize(version.SnapshotJson);
        return snapshot is null
            ? null
            : new ExportPromptVersion(
                version.Id,
                version.CreatedAt,
                new ExportPromptSnapshot(
                    snapshot.SchemaVersion,
                    [.. snapshot.Variants.Select(variant => new ExportSnapshotVariant(
                        variant.Id,
                        variant.Name,
                        variant.Target,
                        variant.Content,
                        variant.SortOrder))],
                    [.. snapshot.Variables.Select(variable => new ExportSnapshotVariable(
                        variable.Id,
                        variable.Name,
                        variable.Label,
                        variable.DefaultValue,
                        variable.IsRequired,
                        variable.IsMultiline,
                        variable.SortOrder))]));
    }
}

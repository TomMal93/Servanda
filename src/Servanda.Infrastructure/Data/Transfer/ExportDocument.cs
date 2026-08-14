using System.Text.Json.Serialization;

namespace Servanda.Infrastructure.Data.Transfer;

/// <summary>
/// Przenośna koperta eksportu w wersji 1 opisana przez servanda-export-v1.schema.json.
/// </summary>
internal sealed record ExportDocument(
    int SchemaVersion,
    string ExportId,
    DateTimeOffset ExportedAt,
    string ApplicationVersion,
    IReadOnlyList<ExportArea> Areas,
    IReadOnlyList<ExportCategory> Categories,
    IReadOnlyList<ExportTag> Tags,
    IReadOnlyList<ExportTool> Tools,
    IReadOnlyList<ExportPrompt> Prompts,
    IReadOnlyList<ExportPromptUsage> PromptUsage)
{
    public const int CurrentSchemaVersion = 1;
}

internal sealed record ExportArea(
    string Id,
    string Name,
    string Description,
    string IconKey,
    string AccentKey,
    string ModuleKey,
    string Availability,
    int SortOrder,
    bool IsHidden,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record ExportCategory(
    string Id,
    string AreaId,
    string? ParentId,
    string Name,
    string Description,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record ExportTag(
    string Id,
    string AreaId,
    string Name,
    string NormalizedName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record ExportTool(
    string Id,
    string AreaId,
    string CategoryId,
    string Name,
    string Description,
    string Url,
    string GroupKey,
    int SortOrder,
    IReadOnlyList<string> TagIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record ExportPromptVariant(
    string Id,
    string Name,
    string? Target,
    string Content,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record ExportPromptVariable(
    string Id,
    string Name,
    string Label,
    string DefaultValue,
    bool IsRequired,
    bool IsMultiline,
    int SortOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record ExportPromptVersion(
    string Id,
    DateTimeOffset CreatedAt,
    [property: JsonPropertyName("snapshot")] ExportPromptSnapshot Snapshot);

internal sealed record ExportPromptSnapshot(
    int SchemaVersion,
    IReadOnlyList<ExportSnapshotVariant> Variants,
    IReadOnlyList<ExportSnapshotVariable> Variables);

internal sealed record ExportSnapshotVariant(
    string Id,
    string Name,
    string? Target,
    string Content,
    int SortOrder);

internal sealed record ExportSnapshotVariable(
    string Id,
    string Name,
    string Label,
    string DefaultValue,
    bool IsRequired,
    bool IsMultiline,
    int SortOrder);

internal sealed record ExportPrompt(
    string Id,
    string AreaId,
    string CategoryId,
    string Title,
    string Description,
    bool IsFavorite,
    int SortOrder,
    IReadOnlyList<string> TagIds,
    IReadOnlyList<ExportPromptVariant> Variants,
    IReadOnlyList<ExportPromptVariable> Variables,
    IReadOnlyList<ExportPromptVersion> Versions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

internal sealed record ExportPromptUsage(
    string Id,
    string? PromptId,
    string? VariantId,
    string TitleSnapshot,
    string VariantNameSnapshot,
    DateTimeOffset UsedAt);

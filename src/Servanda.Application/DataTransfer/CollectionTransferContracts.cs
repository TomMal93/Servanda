namespace Servanda.Application.DataTransfer;

public sealed record ExportSummary(
    string FilePath,
    string ExportId,
    DateTimeOffset ExportedAt,
    int Areas,
    int Categories,
    int Tags,
    int Tools,
    int Prompts,
    int PromptUsage);

public sealed record ImportSectionPreview(string Section, int Added, int Replaced, int Removed);

public enum ImportPreviewStatus
{
    Ready,
    Rejected,
}

/// <summary>
/// Skutki importu wyliczone w bazie stagingowej; kanoniczna baza pozostaje niezmieniona.
/// </summary>
public sealed record ImportPreview(
    ImportPreviewStatus Status,
    string? Token,
    int? SchemaVersion,
    IReadOnlyList<ImportSectionPreview> Sections,
    IReadOnlyList<string> Problems);

public enum ImportApplyStatus
{
    Applied,
    Expired,
    Failed,
}

public sealed record ImportResult(
    ImportApplyStatus Status,
    string? BackupId = null,
    string? ContentEpoch = null,
    IReadOnlyList<string>? Problems = null);

public interface ICollectionExportService
{
    Task<ExportSummary> ExportAsync(CancellationToken cancellationToken = default);
}

public interface ICollectionImportService
{
    /// <summary>Waliduje dokument bez zmiany kanonicznej bazy i zwraca podgląd skutków.</summary>
    Task<ImportPreview> PrepareAsync(Stream document, CancellationToken cancellationToken = default);

    /// <summary>Stosuje zatwierdzony podgląd: tworzy kopię ochronną i zastępuje całą kolekcję.</summary>
    Task<ImportResult> ApplyAsync(string token, CancellationToken cancellationToken = default);
}

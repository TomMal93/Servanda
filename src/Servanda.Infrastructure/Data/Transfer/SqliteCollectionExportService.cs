using System.Globalization;
using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.DataTransfer;
using Servanda.Domain.Areas;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Data.Transfer;

[SupportedOSPlatform("linux")]
internal sealed class SqliteCollectionExportService(
    IDbContextFactory<ServandaDbContext> contextFactory,
    ServandaPaths paths,
    TimeProvider timeProvider,
    string applicationVersion) : ICollectionExportService
{
    public async Task<ExportSummary> ExportAsync(CancellationToken cancellationToken = default)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var exportId = EntityId.NewUlid(timeProvider);
        var exportedAt = timeProvider.GetUtcNow();
        var document = await CollectionSnapshotReader.ReadAsync(
            database,
            exportId,
            exportedAt,
            applicationVersion,
            cancellationToken);

        PrivateFileSystem.EnsureDirectory(paths.ExportsDirectory, LinuxIdentity.GetEffectiveUserId());

        // Nazwa pliku pochodzi wyłącznie z aplikacji, więc nie pozwala na traversal.
        var fileName = string.Create(
            CultureInfo.InvariantCulture,
            $"servanda-export-{exportedAt.ToUniversalTime():yyyyMMdd'T'HHmmss'Z'}-{exportId}.json");
        var filePath = Path.Combine(paths.ExportsDirectory, fileName);

        await using (var stream = new FileStream(filePath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        }))
        {
            await JsonSerializer.SerializeAsync(stream, document, TransferJson.Options, cancellationToken);
        }

        PrivateFileSystem.VerifyPrivateFile(filePath, LinuxIdentity.GetEffectiveUserId());
        return new ExportSummary(
            filePath,
            exportId,
            exportedAt,
            document.Areas.Count,
            document.Categories.Count,
            document.Tags.Count,
            document.Tools.Count,
            document.Prompts.Count,
            document.PromptUsage.Count);
    }
}

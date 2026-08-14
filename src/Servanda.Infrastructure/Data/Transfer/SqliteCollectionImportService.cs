using System.Runtime.Versioning;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.DataProtection;
using Servanda.Application.DataTransfer;
using Servanda.Domain.Areas;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Data.Transfer;

[SupportedOSPlatform("linux")]
internal sealed class SqliteCollectionImportService(
    IDbContextFactory<ServandaDbContext> contextFactory,
    ServandaPaths paths,
    TimeProvider timeProvider,
    IBackupService backupService,
    ImportStagingStore stagingStore) : ICollectionImportService
{
    public async Task<ImportPreview> PrepareAsync(
        Stream document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        using var bufferedDocument = await BufferWithinLimitAsync(document, cancellationToken);
        if (bufferedDocument is null)
        {
            return Rejected(["Dokument przekracza dozwolony rozmiar 64 MiB."]);
        }

        JsonDocument parsed;
        try
        {
            parsed = await JsonDocument.ParseAsync(bufferedDocument, cancellationToken: cancellationToken);
        }
        catch (JsonException)
        {
            return Rejected(["Plik nie jest poprawnym dokumentem JSON."]);
        }

        using (parsed)
        {
            var schemaProblems = ExportSchemaValidator.Validate(parsed.RootElement);
            if (schemaProblems.Count > 0)
            {
                return Rejected(schemaProblems);
            }

            ExportDocument? model;
            try
            {
                model = parsed.RootElement.Deserialize<ExportDocument>(TransferJson.Options);
            }
            catch (JsonException exception)
            {
                return Rejected([$"Nie udało się odczytać dokumentu: {exception.Message}"]);
            }

            if (model is null || model.SchemaVersion != ExportDocument.CurrentSchemaVersion)
            {
                return Rejected(["Obsługiwana jest wyłącznie koperta eksportu w wersji 1."]);
            }

            var domainProblems = ValidateDomainRules(model);
            if (domainProblems.Count > 0)
            {
                return Rejected(domainProblems);
            }

            var stagingDirectory = CreateStagingDirectory();
            try
            {
                await ValidateInStagingAsync(model, stagingDirectory, cancellationToken);
            }
            catch (Exception exception) when (exception is DbUpdateException or SqliteException or InvalidOperationException)
            {
                Directory.Delete(stagingDirectory, recursive: true);
                return Rejected([
                    "Dokument nie przeszedł walidacji w bazie stagingowej: naruszone relacje albo reguły domenowe.",
                ]);
            }

            var token = EntityId.NewUlid(timeProvider);
            stagingStore.Add(token, new StagedImport(model, stagingDirectory));
            return new ImportPreview(
                ImportPreviewStatus.Ready,
                token,
                model.SchemaVersion,
                await BuildSectionsAsync(model, cancellationToken),
                []);
        }
    }

    public async Task<ImportResult> ApplyAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (!stagingStore.TryTake(token, out var staged))
        {
            return new ImportResult(ImportApplyStatus.Expired);
        }

        try
        {
            var backup = await backupService.CreateAsync(BackupReason.Import, cancellationToken);
            var verification = await backupService.VerifyAsync(backup.Id, cancellationToken);
            if (verification.Status != BackupVerificationStatus.Verified)
            {
                return new ImportResult(
                    ImportApplyStatus.Failed,
                    Problems: ["Kopia ochronna przed importem nie przeszła weryfikacji; kolekcja pozostała bez zmian."]);
            }

            var timestamp = timeProvider.GetUtcNow();
            var epoch = EntityId.NewUlid(timeProvider);
            await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
            await CollectionWriter.ReplaceAsync(database, staged.Document, timestamp, cancellationToken);

            // Nowa epoka unieważnia sesje edycji otwarte przed zastąpieniem kolekcji.
            await CollectionState.SetEpochAsync(database, epoch, cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            await backupService.ApplyRetentionAsync(cancellationToken);
            return new ImportResult(ImportApplyStatus.Applied, backup.Id, epoch);
        }
        catch (Exception exception) when (exception is DbUpdateException or SqliteException or InvalidOperationException)
        {
            return new ImportResult(
                ImportApplyStatus.Failed,
                Problems: ["Import został wycofany; kolekcja i kopia ochronna pozostały bez zmian."]);
        }
        finally
        {
            staged.DeleteStagingDirectory();
        }
    }

    public Task DiscardAsync(string token, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        cancellationToken.ThrowIfCancellationRequested();
        stagingStore.Discard(token);
        return Task.CompletedTask;
    }

    private static ImportPreview Rejected(IReadOnlyList<string> problems) =>
        new(ImportPreviewStatus.Rejected, null, null, [], problems);

    private static async Task<MemoryStream?> BufferWithinLimitAsync(
        Stream document,
        CancellationToken cancellationToken)
    {
        if (document.CanSeek
            && document.Length - document.Position > CollectionTransferLimits.MaximumDocumentBytes)
        {
            return null;
        }

        var buffered = new MemoryStream();
        var buffer = new byte[80 * 1024];
        long total = 0;
        while (true)
        {
            var read = await document.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                buffered.Position = 0;
                return buffered;
            }

            total += read;
            if (total > CollectionTransferLimits.MaximumDocumentBytes)
            {
                buffered.Dispose();
                return null;
            }

            await buffered.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    /// <summary>
    /// Reguły, których nie wyraża schemat: dokładnie jeden aktywny obszar modułu i spójność relacji.
    /// </summary>
    private static IReadOnlyList<string> ValidateDomainRules(ExportDocument document)
    {
        var problems = new List<string>();
        foreach (var moduleKey in new[] { "tools", "prompts" })
        {
            var active = document.Areas.Count(area =>
                area.ModuleKey == moduleKey
                && area.Availability == Area.ActiveAvailability
                && area.ArchivedAt is null);
            if (active != 1)
            {
                problems.Add($"Dokument musi zawierać dokładnie jeden aktywny obszar modułu „{moduleKey}”.");
            }
        }

        var areaIds = document.Areas.Select(area => area.Id).ToHashSet(StringComparer.Ordinal);
        var categoriesById = document.Categories.ToDictionary(category => category.Id, StringComparer.Ordinal);
        var tagAreas = document.Tags.ToDictionary(tag => tag.Id, tag => tag.AreaId, StringComparer.Ordinal);

        if (document.Categories.Any(category => !areaIds.Contains(category.AreaId)))
        {
            problems.Add("Kategoria wskazuje obszar spoza dokumentu.");
        }

        foreach (var tool in document.Tools)
        {
            if (!categoriesById.TryGetValue(tool.CategoryId, out var category) || category.AreaId != tool.AreaId)
            {
                problems.Add($"Narzędzie „{tool.Name}” wskazuje kategorię z innego obszaru.");
            }

            if (tool.TagIds.Any(tagId => !tagAreas.TryGetValue(tagId, out var area) || area != tool.AreaId))
            {
                problems.Add($"Narzędzie „{tool.Name}” używa tagu z innego obszaru.");
            }
        }

        foreach (var prompt in document.Prompts)
        {
            if (!categoriesById.TryGetValue(prompt.CategoryId, out var category) || category.AreaId != prompt.AreaId)
            {
                problems.Add($"Prompt „{prompt.Title}” wskazuje kategorię z innego obszaru.");
            }

            if (prompt.TagIds.Any(tagId => !tagAreas.TryGetValue(tagId, out var area) || area != prompt.AreaId))
            {
                problems.Add($"Prompt „{prompt.Title}” używa tagu z innego obszaru.");
            }
        }

        return [.. problems.Distinct(StringComparer.Ordinal).Take(20)];
    }

    private string CreateStagingDirectory()
    {
        var directory = Path.Combine(paths.DataDirectory, $"import-{EntityId.NewUlid(timeProvider)}");
        PrivateFileSystem.EnsureDirectory(directory, LinuxIdentity.GetEffectiveUserId());
        return directory;
    }

    /// <summary>
    /// Walidacja odbywa się w izolowanej bazie stagingowej, bez zmiany kanonicznej bazy.
    /// </summary>
    private async Task ValidateInStagingAsync(
        ExportDocument document,
        string stagingDirectory,
        CancellationToken cancellationToken)
    {
        var databasePath = Path.Combine(stagingDirectory, "staging.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = databasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();
        var options = new DbContextOptionsBuilder<ServandaDbContext>()
            .UseSqlite(connectionString)
            .Options;

        await using var staging = new ServandaDbContext(options);
        await staging.Database.MigrateAsync(cancellationToken);
        await using var transaction = await staging.Database.BeginTransactionAsync(cancellationToken);
        await CollectionWriter.ReplaceAsync(
            staging,
            document,
            timeProvider.GetUtcNow(),
            cancellationToken);
        await VerifyIntegrityAsync(staging, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        SqliteConnection.ClearAllPools();
    }

    private static async Task VerifyIntegrityAsync(
        ServandaDbContext database,
        CancellationToken cancellationToken)
    {
        var connection = (SqliteConnection)database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        foreach (var statement in new[]
                 {
                     "PRAGMA foreign_key_check;",
                     "INSERT INTO tool_search(tool_search) VALUES ('integrity-check');",
                     "INSERT INTO prompt_search(prompt_search) VALUES ('integrity-check');",
                 })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = statement;
            if (statement.StartsWith("PRAGMA", StringComparison.Ordinal))
            {
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    throw new InvalidOperationException("Dokument narusza integralność kluczy obcych.");
                }

                continue;
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private async Task<IReadOnlyList<ImportSectionPreview>> BuildSectionsAsync(
        ExportDocument document,
        CancellationToken cancellationToken)
    {
        await using var database = await contextFactory.CreateDbContextAsync(cancellationToken);
        var current = await CollectionSnapshotReader.ReadAsync(
            database,
            EntityId.NewUlid(timeProvider),
            timeProvider.GetUtcNow(),
            "preview",
            cancellationToken);

        return
        [
            Compare("Obszary", current.Areas.Select(area => area.Id), document.Areas.Select(area => area.Id)),
            Compare(
                "Kategorie",
                current.Categories.Select(category => category.Id),
                document.Categories.Select(category => category.Id)),
            Compare("Tagi", current.Tags.Select(tag => tag.Id), document.Tags.Select(tag => tag.Id)),
            Compare("Narzędzia", current.Tools.Select(tool => tool.Id), document.Tools.Select(tool => tool.Id)),
            Compare("Prompty", current.Prompts.Select(prompt => prompt.Id), document.Prompts.Select(prompt => prompt.Id)),
            Compare(
                "Historia użycia",
                current.PromptUsage.Select(entry => entry.Id),
                document.PromptUsage.Select(entry => entry.Id)),
        ];
    }

    private static ImportSectionPreview Compare(
        string section,
        IEnumerable<string> currentIds,
        IEnumerable<string> documentIds)
    {
        var current = currentIds.ToHashSet(StringComparer.Ordinal);
        var incoming = documentIds.ToHashSet(StringComparer.Ordinal);
        return new ImportSectionPreview(
            section,
            incoming.Except(current, StringComparer.Ordinal).Count(),
            incoming.Intersect(current, StringComparer.Ordinal).Count(),
            current.Except(incoming, StringComparer.Ordinal).Count());
    }
}

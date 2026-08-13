using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Servanda.Application.DataProtection;
using Servanda.Domain.Areas;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Data.Backups;

[SupportedOSPlatform("linux")]
internal sealed class SqliteBackupService(
    ServandaPaths paths,
    TimeProvider timeProvider,
    string applicationVersion) : IBackupService
{
    private const int CurrentFormatVersion = 1;
    private const long MaximumMetadataLength = 64 * 1024;
    private const string DatabaseFileName = "servanda.db";
    private const string MetadataFileName = "metadata.json";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    private readonly string _applicationVersion = !string.IsNullOrWhiteSpace(applicationVersion)
        ? applicationVersion
        : throw new ArgumentException("Wersja aplikacji jest wymagana.", nameof(applicationVersion));
    private readonly uint _effectiveUserId = LinuxIdentity.GetEffectiveUserId();

    public async Task<BackupInfo> CreateAsync(
        BackupReason reason,
        CancellationToken cancellationToken = default)
    {
        LinuxIdentity.EnsureLinux();
        PrivateFileSystem.EnsureDirectory(paths.BackupsDirectory, _effectiveUserId);
        PrivateFileSystem.VerifyPrivateFile(paths.DatabasePath, _effectiveUserId);

        var backupId = EntityId.NewUlid(timeProvider);
        var storedReason = ToStorageValue(reason);
        var stagingDirectory = Path.Combine(paths.BackupsDirectory, $".{backupId}.tmp");
        var publishedDirectory = GetBackupDirectory(backupId);
        Directory.CreateDirectory(
            stagingDirectory,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        PrivateFileSystem.EnsureDirectory(stagingDirectory, _effectiveUserId);

        var published = false;
        try
        {
            var databasePath = Path.Combine(stagingDirectory, DatabaseFileName);
            CreatePrivateFile(databasePath);
            var schemaVersion = await CreateDatabaseSnapshotAsync(databasePath, cancellationToken);
            var metadata = new BackupMetadata(
                CurrentFormatVersion,
                backupId,
                schemaVersion,
                _applicationVersion,
                timeProvider.GetUtcNow().ToUniversalTime(),
                storedReason);
            await WriteMetadataAsync(
                Path.Combine(stagingDirectory, MetadataFileName),
                metadata,
                cancellationToken);

            var verification = await VerifyDirectoryAsync(stagingDirectory, backupId, cancellationToken);
            if (verification.Status != BackupVerificationStatus.Verified || verification.Backup is null)
            {
                throw new InvalidDataException("Utworzona kopia bazy nie przeszła weryfikacji.");
            }

            Directory.Move(stagingDirectory, publishedDirectory);
            published = true;
            return verification.Backup;
        }
        finally
        {
            if (!published && Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    public Task<BackupVerificationResult> VerifyAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        LinuxIdentity.EnsureLinux();
        if (!IsValidBackupId(backupId))
        {
            return Task.FromResult(new BackupVerificationResult(BackupVerificationStatus.NotFound));
        }

        var backupDirectory = GetBackupDirectory(backupId);
        if (!Directory.Exists(backupDirectory))
        {
            return Task.FromResult(new BackupVerificationResult(BackupVerificationStatus.NotFound));
        }

        return VerifyDirectoryAsync(backupDirectory, backupId, cancellationToken);
    }

    private async Task<string> CreateDatabaseSnapshotAsync(
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await using var source = new SqliteConnection(CreateConnectionString(paths.DatabasePath, SqliteOpenMode.ReadWrite));
        await using var destination = new SqliteConnection(CreateConnectionString(destinationPath, SqliteOpenMode.ReadWrite));
        await source.OpenAsync(cancellationToken);
        await destination.OpenAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        source.BackupDatabase(destination);
        PrivateFileSystem.VerifyPrivateFile(destinationPath, _effectiveUserId);
        return await ReadSchemaVersionAsync(destination, cancellationToken)
            ?? throw new InvalidDataException("Baza źródłowa nie ma rozpoznanej wersji schematu.");
    }

    private async Task<BackupVerificationResult> VerifyDirectoryAsync(
        string directory,
        string expectedBackupId,
        CancellationToken cancellationToken)
    {
        try
        {
            PrivateFileSystem.EnsureDirectory(directory, _effectiveUserId);
            var databasePath = Path.Combine(directory, DatabaseFileName);
            var metadataPath = Path.Combine(directory, MetadataFileName);
            if (!File.Exists(databasePath) || !File.Exists(metadataPath))
            {
                return new BackupVerificationResult(BackupVerificationStatus.Invalid);
            }

            PrivateFileSystem.VerifyPrivateFile(databasePath, _effectiveUserId);
            PrivateFileSystem.VerifyPrivateFile(metadataPath, _effectiveUserId);
            var metadata = await ReadMetadataAsync(metadataPath, cancellationToken);
            if (!TryMapMetadata(metadata, expectedBackupId, out var backup))
            {
                return new BackupVerificationResult(BackupVerificationStatus.Invalid);
            }

            await using var connection = new SqliteConnection(
                CreateConnectionString(databasePath, SqliteOpenMode.ReadOnly));
            await connection.OpenAsync(cancellationToken);
            if (!await HasValidIntegrityAsync(connection, cancellationToken)
                || !await HasValidForeignKeysAsync(connection, cancellationToken))
            {
                return new BackupVerificationResult(BackupVerificationStatus.Invalid);
            }

            var appliedMigrations = await ReadAppliedMigrationsAsync(connection, cancellationToken);
            if (appliedMigrations.Count == 0
                || !string.Equals(appliedMigrations[^1], metadata.SchemaVersion, StringComparison.Ordinal))
            {
                return new BackupVerificationResult(BackupVerificationStatus.Invalid);
            }

            var knownMigrations = await GetKnownMigrationsAsync(cancellationToken);
            if (appliedMigrations.Count > knownMigrations.Count
                || !appliedMigrations.SequenceEqual(
                    knownMigrations.Take(appliedMigrations.Count),
                    StringComparer.Ordinal))
            {
                return new BackupVerificationResult(BackupVerificationStatus.Incompatible, backup);
            }

            return new BackupVerificationResult(BackupVerificationStatus.Verified, backup);
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or JsonException
            or SqliteException
            or InvalidOperationException)
        {
            return new BackupVerificationResult(BackupVerificationStatus.Invalid);
        }
    }

    private static async Task<bool> HasValidIntegrityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var receivedResult = false;
        while (await reader.ReadAsync(cancellationToken))
        {
            receivedResult = true;
            if (!string.Equals(reader.GetString(0), "ok", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return receivedResult;
    }

    private static async Task<bool> HasValidForeignKeysAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_key_check;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return !await reader.ReadAsync(cancellationToken);
    }

    private static async Task<string?> ReadSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        var migrations = await ReadAppliedMigrationsAsync(connection, cancellationToken);
        return migrations.Count == 0 ? null : migrations[^1];
    }

    private static async Task<IReadOnlyList<string>> ReadAppliedMigrationsAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var migrations = new List<string>();
        while (await reader.ReadAsync(cancellationToken))
        {
            migrations.Add(reader.GetString(0));
        }

        return migrations;
    }

    private static Task<IReadOnlyList<string>> GetKnownMigrationsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = new DbContextOptionsBuilder<ServandaDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        using var database = new ServandaDbContext(options);
        IReadOnlyList<string> migrations = database.Database.GetMigrations().ToList();
        return Task.FromResult(migrations);
    }

    private static async Task WriteMetadataAsync(
        string path,
        BackupMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 4096,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
        await JsonSerializer.SerializeAsync(stream, metadata, SerializerOptions, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }

    private static async Task<BackupMetadata> ReadMetadataAsync(
        string path,
        CancellationToken cancellationToken)
    {
        var file = new FileInfo(path);
        if (file.Length is <= 0 or > MaximumMetadataLength)
        {
            throw new InvalidDataException("Metadane kopii mają nieprawidłowy rozmiar.");
        }

        await using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = 4096,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        });
        return await JsonSerializer.DeserializeAsync<BackupMetadata>(stream, SerializerOptions, cancellationToken)
            ?? throw new InvalidDataException("Metadane kopii są puste.");
    }

    private static bool TryMapMetadata(
        BackupMetadata metadata,
        string expectedBackupId,
        out BackupInfo? backup)
    {
        backup = null;
        if (metadata.FormatVersion != CurrentFormatVersion
            || !string.Equals(metadata.BackupId, expectedBackupId, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(metadata.SchemaVersion)
            || string.IsNullOrWhiteSpace(metadata.ApplicationVersion)
            || metadata.CreatedAt.Offset != TimeSpan.Zero
            || !TryParseReason(metadata.Reason, out var reason))
        {
            return false;
        }

        backup = new BackupInfo(
            metadata.BackupId,
            metadata.SchemaVersion,
            metadata.ApplicationVersion,
            metadata.CreatedAt,
            reason);
        return true;
    }

    private static string ToStorageValue(BackupReason reason) => reason switch
    {
        BackupReason.Manual => "manual",
        BackupReason.Migration => "migration",
        BackupReason.Import => "import",
        BackupReason.CollectionReset => "collection-reset",
        BackupReason.BulkDataOperation => "bulk-data-operation",
        _ => throw new ArgumentOutOfRangeException(nameof(reason)),
    };

    private static bool TryParseReason(string value, out BackupReason reason)
    {
        reason = value switch
        {
            "manual" => BackupReason.Manual,
            "migration" => BackupReason.Migration,
            "import" => BackupReason.Import,
            "collection-reset" => BackupReason.CollectionReset,
            "bulk-data-operation" => BackupReason.BulkDataOperation,
            _ => default,
        };
        return value is "manual" or "migration" or "import" or "collection-reset" or "bulk-data-operation";
    }

    private string GetBackupDirectory(string backupId) => Path.Combine(paths.BackupsDirectory, backupId);

    private static string CreateConnectionString(string path, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();

    private static void CreatePrivateFile(string path)
    {
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 1,
            Options = FileOptions.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
    }

    private static bool IsValidBackupId(string value)
    {
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        return value.Length == 26 && value.All(character => alphabet.Contains(character, StringComparison.Ordinal));
    }

}

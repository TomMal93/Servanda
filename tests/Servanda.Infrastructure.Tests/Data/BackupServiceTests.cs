using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.DataProtection;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class BackupServiceTests
{
    [Fact]
    public async Task CreateProducesVerifiedPrivateSnapshotWithMetadataFromOpenWalDatabase()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);

        await using var liveConnection = new SqliteConnection(CreateConnectionString(paths.DatabasePath, SqliteOpenMode.ReadWrite));
        await liveConnection.OpenAsync();
        await ExecuteNonQueryAsync(liveConnection, "PRAGMA journal_mode=WAL;");
        await ExecuteNonQueryAsync(
            liveConnection,
            "UPDATE areas SET name = 'Stan z WAL' WHERE name = 'Dom';");

        var service = services.GetRequiredService<IBackupService>();
        var backup = await service.CreateAsync(BackupReason.Migration);
        var verification = await service.VerifyAsync(backup.Id);
        var backupDirectory = Path.Combine(paths.BackupsDirectory, backup.Id);
        var backupDatabasePath = Path.Combine(backupDirectory, "servanda.db");
        var metadataPath = Path.Combine(backupDirectory, "metadata.json");

        Assert.Equal(BackupVerificationStatus.Verified, verification.Status);
        Assert.Equal(backup, verification.Backup);
        Assert.Equal(BackupReason.Migration, backup.Reason);
        Assert.Equal("20260813120000_AddAreaVisibilityIndex", backup.SchemaVersion);
        Assert.NotEqual(default, backup.CreatedAt);
        Assert.Equal(TimeSpan.Zero, backup.CreatedAt.Offset);
        Assert.Equal("test-version", backup.ApplicationVersion);
        Assert.Equal(PrivateDirectoryMode, File.GetUnixFileMode(paths.BackupsDirectory));
        Assert.Equal(PrivateDirectoryMode, File.GetUnixFileMode(backupDirectory));
        Assert.Equal(PrivateFileMode, File.GetUnixFileMode(backupDatabasePath));
        Assert.Equal(PrivateFileMode, File.GetUnixFileMode(metadataPath));
        Assert.Empty(Directory.EnumerateDirectories(paths.BackupsDirectory, ".*.tmp"));

        await using var backupConnection = new SqliteConnection(
            CreateConnectionString(backupDatabasePath, SqliteOpenMode.ReadOnly));
        await backupConnection.OpenAsync();
        Assert.Equal(
            "Stan z WAL",
            await ExecuteScalarAsync(backupConnection, "SELECT name FROM areas WHERE module_key = 'home';"));

        await using var metadataStream = File.OpenRead(metadataPath);
        using var metadata = await JsonDocument.ParseAsync(metadataStream);
        Assert.Equal(1, metadata.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(backup.Id, metadata.RootElement.GetProperty("backupId").GetString());
        Assert.Equal("migration", metadata.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public async Task SnapshotDoesNotChangeAfterLiveDatabaseIsModified()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var service = services.GetRequiredService<IBackupService>();

        var backup = await service.CreateAsync(BackupReason.Manual);
        await using (var liveConnection = new SqliteConnection(
            CreateConnectionString(paths.DatabasePath, SqliteOpenMode.ReadWrite)))
        {
            await liveConnection.OpenAsync();
            await ExecuteNonQueryAsync(
                liveConnection,
                "UPDATE areas SET name = 'Zmiana po kopii' WHERE module_key = 'home';");
        }

        var backupDatabasePath = Path.Combine(paths.BackupsDirectory, backup.Id, "servanda.db");
        await using var backupConnection = new SqliteConnection(
            CreateConnectionString(backupDatabasePath, SqliteOpenMode.ReadOnly));
        await backupConnection.OpenAsync();

        Assert.Equal(
            "Dom",
            await ExecuteScalarAsync(backupConnection, "SELECT name FROM areas WHERE module_key = 'home';"));
        Assert.Equal(BackupVerificationStatus.Verified, (await service.VerifyAsync(backup.Id)).Status);
    }

    [Fact]
    public async Task VerifyRejectsCorruptedMetadata()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var service = services.GetRequiredService<IBackupService>();
        var backup = await service.CreateAsync(BackupReason.Import);
        var metadataPath = Path.Combine(paths.BackupsDirectory, backup.Id, "metadata.json");

        await File.WriteAllTextAsync(metadataPath, "{}");

        var verification = await service.VerifyAsync(backup.Id);
        await service.ApplyRetentionAsync();

        Assert.Equal(BackupVerificationStatus.Invalid, verification.Status);
        Assert.Null(verification.Backup);
        Assert.True(Directory.Exists(Path.Combine(paths.BackupsDirectory, backup.Id)));
    }

    [Fact]
    public async Task VerifyRejectsCorruptedDatabase()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var service = services.GetRequiredService<IBackupService>();
        var backup = await service.CreateAsync(BackupReason.BulkDataOperation);
        var databasePath = Path.Combine(paths.BackupsDirectory, backup.Id, "servanda.db");

        await File.WriteAllBytesAsync(databasePath, new byte[128]);

        var verification = await service.VerifyAsync(backup.Id);

        Assert.Equal(BackupVerificationStatus.Invalid, verification.Status);
        Assert.Null(verification.Backup);
    }

    [Fact]
    public async Task VerifyRejectsNonPrivateBackupFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var service = services.GetRequiredService<IBackupService>();
        var backup = await service.CreateAsync(BackupReason.Manual);
        var metadataPath = Path.Combine(paths.BackupsDirectory, backup.Id, "metadata.json");
        File.SetUnixFileMode(metadataPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);

        var verification = await service.VerifyAsync(backup.Id);

        Assert.Equal(BackupVerificationStatus.Invalid, verification.Status);
        Assert.Null(verification.Backup);
    }

    [Fact]
    public async Task FailedCreationDoesNotPublishPartialBackup()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await File.WriteAllBytesAsync(paths.DatabasePath, new byte[128]);
        File.SetUnixFileMode(paths.DatabasePath, PrivateFileMode);
        await using var services = CreateServices(paths);
        var service = services.GetRequiredService<IBackupService>();

        await Assert.ThrowsAnyAsync<Exception>(() => service.CreateAsync(BackupReason.Manual));

        Assert.Empty(Directory.EnumerateFileSystemEntries(paths.BackupsDirectory));
    }

    [Fact]
    public async Task VerifyDistinguishesInternallyConsistentUnknownSchema()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var service = services.GetRequiredService<IBackupService>();
        var backup = await service.CreateAsync(BackupReason.CollectionReset);
        var backupDirectory = Path.Combine(paths.BackupsDirectory, backup.Id);
        var backupDatabasePath = Path.Combine(backupDirectory, "servanda.db");
        var metadataPath = Path.Combine(backupDirectory, "metadata.json");
        const string futureSchema = "99999999999999_FutureSchema";

        await using (var connection = new SqliteConnection(
            CreateConnectionString(backupDatabasePath, SqliteOpenMode.ReadWrite)))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = """
                DELETE FROM __EFMigrationsHistory;
                INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
                VALUES ($migrationId, '10.0.10');
                """;
            command.Parameters.AddWithValue("$migrationId", futureSchema);
            await command.ExecuteNonQueryAsync();
        }

        var metadata = JsonNode.Parse(await File.ReadAllTextAsync(metadataPath))!.AsObject();
        metadata["schemaVersion"] = futureSchema;
        await File.WriteAllTextAsync(
            metadataPath,
            metadata.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var verification = await service.VerifyAsync(backup.Id);
        await service.ApplyRetentionAsync();

        Assert.Equal(BackupVerificationStatus.Incompatible, verification.Status);
        Assert.NotNull(verification.Backup);
        Assert.Equal(futureSchema, verification.Backup.SchemaVersion);
        Assert.True(Directory.Exists(backupDirectory));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("../servanda.db")]
    [InlineData("01K0000000000000000000000I")]
    public async Task VerifyDoesNotResolveInvalidOrMissingIdentifiers(string backupId)
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        var service = services.GetRequiredService<IBackupService>();

        var verification = await service.VerifyAsync(backupId);

        Assert.Equal(BackupVerificationStatus.NotFound, verification.Status);
    }

    [Fact]
    public async Task RecoverySelectsLatestVerifiedCompatibleBackupAndSkipsInvalidCopy()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var backupService = services.GetRequiredService<IBackupService>();
        var recoveryService = services.GetRequiredService<IDatabaseRecoveryService>();
        var validBackup = await backupService.CreateAsync(BackupReason.Manual);
        var invalidBackup = await backupService.CreateAsync(BackupReason.Manual);
        await File.WriteAllBytesAsync(
            Path.Combine(paths.BackupsDirectory, invalidBackup.Id, "servanda.db"),
            new byte[128]);

        var candidate = await recoveryService.FindLatestVerifiedBackupAsync();

        Assert.Equal(validBackup.Id, candidate?.Id);
    }

    [Fact]
    public async Task RestoreReverifiesBackupBeforeChangingCanonicalDatabase()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var backupService = services.GetRequiredService<IBackupService>();
        var recoveryService = services.GetRequiredService<IDatabaseRecoveryService>();
        var backup = await backupService.CreateAsync(BackupReason.Manual);
        var originalDatabase = await File.ReadAllBytesAsync(paths.DatabasePath);
        await File.WriteAllBytesAsync(
            Path.Combine(paths.BackupsDirectory, backup.Id, "servanda.db"),
            new byte[128]);

        var result = await recoveryService.RestoreAsync(backup.Id);

        Assert.Equal(DatabaseRestoreStatus.BackupInvalid, result.Status);
        Assert.Equal(originalDatabase, await File.ReadAllBytesAsync(paths.DatabasePath));
        Assert.False(Directory.Exists(paths.RecoveryArtifactsDirectory));
    }

    [Fact]
    public async Task RestorePreservesFailedDatabaseAndSidecarBeforeReplacement()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var backupService = services.GetRequiredService<IBackupService>();
        var recoveryService = services.GetRequiredService<IDatabaseRecoveryService>();
        var backup = await backupService.CreateAsync(BackupReason.Manual);
        var failedDatabase = new byte[128];
        var failedWal = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(paths.DatabasePath, failedDatabase);
        await File.WriteAllBytesAsync(paths.DatabasePath + "-wal", failedWal);
        File.SetUnixFileMode(paths.DatabasePath + "-wal", PrivateFileMode);

        var result = await recoveryService.RestoreAsync(backup.Id);

        Assert.Equal(DatabaseRestoreStatus.Restored, result.Status);
        var artifactDirectory = Assert.Single(Directory.EnumerateDirectories(paths.RecoveryArtifactsDirectory));
        Assert.Equal(failedDatabase, await File.ReadAllBytesAsync(Path.Combine(artifactDirectory, "servanda.db")));
        Assert.Equal(failedWal, await File.ReadAllBytesAsync(Path.Combine(artifactDirectory, "servanda.db-wal")));
        Assert.False(File.Exists(paths.DatabasePath + "-wal"));
        Assert.Equal(PrivateDirectoryMode, File.GetUnixFileMode(artifactDirectory));
        Assert.All(
            Directory.EnumerateFiles(artifactDirectory),
            file => Assert.Equal(PrivateFileMode, File.GetUnixFileMode(file)));
        Assert.Equal(BackupVerificationStatus.Verified, (await backupService.VerifyAsync(backup.Id)).Status);
    }

    [Fact]
    public async Task RestoreDoesNotReplaceDatabaseWhenDiagnosticPreservationCannotBeVerified()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var backupService = services.GetRequiredService<IBackupService>();
        var recoveryService = services.GetRequiredService<IDatabaseRecoveryService>();
        var backup = await backupService.CreateAsync(BackupReason.Manual);
        var currentDatabase = await File.ReadAllBytesAsync(paths.DatabasePath);
        Directory.CreateDirectory(paths.RecoveryArtifactsDirectory);
        File.SetUnixFileMode(
            paths.RecoveryArtifactsDirectory,
            PrivateDirectoryMode | UnixFileMode.GroupRead);

        var result = await recoveryService.RestoreAsync(backup.Id);

        Assert.Equal(DatabaseRestoreStatus.Failed, result.Status);
        Assert.Equal(currentDatabase, await File.ReadAllBytesAsync(paths.DatabasePath));
        Assert.Equal(BackupVerificationStatus.Verified, (await backupService.VerifyAsync(backup.Id)).Status);
    }

    [Fact]
    public async Task RetentionDeletesOnlyExcessVerifiedAutomaticBackups()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var service = services.GetRequiredService<IBackupService>();
        var manualBackup = await service.CreateAsync(BackupReason.Manual);
        var invalidBackup = await service.CreateAsync(BackupReason.BulkDataOperation);
        await File.WriteAllTextAsync(
            Path.Combine(paths.BackupsDirectory, invalidBackup.Id, "metadata.json"),
            "{}");
        var automaticBackups = new List<BackupInfo>();
        for (var index = 0; index < 12; index++)
        {
            automaticBackups.Add(await service.CreateAsync(BackupReason.Migration));
        }

        await service.ApplyRetentionAsync();

        var expectedRetainedIds = automaticBackups
            .OrderByDescending(backup => backup.CreatedAt)
            .ThenByDescending(backup => backup.Id, StringComparer.Ordinal)
            .Take(10)
            .Select(backup => backup.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(Directory.Exists(Path.Combine(paths.BackupsDirectory, manualBackup.Id)));
        Assert.True(Directory.Exists(Path.Combine(paths.BackupsDirectory, invalidBackup.Id)));
        Assert.Equal(
            expectedRetainedIds,
            automaticBackups
                .Where(backup => Directory.Exists(Path.Combine(paths.BackupsDirectory, backup.Id)))
                .Select(backup => backup.Id)
                .ToHashSet(StringComparer.Ordinal));
    }

    private static ServiceProvider CreateServices(ServandaPaths paths)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
        services.AddServandaDatabase(paths, "test-version");
        return services.BuildServiceProvider();
    }

    private static ServandaPaths CreatePaths(string root)
    {
        var paths = new ServandaPaths(
            Path.Combine(root, "runtime"),
            Path.Combine(root, "state"),
            Path.Combine(root, "data"));
        Directory.CreateDirectory(paths.RuntimeDirectory, PrivateDirectoryMode);
        Directory.CreateDirectory(paths.StateDirectory, PrivateDirectoryMode);
        Directory.CreateDirectory(paths.DataDirectory, PrivateDirectoryMode);
        return paths;
    }

    private static string CreateConnectionString(string path, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();

    private static async Task ExecuteNonQueryAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ExecuteScalarAsync(SqliteConnection connection, string commandText)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = commandText;
        return await command.ExecuteScalarAsync() as string;
    }

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

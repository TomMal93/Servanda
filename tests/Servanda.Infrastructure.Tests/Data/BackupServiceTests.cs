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
        Assert.Equal("20260812211246_InitialAreas", backup.SchemaVersion);
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

        Assert.Equal(BackupVerificationStatus.Invalid, verification.Status);
        Assert.Null(verification.Backup);
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
            command.CommandText = "UPDATE __EFMigrationsHistory SET MigrationId = $migrationId;";
            command.Parameters.AddWithValue("$migrationId", futureSchema);
            await command.ExecuteNonQueryAsync();
        }

        var metadata = JsonNode.Parse(await File.ReadAllTextAsync(metadataPath))!.AsObject();
        metadata["schemaVersion"] = futureSchema;
        await File.WriteAllTextAsync(
            metadataPath,
            metadata.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var verification = await service.VerifyAsync(backup.Id);

        Assert.Equal(BackupVerificationStatus.Incompatible, verification.Status);
        Assert.NotNull(verification.Backup);
        Assert.Equal(futureSchema, verification.Backup.SchemaVersion);
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

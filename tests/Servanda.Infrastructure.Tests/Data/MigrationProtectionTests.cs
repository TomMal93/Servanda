using System.Data.Common;
using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.DataProtection;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class MigrationProtectionTests
{
    private const string InitialMigration = "20260812211246_InitialAreas";
    private const string VisibilityIndexMigration = "20260813120000_AddAreaVisibilityIndex";
    private const string VisibilityIndex = "IX_areas_archived_at_is_hidden_sort_order";

    [Fact]
    public async Task InitializeMigratesPreviousVersionOnlyAfterCreatingVerifiedBackup()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);
        await CreatePreviousVersionAsync(services, paths);
        await UpdateAreaNameAsync(paths.DatabasePath, "Stan przed migracją");

        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);

        Assert.Equal(
            new[] { InitialMigration, VisibilityIndexMigration },
            await ReadAppliedMigrationsAsync(paths.DatabasePath));
        Assert.True(await IndexExistsAsync(paths.DatabasePath, VisibilityIndex));
        Assert.Equal("Stan przed migracją", await ReadHomeAreaNameAsync(paths.DatabasePath));

        var backupDirectory = Assert.Single(Directory.EnumerateDirectories(paths.BackupsDirectory));
        var backupId = Path.GetFileName(backupDirectory);
        var backupService = services.GetRequiredService<IBackupService>();
        var verification = await backupService.VerifyAsync(backupId);
        var backupDatabasePath = Path.Combine(backupDirectory, "servanda.db");

        Assert.Equal(BackupVerificationStatus.Verified, verification.Status);
        Assert.NotNull(verification.Backup);
        Assert.Equal(BackupReason.Migration, verification.Backup.Reason);
        Assert.Equal(InitialMigration, verification.Backup.SchemaVersion);
        Assert.Equal(new[] { InitialMigration }, await ReadAppliedMigrationsAsync(backupDatabasePath));
        Assert.False(await IndexExistsAsync(backupDatabasePath, VisibilityIndex));
        Assert.Equal("Stan przed migracją", await ReadHomeAreaNameAsync(backupDatabasePath));

        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        Assert.Single(Directory.EnumerateDirectories(paths.BackupsDirectory));
    }

    [Fact]
    public async Task FailedMigrationPreservesPreviousDatabaseAndVerifiedBackup()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using (var previousServices = CreateServices(paths))
        {
            await CreatePreviousVersionAsync(previousServices, paths);
        }

        await UpdateAreaNameAsync(paths.DatabasePath, "Dane chronione przed awarią");
        await using var failingServices = CreateServices(paths, new FailVisibilityIndexInterceptor());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ServandaDatabase.InitializeAsync(failingServices, paths, TimeProvider.System));

        Assert.Equal(new[] { InitialMigration }, await ReadAppliedMigrationsAsync(paths.DatabasePath));
        Assert.False(await IndexExistsAsync(paths.DatabasePath, VisibilityIndex));
        Assert.Equal("Dane chronione przed awarią", await ReadHomeAreaNameAsync(paths.DatabasePath));
        Assert.Equal("ok", await ReadIntegrityResultAsync(paths.DatabasePath));

        var backupDirectory = Assert.Single(Directory.EnumerateDirectories(paths.BackupsDirectory));
        var backupId = Path.GetFileName(backupDirectory);
        var backupService = failingServices.GetRequiredService<IBackupService>();
        var verification = await backupService.VerifyAsync(backupId);
        var backupDatabasePath = Path.Combine(backupDirectory, "servanda.db");

        Assert.Equal(BackupVerificationStatus.Verified, verification.Status);
        Assert.Equal(InitialMigration, verification.Backup?.SchemaVersion);
        Assert.Equal("Dane chronione przed awarią", await ReadHomeAreaNameAsync(backupDatabasePath));
        Assert.Equal("ok", await ReadIntegrityResultAsync(backupDatabasePath));
    }

    [Fact]
    public async Task InitializeFreshDatabaseDoesNotCreateProtectionBackup()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        await using var services = CreateServices(paths);

        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);

        Assert.Equal(
            new[] { InitialMigration, VisibilityIndexMigration },
            await ReadAppliedMigrationsAsync(paths.DatabasePath));
        Assert.False(Directory.Exists(paths.BackupsDirectory));
        var factory = services.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        Assert.False(database.Database.HasPendingModelChanges());
    }

    private static async Task CreatePreviousVersionAsync(
        IServiceProvider services,
        ServandaPaths paths)
    {
        CreatePrivateDatabaseFile(paths.DatabasePath);
        var factory = services.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        await database.Database.MigrateAsync(InitialMigration);
        await InitialAreaSeed.ApplyAsync(database, TimeProvider.System, CancellationToken.None);
    }

    private static ServiceProvider CreateServices(
        ServandaPaths paths,
        DbCommandInterceptor? interceptor = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
        services.AddServandaDatabase(paths, "test-version");
        if (interceptor is not null)
        {
            var options = new DbContextOptionsBuilder<ServandaDbContext>()
                .UseSqlite(CreateConnectionString(paths.DatabasePath, SqliteOpenMode.ReadWriteCreate))
                .AddInterceptors(interceptor)
                .Options;
            services.AddSingleton<IDbContextFactory<ServandaDbContext>>(
                new TestDbContextFactory(options));
        }

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

    private static void CreatePrivateDatabaseFile(string path)
    {
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            UnixCreateMode = PrivateFileMode,
        });
    }

    private static async Task UpdateAreaNameAsync(string databasePath, string name)
    {
        await using var connection = new SqliteConnection(
            CreateConnectionString(databasePath, SqliteOpenMode.ReadWrite));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "UPDATE areas SET name = $name WHERE module_key = 'home';";
        command.Parameters.AddWithValue("$name", name);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadAppliedMigrationsAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(
            CreateConnectionString(databasePath, SqliteOpenMode.ReadOnly));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT MigrationId FROM __EFMigrationsHistory ORDER BY MigrationId;";
        await using var reader = await command.ExecuteReaderAsync();
        var migrations = new List<string>();
        while (await reader.ReadAsync())
        {
            migrations.Add(reader.GetString(0));
        }

        return migrations;
    }

    private static async Task<bool> IndexExistsAsync(string databasePath, string indexName)
    {
        await using var connection = new SqliteConnection(
            CreateConnectionString(databasePath, SqliteOpenMode.ReadOnly));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = $name;";
        command.Parameters.AddWithValue("$name", indexName);
        return (long)(await command.ExecuteScalarAsync() ?? 0L) == 1;
    }

    private static async Task<string?> ReadHomeAreaNameAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(
            CreateConnectionString(databasePath, SqliteOpenMode.ReadOnly));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM areas WHERE module_key = 'home';";
        return await command.ExecuteScalarAsync() as string;
    }

    private static async Task<string?> ReadIntegrityResultAsync(string databasePath)
    {
        await using var connection = new SqliteConnection(
            CreateConnectionString(databasePath, SqliteOpenMode.ReadOnly));
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        return await command.ExecuteScalarAsync() as string;
    }

    private static string CreateConnectionString(string path, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();

    private sealed class TestDbContextFactory(DbContextOptions<ServandaDbContext> options)
        : IDbContextFactory<ServandaDbContext>
    {
        public ServandaDbContext CreateDbContext() => new(options);
    }

    private sealed class FailVisibilityIndexInterceptor : DbCommandInterceptor
    {
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains(VisibilityIndex, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Kontrolowana awaria migracji.");
            }

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

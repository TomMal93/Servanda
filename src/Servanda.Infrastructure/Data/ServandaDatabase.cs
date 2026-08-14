using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Areas;
using Servanda.Application.Catalog;
using Servanda.Application.Prompts;
using Servanda.Application.Tools;
using Servanda.Application.DataProtection;
using Servanda.Infrastructure.Data.Backups;
using Servanda.Infrastructure.Runtime;
using System.Runtime.Versioning;

namespace Servanda.Infrastructure.Data;

[SupportedOSPlatform("linux")]
public static class ServandaDatabase
{
    public static IServiceCollection AddServandaDatabase(
        this IServiceCollection services,
        ServandaPaths paths,
        string applicationVersion)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationVersion);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        services.AddDbContextFactory<ServandaDbContext>(options => options.UseSqlite(connectionString));
        services.AddTransient<IAreaService, SqliteAreaService>();
        services.AddTransient<ICategoryService, SqliteCategoryService>();
        services.AddTransient<ITagService, SqliteTagService>();
        services.AddTransient<IToolCatalogService, SqliteToolCatalogService>();
        services.AddTransient<IPromptLibraryService, SqlitePromptLibraryService>();
        services.AddSingleton(provider => new SqliteBackupService(
            paths,
            provider.GetRequiredService<TimeProvider>(),
            applicationVersion));
        services.AddSingleton<IBackupService>(provider => provider.GetRequiredService<SqliteBackupService>());
        services.AddSingleton<IDatabaseRecoveryService>(provider => new SqliteRecoveryService(
            paths,
            provider.GetRequiredService<TimeProvider>(),
            provider.GetRequiredService<SqliteBackupService>()));
        return services;
    }

    public static async Task InitializeAsync(
        IServiceProvider services,
        ServandaPaths paths,
        TimeProvider timeProvider,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);
        ArgumentNullException.ThrowIfNull(timeProvider);

        var failure = DatabaseInitializationFailure.DatabaseAccess;
        var backupState = ProtectionBackupState.NotCreated;
        try
        {
            PreparePrivateDatabaseFile(paths.DatabasePath);

            await using var scope = services.CreateAsyncScope();
            var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();
            await using var database = await factory.CreateDbContextAsync(cancellationToken);
            await VerifyFullTextSearchAsync(database, cancellationToken);
            var appliedMigrations = (await database.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var pendingMigrations = (await database.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
            if (appliedMigrations.Count > 0 && pendingMigrations.Count > 0)
            {
                failure = DatabaseInitializationFailure.ProtectionBackup;
                var backup = await backupService.CreateAsync(BackupReason.Migration, cancellationToken);
                var verification = await backupService.VerifyAsync(backup.Id, cancellationToken);
                if (verification.Status != BackupVerificationStatus.Verified)
                {
                    throw new InvalidDataException("Kopia ochronna przed migracją nie przeszła weryfikacji.");
                }

                backupState = ProtectionBackupState.Verified;
            }

            failure = DatabaseInitializationFailure.Migration;
            await database.Database.MigrateAsync(cancellationToken);
            failure = DatabaseInitializationFailure.DatabaseAccess;
            await InitialAreaSeed.ApplyAsync(database, timeProvider, cancellationToken);
            PrivateFileSystem.VerifyPrivateFile(paths.DatabasePath, LinuxIdentity.GetEffectiveUserId());
            await backupService.ApplyRetentionAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DatabaseInitializationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new DatabaseInitializationException(failure, backupState, exception);
        }
    }

    /// <summary>
    /// Brak FTS5 jest błędem niezgodnego artefaktu, a nie powodem cichego przejścia na pełny skan.
    /// </summary>
    private static async Task VerifyFullTextSearchAsync(
        ServandaDbContext database,
        CancellationToken cancellationToken)
    {
        var connection = database.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM pragma_compile_options WHERE compile_options = 'ENABLE_FTS5';";
        var enabled = Convert.ToInt64(
            await command.ExecuteScalarAsync(cancellationToken),
            System.Globalization.CultureInfo.InvariantCulture);
        if (enabled == 0)
        {
            throw new InvalidOperationException(
                "Artefakt aplikacji nie zawiera modułu SQLite FTS5 wymaganego przez wyszukiwanie.");
        }
    }

    private static void PreparePrivateDatabaseFile(string path)
    {
        if (File.Exists(path))
        {
            PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.GetEffectiveUserId());
            return;
        }

        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 1,
            Options = FileOptions.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        });
        PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.GetEffectiveUserId());
    }
}

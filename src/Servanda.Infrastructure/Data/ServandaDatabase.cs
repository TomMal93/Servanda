using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Areas;
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
        services.AddSingleton<IBackupService>(provider => new SqliteBackupService(
            paths,
            provider.GetRequiredService<TimeProvider>(),
            applicationVersion));
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

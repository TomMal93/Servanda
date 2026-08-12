using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Areas;
using Servanda.Infrastructure.Runtime;
using System.Runtime.Versioning;

namespace Servanda.Infrastructure.Data;

[SupportedOSPlatform("linux")]
public static class ServandaDatabase
{
    public static IServiceCollection AddServandaDatabase(
        this IServiceCollection services,
        ServandaPaths paths)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(paths);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = paths.DatabasePath,
            ForeignKeys = true,
            Mode = SqliteOpenMode.ReadWriteCreate,
        }.ToString();

        services.AddDbContextFactory<ServandaDbContext>(options => options.UseSqlite(connectionString));
        services.AddTransient<IAreaService, SqliteAreaService>();
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

        PreparePrivateDatabaseFile(paths.DatabasePath);

        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
        await using var database = await factory.CreateDbContextAsync(cancellationToken);
        await database.Database.MigrateAsync(cancellationToken);
        await InitialAreaSeed.ApplyAsync(database, timeProvider, cancellationToken);
        PrivateFileSystem.VerifyPrivateFile(paths.DatabasePath, LinuxIdentity.GetEffectiveUserId());
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

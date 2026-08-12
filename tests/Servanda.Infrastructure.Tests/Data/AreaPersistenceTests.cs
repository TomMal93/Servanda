using System.Runtime.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Areas;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class AreaPersistenceTests
{
    [Fact]
    public async Task InitializeMigratesSeedsAndDoesNotOverwriteSavedEdit()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        var services = CreateServices(paths);

        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var areaService = services.GetRequiredService<IAreaService>();
        var original = Assert.Single(await areaService.ListAsync(), area => area.Name == "Dom");
        var result = await areaService.UpdateAsync(new UpdateAreaCommand(
            original.Id,
            "Mój dom",
            "Własny opis",
            original.Revision,
            original.ContentEpoch));

        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var persisted = Assert.Single(await areaService.ListAsync(), area => area.Id == original.Id);

        Assert.Equal(UpdateAreaStatus.Success, result.Status);
        Assert.Equal("Mój dom", persisted.Name);
        Assert.Equal("Własny opis", persisted.Description);
        Assert.Equal(2, persisted.Revision);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(paths.DatabasePath));
    }

    [Fact]
    public async Task UpdateRejectsStaleRevisionWithoutOverwritingSavedData()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var areaService = services.GetRequiredService<IAreaService>();
        var original = (await areaService.ListAsync())[0];

        var first = await areaService.UpdateAsync(new UpdateAreaCommand(
            original.Id,
            "Pierwsza zmiana",
            original.Description,
            original.Revision,
            original.ContentEpoch));
        var stale = await areaService.UpdateAsync(new UpdateAreaCommand(
            original.Id,
            "Nadpisanie",
            original.Description,
            original.Revision,
            original.ContentEpoch));
        var persisted = Assert.Single(await areaService.ListAsync(), area => area.Id == original.Id);

        Assert.Equal(UpdateAreaStatus.Success, first.Status);
        Assert.Equal(UpdateAreaStatus.Conflict, stale.Status);
        Assert.Equal("Pierwsza zmiana", persisted.Name);
    }

    [Fact]
    public async Task InitialMigrationCreatesRequiredTables()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        var services = CreateServices(paths);

        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        await using var scope = services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
        await using var database = await factory.CreateDbContextAsync();
        var tables = await database.Database.SqlQueryRaw<string>(
            "SELECT name AS Value FROM sqlite_master WHERE type = 'table'").ToListAsync();

        Assert.Contains("app_state", tables);
        Assert.Contains("ordering_scopes", tables);
        Assert.Contains("areas", tables);
        Assert.Contains("__EFMigrationsHistory", tables);
    }

    private static ServiceProvider CreateServices(ServandaPaths paths)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddServandaDatabase(paths);
        return services.BuildServiceProvider();
    }

    private static ServandaPaths CreatePaths(string root)
    {
        var paths = new ServandaPaths(
            Path.Combine(root, "runtime"),
            Path.Combine(root, "state"),
            Path.Combine(root, "data"));
        Directory.CreateDirectory(paths.RuntimeDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateDirectory(paths.StateDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        Directory.CreateDirectory(paths.DataDirectory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        return paths;
    }
}

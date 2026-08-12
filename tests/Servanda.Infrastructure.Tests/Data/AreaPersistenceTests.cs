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
    public async Task CreateAppendsAreaAndRejectsStaleOrderingRevision()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var areaService = services.GetRequiredService<IAreaService>();
        var initial = await areaService.ListAsync();
        var listState = initial[0];

        var created = await areaService.CreateAsync(new CreateAreaCommand(
            "Projekty",
            "Rzeczy do zrobienia",
            "generic",
            "accent-2",
            listState.OrderingRevision,
            listState.ContentEpoch));
        var stale = await areaService.CreateAsync(new CreateAreaCommand(
            "Nie powinien powstać",
            string.Empty,
            "generic",
            "accent-0",
            listState.OrderingRevision,
            listState.ContentEpoch));
        var persisted = await areaService.ListAsync();

        Assert.Equal(CreateAreaStatus.Success, created.Status);
        Assert.NotNull(created.Area);
        Assert.Equal(7, created.Area.SortOrder);
        Assert.Equal(2, created.Area.OrderingRevision);
        Assert.Equal(26, created.Area.Id.Length);
        Assert.Equal(CreateAreaStatus.Conflict, stale.Status);
        Assert.Equal(8, persisted.Count);
        Assert.Equal("Projekty", persisted[^1].Name);
        Assert.Equal(2, persisted[^1].OrderingRevision);
    }

    [Fact]
    public async Task MoveReordersDenseListWithoutChangingContentRevisionAndRejectsStaleCommand()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var areaService = services.GetRequiredService<IAreaService>();
        var initial = await areaService.ListAsync();
        var moved = initial[^1];

        var result = await areaService.MoveAsync(new MoveAreaCommand(
            moved.Id,
            initial[0].Id,
            initial[0].OrderingRevision,
            initial[0].ContentEpoch));
        var stale = await areaService.MoveAsync(new MoveAreaCommand(
            initial[1].Id,
            null,
            initial[0].OrderingRevision,
            initial[0].ContentEpoch));
        var persisted = await areaService.ListAsync();

        Assert.Equal(MoveAreaStatus.Success, result.Status);
        Assert.NotNull(result.Areas);
        Assert.Equal(moved.Id, result.Areas[0].Id);
        Assert.Equal(2, result.Areas[0].OrderingRevision);
        Assert.Equal(MoveAreaStatus.Conflict, stale.Status);
        Assert.Equal(Enumerable.Range(0, persisted.Count), persisted.Select(area => area.SortOrder));
        Assert.All(persisted, area => Assert.Equal(1, area.Revision));
        Assert.Equal(moved.Id, persisted[0].Id);
    }

    [Fact]
    public async Task SetVisibilityFiltersUserListPersistsAndRejectsStaleRevision()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = CreatePaths(temporaryDirectory.Path);
        var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        var areaService = services.GetRequiredService<IAreaService>();
        var original = Assert.Single(await areaService.ListAsync(), area => area.Name == "Dom");

        var hidden = await areaService.SetVisibilityAsync(new SetAreaVisibilityCommand(
            original.Id,
            true,
            original.Revision,
            original.ContentEpoch));
        var stale = await areaService.SetVisibilityAsync(new SetAreaVisibilityCommand(
            original.Id,
            false,
            original.Revision,
            original.ContentEpoch));
        var visibleAreas = await areaService.ListAsync();
        var managedArea = Assert.Single(
            await areaService.ListForManagementAsync(),
            area => area.Id == original.Id);

        Assert.Equal(SetAreaVisibilityStatus.Success, hidden.Status);
        Assert.NotNull(hidden.Area);
        Assert.True(hidden.Area.IsHidden);
        Assert.Equal(2, hidden.Area.Revision);
        Assert.Equal(SetAreaVisibilityStatus.Conflict, stale.Status);
        Assert.DoesNotContain(visibleAreas, area => area.Id == original.Id);
        Assert.True(managedArea.IsHidden);

        var restored = await areaService.SetVisibilityAsync(new SetAreaVisibilityCommand(
            managedArea.Id,
            false,
            managedArea.Revision,
            managedArea.ContentEpoch));

        Assert.Equal(SetAreaVisibilityStatus.Success, restored.Status);
        Assert.Contains(await areaService.ListAsync(), area => area.Id == original.Id && !area.IsHidden);
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

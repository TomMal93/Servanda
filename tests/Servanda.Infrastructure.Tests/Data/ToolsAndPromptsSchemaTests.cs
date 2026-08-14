using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Areas;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class ToolsAndPromptsSchemaTests
{
    private const string PreviousMigration = "20260813120000_AddAreaVisibilityIndex";

    [Fact]
    public async Task FreshDatabaseCreatesModuleTablesAndSearchIndexes()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var tables = await ReadTablesAsync(services);

        Assert.Contains("categories", tables);
        Assert.Contains("tags", tables);
        Assert.Contains("tools", tables);
        Assert.Contains("tool_tags", tables);
        Assert.Contains("prompts", tables);
        Assert.Contains("prompt_tags", tables);
        Assert.Contains("prompt_variants", tables);
        Assert.Contains("prompt_variables", tables);
        Assert.Contains("prompt_versions", tables);
        Assert.Contains("prompt_usage", tables);
        Assert.Contains("tool_search", tables);
        Assert.Contains("prompt_search", tables);
    }

    [Fact]
    public async Task FreshDatabaseActivatesToolAndPromptModulesWithRootCategoryScopes()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await using var database = await CreateContextAsync(services);

        var active = await database.Areas
            .AsNoTracking()
            .Where(area => area.Availability == "active")
            .Select(area => area.ModuleKey)
            .OrderBy(moduleKey => moduleKey)
            .ToListAsync();
        var areaIds = await database.Areas.AsNoTracking().Select(area => area.Id).ToListAsync();
        var scopeKeys = await database.Database
            .SqlQueryRaw<string>("SELECT scope_key AS Value FROM ordering_scopes")
            .ToListAsync();

        Assert.Equal(["prompts", "tools"], active);
        foreach (var areaId in areaIds)
        {
            Assert.Contains($"categories:{areaId}:root", scopeKeys);
        }
    }

    [Fact]
    public async Task MigrationFromPreviousVersionKeepsAreasAndActivatesModules()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var paths = TestDatabase.CreatePaths(temporaryDirectory.Path);
        await using var services = TestDatabase.CreateServices(paths);
        await CreatePreviousVersionAsync(services, paths);

        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);

        var areaService = services.GetRequiredService<IAreaService>();
        var areas = await areaService.ListAsync();
        var tools = Assert.Single(areas, area => area.Id == ToolAreaSeedId);
        var home = Assert.Single(areas, area => area.Name == "Dom");

        Assert.Equal("Moje narzędzia", tools.Name);
        Assert.Equal("active", tools.Availability);
        Assert.Equal("planned", home.Availability);
        Assert.Contains("prompt_search", await ReadTablesAsync(services));
    }

    [Fact]
    public async Task SearchIndexMatchesQueryWithoutPolishDiacritics()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await using var database = await CreateContextAsync(services);
        var connection = (SqliteConnection)database.Database.GetDbConnection();
        await connection.OpenAsync();

        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText =
                "INSERT INTO tool_search(entity_id, name, tags, category_path, url, description) "
                + "VALUES ('01TEST', 'lodz kalkulator', '', '', '', '');";
            await insert.ExecuteNonQueryAsync();
        }

        await using var query = connection.CreateCommand();
        query.CommandText = "SELECT COUNT(*) FROM tool_search WHERE tool_search MATCH '\"lod\"*';";
        Assert.Equal(1L, Assert.IsType<long>(await query.ExecuteScalarAsync()));
    }

    private const string ToolAreaSeedId = "01J00000000000000000000002";

    private static async Task<ServandaDbContext> CreateContextAsync(IServiceProvider services)
    {
        var factory = services.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
        return await factory.CreateDbContextAsync();
    }

    private static async Task<IReadOnlyList<string>> ReadTablesAsync(IServiceProvider services)
    {
        await using var database = await CreateContextAsync(services);
        return await database.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table'")
            .ToListAsync();
    }

    private static async Task CreatePreviousVersionAsync(IServiceProvider services, ServandaPaths paths)
    {
        using (var stream = new FileStream(paths.DatabasePath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
        }))
        {
            await stream.FlushAsync();
        }

        await using var database = await CreateContextAsync(services);
        await database.Database.MigrateAsync(PreviousMigration);
        await InitialAreaSeed.ApplyAsync(database, TimeProvider.System, CancellationToken.None);

        // Baza etapu P3 nie znała aktywnych modułów i zawierała edycję użytkownika.
        await database.Database.ExecuteSqlRawAsync("UPDATE areas SET availability = 'planned';");
        await database.Database.ExecuteSqlRawAsync(
            "UPDATE areas SET name = 'Moje narzędzia' WHERE id = {0};",
            ToolAreaSeedId);
    }
}

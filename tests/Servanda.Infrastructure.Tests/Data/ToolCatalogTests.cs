using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Catalog;
using Servanda.Application.Common;
using Servanda.Application.Tools;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class ToolCatalogTests
{
    private const string ToolAreaId = "01J00000000000000000000002";

    [Fact]
    public async Task CreatePersistsToolWithTagsAndFindsItByPolishSpellingVariants()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);

        var created = await context.CreateToolAsync("Kalkulator Łódź", tags: ["Ważne"]);

        Assert.Equal(WriteStatus.Success, created.Status);
        foreach (var text in new[] { "Łódź", "lodz", "ŁÓDŹ", "lod" })
        {
            var page = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: text));
            var card = Assert.Single(page.Items);
            Assert.Equal("Kalkulator Łódź", card.Name);
            Assert.Equal(1, page.TotalCount);
            Assert.Equal(["Ważne"], card.VisibleTags);
        }
    }

    [Fact]
    public async Task SearchIgnoresTextResemblingFtsSyntaxAndShortQuery()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        await context.CreateToolAsync("Notatnik");

        var syntax = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "\"notatnik\" -*"));
        var logical = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "notatnik AND kalendarz"));
        var single = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "n"));

        Assert.Single(syntax.Items);
        Assert.Empty(logical.Items);
        Assert.True(single.QueryTooShort);
        Assert.Single(single.Items);
    }

    [Fact]
    public async Task SearchPutsExactNameMatchBeforeOtherResults()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        await context.CreateToolAsync("Plan tygodnia", description: "Plan i harmonogram");
        await context.CreateToolAsync("Plan", description: "Zwykły plan");

        var page = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "plan"));

        Assert.Equal(2, page.TotalCount);
        Assert.Equal("Plan", page.Items[0].Name);
    }

    [Fact]
    public async Task SearchExplainsMatchFoundOnlyInHiddenTag()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        await context.CreateToolAsync(
            "Edytor",
            description: "Prosty edytor tekstu",
            tags: ["alfa", "beta", "gamma", "zeta"]);

        var page = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "zeta"));

        var card = Assert.Single(page.Items);
        Assert.Equal("Dopasowanie w tagach", card.MatchExplanation);
        Assert.Equal(3, card.VisibleTags.Count);
        Assert.Equal(1, card.HiddenTagCount);
    }

    [Fact]
    public async Task CategoryFilterIncludesDescendantsAndUnknownIdentifierShowsEverything()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        var child = await context.CreateCategoryAsync("Podkategoria", context.CategoryId);
        await context.CreateToolAsync("Rodzic");
        await context.CreateToolAsync("Dziecko", categoryId: child.Id);

        var parentFilter = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, CategoryId: context.CategoryId));
        var childFilter = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, CategoryId: child.Id));
        var unknownFilter = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, CategoryId: "01UNKNOWN"));

        Assert.Equal(2, parentFilter.TotalCount);
        Assert.Equal(1, childFilter.TotalCount);
        Assert.Equal(2, unknownFilter.TotalCount);
    }

    [Fact]
    public async Task UpdateRejectsStaleRevisionWithoutOverwritingNewerData()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        var created = await context.CreateToolAsync("Pierwsze");
        var tool = created.Tool!;
        var epoch = (await context.Tools.SearchAsync(new ToolQuery(ToolAreaId))).ContentEpoch;

        var first = await context.Tools.UpdateAsync(new UpdateToolCommand(
            tool.Id,
            "Zapisana zmiana",
            tool.Description,
            tool.Url,
            [],
            tool.Revision,
            epoch));
        var stale = await context.Tools.UpdateAsync(new UpdateToolCommand(
            tool.Id,
            "Nadpisanie",
            tool.Description,
            tool.Url,
            [],
            tool.Revision,
            epoch));
        var persisted = Assert.Single((await context.Tools.SearchAsync(new ToolQuery(ToolAreaId))).Items);

        Assert.Equal(WriteStatus.Success, first.Status);
        Assert.Equal(WriteStatus.Conflict, stale.Status);
        Assert.Equal("Zapisana zmiana", persisted.Name);
        Assert.Equal(2, persisted.Revision);
    }

    [Fact]
    public async Task RenamingTagRebuildsSearchDocumentsOfDependentTools()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        await context.CreateToolAsync("Konwerter", tags: ["stary"]);
        var tags = services.GetRequiredService<ITagService>();
        var tag = Assert.Single(await tags.ListAsync(ToolAreaId));
        var epoch = (await context.Tools.SearchAsync(new ToolQuery(ToolAreaId))).ContentEpoch;

        var renamed = await tags.RenameAsync(new RenameTagCommand(tag.Id, "nowy", tag.Revision, epoch));
        var byOldName = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "stary"));
        var byNewName = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "nowy"));

        Assert.Equal(WriteStatus.Success, renamed.Status);
        Assert.Empty(byOldName.Items);
        Assert.Single(byNewName.Items);
    }

    [Fact]
    public async Task MoveBetweenGroupsKeepsDenseOrderAndAdvancesBothScopes()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        var first = (await context.CreateToolAsync("Pierwsze")).Tool!;
        var second = (await context.CreateToolAsync("Drugie")).Tool!;
        var third = (await context.CreateToolAsync("Trzecie")).Tool!;
        var epoch = (await context.Tools.SearchAsync(new ToolQuery(ToolAreaId))).ContentEpoch;
        var regular = await context.Tools.GetScopeAsync(context.CategoryId, "regular");
        var featured = await context.Tools.GetScopeAsync(context.CategoryId, "featured");

        var moved = await context.Tools.MoveAsync(new MoveToolCommand(
            second.Id,
            context.CategoryId,
            "featured",
            null,
            second.Revision,
            regular.Revision,
            featured.Revision,
            epoch));
        var page = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId));
        var stale = await context.Tools.MoveAsync(new MoveToolCommand(
            third.Id,
            context.CategoryId,
            "featured",
            null,
            third.Revision,
            regular.Revision,
            featured.Revision,
            epoch));

        Assert.Equal(WriteStatus.Success, moved.Status);
        Assert.Equal(WriteStatus.Conflict, stale.Status);
        Assert.Equal("featured", page.Items[0].GroupKey);
        Assert.Equal("Drugie", page.Items[0].Name);
        Assert.Equal([0, 1], page.Items.Where(item => item.GroupKey == "regular").Select(item => item.SortOrder));
        Assert.Equal(first.Id, page.Items.First(item => item.GroupKey == "regular").Id);
    }

    [Fact]
    public async Task DeleteRemovesToolFromSearchIndexAndRenumbersGroup()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        var first = (await context.CreateToolAsync("Pierwsze")).Tool!;
        await context.CreateToolAsync("Drugie");
        var epoch = (await context.Tools.SearchAsync(new ToolQuery(ToolAreaId))).ContentEpoch;
        var scope = await context.Tools.GetScopeAsync(context.CategoryId, "regular");

        var deleted = await context.Tools.DeleteAsync(
            new DeleteToolCommand(first.Id, first.Revision, scope.Revision, epoch));
        var page = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId, Text: "pierwsze"));
        var remaining = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId));

        Assert.Equal(WriteStatus.Success, deleted.Status);
        Assert.Empty(page.Items);
        Assert.Equal([0], remaining.Items.Select(item => item.SortOrder));
    }

    [Fact]
    public async Task ConfirmedCategorySubtreeDeletionCreatesBackupAndRemovesContent()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await ModuleContext.CreateAsync(services);
        var child = await context.CreateCategoryAsync("Podkategoria", context.CategoryId);
        await context.CreateToolAsync("Narzędzie", categoryId: child.Id);
        var tree = await context.Categories.GetTreeAsync(ToolAreaId);
        var node = Assert.Single(tree.Roots);
        var preview = await context.Categories.PreviewDeleteAsync(node.Category.Id);

        var unconfirmed = await context.Categories.DeleteAsync(new DeleteCategoryCommand(
            preview!.Id,
            preview.Revision,
            preview.ParentScopeRevision,
            preview.ContentEpoch,
            preview.DescendantCategories,
            preview.Tools,
            preview.Prompts));
        var result = await context.Categories.DeleteAsync(new DeleteCategoryCommand(
            preview.Id,
            preview.Revision,
            preview.ParentScopeRevision,
            preview.ContentEpoch,
            preview.DescendantCategories,
            preview.Tools,
            preview.Prompts,
            Confirmed: true));
        var after = await context.Categories.GetTreeAsync(ToolAreaId);
        var tools = await context.Tools.SearchAsync(new ToolQuery(ToolAreaId));
        var backupDirectories = Directory.GetDirectories(
            Path.Combine(temporaryDirectory.Path, "data", "backups"));

        Assert.Equal(1, preview.DescendantCategories);
        Assert.Equal(1, preview.Tools);
        Assert.Equal(WriteStatus.ValidationFailed, unconfirmed.Status);
        Assert.Equal(WriteStatus.Success, result.Status);
        Assert.Empty(after.Roots);
        Assert.Empty(tools.Items);
        Assert.Single(backupDirectories);
    }

    private sealed class ModuleContext(
        IToolCatalogService tools,
        ICategoryService categories,
        string categoryId)
    {
        public IToolCatalogService Tools { get; } = tools;

        public ICategoryService Categories { get; } = categories;

        public string CategoryId { get; } = categoryId;

        public static async Task<ModuleContext> CreateAsync(IServiceProvider services)
        {
            var categories = services.GetRequiredService<ICategoryService>();
            var tree = await categories.GetTreeAsync(ToolAreaId);
            var created = await categories.CreateAsync(new CreateCategoryCommand(
                ToolAreaId,
                null,
                "Ogólne",
                "Kategoria testowa",
                tree.ScopeRevisionFor(null),
                tree.ContentEpoch));
            Assert.Equal(WriteStatus.Success, created.Status);
            return new ModuleContext(
                services.GetRequiredService<IToolCatalogService>(),
                categories,
                created.Category!.Id);
        }

        public async Task<CategoryItem> CreateCategoryAsync(string name, string? parentId)
        {
            var tree = await Categories.GetTreeAsync(ToolAreaId);
            var created = await Categories.CreateAsync(new CreateCategoryCommand(
                ToolAreaId,
                parentId,
                name,
                string.Empty,
                tree.ScopeRevisionFor(parentId),
                tree.ContentEpoch));
            Assert.Equal(WriteStatus.Success, created.Status);
            return created.Category!;
        }

        public async Task<ToolResult> CreateToolAsync(
            string name,
            string description = "Opis narzędzia",
            string? categoryId = null,
            IReadOnlyList<string>? tags = null,
            string groupKey = "regular")
        {
            var targetCategoryId = categoryId ?? CategoryId;
            var page = await Tools.SearchAsync(new ToolQuery(ToolAreaId));
            var scope = await Tools.GetScopeAsync(targetCategoryId, groupKey);
            return await Tools.CreateAsync(new CreateToolCommand(
                ToolAreaId,
                targetCategoryId,
                groupKey,
                name,
                description,
                "https://example.com/" + Guid.NewGuid().ToString("N"),
                tags ?? [],
                scope.Revision,
                page.ContentEpoch));
        }
    }
}

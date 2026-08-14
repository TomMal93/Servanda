using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Catalog;
using Servanda.Application.Common;
using Servanda.Application.DataTransfer;
using Servanda.Application.Prompts;
using Servanda.Application.Tools;
using Servanda.Domain.Prompts;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class CollectionTransferTests
{
    private const string ToolAreaId = "01J00000000000000000000002";
    private const string PromptAreaId = "01J00000000000000000000001";

    [Fact]
    public async Task ExportedDocumentRestoresCollectionAfterImport()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await SeedAsync(services);

        var export = await services.GetRequiredService<ICollectionExportService>().ExportAsync();
        var tools = services.GetRequiredService<IToolCatalogService>();
        var prompts = services.GetRequiredService<IPromptLibraryService>();
        var before = await tools.SearchAsync(new ToolQuery(ToolAreaId));

        // Kolekcja zmienia się po eksporcie, więc import musi ją odtworzyć.
        var scope = await tools.GetScopeAsync(before.Items[0].CategoryId, before.Items[0].GroupKey);
        var removed = await tools.DeleteAsync(new DeleteToolCommand(
            before.Items[0].Id,
            before.Items[0].Revision,
            scope.Revision,
            before.ContentEpoch));

        var import = services.GetRequiredService<ICollectionImportService>();
        await using var stream = File.OpenRead(export.FilePath);
        var preview = await import.PrepareAsync(stream);
        var applied = await import.ApplyAsync(preview.Token!);
        var after = await tools.SearchAsync(new ToolQuery(ToolAreaId));
        var promptPage = await prompts.SearchAsync(new PromptQuery(PromptAreaId));

        Assert.Equal(WriteStatus.Success, removed.Status);
        Assert.Equal(ImportPreviewStatus.Ready, preview.Status);
        Assert.Equal(1, preview.SchemaVersion);
        Assert.Equal(ImportApplyStatus.Applied, applied.Status);
        Assert.Equal(2, after.TotalCount);
        Assert.Equal(before.Items.Select(item => item.Name).Order(), after.Items.Select(item => item.Name).Order());
        Assert.Single(promptPage.Items);
        Assert.NotEqual(before.ContentEpoch, after.ContentEpoch);
    }

    [Fact]
    public async Task PreviewCountsAddedReplacedAndRemovedItems()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await SeedAsync(services);
        var export = await services.GetRequiredService<ICollectionExportService>().ExportAsync();

        var tools = services.GetRequiredService<IToolCatalogService>();
        var page = await tools.SearchAsync(new ToolQuery(ToolAreaId));
        var scope = await tools.GetScopeAsync(page.Items[0].CategoryId, page.Items[0].GroupKey);
        await tools.CreateAsync(new CreateToolCommand(
            ToolAreaId,
            page.Items[0].CategoryId,
            page.Items[0].GroupKey,
            "Dodane po eksporcie",
            "Opis narzędzia",
            "https://example.com/nowe",
            [],
            scope.Revision,
            page.ContentEpoch));

        await using var stream = File.OpenRead(export.FilePath);
        var preview = await services.GetRequiredService<ICollectionImportService>().PrepareAsync(stream);
        var toolsSection = Assert.Single(preview.Sections, section => section.Section == "Narzędzia");

        Assert.Equal(ImportPreviewStatus.Ready, preview.Status);
        Assert.Equal(0, toolsSection.Added);
        Assert.Equal(2, toolsSection.Replaced);
        Assert.Equal(1, toolsSection.Removed);
    }

    [Fact]
    public async Task InvalidDocumentIsRejectedAndLeavesCollectionUnchanged()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await SeedAsync(services);
        var export = await services.GetRequiredService<ICollectionExportService>().ExportAsync();
        var broken = (await File.ReadAllTextAsync(export.FilePath))
            .Replace("\"url\": \"https://", "\"url\": \"javascript:", StringComparison.Ordinal);

        var import = services.GetRequiredService<ICollectionImportService>();
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(broken));
        var preview = await import.PrepareAsync(stream);
        var tools = services.GetRequiredService<IToolCatalogService>();
        var page = await tools.SearchAsync(new ToolQuery(ToolAreaId));

        Assert.Equal(ImportPreviewStatus.Rejected, preview.Status);
        Assert.NotEmpty(preview.Problems);
        Assert.Null(preview.Token);
        Assert.Equal(2, page.TotalCount);
        Assert.False(Directory.Exists(Path.Combine(temporaryDirectory.Path, "data", "backups")));
    }

    [Fact]
    public async Task DocumentWithoutActiveModuleAreaIsRejected()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await SeedAsync(services);
        var export = await services.GetRequiredService<ICollectionExportService>().ExportAsync();
        var broken = (await File.ReadAllTextAsync(export.FilePath))
            .Replace("\"availability\": \"active\"", "\"availability\": \"planned\"", StringComparison.Ordinal);

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(broken));
        var preview = await services.GetRequiredService<ICollectionImportService>().PrepareAsync(stream);

        Assert.Equal(ImportPreviewStatus.Rejected, preview.Status);
        Assert.Contains(preview.Problems, problem => problem.Contains("tools", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DiscardingPreviewRemovesStagingDatabaseWithoutChangingCollection()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await SeedAsync(services);
        var export = await services.GetRequiredService<ICollectionExportService>().ExportAsync();
        var import = services.GetRequiredService<ICollectionImportService>();
        await using var stream = File.OpenRead(export.FilePath);
        var preview = await import.PrepareAsync(stream);

        await import.DiscardAsync(preview.Token!);

        Assert.Empty(Directory.GetDirectories(Path.Combine(temporaryDirectory.Path, "data"), "import-*"));
        Assert.Equal(2, (await services.GetRequiredService<IToolCatalogService>()
            .SearchAsync(new ToolQuery(ToolAreaId))).TotalCount);
        Assert.Equal(ImportApplyStatus.Expired, (await import.ApplyAsync(preview.Token!)).Status);
    }

    [Fact]
    public async Task ExportedFileIsPrivateAndUsesApplicationGeneratedName()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        await SeedAsync(services);

        var export = await services.GetRequiredService<ICollectionExportService>().ExportAsync();
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(export.FilePath));

        Assert.StartsWith(
            Path.Combine(temporaryDirectory.Path, "data", "exports"),
            export.FilePath,
            StringComparison.Ordinal);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(export.FilePath));
        Assert.Equal(1, document.RootElement.GetProperty("schemaVersion").GetInt32());
        Assert.EndsWith("Z", document.RootElement.GetProperty("exportedAt").GetString(), StringComparison.Ordinal);
        Assert.False(document.RootElement.TryGetProperty("orderingScopes", out _));
    }

    private static async Task SeedAsync(IServiceProvider services)
    {
        var categories = services.GetRequiredService<ICategoryService>();
        var toolTree = await categories.GetTreeAsync(ToolAreaId);
        var toolCategory = await categories.CreateAsync(new CreateCategoryCommand(
            ToolAreaId,
            null,
            "Narzędzia codzienne",
            string.Empty,
            toolTree.ScopeRevisionFor(null),
            toolTree.ContentEpoch));

        var tools = services.GetRequiredService<IToolCatalogService>();
        foreach (var (name, group) in new[] { ("Kalkulator", "regular"), ("Notatnik", "featured") })
        {
            var page = await tools.SearchAsync(new ToolQuery(ToolAreaId));
            var scope = await tools.GetScopeAsync(toolCategory.Category!.Id, group);
            var created = await tools.CreateAsync(new CreateToolCommand(
                ToolAreaId,
                toolCategory.Category.Id,
                group,
                name,
                "Opis narzędzia",
                $"https://example.com/{name.ToLowerInvariant()}",
                ["codzienne"],
                scope.Revision,
                page.ContentEpoch));
            Assert.Equal(WriteStatus.Success, created.Status);
        }

        var promptTree = await categories.GetTreeAsync(PromptAreaId);
        var promptCategory = await categories.CreateAsync(new CreateCategoryCommand(
            PromptAreaId,
            null,
            "Pisanie",
            string.Empty,
            promptTree.ScopeRevisionFor(null),
            promptTree.ContentEpoch));

        var prompts = services.GetRequiredService<IPromptLibraryService>();
        var promptPage = await prompts.SearchAsync(new PromptQuery(PromptAreaId));
        var promptScope = await prompts.GetScopeAsync(promptCategory.Category!.Id);
        var prompt = await prompts.CreateAsync(new CreatePromptCommand(
            PromptAreaId,
            promptCategory.Category.Id,
            "Streszczenie",
            "Streszcza tekst",
            ["tekst"],
            [new PromptVariantDraft(null, "Krótki", "czat", "Streść {{temat}}")],
            [new PromptVariableDraft(null, "temat", "Temat", string.Empty, true, false)],
            false,
            promptScope.Revision,
            promptPage.ContentEpoch));
        Assert.Equal(WriteStatus.Success, prompt.Status);

        var editor = await prompts.GetForEditAsync(prompt.Prompt!.Id);
        await prompts.RecordUsageAsync(new RecordPromptUsageCommand(prompt.Prompt.Id, editor!.Variants[0].Id));
    }
}

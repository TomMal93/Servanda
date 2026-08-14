using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Catalog;
using Servanda.Application.Common;
using Servanda.Application.Prompts;
using Servanda.Domain.Prompts;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class PromptLibraryTests
{
    private const string PromptAreaId = "01J00000000000000000000001";

    [Fact]
    public async Task CreateStoresVariantsVariablesAndFindsPromptByVariantContent()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);

        var created = await context.CreatePromptAsync(
            "Podsumowanie",
            variants: [new PromptVariantDraft(null, "Krótki", "czat", "Streść {{temat}} po polsku")]);
        var editor = await context.Prompts.GetForEditAsync(created.Prompt!.Id);
        var byContent = await context.Prompts.SearchAsync(new PromptQuery(PromptAreaId, Text: "streść"));

        Assert.Equal(WriteStatus.Success, created.Status);
        Assert.NotNull(editor);
        Assert.Equal(["Krótki"], editor.Variants.Select(variant => variant.Name));
        Assert.Equal(["temat"], editor.Variables.Select(variable => variable.Name));
        var card = Assert.Single(byContent.Items);
        Assert.Equal("Dopasowanie w treści", card.MatchExplanation);
        Assert.Equal(1, card.VariantCount);
    }

    [Fact]
    public async Task SavingChangedVariantsCreatesVersionThatCanBeRestored()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);
        var prompt = (await context.CreatePromptAsync("Wersjonowany")).Prompt!;
        var editor = await context.Prompts.GetForEditAsync(prompt.Id);

        var updated = await context.Prompts.UpdateAsync(new UpdatePromptCommand(
            prompt.Id,
            "Wersjonowany",
            "Opis promptu",
            [],
            [new PromptVariantDraft(editor!.Variants[0].Id, "Krótki", null, "Nowa treść {{temat}}")],
            [new PromptVariableDraft(editor.Variables[0].Id, "temat", "Temat", "", true, false)],
            false,
            prompt.Revision,
            editor.ContentEpoch));
        var versions = await context.Prompts.ListVersionsAsync(prompt.Id);
        var version = Assert.Single(versions);

        var restored = await context.Prompts.RestoreVersionAsync(new RestorePromptVersionCommand(
            prompt.Id,
            version.Id,
            updated.Prompt!.Revision,
            editor.ContentEpoch));
        var afterRestore = await context.Prompts.GetForEditAsync(prompt.Id);

        Assert.Equal(WriteStatus.Success, updated.Status);
        Assert.True(version.IsSupported);
        Assert.Equal(WriteStatus.Success, restored.Status);
        Assert.Equal(prompt.Id, afterRestore!.Id);
        Assert.Equal("Treść {{temat}}", afterRestore.Variants[0].Content);
        Assert.Equal(2, (await context.Prompts.ListVersionsAsync(prompt.Id)).Count);
    }

    [Fact]
    public async Task SaveWithoutVariantChangeDoesNotCreateVersion()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);
        var prompt = (await context.CreatePromptAsync("Bez zmian")).Prompt!;
        var editor = await context.Prompts.GetForEditAsync(prompt.Id);

        var updated = await context.Prompts.UpdateAsync(new UpdatePromptCommand(
            prompt.Id,
            "Inny tytuł",
            "Inny opis",
            [],
            [new PromptVariantDraft(editor!.Variants[0].Id, "Krótki", null, editor.Variants[0].Content)],
            [new PromptVariableDraft(editor.Variables[0].Id, "temat", "Temat", "", true, false)],
            false,
            prompt.Revision,
            editor.ContentEpoch));

        Assert.Equal(WriteStatus.Success, updated.Status);
        Assert.Empty(await context.Prompts.ListVersionsAsync(prompt.Id));
    }

    [Fact]
    public async Task FavoriteToggleUsesExpectedRevisionAndConflictsWithStaleEditor()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);
        var prompt = (await context.CreatePromptAsync("Ulubiony")).Prompt!;
        var editor = await context.Prompts.GetForEditAsync(prompt.Id);

        var favorite = await context.Prompts.SetFavoriteAsync(
            new SetPromptFavoriteCommand(prompt.Id, true, prompt.Revision, editor!.ContentEpoch));
        var staleEditorSave = await context.Prompts.UpdateAsync(new UpdatePromptCommand(
            prompt.Id,
            "Nadpisanie",
            "Opis promptu",
            [],
            [new PromptVariantDraft(editor.Variants[0].Id, "Krótki", null, editor.Variants[0].Content)],
            [new PromptVariableDraft(editor.Variables[0].Id, "temat", "Temat", "", true, false)],
            false,
            editor.Revision,
            editor.ContentEpoch));
        var favorites = await context.Prompts.SearchAsync(
            new PromptQuery(PromptAreaId, Filter: PromptFilter.Favorites));

        Assert.Equal(WriteStatus.Success, favorite.Status);
        Assert.Equal(WriteStatus.Conflict, staleEditorSave.Status);
        Assert.Single(favorites.Items);
        Assert.Equal("Ulubiony", favorites.Items[0].Title);
    }

    [Fact]
    public async Task ReorderingPromptAndOwnedRowsPersistsDenseOrder()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);
        var first = (await context.CreatePromptAsync("Pierwszy")).Prompt!;
        var second = (await context.CreatePromptAsync(
            "Drugi",
            variants:
            [
                new PromptVariantDraft(null, "A", null, "{{pierwsza}}"),
                new PromptVariantDraft(null, "B", null, "{{druga}}"),
            ],
            variables:
            [
                new PromptVariableDraft(null, "pierwsza", "Pierwsza", "", false, false),
                new PromptVariableDraft(null, "druga", "Druga", "", false, false),
            ])).Prompt!;
        var scope = await context.Prompts.GetScopeAsync(context.CategoryId);
        var page = await context.Prompts.SearchAsync(new PromptQuery(PromptAreaId));

        var moved = await context.Prompts.MoveAsync(new MovePromptCommand(
            second.Id,
            context.CategoryId,
            first.Id,
            second.Revision,
            scope.Revision,
            scope.Revision,
            page.ContentEpoch));
        var editor = await context.Prompts.GetForEditAsync(second.Id);
        var updated = await context.Prompts.UpdateAsync(new UpdatePromptCommand(
            second.Id,
            second.Title,
            second.Description,
            [],
            [
                new PromptVariantDraft(editor!.Variants[1].Id, "B", null, "{{druga}}"),
                new PromptVariantDraft(editor.Variants[0].Id, "A", null, "{{pierwsza}}"),
            ],
            [
                new PromptVariableDraft(editor.Variables[1].Id, "druga", "Druga", "", false, false),
                new PromptVariableDraft(editor.Variables[0].Id, "pierwsza", "Pierwsza", "", false, false),
            ],
            false,
            editor.Revision,
            editor.ContentEpoch));
        var reordered = await context.Prompts.SearchAsync(new PromptQuery(PromptAreaId));
        var after = await context.Prompts.GetForEditAsync(second.Id);

        Assert.Equal(WriteStatus.Success, moved.Status);
        Assert.Equal(WriteStatus.Success, updated.Status);
        Assert.Equal(["Drugi", "Pierwszy"], reordered.Items.Select(item => item.Title));
        Assert.Equal(["B", "A"], after!.Variants.Select(item => item.Name));
        Assert.Equal(["druga", "pierwsza"], after.Variables.Select(item => item.Name));
    }

    [Fact]
    public async Task RecordingUsageFillsHistoryAndRecentlyUsedFilter()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);
        var prompt = (await context.CreatePromptAsync("Używany")).Prompt!;
        await context.CreatePromptAsync("Nieużywany");
        var editor = await context.Prompts.GetForEditAsync(prompt.Id);

        var recorded = await context.Prompts.RecordUsageAsync(
            new RecordPromptUsageCommand(prompt.Id, editor!.Variants[0].Id));
        var history = await context.Prompts.ListUsageAsync();
        var recent = await context.Prompts.SearchAsync(
            new PromptQuery(PromptAreaId, Filter: PromptFilter.RecentlyUsed));

        Assert.Equal(WriteStatus.Success, recorded);
        var entry = Assert.Single(history);
        Assert.Equal("Używany", entry.PromptTitle);
        Assert.Equal("Krótki", entry.VariantName);
        var card = Assert.Single(recent.Items);
        Assert.NotNull(card.LastUsedAt);
    }

    [Fact]
    public async Task DeletedPromptKeepsReadableUsageSnapshot()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);
        var prompt = (await context.CreatePromptAsync("Do usunięcia")).Prompt!;
        var editor = await context.Prompts.GetForEditAsync(prompt.Id);
        await context.Prompts.RecordUsageAsync(new RecordPromptUsageCommand(prompt.Id, editor!.Variants[0].Id));
        var scope = await context.Prompts.GetScopeAsync(context.CategoryId);

        var deleted = await context.Prompts.DeleteAsync(
            new DeletePromptCommand(prompt.Id, prompt.Revision, scope.Revision, editor.ContentEpoch));
        var history = await context.Prompts.ListUsageAsync();
        var search = await context.Prompts.SearchAsync(new PromptQuery(PromptAreaId, Text: "usunięcia"));

        Assert.Equal(WriteStatus.Success, deleted.Status);
        var entry = Assert.Single(history);
        Assert.Null(entry.PromptId);
        Assert.Equal("Do usunięcia", entry.PromptTitle);
        Assert.Empty(search.Items);
    }

    [Fact]
    public async Task UndefinedPlaceholderIsRejectedBeforeSave()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var context = await LibraryContext.CreateAsync(services);

        var result = await context.CreatePromptAsync(
            "Błędny",
            variants: [new PromptVariantDraft(null, "Krótki", null, "Treść {{nieznana}}")],
            variables: [new PromptVariableDraft(null, "temat", "Temat", "", true, false)]);

        Assert.Equal(WriteStatus.ValidationFailed, result.Status);
        Assert.Contains(nameof(Prompt.Variants), result.Errors!.Keys);
    }

    private sealed class LibraryContext(
        IPromptLibraryService prompts,
        ICategoryService categories,
        string categoryId)
    {
        public IPromptLibraryService Prompts { get; } = prompts;

        public ICategoryService Categories { get; } = categories;

        public string CategoryId { get; } = categoryId;

        public static async Task<LibraryContext> CreateAsync(IServiceProvider services)
        {
            var categories = services.GetRequiredService<ICategoryService>();
            var tree = await categories.GetTreeAsync(PromptAreaId);
            var created = await categories.CreateAsync(new CreateCategoryCommand(
                PromptAreaId,
                null,
                "Ogólne",
                "Kategoria testowa",
                tree.ScopeRevisionFor(null),
                tree.ContentEpoch));
            Assert.Equal(WriteStatus.Success, created.Status);
            return new LibraryContext(
                services.GetRequiredService<IPromptLibraryService>(),
                categories,
                created.Category!.Id);
        }

        public async Task<PromptResult> CreatePromptAsync(
            string title,
            IReadOnlyList<PromptVariantDraft>? variants = null,
            IReadOnlyList<PromptVariableDraft>? variables = null,
            IReadOnlyList<string>? tags = null)
        {
            var page = await Prompts.SearchAsync(new PromptQuery(PromptAreaId));
            var scope = await Prompts.GetScopeAsync(CategoryId);
            return await Prompts.CreateAsync(new CreatePromptCommand(
                PromptAreaId,
                CategoryId,
                title,
                "Opis promptu",
                tags ?? [],
                variants ?? [new PromptVariantDraft(null, "Krótki", null, "Treść {{temat}}")],
                variables ?? [new PromptVariableDraft(null, "temat", "Temat", "", true, false)],
                false,
                scope.Revision,
                page.ContentEpoch));
        }
    }
}

using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.Prompts;
using Servanda.Application.Tools;
using Servanda.Domain.Catalog;
using Servanda.Domain.Prompts;
using Servanda.Domain.Tools;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Data.Transfer;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
public sealed class P4SearchPerformanceTests
{
    [Fact]
    public void ProfilesAndProbesMatchP4QualityContract()
    {
        Assert.Equal((2_000, 1_000),
            (P4SearchPerformanceProfile.Reference.ToolCount, P4SearchPerformanceProfile.Reference.PromptCount));
        Assert.Equal((10_000, 5_000),
            (P4SearchPerformanceProfile.Boundary.ToolCount, P4SearchPerformanceProfile.Boundary.PromptCount));
        Assert.Equal(20260814, P4SearchPerformanceProfile.Seed);

        var document = P4SearchPerformanceProfile.Create(P4SearchPerformanceProfile.Reference);

        Assert.Equal(Category.MaxDepth * 2, document.Categories.Count);
        Assert.All(document.Tools, tool => Assert.Equal(Tool.MaxTags, tool.TagIds.Count));
        Assert.All(document.Prompts, prompt => Assert.Equal(Prompt.MaxTags, prompt.TagIds.Count));
        Assert.Equal(PromptUsageEntry.RetainedEntries, document.PromptUsage.Count);
        var longPrompt = Assert.Single(document.Prompts, prompt => prompt.Title == "Asystent Łódź");
        Assert.Equal(Prompt.MaxVariants, longPrompt.Variants.Count);
        Assert.All(longPrompt.Variants, variant => Assert.Equal(30_000, variant.Content.Length));
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "Kalkulator Łódź");
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "kalk");
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "plan rodziny");
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "Archiwum Łódź");
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "biohacking");
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "docs example");
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "lodz");
        Assert.Contains(P4SearchPerformanceProfile.Probes, probe => probe.Text == "ukrytatresc");
    }

    [Theory]
    [InlineData("reference")]
    [InlineData("boundary")]
    [Trait("Category", "Performance")]
    public async Task SearchProfilesStayWithinSqliteBudgets(string profileName)
    {
        var profile = profileName == "reference"
            ? P4SearchPerformanceProfile.Reference
            : P4SearchPerformanceProfile.Boundary;
        var maximumP95 = profileName == "reference"
            ? TimeSpan.FromMilliseconds(100)
            : TimeSpan.FromMilliseconds(750);

        using var temporaryDirectory = new TemporaryDirectory();
        await using var services = await TestDatabase.InitializeAsync(temporaryDirectory.Path);
        var factory = services.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
        await using (var database = await factory.CreateDbContextAsync())
        await using (var transaction = await database.Database.BeginTransactionAsync())
        {
            await CollectionWriter.ReplaceAsync(
                database,
                P4SearchPerformanceProfile.Create(profile),
                new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero),
                CancellationToken.None);
            await transaction.CommitAsync();
        }

        var tools = services.GetRequiredService<IToolCatalogService>();
        var prompts = services.GetRequiredService<IPromptLibraryService>();
        var first = Stopwatch.StartNew();
        await SearchAsync(P4SearchPerformanceProfile.Probes[0], tools, prompts);
        first.Stop();

        for (var index = 0; index < 10; index++)
        {
            await SearchAsync(
                P4SearchPerformanceProfile.Probes[index % P4SearchPerformanceProfile.Probes.Count],
                tools,
                prompts);
        }

        var samples = new List<TimeSpan>(100);
        for (var index = 0; index < 100; index++)
        {
            var probe = P4SearchPerformanceProfile.Probes[index % P4SearchPerformanceProfile.Probes.Count];
            var stopwatch = Stopwatch.StartNew();
            var hasResults = await SearchAsync(probe, tools, prompts);
            stopwatch.Stop();
            Assert.True(hasResults, $"Profil {profile.Name}: zapytanie „{probe.Text}” nie zwróciło wyniku.");
            samples.Add(stopwatch.Elapsed);
        }

        var ordered = samples.Order().ToArray();
        var median = ordered[ordered.Length / 2];
        var p95 = ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];

        Assert.True(
            first.Elapsed <= TimeSpan.FromMilliseconds(750),
            $"Profil {profile.Name}: pierwsze zapytanie {first.Elapsed.TotalMilliseconds:F1} ms przekracza 750 ms.");
        Assert.True(
            p95 <= maximumP95,
            $"Profil {profile.Name}: mediana {median.TotalMilliseconds:F1} ms, p95 {p95.TotalMilliseconds:F1} ms, budżet {maximumP95.TotalMilliseconds:F0} ms.");
    }

    private static async Task<bool> SearchAsync(
        P4SearchProbe probe,
        IToolCatalogService tools,
        IPromptLibraryService prompts)
    {
        if (probe.Module == "tools")
        {
            var page = await tools.SearchAsync(new ToolQuery(P4SearchPerformanceProfile.ToolAreaId, Text: probe.Text));
            return page.Items.Count > 0;
        }

        var promptPage = await prompts.SearchAsync(
            new PromptQuery(P4SearchPerformanceProfile.PromptAreaId, Text: probe.Text));
        return promptPage.Items.Count > 0;
    }
}

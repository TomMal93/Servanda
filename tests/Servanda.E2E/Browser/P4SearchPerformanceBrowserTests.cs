using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Data.Transfer;
using Servanda.Infrastructure.Runtime;
using Servanda.Infrastructure.Tests.Data;
using Xunit.Abstractions;

namespace Servanda.E2E.Browser;

[CollectionDefinition("P4 browser performance", DisableParallelization = true)]
public sealed class P4SearchPerformanceBrowserCollectionDefinition
{
    public const string CollectionName = "P4 browser performance";
}

[SupportedOSPlatform("linux")]
[Collection(P4SearchPerformanceBrowserCollectionDefinition.CollectionName)]
public sealed class P4SearchPerformanceBrowserTests(ITestOutputHelper output)
{
    [Theory]
    [Trait("Category", "PerformanceBrowser")]
    [InlineData("chromium", "reference")]
    [InlineData("firefox", "reference")]
    [InlineData("chromium", "boundary")]
    [InlineData("firefox", "boundary")]
    public async Task PublishedHostMeetsFullSearchLoopBudget(string browserName, string profileName)
    {
        var artifactDirectory = Environment.GetEnvironmentVariable("SERVANDA_BROWSER_E2E_ARTIFACT");
        Assert.False(
            string.IsNullOrWhiteSpace(artifactDirectory),
            "Ustaw SERVANDA_BROWSER_E2E_ARTIFACT albo uruchom tests/Servanda.E2E/run-p4-performance-tests.sh.");

        var profile = profileName == "reference"
            ? P4SearchPerformanceProfile.Reference
            : P4SearchPerformanceProfile.Boundary;
        var maximumP95 = profileName == "reference"
            ? TimeSpan.FromMilliseconds(500)
            : TimeSpan.FromMilliseconds(1_000);
        var executablePath = Path.Combine(Path.GetFullPath(artifactDirectory!), "Servanda");
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-performance-{browserName}-{profileName}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryPath, BrowserHostFlowTests.PrivateDirectoryMode);
        var runtimeBase = Path.Combine(temporaryPath, "runtime");
        var stateBase = Path.Combine(temporaryPath, "state");
        var homeDirectory = Path.Combine(temporaryPath, "home");
        var shimDirectory = Path.Combine(temporaryPath, "bin");
        var openedAddressesPath = Path.Combine(temporaryPath, "opened-addresses.txt");
        Directory.CreateDirectory(homeDirectory, BrowserHostFlowTests.PrivateDirectoryMode);
        Directory.CreateDirectory(shimDirectory, BrowserHostFlowTests.PrivateDirectoryMode);
        await BrowserHostFlowTests.CreateXdgOpenShimAsync(shimDirectory);

        var applicationData = Path.Combine(homeDirectory, ".local", "share", "servanda");
        Directory.CreateDirectory(applicationData, BrowserHostFlowTests.PrivateDirectoryMode);
        var databasePaths = new ServandaPaths(
            Path.Combine(runtimeBase, "servanda"),
            Path.Combine(stateBase, "servanda"),
            applicationData);
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(databasePaths);
        services.AddServandaDatabase(databasePaths, "p4-browser-performance");
        string sqliteVersion;
        await using (var provider = services.BuildServiceProvider())
        {
            await ServandaDatabase.InitializeAsync(provider, databasePaths, TimeProvider.System);
            var factory = provider.GetRequiredService<IDbContextFactory<ServandaDbContext>>();
            await using var database = await factory.CreateDbContextAsync();
            sqliteVersion = await database.Database
                .SqlQueryRaw<string>("SELECT sqlite_version() AS Value")
                .SingleAsync();
            await using var transaction = await database.Database.BeginTransactionAsync();
            await CollectionWriter.ReplaceAsync(
                database,
                P4SearchPerformanceProfile.Create(profile),
                new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero),
                CancellationToken.None);
            await transaction.CommitAsync();
        }

        var paths = new ServandaPaths(Path.Combine(runtimeBase, "servanda"), Path.Combine(stateBase, "servanda"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(8));
        Process? host = null;
        try
        {
            Assert.Equal(0, await BrowserHostFlowTests.RunLauncherAsync(
                executablePath,
                runtimeBase,
                stateBase,
                homeDirectory,
                shimDirectory,
                openedAddressesPath,
                timeout.Token,
                quietLogging: true));
            var descriptor = await BrowserHostFlowTests.WaitForReadyDescriptorAsync(paths.DescriptorPath, timeout.Token);
            host = Process.GetProcessById(descriptor.ProcessId);
            var bootstrapAddress = Assert.Single(await BrowserHostFlowTests.WaitForOpenedAddressesAsync(
                openedAddressesPath,
                1,
                timeout.Token));

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await BrowserHostFlowTests.LaunchBrowserAsync(playwright, browserName);
            await using var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var circuit = new BrowserHostFlowTests.CircuitWatcher(page);
            page.SetDefaultTimeout(15_000);
            circuit.Reset();
            await page.GotoAsync(bootstrapAddress, new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await page.WaitForURLAsync($"{descriptor.Origin}/");
            await circuit.WaitAsync(timeout.Token);

            var firstQuery = Stopwatch.StartNew();
            circuit.Reset();
            await page.GotoAsync($"{descriptor.Origin}/narzedzia", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await WaitForCompletedSearchAsync(page, "Widoczne narzędzia:");
            await circuit.WaitAsync(timeout.Token);
            firstQuery.Stop();

            var samples = new List<TimeSpan>(100);
            await MeasureModuleAsync(
                page,
                page.GetByLabel("Szukaj narzędzia", new() { Exact = true }),
                "Widoczne narzędzia:",
                P4SearchPerformanceProfile.Probes.Where(probe => probe.Module == "tools").Select(probe => probe.Text).ToArray(),
                samples);

            circuit.Reset();
            await page.GotoAsync($"{descriptor.Origin}/prompty", new PageGotoOptions { WaitUntil = WaitUntilState.DOMContentLoaded });
            await WaitForCompletedSearchAsync(page, "Widoczne prompty:");
            await circuit.WaitAsync(timeout.Token);
            await MeasureModuleAsync(
                page,
                page.GetByLabel("Szukaj promptu", new() { Exact = true }),
                "Widoczne prompty:",
                P4SearchPerformanceProfile.Probes.Where(probe => probe.Module == "prompts").Select(probe => probe.Text).ToArray(),
                samples);

            var ordered = samples.Order().ToArray();
            var median = ordered[ordered.Length / 2];
            var p95 = ordered[(int)Math.Ceiling(ordered.Length * 0.95) - 1];
            output.WriteLine(
                "Aplikacja=p4-browser-performance; profil={0}; przeglądarka={1} {2}; .NET={3}; SQLite={4}; "
                + "tryb=Release self-contained; CPU={5}; RAM={6}; dysk={7}; system plików={8}; "
                + "próby={9}; first={10:F1} ms; mediana={11:F1} ms; p95={12:F1} ms",
                profile.Name,
                browserName,
                browser.Version,
                Environment.Version,
                sqliteVersion,
                ReadLinuxValue("/proc/cpuinfo", "model name"),
                ReadLinuxValue("/proc/meminfo", "MemTotal"),
                new DriveInfo(Path.GetPathRoot(temporaryPath)!).DriveType,
                new DriveInfo(Path.GetPathRoot(temporaryPath)!).DriveFormat,
                samples.Count,
                firstQuery.Elapsed.TotalMilliseconds,
                median.TotalMilliseconds,
                p95.TotalMilliseconds);

            Assert.Equal(100, samples.Count);
            Assert.True(
                p95 <= maximumP95,
                $"Profil {profile.Name}, {browserName}: p95 {p95.TotalMilliseconds:F1} ms przekracza {maximumP95.TotalMilliseconds:F0} ms.");
        }
        finally
        {
            if (host is not null)
            {
                await BrowserHostFlowTests.StopProcessAsync(host);
                host.Dispose();
            }

            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    private static async Task MeasureModuleAsync(
        IPage page,
        ILocator search,
        string completedStatus,
        string[] probes,
        List<TimeSpan> samples)
    {
        for (var index = 0; index < 5; index++)
        {
            await SearchAsync(page, search, completedStatus, probes[index % probes.Length]);
        }

        for (var index = 0; index < 50; index++)
        {
            var stopwatch = Stopwatch.StartNew();
            await SearchAsync(page, search, completedStatus, probes[index % probes.Length]);
            stopwatch.Stop();
            samples.Add(stopwatch.Elapsed);
        }
    }

    private static async Task SearchAsync(IPage page, ILocator search, string completedStatus, string text)
    {
        var status = page.Locator(".module-page__status");
        for (var attempt = 0; attempt < 150; attempt++)
        {
            if (!string.Equals(await search.InputValueAsync(), text, StringComparison.Ordinal))
            {
                await search.FillAsync(text);
            }

            if (string.Equals(await status.GetAttributeAsync("data-query"), text, StringComparison.Ordinal)
                && (await status.TextContentAsync())?.Contains(completedStatus, StringComparison.Ordinal) == true)
            {
                return;
            }

            await page.WaitForTimeoutAsync(100);
        }

        throw new TimeoutException(
            $"Wyszukiwanie „{text}” nie zakończyło się. URL={page.Url}; "
            + $"pole={await search.InputValueAsync()}; data-query={await status.GetAttributeAsync("data-query")}; "
            + $"status={await status.TextContentAsync()}");
    }

    private static async Task WaitForCompletedSearchAsync(IPage page, string prefix) =>
        await page.Locator(".module-page__status").Filter(new() { HasText = prefix }).WaitForAsync();

    private static string ReadLinuxValue(string path, string key) =>
        File.ReadLines(path)
            .FirstOrDefault(line => line.StartsWith(key, StringComparison.OrdinalIgnoreCase))?
            .Split(':', 2)[1]
            .Trim()
        ?? "nieznane";

}

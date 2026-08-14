using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using Deque.AxeCore.Playwright;
using Microsoft.Playwright;
using Servanda.App.Security;
using Servanda.Infrastructure.Runtime;

namespace Servanda.E2E.Browser;

[SupportedOSPlatform("linux")]
public sealed class BrowserHostFlowTests
{
    [Theory]
    [Trait("Category", "Browser")]
    [InlineData("chromium")]
    [InlineData("firefox")]
    public async Task PublishedHostShowsRestrictedAccessibleRecovery(string browserName)
    {
        var artifactDirectory = Environment.GetEnvironmentVariable("SERVANDA_BROWSER_E2E_ARTIFACT");
        Assert.False(
            string.IsNullOrWhiteSpace(artifactDirectory),
            "Ustaw SERVANDA_BROWSER_E2E_ARTIFACT albo uruchom tests/Servanda.E2E/run-browser-tests.sh.");

        var executablePath = Path.Combine(Path.GetFullPath(artifactDirectory!), "Servanda");
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-recovery-browser-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryPath, PrivateDirectoryMode);
        var runtimeBase = Path.Combine(temporaryPath, "runtime");
        var stateBase = Path.Combine(temporaryPath, "state");
        var homeDirectory = Path.Combine(temporaryPath, "home");
        var shimDirectory = Path.Combine(temporaryPath, "bin");
        var openedAddressesPath = Path.Combine(temporaryPath, "opened-addresses.txt");
        Directory.CreateDirectory(homeDirectory, PrivateDirectoryMode);
        Directory.CreateDirectory(shimDirectory, PrivateDirectoryMode);
        await CreateXdgOpenShimAsync(shimDirectory);
        var applicationData = Path.Combine(homeDirectory, ".local", "share", "servanda");
        Directory.CreateDirectory(applicationData, PrivateDirectoryMode);
        var databasePath = Path.Combine(applicationData, "servanda.db");
        await File.WriteAllBytesAsync(databasePath, new byte[128]);
        File.SetUnixFileMode(databasePath, PrivateFileMode);
        var paths = new ServandaPaths(Path.Combine(runtimeBase, "servanda"), Path.Combine(stateBase, "servanda"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Process? host = null;

        try
        {
            var launcherResult = await RunLauncherAsync(
                executablePath,
                runtimeBase,
                stateBase,
                homeDirectory,
                shimDirectory,
                openedAddressesPath,
                timeout.Token);
            Assert.Equal(0, launcherResult);
            var descriptor = await WaitForAvailableDescriptorAsync(paths.DescriptorPath, timeout.Token);
            Assert.Equal("recovery", descriptor.State);
            host = Process.GetProcessById(descriptor.ProcessId);
            var bootstrapAddress = Assert.Single(
                await WaitForOpenedAddressesAsync(openedAddressesPath, 1, timeout.Token));

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await LaunchBrowserAsync(playwright, browserName);
            await using var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var retryScriptResponse = new TaskCompletionSource<IResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recoveryConsoleErrors = new ConcurrentBag<string>();
            page.Response += (_, response) =>
            {
                if (response.Url.Contains("recovery-retry", StringComparison.Ordinal))
                {
                    retryScriptResponse.TrySetResult(response);
                }
            };
            page.Console += (_, message) =>
            {
                if (message.Type == "error")
                {
                    recoveryConsoleErrors.Add(message.Text);
                }
            };
            await page.GotoAsync(bootstrapAddress, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
            });
            await page.WaitForURLAsync($"{descriptor.Origin}/recovery");
            await page.GetByRole(
                AriaRole.Heading,
                new() { Name = "Servanda nie może otworzyć magazynu danych", Level = 1 }).WaitForAsync();

            Assert.Equal("Odzyskiwanie — Servanda", await page.TitleAsync());
            Assert.Equal(1, await page.GetByRole(AriaRole.Main).CountAsync());
            Assert.Equal(0, await page.GetByRole(AriaRole.Complementary).CountAsync());
            Assert.Equal(0, await page.GetByText("Zarządzaj obszarami", new() { Exact = true }).CountAsync());
            foreach (var viewportWidth in new[] { 1024, 1280, 1440, 1920 })
            {
                await page.SetViewportSizeAsync(viewportWidth, 768);
                Assert.True(
                    await page.Locator("html").EvaluateAsync<bool>(
                        "element => element.scrollWidth <= element.clientWidth"),
                    $"Recovery przewija się poziomo przy szerokości {viewportWidth}px.");
            }

            var retry = page.GetByRole(
                AriaRole.Button,
                new() { Name = "Ponów przygotowanie magazynu", Exact = true });
            var scriptResponse = await retryScriptResponse.Task.WaitAsync(timeout.Token);
            Assert.True(
                scriptResponse.Status == 200,
                $"Moduł recovery zwrócił {scriptResponse.Status}: {scriptResponse.Url}.");
            Assert.Empty(recoveryConsoleErrors);
            await page.Locator("html[data-recovery-retry-ready='true']").WaitForAsync();
            await retry.FocusAsync();
            Assert.True(await retry.EvaluateAsync<bool>("element => element === document.activeElement"));
            await page.Keyboard.PressAsync("Enter");
            await page.GetByRole(AriaRole.Alert).WaitForAsync();
            Assert.Equal($"{descriptor.Origin}/recovery", page.Url);
            Assert.Empty(recoveryConsoleErrors);
            await AssertNoAxeViolationsAsync(page, "tryb odzyskiwania");
        }
        finally
        {
            if (host is not null)
            {
                await StopProcessAsync(host);
                host.Dispose();
            }

            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    [Theory]
    [Trait("Category", "Browser")]
    [InlineData("chromium")]
    [InlineData("firefox")]
    public async Task PublishedHostCompletesProtectedBrowserFlow(string browserName)
    {
        var artifactDirectory = Environment.GetEnvironmentVariable("SERVANDA_BROWSER_E2E_ARTIFACT");
        Assert.False(
            string.IsNullOrWhiteSpace(artifactDirectory),
            "Ustaw SERVANDA_BROWSER_E2E_ARTIFACT albo uruchom tests/Servanda.E2E/run-browser-tests.sh.");

        artifactDirectory = Path.GetFullPath(artifactDirectory!);
        var executablePath = Path.Combine(artifactDirectory, "Servanda");
        Assert.True(File.Exists(executablePath), $"Brak opublikowanego pliku wykonywalnego: {executablePath}");

        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-browser-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            temporaryPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var runtimeBase = Path.Combine(temporaryPath, "runtime");
        var stateBase = Path.Combine(temporaryPath, "state");
        var dataBase = Path.Combine(temporaryPath, "data");
        var homeDirectory = Path.Combine(temporaryPath, "home");
        var shimDirectory = Path.Combine(temporaryPath, "bin");
        var openedAddressesPath = Path.Combine(temporaryPath, "opened-addresses.txt");
        Directory.CreateDirectory(homeDirectory);
        Directory.CreateDirectory(shimDirectory);
        await CreateXdgOpenShimAsync(shimDirectory);
        var paths = new ServandaPaths(Path.Combine(runtimeBase, "servanda"), Path.Combine(stateBase, "servanda"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        Process? host = null;

        try
        {
            var installResult = await RunDesktopInstallerAsync(
                artifactDirectory,
                dataBase,
                homeDirectory,
                timeout.Token);
            Assert.Equal(0, installResult);
            var installedExecutablePath = await ReadDesktopExecutableAsync(dataBase, timeout.Token);
            Assert.Equal(executablePath, installedExecutablePath);

            var firstLauncherResult = await RunLauncherAsync(
                installedExecutablePath,
                runtimeBase,
                stateBase,
                homeDirectory,
                shimDirectory,
                openedAddressesPath,
                timeout.Token);
            Assert.Equal(0, firstLauncherResult);
            var descriptor = await WaitForReadyDescriptorAsync(paths.DescriptorPath, timeout.Token);
            host = Process.GetProcessById(descriptor.ProcessId);
            var firstOpenedAddresses = await WaitForOpenedAddressesAsync(openedAddressesPath, 1, timeout.Token);
            Assert.StartsWith(
                $"{descriptor.Origin}/bootstrap#ticket=",
                firstOpenedAddresses[0],
                StringComparison.Ordinal);

            var secondLauncherResult = await RunLauncherAsync(
                installedExecutablePath,
                runtimeBase,
                stateBase,
                homeDirectory,
                shimDirectory,
                openedAddressesPath,
                timeout.Token);
            Assert.Equal(0, secondLauncherResult);
            var descriptorAfterSecondLaunch = await WaitForReadyDescriptorAsync(paths.DescriptorPath, timeout.Token);
            var openedAddresses = await WaitForOpenedAddressesAsync(openedAddressesPath, 2, timeout.Token);

            Assert.Equal(descriptor.InstanceId, descriptorAfterSecondLaunch.InstanceId);
            Assert.Equal(descriptor.ProcessId, descriptorAfterSecondLaunch.ProcessId);
            Assert.False(host.HasExited);
            var bootstrapAddress = openedAddresses[1];
            Assert.StartsWith($"{descriptor.Origin}/bootstrap#ticket=", bootstrapAddress, StringComparison.Ordinal);
            Assert.NotEqual(openedAddresses[0], bootstrapAddress);

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await LaunchBrowserAsync(playwright, browserName);
            await using var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            var requestedAddresses = new ConcurrentBag<string>();
            var failedResponses = new ConcurrentBag<string>();
            var browserErrors = new ConcurrentBag<string>();
            var webSocketConnected = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var circuitMessageReceived = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Request += (_, request) => requestedAddresses.Add(request.Url);
            page.Response += (_, response) =>
            {
                if (response.Status >= 400)
                {
                    failedResponses.Add($"{response.Status} {response.Url}");
                }
            };
            page.Console += (_, message) =>
            {
                if (message.Type == "error")
                {
                    browserErrors.Add(message.Text);
                }
            };
            page.WebSocket += (_, socket) =>
            {
                webSocketConnected.TrySetResult(socket.Url);
                socket.FrameReceived += (_, _) => circuitMessageReceived.TrySetResult();
            };

            var interactionStopwatch = Stopwatch.StartNew();
            var applicationResponse = await page.GotoAsync(bootstrapAddress, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
            });
            await page.WaitForURLAsync($"{descriptor.Origin}/");
            await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).WaitForAsync();
            interactionStopwatch.Stop();

            Assert.NotNull(applicationResponse);
            Assert.Equal(200, applicationResponse.Status);
            Assert.True(
                interactionStopwatch.Elapsed <= TimeSpan.FromSeconds(2),
                $"Pulpit był gotowy do interakcji po {interactionStopwatch.Elapsed.TotalMilliseconds:F0} ms.");
            var contentSecurityPolicy = await applicationResponse.HeaderValueAsync("content-security-policy");
            Assert.NotNull(contentSecurityPolicy);
            Assert.Contains("frame-ancestors 'none'", contentSecurityPolicy, StringComparison.Ordinal);
            Assert.Contains("object-src 'none'", contentSecurityPolicy, StringComparison.Ordinal);
            Assert.DoesNotContain("unsafe-eval", contentSecurityPolicy, StringComparison.Ordinal);
            Assert.Equal("Pulpit — Servanda", await page.TitleAsync());
            Assert.Equal("Twoje obszary", await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).TextContentAsync());
            Assert.Equal("pl", await page.Locator("html").GetAttributeAsync("lang"));
            Assert.Equal(1, await page.GetByRole(AriaRole.Main).CountAsync());
            Assert.Equal(
                1,
                await page.GetByRole(AriaRole.Complementary, new() { Name = "Panel boczny" }).CountAsync());
            Assert.Equal(
                1,
                await page.GetByRole(AriaRole.Navigation, new() { Name = "Główna nawigacja" }).CountAsync());
            Assert.Equal(
                "page",
                await page.GetByRole(AriaRole.Link, new() { Name = "Pulpit", Exact = true })
                    .GetAttributeAsync("aria-current"));
            Assert.Equal(
                7,
                await page.Locator("aside.sidebar .sidebar-content__status")
                    .GetByText("Planowane", new() { Exact = true })
                    .CountAsync());
            Assert.Equal(7, await page.Locator(".area-tile").CountAsync());
            Assert.Equal(7, await page.Locator(".area-tile__status").GetByText("Planowane", new() { Exact = true }).CountAsync());
            Assert.Equal(7, await page.Locator(".area-tile svg[viewBox='0 0 24 24']").CountAsync());
            Assert.Equal(1, await page.Locator("aside.sidebar").GetByText("Zarządzaj obszarami", new() { Exact = true }).CountAsync());
            Assert.Equal(0, await page.Locator("input:visible, textarea:visible, select:visible").CountAsync());
            foreach (var unavailableAction in new[] { "Zapisz", "Szukaj", "Importuj", "Eksportuj" })
            {
                Assert.Equal(0, await page.GetByText(unavailableAction, new() { Exact = true }).CountAsync());
            }

            foreach (var area in new[]
            {
                (Name: "Skarbiec promptów", Description: "Przechowywanie, przygotowywanie i ponowne używanie promptów."),
                (Name: "Przechowalnia narzędzi", Description: "Katalog sprawdzonych stron i aplikacji przydatnych na co dzień."),
                (Name: "Dom", Description: "Harmonogram prac porządkowych i innych obowiązków domowych."),
                (Name: "Rodzina", Description: "Ważne informacje, potrzeby, daty i relacje dotyczące bliskich."),
                (Name: "Witalność", Description: "Zdrowie, biohacking, dieta i trening w jednym uporządkowanym miejscu."),
                (Name: "Przechowalnia notatek", Description: "Pomysły, obserwacje i informacje zachowane do późniejszego użycia."),
                (Name: "Budżet domowy", Description: "Planowanie miesięcznego budżetu gospodarstwa domowego."),
            })
            {
                var sidebarArea = page.Locator("aside.sidebar .sidebar-content__area-name")
                    .GetByText(area.Name, new() { Exact = true });
                Assert.Equal(1, await sidebarArea.CountAsync());
                Assert.False(
                    await sidebarArea.EvaluateAsync<bool>(
                        "element => element.closest('a, button') !== null"),
                    $"Planowany obszar „{area.Name}” nie może być interaktywny w panelu v1.");

                var tile = page.GetByRole(AriaRole.Heading, new() { Name = area.Name, Exact = true, Level = 3 })
                    .Locator("xpath=ancestor::article");
                Assert.Equal(1, await tile.CountAsync());
                Assert.Equal(area.Name, await tile.GetByRole(AriaRole.Heading, new() { Level = 3 }).TextContentAsync());
                Assert.Equal(area.Description, await tile.Locator("p").TextContentAsync());
                Assert.Equal(0, await tile.Locator("a, button, input, select, textarea").CountAsync());
            }

            Assert.Equal(
                "rgb(11, 13, 17)",
                await page.Locator("body").EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor"));
            Assert.Equal(
                "rgb(243, 245, 247)",
                await page.Locator("body").EvaluateAsync<string>("element => getComputedStyle(element).color"));
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.Reduce });
            Assert.Equal(
                "none",
                await page.Locator(".components-rejoining-animation div").First.EvaluateAsync<string>(
                    "element => getComputedStyle(element).animationName"));
            await page.EmulateMediaAsync(new() { ReducedMotion = ReducedMotion.NoPreference });
            await AssertNoAxeViolationsAsync(page, "pulpit desktopowy");

            foreach (var control in await page.Locator("a:visible, button:visible").AllAsync())
            {
                var box = await control.BoundingBoxAsync();
                Assert.NotNull(box);
                Assert.True(
                    box.Width >= 32 && box.Height >= 32,
                    $"Widoczna kontrolka ma rozmiar {box.Width}×{box.Height}px zamiast co najmniej 32×32px.");
            }

            foreach (var viewportWidth in new[] { 1024, 1280, 1440, 1920 })
            {
                await page.SetViewportSizeAsync(viewportWidth, 768);
                await page.WaitForFunctionAsync(
                    "document.querySelector('.sidebar')?.getBoundingClientRect().width > 0");
                Assert.True(
                    await page.Locator("html").EvaluateAsync<bool>(
                        "element => element.scrollWidth <= element.clientWidth"),
                    $"Strona przewija się poziomo przy szerokości {viewportWidth}px.");
                Assert.Equal(
                    292,
                    await page.Locator(".sidebar").EvaluateAsync<int>(
                        "element => Math.round(element.getBoundingClientRect().width)"));
                Assert.True(
                    await page.Locator(".area-grid").EvaluateAsync<bool>(
                        "element => element.getBoundingClientRect().width <= element.parentElement.getBoundingClientRect().width"),
                    $"Siatka kafli wychodzi poza dostępną treść przy szerokości {viewportWidth}px.");
                Assert.True(
                    await page.Locator(".dashboard").EvaluateAsync<bool>(
                        """
                        element => {
                            const dashboard = element.getBoundingClientRect();
                            const main = element.closest('main').getBoundingClientRect();
                            return Math.abs((dashboard.left - main.left) - (main.right - dashboard.right)) <= 1;
                        }
                        """),
                    $"Pulpit nie jest wyśrodkowany przy szerokości {viewportWidth}px.");
            }

            await page.SetViewportSizeAsync(1024, 768);
            await page.EvaluateAsync(
                """
                () => {
                    const sheet = [...document.styleSheets].find(candidate => candidate.href?.includes('/app.'));
                    if (!sheet) {
                        throw new Error('Nie znaleziono lokalnego arkusza app.css.');
                    }

                    window.servandaSpacingTest = { sheet, initialRuleCount: sheet.cssRules.length };
                    sheet.insertRule('* { line-height: 1.5 !important; letter-spacing: 0.12em !important; word-spacing: 0.16em !important; }', sheet.cssRules.length);
                    sheet.insertRule('p { margin-block-end: 2em !important; }', sheet.cssRules.length);
                }
                """);
            Assert.True(
                await page.Locator("html").EvaluateAsync<bool>(
                    "element => element.scrollWidth <= element.clientWidth"),
                "Nadpisane odstępy tekstu powodują poziome przewijanie strony.");
            Assert.All(
                await page.Locator(".area-tile").AllTextContentsAsync(),
                content => Assert.False(string.IsNullOrWhiteSpace(content)));
            await page.EvaluateAsync(
                """
                () => {
                    const { sheet, initialRuleCount } = window.servandaSpacingTest;
                    while (sheet.cssRules.length > initialRuleCount) {
                        sheet.deleteRule(initialRuleCount);
                    }
                    delete window.servandaSpacingTest;
                }
                """);

            const string longLabel =
                "BardzoDługiNieprzerwanyCiągSprawdzającyZawijanieTreściBezUcinaniaLubPoziomegoPrzewijaniaCałejStronyServandy";
            var stressedHeading = page.Locator(".area-tile h3").First;
            var originalHeading = await stressedHeading.TextContentAsync();
            await stressedHeading.EvaluateAsync("(element, value) => element.textContent = value", longLabel);
            Assert.True(
                await stressedHeading.EvaluateAsync<bool>("element => element.scrollWidth <= element.clientWidth"));
            Assert.True(
                await page.Locator("html").EvaluateAsync<bool>(
                    "element => element.scrollWidth <= element.clientWidth"));
            await stressedHeading.EvaluateAsync("(element, value) => element.textContent = value", originalHeading);

            await page.Locator("aside.sidebar .sidebar-content__brand").FocusAsync();
            await page.Keyboard.PressAsync("Shift+Tab");
            var activeElement = await page.EvaluateAsync<string>(
                "() => `${document.activeElement?.tagName}.${document.activeElement?.className}`");
            Assert.True(
                await page.Locator(".skip-link").EvaluateAsync<bool>(
                    "element => element === document.activeElement"),
                $"Fokus klawiatury trafił na {activeElement} zamiast linku pomijającego.");

            foreach (var reflowViewport in new[]
            {
                (Width: 512, Description: "200% przy widoku 1024px"),
                (Width: 320, Description: "400% przy widoku 1280px"),
            })
            {
                await page.SetViewportSizeAsync(reflowViewport.Width, 768);
                var drawerTrigger = page.GetByRole(
                    AriaRole.Button,
                    new() { Name = "Otwórz panel boczny", Exact = true });
                await drawerTrigger.WaitForAsync(new() { State = WaitForSelectorState.Visible });
                Assert.False(await page.Locator("aside.sidebar").IsVisibleAsync());
                Assert.True(
                    await page.Locator("html").EvaluateAsync<bool>(
                        "element => element.scrollWidth <= element.clientWidth"),
                    $"Strona przewija się poziomo dla {reflowViewport.Description}.");
                foreach (var tile in await page.Locator(".area-tile").AllAsync())
                {
                    Assert.True(await tile.IsVisibleAsync());
                }
            }

            var openDrawer = page.GetByRole(
                AriaRole.Button,
                new() { Name = "Otwórz panel boczny", Exact = true });
            await openDrawer.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            var drawer = page.GetByRole(AriaRole.Dialog, new() { Name = "Panel boczny" });
            await drawer.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            var closeDrawer = drawer.GetByRole(
                AriaRole.Button,
                new() { Name = "Zamknij panel boczny", Exact = true });
            Assert.True(await closeDrawer.EvaluateAsync<bool>("element => element === document.activeElement"));
            await AssertNoAxeViolationsAsync(page, "modalna szuflada panelu");
            await page.Keyboard.PressAsync("Shift+Tab");
            Assert.True(
                await drawer.EvaluateAsync<bool>("element => element.contains(document.activeElement)"),
                "Fokus opuścił modalną szufladę.");
            await page.Keyboard.PressAsync("Tab");
            Assert.True(await closeDrawer.EvaluateAsync<bool>("element => element === document.activeElement"));
            await page.Keyboard.PressAsync("Escape");
            await drawer.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            Assert.True(await openDrawer.EvaluateAsync<bool>("element => element === document.activeElement"));

            await page.SetViewportSizeAsync(320, 768);
            await openDrawer.ClickAsync();
            await drawer.WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await page.SetViewportSizeAsync(1024, 768);
            await page.Locator("aside.sidebar").WaitForAsync(new() { State = WaitForSelectorState.Visible });
            await drawer.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
            await page.Locator("dialog.navigation-drawer__dialog:not([open])").WaitForAsync(
                new() { State = WaitForSelectorState.Attached });
            Assert.Null(await page.Locator("dialog.navigation-drawer__dialog").GetAttributeAsync("open"));
            var desktopBrand = page.Locator("aside.sidebar .sidebar-content__brand");
            await desktopBrand.FocusAsync();
            Assert.True(await desktopBrand.EvaluateAsync<bool>("element => element === document.activeElement"));
            Assert.DoesNotContain("ticket=", page.Url, StringComparison.Ordinal);

            await page.GetByRole(AriaRole.Link, new() { Name = "Zarządzaj obszarami", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Zarządzaj obszarami", Level = 1 }).WaitForAsync();
            await page.ReloadAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Zarządzaj obszarami", Level = 1 }).WaitForAsync();
            foreach (var viewportWidth in new[] { 1024, 1280, 1440, 1920 })
            {
                await page.SetViewportSizeAsync(viewportWidth, 768);
                Assert.True(
                    await page.Locator("html").EvaluateAsync<bool>(
                        "element => element.scrollWidth <= element.clientWidth"),
                    $"Zarządzanie obszarami przewija się poziomo przy szerokości {viewportWidth}px.");
            }

            await page.SetViewportSizeAsync(320, 768);
            var addAreaButton = page.GetByRole(AriaRole.Button, new() { Name = "Dodaj obszar", Exact = true });
            await addAreaButton.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            var creator = page.GetByRole(AriaRole.Heading, new() { Name = "Nowy obszar", Exact = true, Level = 2 })
                .Locator("xpath=ancestor::section");
            Assert.True(
                await page.Locator("html").EvaluateAsync<bool>(
                    "element => element.scrollWidth <= element.clientWidth"),
                "Formularz tworzenia przewija się poziomo przy reflow 400%. ");
            await page.SetViewportSizeAsync(1024, 768);
            await creator.GetByRole(AriaRole.Textbox, new() { Name = "Nazwa", Exact = true }).FillAsync("Projekty");
            await creator.GetByRole(AriaRole.Textbox, new() { Name = "Opis", Exact = true }).FillAsync("Własne projekty i kolejne kroki.");
            await creator.GetByLabel("Ikona").SelectOptionAsync("notes");
            await creator.GetByLabel("Akcent").SelectOptionAsync("accent-2");
            await creator.GetByRole(AriaRole.Button, new() { Name = "Dodaj obszar", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Projekty", Exact = true, Level = 2 }).WaitForAsync();
            Assert.Equal(
                1,
                await page.Locator("aside.sidebar .sidebar-content__area-name")
                    .GetByText("Projekty", new() { Exact = true })
                    .CountAsync());
            await page.GetByRole(
                AriaRole.Button,
                new() { Name = "Przenieś wyżej: Projekty", Exact = true }).ClickAsync();
            await page.GetByText("Zmieniono kolejność obszaru „Projekty”.", new() { Exact = true }).WaitForAsync();
            var managedAreaNames = await page.Locator(".area-editor h2").AllTextContentsAsync();
            Assert.True(
                managedAreaNames.ToList().IndexOf("Projekty") < managedAreaNames.ToList().IndexOf("Budżet domowy"),
                "Obszar nie został przeniesiony wyżej na liście zarządzania.");
            var sidebarAreaNames = await page.Locator("aside.sidebar .sidebar-content__area-name").AllTextContentsAsync();
            Assert.True(
                sidebarAreaNames.ToList().IndexOf("Projekty") < sidebarAreaNames.ToList().IndexOf("Budżet domowy"),
                "Panel boczny nie odświeżył zmienionej kolejności obszarów.");
            var homeEditor = page.GetByRole(AriaRole.Heading, new() { Name = "Dom", Exact = true, Level = 2 })
                .Locator("xpath=ancestor::article");
            await homeEditor.GetByRole(AriaRole.Button, new() { Name = "Edytuj obszar" }).ClickAsync();
            await homeEditor.GetByRole(AriaRole.Textbox, new() { Name = "Nazwa", Exact = true }).FillAsync("Mój dom");
            await homeEditor.GetByRole(AriaRole.Textbox, new() { Name = "Opis", Exact = true }).FillAsync("Własny opis zapisany w lokalnej bazie.");
            await homeEditor.GetByRole(AriaRole.Button, new() { Name = "Zapisz zmiany" }).ClickAsync();
            await page.WaitForTimeoutAsync(1_000);
            Assert.True(
                await page.GetByRole(AriaRole.Heading, new() { Name = "Mój dom", Exact = true, Level = 2 }).CountAsync() == 1,
                $"Edycja obszaru nie została zapisana. URL={page.Url}; " +
                $"Komunikaty={string.Join(" | ", await page.Locator(".area-management__message").AllTextContentsAsync())}; " +
                $"Konsola={string.Join(" | ", browserErrors)}");
            Assert.Equal(
                1,
                await page.Locator("aside.sidebar .sidebar-content__area-name")
                    .GetByText("Mój dom", new() { Exact = true })
                    .CountAsync());
            var projectEditor = page.GetByRole(AriaRole.Heading, new() { Name = "Projekty", Exact = true, Level = 2 })
                .Locator("xpath=ancestor::article");
            await projectEditor.GetByRole(
                AriaRole.Button,
                new() { Name = "Ukryj obszar: Projekty", Exact = true }).ClickAsync();
            await page.GetByText(
                "Ukryto obszar „Projekty” na pulpicie i w panelu bocznym.",
                new() { Exact = true }).WaitForAsync();
            Assert.Equal(1, await projectEditor.GetByText("Ukryty · Planowane", new() { Exact = true }).CountAsync());
            Assert.Equal(
                0,
                await page.Locator("aside.sidebar .sidebar-content__area-name")
                    .GetByText("Projekty", new() { Exact = true })
                    .CountAsync());
            await page.GetByRole(AriaRole.Link, new() { Name = "Pulpit", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Twoje obszary", Exact = true, Level = 1 })
                .WaitForAsync();
            Assert.Equal(
                0,
                await page.GetByRole(AriaRole.Heading, new() { Name = "Projekty", Exact = true, Level = 3 }).CountAsync());
            Assert.Equal(7, await page.Locator(".area-tile").CountAsync());
            await page.GetByRole(AriaRole.Link, new() { Name = "Zarządzaj obszarami", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Zarządzaj obszarami", Exact = true, Level = 1 })
                .WaitForAsync();
            projectEditor = page.GetByRole(AriaRole.Heading, new() { Name = "Projekty", Exact = true, Level = 2 })
                .Locator("xpath=ancestor::article");
            await projectEditor.GetByRole(
                AriaRole.Button,
                new() { Name = "Pokaż obszar: Projekty", Exact = true }).ClickAsync();
            await page.GetByText("Obszar „Projekty” jest ponownie widoczny.", new() { Exact = true }).WaitForAsync();
            Assert.Equal(
                1,
                await page.Locator("aside.sidebar .sidebar-content__area-name")
                    .GetByText("Projekty", new() { Exact = true })
                    .CountAsync());
            var archiveButton = projectEditor.GetByRole(
                AriaRole.Button,
                new() { Name = "Archiwizuj obszar: Projekty", Exact = true });
            await archiveButton.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            var archiveConfirmation = projectEditor.GetByRole(AriaRole.Alert);
            await archiveConfirmation.WaitForAsync();
            Assert.Contains(
                "dane zostaną zachowane",
                await archiveConfirmation.TextContentAsync(),
                StringComparison.Ordinal);
            var confirmArchiveButton = archiveConfirmation.GetByRole(
                AriaRole.Button,
                new() { Name = "Potwierdź archiwizację: Projekty", Exact = true });
            await confirmArchiveButton.FocusAsync();
            await page.Keyboard.PressAsync("Enter");
            await page.GetByText(
                "Zarchiwizowano obszar „Projekty”. Dane zostały zachowane.",
                new() { Exact = true }).WaitForAsync();
            var archivedProject = page.GetByRole(
                    AriaRole.Heading,
                    new() { Name = "Projekty", Exact = true, Level = 3 })
                .Locator("xpath=ancestor::article");
            await archivedProject.WaitForAsync();
            foreach (var viewportWidth in new[] { 1024, 1280, 1440, 1920 })
            {
                await page.SetViewportSizeAsync(viewportWidth, 768);
                Assert.True(
                    await page.Locator("html").EvaluateAsync<bool>(
                        "element => element.scrollWidth <= element.clientWidth"),
                    $"Archiwum przewija się poziomo przy szerokości {viewportWidth}px.");
            }

            await page.SetViewportSizeAsync(1024, 768);
            Assert.Equal(0, await page.GetByRole(
                AriaRole.Heading,
                new() { Name = "Projekty", Exact = true, Level = 2 }).CountAsync());
            Assert.Equal(
                0,
                await page.Locator("aside.sidebar .sidebar-content__area-name")
                    .GetByText("Projekty", new() { Exact = true })
                    .CountAsync());
            await page.GetByRole(AriaRole.Link, new() { Name = "Pulpit", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Twoje obszary", Exact = true, Level = 1 })
                .WaitForAsync();
            Assert.Equal(
                0,
                await page.GetByRole(AriaRole.Heading, new() { Name = "Projekty", Exact = true, Level = 3 }).CountAsync());
            Assert.Equal(7, await page.Locator(".area-tile").CountAsync());
            await page.GetByRole(AriaRole.Link, new() { Name = "Zarządzaj obszarami", Exact = true }).ClickAsync();
            await page.GetByRole(AriaRole.Heading, new() { Name = "Archiwum", Exact = true, Level = 2 })
                .WaitForAsync();
            archivedProject = page.GetByRole(
                    AriaRole.Heading,
                    new() { Name = "Projekty", Exact = true, Level = 3 })
                .Locator("xpath=ancestor::article");
            await archivedProject.GetByRole(
                AriaRole.Button,
                new() { Name = "Przywróć obszar: Projekty", Exact = true }).ClickAsync();
            await page.GetByText(
                "Przywrócono obszar „Projekty” w poprzednim miejscu.",
                new() { Exact = true }).WaitForAsync();
            projectEditor = page.GetByRole(
                    AriaRole.Heading,
                    new() { Name = "Projekty", Exact = true, Level = 2 })
                .Locator("xpath=ancestor::article");
            await projectEditor.WaitForAsync();
            Assert.Equal(
                1,
                await page.Locator("aside.sidebar .sidebar-content__area-name")
                    .GetByText("Projekty", new() { Exact = true })
                    .CountAsync());
            await AssertNoAxeViolationsAsync(page, "zarządzanie obszarami");
            foreach (var control in await page.Locator("main a:visible, main button:visible, main input:visible, main textarea:visible, main select:visible").AllAsync())
            {
                var box = await control.BoundingBoxAsync();
                Assert.NotNull(box);
                Assert.True(box.Width >= 32 && box.Height >= 32);
            }
            await page.GetByRole(AriaRole.Link, new() { Name = "Pulpit", Exact = true }).ClickAsync();
            var savedTile = page.GetByRole(AriaRole.Heading, new() { Name = "Mój dom", Exact = true, Level = 3 })
                .Locator("xpath=ancestor::article");
            Assert.Equal("Własny opis zapisany w lokalnej bazie.", await savedTile.Locator("p").TextContentAsync());
            var projectTile = page.GetByRole(AriaRole.Heading, new() { Name = "Projekty", Exact = true, Level = 3 })
                .Locator("xpath=ancestor::article");
            Assert.Equal("Własne projekty i kolejne kroki.", await projectTile.Locator("p").TextContentAsync());
            Assert.Equal(8, await page.Locator(".area-tile").CountAsync());
            var dashboardAreaNames = await page.Locator(".area-tile h3").AllTextContentsAsync();
            Assert.True(
                dashboardAreaNames.ToList().IndexOf("Projekty") < dashboardAreaNames.ToList().IndexOf("Budżet domowy"),
                "Pulpit nie zachował zmienionej kolejności obszarów.");
            await page.ReloadAsync();
            Assert.Equal(
                1,
                await page.GetByRole(AriaRole.Heading, new() { Name = "Mój dom", Exact = true, Level = 3 }).CountAsync());
            Assert.Equal(
                1,
                await page.GetByRole(AriaRole.Heading, new() { Name = "Projekty", Exact = true, Level = 3 }).CountAsync());
            await AssertNoAxeViolationsAsync(page, "edycja obszaru");

            var sessionCookie = Assert.Single(
                await context.CookiesAsync(),
                cookie => cookie.Name == ProcessSessionStore.CookieName);
            Assert.True(sessionCookie.HttpOnly);
            Assert.Equal(SameSiteAttribute.Strict, sessionCookie.SameSite);
            Assert.Equal("/", sessionCookie.Path);

            var webSocketTask = webSocketConnected.Task;
            var connectionDeadline = Task.Delay(TimeSpan.FromSeconds(10), timeout.Token);
            if (await Task.WhenAny(webSocketTask, connectionDeadline) != webSocketTask)
            {
                Assert.Fail(
                    $"Blazor nie otworzył WebSocket.{Environment.NewLine}" +
                    $"Odpowiedzi błędne: {string.Join(", ", failedResponses)}{Environment.NewLine}" +
                    $"Konsola: {string.Join(" | ", browserErrors)}");
            }

            var webSocketAddress = await webSocketTask;
            Assert.StartsWith("ws://127.0.0.1:", webSocketAddress, StringComparison.Ordinal);
            await circuitMessageReceived.Task.WaitAsync(timeout.Token);
            await page.WaitForTimeoutAsync(500);

            var shutdownToggle = page.GetByRole(AriaRole.Button, new() { Name = "Zamknij Servandę" });
            await shutdownToggle.ClickAsync();
            var shutdownConfirmation = page.GetByRole(AriaRole.Button, new() { Name = "Tak, zamknij Servandę" });
            await shutdownConfirmation.WaitForAsync(new LocatorWaitForOptions { State = WaitForSelectorState.Visible });

            Assert.Empty(failedResponses);
            Assert.Empty(browserErrors);
            Assert.All(requestedAddresses, address => AssertAllowedAddress(address, descriptor.Origin));

            var shutdownResponseTask = page.WaitForResponseAsync(response =>
                response.Url == $"{descriptor.Origin}/shutdown"
                && response.Request.Method == "POST");
            await shutdownConfirmation.ClickAsync();
            var shutdownResponse = await shutdownResponseTask;
            var shutdownRequestHeaders = await shutdownResponse.Request.AllHeadersAsync();
            Assert.True(
                shutdownResponse.Status == 200,
                $"Shutdown zwrócił {shutdownResponse.Status}. " +
                $"Origin={GetHeader(shutdownRequestHeaders, "origin")}, " +
                $"Sec-Fetch-Site={GetHeader(shutdownRequestHeaders, "sec-fetch-site")}, " +
                $"Sec-Fetch-Mode={GetHeader(shutdownRequestHeaders, "sec-fetch-mode")}, " +
                $"Content-Type={GetHeader(shutdownRequestHeaders, "content-type")}.");
            Assert.Equal("text/html; charset=utf-8", shutdownResponse.Headers["content-type"]);
            var closedHeading = page.GetByRole(AriaRole.Heading, new()
            {
                Name = "Servanda została zamknięta",
                Exact = true,
                Level = 1,
            });
            await closedHeading.WaitForAsync();
            Assert.Equal(
                "Servanda została zamknięta",
                await closedHeading.TextContentAsync());
            Assert.StartsWith("blob:", page.Url, StringComparison.Ordinal);
            await host.WaitForExitAsync(timeout.Token);
            Assert.True(host.HasExited);
            Assert.False(File.Exists(paths.DescriptorPath));
            Assert.False(File.Exists(paths.ControlSecretPath));
        }
        finally
        {
            if (host is not null)
            {
                await StopProcessAsync(host);
                host.Dispose();
            }

            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright, string browserName) =>
        browserName switch
        {
            "chromium" => await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }),
            "firefox" => await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                FirefoxUserPrefs = new Dictionary<string, object>
                {
                    ["accessibility.tabfocus"] = 7,
                },
            }),
            _ => throw new ArgumentOutOfRangeException(nameof(browserName), browserName, "Nieobsługiwana przeglądarka testowa."),
        };

    private static async Task AssertNoAxeViolationsAsync(IPage page, string context)
    {
        var result = await page.RunAxe();
        Assert.True(
            result.Violations is null || result.Violations.Length == 0,
            result.Violations is null
                ? string.Empty
                : $"Axe wykrył naruszenia dla: {context}.{Environment.NewLine}" +
                  string.Join(
                      Environment.NewLine,
                      result.Violations.Select(violation =>
                          $"{violation.Id}: {violation.Help} ({string.Join(", ", violation.Nodes.Select(node => node.Html))})")));
    }

    private static async Task CreateXdgOpenShimAsync(string shimDirectory)
    {
        var shimPath = Path.Combine(shimDirectory, "xdg-open");
        await File.WriteAllTextAsync(
            shimPath,
            "#!/bin/sh\nset -eu\nprintf '%s\\n' \"$1\" >> \"$SERVANDA_XDG_OPEN_CAPTURE\"\n");
        File.SetUnixFileMode(
            shimPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    private static async Task<int> RunDesktopInstallerAsync(
        string artifactDirectory,
        string dataHome,
        string homeDirectory,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = "/bin/sh",
            UseShellExecute = false,
            ArgumentList = { Path.Combine(artifactDirectory, "install-desktop.sh") },
        };
        startInfo.Environment["HOME"] = homeDirectory;
        startInfo.Environment["XDG_DATA_HOME"] = dataHome;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Nie udało się uruchomić instalatora wpisu desktop.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    private static async Task<string> ReadDesktopExecutableAsync(
        string dataHome,
        CancellationToken cancellationToken)
    {
        var desktopPath = Path.Combine(dataHome, "applications", "servanda.desktop");
        var lines = await File.ReadAllLinesAsync(desktopPath, cancellationToken);
        var execLine = Assert.Single(lines, line => line.StartsWith("Exec=", StringComparison.Ordinal));
        Assert.StartsWith("Exec=\"", execLine, StringComparison.Ordinal);
        Assert.EndsWith("\"", execLine, StringComparison.Ordinal);
        return execLine[6..^1];
    }

    private static async Task<int> RunLauncherAsync(
        string executablePath,
        string runtimeBase,
        string stateBase,
        string homeDirectory,
        string shimDirectory,
        string openedAddressesPath,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
        };
        startInfo.Environment["HOME"] = homeDirectory;
        startInfo.Environment["PATH"] = $"{shimDirectory}:{Environment.GetEnvironmentVariable("PATH")}";
        startInfo.Environment["XDG_RUNTIME_DIR"] = runtimeBase;
        startInfo.Environment["XDG_STATE_HOME"] = stateBase;
        startInfo.Environment["XDG_DATA_HOME"] = Path.Combine(homeDirectory, ".local", "share");
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        startInfo.Environment["SERVANDA_XDG_OPEN_CAPTURE"] = openedAddressesPath;

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Nie udało się uruchomić launchera z artefaktu.");
        await process.WaitForExitAsync(cancellationToken);
        return process.ExitCode;
    }

    private static async Task<string[]> WaitForOpenedAddressesAsync(
        string path,
        int expectedCount,
        CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path))
            {
                var addresses = await File.ReadAllLinesAsync(path, cancellationToken);
                if (addresses.Length >= expectedCount)
                {
                    return addresses;
                }
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    private static async Task<InstanceDescriptor> WaitForReadyDescriptorAsync(
        string descriptorPath,
        CancellationToken cancellationToken)
    {
        var reader = new InstanceDescriptorReader(descriptorPath);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = await reader.TryReadReadyAsync(cancellationToken);
            if (descriptor is not null)
            {
                return descriptor;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    private static async Task<InstanceDescriptor> WaitForAvailableDescriptorAsync(
        string descriptorPath,
        CancellationToken cancellationToken)
    {
        var reader = new InstanceDescriptorReader(descriptorPath);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = await reader.TryReadAvailableAsync(cancellationToken);
            if (descriptor is not null)
            {
                return descriptor;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    private static void AssertAllowedAddress(string address, string origin)
    {
        var uri = new Uri(address, UriKind.Absolute);
        if (uri.Scheme is "data" or "blob")
        {
            return;
        }

        Assert.Equal(new Uri(origin).Authority, uri.Authority);
        Assert.Contains(uri.Scheme, new[] { Uri.UriSchemeHttp, "ws" });
    }

    private static string GetHeader(Dictionary<string, string> headers, string name) =>
        headers.TryGetValue(name, out var value) ? value : "<brak>";

    private static async Task StopProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill();
            await process.WaitForExitAsync();
        }
    }

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

}

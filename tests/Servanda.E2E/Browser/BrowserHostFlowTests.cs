using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
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

            var applicationResponse = await page.GotoAsync(bootstrapAddress, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.DOMContentLoaded,
            });
            await page.WaitForURLAsync($"{descriptor.Origin}/");

            Assert.NotNull(applicationResponse);
            Assert.Equal(200, applicationResponse.Status);
            var contentSecurityPolicy = await applicationResponse.HeaderValueAsync("content-security-policy");
            Assert.NotNull(contentSecurityPolicy);
            Assert.Contains("frame-ancestors 'none'", contentSecurityPolicy, StringComparison.Ordinal);
            Assert.Contains("object-src 'none'", contentSecurityPolicy, StringComparison.Ordinal);
            Assert.DoesNotContain("unsafe-eval", contentSecurityPolicy, StringComparison.Ordinal);
            Assert.Equal("Servanda", await page.TitleAsync());
            Assert.Equal("Servanda", await page.GetByRole(AriaRole.Heading, new() { Level = 1 }).TextContentAsync());
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
            Assert.Equal(7, await page.Locator(".sidebar__status").GetByText("Planowane", new() { Exact = true }).CountAsync());
            Assert.Equal(0, await page.GetByText("Zarządzaj obszarami", new() { Exact = true }).CountAsync());

            foreach (var areaName in new[]
            {
                "Skarbiec promptów",
                "Przechowalnia narzędzi",
                "Dom",
                "Rodzina",
                "Witalność",
                "Przechowalnia notatek",
                "Budżet domowy",
            })
            {
                var plannedArea = page.GetByText(areaName, new() { Exact = true });
                Assert.Equal(1, await plannedArea.CountAsync());
                Assert.False(
                    await plannedArea.EvaluateAsync<bool>(
                        "element => element.closest('a, button') !== null"),
                    $"Planowany obszar „{areaName}” nie może być interaktywny w v1.");
            }

            Assert.Equal(
                "rgb(11, 13, 17)",
                await page.Locator("body").EvaluateAsync<string>("element => getComputedStyle(element).backgroundColor"));
            Assert.Equal(
                "rgb(243, 245, 247)",
                await page.Locator("body").EvaluateAsync<string>("element => getComputedStyle(element).color"));

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
            }

            await page.Locator(".sidebar__brand").FocusAsync();
            await page.Keyboard.PressAsync("Shift+Tab");
            var activeElement = await page.EvaluateAsync<string>(
                "() => `${document.activeElement?.tagName}.${document.activeElement?.className}`");
            Assert.True(
                await page.Locator(".skip-link").EvaluateAsync<bool>(
                    "element => element === document.activeElement"),
                $"Fokus klawiatury trafił na {activeElement} zamiast linku pomijającego.");
            Assert.DoesNotContain("ticket=", page.Url, StringComparison.Ordinal);

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

}

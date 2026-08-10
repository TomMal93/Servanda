using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.Versioning;
using Microsoft.Playwright;
using Servanda.App.Launching;
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
        var paths = new ServandaPaths(Path.Combine(runtimeBase, "servanda"), Path.Combine(stateBase, "servanda"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var host = StartHost(executablePath, runtimeBase, stateBase);

        try
        {
            var descriptor = await WaitForReadyDescriptorAsync(paths.DescriptorPath, timeout.Token);
            var platform = new RecordingLauncherPlatform();

            var launcherResult = await new Launcher(paths, platform).RunAsync(timeout.Token);

            Assert.Equal(0, launcherResult);
            Assert.Equal(0, platform.HostStartCount);
            var bootstrapAddress = Assert.IsType<string>(platform.OpenedAddress);
            Assert.StartsWith($"{descriptor.Origin}/bootstrap#ticket=", bootstrapAddress, StringComparison.Ordinal);

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

            Assert.Contains(requestedAddresses, address => new Uri(address).AbsolutePath.Contains("shutdown", StringComparison.Ordinal));
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
            Assert.Contains(
                "Servanda została zamknięta",
                await shutdownResponse.TextAsync(),
                StringComparison.Ordinal);
            await host.WaitForExitAsync(timeout.Token);
            Assert.Equal(0, host.ExitCode);
        }
        finally
        {
            await StopProcessAsync(host);
            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    private static async Task<IBrowser> LaunchBrowserAsync(IPlaywright playwright, string browserName) =>
        browserName switch
        {
            "chromium" => await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }),
            "firefox" => await playwright.Firefox.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true }),
            _ => throw new ArgumentOutOfRangeException(nameof(browserName), browserName, "Nieobsługiwana przeglądarka testowa."),
        };

    private static Process StartHost(string executablePath, string runtimeBase, string stateBase)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            ArgumentList = { "--host" },
        };
        startInfo.Environment["XDG_RUNTIME_DIR"] = runtimeBase;
        startInfo.Environment["XDG_STATE_HOME"] = stateBase;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Nie udało się uruchomić hosta z artefaktu.");
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

    private sealed class RecordingLauncherPlatform : ILauncherPlatform
    {
        internal int HostStartCount { get; private set; }

        internal string? OpenedAddress { get; private set; }

        public bool StartHost()
        {
            HostStartCount++;
            return false;
        }

        public bool OpenBrowser(string address)
        {
            OpenedAddress = address;
            return true;
        }

        public bool ShowError() => false;
    }
}

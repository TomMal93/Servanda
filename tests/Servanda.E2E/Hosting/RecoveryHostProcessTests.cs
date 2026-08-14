using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using Servanda.Infrastructure.Runtime;

namespace Servanda.E2E.Hosting;

[SupportedOSPlatform("linux")]
public sealed partial class RecoveryHostProcessTests
{
    [Fact]
    public async Task CorruptedDatabasePublishesRestrictedRecoveryAndAllowsProtectedRetry()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-recovery-host-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryPath, PrivateDirectoryMode);
        var runtimeBase = Path.Combine(temporaryPath, "runtime");
        var stateBase = Path.Combine(temporaryPath, "state");
        var dataBase = Path.Combine(temporaryPath, "data");
        var applicationData = Path.Combine(dataBase, "servanda");
        Directory.CreateDirectory(applicationData, PrivateDirectoryMode);
        var databasePath = Path.Combine(applicationData, "servanda.db");
        await File.WriteAllBytesAsync(databasePath, new byte[128]);
        File.SetUnixFileMode(databasePath, PrivateFileMode);
        var descriptorPath = Path.Combine(runtimeBase, "servanda", "instance.json");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var host = StartHost(runtimeBase, stateBase, dataBase);

        try
        {
            var descriptor = await WaitForRecoveryDescriptorAsync(descriptorPath, timeout.Token);
            Assert.Equal("recovery", descriptor.State);
            var origin = new Uri(descriptor.Origin);
            var cookies = new CookieContainer();
            using var handler = new HttpClientHandler
            {
                AllowAutoRedirect = false,
                CookieContainer = cookies,
                UseCookies = true,
                UseProxy = false,
            };
            using var client = new HttpClient(handler) { BaseAddress = origin };

            using var unauthorizedRecovery = await client.GetAsync("recovery", timeout.Token);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthorizedRecovery.StatusCode);

            await BootstrapSessionAsync(client, runtimeBase, origin, timeout.Token);

            using var dashboard = await client.GetAsync("", timeout.Token);
            Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
            Assert.Equal("/recovery", dashboard.Headers.Location?.OriginalString);

            using var recoveryPage = await client.GetAsync("recovery", timeout.Token);
            var recoveryHtml = await recoveryPage.Content.ReadAsStringAsync(timeout.Token);
            Assert.Equal(HttpStatusCode.OK, recoveryPage.StatusCode);
            Assert.Contains("Tryb odzyskiwania", recoveryHtml, StringComparison.Ordinal);
            Assert.Contains("Servanda nie może otworzyć magazynu danych", recoveryHtml, StringComparison.Ordinal);
            Assert.Contains("recovery-retry", recoveryHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("sidebar", recoveryHtml, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(databasePath, recoveryHtml, StringComparison.Ordinal);
            Assert.DoesNotContain("stack trace", recoveryHtml, StringComparison.OrdinalIgnoreCase);
            Assert.True(recoveryPage.Headers.Contains("Content-Security-Policy"));

            var antiforgeryToken = AntiforgeryTokenPattern().Match(recoveryHtml).Groups[1].Value;
            Assert.False(string.IsNullOrWhiteSpace(antiforgeryToken));
            using var retryRequest = new HttpRequestMessage(HttpMethod.Post, "recovery/retry")
            {
                Content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("__RequestVerificationToken", antiforgeryToken),
                ]),
            };
            retryRequest.Headers.TryAddWithoutValidation("Origin", origin.GetLeftPart(UriPartial.Authority));
            using var retryResponse = await client.SendAsync(retryRequest, timeout.Token);

            Assert.Equal(HttpStatusCode.Redirect, retryResponse.StatusCode);
            Assert.Equal("/recovery?retry=failed", retryResponse.Headers.Location?.OriginalString);
            Assert.Equal(
                "recovery",
                (await new InstanceDescriptorReader(descriptorPath).TryReadAvailableAsync(timeout.Token))?.State);

            using var databaseLock = DatabaseLock.TryAcquire(Path.Combine(applicationData, "servanda.lock"));
            Assert.Null(databaseLock);
        }
        finally
        {
            await StopProcessAsync(host);
            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    private static async Task BootstrapSessionAsync(
        HttpClient client,
        string runtimeBase,
        Uri origin,
        CancellationToken cancellationToken)
    {
        var secretPath = Path.Combine(runtimeBase, "servanda", "control.secret");
        var secret = await File.ReadAllBytesAsync(secretPath, cancellationToken);
        using var ticketRequest = new HttpRequestMessage(HttpMethod.Post, "launcher/ticket");
        ticketRequest.Headers.TryAddWithoutValidation("X-Servanda-Control", Convert.ToBase64String(secret));
        using var ticketResponse = await client.SendAsync(ticketRequest, cancellationToken);
        ticketResponse.EnsureSuccessStatusCode();
        using var ticketJson = JsonDocument.Parse(await ticketResponse.Content.ReadAsStringAsync(cancellationToken));
        var ticket = ticketJson.RootElement.GetProperty("ticket").GetString();
        Assert.False(string.IsNullOrWhiteSpace(ticket));

        using var bootstrapRequest = new HttpRequestMessage(HttpMethod.Post, "session/bootstrap")
        {
            Content = JsonContent.Create(new { ticket }),
        };
        bootstrapRequest.Headers.TryAddWithoutValidation("Origin", origin.GetLeftPart(UriPartial.Authority));
        using var bootstrapResponse = await client.SendAsync(bootstrapRequest, cancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, bootstrapResponse.StatusCode);
    }

    private static Process StartHost(string runtimeBase, string stateBase, string dataBase)
    {
        var executablePath = Path.Combine(AppContext.BaseDirectory, "Servanda");
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = AppContext.BaseDirectory,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            ArgumentList = { "--host" },
        };
        startInfo.Environment["XDG_RUNTIME_DIR"] = runtimeBase;
        startInfo.Environment["XDG_STATE_HOME"] = stateBase;
        startInfo.Environment["XDG_DATA_HOME"] = dataBase;
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        return Process.Start(startInfo)
            ?? throw new InvalidOperationException("Nie udało się uruchomić hosta recovery.");
    }

    private static async Task<InstanceDescriptor> WaitForRecoveryDescriptorAsync(
        string descriptorPath,
        CancellationToken cancellationToken)
    {
        var reader = new InstanceDescriptorReader(descriptorPath);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = await reader.TryReadAvailableAsync(cancellationToken);
            if (descriptor is { State: "recovery" })
            {
                return descriptor;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }

    private static async Task StopProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill();
            await process.WaitForExitAsync();
        }
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex AntiforgeryTokenPattern();

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Servanda.App.Security;
using Servanda.Infrastructure.Runtime;

namespace Servanda.E2E.Security;

[SupportedOSPlatform("linux")]
public sealed class HostSecurityProcessTests
{
    [Fact]
    public async Task RunningHostEnforcesHostOriginSessionAntiforgeryAndSecurityHeaders()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-security-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            temporaryPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var runtimeBase = Path.Combine(temporaryPath, "runtime");
        var stateBase = Path.Combine(temporaryPath, "state");
        var runtimeDirectory = Path.Combine(runtimeBase, "servanda");
        var descriptorPath = Path.Combine(runtimeDirectory, "instance.json");
        var controlSecretPath = Path.Combine(runtimeDirectory, "control.secret");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        using var host = StartHost(runtimeBase, stateBase);

        try
        {
            var descriptor = await WaitForReadyDescriptorAsync(descriptorPath, timeout.Token);
            var origin = new Uri(descriptor.Origin, UriKind.Absolute);
            var cookieContainer = new CookieContainer();
            using var client = CreateClient(origin, cookieContainer);

            using (var foreignHostRequest = new HttpRequestMessage(HttpMethod.Get, "/instance"))
            {
                foreignHostRequest.Headers.Host = "example.com";
                using var foreignHostResponse = await client.SendAsync(foreignHostRequest, timeout.Token);
                Assert.Equal(HttpStatusCode.BadRequest, foreignHostResponse.StatusCode);
            }

            using (var anonymousResponse = await client.GetAsync("/", timeout.Token))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, anonymousResponse.StatusCode);
                AssertSecurityHeaders(anonymousResponse, origin);
                Assert.False(anonymousResponse.Headers.Contains("Access-Control-Allow-Origin"));
            }

            for (var attempt = 0; attempt < BootstrapRateLimiter.PermitLimit + 2; attempt++)
            {
                using var wrongSecretRequest = new HttpRequestMessage(HttpMethod.Post, "/launcher/ticket");
                wrongSecretRequest.Headers.Add(
                    "X-Servanda-Control",
                    Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
                using var wrongSecretResponse = await client.SendAsync(wrongSecretRequest, timeout.Token);
                Assert.Equal(HttpStatusCode.Unauthorized, wrongSecretResponse.StatusCode);
            }

            using (var ticketWithBodyRequest = new HttpRequestMessage(HttpMethod.Post, "/launcher/ticket"))
            {
                ticketWithBodyRequest.Headers.Add(
                    "X-Servanda-Control",
                    await ReadControlSecretHeaderAsync(controlSecretPath, timeout.Token));
                ticketWithBodyRequest.Content = new StringContent("{}", Encoding.UTF8, "application/json");
                using var ticketWithBodyResponse = await client.SendAsync(ticketWithBodyRequest, timeout.Token);
                Assert.Equal(HttpStatusCode.Unauthorized, ticketWithBodyResponse.StatusCode);
            }

            var firstTicket = await IssueTicketAsync(client, controlSecretPath, timeout.Token);
            using (var foreignOriginResponse = await BootstrapAsync(
                       client,
                       firstTicket,
                       "https://example.com",
                       timeout.Token))
            {
                Assert.Equal(HttpStatusCode.Forbidden, foreignOriginResponse.StatusCode);
            }

            using (var bootstrapResponse = await BootstrapAsync(
                       client,
                       firstTicket,
                       origin.GetLeftPart(UriPartial.Authority),
                       timeout.Token))
            {
                Assert.Equal(HttpStatusCode.NoContent, bootstrapResponse.StatusCode);
                var sessionCookie = Assert.Single(bootstrapResponse.Headers.GetValues("Set-Cookie"));
                Assert.Contains("HttpOnly", sessionCookie, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("SameSite=Strict", sessionCookie, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Path=/", sessionCookie, StringComparison.OrdinalIgnoreCase);
                Assert.DoesNotContain("Domain=", sessionCookie, StringComparison.OrdinalIgnoreCase);
            }

            using (var replayResponse = await BootstrapAsync(
                       client,
                       firstTicket,
                       origin.GetLeftPart(UriPartial.Authority),
                       timeout.Token))
            {
                Assert.Equal(HttpStatusCode.Unauthorized, replayResponse.StatusCode);
            }

            using (var oversizedBootstrapRequest = new HttpRequestMessage(HttpMethod.Post, "/session/bootstrap"))
            {
                oversizedBootstrapRequest.Headers.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
                oversizedBootstrapRequest.Content = new StringContent(
                    JsonSerializer.Serialize(new { ticket = new string('a', 2048) }),
                    Encoding.UTF8,
                    "application/json");
                using var oversizedBootstrapResponse = await client.SendAsync(oversizedBootstrapRequest, timeout.Token);
                Assert.Equal(HttpStatusCode.RequestEntityTooLarge, oversizedBootstrapResponse.StatusCode);
            }

            string antiforgeryToken;
            using (var applicationResponse = await client.GetAsync("/", timeout.Token))
            {
                Assert.Equal(HttpStatusCode.OK, applicationResponse.StatusCode);
                AssertSecurityHeaders(applicationResponse, origin);
                var applicationHtml = await applicationResponse.Content.ReadAsStringAsync(timeout.Token);
                var tokenMatch = Regex.Match(
                    applicationHtml,
                    "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"",
                    RegexOptions.CultureInvariant);
                Assert.True(tokenMatch.Success, "Powłoka nie zawiera tokenu antiforgery formularza zamykania.");
                antiforgeryToken = WebUtility.HtmlDecode(tokenMatch.Groups[1].Value);
            }

            using (var foreignBlazorRequest = new HttpRequestMessage(HttpMethod.Post, "/_blazor/negotiate"))
            {
                foreignBlazorRequest.Headers.Add("Origin", "https://example.com");
                using var foreignBlazorResponse = await client.SendAsync(foreignBlazorRequest, timeout.Token);
                Assert.Equal(HttpStatusCode.Forbidden, foreignBlazorResponse.StatusCode);
            }

            var session = cookieContainer.GetCookies(origin)[ProcessSessionStore.CookieName]?.Value;
            Assert.False(string.IsNullOrWhiteSpace(session));
            var webSocketStatusLine = await SendForeignOriginWebSocketUpgradeAsync(
                origin,
                session,
                timeout.Token);
            Assert.Contains(" 403 ", webSocketStatusLine, StringComparison.Ordinal);

            var wasRateLimited = false;
            for (var attempt = 0; attempt < 12 && !wasRateLimited; attempt++)
            {
                using var rateLimitedRequest = new HttpRequestMessage(HttpMethod.Post, "/launcher/ticket");
                rateLimitedRequest.Headers.Add(
                    "X-Servanda-Control",
                    await ReadControlSecretHeaderAsync(controlSecretPath, timeout.Token));
                using var rateLimitedResponse = await client.SendAsync(rateLimitedRequest, timeout.Token);
                wasRateLimited = rateLimitedResponse.StatusCode == HttpStatusCode.TooManyRequests;
            }

            Assert.True(wasRateLimited, "Endpoint launchera nie wymusił limitu żądań.");

            using (var shutdownWithoutAntiforgery = new HttpRequestMessage(HttpMethod.Post, "/shutdown"))
            {
                shutdownWithoutAntiforgery.Headers.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
                using var shutdownResponse = await client.SendAsync(shutdownWithoutAntiforgery, timeout.Token);
                Assert.Equal(HttpStatusCode.BadRequest, shutdownResponse.StatusCode);
                Assert.False(host.HasExited);
            }

            using var stillReadyResponse = await client.GetAsync("/instance", timeout.Token);
            Assert.Equal(HttpStatusCode.OK, stillReadyResponse.StatusCode);

            using var confirmedShutdown = new HttpRequestMessage(HttpMethod.Post, "/shutdown")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["__RequestVerificationToken"] = antiforgeryToken,
                }),
            };
            confirmedShutdown.Headers.Add("Origin", origin.GetLeftPart(UriPartial.Authority));
            using var confirmedShutdownResponse = await client.SendAsync(confirmedShutdown, timeout.Token);
            Assert.Equal(HttpStatusCode.OK, confirmedShutdownResponse.StatusCode);
            Assert.Contains(
                "Servanda została zamknięta",
                await confirmedShutdownResponse.Content.ReadAsStringAsync(timeout.Token),
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

    private static Process StartHost(string runtimeBase, string stateBase)
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
        startInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Nie udało się uruchomić testowego hosta.");
    }

    private static HttpClient CreateClient(Uri origin, CookieContainer cookies)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            UseCookies = true,
            UseProxy = false,
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = origin,
            Timeout = TimeSpan.FromSeconds(5),
        };
    }

    private static async Task<string> IssueTicketAsync(
        HttpClient client,
        string controlSecretPath,
        CancellationToken cancellationToken)
    {
        var secret = await ControlSecretReader.TryReadAsync(controlSecretPath, cancellationToken);
        Assert.NotNull(secret);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/launcher/ticket");
            request.Headers.Add("X-Servanda-Control", Convert.ToBase64String(secret));
            using var response = await client.SendAsync(request, cancellationToken);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var payload = await response.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken);
            return payload!.RootElement.GetProperty("ticket").GetString()!;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static async Task<string> ReadControlSecretHeaderAsync(
        string controlSecretPath,
        CancellationToken cancellationToken)
    {
        var secret = await ControlSecretReader.TryReadAsync(controlSecretPath, cancellationToken);
        Assert.NotNull(secret);

        try
        {
            return Convert.ToBase64String(secret);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static async Task<string> SendForeignOriginWebSocketUpgradeAsync(
        Uri origin,
        string session,
        CancellationToken cancellationToken)
    {
        using var socket = new TcpClient();
        await socket.ConnectAsync(origin.Host, origin.Port, cancellationToken);
        await using var stream = socket.GetStream();
        var request = string.Join(
            "\r\n",
            "GET /_blazor?id=invalid HTTP/1.1",
            $"Host: {origin.Authority}",
            "Connection: Upgrade",
            "Upgrade: websocket",
            "Sec-WebSocket-Version: 13",
            $"Sec-WebSocket-Key: {Convert.ToBase64String(RandomNumberGenerator.GetBytes(16))}",
            "Origin: https://example.com",
            $"Cookie: {ProcessSessionStore.CookieName}={session}",
            string.Empty,
            string.Empty);
        await stream.WriteAsync(Encoding.ASCII.GetBytes(request), cancellationToken);

        using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
        return await reader.ReadLineAsync(cancellationToken)
            ?? throw new InvalidOperationException("Host nie zwrócił odpowiedzi na próbę WebSocket.");
    }

    private static Task<HttpResponseMessage> BootstrapAsync(
        HttpClient client,
        string ticket,
        string origin,
        CancellationToken cancellationToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/session/bootstrap")
        {
            Content = JsonContent.Create(new { ticket }),
        };
        request.Headers.Add("Origin", origin);
        return client.SendAsync(request, cancellationToken);
    }

    private static void AssertSecurityHeaders(HttpResponseMessage response, Uri origin)
    {
        var contentSecurityPolicy = string.Join("; ", response.Headers.GetValues("Content-Security-Policy"));
        Assert.Contains("default-src 'self'", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("script-src 'self'", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("object-src 'none'", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("base-uri 'none'", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains("frame-ancestors 'none'", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Contains($"connect-src {origin.GetLeftPart(UriPartial.Authority)} ws://{origin.Authority}", contentSecurityPolicy, StringComparison.Ordinal);
        Assert.Equal("no-referrer", Assert.Single(response.Headers.GetValues("Referrer-Policy")));
        Assert.Equal("nosniff", Assert.Single(response.Headers.GetValues("X-Content-Type-Options")));
        Assert.Contains("camera=()", Assert.Single(response.Headers.GetValues("Permissions-Policy")), StringComparison.Ordinal);
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

    private static async Task StopProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill();
            await process.WaitForExitAsync();
        }
    }
}

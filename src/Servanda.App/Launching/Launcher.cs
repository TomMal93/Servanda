using System.Net.Http.Json;
using System.Security.Cryptography;
using Servanda.Infrastructure.Runtime;

namespace Servanda.App.Launching;

public sealed class Launcher
{
    private static readonly TimeSpan ExistingInstanceWait = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan HostStartTimeout = TimeSpan.FromSeconds(10);

    private readonly ServandaPaths _paths;
    private readonly InstanceDescriptorReader _descriptorReader;
    private readonly ILauncherPlatform _platform;

    public Launcher(ServandaPaths paths, ILauncherPlatform? platform = null)
    {
        _paths = paths;
        _descriptorReader = new InstanceDescriptorReader(paths.DescriptorPath);
        _platform = platform ?? new LinuxLauncherPlatform();
    }

    public async Task<int> RunAsync(CancellationToken cancellationToken = default)
    {
        var descriptor = await WaitForConfirmedInstanceAsync(ExistingInstanceWait, cancellationToken);
        if (descriptor is null)
        {
            if (!_platform.StartHost())
            {
                return Fail("Nie udało się uruchomić lokalnego procesu Servandy.");
            }

            descriptor = await WaitForConfirmedInstanceAsync(HostStartTimeout, cancellationToken);
        }

        if (descriptor is null)
        {
            return Fail("Servanda nie potwierdziła gotowości. Spróbuj uruchomić ją ponownie.");
        }

        var ticket = await RequestTicketAsync(descriptor, cancellationToken);
        if (ticket is null)
        {
            return Fail("Nie udało się utworzyć bezpiecznej sesji Servandy.");
        }

        var bootstrapAddress = $"{descriptor.Origin}/bootstrap#ticket={Uri.EscapeDataString(ticket)}";
        if (!_platform.OpenBrowser(bootstrapAddress))
        {
            return Fail("Nie udało się otworzyć domyślnej przeglądarki.");
        }

        return 0;
    }

    private async Task<InstanceDescriptor?> WaitForConfirmedInstanceAsync(
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var deadline = TimeProvider.System.GetUtcNow().Add(timeout);
        do
        {
            var descriptor = await _descriptorReader.TryReadReadyAsync(cancellationToken);
            if (descriptor is not null && await ConfirmInstanceAsync(descriptor, cancellationToken))
            {
                return descriptor;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
        while (TimeProvider.System.GetUtcNow() < deadline);

        return null;
    }

    private static async Task<bool> ConfirmInstanceAsync(
        InstanceDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = CreateHttpClient(descriptor.Origin);
            var confirmation = await client.GetFromJsonAsync<InstanceConfirmation>("instance", cancellationToken);
            return confirmation is not null
                && confirmation.FormatVersion == InstanceDescriptor.CurrentFormatVersion
                && confirmation.InstanceId == descriptor.InstanceId
                && confirmation.State == descriptor.State;
        }
        catch (HttpRequestException)
        {
            return false;
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }

    private async Task<string?> RequestTicketAsync(
        InstanceDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var secret = await ControlSecretReader.TryReadAsync(_paths.ControlSecretPath, cancellationToken);
        if (secret is null)
        {
            return null;
        }

        try
        {
            using var client = CreateHttpClient(descriptor.Origin);
            using var request = new HttpRequestMessage(HttpMethod.Post, "launcher/ticket");
            request.Headers.Add("X-Servanda-Control", Convert.ToBase64String(secret));
            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var result = await response.Content.ReadFromJsonAsync<TicketResponse>(cancellationToken);
            return string.IsNullOrWhiteSpace(result?.Ticket) ? null : result.Ticket;
        }
        catch (HttpRequestException)
        {
            return null;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }
    }

    private static HttpClient CreateHttpClient(string origin)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = TimeSpan.FromSeconds(2),
            UseCookies = false,
            UseProxy = false,
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            BaseAddress = new Uri(origin, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(3),
        };
    }

    private static int Fail(string message)
    {
        Console.Error.WriteLine(message);
        return 1;
    }

    private sealed record InstanceConfirmation(int FormatVersion, string InstanceId, string State);

    private sealed record TicketResponse(string Ticket, int ExpiresInSeconds);
}

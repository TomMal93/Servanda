using System.Net;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Servanda.App.Launching;
using Servanda.Infrastructure.Runtime;

namespace Servanda.E2E.Launching;

[SupportedOSPlatform("linux")]
public sealed class LauncherTests
{
    [Fact]
    public async Task ExistingConfirmedInstanceIsOpenedWithoutStartingAnotherHost()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-launcher-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            temporaryPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var paths = new ServandaPaths(temporaryPath, temporaryPath);
        using var controlSecret = await ControlSecret.CreateAndPublishAsync(paths.ControlSecretPath);

        var instanceId = Guid.NewGuid().ToString("N");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var app = builder.Build();
        app.MapGet("/instance", () => Results.Ok(new
        {
            formatVersion = InstanceDescriptor.CurrentFormatVersion,
            instanceId,
            state = "ready",
        }));
        app.MapPost("/launcher/ticket", (HttpRequest request) =>
            controlSecret.Authenticate(request.Headers["X-Servanda-Control"])
                ? Results.Ok(new { ticket = "test-ticket", expiresInSeconds = 60 })
                : Results.Unauthorized());

        try
        {
            await app.StartAsync();
            var origin = app.Urls.Single();
            var descriptor = InstanceDescriptor.Starting(instanceId, Environment.ProcessId, origin).Ready();
            await new AtomicInstanceDescriptorStore(paths.DescriptorPath).PublishAsync(descriptor);
            var platform = new RecordingLauncherPlatform();

            var result = await new Launcher(paths, platform).RunAsync();

            Assert.Equal(0, result);
            Assert.Equal(0, platform.HostStartCount);
            Assert.Equal($"{origin}/bootstrap#ticket=test-ticket", platform.OpenedAddress);
        }
        finally
        {
            await app.StopAsync();
            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    private sealed class RecordingLauncherPlatform : ILauncherPlatform
    {
        internal int HostStartCount { get; private set; }

        internal string? OpenedAddress { get; private set; }

        public bool StartHost()
        {
            HostStartCount++;
            return true;
        }

        public bool OpenBrowser(string address)
        {
            OpenedAddress = address;
            return true;
        }
    }
}

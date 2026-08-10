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
    public void BrowserUsesDesktopRuntimeInsteadOfIsolatedHostRuntime()
    {
        var startInfo = LinuxLauncherPlatform.CreateBrowserStartInfo(
            "http://127.0.0.1:43210/bootstrap#ticket=test-ticket",
            "/tmp/servanda-development-runtime",
            1234,
            path => path == "/run/user/1234");

        Assert.Equal("xdg-open", startInfo.FileName);
        Assert.False(startInfo.UseShellExecute);
        Assert.Equal("http://127.0.0.1:43210/bootstrap#ticket=test-ticket", Assert.Single(startInfo.ArgumentList));
        Assert.Equal("/run/user/1234", startInfo.Environment["XDG_RUNTIME_DIR"]);
    }

    [Fact]
    public void BrowserDoesNotInheritIsolatedRuntimeWhenDesktopRuntimeIsUnavailable()
    {
        var startInfo = LinuxLauncherPlatform.CreateBrowserStartInfo(
            "http://127.0.0.1:43210/bootstrap#ticket=test-ticket",
            "/tmp/servanda-development-runtime",
            1234,
            _ => false);

        Assert.False(startInfo.Environment.ContainsKey("XDG_RUNTIME_DIR"));
    }

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

    [Fact]
    public async Task HostStartFailureShowsSafeErrorWithoutOpeningApplicationAddress()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-launcher-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            temporaryPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

        try
        {
            var platform = new RecordingLauncherPlatform
            {
                HostStartResult = false,
            };

            var result = await new Launcher(new ServandaPaths(temporaryPath, temporaryPath), platform).RunAsync();

            Assert.Equal(1, result);
            Assert.Equal(1, platform.HostStartCount);
            Assert.Equal(1, platform.ErrorCount);
            Assert.Null(platform.OpenedAddress);
        }
        finally
        {
            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    [Fact]
    public async Task UnconfirmedDescriptorIsNotOpenedAndFallsBackToStartingHost()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-launcher-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            temporaryPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var paths = new ServandaPaths(temporaryPath, temporaryPath);
        var expectedInstanceId = Guid.NewGuid().ToString("N");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));
        await using var app = builder.Build();
        app.MapGet("/instance", () => Results.Ok(new
        {
            formatVersion = InstanceDescriptor.CurrentFormatVersion,
            instanceId = Guid.NewGuid().ToString("N"),
            state = "ready",
        }));

        try
        {
            await app.StartAsync();
            var descriptor = InstanceDescriptor
                .Starting(expectedInstanceId, Environment.ProcessId, app.Urls.Single())
                .Ready();
            await new AtomicInstanceDescriptorStore(paths.DescriptorPath).PublishAsync(descriptor);
            var platform = new RecordingLauncherPlatform
            {
                HostStartResult = false,
            };

            var result = await new Launcher(paths, platform).RunAsync();

            Assert.Equal(1, result);
            Assert.Equal(1, platform.HostStartCount);
            Assert.Equal(1, platform.ErrorCount);
            Assert.Null(platform.OpenedAddress);
        }
        finally
        {
            await app.StopAsync();
            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    [Fact]
    public async Task ConfirmedInstanceWithoutControlSecretShowsErrorWithoutOpeningBrowser()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-launcher-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            temporaryPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var paths = new ServandaPaths(temporaryPath, temporaryPath);
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

        try
        {
            await app.StartAsync();
            var descriptor = InstanceDescriptor
                .Starting(instanceId, Environment.ProcessId, app.Urls.Single())
                .Ready();
            await new AtomicInstanceDescriptorStore(paths.DescriptorPath).PublishAsync(descriptor);
            var platform = new RecordingLauncherPlatform();

            var result = await new Launcher(paths, platform).RunAsync();

            Assert.Equal(1, result);
            Assert.Equal(0, platform.HostStartCount);
            Assert.Equal(1, platform.ErrorCount);
            Assert.Null(platform.OpenedAddress);
        }
        finally
        {
            await app.StopAsync();
            Directory.Delete(temporaryPath, recursive: true);
        }
    }

    private sealed class RecordingLauncherPlatform : ILauncherPlatform
    {
        internal bool HostStartResult { get; init; } = true;

        internal int HostStartCount { get; private set; }

        internal string? OpenedAddress { get; private set; }

        internal int ErrorCount { get; private set; }

        public bool StartHost()
        {
            HostStartCount++;
            return HostStartResult;
        }

        public bool OpenBrowser(string address)
        {
            OpenedAddress = address;
            return true;
        }

        public bool ShowError()
        {
            ErrorCount++;
            return true;
        }
    }
}

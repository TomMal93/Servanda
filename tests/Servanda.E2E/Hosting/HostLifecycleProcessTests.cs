using System.Diagnostics;
using System.Runtime.Versioning;
using Servanda.Infrastructure.Runtime;

namespace Servanda.E2E.Hosting;

[SupportedOSPlatform("linux")]
public sealed class HostLifecycleProcessTests
{
    [Fact]
    public async Task SecondHostIsRejectedAndOrphanedDescriptorDoesNotBlockRestart()
    {
        var temporaryPath = Path.Combine(Path.GetTempPath(), $"servanda-host-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(
            temporaryPath,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        var runtimeBase = Path.Combine(temporaryPath, "runtime");
        var stateBase = Path.Combine(temporaryPath, "state");
        var dataBase = Path.Combine(temporaryPath, "data");
        var descriptorPath = Path.Combine(runtimeBase, "servanda", "instance.json");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        using var firstHost = StartHost(runtimeBase, stateBase, dataBase);
        Process? replacementHost = null;

        try
        {
            var firstDescriptor = await WaitForReadyDescriptorAsync(descriptorPath, timeout.Token);
            using var secondHost = StartHost(runtimeBase, stateBase, dataBase);

            await secondHost.WaitForExitAsync(timeout.Token);

            Assert.Equal(2, secondHost.ExitCode);

            firstHost.Kill();
            await firstHost.WaitForExitAsync(timeout.Token);
            Assert.True(File.Exists(descriptorPath));

            replacementHost = StartHost(runtimeBase, stateBase, dataBase);
            var replacementDescriptor = await WaitForReadyDescriptorAsync(
                descriptorPath,
                timeout.Token,
                firstDescriptor.InstanceId);

            Assert.NotEqual(firstDescriptor.InstanceId, replacementDescriptor.InstanceId);
            Assert.True(Uri.TryCreate(replacementDescriptor.Origin, UriKind.Absolute, out var origin));
            Assert.True(origin.IsLoopback);
        }
        finally
        {
            await StopProcessAsync(firstHost);
            if (replacementHost is not null)
            {
                await StopProcessAsync(replacementHost);
                replacementHost.Dispose();
            }

            Directory.Delete(temporaryPath, recursive: true);
        }
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

        return Process.Start(startInfo) ?? throw new InvalidOperationException("Nie udało się uruchomić testowego hosta.");
    }

    private static async Task<InstanceDescriptor> WaitForReadyDescriptorAsync(
        string descriptorPath,
        CancellationToken cancellationToken,
        string? rejectedInstanceId = null)
    {
        var reader = new InstanceDescriptorReader(descriptorPath);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var descriptor = await reader.TryReadReadyAsync(cancellationToken);
            if (descriptor is not null && descriptor.InstanceId != rejectedInstanceId)
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

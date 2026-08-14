using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Servanda.App.Hosting;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Diagnostics;
using Servanda.Infrastructure.Runtime;

namespace Servanda.E2E.Hosting;

[SupportedOSPlatform("linux")]
public sealed class DatabaseRecoveryCoordinatorTests
{
    [Fact]
    public async Task RetryTransitionsRecoveryToReadyAfterDatabaseBecomesAvailable()
    {
        using var temporaryDirectory = new TestDirectory();
        var paths = temporaryDirectory.Paths;
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        File.SetUnixFileMode(
            paths.DatabasePath,
            PrivateFileMode | UnixFileMode.GroupRead);
        var runtimeState = new InstanceRuntimeState();
        var descriptorStore = new AtomicInstanceDescriptorStore(paths.DescriptorPath);
        using var technicalLog = new TechnicalLogWriter(paths);
        using var coordinator = new DatabaseRecoveryCoordinator(
            services,
            paths,
            TimeProvider.System,
            runtimeState,
            descriptorStore,
            technicalLog);

        var initialized = await coordinator.InitializeAsync();

        Assert.False(initialized);
        Assert.True(coordinator.Snapshot.RequiresRecovery);
        Assert.Equal(DatabaseInitializationFailure.DatabaseAccess, coordinator.Snapshot.Failure);
        Assert.Equal(ProtectionBackupState.NotCreated, coordinator.Snapshot.BackupState);

        runtimeState.AttachOrigin(new Uri("http://127.0.0.1:43210"));
        File.SetUnixFileMode(paths.DatabasePath, PrivateFileMode);

        var recovered = await coordinator.InitializeAsync();
        var descriptor = await new InstanceDescriptorReader(paths.DescriptorPath).TryReadReadyAsync();

        Assert.True(recovered);
        Assert.True(runtimeState.IsReady);
        Assert.False(coordinator.Snapshot.RequiresRecovery);
        Assert.NotNull(descriptor);
        Assert.Equal("ready", descriptor.State);
        Assert.Contains("RECOVERY_RETRY_SUCCEEDED", await File.ReadAllTextAsync(paths.TechnicalLogPath));
    }

    private static ServiceProvider CreateServices(ServandaPaths paths)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
        services.AddServandaDatabase(paths, "test-version");
        return services.BuildServiceProvider();
    }

    private sealed class TestDirectory : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"servanda-recovery-tests-{Guid.NewGuid():N}");

        internal TestDirectory()
        {
            Paths = new ServandaPaths(
                Path.Combine(_root, "runtime"),
                Path.Combine(_root, "state"),
                Path.Combine(_root, "data"));
            Directory.CreateDirectory(Paths.RuntimeDirectory, PrivateDirectoryMode);
            Directory.CreateDirectory(Paths.StateDirectory, PrivateDirectoryMode);
            Directory.CreateDirectory(Paths.DataDirectory, PrivateDirectoryMode);
        }

        internal ServandaPaths Paths { get; }

        public void Dispose() => Directory.Delete(_root, recursive: true);
    }

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

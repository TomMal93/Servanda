using System.Runtime.Versioning;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Application.DataProtection;
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
            services.GetRequiredService<IDatabaseRecoveryService>(),
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

    [Fact]
    public async Task RestorePreservesFailedDatabaseAndTransitionsToVerifiedBackup()
    {
        using var temporaryDirectory = new TestDirectory();
        var paths = temporaryDirectory.Paths;
        await using var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        await using (var connection = new SqliteConnection($"Data Source={paths.DatabasePath};Pooling=False"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "UPDATE areas SET name = 'Stan z kopii' WHERE module_key = 'home';";
            await command.ExecuteNonQueryAsync();
        }

        var backupService = services.GetRequiredService<IBackupService>();
        var backup = await backupService.CreateAsync(BackupReason.Manual);
        SqliteConnection.ClearAllPools();
        await File.WriteAllBytesAsync(paths.DatabasePath, new byte[128]);
        var runtimeState = new InstanceRuntimeState();
        var descriptorStore = new AtomicInstanceDescriptorStore(paths.DescriptorPath);
        using var technicalLog = new TechnicalLogWriter(paths);
        using var coordinator = new DatabaseRecoveryCoordinator(
            services,
            paths,
            TimeProvider.System,
            services.GetRequiredService<IDatabaseRecoveryService>(),
            runtimeState,
            descriptorStore,
            technicalLog);

        var initialized = await coordinator.InitializeAsync();

        Assert.False(initialized);
        Assert.Equal(backup.Id, coordinator.Snapshot.RestorableBackup?.Id);
        runtimeState.AttachOrigin(new Uri("http://127.0.0.1:43210"));

        var restored = await coordinator.RestoreAsync();

        Assert.True(restored);
        Assert.True(runtimeState.IsReady);
        Assert.False(coordinator.Snapshot.RequiresRecovery);
        Assert.Equal(BackupVerificationStatus.Verified, (await backupService.VerifyAsync(backup.Id)).Status);
        var artifactDirectory = Assert.Single(Directory.EnumerateDirectories(paths.RecoveryArtifactsDirectory));
        Assert.Equal(
            new byte[128],
            await File.ReadAllBytesAsync(Path.Combine(artifactDirectory, "servanda.db")));
        await using var restoredConnection = new SqliteConnection($"Data Source={paths.DatabasePath};Mode=ReadOnly;Pooling=False");
        await restoredConnection.OpenAsync();
        await using var restoredCommand = restoredConnection.CreateCommand();
        restoredCommand.CommandText = "SELECT name FROM areas WHERE module_key = 'home';";
        Assert.Equal("Stan z kopii", await restoredCommand.ExecuteScalarAsync());
        Assert.Contains("RECOVERY_RESTORE_SUCCEEDED", await File.ReadAllTextAsync(paths.TechnicalLogPath));
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

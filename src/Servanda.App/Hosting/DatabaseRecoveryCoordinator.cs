using System.Runtime.Versioning;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Diagnostics;
using Servanda.Infrastructure.Runtime;

namespace Servanda.App.Hosting;

[SupportedOSPlatform("linux")]
public sealed class DatabaseRecoveryCoordinator(
    IServiceProvider services,
    ServandaPaths paths,
    TimeProvider timeProvider,
    InstanceRuntimeState runtimeState,
    AtomicInstanceDescriptorStore descriptorStore,
    TechnicalLogWriter technicalLog) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private RecoverySnapshot _snapshot = RecoverySnapshot.Starting;

    public RecoverySnapshot Snapshot => Volatile.Read(ref _snapshot);

    public async Task<bool> InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        var wasRecovery = runtimeState.IsRecovery;
        if (wasRecovery)
        {
            Volatile.Write(ref _snapshot, Snapshot with { IsRetrying = true });
        }

        try
        {
            await ServandaDatabase.InitializeAsync(services, paths, timeProvider, cancellationToken);
            runtimeState.MarkDatabaseReady();
            Volatile.Write(ref _snapshot, RecoverySnapshot.Ready);
            if (runtimeState.HasOrigin)
            {
                await descriptorStore.PublishAsync(runtimeState.CreateDescriptor(), CancellationToken.None);
                await technicalLog.WriteAsync(TechnicalEvent.RecoveryRetrySucceeded, CancellationToken.None);
            }

            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DatabaseInitializationException exception)
        {
            runtimeState.MarkRecovery();
            Volatile.Write(
                ref _snapshot,
                RecoverySnapshot.Failed(exception.Failure, exception.BackupState));
            if (runtimeState.HasOrigin)
            {
                await descriptorStore.PublishAsync(runtimeState.CreateDescriptor(), CancellationToken.None);
                await technicalLog.WriteAsync(TechnicalEvent.RecoveryRetryFailed, CancellationToken.None);
            }

            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}

public sealed record RecoverySnapshot(
    bool RequiresRecovery,
    bool IsRetrying,
    DatabaseInitializationFailure? Failure,
    ProtectionBackupState BackupState)
{
    public static RecoverySnapshot Starting { get; } = new(false, false, null, ProtectionBackupState.NotCreated);

    public static RecoverySnapshot Ready { get; } = new(false, false, null, ProtectionBackupState.NotCreated);

    public static RecoverySnapshot Failed(
        DatabaseInitializationFailure failure,
        ProtectionBackupState backupState) => new(true, false, failure, backupState);
}

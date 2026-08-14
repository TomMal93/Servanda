using System.Runtime.Versioning;
using Servanda.Application.DataProtection;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Diagnostics;
using Servanda.Infrastructure.Runtime;

namespace Servanda.App.Hosting;

[SupportedOSPlatform("linux")]
public sealed class DatabaseRecoveryCoordinator(
    IServiceProvider services,
    ServandaPaths paths,
    TimeProvider timeProvider,
    IDatabaseRecoveryService databaseRecovery,
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

        var isRetry = runtimeState.IsRecovery;
        if (isRetry)
        {
            Volatile.Write(ref _snapshot, Snapshot with { IsRetrying = true });
        }

        try
        {
            return await InitializeCoreAsync(
                isRetry ? TechnicalEvent.RecoveryRetrySucceeded : null,
                isRetry ? TechnicalEvent.RecoveryRetryFailed : null,
                cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<bool> RestoreAsync(CancellationToken cancellationToken = default)
    {
        if (!await _gate.WaitAsync(0, cancellationToken))
        {
            return false;
        }

        try
        {
            var backup = Snapshot.RestorableBackup;
            if (!runtimeState.IsRecovery || backup is null)
            {
                return false;
            }

            Volatile.Write(ref _snapshot, Snapshot with { IsRestoring = true });
            var restore = await databaseRecovery.RestoreAsync(backup.Id, cancellationToken);
            if (restore.Status != DatabaseRestoreStatus.Restored)
            {
                await PublishRestoreFailureAsync(cancellationToken);
                return false;
            }

            return await InitializeCoreAsync(
                TechnicalEvent.RecoveryRestoreSucceeded,
                TechnicalEvent.RecoveryRestoreFailed,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            await PublishRestoreFailureAsync(cancellationToken);
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> InitializeCoreAsync(
        TechnicalEvent? successEvent,
        TechnicalEvent? failureEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            await ServandaDatabase.InitializeAsync(services, paths, timeProvider, cancellationToken);
            runtimeState.MarkDatabaseReady();
            Volatile.Write(ref _snapshot, RecoverySnapshot.Ready);
            if (runtimeState.HasOrigin)
            {
                await descriptorStore.PublishAsync(runtimeState.CreateDescriptor(), CancellationToken.None);
                if (successEvent is { } value)
                {
                    await technicalLog.WriteAsync(value, CancellationToken.None);
                }
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
            var backup = await FindLatestBackupAsync(cancellationToken);
            Volatile.Write(
                ref _snapshot,
                RecoverySnapshot.Failed(exception.Failure, exception.BackupState, backup));
            if (runtimeState.HasOrigin)
            {
                await descriptorStore.PublishAsync(runtimeState.CreateDescriptor(), CancellationToken.None);
                if (failureEvent is { } value)
                {
                    await technicalLog.WriteAsync(value, CancellationToken.None);
                }
            }

            return false;
        }
    }

    private async Task PublishRestoreFailureAsync(CancellationToken cancellationToken)
    {
        var backup = await FindLatestBackupAsync(cancellationToken);
        Volatile.Write(ref _snapshot, Snapshot with
        {
            IsRestoring = false,
            RestorableBackup = backup,
        });
        if (runtimeState.HasOrigin)
        {
            await technicalLog.WriteAsync(TechnicalEvent.RecoveryRestoreFailed, CancellationToken.None);
        }
    }

    private async Task<BackupInfo?> FindLatestBackupAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await databaseRecovery.FindLatestVerifiedBackupAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or InvalidOperationException)
        {
            return null;
        }
    }

    public void Dispose() => _gate.Dispose();
}

public sealed record RecoverySnapshot(
    bool RequiresRecovery,
    bool IsRetrying,
    bool IsRestoring,
    DatabaseInitializationFailure? Failure,
    ProtectionBackupState BackupState,
    BackupInfo? RestorableBackup)
{
    public static RecoverySnapshot Starting { get; } = new(
        false,
        false,
        false,
        null,
        ProtectionBackupState.NotCreated,
        null);

    public static RecoverySnapshot Ready { get; } = Starting;

    public static RecoverySnapshot Failed(
        DatabaseInitializationFailure failure,
        ProtectionBackupState backupState,
        BackupInfo? restorableBackup) => new(true, false, false, failure, backupState, restorableBackup);
}

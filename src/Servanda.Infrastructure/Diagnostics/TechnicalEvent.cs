namespace Servanda.Infrastructure.Diagnostics;

public enum TechnicalEvent
{
    HostStarting,
    HostReady,
    HostRecovery,
    RecoveryRetrySucceeded,
    RecoveryRetryFailed,
    RecoveryRestoreSucceeded,
    RecoveryRestoreFailed,
    HostStartFailed,
    HostStopped,
}

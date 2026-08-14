namespace Servanda.Infrastructure.Diagnostics;

public enum TechnicalEvent
{
    HostStarting,
    HostReady,
    HostRecovery,
    RecoveryRetrySucceeded,
    RecoveryRetryFailed,
    HostStartFailed,
    HostStopped,
}

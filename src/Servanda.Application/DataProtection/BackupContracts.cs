namespace Servanda.Application.DataProtection;

public enum BackupReason
{
    Manual,
    Migration,
    Import,
    CollectionReset,
    BulkDataOperation,
}

public sealed record BackupInfo(
    string Id,
    string SchemaVersion,
    string ApplicationVersion,
    DateTimeOffset CreatedAt,
    BackupReason Reason);

public enum BackupVerificationStatus
{
    Verified,
    NotFound,
    Invalid,
    Incompatible,
}

public sealed record BackupVerificationResult(
    BackupVerificationStatus Status,
    BackupInfo? Backup = null);

public interface IBackupService
{
    Task<BackupInfo> CreateAsync(
        BackupReason reason,
        CancellationToken cancellationToken = default);

    Task<BackupVerificationResult> VerifyAsync(
        string backupId,
        CancellationToken cancellationToken = default);

    Task ApplyRetentionAsync(CancellationToken cancellationToken = default);
}

public enum DatabaseRestoreStatus
{
    Restored,
    BackupNotFound,
    BackupInvalid,
    BackupIncompatible,
    Failed,
}

public sealed record DatabaseRestoreResult(DatabaseRestoreStatus Status);

public interface IDatabaseRecoveryService
{
    Task<BackupInfo?> FindLatestVerifiedBackupAsync(CancellationToken cancellationToken = default);

    Task<DatabaseRestoreResult> RestoreAsync(
        string backupId,
        CancellationToken cancellationToken = default);
}

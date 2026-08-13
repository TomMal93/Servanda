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
}

namespace Servanda.Infrastructure.Data.Backups;

internal sealed record BackupMetadata(
    int FormatVersion,
    string BackupId,
    string SchemaVersion,
    string ApplicationVersion,
    DateTimeOffset CreatedAt,
    string Reason);

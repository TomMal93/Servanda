namespace Servanda.Infrastructure.Data;

public enum DatabaseInitializationFailure
{
    DatabaseAccess,
    ProtectionBackup,
    Migration,
}

public enum ProtectionBackupState
{
    NotCreated,
    Verified,
}

public sealed class DatabaseInitializationException : Exception
{
    public DatabaseInitializationException(
        DatabaseInitializationFailure failure,
        ProtectionBackupState backupState,
        Exception innerException)
        : base("Nie udało się przygotować magazynu danych Servandy.", innerException)
    {
        Failure = failure;
        BackupState = backupState;
    }

    public DatabaseInitializationFailure Failure { get; }

    public ProtectionBackupState BackupState { get; }
}

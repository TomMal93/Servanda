using System.Runtime.Versioning;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using Servanda.Application.DataProtection;
using Servanda.Domain.Areas;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Data.Backups;

[SupportedOSPlatform("linux")]
internal sealed class SqliteRecoveryService(
    ServandaPaths paths,
    TimeProvider timeProvider,
    SqliteBackupService backupService) : IDatabaseRecoveryService
{
    private static readonly string[] DatabaseSidecarSuffixes = ["-wal", "-shm", "-journal"];
    private readonly uint _effectiveUserId = LinuxIdentity.GetEffectiveUserId();

    public async Task<BackupInfo?> FindLatestVerifiedBackupAsync(
        CancellationToken cancellationToken = default)
    {
        LinuxIdentity.EnsureLinux();
        if (!Directory.Exists(paths.BackupsDirectory))
        {
            return null;
        }

        try
        {
            PrivateFileSystem.EnsureDirectory(paths.BackupsDirectory, _effectiveUserId);
            BackupInfo? latest = null;
            foreach (var directory in Directory.EnumerateDirectories(paths.BackupsDirectory))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var backupId = Path.GetFileName(Path.TrimEndingDirectorySeparator(directory));
                var verification = await backupService.VerifyAsync(backupId, cancellationToken);
                if (verification is not { Status: BackupVerificationStatus.Verified, Backup: { } backup })
                {
                    continue;
                }

                if (latest is null
                    || backup.CreatedAt > latest.CreatedAt
                    || backup.CreatedAt == latest.CreatedAt
                        && string.CompareOrdinal(backup.Id, latest.Id) > 0)
                {
                    latest = backup;
                }
            }

            return latest;
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

    public async Task<DatabaseRestoreResult> RestoreAsync(
        string backupId,
        CancellationToken cancellationToken = default)
    {
        LinuxIdentity.EnsureLinux();
        var verification = await backupService.VerifyAsync(backupId, cancellationToken);
        if (verification.Status != BackupVerificationStatus.Verified || verification.Backup is null)
        {
            return new DatabaseRestoreResult(verification.Status switch
            {
                BackupVerificationStatus.NotFound => DatabaseRestoreStatus.BackupNotFound,
                BackupVerificationStatus.Incompatible => DatabaseRestoreStatus.BackupIncompatible,
                _ => DatabaseRestoreStatus.BackupInvalid,
            });
        }

        var operationId = EntityId.NewUlid(timeProvider);
        var stagedDatabasePath = Path.Combine(paths.DataDirectory, $".{operationId}.restore.tmp");
        try
        {
            SqliteConnection.ClearAllPools();
            await CreateVerifiedRestoreCandidateAsync(
                backupService.GetBackupDatabasePath(backupId),
                stagedDatabasePath,
                verification.Backup,
                cancellationToken);
            await PreserveFailedDatabaseAsync(operationId, cancellationToken);
            RemoveDatabaseSidecars();
            File.Move(stagedDatabasePath, paths.DatabasePath, overwrite: true);
            PrivateFileSystem.VerifyPrivateFile(paths.DatabasePath, _effectiveUserId);
            if (!await backupService.VerifyRestoredDatabaseAsync(
                    paths.DatabasePath,
                    verification.Backup,
                    cancellationToken))
            {
                return new DatabaseRestoreResult(DatabaseRestoreStatus.Failed);
            }

            return new DatabaseRestoreResult(DatabaseRestoreStatus.Restored);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (exception is IOException
            or UnauthorizedAccessException
            or SqliteException
            or InvalidOperationException
            or ArgumentException)
        {
            return new DatabaseRestoreResult(DatabaseRestoreStatus.Failed);
        }
        finally
        {
            if (File.Exists(stagedDatabasePath))
            {
                PrivateFileSystem.VerifyPrivateFile(stagedDatabasePath, _effectiveUserId);
                File.Delete(stagedDatabasePath);
            }
        }
    }

    private async Task CreateVerifiedRestoreCandidateAsync(
        string sourcePath,
        string destinationPath,
        BackupInfo backup,
        CancellationToken cancellationToken)
    {
        PrivateFileSystem.VerifyPrivateFile(sourcePath, _effectiveUserId);
        CreatePrivateFile(destinationPath);
        await using (var source = new SqliteConnection(CreateConnectionString(sourcePath, SqliteOpenMode.ReadOnly)))
        await using (var destination = new SqliteConnection(
            CreateConnectionString(destinationPath, SqliteOpenMode.ReadWrite)))
        {
            await source.OpenAsync(cancellationToken);
            await destination.OpenAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            source.BackupDatabase(destination);
        }

        if (!await backupService.VerifyRestoredDatabaseAsync(destinationPath, backup, cancellationToken))
        {
            throw new InvalidDataException("Kandydat odtworzenia nie przeszedł ponownej weryfikacji.");
        }
    }

    private async Task PreserveFailedDatabaseAsync(
        string operationId,
        CancellationToken cancellationToken)
    {
        PrivateFileSystem.EnsureDirectory(paths.RecoveryArtifactsDirectory, _effectiveUserId);
        var stagingDirectory = Path.Combine(paths.RecoveryArtifactsDirectory, $".{operationId}.tmp");
        var publishedDirectory = Path.Combine(paths.RecoveryArtifactsDirectory, $"failed-{operationId}");
        Directory.CreateDirectory(stagingDirectory, PrivateDirectoryMode);
        PrivateFileSystem.EnsureDirectory(stagingDirectory, _effectiveUserId);
        var published = false;
        try
        {
            if (!File.Exists(paths.DatabasePath))
            {
                throw new FileNotFoundException("Brak bazy wymagającej zachowania.", paths.DatabasePath);
            }

            await CopyAndVerifyAsync(
                paths.DatabasePath,
                Path.Combine(stagingDirectory, Path.GetFileName(paths.DatabasePath)),
                cancellationToken);
            foreach (var suffix in DatabaseSidecarSuffixes)
            {
                var sourcePath = paths.DatabasePath + suffix;
                if (File.Exists(sourcePath))
                {
                    await CopyAndVerifyAsync(
                        sourcePath,
                        Path.Combine(stagingDirectory, Path.GetFileName(sourcePath)),
                        cancellationToken);
                }
            }

            Directory.Move(stagingDirectory, publishedDirectory);
            published = true;
        }
        finally
        {
            if (!published && Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }

    private async Task CopyAndVerifyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        PrivateFileSystem.VerifyPrivateFile(sourcePath, _effectiveUserId);
        await using (var source = new FileStream(sourcePath, new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = 81920,
            Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
        }))
        await using (var destination = new FileStream(destinationPath, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 81920,
            Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
            UnixCreateMode = PrivateFileMode,
        }))
        {
            await source.CopyToAsync(destination, cancellationToken);
            await destination.FlushAsync(cancellationToken);
        }

        PrivateFileSystem.VerifyPrivateFile(destinationPath, _effectiveUserId);
        await using var sourceForHash = File.OpenRead(sourcePath);
        await using var destinationForHash = File.OpenRead(destinationPath);
        var sourceHash = await SHA256.HashDataAsync(sourceForHash, cancellationToken);
        var destinationHash = await SHA256.HashDataAsync(destinationForHash, cancellationToken);
        if (!CryptographicOperations.FixedTimeEquals(sourceHash, destinationHash))
        {
            throw new IOException("Artefakt diagnostyczny nie jest zgodny z zachowaną bazą.");
        }
    }

    private void RemoveDatabaseSidecars()
    {
        foreach (var suffix in DatabaseSidecarSuffixes)
        {
            var sidecarPath = paths.DatabasePath + suffix;
            if (!File.Exists(sidecarPath))
            {
                continue;
            }

            PrivateFileSystem.VerifyPrivateFile(sidecarPath, _effectiveUserId);
            File.Delete(sidecarPath);
        }
    }

    private static string CreateConnectionString(string path, SqliteOpenMode mode) =>
        new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = mode,
            ForeignKeys = true,
            Pooling = false,
        }.ToString();

    private static void CreatePrivateFile(string path)
    {
        using var stream = new FileStream(path, new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.ReadWrite,
            Share = FileShare.None,
            BufferSize = 1,
            Options = FileOptions.None,
            UnixCreateMode = PrivateFileMode,
        });
    }

    private const UnixFileMode PrivateDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
}

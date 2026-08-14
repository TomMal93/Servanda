using Servanda.Application.DataProtection;

namespace Servanda.Infrastructure.Data.Backups;

internal static class BackupRetentionPolicy
{
    internal const int RecentBackupCount = 10;
    internal const int DailyRetentionDays = 30;

    internal static IReadOnlySet<string> SelectRemovalCandidates(
        IEnumerable<BackupInfo> verifiedBackups,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(verifiedBackups);

        var nowUtc = now.ToUniversalTime();
        var automaticBackups = verifiedBackups
            .Where(backup => backup.Reason != BackupReason.Manual)
            .GroupBy(backup => backup.Id, StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(backup => backup.CreatedAt)
                .First())
            .OrderByDescending(backup => backup.CreatedAt)
            .ThenByDescending(backup => backup.Id, StringComparer.Ordinal)
            .ToList();
        var retainedIds = automaticBackups
            .Where(backup => backup.CreatedAt <= nowUtc)
            .Take(RecentBackupCount)
            .Select(backup => backup.Id)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var futureBackup in automaticBackups.Where(backup => backup.CreatedAt > nowUtc))
        {
            retainedIds.Add(futureBackup.Id);
        }

        var firstRetainedDate = nowUtc.UtcDateTime.Date.AddDays(-(DailyRetentionDays - 1));
        var currentDate = nowUtc.UtcDateTime.Date;
        foreach (var dailyBackup in automaticBackups
                     .Where(backup =>
                     {
                         var createdDate = backup.CreatedAt.UtcDateTime.Date;
                         return createdDate >= firstRetainedDate && createdDate <= currentDate;
                     })
                     .GroupBy(backup => backup.CreatedAt.UtcDateTime.Date)
                     .Select(group => group
                         .OrderByDescending(backup => backup.CreatedAt)
                         .ThenByDescending(backup => backup.Id, StringComparer.Ordinal)
                         .First()))
        {
            retainedIds.Add(dailyBackup.Id);
        }

        return automaticBackups
            .Where(backup => !retainedIds.Contains(backup.Id))
            .Select(backup => backup.Id)
            .ToHashSet(StringComparer.Ordinal);
    }
}

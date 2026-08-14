using Servanda.Application.DataProtection;
using Servanda.Infrastructure.Data.Backups;

namespace Servanda.Infrastructure.Tests.Data;

public sealed class BackupRetentionPolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SelectRemovalCandidatesKeepsRecentDailyAndManualBackups()
    {
        var backups = new List<BackupInfo>();
        var todayBackups = Enumerable.Range(0, 12)
            .Select(index => CreateBackup($"today-{index:00}", Now.AddHours(-2).AddMinutes(index)))
            .ToList();
        backups.AddRange(todayBackups);

        var dailyPairs = Enumerable.Range(1, 29)
            .Select(daysAgo => new
            {
                Older = CreateBackup($"daily-{daysAgo:00}-older", AtUtcDay(daysAgo, 8)),
                Newer = CreateBackup($"daily-{daysAgo:00}-newer", AtUtcDay(daysAgo, 18)),
            })
            .ToList();
        backups.AddRange(dailyPairs.SelectMany(pair => new[] { pair.Older, pair.Newer }));
        var outsideWindow = CreateBackup("outside-window", Now.AddDays(-30));
        var manual = CreateBackup("manual", Now.AddYears(-1), BackupReason.Manual);
        backups.Add(outsideWindow);
        backups.Add(manual);

        var candidates = BackupRetentionPolicy.SelectRemovalCandidates(backups, Now);

        Assert.DoesNotContain(manual.Id, candidates);
        Assert.Contains(outsideWindow.Id, candidates);
        Assert.Equal(
            todayBackups
                .OrderByDescending(backup => backup.CreatedAt)
                .Skip(10)
                .Select(backup => backup.Id)
                .ToHashSet(StringComparer.Ordinal),
            todayBackups
                .Where(backup => candidates.Contains(backup.Id))
                .Select(backup => backup.Id)
                .ToHashSet(StringComparer.Ordinal));
        Assert.All(dailyPairs, pair =>
        {
            Assert.Contains(pair.Older.Id, candidates);
            Assert.DoesNotContain(pair.Newer.Id, candidates);
        });
    }

    [Fact]
    public void SelectRemovalCandidatesKeepsAllFutureDatedBackups()
    {
        var backups = Enumerable.Range(1, 20)
            .Select(index => CreateBackup($"future-{index:00}", Now.AddDays(index)))
            .ToList();

        var candidates = BackupRetentionPolicy.SelectRemovalCandidates(backups, Now);

        Assert.Empty(candidates);
    }

    private static BackupInfo CreateBackup(
        string id,
        DateTimeOffset createdAt,
        BackupReason reason = BackupReason.Migration) =>
        new(id, "schema", "version", createdAt, reason);

    private static DateTimeOffset AtUtcDay(int daysAgo, int hour) =>
        new(Now.UtcDateTime.Date.AddDays(-daysAgo).AddHours(hour), TimeSpan.Zero);
}

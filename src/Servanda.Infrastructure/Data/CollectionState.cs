using Microsoft.EntityFrameworkCore;

namespace Servanda.Infrastructure.Data;

/// <summary>
/// Wspólne operacje na epoce kolekcji i zakresach kolejności wymagane przez ADR 0004.
/// </summary>
internal static class CollectionState
{
    public static Task<string> ReadEpochAsync(ServandaDbContext database, CancellationToken cancellationToken) =>
        database.AppState
            .AsNoTracking()
            .Where(item => item.Id == 1)
            .Select(item => item.ContentEpoch)
            .SingleAsync(cancellationToken);

    public static async Task<long?> ReadScopeRevisionAsync(
        ServandaDbContext database,
        string scopeKey,
        CancellationToken cancellationToken)
    {
        var revisions = await database.OrderingScopes
            .AsNoTracking()
            .Where(scope => scope.ScopeKey == scopeKey)
            .Select(scope => (long?)scope.Revision)
            .ToListAsync(cancellationToken);
        return revisions.Count == 1 ? revisions[0] : null;
    }

    public static async Task<IReadOnlyDictionary<string, long>> ReadScopeRevisionsAsync(
        ServandaDbContext database,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        var scopes = await database.OrderingScopes
            .AsNoTracking()
            .Where(scope => scopeKeys.Contains(scope.ScopeKey))
            .Select(scope => new { scope.ScopeKey, scope.Revision })
            .ToListAsync(cancellationToken);
        return scopes.ToDictionary(scope => scope.ScopeKey, scope => scope.Revision, StringComparer.Ordinal);
    }

    /// <summary>
    /// Zwiększa rewizję zakresu wyłącznie wtedy, gdy zgadza się z oczekiwaną wartością komendy.
    /// </summary>
    public static async Task<bool> TryAdvanceScopeAsync(
        ServandaDbContext database,
        string scopeKey,
        long expectedRevision,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var changed = await database.OrderingScopes
            .Where(scope => scope.ScopeKey == scopeKey && scope.Revision == expectedRevision)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(scope => scope.Revision, scope => scope.Revision + 1)
                    .SetProperty(scope => scope.UpdatedAt, timestamp),
                cancellationToken);
        return changed == 1;
    }

    public static async Task EnsureScopesAsync(
        ServandaDbContext database,
        IReadOnlyCollection<string> scopeKeys,
        DateTimeOffset timestamp,
        CancellationToken cancellationToken)
    {
        var existing = await database.OrderingScopes
            .AsNoTracking()
            .Where(scope => scopeKeys.Contains(scope.ScopeKey))
            .Select(scope => scope.ScopeKey)
            .ToListAsync(cancellationToken);
        foreach (var missing in scopeKeys.Except(existing, StringComparer.Ordinal))
        {
            database.OrderingScopes.Add(new OrderingScope
            {
                ScopeKey = missing,
                Revision = 1,
                UpdatedAt = timestamp,
            });
        }

        await database.SaveChangesAsync(cancellationToken);
    }

    public static async Task RemoveScopesAsync(
        ServandaDbContext database,
        IReadOnlyCollection<string> scopeKeys,
        CancellationToken cancellationToken)
    {
        await database.OrderingScopes
            .Where(scope => scopeKeys.Contains(scope.ScopeKey))
            .ExecuteDeleteAsync(cancellationToken);
    }

    /// <summary>
    /// Nowa epoka unieważnia sesje edycji otwarte przed zastąpieniem kolekcji.
    /// </summary>
    public static async Task SetEpochAsync(
        ServandaDbContext database,
        string epoch,
        CancellationToken cancellationToken)
    {
        await database.AppState
            .Where(item => item.Id == 1)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(item => item.ContentEpoch, epoch),
                cancellationToken);
    }
}

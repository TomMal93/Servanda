using Microsoft.EntityFrameworkCore;
using Servanda.Domain.Areas;

namespace Servanda.Infrastructure.Data;

internal static class InitialAreaSeed
{
    private static readonly SeedArea[] Areas =
    [
        new("01J00000000000000000000001", "Skarbiec promptów", "Przechowywanie, przygotowywanie i ponowne używanie promptów.", "prompts", "accent-0", "prompts"),
        new("01J00000000000000000000002", "Przechowalnia narzędzi", "Katalog sprawdzonych stron i aplikacji przydatnych na co dzień.", "tools", "accent-1", "tools"),
        new("01J00000000000000000000003", "Dom", "Harmonogram prac porządkowych i innych obowiązków domowych.", "home", "accent-2", "home"),
        new("01J00000000000000000000004", "Rodzina", "Ważne informacje, potrzeby, daty i relacje dotyczące bliskich.", "family", "accent-3", "family"),
        new("01J00000000000000000000005", "Witalność", "Zdrowie, biohacking, dieta i trening w jednym uporządkowanym miejscu.", "vitality", "accent-4", "vitality"),
        new("01J00000000000000000000006", "Przechowalnia notatek", "Pomysły, obserwacje i informacje zachowane do późniejszego użycia.", "notes", "accent-5", "notes"),
        new("01J00000000000000000000007", "Budżet domowy", "Planowanie miesięcznego budżetu gospodarstwa domowego.", "budget", "accent-0", "budget"),
    ];

    public static async Task ApplyAsync(
        ServandaDbContext database,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var timestamp = timeProvider.GetUtcNow();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);

        if (!await database.AppState.AnyAsync(cancellationToken))
        {
            database.AppState.Add(new AppState
            {
                Id = 1,
                ContentEpoch = EntityId.NewUlid(timeProvider),
            });
        }

        if (!await database.OrderingScopes.AnyAsync(
                item => item.ScopeKey == "areas",
                cancellationToken))
        {
            database.OrderingScopes.Add(new OrderingScope
            {
                ScopeKey = "areas",
                Revision = 1,
                UpdatedAt = timestamp,
            });
        }

        if (!await database.Areas.AnyAsync(cancellationToken))
        {
            for (var index = 0; index < Areas.Length; index++)
            {
                var seed = Areas[index];
                database.Areas.Add(Area.CreateSeed(
                    seed.Id,
                    seed.Name,
                    seed.Description,
                    seed.IconKey,
                    seed.AccentKey,
                    seed.ModuleKey,
                    index,
                    timestamp));
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private sealed record SeedArea(
        string Id,
        string Name,
        string Description,
        string IconKey,
        string AccentKey,
        string ModuleKey);
}

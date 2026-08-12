using Servanda.App.Security;

namespace Servanda.E2E.Security;

public sealed class BootstrapTicketStoreTests
{
    [Fact]
    public void IssuedTicketCanBeConsumedOnlyOnce()
    {
        var store = new BootstrapTicketStore(new ManualTimeProvider());
        var ticket = store.Issue();

        Assert.True(store.TryConsume(ticket));
        Assert.False(store.TryConsume(ticket));
    }

    [Fact]
    public void ExpiredTicketCannotBeConsumed()
    {
        var timeProvider = new ManualTimeProvider();
        var store = new BootstrapTicketStore(timeProvider);
        var ticket = store.Issue();

        timeProvider.Advance(BootstrapTicketStore.Lifetime + TimeSpan.FromMilliseconds(1));

        Assert.False(store.TryConsume(ticket));
    }

    [Fact]
    public async Task RateLimitedExchangeDoesNotConsumeTicket()
    {
        var store = new BootstrapTicketStore(new ManualTimeProvider());
        var ticket = store.Issue();
        await using var exhaustedLimiter = new BootstrapRateLimiter();
        for (var attempt = 0; attempt < BootstrapRateLimiter.PermitLimit; attempt++)
        {
            using var lease = exhaustedLimiter.AttemptSessionBootstrap();
            Assert.True(lease.IsAcquired);
        }

        Assert.False(store.TryConsume(ticket, () =>
        {
            using var lease = exhaustedLimiter.AttemptSessionBootstrap();
            return lease.IsAcquired;
        }));

        await using var replenishedLimiter = new BootstrapRateLimiter();
        Assert.True(store.TryConsume(ticket, () =>
        {
            using var lease = replenishedLimiter.AttemptSessionBootstrap();
            return lease.IsAcquired;
        }));

        Assert.False(store.TryConsume(ticket));
    }
}

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
}

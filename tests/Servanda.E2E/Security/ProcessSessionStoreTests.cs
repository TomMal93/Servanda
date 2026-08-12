using Servanda.App.Security;

namespace Servanda.E2E.Security;

public sealed class ProcessSessionStoreTests
{
    [Fact]
    public void CreatedSessionIsValidOnlyForThisProcessStore()
    {
        var timeProvider = new ManualTimeProvider();
        var store = new ProcessSessionStore(timeProvider);
        var session = store.Create();

        Assert.True(store.IsValid(session));
        Assert.False(store.IsValid("invalid-session"));
        Assert.False(new ProcessSessionStore(timeProvider).IsValid(session));
    }

    [Fact]
    public void SessionExpiresAfterIdleLifetime()
    {
        var timeProvider = new ManualTimeProvider();
        var store = new ProcessSessionStore(timeProvider);
        var session = store.Create();

        timeProvider.Advance(ProcessSessionStore.IdleLifetime + TimeSpan.FromMilliseconds(1));

        Assert.False(store.IsValid(session));
    }

    [Fact]
    public void ValidatingSessionExtendsIdleLifetime()
    {
        var timeProvider = new ManualTimeProvider();
        var store = new ProcessSessionStore(timeProvider);
        var session = store.Create();

        timeProvider.Advance(ProcessSessionStore.IdleLifetime - TimeSpan.FromHours(1));
        Assert.True(store.IsValid(session));
        timeProvider.Advance(ProcessSessionStore.IdleLifetime - TimeSpan.FromHours(1));

        Assert.True(store.IsValid(session));
    }

    [Fact]
    public void CreatingSessionBeyondLimitEvictsOldestSession()
    {
        var timeProvider = new ManualTimeProvider();
        var store = new ProcessSessionStore(timeProvider);
        var oldestSession = store.Create();
        string newestSession = oldestSession;

        for (var index = 0; index < ProcessSessionStore.MaximumSessions; index++)
        {
            timeProvider.Advance(TimeSpan.FromMilliseconds(1));
            newestSession = store.Create();
        }

        Assert.False(store.IsValid(oldestSession));
        Assert.True(store.IsValid(newestSession));
    }
}

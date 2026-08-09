using Servanda.App.Security;

namespace Servanda.E2E.Security;

public sealed class ProcessSessionStoreTests
{
    [Fact]
    public void CreatedSessionIsValidOnlyForThisProcessStore()
    {
        var store = new ProcessSessionStore();
        var session = store.Create();

        Assert.True(store.IsValid(session));
        Assert.False(store.IsValid("invalid-session"));
        Assert.False(new ProcessSessionStore().IsValid(session));
    }
}

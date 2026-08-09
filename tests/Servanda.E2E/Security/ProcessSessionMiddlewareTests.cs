using Microsoft.AspNetCore.Http;
using Servanda.App.Hosting;
using Servanda.App.Security;

namespace Servanda.E2E.Security;

public sealed class ProcessSessionMiddlewareTests
{
    private static readonly Uri Origin = new("http://127.0.0.1:43210");

    [Fact]
    public async Task ApplicationRouteWithoutSessionIsRejected()
    {
        var nextCalled = false;
        var middleware = new ProcessSessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("GET", "/");

        await middleware.InvokeAsync(context, CreateRuntimeState(), new ProcessSessionStore());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status401Unauthorized, context.Response.StatusCode);
    }

    [Fact]
    public async Task PublicBootstrapRouteDoesNotRequireSession()
    {
        var nextCalled = false;
        var middleware = new ProcessSessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("GET", "/bootstrap");

        await middleware.InvokeAsync(context, CreateRuntimeState(), new ProcessSessionStore());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task LauncherTicketRequestDoesNotRequireBrowserOrigin()
    {
        var nextCalled = false;
        var middleware = new ProcessSessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = CreateContext("POST", "/launcher/ticket");

        await middleware.InvokeAsync(context, CreateRuntimeState(), new ProcessSessionStore());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task StatefulRequestWithForeignOriginIsRejectedBeforeSession()
    {
        var middleware = new ProcessSessionMiddleware(_ => Task.CompletedTask);
        var context = CreateContext("POST", "/session/bootstrap");
        context.Request.Headers.Origin = "https://example.com";

        await middleware.InvokeAsync(context, CreateRuntimeState(), new ProcessSessionStore());

        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task ApplicationRouteWithValidSessionContinues()
    {
        var nextCalled = false;
        var middleware = new ProcessSessionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var sessions = new ProcessSessionStore();
        var session = sessions.Create();
        var context = CreateContext("GET", "/");
        context.Request.Headers.Cookie = $"{ProcessSessionStore.CookieName}={session}";

        await middleware.InvokeAsync(context, CreateRuntimeState(), sessions);

        Assert.True(nextCalled);
    }

    private static DefaultHttpContext CreateContext(string method, string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.Request.Host = new HostString(Origin.Authority);
        context.Response.Body = new MemoryStream();
        return context;
    }

    private static InstanceRuntimeState CreateRuntimeState()
    {
        var state = new InstanceRuntimeState();
        state.MarkReady(Origin);
        return state;
    }
}

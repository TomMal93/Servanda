using Microsoft.AspNetCore.Http;
using Servanda.App.Hosting;
using Servanda.App.Security;

namespace Servanda.E2E.Security;

public sealed class RecoveryModeMiddlewareTests
{
    private static readonly Uri Origin = new("http://127.0.0.1:43210");

    [Fact]
    public async Task RecoveryRedirectsNormalGetToRecoveryPage()
    {
        var nextCalled = false;
        var middleware = new RecoveryModeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/manage-areas";

        await middleware.InvokeAsync(context, CreateRecoveryState());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/recovery", context.Response.Headers.Location);
    }

    [Fact]
    public async Task RecoveryBlocksNormalStateChangingRequest()
    {
        var middleware = new RecoveryModeMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/shutdown";

        await middleware.InvokeAsync(context, CreateRecoveryState());

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Theory]
    [InlineData("/recovery")]
    [InlineData("/recovery/retry")]
    [InlineData("/recovery/restore")]
    [InlineData("/_blazor")]
    [InlineData("/instance")]
    public async Task RecoveryAllowsOnlyRecoveryInfrastructure(string path)
    {
        var nextCalled = false;
        var middleware = new RecoveryModeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Method = path is "/recovery/retry" or "/recovery/restore"
            ? HttpMethods.Post
            : HttpMethods.Get;
        context.Request.Path = path;

        await middleware.InvokeAsync(context, CreateRecoveryState());

        Assert.True(nextCalled);
    }

    [Theory]
    [InlineData("/launcher/ticket")]
    [InlineData("/session/bootstrap")]
    public async Task RecoveryAllowsSessionBootstrapInfrastructure(string path)
    {
        var nextCalled = false;
        var middleware = new RecoveryModeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = path;

        await middleware.InvokeAsync(context, CreateRecoveryState());

        Assert.True(nextCalled);
    }

    [Fact]
    public async Task RecoveryDoesNotAllowPostToStaticAsset()
    {
        var nextCalled = false;
        var middleware = new RecoveryModeMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Post;
        context.Request.Path = "/recovery-retry.hash.js";

        await middleware.InvokeAsync(context, CreateRecoveryState());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
    }

    [Fact]
    public async Task ReadyStateRedirectsRecoveryPageToDashboard()
    {
        var middleware = new RecoveryModeMiddleware(_ => Task.CompletedTask);
        var context = new DefaultHttpContext();
        context.Request.Method = HttpMethods.Get;
        context.Request.Path = "/recovery";
        var state = new InstanceRuntimeState();
        state.MarkDatabaseReady();
        state.AttachOrigin(Origin);

        await middleware.InvokeAsync(context, state);

        Assert.Equal(StatusCodes.Status302Found, context.Response.StatusCode);
        Assert.Equal("/", context.Response.Headers.Location);
    }

    private static InstanceRuntimeState CreateRecoveryState()
    {
        var state = new InstanceRuntimeState();
        state.MarkRecovery();
        state.AttachOrigin(Origin);
        return state;
    }
}

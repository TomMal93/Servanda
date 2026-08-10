using Servanda.App.Hosting;

namespace Servanda.App.Security;

public sealed class ProcessSessionMiddleware
{
    private static readonly HashSet<PathString> PublicPaths =
    [
        new("/instance"),
        new("/launcher/ticket"),
        new("/bootstrap"),
        new("/bootstrap.js"),
        new("/app.css"),
        new("/session/bootstrap"),
    ];

    private readonly RequestDelegate _next;

    public ProcessSessionMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(
        HttpContext context,
        InstanceRuntimeState runtimeState,
        ProcessSessionStore sessionStore)
    {
        var isLauncherTicketRequest = context.Request.Path == "/launcher/ticket";
        var isBlazorInitializerRequest = HttpMethods.IsGet(context.Request.Method)
            && context.Request.Path == "/_blazor/initializers";
        var requiresOrigin = !isLauncherTicketRequest
            && !isBlazorInitializerRequest
            && (!HttpMethods.IsGet(context.Request.Method)
                && !HttpMethods.IsHead(context.Request.Method)
                && !HttpMethods.IsOptions(context.Request.Method)
                || context.Request.Path.StartsWithSegments("/_blazor", StringComparison.Ordinal));

        if (requiresOrigin && !HasCanonicalOrigin(context.Request, runtimeState.Origin))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (requiresOrigin
            && string.Equals(
                context.Request.Headers["Sec-Fetch-Site"],
                "cross-site",
                StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return;
        }

        if (PublicPaths.Contains(context.Request.Path))
        {
            await _next(context);
            return;
        }

        context.Request.Cookies.TryGetValue(ProcessSessionStore.CookieName, out var session);
        if (sessionStore.IsValid(session))
        {
            await _next(context);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "text/plain; charset=utf-8";
        await context.Response.WriteAsync("Otwórz Servandę przez launcher.", context.RequestAborted);
    }

    private static bool HasCanonicalOrigin(HttpRequest request, Uri canonicalOrigin) =>
        request.Headers.Origin.Count == 1
        && string.Equals(
            request.Headers.Origin.ToString(),
            canonicalOrigin.GetLeftPart(UriPartial.Authority),
            StringComparison.Ordinal);
}

using Servanda.App.Hosting;

namespace Servanda.App.Security;

public sealed class LocalHostSecurityMiddleware
{
    private readonly RequestDelegate _next;

    public LocalHostSecurityMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, InstanceRuntimeState runtimeState)
    {
        if (!runtimeState.IsReady)
        {
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            return;
        }

        var origin = runtimeState.Origin;
        if (!string.Equals(context.Request.Host.Value, origin.Authority, StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        AddSecurityHeaders(context.Response.Headers, origin);
        await _next(context);
    }

    private static void AddSecurityHeaders(IHeaderDictionary headers, Uri origin)
    {
        var webSocketScheme = origin.Scheme == Uri.UriSchemeHttps ? "wss" : "ws";
        var webSocketOrigin = $"{webSocketScheme}://{origin.Authority}";

        headers.ContentSecurityPolicy =
            $"default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; " +
            $"connect-src '{origin.GetLeftPart(UriPartial.Authority)}' '{webSocketOrigin}'; " +
            "object-src 'none'; base-uri 'none'; frame-ancestors 'none'; form-action 'self'";
        headers.Append("Referrer-Policy", "no-referrer");
        headers.XContentTypeOptions = "nosniff";
        headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=(), clipboard-write=(self)");
    }
}

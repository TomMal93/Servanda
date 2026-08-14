using Servanda.App.Hosting;

namespace Servanda.App.Security;

public sealed class RecoveryModeMiddleware(RequestDelegate next)
{
    private static readonly PathString[] AllowedPrefixes =
    [
        new("/_blazor"),
        new("/_framework"),
    ];

    private static readonly HashSet<PathString> AllowedGetFiles =
    [
        new("/instance"),
        new("/bootstrap"),
        new("/bootstrap.js"),
        new("/app.css"),
        new("/Servanda.styles.css"),
    ];

    private static readonly HashSet<PathString> AllowedPostFiles =
    [
        new("/launcher/ticket"),
        new("/session/bootstrap"),
    ];

    public async Task InvokeAsync(HttpContext context, InstanceRuntimeState runtimeState)
    {
        if (!runtimeState.IsRecovery)
        {
            if (context.Request.Path.StartsWithSegments("/recovery"))
            {
                if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
                {
                    context.Response.Redirect("/");
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status404NotFound;
                }

                return;
            }

            await next(context);
            return;
        }

        var isRecoveryPage = context.Request.Path == "/recovery"
            && (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method));
        var isRecoveryRetry = context.Request.Path == RecoveryEndpoint.RetryPath
            && HttpMethods.IsPost(context.Request.Method);
        if (isRecoveryPage
            || isRecoveryRetry
            || IsAllowedStaticAsset(context.Request)
            || AllowedPrefixes.Any(prefix => context.Request.Path.StartsWithSegments(prefix)))
        {
            await next(context);
            return;
        }

        if (HttpMethods.IsGet(context.Request.Method) || HttpMethods.IsHead(context.Request.Method))
        {
            context.Response.Redirect("/recovery");
            return;
        }

        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
    }

    private static bool IsAllowedStaticAsset(HttpRequest request)
    {
        if (HttpMethods.IsPost(request.Method))
        {
            return AllowedPostFiles.Contains(request.Path);
        }

        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method))
        {
            return false;
        }

        var path = request.Path;
        if (AllowedGetFiles.Contains(path))
        {
            return true;
        }

        var value = path.Value;
        if (value is null)
        {
            return false;
        }

        return value.StartsWith("/app.", StringComparison.Ordinal)
                && value.EndsWith(".css", StringComparison.Ordinal)
            || value.StartsWith("/Servanda.", StringComparison.Ordinal)
                && value.EndsWith(".styles.css", StringComparison.Ordinal)
            || value.StartsWith("/shutdown.", StringComparison.Ordinal)
                && value.EndsWith(".js", StringComparison.Ordinal)
            || value.StartsWith("/recovery-retry.", StringComparison.Ordinal)
                && value.EndsWith(".js", StringComparison.Ordinal)
            || value.StartsWith("/Components/Layout/ReconnectModal.", StringComparison.Ordinal)
                && value.EndsWith(".razor.js", StringComparison.Ordinal);
    }
}

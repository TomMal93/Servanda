using System.Runtime.Versioning;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;

namespace Servanda.App.Hosting;

public static class RecoveryEndpoint
{
    public const string RetryPath = "/recovery/retry";
    public const string RestorePath = "/recovery/restore";

    [SupportedOSPlatform("linux")]
    public static IEndpointConventionBuilder MapRecoveryRetry(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost(RetryPath, async (
            HttpContext context,
            DatabaseRecoveryCoordinator coordinator) =>
        {
            var antiforgeryValidation = context.Features.Get<IAntiforgeryValidationFeature>();
            if (antiforgeryValidation is not { IsValid: true })
            {
                return Results.BadRequest();
            }

            var succeeded = await coordinator.InitializeAsync(context.RequestAborted);
            return CreateResult(context.Request, succeeded ? "/" : "/recovery?retry=failed");
        })
            .WithMetadata(new RequireAntiforgeryTokenAttribute(true))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));
    }

    [SupportedOSPlatform("linux")]
    public static IEndpointConventionBuilder MapRecoveryRestore(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost(RestorePath, async (
            HttpContext context,
            DatabaseRecoveryCoordinator coordinator) =>
        {
            var antiforgeryValidation = context.Features.Get<IAntiforgeryValidationFeature>();
            if (antiforgeryValidation is not { IsValid: true })
            {
                return Results.BadRequest();
            }

            if (!context.Request.HasFormContentType)
            {
                return Results.BadRequest();
            }

            var form = await context.Request.ReadFormAsync(context.RequestAborted);
            if (!string.Equals(form["confirmRestore"], "true", StringComparison.Ordinal))
            {
                return Results.BadRequest();
            }

            var succeeded = await coordinator.RestoreAsync(context.RequestAborted);
            return CreateResult(context.Request, succeeded ? "/" : "/recovery?restore=failed");
        })
            .WithMetadata(new RequireAntiforgeryTokenAttribute(true))
            .WithMetadata(new RequestSizeLimitAttribute(16 * 1024));
    }

    private static IResult CreateResult(HttpRequest request, string redirectTo) =>
        request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
            ? Results.Json(new { redirectTo })
            : Results.Redirect(redirectTo);
}

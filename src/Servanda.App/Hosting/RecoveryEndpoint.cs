using System.Runtime.Versioning;
using Microsoft.AspNetCore.Antiforgery;

namespace Servanda.App.Hosting;

public static class RecoveryEndpoint
{
    public const string RetryPath = "/recovery/retry";

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
            var redirectTo = succeeded ? "/" : "/recovery?retry=failed";
            return context.Request.Headers.Accept.ToString().Contains(
                "application/json",
                StringComparison.OrdinalIgnoreCase)
                ? Results.Json(new { redirectTo })
                : Results.Redirect(redirectTo);
        }).WithMetadata(new RequireAntiforgeryTokenAttribute(true));
    }
}

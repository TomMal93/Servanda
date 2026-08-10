using Microsoft.AspNetCore.Antiforgery;

namespace Servanda.App.Hosting;

public static class ShutdownEndpoint
{
    public const string Path = "/shutdown";

    public static IEndpointConventionBuilder MapShutdown(this IEndpointRouteBuilder endpoints)
    {
        return endpoints.MapPost(Path, (
            HttpContext context,
            IHostApplicationLifetime lifetime) =>
        {
            var antiforgeryValidation = context.Features.Get<IAntiforgeryValidationFeature>();
            if (antiforgeryValidation is not { IsValid: true })
            {
                return Results.BadRequest();
            }

            context.Response.OnCompleted(() =>
            {
                lifetime.StopApplication();
                return Task.CompletedTask;
            });

            return Results.Text(
                "Servanda została zamknięta. Możesz zamknąć tę kartę.",
                "text/plain; charset=utf-8");
        }).WithMetadata(new RequireAntiforgeryTokenAttribute(true));
    }
}

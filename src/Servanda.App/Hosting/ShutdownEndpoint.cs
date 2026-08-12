using Microsoft.AspNetCore.Antiforgery;

namespace Servanda.App.Hosting;

public static class ShutdownEndpoint
{
    public const string Path = "/shutdown";
    private const string ConfirmationPage = """
        <!DOCTYPE html>
        <html lang="pl">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Servanda została zamknięta</title>
        </head>
        <body>
            <main>
                <h1>Servanda została zamknięta</h1>
                <p>Możesz zamknąć tę kartę.</p>
            </main>
        </body>
        </html>
        """;

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

            return Results.Content(ConfirmationPage, "text/html; charset=utf-8");
        }).WithMetadata(new RequireAntiforgeryTokenAttribute(true));
    }
}

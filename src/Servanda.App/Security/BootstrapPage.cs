namespace Servanda.App.Security;

internal static class BootstrapPage
{
    internal const string Html = """
        <!DOCTYPE html>
        <html lang="pl">
        <head>
            <meta charset="utf-8">
            <meta name="viewport" content="width=device-width, initial-scale=1">
            <title>Otwieranie Servandy</title>
            <link rel="stylesheet" href="/app.css">
            <script src="/bootstrap.js" type="module"></script>
        </head>
        <body>
            <main>
                <h1>Otwieranie Servandy</h1>
                <p id="bootstrap-status" role="status" aria-live="polite">Potwierdzanie lokalnej sesji…</p>
            </main>
        </body>
        </html>
        """;
}

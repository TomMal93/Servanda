using System.Net;
using System.Runtime.Versioning;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Servanda.App.Hosting;
using Servanda.App.Components;
using Servanda.App.Launching;
using Servanda.App.Security;
using Servanda.Infrastructure.Diagnostics;
using Servanda.Infrastructure.Runtime;

namespace Servanda.App;

[SupportedOSPlatform("linux")]
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var paths = new ServandaPathProvider().CreateAndVerify();
        if (args.Length == 0)
        {
            return await new Launcher(paths).RunAsync();
        }

        if (args is not ["--host"])
        {
            Console.Error.WriteLine("Nieznany tryb uruchomienia Servandy.");
            return 64;
        }

        return await RunHostAsync(paths);
    }

    private static async Task<int> RunHostAsync(ServandaPaths paths)
    {
        using var instanceLock = InstanceLock.TryAcquire(paths.InstanceLockPath);
        if (instanceLock is null)
        {
            Console.Error.WriteLine("Servanda jest już uruchomiona. Użyj launchera, aby otworzyć istniejącą instancję.");
            return 2;
        }

        using var technicalLog = new TechnicalLogWriter(paths);
        await technicalLog.WriteAsync(TechnicalEvent.HostStarting);

        try
        {
            using var controlSecret = await ControlSecret.CreateAndPublishAsync(paths.ControlSecretPath);

            var builder = WebApplication.CreateBuilder(new WebApplicationOptions
            {
                Args = [],
                ContentRootPath = ResolveContentRoot(),
            });
            builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

            builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
            builder.Services.AddSingleton(TimeProvider.System);
            builder.Services.AddSingleton(controlSecret);
            builder.Services.AddSingleton(technicalLog);
            builder.Services.AddSingleton<BootstrapTicketStore>();
            builder.Services.AddSingleton<ProcessSessionStore>();
            builder.Services.AddRateLimiter(options =>
            {
                options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
                options.AddFixedWindowLimiter("launcher", limiter =>
                {
                    limiter.PermitLimit = 10;
                    limiter.Window = TimeSpan.FromMinutes(1);
                    limiter.QueueLimit = 0;
                    limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                });
            });
            builder.Services.AddRazorComponents()
                .AddInteractiveServerComponents();
            builder.Services.AddSingleton(paths);
            builder.Services.AddSingleton<InstanceRuntimeState>();
            builder.Services.AddSingleton(new AtomicInstanceDescriptorStore(paths.DescriptorPath));
            builder.Services.AddHostedService<InstanceLifecyclePublisher>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
            }

            app.UseMiddleware<LocalHostSecurityMiddleware>();
            app.UseRouting();
            app.UseRateLimiter();
            app.UseMiddleware<ProcessSessionMiddleware>();
            app.UseAntiforgery();

            app.MapGet("/instance", (InstanceRuntimeState state) => Results.Json(new
            {
                formatVersion = InstanceDescriptor.CurrentFormatVersion,
                instanceId = state.InstanceId,
                state = "ready",
            }));

            app.MapPost("/launcher/ticket", (HttpRequest request, ControlSecret secret, BootstrapTicketStore tickets) =>
            {
                if (request.HttpContext.Connection.RemoteIpAddress is not { } remoteAddress
                    || !IPAddress.IsLoopback(remoteAddress)
                    || request.ContentLength is > 0
                    || request.Headers.ContainsKey("Transfer-Encoding")
                    || !secret.Authenticate(request.Headers["X-Servanda-Control"].ToString()))
                {
                    return Results.Unauthorized();
                }

                return Results.Ok(new
                {
                    ticket = tickets.Issue(),
                    expiresInSeconds = (int)BootstrapTicketStore.Lifetime.TotalSeconds,
                });
            })
                .DisableAntiforgery()
                .RequireRateLimiting("launcher")
                .WithMetadata(new RequestSizeLimitAttribute(1024));

            app.MapGet("/bootstrap", () => Results.Content(BootstrapPage.Html, "text/html; charset=utf-8"));

            app.MapPost("/session/bootstrap", (
                BootstrapRequest request,
                HttpResponse response,
                BootstrapTicketStore tickets,
                ProcessSessionStore sessions) =>
            {
                if (!tickets.TryConsume(request.Ticket))
                {
                    return Results.Unauthorized();
                }

                response.Cookies.Append(ProcessSessionStore.CookieName, sessions.Create(), new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    Path = "/",
                    SameSite = SameSiteMode.Strict,
                    Secure = false,
                });
                return Results.NoContent();
            })
                .DisableAntiforgery()
                .RequireRateLimiting("launcher")
                .WithMetadata(new RequestSizeLimitAttribute(1024));

            app.MapStaticAssets();
            app.MapShutdown();
            app.MapRazorComponents<Servanda.App.Components.App>()
                .AddInteractiveServerRenderMode();

            await app.RunAsync();
            return 0;
        }
        catch
        {
            await technicalLog.WriteAsync(TechnicalEvent.HostStartFailed);
            throw;
        }
        finally
        {
            DeleteRuntimeFile(paths.DescriptorPath);
            DeleteRuntimeFile(paths.ControlSecretPath);
            await technicalLog.WriteAsync(TechnicalEvent.HostStopped);
        }
    }

    private static void DeleteRuntimeFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static string ResolveContentRoot()
    {
        var publishedRoot = AppContext.BaseDirectory;
        if (Directory.Exists(Path.Combine(publishedRoot, "wwwroot")))
        {
            return publishedRoot;
        }

        var projectRoot = Path.GetFullPath(Path.Combine(publishedRoot, "..", "..", ".."));
        return File.Exists(Path.Combine(projectRoot, "Servanda.App.csproj"))
            ? projectRoot
            : publishedRoot;
    }
}

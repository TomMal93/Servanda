using System.Net;
using System.Runtime.Versioning;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
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
    private const int MaximumContentRootTraversalDepth = 8;

    public static async Task<int> Main(string[] args)
    {
        var launcherPlatform = new LinuxLauncherPlatform();
        return await RunWithErrorBoundaryAsync(
            args,
            launcherPlatform,
            () => RunAsync(args, launcherPlatform));
    }

    internal static async Task<int> RunWithErrorBoundaryAsync(
        string[] args,
        ILauncherPlatform launcherPlatform,
        Func<Task<int>> run)
    {
        try
        {
            return await run();
        }
        catch (Exception)
        {
            Console.Error.WriteLine("Servanda nie mogła się bezpiecznie uruchomić.");
            if (args.Length == 0)
            {
                TryShowError(launcherPlatform);
            }

            return 1;
        }
    }

    private static async Task<int> RunAsync(string[] args, ILauncherPlatform launcherPlatform)
    {
        var paths = new ServandaPathProvider().CreateAndVerify();

        if (args.Length == 0)
        {
            return await new Launcher(paths, launcherPlatform).RunAsync();
        }

        if (args is not ["--host"])
        {
            Console.Error.WriteLine("Nieznany tryb uruchomienia Servandy.");
            return 64;
        }

        return await RunHostAsync(paths);
    }

    private static void TryShowError(ILauncherPlatform launcherPlatform)
    {
        try
        {
            launcherPlatform.ShowError();
        }
        catch (Exception)
        {
            // The static error page is the final fallback and must not replace the original failure.
        }
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
            builder.Services.AddSingleton<BootstrapRateLimiter>();
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
            app.UseMiddleware<ProcessSessionMiddleware>();
            app.UseAntiforgery();

            app.MapGet("/instance", (InstanceRuntimeState state) => Results.Json(new
            {
                formatVersion = InstanceDescriptor.CurrentFormatVersion,
                instanceId = state.InstanceId,
                state = "ready",
            }));

            app.MapPost("/launcher/ticket", (
                HttpRequest request,
                ControlSecret secret,
                BootstrapTicketStore tickets,
                BootstrapRateLimiter rateLimiter) =>
            {
                if (request.HttpContext.Connection.RemoteIpAddress is not { } remoteAddress
                    || !IPAddress.IsLoopback(remoteAddress)
                    || request.ContentLength is > 0
                    || request.Headers.ContainsKey("Transfer-Encoding")
                    || !secret.Authenticate(request.Headers["X-Servanda-Control"].ToString()))
                {
                    return Results.Unauthorized();
                }

                using var lease = rateLimiter.AttemptTicketIssue();
                if (!lease.IsAcquired)
                {
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
                }

                return Results.Ok(new
                {
                    ticket = tickets.Issue(),
                    expiresInSeconds = (int)BootstrapTicketStore.Lifetime.TotalSeconds,
                });
            })
                .DisableAntiforgery()
                .WithMetadata(new RequestSizeLimitAttribute(1024));

            app.MapGet("/bootstrap", () => Results.Content(BootstrapPage.Html, "text/html; charset=utf-8"));

            app.MapPost("/session/bootstrap", (
                BootstrapRequest request,
                HttpResponse response,
                BootstrapTicketStore tickets,
                ProcessSessionStore sessions,
                BootstrapRateLimiter rateLimiter) =>
            {
                if (!tickets.TryConsume(request.Ticket))
                {
                    return Results.Unauthorized();
                }

                using var lease = rateLimiter.AttemptSessionBootstrap();
                if (!lease.IsAcquired)
                {
                    return Results.StatusCode(StatusCodes.Status429TooManyRequests);
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

    private static string ResolveContentRoot() =>
        ResolveContentRoot(AppContext.BaseDirectory, Environment.CurrentDirectory);

    internal static string ResolveContentRoot(
        string applicationBaseDirectory,
        string workingDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(workingDirectory);

        var applicationRoot = Path.GetFullPath(applicationBaseDirectory);
        if (Directory.Exists(Path.Combine(applicationRoot, "wwwroot")))
        {
            return applicationRoot;
        }

        var projectRoot = FindProjectRoot(applicationRoot)
            ?? FindProjectRoot(Path.GetFullPath(workingDirectory));
        return projectRoot
            ?? throw new InvalidOperationException(
                "Nie można odnaleźć katalogu zawartości Servandy z plikiem projektu i wwwroot.");
    }

    private static string? FindProjectRoot(string startDirectory)
    {
        var candidate = new DirectoryInfo(startDirectory);
        for (var depth = 0;
             candidate is not null && depth <= MaximumContentRootTraversalDepth;
             depth++, candidate = candidate.Parent)
        {
            if (IsProjectRoot(candidate.FullName))
            {
                return candidate.FullName;
            }

            var repositoryProjectRoot = Path.Combine(candidate.FullName, "src", "Servanda.App");
            if (IsProjectRoot(repositoryProjectRoot))
            {
                return repositoryProjectRoot;
            }
        }

        return null;
    }

    private static bool IsProjectRoot(string path) =>
        File.Exists(Path.Combine(path, "Servanda.App.csproj"))
        && Directory.Exists(Path.Combine(path, "wwwroot"));
}

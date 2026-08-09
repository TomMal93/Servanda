using System.Net;
using Microsoft.AspNetCore.DataProtection;
using Servanda.App.Hosting;
using Servanda.App.Components;
using Servanda.App.Security;
using Servanda.Infrastructure.Runtime;

namespace Servanda.App;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var paths = new ServandaPathProvider().CreateAndVerify();
        using var instanceLock = InstanceLock.TryAcquire(paths.InstanceLockPath);
        if (instanceLock is null)
        {
            Console.Error.WriteLine("Servanda jest już uruchomiona. Użyj launchera, aby otworzyć istniejącą instancję.");
            return 2;
        }

        await ControlSecretFile.CreateAsync(paths.ControlSecretPath);

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args,
            ContentRootPath = ResolveContentRoot(),
        });
        builder.WebHost.ConfigureKestrel(options => options.Listen(IPAddress.Loopback, 0));

        builder.Services.AddDataProtection().UseEphemeralDataProtectionProvider();
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
        app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
        app.UseAntiforgery();

        app.MapGet("/instance", (InstanceRuntimeState state) => Results.Json(new
        {
            formatVersion = InstanceDescriptor.CurrentFormatVersion,
            instanceId = state.InstanceId,
            state = "ready",
        }));

        app.MapStaticAssets();
        app.MapRazorComponents<Servanda.App.Components.App>()
            .AddInteractiveServerRenderMode();

        try
        {
            await app.RunAsync();
            return 0;
        }
        finally
        {
            DeleteRuntimeFile(paths.DescriptorPath);
            DeleteRuntimeFile(paths.ControlSecretPath);
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

        var projectRoot = Path.GetFullPath(Path.Combine(publishedRoot, "..", "..", "..", ".."));
        return File.Exists(Path.Combine(projectRoot, "Servanda.App.csproj"))
            ? projectRoot
            : publishedRoot;
    }
}

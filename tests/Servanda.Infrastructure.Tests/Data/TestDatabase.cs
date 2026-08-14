using System.Runtime.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Servanda.Infrastructure.Data;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Tests.Data;

[SupportedOSPlatform("linux")]
internal static class TestDatabase
{
    public static ServandaPaths CreatePaths(string root)
    {
        var paths = new ServandaPaths(
            Path.Combine(root, "runtime"),
            Path.Combine(root, "state"),
            Path.Combine(root, "data"));
        var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        Directory.CreateDirectory(paths.RuntimeDirectory, mode);
        Directory.CreateDirectory(paths.StateDirectory, mode);
        Directory.CreateDirectory(paths.DataDirectory, mode);
        return paths;
    }

    public static ServiceProvider CreateServices(ServandaPaths paths)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(paths);
        services.AddServandaDatabase(paths, "test-version");
        return services.BuildServiceProvider();
    }

    public static async Task<ServiceProvider> InitializeAsync(string root)
    {
        var paths = CreatePaths(root);
        var services = CreateServices(paths);
        await ServandaDatabase.InitializeAsync(services, paths, TimeProvider.System);
        return services;
    }
}

using Servanda.Infrastructure.Runtime;
using System.Runtime.Versioning;

namespace Servanda.Infrastructure.Tests.Runtime;

[SupportedOSPlatform("linux")]
public sealed class ServandaPathProviderTests
{
    [Fact]
    public void CreateAndVerifyUsesPrivateXdgDirectoriesWithoutCreatingDataOrConfig()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var environment = new Dictionary<string, string?>
        {
            ["XDG_RUNTIME_DIR"] = Path.Combine(temporaryDirectory.Path, "runtime"),
            ["XDG_STATE_HOME"] = Path.Combine(temporaryDirectory.Path, "state"),
            ["XDG_DATA_HOME"] = Path.Combine(temporaryDirectory.Path, "data"),
            ["XDG_CONFIG_HOME"] = Path.Combine(temporaryDirectory.Path, "config"),
        };
        Directory.CreateDirectory(environment["XDG_RUNTIME_DIR"]!);

        var provider = new ServandaPathProvider(
            name => environment.GetValueOrDefault(name),
            Path.Combine(temporaryDirectory.Path, "home"));

        var paths = provider.CreateAndVerify();

        Assert.Equal(Path.Combine(environment["XDG_RUNTIME_DIR"]!, "servanda"), paths.RuntimeDirectory);
        Assert.Equal(Path.Combine(environment["XDG_STATE_HOME"]!, "servanda"), paths.StateDirectory);
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(paths.RuntimeDirectory));
        Assert.Equal(
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
            File.GetUnixFileMode(paths.StateDirectory));
        Assert.False(Directory.Exists(environment["XDG_DATA_HOME"]));
        Assert.False(Directory.Exists(environment["XDG_CONFIG_HOME"]));
    }

    [Fact]
    public void CreateAndVerifyRejectsRelativeXdgPath()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var provider = new ServandaPathProvider(
            name => name == "XDG_RUNTIME_DIR" ? "relative/runtime" : null,
            temporaryDirectory.Path);

        var exception = Assert.Throws<InvalidOperationException>(() => provider.CreateAndVerify());

        Assert.Contains("XDG_RUNTIME_DIR", exception.Message, StringComparison.Ordinal);
    }
}

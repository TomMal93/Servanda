using System.Runtime.Versioning;

namespace Servanda.E2E.Hosting;

[SupportedOSPlatform("linux")]
public sealed class ContentRootTests
{
    [Fact]
    public void PublishedOutputUsesAdjacentWebRoot()
    {
        using var directories = new TestDirectories();
        var publishedRoot = directories.CreateDirectory("publish");
        directories.CreateDirectory("publish", "wwwroot");

        var result = Servanda.App.Program.ResolveContentRoot(
            publishedRoot,
            directories.CreateDirectory("working"));

        Assert.Equal(publishedRoot, result);
    }

    [Fact]
    public void RidOutputFindsProjectRootAtVariableDepth()
    {
        using var directories = new TestDirectories();
        var projectRoot = directories.CreateProjectRoot("src", "Servanda.App");
        var ridOutput = directories.CreateDirectory(
            "src",
            "Servanda.App",
            "bin",
            "Debug",
            "net10.0",
            "linux-x64");

        var result = Servanda.App.Program.ResolveContentRoot(
            ridOutput,
            directories.CreateDirectory("working"));

        Assert.Equal(projectRoot, result);
    }

    [Fact]
    public void ExternalBuildOutputCanUseRepositoryWorkingDirectory()
    {
        using var directories = new TestDirectories();
        var repositoryRoot = directories.CreateDirectory("repository");
        var projectRoot = directories.CreateProjectRoot("repository", "src", "Servanda.App");
        var externalOutput = directories.CreateDirectory("external-output");

        var result = Servanda.App.Program.ResolveContentRoot(externalOutput, repositoryRoot);

        Assert.Equal(projectRoot, result);
    }

    [Fact]
    public void UnknownLayoutFailsInsteadOfStartingWithoutStaticAssets()
    {
        using var directories = new TestDirectories();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Servanda.App.Program.ResolveContentRoot(
                directories.CreateDirectory("output"),
                directories.CreateDirectory("working")));

        Assert.Contains("wwwroot", exception.Message, StringComparison.Ordinal);
    }

    private sealed class TestDirectories : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(),
            $"servanda-content-root-tests-{Guid.NewGuid():N}");

        internal string CreateDirectory(params string[] segments)
        {
            var path = segments.Aggregate(_root, Path.Combine);
            Directory.CreateDirectory(path);
            return path;
        }

        internal string CreateProjectRoot(params string[] segments)
        {
            var path = CreateDirectory(segments);
            Directory.CreateDirectory(Path.Combine(path, "wwwroot"));
            File.WriteAllText(Path.Combine(path, "Servanda.App.csproj"), "<Project />");
            return path;
        }

        public void Dispose()
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
    }
}

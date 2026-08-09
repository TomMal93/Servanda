namespace Servanda.Infrastructure.Tests;

[System.Runtime.Versioning.SupportedOSPlatform("linux")]
internal sealed class TemporaryDirectory : IDisposable
{
    internal TemporaryDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"servanda-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
    }

    internal string Path { get; }

    public void Dispose()
    {
        Directory.Delete(Path, recursive: true);
    }
}

using System.Runtime.Versioning;

namespace Servanda.Infrastructure.Runtime;

[SupportedOSPlatform("linux")]
public sealed class DatabaseLock : IDisposable
{
    private readonly FileStream _stream;
    private bool _disposed;

    private DatabaseLock(FileStream stream)
    {
        _stream = stream;
    }

    public static DatabaseLock? TryAcquire(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        LinuxIdentity.EnsureLinux();

        FileStream stream;
        try
        {
            stream = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.OpenOrCreate,
                Access = FileAccess.ReadWrite,
                Share = FileShare.None,
                BufferSize = 1,
                Options = FileOptions.None,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            });
        }
        catch (IOException)
        {
            return null;
        }

        var acquired = false;
        try
        {
            PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.GetEffectiveUserId());
            acquired = true;
            return new DatabaseLock(stream);
        }
        finally
        {
            if (!acquired)
            {
                stream.Dispose();
            }
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _stream.Dispose();
        _disposed = true;
    }
}

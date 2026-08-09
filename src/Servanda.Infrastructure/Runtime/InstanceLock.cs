namespace Servanda.Infrastructure.Runtime;

public sealed class InstanceLock : IDisposable
{
    private readonly FileStream _stream;
    private bool _disposed;

    private InstanceLock(FileStream stream)
    {
        _stream = stream;
    }

    public static InstanceLock? TryAcquire(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Servanda v1 obsługuje wyłącznie system Linux.");
        }

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
            PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.geteuid());
            acquired = true;
            return new InstanceLock(stream);
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

        // FileShare.None is backed by an OS lock on Linux and is released with the descriptor.
        _stream.Dispose();
        _disposed = true;
    }
}

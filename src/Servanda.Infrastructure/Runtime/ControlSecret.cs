using System.Security.Cryptography;

namespace Servanda.Infrastructure.Runtime;

public sealed class ControlSecret : IDisposable
{
    private const int SecretSize = 32;

    private readonly byte[] _value;
    private bool _disposed;

    private ControlSecret(byte[] value)
    {
        _value = value;
    }

    public static async Task<ControlSecret> CreateAndPublishAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Servanda v1 obsługuje wyłącznie system Linux.");
        }

        var value = RandomNumberGenerator.GetBytes(SecretSize);
        ControlSecret? result = null;
        try
        {
            await using var stream = new FileStream(path, new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            });
            await stream.WriteAsync(value, cancellationToken);
            await stream.FlushAsync(cancellationToken);
            PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.geteuid());
            result = new ControlSecret(value);
            return result;
        }
        finally
        {
            if (result is null)
            {
                CryptographicOperations.ZeroMemory(value);
            }
        }
    }

    public bool Authenticate(string? encodedCandidate)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(encodedCandidate))
        {
            return false;
        }

        Span<byte> candidate = stackalloc byte[SecretSize];
        var decoded = Convert.TryFromBase64String(encodedCandidate, candidate, out var bytesWritten);
        var authenticated = decoded
            && bytesWritten == SecretSize
            && CryptographicOperations.FixedTimeEquals(candidate, _value);
        CryptographicOperations.ZeroMemory(candidate);
        return authenticated;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_value);
        _disposed = true;
    }
}

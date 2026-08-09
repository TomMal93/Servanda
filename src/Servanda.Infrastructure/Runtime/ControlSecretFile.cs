using System.Security.Cryptography;

namespace Servanda.Infrastructure.Runtime;

public static class ControlSecretFile
{
    private const int SecretSize = 32;

    public static async Task CreateAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Servanda v1 obsługuje wyłącznie system Linux.");
        }

        var secret = RandomNumberGenerator.GetBytes(SecretSize);
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
            await stream.WriteAsync(secret, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(secret);
        }

        PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.geteuid());
    }
}

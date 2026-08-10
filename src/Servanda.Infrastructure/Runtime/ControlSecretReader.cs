namespace Servanda.Infrastructure.Runtime;

public static class ControlSecretReader
{
    private const int SecretSize = 32;

    public static async Task<byte[]?> TryReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var secret = new byte[SecretSize];
        var succeeded = false;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.geteuid());
            if (new FileInfo(path).Length != SecretSize)
            {
                return null;
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: SecretSize,
                useAsync: true);
            await stream.ReadExactlyAsync(secret, cancellationToken);
            succeeded = true;
            return secret;
        }
        catch (IOException)
        {
            return null;
        }
        finally
        {
            if (!succeeded)
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(secret);
            }
        }
    }
}

namespace Servanda.Infrastructure.Runtime;

public static class ControlSecretReader
{
    public static async Task<byte[]?> TryReadAsync(string path, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var secret = new byte[ControlSecretFormat.SizeInBytes];
        var succeeded = false;
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.geteuid());
            if (new FileInfo(path).Length != ControlSecretFormat.SizeInBytes)
            {
                return null;
            }

            await using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: ControlSecretFormat.SizeInBytes,
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

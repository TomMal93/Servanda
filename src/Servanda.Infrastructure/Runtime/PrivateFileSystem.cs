namespace Servanda.Infrastructure.Runtime;

internal static class PrivateFileSystem
{
    private const uint PrivateDirectoryMode = 0x1C0; // 0700
    private const uint PrivateFileMode = 0x180; // 0600

    internal static void EnsureDirectory(string path, uint effectiveUserId)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Servanda v1 obsługuje wyłącznie system Linux.");
        }

        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        RejectSymbolicLink(path);
        var (owner, mode) = LinuxIdentity.GetOwnershipAndMode(path);
        if (owner != effectiveUserId || mode != PrivateDirectoryMode)
        {
            throw new UnauthorizedAccessException("Katalog Servandy nie należy do bieżącego użytkownika albo nie ma trybu 0700.");
        }
    }

    internal static void VerifyPrivateFile(string path, uint effectiveUserId)
    {
        LinuxIdentity.EnsureLinux();
        RejectSymbolicLink(path);

        var (owner, mode) = LinuxIdentity.GetOwnershipAndMode(path);
        if (owner != effectiveUserId || mode != PrivateFileMode)
        {
            throw new UnauthorizedAccessException("Plik runtime Servandy nie należy do bieżącego użytkownika albo nie ma trybu 0600.");
        }
    }

    private static void RejectSymbolicLink(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
        {
            throw new UnauthorizedAccessException("Prywatna ścieżka Servandy nie może być dowiązaniem symbolicznym.");
        }
    }
}

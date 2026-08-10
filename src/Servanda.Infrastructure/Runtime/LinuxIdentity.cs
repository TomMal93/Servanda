using System.Runtime.InteropServices;

namespace Servanda.Infrastructure.Runtime;

internal static partial class LinuxIdentity
{
    // glibc uses version 1 of struct stat on Linux x86-64. The managed layout below
    // is intentionally restricted to that ABI and must not be reused for another RID.
    private const int LinuxX64StatVersion = 1;

    internal static uint GetEffectiveUserId()
    {
        EnsureLinux();
        return geteuid();
    }

    [LibraryImport("libc")]
    internal static partial uint geteuid();

    [LibraryImport("libc", EntryPoint = "stat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int Stat(string path, out LinuxStat buffer);

    [LibraryImport("libc", EntryPoint = "__xstat", StringMarshalling = StringMarshalling.Utf8, SetLastError = true)]
    private static partial int LegacyStat(int version, string path, out LinuxStat buffer);

    internal static (uint Owner, uint Mode) GetOwnershipAndMode(string path)
    {
        EnsureLinux();
        EnsureSupportedArchitecture(RuntimeInformation.ProcessArchitecture);

        if (CompatibleStat(path, out var buffer) != 0)
        {
            throw new IOException($"Nie można zweryfikować prywatnej ścieżki runtime (errno {Marshal.GetLastPInvokeError()}).");
        }

        return (buffer.UserId, buffer.Mode & 0x0FFFu);
    }

    private static int CompatibleStat(string path, out LinuxStat buffer)
    {
        try
        {
            return Stat(path, out buffer);
        }
        catch (EntryPointNotFoundException)
        {
            // Before glibc 2.33, x86-64 exposed __xstat instead of stat.
            return LegacyStat(LinuxX64StatVersion, path, out buffer);
        }
    }

    internal static void EnsureSupportedArchitecture(Architecture architecture)
    {
        if (architecture != Architecture.X64)
        {
            throw new PlatformNotSupportedException("Servanda v1 obsługuje wyłącznie Linux x86-64.");
        }
    }

    internal static void EnsureLinux()
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Servanda v1 obsługuje wyłącznie system Linux.");
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct LinuxStat
    {
        internal ulong Device;
        internal ulong Inode;
        internal ulong HardLinks;
        internal uint Mode;
        internal uint UserId;
        internal uint GroupId;
        internal int Padding;
        internal ulong SpecialDevice;
        internal long Size;
        internal long BlockSize;
        internal long Blocks;
        internal long AccessSeconds;
        internal long AccessNanoseconds;
        internal long ModificationSeconds;
        internal long ModificationNanoseconds;
        internal long ChangeSeconds;
        internal long ChangeNanoseconds;
        internal long Reserved1;
        internal long Reserved2;
        internal long Reserved3;
    }
}

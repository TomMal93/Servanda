using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Tests.Runtime;

[SupportedOSPlatform("linux")]
public sealed class LinuxIdentityTests
{
    [Fact]
    public void OwnershipAndModeAreReadForLinuxX64StatAbi()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
        {
            return;
        }

        using var temporaryDirectory = new TemporaryDirectory();

        var (owner, mode) = LinuxIdentity.GetOwnershipAndMode(temporaryDirectory.Path);

        Assert.Equal(LinuxIdentity.GetEffectiveUserId(), owner);
        Assert.Equal(0x1C0u, mode);
    }

    [Fact]
    public void StatAbiRejectsUnsupportedArchitecture()
    {
        var exception = Assert.Throws<PlatformNotSupportedException>(
            () => LinuxIdentity.EnsureSupportedArchitecture(Architecture.Arm64));

        Assert.Contains("x86-64", exception.Message, StringComparison.Ordinal);
    }
}

using System.Runtime.Versioning;
using Servanda.Infrastructure.Diagnostics;

namespace Servanda.Infrastructure.Tests.Diagnostics;

[SupportedOSPlatform("linux")]
public sealed class TechnicalLogWriterTests
{
    private static readonly TimeProvider FixedTime = new FixedTimeProvider(
        new DateTimeOffset(2026, 8, 10, 12, 30, 0, TimeSpan.Zero));

    [Fact]
    public async Task WriteAsyncCreatesPrivateLogWithOnlyTechnicalEvent()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logPath = Path.Combine(temporaryDirectory.Path, "servanda.log");
        using var writer = new TechnicalLogWriter(logPath, 1024, 3, FixedTime);

        await writer.WriteAsync(TechnicalEvent.HostReady);

        Assert.Equal(
            "2026-08-10T12:30:00.0000000+00:00 HOST_READY\n",
            await File.ReadAllTextAsync(logPath));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(logPath));
    }

    [Fact]
    public async Task WriteAsyncRetainsOnlyConfiguredNumberOfPrivateArchives()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logPath = Path.Combine(temporaryDirectory.Path, "servanda.log");
        using var writer = new TechnicalLogWriter(logPath, 1, 2, FixedTime);

        await writer.WriteAsync(TechnicalEvent.HostStarting);
        await writer.WriteAsync(TechnicalEvent.HostReady);
        await writer.WriteAsync(TechnicalEvent.HostStartFailed);
        await writer.WriteAsync(TechnicalEvent.HostStopped);

        Assert.True(File.Exists(logPath));
        Assert.True(File.Exists($"{logPath}.1"));
        Assert.True(File.Exists($"{logPath}.2"));
        Assert.False(File.Exists($"{logPath}.3"));
        Assert.All(
            Directory.EnumerateFiles(temporaryDirectory.Path),
            path => Assert.Equal(
                UnixFileMode.UserRead | UnixFileMode.UserWrite,
                File.GetUnixFileMode(path)));
    }

    [Fact]
    public async Task WriteAsyncRejectsExistingNonPrivateLog()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var logPath = Path.Combine(temporaryDirectory.Path, "servanda.log");
        await File.WriteAllTextAsync(logPath, "untrusted");
        File.SetUnixFileMode(logPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        using var writer = new TechnicalLogWriter(logPath, 1024, 3, FixedTime);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => writer.WriteAsync(TechnicalEvent.HostReady));
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}

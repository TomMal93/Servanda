using System.Text.Json;
using System.Runtime.Versioning;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Tests.Runtime;

[SupportedOSPlatform("linux")]
public sealed class RuntimeFilesTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task PublishAsyncReplacesDescriptorAndLeavesNoTemporaryFile()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var descriptorPath = Path.Combine(temporaryDirectory.Path, "instance.json");
        var store = new AtomicInstanceDescriptorStore(descriptorPath);
        var starting = InstanceDescriptor.Starting("instance-1", 123, "http://127.0.0.1:43210");

        await store.PublishAsync(starting);
        await store.PublishAsync(starting.Ready());

        await using var stream = File.OpenRead(descriptorPath);
        var descriptor = await JsonSerializer.DeserializeAsync<InstanceDescriptor>(stream, SerializerOptions);
        Assert.NotNull(descriptor);
        Assert.Equal("ready", descriptor.State);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(descriptorPath));
        Assert.Empty(Directory.EnumerateFiles(temporaryDirectory.Path, "*.tmp"));
    }

    [Fact]
    public async Task CreateAsyncWritesPrivate256BitSecret()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var secretPath = Path.Combine(temporaryDirectory.Path, "control.secret");

        await ControlSecretFile.CreateAsync(secretPath);

        var secret = await File.ReadAllBytesAsync(secretPath);
        Assert.Equal(32, secret.Length);
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(secretPath));
    }

    [Fact]
    public void TryAcquireAllowsOnlyOneActiveLock()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var lockPath = Path.Combine(temporaryDirectory.Path, "instance.lock");
        using var firstLock = InstanceLock.TryAcquire(lockPath);

        using var secondLock = InstanceLock.TryAcquire(lockPath);

        Assert.NotNull(firstLock);
        Assert.Null(secondLock);
    }
}

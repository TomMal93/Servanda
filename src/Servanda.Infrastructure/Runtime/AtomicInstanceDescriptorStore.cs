using System.Text.Json;

namespace Servanda.Infrastructure.Runtime;

public sealed class AtomicInstanceDescriptorStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
    };

    private readonly string _descriptorPath;
    private readonly uint _effectiveUserId;

    public AtomicInstanceDescriptorStore(string descriptorPath)
        : this(descriptorPath, GetEffectiveUserId())
    {
    }

    public AtomicInstanceDescriptorStore(string descriptorPath, uint effectiveUserId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptorPath);
        _descriptorPath = descriptorPath;
        _effectiveUserId = effectiveUserId;
    }

    public async Task PublishAsync(InstanceDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(descriptor);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Servanda v1 obsługuje wyłącznie system Linux.");
        }

        var temporaryPath = Path.Combine(
            Path.GetDirectoryName(_descriptorPath)!,
            $".{Path.GetFileName(_descriptorPath)}.{Guid.NewGuid():N}.tmp");
        var published = false;

        try
        {
            await using (var stream = new FileStream(temporaryPath, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous | FileOptions.WriteThrough,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            }))
            {
                await JsonSerializer.SerializeAsync(stream, descriptor, SerializerOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            PrivateFileSystem.VerifyPrivateFile(temporaryPath, _effectiveUserId);
            File.Move(temporaryPath, _descriptorPath, overwrite: true);
            published = true;
        }
        finally
        {
            if (!published && File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private static uint GetEffectiveUserId()
    {
        LinuxIdentity.EnsureLinux();
        return LinuxIdentity.geteuid();
    }
}

using System.Text.Json;

namespace Servanda.Infrastructure.Runtime;

public sealed class InstanceDescriptorReader
{
    private const long MaximumDescriptorSize = 4096;
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly string _path;

    public InstanceDescriptorReader(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        _path = path;
    }

    public async Task<InstanceDescriptor?> TryReadReadyAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!File.Exists(_path))
            {
                return null;
            }

            PrivateFileSystem.VerifyPrivateFile(_path, LinuxIdentity.geteuid());
            if (new FileInfo(_path).Length is <= 0 or > MaximumDescriptorSize)
            {
                return null;
            }

            await using var stream = new FileStream(
                _path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                useAsync: true);
            var descriptor = await JsonSerializer.DeserializeAsync<InstanceDescriptor>(
                stream,
                SerializerOptions,
                cancellationToken);
            return IsValidReadyDescriptor(descriptor) ? descriptor : null;
        }
        catch (JsonException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }

    private static bool IsValidReadyDescriptor(InstanceDescriptor? descriptor) =>
        descriptor is
        {
            FormatVersion: InstanceDescriptor.CurrentFormatVersion,
            State: "ready",
            ProcessId: > 0,
        }
        && descriptor.InstanceId.Length is > 0 and <= 128
        && Uri.TryCreate(descriptor.Origin, UriKind.Absolute, out var origin)
        && origin.IsLoopback
        && origin.Scheme == Uri.UriSchemeHttp
        && origin.AbsolutePath == "/"
        && string.IsNullOrEmpty(origin.Query)
        && string.IsNullOrEmpty(origin.Fragment);
}

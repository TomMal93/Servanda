using System.Text;
using System.Runtime.Versioning;
using Servanda.Infrastructure.Runtime;

namespace Servanda.Infrastructure.Diagnostics;

[SupportedOSPlatform("linux")]
public sealed class TechnicalLogWriter : IDisposable
{
    private const long DefaultMaximumFileSize = 256 * 1024;
    private const int DefaultRetainedFileCount = 3;

    private readonly string _path;
    private readonly long _maximumFileSize;
    private readonly int _retainedFileCount;
    private readonly TimeProvider _timeProvider;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public TechnicalLogWriter(ServandaPaths paths)
        : this(paths.TechnicalLogPath, DefaultMaximumFileSize, DefaultRetainedFileCount, TimeProvider.System)
    {
    }

    internal TechnicalLogWriter(
        string path,
        long maximumFileSize,
        int retainedFileCount,
        TimeProvider timeProvider)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumFileSize);
        ArgumentOutOfRangeException.ThrowIfNegative(retainedFileCount);
        ArgumentNullException.ThrowIfNull(timeProvider);

        _path = path;
        _maximumFileSize = maximumFileSize;
        _retainedFileCount = retainedFileCount;
        _timeProvider = timeProvider;
    }

    public async Task WriteAsync(TechnicalEvent technicalEvent, CancellationToken cancellationToken = default)
    {
        var eventIdentifier = technicalEvent switch
        {
            TechnicalEvent.HostStarting => "HOST_STARTING",
            TechnicalEvent.HostReady => "HOST_READY",
            TechnicalEvent.HostStartFailed => "HOST_START_FAILED",
            TechnicalEvent.HostStopped => "HOST_STOPPED",
            _ => throw new ArgumentOutOfRangeException(nameof(technicalEvent)),
        };
        var entry = Encoding.UTF8.GetBytes($"{_timeProvider.GetUtcNow():O} {eventIdentifier}\n");

        await _gate.WaitAsync(cancellationToken);
        try
        {
            RotateIfRequired(entry.Length);
            await AppendPrivateFileAsync(entry, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose()
    {
        _gate.Dispose();
    }

    private void RotateIfRequired(int nextEntryLength)
    {
        if (!File.Exists(_path))
        {
            return;
        }

        PrivateFileSystem.VerifyPrivateFile(_path, LinuxIdentity.GetEffectiveUserId());
        if (new FileInfo(_path).Length + nextEntryLength <= _maximumFileSize)
        {
            return;
        }

        if (_retainedFileCount == 0)
        {
            File.Delete(_path);
            return;
        }

        var oldestPath = GetArchivePath(_retainedFileCount);
        DeleteVerifiedFileIfPresent(oldestPath);

        for (var generation = _retainedFileCount - 1; generation >= 1; generation--)
        {
            var source = GetArchivePath(generation);
            if (!File.Exists(source))
            {
                continue;
            }

            PrivateFileSystem.VerifyPrivateFile(source, LinuxIdentity.GetEffectiveUserId());
            File.Move(source, GetArchivePath(generation + 1));
        }

        File.Move(_path, GetArchivePath(1));
    }

    private async Task AppendPrivateFileAsync(byte[] entry, CancellationToken cancellationToken)
    {
        FileStream stream;
        try
        {
            stream = new FileStream(_path, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.Read,
                BufferSize = 4096,
                Options = FileOptions.Asynchronous,
                UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite,
            });
        }
        catch (IOException) when (File.Exists(_path))
        {
            PrivateFileSystem.VerifyPrivateFile(_path, LinuxIdentity.GetEffectiveUserId());
            stream = new FileStream(
                _path,
                FileMode.Append,
                FileAccess.Write,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.Asynchronous);
        }

        await using (stream)
        {
            await stream.WriteAsync(entry, cancellationToken);
            await stream.FlushAsync(cancellationToken);
        }
    }

    private static void DeleteVerifiedFileIfPresent(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        PrivateFileSystem.VerifyPrivateFile(path, LinuxIdentity.GetEffectiveUserId());
        File.Delete(path);
    }

    private string GetArchivePath(int generation) => $"{_path}.{generation}";
}

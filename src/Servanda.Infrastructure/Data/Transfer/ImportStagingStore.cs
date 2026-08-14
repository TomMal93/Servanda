using System.Collections.Concurrent;

namespace Servanda.Infrastructure.Data.Transfer;

/// <summary>
/// Przechowuje zweryfikowane dokumenty importu między podglądem a jawnym potwierdzeniem.
/// </summary>
internal sealed class ImportStagingStore
{
    private readonly ConcurrentDictionary<string, StagedImport> _entries = new(StringComparer.Ordinal);

    public void Add(string token, StagedImport staged) => _entries[token] = staged;

    public bool TryTake(string token, out StagedImport staged) => _entries.TryRemove(token, out staged!);

    public void Discard(string token)
    {
        if (_entries.TryRemove(token, out var staged))
        {
            staged.DeleteStagingDirectory();
        }
    }
}

internal sealed record StagedImport(ExportDocument Document, string StagingDirectory)
{
    public void DeleteStagingDirectory()
    {
        if (Directory.Exists(StagingDirectory))
        {
            Directory.Delete(StagingDirectory, recursive: true);
        }
    }
}

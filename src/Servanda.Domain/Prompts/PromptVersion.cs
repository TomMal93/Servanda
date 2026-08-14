namespace Servanda.Domain.Prompts;

public sealed class PromptVersion
{
    public const int RetainedVersionsPerPrompt = 50;

    private PromptVersion()
    {
    }

    public PromptVersion(string id, string promptId, string snapshotJson, DateTimeOffset createdAt)
    {
        Id = id;
        PromptId = promptId;
        SnapshotJson = snapshotJson;
        CreatedAt = createdAt;
    }

    public string Id { get; private set; } = string.Empty;

    public string PromptId { get; private set; } = string.Empty;

    public string SnapshotJson { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }
}

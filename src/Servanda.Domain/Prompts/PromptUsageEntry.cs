namespace Servanda.Domain.Prompts;

public sealed class PromptUsageEntry
{
    public const int RetainedEntries = 500;

    private PromptUsageEntry()
    {
    }

    public PromptUsageEntry(
        string id,
        string? promptId,
        string? variantId,
        string promptTitle,
        string variantName,
        DateTimeOffset usedAt)
    {
        Id = id;
        PromptId = promptId;
        VariantId = variantId;
        PromptTitle = promptTitle;
        VariantName = variantName;
        UsedAt = usedAt;
    }

    public string Id { get; private set; } = string.Empty;

    public string? PromptId { get; private set; }

    public string? VariantId { get; private set; }

    public string PromptTitle { get; private set; } = string.Empty;

    public string VariantName { get; private set; } = string.Empty;

    public DateTimeOffset UsedAt { get; private set; }
}

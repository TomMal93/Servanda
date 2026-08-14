namespace Servanda.Domain.Prompts;

public sealed class PromptVariant
{
    public const int MaxNameLength = 80;
    public const int MaxTargetLength = 80;
    public const int MaxContentLength = 30_000;

    private PromptVariant()
    {
    }

    internal PromptVariant(
        string id,
        string promptId,
        string name,
        string? target,
        string content,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        Id = id;
        PromptId = promptId;
        Name = name;
        Target = target;
        Content = content;
        SortOrder = sortOrder;
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
    }

    public string Id { get; private set; } = string.Empty;

    public string PromptId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string? Target { get; private set; }

    public string Content { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Update(string name, string? target, string content, int sortOrder, DateTimeOffset timestamp)
    {
        if (string.Equals(Name, name, StringComparison.Ordinal)
            && string.Equals(Target, target, StringComparison.Ordinal)
            && string.Equals(Content, content, StringComparison.Ordinal)
            && SortOrder == sortOrder)
        {
            return;
        }

        Name = name;
        Target = target;
        Content = content;
        SortOrder = sortOrder;
        UpdatedAt = timestamp;
    }
}

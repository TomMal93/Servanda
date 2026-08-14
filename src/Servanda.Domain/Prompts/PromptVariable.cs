using System.Text.RegularExpressions;

namespace Servanda.Domain.Prompts;

public sealed partial class PromptVariable
{
    public const int MaxNameLength = 50;
    public const int MaxLabelLength = 80;
    public const int MaxDefaultValueLength = 4000;

    private PromptVariable()
    {
    }

    internal PromptVariable(
        string id,
        string promptId,
        string name,
        string label,
        string defaultValue,
        bool isRequired,
        bool isMultiline,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        Id = id;
        PromptId = promptId;
        Name = name;
        Label = label;
        DefaultValue = defaultValue;
        IsRequired = isRequired;
        IsMultiline = isMultiline;
        SortOrder = sortOrder;
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
    }

    internal PromptVariable(
        string id,
        string promptId,
        string name,
        string label,
        string defaultValue,
        bool isRequired,
        bool isMultiline,
        int sortOrder,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
        : this(id, promptId, name, label, defaultValue, isRequired, isMultiline, sortOrder, createdAt)
    {
        UpdatedAt = updatedAt;
    }

    public string Id { get; private set; } = string.Empty;

    public string PromptId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Label { get; private set; } = string.Empty;

    public string DefaultValue { get; private set; } = string.Empty;

    public bool IsRequired { get; private set; }

    public bool IsMultiline { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    internal void Update(
        string name,
        string label,
        string defaultValue,
        bool isRequired,
        bool isMultiline,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        if (string.Equals(Name, name, StringComparison.Ordinal)
            && string.Equals(Label, label, StringComparison.Ordinal)
            && string.Equals(DefaultValue, defaultValue, StringComparison.Ordinal)
            && IsRequired == isRequired
            && IsMultiline == isMultiline
            && SortOrder == sortOrder)
        {
            return;
        }

        Name = name;
        Label = label;
        DefaultValue = defaultValue;
        IsRequired = isRequired;
        IsMultiline = isMultiline;
        SortOrder = sortOrder;
        UpdatedAt = timestamp;
    }

    public static bool IsValidName(string? name) =>
        !string.IsNullOrEmpty(name) && name.Length <= MaxNameLength && NamePattern().IsMatch(name);

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_-]*$")]
    private static partial Regex NamePattern();
}

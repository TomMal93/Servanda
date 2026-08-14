namespace Servanda.Domain.Prompts;

/// <summary>
/// Przenośna migawka wariantów i zmiennych promptu zapisywana w historii wersji.
/// </summary>
public sealed record PromptSnapshot(
    int SchemaVersion,
    IReadOnlyList<PromptVariantSnapshot> Variants,
    IReadOnlyList<PromptVariableSnapshot> Variables)
{
    public const int CurrentSchemaVersion = 1;

    public bool IsSupported => SchemaVersion == CurrentSchemaVersion;
}

public sealed record PromptVariantSnapshot(
    string Id,
    string Name,
    string? Target,
    string Content,
    int SortOrder);

public sealed record PromptVariableSnapshot(
    string Id,
    string Name,
    string Label,
    string DefaultValue,
    bool IsRequired,
    bool IsMultiline,
    int SortOrder);

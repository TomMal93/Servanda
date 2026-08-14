using System.Text.Json;
using System.Text.Json.Serialization;
using Servanda.Domain.Prompts;

namespace Servanda.Infrastructure.Data.Prompts;

/// <summary>
/// Migawka wersji promptu ma własną wersję schematu i jest walidowana przed przywróceniem.
/// </summary>
internal static class PromptSnapshotSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    public static string Serialize(PromptSnapshot snapshot) => JsonSerializer.Serialize(snapshot, Options);

    public static PromptSnapshot? Deserialize(string json)
    {
        PromptSnapshot? snapshot;
        try
        {
            snapshot = JsonSerializer.Deserialize<PromptSnapshot>(json, Options);
        }
        catch (JsonException)
        {
            return null;
        }

        if (snapshot is null || !snapshot.IsSupported || snapshot.Variants is null || snapshot.Variables is null)
        {
            return null;
        }

        return snapshot.Variants.Count is >= Prompt.MinVariants and <= Prompt.MaxVariants
            && snapshot.Variables.Count <= Prompt.MaxVariables
            ? snapshot
            : null;
    }
}

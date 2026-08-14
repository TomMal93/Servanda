using System.Reflection;
using System.Text.Json;
using Json.Schema;

namespace Servanda.Infrastructure.Data.Transfer;

/// <summary>
/// Waliduje dokument importu bezpośrednio przez normatywny JSON Schema formatu 1.
/// </summary>
internal static class ExportSchemaValidator
{
    private const string ResourceName = "Servanda.Infrastructure.Schemas.servanda-export-v1.schema.json";

    private static readonly Lazy<JsonSchema> Schema = new(Load);

    public static IReadOnlyList<string> Validate(JsonElement document)
    {
        var result = Schema.Value.Evaluate(
            document,
            new EvaluationOptions { OutputFormat = OutputFormat.List });
        if (result.IsValid)
        {
            return [];
        }

        var problems = (result.Details ?? [])
            .Where(detail => detail.Errors is { Count: > 0 })
            .SelectMany(detail => detail.Errors!.Select(error =>
                $"{detail.InstanceLocation}: {error.Value}"))
            .Distinct(StringComparer.Ordinal)
            .Take(20)
            .ToList();
        return problems.Count > 0 ? problems : ["Dokument nie spełnia schematu eksportu w wersji 1."];
    }

    private static JsonSchema Load()
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException("Artefakt nie zawiera schematu eksportu formatu 1.");
        return JsonSerializer.Deserialize<JsonSchema>(stream)
            ?? throw new InvalidOperationException("Schemat eksportu formatu 1 jest niepoprawny.");
    }
}

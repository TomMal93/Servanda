using Servanda.Domain.Catalog;
using Servanda.Domain.Prompts;
using Servanda.Domain.Tools;
using Servanda.Infrastructure.Data.Transfer;

namespace Servanda.Infrastructure.Tests.Data;

internal sealed record P4SearchProfileDefinition(string Name, int ToolCount, int PromptCount);

internal sealed record P4SearchProbe(string Module, string Text);

/// <summary>
/// Deterministyczne dane pomiarowe P4. Stałe ziarno wpływa na rozkład nazw,
/// kategorii i tagów, a identyfikatory nie zależą od zegara ani losowości procesu.
/// </summary>
internal static class P4SearchPerformanceProfile
{
    public const int Seed = 20260814;
    public const string ToolAreaId = "01J00000000000000000000002";
    public const string PromptAreaId = "01J00000000000000000000001";

    public static readonly P4SearchProfileDefinition Reference = new("referencyjny", 2_000, 1_000);
    public static readonly P4SearchProfileDefinition Boundary = new("graniczny", 10_000, 5_000);

    public static IReadOnlyList<P4SearchProbe> Probes { get; } =
    [
        new("tools", "Kalkulator Łódź"),
        new("tools", "kalk"),
        new("tools", "plan rodziny"),
        new("tools", "Archiwum Łódź"),
        new("tools", "biohacking"),
        new("tools", "docs example"),
        new("tools", "lodz"),
        new("prompts", "Asystent Łódź"),
        new("prompts", "asyst"),
        new("prompts", "analiza projektu"),
        new("prompts", "Archiwum Łódź"),
        new("prompts", "biohacking"),
        new("prompts", "lodz"),
        new("prompts", "ukrytatresc"),
    ];

    public static ExportDocument Create(P4SearchProfileDefinition profile)
    {
        ArgumentNullException.ThrowIfNull(profile);

        var timestamp = new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
        var random = new Random(Seed);
        var areas = CreateAreas(timestamp);
        var categories = CreateCategories(timestamp);
        var toolCategoryIds = categories
            .Where(category => category.AreaId == ToolAreaId)
            .Select(category => category.Id)
            .ToArray();
        var promptCategoryIds = categories
            .Where(category => category.AreaId == PromptAreaId)
            .Select(category => category.Id)
            .ToArray();
        var tags = CreateTags(timestamp);
        var toolTagIds = tags.Where(tag => tag.AreaId == ToolAreaId).Select(tag => tag.Id).ToArray();
        var promptTagIds = tags.Where(tag => tag.AreaId == PromptAreaId).Select(tag => tag.Id).ToArray();
        var tools = CreateTools(profile.ToolCount, toolCategoryIds, toolTagIds, timestamp, random);
        var prompts = CreatePrompts(profile.PromptCount, promptCategoryIds, promptTagIds, timestamp, random);
        var usage = CreateUsage(prompts, timestamp);

        return new ExportDocument(
            ExportDocument.CurrentSchemaVersion,
            Id('E', 1),
            timestamp,
            $"p4-performance-{profile.Name}",
            areas,
            categories,
            tags,
            tools,
            prompts,
            usage);
    }

    private static IReadOnlyList<ExportArea> CreateAreas(DateTimeOffset timestamp) =>
    [
        new(
            PromptAreaId,
            "Skarbiec promptów",
            "Biblioteka promptów",
            "prompt",
            "violet",
            "prompts",
            "active",
            0,
            false,
            null,
            timestamp,
            timestamp),
        new(
            ToolAreaId,
            "Przechowalnia narzędzi",
            "Katalog narzędzi",
            "tool",
            "blue",
            "tools",
            "active",
            1,
            false,
            null,
            timestamp,
            timestamp),
    ];

    private static List<ExportCategory> CreateCategories(DateTimeOffset timestamp)
    {
        var categories = new List<ExportCategory>(Category.MaxDepth * 2);
        AddCategoryPath(categories, ToolAreaId, 'C', timestamp);
        AddCategoryPath(categories, PromptAreaId, 'D', timestamp);
        return categories;
    }

    private static void AddCategoryPath(
        List<ExportCategory> categories,
        string areaId,
        char prefix,
        DateTimeOffset timestamp)
    {
        string? parentId = null;
        for (var depth = 0; depth < Category.MaxDepth; depth++)
        {
            var id = Id(prefix, depth);
            var name = depth == Category.MaxDepth - 1 ? "Archiwum Łódź" : $"Poziom {depth + 1:D2}";
            categories.Add(new ExportCategory(
                id,
                areaId,
                parentId,
                name,
                $"Deterministyczna kategoria poziomu {depth + 1}",
                0,
                timestamp,
                timestamp));
            parentId = id;
        }
    }

    private static List<ExportTag> CreateTags(DateTimeOffset timestamp)
    {
        var tags = new List<ExportTag>(Tool.MaxTags + Prompt.MaxTags);
        for (var index = 0; index < Tool.MaxTags; index++)
        {
            var name = index == Tool.MaxTags - 1 ? "biohacking" : $"narzędzie-{index:D2}";
            tags.Add(new ExportTag(
                Id('G', index),
                ToolAreaId,
                name,
                Tag.NormalizeName(name),
                timestamp,
                timestamp));
        }

        for (var index = 0; index < Prompt.MaxTags; index++)
        {
            var name = index == Prompt.MaxTags - 1 ? "biohacking" : $"prompt-{index:D2}";
            tags.Add(new ExportTag(
                Id('H', index),
                PromptAreaId,
                name,
                Tag.NormalizeName(name),
                timestamp,
                timestamp));
        }

        return tags;
    }

    private static List<ExportTool> CreateTools(
        int count,
        string[] categoryIds,
        string[] tagIds,
        DateTimeOffset timestamp,
        Random random)
    {
        var tools = new List<ExportTool>(count);
        var positions = new Dictionary<(string CategoryId, string GroupKey), int>();
        for (var index = 0; index < count; index++)
        {
            var categoryId = index == 0 ? categoryIds[^1] : categoryIds[random.Next(categoryIds.Length)];
            var groupKey = index % 7 == 0 ? Tool.FeaturedGroup : Tool.RegularGroup;
            var scope = (categoryId, groupKey);
            positions.TryGetValue(scope, out var sortOrder);
            positions[scope] = sortOrder + 1;
            var name = index == 0 ? "Kalkulator Łódź" : $"Narzędzie {index:D5}";
            var description = index == 0
                ? "Plan rodziny i codzienne obowiązki"
                : $"Deterministyczny opis narzędzia {index:D5}";
            var url = index == 0
                ? "https://docs.example.com/kalkulator"
                : $"https://example.com/tools/{index:D5}";
            tools.Add(new ExportTool(
                Id('T', index),
                ToolAreaId,
                categoryId,
                name,
                description,
                url,
                groupKey,
                sortOrder,
                tagIds,
                timestamp.AddSeconds(index),
                timestamp.AddSeconds(index)));
        }

        return tools;
    }

    private static List<ExportPrompt> CreatePrompts(
        int count,
        string[] categoryIds,
        string[] tagIds,
        DateTimeOffset timestamp,
        Random random)
    {
        var prompts = new List<ExportPrompt>(count);
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        var regularContent = BuildContent(2_000, "treść wariantu do wyszukiwania");
        var longContent = BuildContent(30_000, "długi wariant graniczny");
        var childIndex = 0;

        for (var index = 0; index < count; index++)
        {
            var categoryId = index == 0 ? categoryIds[^1] : categoryIds[random.Next(categoryIds.Length)];
            positions.TryGetValue(categoryId, out var sortOrder);
            positions[categoryId] = sortOrder + 1;
            var variantCount = index == 0 ? Prompt.MaxVariants : 5;
            var variants = new List<ExportPromptVariant>(variantCount);
            for (var variantIndex = 0; variantIndex < variantCount; variantIndex++)
            {
                var content = index == 0
                    ? $"ukrytatresc {longContent}"[..30_000]
                    : regularContent;
                variants.Add(new ExportPromptVariant(
                    Id('V', childIndex++),
                    $"Wariant {variantIndex + 1:D2}",
                    variantIndex % 2 == 0 ? "czat" : "dokument",
                    content,
                    variantIndex,
                    timestamp,
                    timestamp));
            }

            var title = index == 0 ? "Asystent Łódź" : $"Prompt {index:D5}";
            var description = index == 0
                ? "Analiza projektu i przygotowanie odpowiedzi"
                : $"Deterministyczny opis promptu {index:D5}";
            prompts.Add(new ExportPrompt(
                Id('P', index),
                PromptAreaId,
                categoryId,
                title,
                description,
                index % 10 == 0,
                sortOrder,
                tagIds,
                variants,
                [],
                [],
                timestamp.AddSeconds(index),
                timestamp.AddSeconds(index)));
        }

        return prompts;
    }

    private static List<ExportPromptUsage> CreateUsage(
        List<ExportPrompt> prompts,
        DateTimeOffset timestamp)
    {
        var usage = new List<ExportPromptUsage>(PromptUsageEntry.RetainedEntries);
        for (var index = 0; index < PromptUsageEntry.RetainedEntries; index++)
        {
            var prompt = prompts[index % prompts.Count];
            var variant = prompt.Variants[index % prompt.Variants.Count];
            usage.Add(new ExportPromptUsage(
                Id('U', index),
                prompt.Id,
                variant.Id,
                prompt.Title,
                variant.Name,
                timestamp.AddMinutes(index)));
        }

        return usage;
    }

    private static string BuildContent(int length, string phrase)
    {
        var repetitions = (length / (phrase.Length + 1)) + 1;
        return string.Join(' ', Enumerable.Repeat(phrase, repetitions))[..length];
    }

    private static string Id(char prefix, int value) => $"{prefix}{value:D25}";
}

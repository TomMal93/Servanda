namespace Servanda.Domain.Prompts;

public sealed record PromptVariantDraft(string? Id, string Name, string? Target, string Content);

public sealed record PromptVariableDraft(
    string? Id,
    string Name,
    string Label,
    string DefaultValue,
    bool IsRequired,
    bool IsMultiline);

public sealed class Prompt
{
    public const int MaxTitleLength = 100;
    public const int MaxDescriptionLength = 400;
    public const int MaxTags = 12;
    public const int MinVariants = 1;
    public const int MaxVariants = 20;
    public const int MaxVariables = 50;

    private readonly List<PromptTag> _tags = [];
    private readonly List<PromptVariant> _variants = [];
    private readonly List<PromptVariable> _variables = [];

    private Prompt()
    {
    }

    private Prompt(
        string id,
        string areaId,
        string categoryId,
        string title,
        string description,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        Id = id;
        AreaId = areaId;
        CategoryId = categoryId;
        Title = title;
        Description = description;
        SortOrder = sortOrder;
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
        Revision = 1;
    }

    public string Id { get; private set; } = string.Empty;

    public string AreaId { get; private set; } = string.Empty;

    public string CategoryId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public bool IsFavorite { get; private set; }

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Revision { get; private set; }

    public IReadOnlyList<PromptTag> Tags => _tags;

    public IReadOnlyList<PromptVariant> Variants => _variants;

    public IReadOnlyList<PromptVariable> Variables => _variables;

    public static Prompt? Create(
        string id,
        string areaId,
        string categoryId,
        string title,
        string description,
        IReadOnlyCollection<string> tagIds,
        IReadOnlyList<PromptVariantDraft> variants,
        IReadOnlyList<PromptVariableDraft> variables,
        bool allowUnusedVariables,
        int sortOrder,
        DateTimeOffset timestamp,
        Func<string> newChildId,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        ArgumentNullException.ThrowIfNull(newChildId);

        errors = Validate(title, description, tagIds, variants, variables, allowUnusedVariables);
        if (errors.Count > 0)
        {
            return null;
        }

        var prompt = new Prompt(
            id,
            areaId,
            categoryId,
            title.Trim(),
            description.Trim(),
            sortOrder,
            timestamp);
        prompt.ReplaceTags(tagIds);
        prompt.ReplaceChildren(variants, variables, timestamp, newChildId);
        return prompt;
    }

    public IReadOnlyDictionary<string, string[]> UpdateContent(
        string title,
        string description,
        IReadOnlyCollection<string> tagIds,
        IReadOnlyList<PromptVariantDraft> variants,
        IReadOnlyList<PromptVariableDraft> variables,
        bool allowUnusedVariables,
        DateTimeOffset timestamp,
        Func<string> newChildId)
    {
        ArgumentNullException.ThrowIfNull(newChildId);

        var errors = Validate(title, description, tagIds, variants, variables, allowUnusedVariables);
        if (errors.Count > 0)
        {
            return errors;
        }

        Title = title.Trim();
        Description = description.Trim();
        ReplaceTags(tagIds);
        ReplaceChildren(variants, variables, timestamp, newChildId);
        UpdatedAt = timestamp;
        Revision++;
        return errors;
    }

    public void SetFavorite(bool isFavorite, DateTimeOffset timestamp)
    {
        IsFavorite = isFavorite;
        UpdatedAt = timestamp;
        Revision++;
    }

    public void MoveTo(string categoryId, DateTimeOffset timestamp)
    {
        CategoryId = categoryId;
        UpdatedAt = timestamp;
        Revision++;
    }

    public PromptSnapshot BuildSnapshot() =>
        new(
            PromptSnapshot.CurrentSchemaVersion,
            [.. _variants
                .OrderBy(variant => variant.SortOrder)
                .ThenBy(variant => variant.Id, StringComparer.Ordinal)
                .Select(variant => new PromptVariantSnapshot(variant.Name, variant.Target, variant.Content))],
            [.. _variables
                .OrderBy(variable => variable.SortOrder)
                .ThenBy(variable => variable.Id, StringComparer.Ordinal)
                .Select(variable => new PromptVariableSnapshot(
                    variable.Name,
                    variable.Label,
                    variable.DefaultValue,
                    variable.IsRequired,
                    variable.IsMultiline))]);

    public static IReadOnlyDictionary<string, string[]> Validate(
        string title,
        string description,
        IReadOnlyCollection<string> tagIds,
        IReadOnlyList<PromptVariantDraft> variants,
        IReadOnlyList<PromptVariableDraft> variables,
        bool allowUnusedVariables)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedTitle = title?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;

        if (normalizedTitle.Length is < 1 or > MaxTitleLength)
        {
            errors[nameof(Title)] = [$"Tytuł musi mieć od 1 do {MaxTitleLength} znaków."];
        }

        if (normalizedDescription.Length is < 1 or > MaxDescriptionLength)
        {
            errors[nameof(Description)] = [$"Opis musi mieć od 1 do {MaxDescriptionLength} znaków."];
        }

        if (tagIds is null || tagIds.Count > MaxTags)
        {
            errors[nameof(Tags)] = [$"Prompt może mieć najwyżej {MaxTags} tagów."];
        }
        else if (tagIds.Distinct(StringComparer.Ordinal).Count() != tagIds.Count)
        {
            errors[nameof(Tags)] = ["Tag nie może zostać przypisany dwa razy."];
        }

        ValidateVariants(variants, errors);
        ValidateVariables(variables, errors);
        if (errors.ContainsKey(nameof(Variants)) || errors.ContainsKey(nameof(Variables)))
        {
            return errors;
        }

        ValidatePlaceholders(variants, variables, allowUnusedVariables, errors);
        return errors;
    }

    private static void ValidateVariants(
        IReadOnlyList<PromptVariantDraft> variants,
        Dictionary<string, string[]> errors)
    {
        if (variants is null || variants.Count is < MinVariants or > MaxVariants)
        {
            errors[nameof(Variants)] = [$"Prompt musi mieć od {MinVariants} do {MaxVariants} wariantów."];
            return;
        }

        var messages = new List<string>();
        foreach (var variant in variants)
        {
            var name = variant.Name?.Trim() ?? string.Empty;
            if (name.Length is < 1 or > PromptVariant.MaxNameLength)
            {
                messages.Add($"Nazwa wariantu musi mieć od 1 do {PromptVariant.MaxNameLength} znaków.");
            }

            if ((variant.Target?.Trim().Length ?? 0) > PromptVariant.MaxTargetLength)
            {
                messages.Add($"Przeznaczenie wariantu może mieć najwyżej {PromptVariant.MaxTargetLength} znaków.");
            }

            if ((variant.Content?.Length ?? 0) is < 1 or > PromptVariant.MaxContentLength)
            {
                messages.Add($"Treść wariantu musi mieć od 1 do {PromptVariant.MaxContentLength} znaków.");
            }
        }

        if (messages.Count > 0)
        {
            errors[nameof(Variants)] = [.. messages.Distinct(StringComparer.Ordinal)];
        }
    }

    private static void ValidateVariables(
        IReadOnlyList<PromptVariableDraft> variables,
        Dictionary<string, string[]> errors)
    {
        if (variables is null || variables.Count > MaxVariables)
        {
            errors[nameof(Variables)] = [$"Prompt może mieć najwyżej {MaxVariables} zmiennych."];
            return;
        }

        var messages = new List<string>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var variable in variables)
        {
            var name = variable.Name?.Trim() ?? string.Empty;
            if (!PromptVariable.IsValidName(name))
            {
                messages.Add("Nazwa zmiennej zaczyna się literą lub podkreśleniem i zawiera litery, cyfry, „_” albo „-”.");
            }
            else if (!names.Add(name))
            {
                messages.Add($"Zmienna „{name}” została zdefiniowana dwa razy.");
            }

            if ((variable.Label?.Trim().Length ?? 0) > PromptVariable.MaxLabelLength)
            {
                messages.Add($"Etykieta zmiennej może mieć najwyżej {PromptVariable.MaxLabelLength} znaków.");
            }

            if ((variable.DefaultValue?.Length ?? 0) > PromptVariable.MaxDefaultValueLength)
            {
                messages.Add(
                    $"Wartość domyślna zmiennej może mieć najwyżej {PromptVariable.MaxDefaultValueLength} znaków.");
            }
        }

        if (messages.Count > 0)
        {
            errors[nameof(Variables)] = [.. messages.Distinct(StringComparer.Ordinal)];
        }
    }

    private static void ValidatePlaceholders(
        IReadOnlyList<PromptVariantDraft> variants,
        IReadOnlyList<PromptVariableDraft> variables,
        bool allowUnusedVariables,
        Dictionary<string, string[]> errors)
    {
        var defined = variables
            .Select(variable => variable.Name.Trim())
            .ToHashSet(StringComparer.Ordinal);
        var used = variants
            .SelectMany(variant => PromptTemplate.ExtractPlaceholders(variant.Content))
            .ToHashSet(StringComparer.Ordinal);

        var undefined = used.Except(defined, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (undefined.Count > 0)
        {
            errors[nameof(Variants)] =
                [$"Warianty używają niezdefiniowanych zmiennych: {string.Join(", ", undefined)}."];
        }

        if (allowUnusedVariables)
        {
            return;
        }

        var unused = defined.Except(used, StringComparer.Ordinal).Order(StringComparer.Ordinal).ToList();
        if (unused.Count > 0)
        {
            errors[nameof(Variables)] =
                [$"Zmienne nieużywane w żadnym wariancie wymagają potwierdzenia: {string.Join(", ", unused)}."];
        }
    }

    private void ReplaceTags(IReadOnlyCollection<string> tagIds)
    {
        // Zachowanie istniejących powiązań pozwala zapisać zmianę bez usuwania i wstawiania tego samego wiersza.
        var kept = new List<PromptTag>(tagIds.Count);
        foreach (var tagId in tagIds)
        {
            var existing = _tags.Find(tag => string.Equals(tag.TagId, tagId, StringComparison.Ordinal));
            kept.Add(existing ?? new PromptTag(Id, tagId));
        }

        _tags.Clear();
        _tags.AddRange(kept);
    }

    private void ReplaceChildren(
        IReadOnlyList<PromptVariantDraft> variants,
        IReadOnlyList<PromptVariableDraft> variables,
        DateTimeOffset timestamp,
        Func<string> newChildId)
    {
        var keptVariants = new List<PromptVariant>(variants.Count);
        for (var index = 0; index < variants.Count; index++)
        {
            var draft = variants[index];
            var target = draft.Target?.Trim();
            target = string.IsNullOrEmpty(target) ? null : target;
            var existing = FindById(_variants, draft.Id, variant => variant.Id);
            if (existing is null)
            {
                existing = new PromptVariant(
                    newChildId(),
                    Id,
                    draft.Name.Trim(),
                    target,
                    draft.Content,
                    index,
                    timestamp);
            }
            else
            {
                existing.Update(draft.Name.Trim(), target, draft.Content, index, timestamp);
            }

            keptVariants.Add(existing);
        }

        _variants.Clear();
        _variants.AddRange(keptVariants);

        var keptVariables = new List<PromptVariable>(variables.Count);
        for (var index = 0; index < variables.Count; index++)
        {
            var draft = variables[index];
            var label = draft.Label?.Trim() ?? string.Empty;
            var defaultValue = draft.DefaultValue ?? string.Empty;
            var existing = FindById(_variables, draft.Id, variable => variable.Id);
            if (existing is null)
            {
                existing = new PromptVariable(
                    newChildId(),
                    Id,
                    draft.Name.Trim(),
                    label,
                    defaultValue,
                    draft.IsRequired,
                    draft.IsMultiline,
                    index,
                    timestamp);
            }
            else
            {
                existing.Update(
                    draft.Name.Trim(),
                    label,
                    defaultValue,
                    draft.IsRequired,
                    draft.IsMultiline,
                    index,
                    timestamp);
            }

            keptVariables.Add(existing);
        }

        _variables.Clear();
        _variables.AddRange(keptVariables);
    }

    private static TChild? FindById<TChild>(List<TChild> children, string? id, Func<TChild, string> selector)
        where TChild : class
    {
        return string.IsNullOrEmpty(id)
            ? null
            : children.Find(child => string.Equals(selector(child), id, StringComparison.Ordinal));
    }
}

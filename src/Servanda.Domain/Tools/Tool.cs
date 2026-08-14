namespace Servanda.Domain.Tools;

public sealed class Tool
{
    public const int MaxNameLength = 70;
    public const int MaxDescriptionLength = 280;
    public const int MaxUrlLength = 2048;
    public const int MaxTags = 8;
    public const string FeaturedGroup = "featured";
    public const string RegularGroup = "regular";

    private readonly List<ToolTag> _tags = [];

    private Tool()
    {
    }

    private Tool(
        string id,
        string areaId,
        string categoryId,
        string name,
        string description,
        string url,
        string groupKey,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        Id = id;
        AreaId = areaId;
        CategoryId = categoryId;
        Name = name;
        Description = description;
        Url = url;
        GroupKey = groupKey;
        SortOrder = sortOrder;
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
        Revision = 1;
    }

    public string Id { get; private set; } = string.Empty;

    public string AreaId { get; private set; } = string.Empty;

    public string CategoryId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string Url { get; private set; } = string.Empty;

    public string GroupKey { get; private set; } = RegularGroup;

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Revision { get; private set; }

    public IReadOnlyList<ToolTag> Tags => _tags;

    public static bool IsSupportedGroup(string groupKey) =>
        string.Equals(groupKey, FeaturedGroup, StringComparison.Ordinal)
        || string.Equals(groupKey, RegularGroup, StringComparison.Ordinal);

    public static bool IsAllowedUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength)
        {
            return false;
        }

        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var parsed)
            && (parsed.Scheme == Uri.UriSchemeHttp || parsed.Scheme == Uri.UriSchemeHttps);
    }

    public static Tool? Create(
        string id,
        string areaId,
        string categoryId,
        string name,
        string description,
        string url,
        string groupKey,
        IReadOnlyCollection<string> tagIds,
        int sortOrder,
        DateTimeOffset timestamp,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        var validation = new Dictionary<string, string[]>(ValidateContent(name, description, url, tagIds));
        if (!IsSupportedGroup(groupKey))
        {
            validation[nameof(GroupKey)] = ["Wybierz grupę „Lubię i szanuję” albo „Fajne”."];
        }

        errors = validation;
        if (validation.Count > 0)
        {
            return null;
        }

        var tool = new Tool(
            id,
            areaId,
            categoryId,
            name.Trim(),
            description.Trim(),
            url.Trim(),
            groupKey,
            sortOrder,
            timestamp);
        tool.ReplaceTags(tagIds);
        return tool;
    }

    /// <summary>Odtwarza narzędzie z dokumentu importu z zachowaniem identyfikatorów i pól audytowych.</summary>
    public static Tool Restore(
        string id,
        string areaId,
        string categoryId,
        string name,
        string description,
        string url,
        string groupKey,
        int sortOrder,
        IReadOnlyCollection<string> tagIds,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt)
    {
        var tool = new Tool(id, areaId, categoryId, name, description, url, groupKey, sortOrder, createdAt)
        {
            UpdatedAt = updatedAt,
        };
        tool.ReplaceTags(tagIds);
        return tool;
    }

    public IReadOnlyDictionary<string, string[]> UpdateContent(
        string name,
        string description,
        string url,
        IReadOnlyCollection<string> tagIds,
        DateTimeOffset timestamp)
    {
        var errors = ValidateContent(name, description, url, tagIds);
        if (errors.Count > 0)
        {
            return errors;
        }

        Name = name.Trim();
        Description = description.Trim();
        Url = url.Trim();
        ReplaceTags(tagIds);
        UpdatedAt = timestamp;
        Revision++;
        return errors;
    }

    public void MoveTo(string categoryId, string groupKey, DateTimeOffset timestamp)
    {
        CategoryId = categoryId;
        GroupKey = groupKey;
        UpdatedAt = timestamp;
        Revision++;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateContent(
        string name,
        string description,
        string url,
        IReadOnlyCollection<string> tagIds)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;

        if (normalizedName.Length is < 1 or > MaxNameLength)
        {
            errors[nameof(Name)] = [$"Nazwa musi mieć od 1 do {MaxNameLength} znaków."];
        }

        if (normalizedDescription.Length is < 1 or > MaxDescriptionLength)
        {
            errors[nameof(Description)] = [$"Opis musi mieć od 1 do {MaxDescriptionLength} znaków."];
        }

        if (!IsAllowedUrl(url))
        {
            errors[nameof(Url)] = ["Podaj poprawny adres zaczynający się od http:// albo https://."];
        }

        if (tagIds is null || tagIds.Count > MaxTags)
        {
            errors[nameof(Tags)] = [$"Narzędzie może mieć najwyżej {MaxTags} tagów."];
        }
        else if (tagIds.Distinct(StringComparer.Ordinal).Count() != tagIds.Count)
        {
            errors[nameof(Tags)] = ["Tag nie może zostać przypisany dwa razy."];
        }

        return errors;
    }

    private void ReplaceTags(IReadOnlyCollection<string> tagIds)
    {
        // Zachowanie istniejących powiązań pozwala zapisać zmianę bez usuwania i wstawiania tego samego wiersza.
        var kept = new List<ToolTag>(tagIds.Count);
        foreach (var tagId in tagIds)
        {
            var existing = _tags.Find(tag => string.Equals(tag.TagId, tagId, StringComparison.Ordinal));
            kept.Add(existing ?? new ToolTag(Id, tagId));
        }

        _tags.Clear();
        _tags.AddRange(kept);
    }
}

namespace Servanda.Domain.Catalog;

public sealed class Category
{
    public const int MaxDepth = 12;
    public const int MaxNameLength = 60;
    public const int MaxDescriptionLength = 240;

    private Category()
    {
    }

    private Category(
        string id,
        string areaId,
        string? parentId,
        string name,
        string description,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        Id = id;
        AreaId = areaId;
        ParentId = parentId;
        Name = name;
        Description = description;
        SortOrder = sortOrder;
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
        Revision = 1;
    }

    public string Id { get; private set; } = string.Empty;

    public string AreaId { get; private set; } = string.Empty;

    public string? ParentId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Revision { get; private set; }

    public static Category? Create(
        string id,
        string areaId,
        string? parentId,
        string name,
        string description,
        int sortOrder,
        DateTimeOffset timestamp,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        errors = ValidateContent(name, description);
        return errors.Count == 0
            ? new Category(id, areaId, parentId, name.Trim(), description.Trim(), sortOrder, timestamp)
            : null;
    }

    public IReadOnlyDictionary<string, string[]> UpdateContent(
        string name,
        string description,
        DateTimeOffset timestamp)
    {
        var errors = ValidateContent(name, description);
        if (errors.Count > 0)
        {
            return errors;
        }

        Name = name.Trim();
        Description = description.Trim();
        UpdatedAt = timestamp;
        Revision++;
        return errors;
    }

    public void MoveTo(string? parentId, DateTimeOffset timestamp)
    {
        ParentId = parentId;
        UpdatedAt = timestamp;
        Revision++;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateContent(string name, string description)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;

        if (normalizedName.Length is < 1 or > MaxNameLength)
        {
            errors[nameof(Name)] = [$"Nazwa musi mieć od 1 do {MaxNameLength} znaków."];
        }

        if (normalizedDescription.Length > MaxDescriptionLength)
        {
            errors[nameof(Description)] = [$"Opis może mieć najwyżej {MaxDescriptionLength} znaków."];
        }

        return errors;
    }
}

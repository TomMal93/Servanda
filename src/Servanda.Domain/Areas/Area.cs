namespace Servanda.Domain.Areas;

public sealed class Area
{
    private Area()
    {
    }

    private Area(
        string id,
        string name,
        string description,
        string iconKey,
        string accentKey,
        string moduleKey,
        int sortOrder,
        DateTimeOffset timestamp)
    {
        Id = id;
        Name = name;
        Description = description;
        IconKey = iconKey;
        AccentKey = accentKey;
        ModuleKey = moduleKey;
        Availability = "planned";
        SortOrder = sortOrder;
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
        Revision = 1;
    }

    public string Id { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public string IconKey { get; private set; } = string.Empty;

    public string AccentKey { get; private set; } = string.Empty;

    public string ModuleKey { get; private set; } = string.Empty;

    public string Availability { get; private set; } = string.Empty;

    public int SortOrder { get; private set; }

    public bool IsHidden { get; private set; }

    public DateTimeOffset? ArchivedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Revision { get; private set; }

    public static Area CreateSeed(
        string id,
        string name,
        string description,
        string iconKey,
        string accentKey,
        string moduleKey,
        int sortOrder,
        DateTimeOffset timestamp) =>
        new(id, name, description, iconKey, accentKey, moduleKey, sortOrder, timestamp);

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

    public static IReadOnlyDictionary<string, string[]> ValidateContent(string name, string description)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedDescription = description?.Trim() ?? string.Empty;

        if (normalizedName.Length is < 1 or > 80)
        {
            errors[nameof(Name)] = ["Nazwa musi mieć od 1 do 80 znaków."];
        }

        if (normalizedDescription.Length > 300)
        {
            errors[nameof(Description)] = ["Opis może mieć najwyżej 300 znaków."];
        }

        return errors;
    }
}

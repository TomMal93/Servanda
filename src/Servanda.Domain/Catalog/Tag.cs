using Servanda.Domain.Search;

namespace Servanda.Domain.Catalog;

public sealed class Tag
{
    public const int MaxNameLength = 60;

    private Tag()
    {
    }

    private Tag(string id, string areaId, string name, DateTimeOffset timestamp)
    {
        Id = id;
        AreaId = areaId;
        Name = name;
        NormalizedName = NormalizeName(name);
        CreatedAt = timestamp;
        UpdatedAt = timestamp;
        Revision = 1;
    }

    public string Id { get; private set; } = string.Empty;

    public string AreaId { get; private set; } = string.Empty;

    public string Name { get; private set; } = string.Empty;

    public string NormalizedName { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long Revision { get; private set; }

    public static Tag? Create(
        string id,
        string areaId,
        string name,
        DateTimeOffset timestamp,
        out IReadOnlyDictionary<string, string[]> errors)
    {
        errors = ValidateName(name);
        return errors.Count == 0
            ? new Tag(id, areaId, name.Trim(), timestamp)
            : null;
    }

    /// <summary>Odtwarza tag z dokumentu importu z zachowaniem identyfikatora i pól audytowych.</summary>
    public static Tag Restore(
        string id,
        string areaId,
        string name,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt) =>
        new(id, areaId, name, createdAt)
        {
            UpdatedAt = updatedAt,
        };

    public IReadOnlyDictionary<string, string[]> Rename(string name, DateTimeOffset timestamp)
    {
        var errors = ValidateName(name);
        if (errors.Count > 0)
        {
            return errors;
        }

        Name = name.Trim();
        NormalizedName = NormalizeName(Name);
        UpdatedAt = timestamp;
        Revision++;
        return errors;
    }

    public static string NormalizeName(string name)
    {
        var normalized = SearchText.Normalize(name);
        return normalized.Length > MaxNameLength ? normalized[..MaxNameLength] : normalized;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateName(string name)
    {
        var errors = new Dictionary<string, string[]>();
        var normalizedName = name?.Trim() ?? string.Empty;

        if (normalizedName.Length is < 1 or > MaxNameLength)
        {
            errors[nameof(Name)] = [$"Nazwa tagu musi mieć od 1 do {MaxNameLength} znaków."];
        }
        else if (NormalizeName(normalizedName).Length == 0)
        {
            errors[nameof(Name)] = ["Nazwa tagu musi zawierać literę lub cyfrę."];
        }

        return errors;
    }
}

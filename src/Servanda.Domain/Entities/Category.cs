namespace Servanda.Domain.Entities;

public class Category
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public int SortOrder { get; set; }

    public ICollection<Note> Notes { get; set; } = new List<Note>();
}

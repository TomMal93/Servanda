namespace Servanda.Domain.Entities;

public class Note
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? CategoryId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int SortOrder { get; set; }
    public bool IsPinned { get; set; }
    public bool IsArchived { get; set; }

    public Category? Category { get; set; }
    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}

namespace Servanda.Domain.Entities;

public class Tag
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;

    public ICollection<NoteTag> NoteTags { get; set; } = new List<NoteTag>();
}

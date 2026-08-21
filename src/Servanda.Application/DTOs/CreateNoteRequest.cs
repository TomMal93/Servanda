namespace Servanda.Application.DTOs;

public record CreateNoteRequest(
    string Title,
    string Content,
    Guid? CategoryId = null,
    bool IsPinned = false
);

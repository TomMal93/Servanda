namespace Servanda.Application.DTOs;

public record NoteDto(
    Guid Id,
    Guid? CategoryId,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    bool IsPinned,
    bool IsArchived
);

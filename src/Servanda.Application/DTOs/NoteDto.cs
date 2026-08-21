namespace Servanda.Application.DTOs;

public record NoteDto(
    Guid Id,
    Guid? CategoryId,
    string Title,
    string Content,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int SortOrder,
    bool IsPinned,
    bool IsArchived
);

public record ReorderNotesRequest(
    Guid? TargetCategoryId,
    List<Guid> OrderedNoteIds
);

public record MoveNoteRequest(
    Guid? TargetCategoryId,
    int? NewSortOrder = null
);

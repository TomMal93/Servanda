namespace Servanda.Application.DTOs;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Color,
    int SortOrder
);

public record ReorderCategoriesRequest(
    List<Guid> OrderedIds
);

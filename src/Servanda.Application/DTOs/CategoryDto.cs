namespace Servanda.Application.DTOs;

public record CategoryDto(
    Guid Id,
    string Name,
    string? Color,
    int SortOrder,
    Guid? ParentCategoryId = null
);

public record ReorderCategoriesRequest(
    List<Guid> OrderedIds
);

public record UpdateCategoryRequest(
    string Name,
    string? Color = null
);


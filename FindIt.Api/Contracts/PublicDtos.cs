using FindIt.Api.Domain;

namespace FindIt.Api.Contracts;

public sealed record ItemPublicDto(
    int Id,
    string Name,
    string Description,
    string Location,
    string Category,
    ItemStatus Status,
    DateTime FoundOn,
    DateTime CreatedAt,
    DateTime StatusChangedAt,
    IReadOnlyList<ItemPhotoDto> Photos);

public sealed record ItemPhotoDto(int Id);

public sealed record CategoryPublicDto(int Id, string Name);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);

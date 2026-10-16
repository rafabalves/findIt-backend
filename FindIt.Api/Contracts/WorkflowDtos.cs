using FindIt.Api.Domain;
using System.ComponentModel.DataAnnotations;

namespace FindIt.Api.Contracts;

public sealed record MeDto(int Id, string Name, string Email, bool IsAdmin);
public sealed record CreateRequestDto(string Justification);
public sealed record ApproveRequestDto(string? AdminNotes);
public sealed record RejectRequestDto(string AdminNotes);
public sealed record ReturnItemDto([param: Required] string PickupCode);
public sealed record PickupCodeDto(string Code);
public sealed record RequestDto(
    int Id, int ItemId, string ItemName, RequestStatus Status,
    string Justification, string? AdminNotes, DateTime? ExpiresAt,
    DateTime CreatedAt, DateTime UpdatedAt, bool PickupCodeAvailable);
public sealed record CreateItemDto(
    int CategoryId, [param: Required] Guid? QRCodeGuid, string Name, string Description,
    string Location, DateTime FoundOn);
public sealed record UpdateItemDto(
    int CategoryId, string Name, string Description, string Location, DateTime FoundOn);
public sealed record PatchItemDto(
    int? CategoryId, string? Name, string? Description, string? Location, DateTime? FoundOn);
public sealed record ChangeItemStatusDto(ItemStatus Status, string Reason);
public sealed record GenerateQRCodesDto(int Count);
public sealed record QRCodeDto(int Id, Guid Guid);
public sealed record ActivityDto(
    int Id, int? UserId, ActivityType Type, string Description, DateTime CreatedAt);
public sealed record CategoryInputDto(string Name);
public sealed record SetAdministratorDto(bool IsAdmin);
public sealed record AdminUserDto(int Id, string Name, string Email, bool IsAdmin);

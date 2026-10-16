namespace FindIt.Api.Domain;

public enum ItemStatus { Active, Returned, Archived }
public enum RequestStatus { Pending, Approved, Rejected, Cancelled, Expired }
public enum ActivityType
{
    ItemCreated, ItemUpdated, RequestCreated, RequestApproved, RequestRejected,
    RequestCancelled, RequestExpired, ItemReturned, ItemArchived, ItemStatusOverridden
}

public sealed class User
{
    public int Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid ObjectId { get; set; }
    public required string Name { get; set; }
    public required string Email { get; set; }
    public bool IsAdmin { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<RecoveryRequest> RecoveryRequests { get; set; } = [];
}

public sealed class Category
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<Item> Items { get; set; } = [];
}

public sealed class QRCode
{
    public int Id { get; set; }
    public Guid Guid { get; set; }
    public DateTime CreatedAt { get; set; }
    public Item? Item { get; set; }
}

public sealed class Item
{
    public int Id { get; set; }
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
    public int? QRCodeId { get; set; }
    public QRCode? QRCode { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
    public required string Location { get; set; }
    public ItemStatus Status { get; set; } = ItemStatus.Active;
    public DateTime FoundOn { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime StatusChangedAt { get; set; }
    public ICollection<RecoveryRequest> RecoveryRequests { get; set; } = [];
    public ICollection<ActivityLog> ActivityLogs { get; set; } = [];
    public ICollection<ItemPhoto> Photos { get; set; } = [];
}

public sealed class ItemPhoto
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public required string FileName { get; set; }
    public required string ContentType { get; set; }
    public long Size { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RecoveryRequest
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public required string Justification { get; set; }
    public string? AdminNotes { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime? ExpiresAt { get; set; }
    public string? PickupCodeEncrypted { get; set; }
    public int PickupCodeFailedAttempts { get; set; }
    public DateTime? PickupCodeLockedUntil { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ActivityLog
{
    public int Id { get; set; }
    public int ItemId { get; set; }
    public Item Item { get; set; } = null!;
    public int? UserId { get; set; }
    public User? User { get; set; }
    public ActivityType Type { get; set; }
    public required string Description { get; set; }
    public DateTime CreatedAt { get; set; }
}

using FindIt.Api.Data;
using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Services;

public sealed class RequestExpirationService(FindItDbContext db)
{
    public async Task<int> ExpireDueAsync(DateTime utcNow, CancellationToken cancellationToken)
    {
        if (utcNow.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O instante informado deve estar em UTC.", nameof(utcNow));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, cancellationToken);

        var requests = await db.RecoveryRequests
            .Where(x => x.Status == RequestStatus.Approved && x.ExpiresAt <= utcNow
                && x.Item.Status == ItemStatus.Active)
            .ToListAsync(cancellationToken);

        foreach (var request in requests)
        {
            request.Status = RequestStatus.Expired;
            request.PickupCodeEncrypted = null;
            request.UpdatedAt = utcNow;
            db.ActivityLogs.Add(new ActivityLog
            {
                ItemId = request.ItemId,
                UserId = null,
                Type = ActivityType.RequestExpired,
                Description = $"A solicitação {request.Id} expirou sem registro de entrega.",
                CreatedAt = utcNow
            });
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return requests.Count;
    }
}

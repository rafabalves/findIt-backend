using System.Data;
using FindIt.Api.Data;
using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Services;

public sealed class RecoveryWorkflowService(FindItDbContext db, TimeProvider clock)
{
    public async Task<int> CreateAsync(
        int itemId, int userId, string justification, CancellationToken cancellationToken)
    {
        justification = RequiredText(justification, 2000, nameof(justification));
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var item = await db.Items.FindAsync([itemId], cancellationToken)
            ?? throw new KeyNotFoundException("Item não encontrado.");
        if (item.Status != ItemStatus.Active)
        {
            throw new BusinessConflictException("Somente itens ativos aceitam solicitações.");
        }

        var userExists = await db.Users.AnyAsync(x => x.Id == userId, cancellationToken);
        if (!userExists)
        {
            throw new KeyNotFoundException("Usuário não encontrado.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var approved = await db.RecoveryRequests.SingleOrDefaultAsync(x =>
            x.ItemId == itemId && x.Status == RequestStatus.Approved,
            cancellationToken);
        if (approved is not null && approved.ExpiresAt <= now)
        {
            approved.Status = RequestStatus.Expired;
            approved.UpdatedAt = now;
            AddActivity(itemId, null, ActivityType.RequestExpired,
                $"A solicitação {approved.Id} expirou sem registro de entrega.", now);
        }
        else if (approved is not null)
        {
            throw new BusinessConflictException("Este item já possui uma retirada aprovada.");
        }

        var hasPending = await db.RecoveryRequests.AnyAsync(x =>
            x.ItemId == itemId && x.UserId == userId && x.Status == RequestStatus.Pending,
            cancellationToken);
        if (hasPending)
        {
            throw new BusinessConflictException("Você já possui uma solicitação pendente para este item.");
        }

        var request = new RecoveryRequest
        {
            ItemId = itemId,
            UserId = userId,
            Justification = justification,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.RecoveryRequests.Add(request);
        await db.SaveChangesAsync(cancellationToken);
        AddActivity(itemId, userId, ActivityType.RequestCreated,
            $"O usuário {userId} abriu a solicitação {request.Id}.", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return request.Id;
    }

    public async Task CancelAsync(int requestId, int userId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var request = await db.RecoveryRequests.FindAsync([requestId], cancellationToken)
            ?? throw new KeyNotFoundException("Solicitação não encontrada.");
        if (request.UserId != userId)
        {
            throw new UnauthorizedAccessException("A solicitação pertence a outro usuário.");
        }
        if (request.Status != RequestStatus.Pending)
        {
            throw new BusinessConflictException("Somente solicitações pendentes podem ser canceladas pelo solicitante.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        request.Status = RequestStatus.Cancelled;
        request.UpdatedAt = now;
        AddActivity(request.ItemId, userId, ActivityType.RequestCancelled,
            $"O usuário {userId} cancelou a solicitação {request.Id}.", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ApproveAsync(
        int requestId, int administratorId, string? adminNotes, CancellationToken cancellationToken)
    {
        adminNotes = OptionalText(adminNotes, 2000, nameof(adminNotes));
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var administrator = await GetAdministratorAsync(administratorId, cancellationToken);
        var request = await db.RecoveryRequests.FindAsync([requestId], cancellationToken)
            ?? throw new KeyNotFoundException("Solicitação não encontrada.");
        var item = await db.Items.FindAsync([request.ItemId], cancellationToken)
            ?? throw new KeyNotFoundException("Item não encontrado.");
        if (item.Status != ItemStatus.Active || request.Status != RequestStatus.Pending)
        {
            throw new BusinessConflictException("O item deve estar ativo e a solicitação pendente.");
        }

        var requests = await db.RecoveryRequests
            .Where(x => x.ItemId == item.Id &&
                (x.Status == RequestStatus.Pending || x.Status == RequestStatus.Approved))
            .ToListAsync(cancellationToken);
        if (requests.Any(x => x.Status == RequestStatus.Approved))
        {
            throw new BusinessConflictException("Este item já possui uma retirada aprovada.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        request.Status = RequestStatus.Approved;
        request.AdminNotes = adminNotes;
        request.ExpiresAt = BusinessDayDeadline.FiveBusinessDaysAfter(now);
        request.UpdatedAt = now;
        AddActivity(item.Id, administratorId, ActivityType.RequestApproved,
            $"{administrator.Name} aprovou a solicitação {request.Id}.", now);

        foreach (var other in requests.Where(x => x.Id != requestId))
        {
            other.Status = RequestStatus.Rejected;
            other.AdminNotes = "Outra solicitação para este item foi aprovada.";
            other.UpdatedAt = now;
            AddActivity(item.Id, administratorId, ActivityType.RequestRejected,
                $"{administrator.Name} recusou a solicitação {other.Id} após a aprovação da solicitação {request.Id}.", now);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task RejectAsync(
        int requestId, int administratorId, string adminNotes, CancellationToken cancellationToken)
    {
        adminNotes = RequiredText(adminNotes, 2000, nameof(adminNotes));
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var administrator = await GetAdministratorAsync(administratorId, cancellationToken);
        var request = await db.RecoveryRequests.FindAsync([requestId], cancellationToken)
            ?? throw new KeyNotFoundException("Solicitação não encontrada.");
        if (request.Status != RequestStatus.Pending)
        {
            throw new BusinessConflictException("Somente solicitações pendentes podem ser recusadas.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        request.Status = RequestStatus.Rejected;
        request.AdminNotes = adminNotes;
        request.UpdatedAt = now;
        AddActivity(request.ItemId, administratorId, ActivityType.RequestRejected,
            $"{administrator.Name} recusou a solicitação {request.Id}: {adminNotes}", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ReturnAsync(
        int itemId, int administratorId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var administrator = await GetAdministratorAsync(administratorId, cancellationToken);
        var item = await db.Items.FindAsync([itemId], cancellationToken)
            ?? throw new KeyNotFoundException("Item não encontrado.");
        if (item.Status != ItemStatus.Active)
        {
            throw new BusinessConflictException("Somente um item ativo pode ser entregue.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var request = await db.RecoveryRequests.Include(x => x.User)
            .SingleOrDefaultAsync(x => x.ItemId == itemId && x.Status == RequestStatus.Approved,
                cancellationToken);
        if (request is null || request.ExpiresAt <= now)
        {
            throw new BusinessConflictException("Não há solicitação aprovada e válida para esta entrega.");
        }

        item.Status = ItemStatus.Returned;
        item.StatusChangedAt = now;
        item.UpdatedAt = now;
        AddActivity(itemId, administratorId, ActivityType.ItemReturned,
            $"{administrator.Name} registrou a entrega do item ao usuário {request.User.Name} pela solicitação {request.Id}.", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task OverrideStatusAsync(
        int itemId, int administratorId, ItemStatus newStatus, string reason,
        CancellationToken cancellationToken)
    {
        reason = RequiredText(reason, 1000, nameof(reason));
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus));
        }

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var administrator = await GetAdministratorAsync(administratorId, cancellationToken);
        var item = await db.Items.FindAsync([itemId], cancellationToken)
            ?? throw new KeyNotFoundException("Item não encontrado.");
        if (item.Status == newStatus)
        {
            throw new BusinessConflictException("O item já está no estado informado.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var previousStatus = item.Status;
        item.Status = newStatus;
        item.StatusChangedAt = now;
        item.UpdatedAt = now;

        // An exceptional transition never reopens requests from an earlier cycle.
        var openRequests = await db.RecoveryRequests.Where(x => x.ItemId == itemId &&
            (x.Status == RequestStatus.Pending || x.Status == RequestStatus.Approved))
            .ToListAsync(cancellationToken);
        foreach (var request in openRequests)
        {
            request.Status = request.Status == RequestStatus.Approved
                ? RequestStatus.Cancelled : RequestStatus.Rejected;
            request.AdminNotes = reason;
            request.UpdatedAt = now;
            AddActivity(itemId, administratorId,
                request.Status == RequestStatus.Cancelled
                    ? ActivityType.RequestCancelled : ActivityType.RequestRejected,
                $"{administrator.Name} encerrou a solicitação {request.Id}: {reason}", now);
        }

        AddActivity(itemId, administratorId, ActivityType.ItemStatusOverridden,
            $"{administrator.Name} alterou o estado do item de {previousStatus} para {newStatus}: {reason}", now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<User> GetAdministratorAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken)
            ?? throw new KeyNotFoundException("Administrador não encontrado.");
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException("Esta ação exige permissão de administrador.");
        }
        return user;
    }

    private void AddActivity(int itemId, int? userId, ActivityType type, string description, DateTime now) =>
        db.ActivityLogs.Add(new ActivityLog
        {
            ItemId = itemId,
            UserId = userId,
            Type = type,
            Description = description,
            CreatedAt = now
        });

    private static string RequiredText(string value, int maxLength, string name)
    {
        value = value?.Trim() ?? "";
        if (value.Length is 0 || value.Length > maxLength)
        {
            throw new ArgumentException($"O texto deve ter entre 1 e {maxLength} caracteres.", name);
        }
        return value;
    }

    private static string? OptionalText(string? value, int maxLength, string name)
    {
        value = value?.Trim();
        if (value?.Length > maxLength)
        {
            throw new ArgumentException($"O texto deve ter no máximo {maxLength} caracteres.", name);
        }
        return value is { Length: > 0 } ? value : null;
    }
}

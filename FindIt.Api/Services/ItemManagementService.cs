using System.Data;
using FindIt.Api.Data;
using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Services;

public sealed record NewItem(
    int CategoryId, Guid? QRCodeGuid, string Name, string Description,
    string Location, DateTime FoundOn);

public sealed record ItemChanges(
    int CategoryId, string Name, string Description, string Location, DateTime FoundOn);

public sealed class ItemManagementService(FindItDbContext db, TimeProvider clock)
{
    public async Task<int> CreateAsync(
        int administratorId, NewItem input, CancellationToken cancellationToken)
    {
        ValidateItem(input.CategoryId, input.Name, input.Description, input.Location, input.FoundOn);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var administrator = await GetAdministratorAsync(administratorId, cancellationToken);
        await RequireCategoryAsync(input.CategoryId, cancellationToken);

        QRCode? qrCode = null;
        if (input.QRCodeGuid is Guid guid)
        {
            qrCode = await db.QRCodes.SingleOrDefaultAsync(x => x.Guid == guid, cancellationToken)
                ?? throw new KeyNotFoundException("Etiqueta QR não encontrada.");
            if (await db.Items.AnyAsync(x => x.QRCodeId == qrCode.Id, cancellationToken))
            {
                throw new BusinessConflictException("Esta etiqueta QR já está vinculada a um item.");
            }
        }

        var now = clock.GetUtcNow().UtcDateTime;
        var item = new Item
        {
            CategoryId = input.CategoryId,
            QRCodeId = qrCode?.Id,
            Name = input.Name.Trim(),
            Description = input.Description.Trim(),
            Location = input.Location.Trim(),
            FoundOn = input.FoundOn,
            CreatedAt = now,
            UpdatedAt = now,
            StatusChangedAt = now
        };
        db.Items.Add(item);
        await db.SaveChangesAsync(cancellationToken);
        db.ActivityLogs.Add(new ActivityLog
        {
            ItemId = item.Id,
            UserId = administratorId,
            Type = ActivityType.ItemCreated,
            Description = $"{administrator.Name} cadastrou o item {item.Id}.",
            CreatedAt = now
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return item.Id;
    }

    public async Task UpdateAsync(
        int itemId, int administratorId, ItemChanges input, CancellationToken cancellationToken)
    {
        ValidateItem(input.CategoryId, input.Name, input.Description, input.Location, input.FoundOn);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        var administrator = await GetAdministratorAsync(administratorId, cancellationToken);
        await RequireCategoryAsync(input.CategoryId, cancellationToken);
        var item = await db.Items.FindAsync([itemId], cancellationToken)
            ?? throw new KeyNotFoundException("Item não encontrado.");

        item.CategoryId = input.CategoryId;
        item.Name = input.Name.Trim();
        item.Description = input.Description.Trim();
        item.Location = input.Location.Trim();
        item.FoundOn = input.FoundOn;
        item.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        db.ActivityLogs.Add(new ActivityLog
        {
            ItemId = itemId,
            UserId = administratorId,
            Type = ActivityType.ItemUpdated,
            Description = $"{administrator.Name} atualizou o cadastro do item {itemId}.",
            CreatedAt = item.UpdatedAt
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<QRCode>> GenerateQRCodesAsync(
        int administratorId, int count, CancellationToken cancellationToken)
    {
        if (count is < 1 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(count), "Gere entre 1 e 100 etiquetas por vez.");
        }
        await GetAdministratorAsync(administratorId, cancellationToken);

        var now = clock.GetUtcNow().UtcDateTime;
        var codes = Enumerable.Range(0, count).Select(_ =>
        {
            var guid = Guid.NewGuid();
            return new QRCode { Guid = guid, Code = guid.ToString("N"), CreatedAt = now };
        }).ToArray();
        db.QRCodes.AddRange(codes);
        await db.SaveChangesAsync(cancellationToken);
        return codes;
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

    private async Task RequireCategoryAsync(int categoryId, CancellationToken cancellationToken)
    {
        if (!await db.Categories.AnyAsync(x => x.Id == categoryId, cancellationToken))
        {
            throw new KeyNotFoundException("Categoria não encontrada.");
        }
    }

    private static void ValidateItem(
        int categoryId, string name, string description, string location, DateTime foundOn)
    {
        if (categoryId <= 0 || !IsValid(name, 255) || !IsValid(description, 2000)
            || !IsValid(location, 255) || foundOn.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("Dados do item inválidos. Informe categoria, textos e data em UTC.");
        }
    }

    private static bool IsValid(string? value, int maxLength) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maxLength;
}

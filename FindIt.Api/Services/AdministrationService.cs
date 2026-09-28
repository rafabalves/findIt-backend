using System.Data;
using FindIt.Api.Data;
using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Services;

public sealed class AdministrationService(FindItDbContext db, TimeProvider clock)
{
    public async Task<int> CreateCategoryAsync(
        int administratorId, string name, CancellationToken cancellationToken)
    {
        name = ValidName(name);
        await RequireAdministratorAsync(administratorId, cancellationToken);
        if (await db.Categories.AnyAsync(x => x.Name == name, cancellationToken))
        {
            throw new BusinessConflictException("A categoria já existe.");
        }
        var category = new Category
        {
            Name = name,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task RenameCategoryAsync(
        int categoryId, int administratorId, string name, CancellationToken cancellationToken)
    {
        name = ValidName(name);
        await RequireAdministratorAsync(administratorId, cancellationToken);
        var category = await db.Categories.FindAsync([categoryId], cancellationToken)
            ?? throw new KeyNotFoundException("Categoria não encontrada.");
        if (await db.Categories.AnyAsync(x => x.Id != categoryId && x.Name == name,
                cancellationToken))
        {
            throw new BusinessConflictException("A categoria já existe.");
        }
        category.Name = name;
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task SetAdministratorAsync(
        int userId, int administratorId, bool isAdmin, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);
        await RequireAdministratorAsync(administratorId, cancellationToken);
        var user = await db.Users.FindAsync([userId], cancellationToken)
            ?? throw new KeyNotFoundException("Usuário não encontrado.");
        if (user.IsAdmin == isAdmin)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (!isAdmin && await db.Users.CountAsync(x => x.IsAdmin, cancellationToken) <= 1)
        {
            throw new BusinessConflictException("O sistema precisa manter ao menos um administrador.");
        }

        user.IsAdmin = isAdmin;
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RequireAdministratorAsync(int userId, CancellationToken cancellationToken)
    {
        var user = await db.Users.FindAsync([userId], cancellationToken)
            ?? throw new KeyNotFoundException("Administrador não encontrado.");
        if (!user.IsAdmin)
        {
            throw new UnauthorizedAccessException("Esta ação exige permissão de administrador.");
        }
    }

    private static string ValidName(string name)
    {
        name = name?.Trim() ?? "";
        if (name.Length is 0 or > 255)
        {
            throw new ArgumentException("O nome da categoria deve ter entre 1 e 255 caracteres.");
        }
        return name;
    }
}

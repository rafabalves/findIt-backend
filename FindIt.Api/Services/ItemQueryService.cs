using FindIt.Api.Contracts;
using FindIt.Api.Data;
using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Services;

public sealed class ItemQueryService(FindItDbContext db)
{
    public async Task<PagedResult<ItemPublicDto>> SearchActiveAsync(
        string? search, int? categoryId, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = db.Items.AsNoTracking().Where(x => x.Status == ItemStatus.Active);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            if (term.Length > 200)
            {
                throw new ArgumentException("A busca deve ter no máximo 200 caracteres.", nameof(search));
            }
            query = query.Where(x => x.Name.Contains(term) || x.Description.Contains(term));
        }

        if (categoryId is not null)
        {
            query = query.Where(x => x.CategoryId == categoryId);
        }

        return await ToPageAsync(query, page, pageSize, cancellationToken);
    }

    public async Task<PagedResult<ItemPublicDto>> RecentlyClosedAsync(
        int page, int pageSize, CancellationToken cancellationToken)
    {
        var cutoff = DateTime.UtcNow.AddDays(-7);
        var query = db.Items.AsNoTracking().Where(x =>
            (x.Status == ItemStatus.Returned || x.Status == ItemStatus.Archived)
            && x.StatusChangedAt >= cutoff);

        return await ToPageAsync(query, page, pageSize, cancellationToken);
    }

    public Task<ItemPublicDto?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        Project(db.Items.AsNoTracking().Where(x => x.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ItemPublicDto?> GetByQrAsync(Guid guid, CancellationToken cancellationToken) =>
        Project(db.Items.AsNoTracking().Where(x => x.QRCode != null && x.QRCode.Guid == guid))
            .FirstOrDefaultAsync(cancellationToken);

    private static async Task<PagedResult<ItemPublicDto>> ToPageAsync(
        IQueryable<Item> query, int page, int pageSize, CancellationToken cancellationToken)
    {
        page = Math.Clamp(page, 1, 100_000);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var items = await Project(query.OrderByDescending(x => x.CreatedAt)
                .ThenByDescending(x => x.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize))
            .ToListAsync(cancellationToken);

        return new PagedResult<ItemPublicDto>(items, page, pageSize, total);
    }

    private static IQueryable<ItemPublicDto> Project(IQueryable<Item> query) =>
        query.Select(x => new ItemPublicDto(
            x.Id, x.Name, x.Description, x.Location, x.Category.Name, x.Status,
            x.FoundOn, x.CreatedAt, x.StatusChangedAt));
}

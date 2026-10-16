using FindIt.Api.Data;
using FindIt.Api.Domain;
using FindIt.Api.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Tests;

public sealed class ItemQueryServiceTests
{
    [Fact]
    public async Task RecentlyReturnedExcludesArchivedAndOldItems()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<FindItDbContext>()
            .UseSqlite(connection).Options;
        await using var db = new FindItDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var now = DateTime.UtcNow;
        var category = new Category { Name = "Outros", CreatedAt = now };
        db.Items.AddRange(
            MakeItem("Devolvido recente", ItemStatus.Returned, now),
            MakeItem("Arquivado recente", ItemStatus.Archived, now),
            MakeItem("Devolvido antigo", ItemStatus.Returned, now.AddDays(-8)));
        await db.SaveChangesAsync();

        var result = await new ItemQueryService(db).RecentlyReturnedAsync(1, 3, default);

        Assert.Equal(1, result.Total);
        Assert.Equal("Devolvido recente", Assert.Single(result.Items).Name);

        Item MakeItem(string name, ItemStatus status, DateTime changedAt) => new()
        {
            Category = category, Name = name, Description = name,
            Location = "Biblioteca", Status = status, FoundOn = now,
            CreatedAt = changedAt, UpdatedAt = changedAt, StatusChangedAt = changedAt
        };
    }
}

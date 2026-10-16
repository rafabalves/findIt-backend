using FindIt.Api.Data;
using FindIt.Api.Domain;
using FindIt.Api.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace FindIt.Api.Tests;

public sealed class ItemPhotoServiceTests
{
    private static readonly byte[] TinyPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAusB9Y9f5xkAAAAASUVORK5CYII=");

    [Fact]
    public async Task InvalidReplacementPreservesExistingPhoto()
    {
        var folder = Path.Combine(Path.GetTempPath(), "findit-photo-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            await using var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            await using var db = new FindItDbContext(new DbContextOptionsBuilder<FindItDbContext>()
                .UseSqlite(connection).Options);
            await db.Database.EnsureCreatedAsync();
            var now = DateTime.UtcNow;
            var admin = new User { TenantId = Guid.NewGuid(), ObjectId = Guid.NewGuid(),
                Name = "Admin", Email = "admin@example.com", IsAdmin = true, CreatedAt = now };
            var item = new Item { Category = new Category { Name = "Outros", CreatedAt = now },
                Name = "Chave", Description = "Chave encontrada", Location = "Biblioteca",
                FoundOn = now, CreatedAt = now, UpdatedAt = now, StatusChangedAt = now };
            db.Users.Add(admin);
            db.Items.Add(item);
            await db.SaveChangesAsync();

            var settings = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
                { ["Photos:RootPath"] = folder }).Build();
            var service = new ItemPhotoService(db, settings, TimeProvider.System,
                NullLogger<ItemPhotoService>.Instance);
            var original = await service.ReplaceAsync(item.Id, admin.Id,
                [Upload(TinyPng)], default);
            var first = await service.FindAsync(item.Id, original[0].Id, default);
            Assert.NotNull(first);
            var publicItem = await new ItemQueryService(db).GetByIdAsync(item.Id, default);
            Assert.Equal(original[0].Id, Assert.Single(publicItem!.Photos).Id);

            await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceAsync(item.Id,
                admin.Id, [Upload("invalid image"u8.ToArray())], default));
            Assert.True(File.Exists(first.Value.Path));
            Assert.Single(await db.ItemPhotos.AsNoTracking().ToListAsync());
            Assert.Single(Directory.GetFiles(folder));
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static IFormFile Upload(byte[] bytes) =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "files", "photo.png");
}

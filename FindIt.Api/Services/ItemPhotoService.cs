using FindIt.Api.Contracts;
using FindIt.Api.Data;
using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace FindIt.Api.Services;

public sealed class ItemPhotoService(
    FindItDbContext db, IConfiguration configuration, TimeProvider clock,
    ILogger<ItemPhotoService> logger)
{
    private readonly string rootPath = Path.GetFullPath(
        configuration["Photos:RootPath"] ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FindIt", "photos"));
    private readonly int maxCount = Math.Max(1, configuration.GetValue("Photos:MaxCount", 3));
    private readonly long maxBytes = Math.Max(1, configuration.GetValue("Photos:MaxBytes", 5_000_000L));

    public async Task<IReadOnlyList<ItemPhotoDto>> ReplaceAsync(
        int itemId, int administratorId, IReadOnlyList<IFormFile> files,
        CancellationToken cancellationToken)
    {
        if (files.Count > maxCount)
            throw new ArgumentException($"Envie no máximo {maxCount} fotos.");

        var administrator = await db.Users.FindAsync([administratorId], cancellationToken);
        if (administrator?.IsAdmin != true)
            throw new UnauthorizedAccessException("Esta ação exige permissão de administrador.");
        var item = await db.Items.Include(x => x.Photos)
            .SingleOrDefaultAsync(x => x.Id == itemId, cancellationToken)
            ?? throw new KeyNotFoundException("Item não encontrado.");

        Directory.CreateDirectory(rootPath);
        var staged = new List<(string Temporary, string Final, ItemPhoto Photo)>();
        var committed = false;
        try
        {
            foreach (var (file, index) in files.Select((value, index) => (value, index)))
            {
                if (file.Length is <= 0 || file.Length > maxBytes)
                    throw new ArgumentException($"Cada foto deve ter até {maxBytes / 1_000_000} MB.");

                var temporary = Path.Combine(rootPath, $"{Guid.NewGuid():N}.tmp");
                staged.Add((temporary, "", new ItemPhoto
                {
                    ItemId = itemId, FileName = "", ContentType = "",
                    Size = file.Length, SortOrder = index,
                    CreatedAt = clock.GetUtcNow().UtcDateTime
                }));
                await using (var output = new FileStream(temporary, FileMode.CreateNew,
                    FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                    await file.CopyToAsync(output, cancellationToken);

                var (contentType, extension) = DetectFormat(temporary);
                var name = $"{Guid.NewGuid():N}{extension}";
                staged[^1] = (temporary, Path.Combine(rootPath, name), new ItemPhoto
                {
                    ItemId = itemId, FileName = name, ContentType = contentType,
                    Size = file.Length, SortOrder = index,
                    CreatedAt = clock.GetUtcNow().UtcDateTime
                });
            }

            var oldNames = item.Photos.Select(x => x.FileName).ToArray();
            await using (var transaction = await db.Database.BeginTransactionAsync(cancellationToken))
            {
                foreach (var entry in staged)
                    File.Move(entry.Temporary, entry.Final);
                db.ItemPhotos.RemoveRange(item.Photos);
                db.ItemPhotos.AddRange(staged.Select(x => x.Photo));
                item.UpdatedAt = clock.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                committed = true;
            }

            foreach (var name in oldNames)
            {
                try { File.Delete(Path.Combine(rootPath, Path.GetFileName(name))); }
                catch (IOException error) { logger.LogWarning(error, "Could not remove old item photo {FileName}", name); }
                catch (UnauthorizedAccessException error) { logger.LogWarning(error, "Could not remove old item photo {FileName}", name); }
            }
            return staged.Select(x => new ItemPhotoDto(x.Photo.Id)).ToArray();
        }
        finally
        {
            foreach (var entry in staged)
            {
                if (File.Exists(entry.Temporary)) File.Delete(entry.Temporary);
                if (!committed && File.Exists(entry.Final)) File.Delete(entry.Final);
            }
        }
    }

    public async Task<(string Path, string ContentType)?> FindAsync(
        int itemId, int photoId, CancellationToken cancellationToken)
    {
        var photo = await db.ItemPhotos.AsNoTracking()
            .Where(x => x.Id == photoId && x.ItemId == itemId)
            .Select(x => new { x.FileName, x.ContentType })
            .SingleOrDefaultAsync(cancellationToken);
        if (photo is null) return null;
        var path = Path.Combine(rootPath, Path.GetFileName(photo.FileName));
        return File.Exists(path) ? (path, photo.ContentType) : null;
    }

    private static (string ContentType, string Extension) DetectFormat(string path)
    {
        Span<byte> header = stackalloc byte[12];
        using var stream = File.OpenRead(path);
        if (stream.Read(header) < header.Length)
            throw new ArgumentException("Arquivo de imagem inválido.");
        if (header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF)
            return ("image/jpeg", ".jpg");
        if (header[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }))
            return ("image/png", ".png");
        if (header[..4].SequenceEqual("RIFF"u8) && header[8..12].SequenceEqual("WEBP"u8))
            return ("image/webp", ".webp");
        throw new ArgumentException("Use fotos JPEG, PNG ou WebP.");
    }
}

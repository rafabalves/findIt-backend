using FindIt.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FindIt.Api.Data;

public sealed class FindItDbContext(DbContextOptions<FindItDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<QRCode> QRCodes => Set<QRCode>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<RecoveryRequest> RecoveryRequests => Set<RecoveryRequest>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("user");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.TenantId, x.ObjectId }).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("category");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Name).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired();
        });

        modelBuilder.Entity<QRCode>(entity =>
        {
            entity.ToTable("qr_code");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Guid).IsUnique();
        });

        modelBuilder.Entity<Item>(entity =>
        {
            entity.ToTable("item");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.QRCodeId).IsUnique();
            entity.HasIndex(x => new { x.Status, x.StatusChangedAt });
            entity.Property(x => x.Name).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(255).IsRequired();
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Category).WithMany(x => x.Items)
                .HasForeignKey(x => x.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.QRCode).WithOne(x => x.Item)
                .HasForeignKey<Item>(x => x.QRCodeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<RecoveryRequest>(entity =>
        {
            entity.ToTable("recovery_request");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ItemId, x.Status });
            entity.HasIndex(x => new { x.Status, x.ExpiresAt });
            entity.Property(x => x.Justification).HasMaxLength(2000).IsRequired();
            entity.Property(x => x.AdminNotes).HasMaxLength(2000);
            entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(x => x.Item).WithMany(x => x.RecoveryRequests)
                .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany(x => x.RecoveryRequests)
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.ToTable("activity_log");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ItemId, x.CreatedAt });
            entity.Property(x => x.Type).HasConversion<string>().HasMaxLength(40);
            entity.Property(x => x.Description).HasMaxLength(3000).IsRequired();
            entity.HasOne(x => x.Item).WithMany(x => x.ActivityLogs)
                .HasForeignKey(x => x.ItemId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

public sealed class UtcDateTimeConverter() : ValueConverter<DateTime, DateTime>(
    value => value,
    value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Infrastructure;

public sealed class UrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options) : DbContext(options)
{
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var entity = modelBuilder.Entity<ShortUrl>();

        entity.ToTable("short_urls");
        entity.HasKey(shortUrl => shortUrl.Id);
        entity.Property(shortUrl => shortUrl.Id)
            .HasColumnName("id")
            .UseIdentityAlwaysColumn();
        entity.Property(shortUrl => shortUrl.ShortCode)
            .HasColumnName("short_code")
            .HasMaxLength(6)
            .IsRequired();
        entity.HasIndex(shortUrl => shortUrl.ShortCode)
            .IsUnique()
            .HasDatabaseName("ux_short_urls_short_code");
        entity.Property(shortUrl => shortUrl.LongUrl)
            .HasColumnName("long_url")
            .HasMaxLength(2048)
            .IsRequired();
        entity.Property(shortUrl => shortUrl.OwnerId)
            .HasColumnName("owner_id")
            .HasMaxLength(256)
            .IsRequired();
        entity.Property(shortUrl => shortUrl.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false);
        entity.Property(shortUrl => shortUrl.CreatedAt)
            .HasColumnName("created_at")
            .HasColumnType("timestamp with time zone");
        entity.Property(shortUrl => shortUrl.UpdatedAt)
            .HasColumnName("updated_at")
            .HasColumnType("timestamp with time zone");
    }
}

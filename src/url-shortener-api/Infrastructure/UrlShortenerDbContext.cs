using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Infrastructure.Services;

namespace UrlShortener.Infrastructure;

public sealed class UrlShortenerDbContext(DbContextOptions<UrlShortenerDbContext> options) : DbContext(options)
{
    public DbSet<ShortUrl> ShortUrls => Set<ShortUrl>();
    protected override void OnModelCreating(ModelBuilder b)
    {
        var e = b.Entity<ShortUrl>(); e.ToTable("short_urls"); e.HasKey(x => x.Id);
        e.Property(x => x.Id).HasColumnName("id").UseIdentityAlwaysColumn();
        e.Property(x => x.ShortCode).HasColumnName("short_code").HasMaxLength(6).IsRequired();
        e.HasIndex(x => x.ShortCode).IsUnique().HasDatabaseName("ux_short_urls_short_code");
        e.Property(x => x.LongUrl).HasColumnName("long_url").HasMaxLength(2048).IsRequired();
        e.Property(x => x.OwnerId).HasColumnName("owner_id").HasMaxLength(256).IsRequired();
        e.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasDefaultValue(false);
        e.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamp with time zone");
        e.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamp with time zone");
    }
}

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Required configuration key 'ConnectionStrings:PostgreSql' is missing.");
        services.AddDbContext<UrlShortenerDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IShortUrlRepository, EFShortUrlRepository>();
        services.AddSingleton<IShortCodeGenerator, Sha256Base62Generator>();
        services.AddSingleton<IShortUrlClock, SystemShortUrlClock>();
        services.AddSingleton<IPublicUrlBuilder, ConfigurationPublicUrlBuilder>();
        return services;
    }
}

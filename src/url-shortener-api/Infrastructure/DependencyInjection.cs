using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Infrastructure.Services;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Required configuration key 'ConnectionStrings:PostgreSql' is missing.");
        }

        services.AddDbContext<UrlShortenerDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IShortUrlRepository, EFShortUrlRepository>();
        services.AddSingleton<IShortCodeGenerator, Sha256Base62Generator>();
        services.AddSingleton<IShortUrlClock, SystemShortUrlClock>();
        services.AddSingleton<IPublicUrlBuilder, ConfigurationPublicUrlBuilder>();
        return services;
    }
}

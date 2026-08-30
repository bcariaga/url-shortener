using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;
using UrlShortener.Infrastructure.Cache;
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

        services.AddOptions<CacheOptions>().Configure(o =>
        {
            if (int.TryParse(configuration["Cache:TtlSeconds"], out var ttl)) o.TtlSeconds = ttl;
            if (int.TryParse(configuration["Cache:TimeoutMilliseconds"], out var timeout)) o.TimeoutMilliseconds = timeout;
        })
        .Validate(o => o.TtlSeconds > 0 && o.TimeoutMilliseconds > 0, "Cache policy values must be positive")
        .ValidateOnStart();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
            services.AddSingleton<ICacheProvider, NoOpCacheProvider>();
        else
        {
            services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(
                new ConfigurationOptions
                {
                    EndPoints = { redisConnection },
                    AbortOnConnectFail = false,
                    ConnectTimeout = 100,
                    SyncTimeout = 100,
                    AsyncTimeout = 100
                }));
            services.AddSingleton<ICacheProvider, RedisCacheProvider>();
        }
        services.AddScoped<EFShortUrlRepository>();
        services.AddScoped<IShortUrlRepository>(sp =>
            new CachingShortUrlRepository(
                    realRepository: sp.GetRequiredService<EFShortUrlRepository>(),
                    cache: sp.GetRequiredService<ICacheProvider>(),
                    options: sp.GetRequiredService<IOptions<CacheOptions>>(),
                    logger: sp.GetRequiredService<ILogger<CachingShortUrlRepository>>()
                ));
        services.AddSingleton<IShortCodeGenerator, Sha256Base62Generator>();
        services.AddSingleton<IShortUrlClock, SystemShortUrlClock>();
        services.AddSingleton<IPublicUrlBuilder, ConfigurationPublicUrlBuilder>();
        return services;
    }

    private static IServiceCollection AddCache(
        this IServiceCollection services,
        IConfiguration configuration
    )
    {
        services.AddOptions<CacheOptions>()
            .Configure(o =>
                {
                    if (int.TryParse(configuration["Cache:TtlSeconds"], out var ttl)) o.TtlSeconds = ttl;
                    if (int.TryParse(configuration["Cache:TimeoutMilliseconds"], out var timeout)) o.TimeoutMilliseconds = timeout;
                    if (int.TryParse(configuration["Cache:ConnectionTimeoutMilliseconds"], out var connectionTimeout)) o.ConnectionTimeoutMilliseconds = connectionTimeout;
                })
                .Validate(o =>
                    o.TtlSeconds > 0 &&
                    o.TimeoutMilliseconds > 0 &&
                    o.ConnectionTimeoutMilliseconds > 0, "Cache policy values must be positive")
                .ValidateOnStart();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
            services.AddSingleton<ICacheProvider, NoOpCacheProvider>();
        else
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var connectionTimeoutMilliseconds = sp.GetRequiredService<IOptions<CacheOptions>>().Value.ConnectionTimeoutMilliseconds;
                return ConnectionMultiplexer.Connect(
                    new ConfigurationOptions
                    {
                        EndPoints = { redisConnection },
                        AbortOnConnectFail = false,
                        ConnectTimeout = connectionTimeoutMilliseconds,
                        SyncTimeout = connectionTimeoutMilliseconds,
                        AsyncTimeout = connectionTimeoutMilliseconds
                    });
            });
            services.AddSingleton<ICacheProvider, RedisCacheProvider>();
        }

        return services;
    }
}

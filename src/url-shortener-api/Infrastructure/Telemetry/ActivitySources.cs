using System.Diagnostics;
using OpenTelemetry.Trace;
using UrlShortener.Infrastructure.Cache;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Infrastructure.Services;

namespace UrlShortener.Infrastructure.Telemetry;

public static class ActivitySources
{
    public static readonly ActivitySource CachingShortUrlRepository = new(nameof(Repositories.CachingShortUrlRepository));
    public static readonly ActivitySource EfShortUrlRepository = new(nameof(EFShortUrlRepository));
    public static readonly ActivitySource RedisCache = new(nameof(RedisCacheProvider));
    public static readonly ActivitySource PublicUrlBuilder = new(nameof(ConfigurationPublicUrlBuilder));
    public static readonly ActivitySource ShortCodeGenerator = new(nameof(Sha256Base62Generator));

    public static TracerProviderBuilder AddInfrastructureActivitySources(this TracerProviderBuilder builder) =>
        builder
            .AddSource(CachingShortUrlRepository.Name)
            .AddSource(EfShortUrlRepository.Name)
            .AddSource(RedisCache.Name)
            .AddSource(PublicUrlBuilder.Name)
            .AddSource(ShortCodeGenerator.Name);
}

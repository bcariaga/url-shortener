namespace UrlShortener.Infrastructure.Cache;

public sealed class CacheOptions
{
    public int TtlSeconds { get; set; } = 300;
    public int TimeoutMilliseconds { get; set; } = 100;
    public int ConnectionTimeoutMilliseconds { get; set; } = 300;
}

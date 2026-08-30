using StackExchange.Redis;
using UrlShortener.Infrastructure.Telemetry;
namespace UrlShortener.Infrastructure.Cache;
public sealed class RedisCacheProvider(IConnectionMultiplexer redis) : ICacheProvider
{
    public async Task<string?> GetAndRefreshAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.RedisCache.StartActivity(nameof(GetAndRefreshAsync));
        var value = await redis.GetDatabase().ExecuteAsync("GETEX", key, "EX", (long)ttl.TotalSeconds);
        return value.IsNull ? null : value.ToString();
    }

    public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.RedisCache.StartActivity(nameof(SetAsync));
        await redis.GetDatabase().StringSetAsync(key, value, ttl);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.RedisCache.StartActivity(nameof(RemoveAsync));
        await redis.GetDatabase().KeyDeleteAsync(key);
    }
}

using StackExchange.Redis;
namespace UrlShortener.Infrastructure.Cache;
public sealed class RedisCacheProvider(IConnectionMultiplexer redis) : ICacheProvider
{
    public async Task<string?> GetAndRefreshAsync(string key, TimeSpan ttl, CancellationToken cancellationToken)
    {
        var value = await redis.GetDatabase().ExecuteAsync("GETEX", key, "EX", (long)ttl.TotalSeconds);
        return value.IsNull ? null : value.ToString();
    }
    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken) => redis.GetDatabase().StringSetAsync(key, value, ttl);
    public Task RemoveAsync(string key, CancellationToken cancellationToken) => redis.GetDatabase().KeyDeleteAsync(key);
}

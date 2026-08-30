using StackExchange.Redis;
using UrlShortener.Infrastructure.Cache;
using Xunit;

namespace UrlShortener.Infrastructure.Tests;

public sealed class RedisCacheProviderTests
{
    [Fact]
    public async Task GetAndRefresh_uses_getex_and_sliding_ttl_when_redis_is_opted_in()
    {
        var endpoint = Environment.GetEnvironmentVariable("URL_SHORTENER_TEST_REDIS");
        if (string.IsNullOrWhiteSpace(endpoint)) return;
        using var redis = await ConnectionMultiplexer.ConnectAsync(endpoint);
        var provider = new RedisCacheProvider(redis);
        await redis.GetDatabase().StringSetAsync("short-url:integration", "https://integration.test", TimeSpan.FromSeconds(1));
        await Task.Delay(100);
        Assert.Equal("https://integration.test", await provider.GetAndRefreshAsync("short-url:integration", TimeSpan.FromSeconds(300), default));
        Assert.True((await redis.GetDatabase().KeyTimeToLiveAsync("short-url:integration")) > TimeSpan.FromSeconds(295));
        await provider.SetAsync("short-url:integration", "https://set.test", TimeSpan.FromSeconds(30), default);
        Assert.Equal("https://set.test", await redis.GetDatabase().StringGetAsync("short-url:integration"));
        Assert.True((await redis.GetDatabase().KeyTimeToLiveAsync("short-url:integration")) > TimeSpan.FromSeconds(25));
        await provider.RemoveAsync("short-url:integration", default);
        Assert.False(await redis.GetDatabase().KeyExistsAsync("short-url:integration"));
    }
}

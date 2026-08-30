namespace UrlShortener.Infrastructure.Cache;
public sealed class NoOpCacheProvider : ICacheProvider
{
    public Task<string?> GetAndRefreshAsync(string key, TimeSpan ttl, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
    public Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken) => Task.CompletedTask;
    public Task RemoveAsync(string key, CancellationToken cancellationToken) => Task.CompletedTask;
}

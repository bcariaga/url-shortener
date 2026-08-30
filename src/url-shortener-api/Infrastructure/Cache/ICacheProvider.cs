namespace UrlShortener.Infrastructure.Cache;
public interface ICacheProvider
{
    Task<string?> GetAndRefreshAsync(string key, TimeSpan ttl, CancellationToken cancellationToken);
    Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken);
    Task RemoveAsync(string key, CancellationToken cancellationToken);
}

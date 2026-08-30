using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Infrastructure.Cache;
using UrlShortener.Infrastructure.Telemetry;
namespace UrlShortener.Infrastructure.Repositories;

public sealed class CachingShortUrlRepository(
    IShortUrlRepository realRepository,
    ICacheProvider cache,
    IOptions<CacheOptions> options,
    ILogger<CachingShortUrlRepository> logger) : IShortUrlRepository
{
    private TimeSpan Ttl => TimeSpan.FromSeconds(options.Value.TtlSeconds);
    private static string Key(string code) => $"short-url:{code}";

    public async Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.CachingShortUrlRepository.StartActivity(nameof(InsertAsync));
        var result = await realRepository.InsertAsync(entity, cancellationToken);
        await Cache(ct => cache.SetAsync(Key(entity.ShortCode), entity.LongUrl, Ttl, ct), cancellationToken);
        return result;
    }

    public async Task<ShortUrl?> FindActiveAsync(string ownerId, string code, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.CachingShortUrlRepository.StartActivity(nameof(FindActiveAsync));
        return await realRepository.FindActiveAsync(ownerId, code, cancellationToken);
    }

    public async Task<string?> FindActiveDestinationByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.CachingShortUrlRepository.StartActivity(nameof(FindActiveDestinationByCodeAsync));
        var hit = await Read(ct => cache.GetAndRefreshAsync(Key(code), Ttl, ct), cancellationToken);
        if (Uri.TryCreate(hit, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return hit;
        }

        var value = await realRepository.FindActiveDestinationByCodeAsync(code, cancellationToken);
        if (value is not null)
        {
            await Cache(ct => cache.SetAsync(Key(code), value, Ttl, ct), cancellationToken);
        }

        return value;
    }

    public async Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.CachingShortUrlRepository.StartActivity(nameof(SaveAsync));
        await realRepository.SaveAsync(entity, cancellationToken);
        if (entity.IsDeleted)
        {
            await Cache(ct => cache.RemoveAsync(Key(entity.ShortCode), ct), cancellationToken);
        }
        else
        {
            await Cache(ct => cache.SetAsync(Key(entity.ShortCode), entity.LongUrl, Ttl, ct), cancellationToken);
        }
    }

    private async Task Cache(Func<CancellationToken, Task> action, CancellationToken callerToken)
    {
        try
        {
            using var c = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            c.CancelAfter(options.Value.TimeoutMilliseconds);
            await action(c.Token)
                .WaitAsync(TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds), callerToken);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { }
        catch (Exception ex) { logger.LogWarning(ex, "Cache operation failed."); }
    }
    private async Task<T?> Read<T>(Func<CancellationToken, Task<T?>> action, CancellationToken callerToken) where T : class
    {
        try
        {
            using var c = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            c.CancelAfter(options.Value.TimeoutMilliseconds);
            return await action(c.Token).WaitAsync(TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds), callerToken);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested) { throw; }
        catch (OperationCanceledException) { return null; }
        catch (Exception ex) { logger.LogWarning(ex, "Cache operation failed."); return null; }
    }
}

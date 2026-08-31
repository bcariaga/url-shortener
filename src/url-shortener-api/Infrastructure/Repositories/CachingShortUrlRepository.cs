using System.Diagnostics;
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

        await Cache(ct =>
            cache.SetAsync(
                Key(entity.ShortCode),
                entity.LongUrl,
                Ttl,
                ct),
            activity!,
            cancellationToken);

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

        var (Value, Outcome) = await Read(ct => cache.GetAndRefreshAsync(Key(code), Ttl, ct), activity!, cancellationToken);

        var hit = Value;

        if (hit is not null)
            activity?.AddEvent(new ActivityEvent("cache.hit"));

        if (Uri.TryCreate(hit, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return hit;
        }

        if (Outcome == CacheReadOutcome.Value)
            activity?.AddEvent(new ActivityEvent(hit is null ? "cache.miss" : "cache.invalid_value"));

        var value = await realRepository.FindActiveDestinationByCodeAsync(code, cancellationToken);

        if (value is not null)
        {
            await Cache(ct => cache.SetAsync(Key(code), value, Ttl, ct), activity!, cancellationToken);
        }

        return value;
    }

    public async Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.CachingShortUrlRepository.StartActivity(nameof(SaveAsync));
        await realRepository.SaveAsync(entity, cancellationToken);
        if (entity.IsDeleted)
        {
            await Cache(ct => cache.RemoveAsync(Key(entity.ShortCode), ct), activity!, cancellationToken);
        }
        else
        {
            await Cache(ct => cache.SetAsync(Key(entity.ShortCode), entity.LongUrl, Ttl, ct), activity!, cancellationToken);
        }
    }

    private async Task Cache(Func<CancellationToken, Task> action, Activity activity, CancellationToken callerToken)
    {
        try
        {
            using var c = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            c.CancelAfter(options.Value.TimeoutMilliseconds);
            await action(c.Token)
                .WaitAsync(TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds), callerToken);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            activity?.AddEvent(new ActivityEvent("cache.read.cancellationRequest"));
            throw;
        }
        catch (OperationCanceledException)
        {
            activity?.AddEvent(new ActivityEvent("cache.write.timeout"));
        }
        catch (TimeoutException)
        {
            activity?.AddEvent(new ActivityEvent("cache.write.timeout"));
        }
        catch (Exception ex)
        {
            activity?.AddEvent(new ActivityEvent(
                "cache.write.error",
                tags: new ActivityTagsCollection {
                    { "exception.type", ex.GetType().FullName }
                }));
            logger.LogWarning(ex, "Cache operation failed.");
        }
    }
    private async Task<(T? Value, CacheReadOutcome Outcome)> Read<T>(
        Func<CancellationToken, Task<T?>> action,
        Activity activity,
        CancellationToken callerToken) where T : class
    {
        try
        {
            using var c = CancellationTokenSource.CreateLinkedTokenSource(callerToken);
            c.CancelAfter(options.Value.TimeoutMilliseconds);
            return (
                await action(c.Token).WaitAsync(
                    TimeSpan.FromMilliseconds(options.Value.TimeoutMilliseconds),
                callerToken),
            CacheReadOutcome.Value);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            activity?.AddEvent(new ActivityEvent("cache.read.cancellationRequest"));
            throw;
        }
        catch (OperationCanceledException)
        {
            activity?.AddEvent(new ActivityEvent("cache.read.timeout"));
            return (null, CacheReadOutcome.Degraded);
        }
        catch (TimeoutException)
        {
            activity?.AddEvent(new ActivityEvent("cache.read.timeout"));
            return (null, CacheReadOutcome.Degraded);
        }
        catch (Exception ex)
        {
            activity?.AddEvent(new ActivityEvent(
                "cache.read.error",
                tags: new ActivityTagsCollection {
                    { "exception.type", ex.GetType().FullName}
                }));
            logger.LogWarning(ex, "Cache operation failed.");

            return (null, CacheReadOutcome.Degraded);
        }
    }

    private enum CacheReadOutcome { Value, Degraded }
}

using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Infrastructure.Exceptions;
using UrlShortener.Infrastructure.Resilience;

namespace UrlShortener.Infrastructure.Repositories;

public sealed class ResilientPostgresShortUrlRepository(
    IShortUrlRepository repository,
    ResiliencePipeline circuitBreakerPipeline,
    ResiliencePipeline readRetryPipeline,
    IOptions<DatabaseResilienceOptions> options) : IShortUrlRepository
{
    public Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken) =>
        ExecuteAsync(token => repository.InsertAsync(entity, token), cancellationToken);

    public Task<ShortUrl?> FindActiveAsync(string ownerId, string code, CancellationToken cancellationToken) =>
        ExecuteReadAsync(token => repository.FindActiveAsync(ownerId, code, token), cancellationToken);

    public Task<string?> FindActiveDestinationByCodeAsync(string code, CancellationToken cancellationToken) =>
        ExecuteReadAsync(token => repository.FindActiveDestinationByCodeAsync(code, token), cancellationToken);

    public Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken) =>
        ExecuteAsync(token => repository.SaveAsync(entity, token), cancellationToken);

    private async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken cancellationToken)
    {
        try
        {
            await circuitBreakerPipeline.ExecuteAsync(
                async token => await action(token),
                cancellationToken);
        }
        catch (BrokenCircuitException exception)
        {
            throw new DatabaseUnavailableException(
                TimeSpan.FromSeconds(options.Value.BreakDurationSeconds),
                exception);
        }
        catch (Exception exception) when (PostgresTransientFailureDetector.IsTransient(exception))
        {
            throw new DatabaseUnavailableException(null, exception);
        }
    }

    private async Task<T> ExecuteAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await circuitBreakerPipeline.ExecuteAsync(
                async token => await action(token),
                cancellationToken);
        }
        catch (BrokenCircuitException exception)
        {
            throw new DatabaseUnavailableException(
                TimeSpan.FromSeconds(options.Value.BreakDurationSeconds),
                exception);
        }
        catch (Exception exception) when (PostgresTransientFailureDetector.IsTransient(exception))
        {
            throw new DatabaseUnavailableException(null, exception);
        }
    }

    private Task<T> ExecuteReadAsync<T>(
        Func<CancellationToken, Task<T>> action,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            async token => await readRetryPipeline.ExecuteAsync(
                async retryToken => await action(retryToken),
                token),
            cancellationToken);
}

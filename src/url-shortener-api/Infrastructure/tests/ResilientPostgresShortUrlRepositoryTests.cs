using Microsoft.Extensions.Options;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Npgsql;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Infrastructure.Exceptions;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Infrastructure.Resilience;
using Xunit;

namespace UrlShortener.Infrastructure.Tests;

public sealed class ResilientPostgresShortUrlRepositoryTests
{
    [Fact]
    public async Task Exhausted_read_retries_open_shared_circuit_and_recovery_closes_it()
    {
        var database = new FakeRepository();
        for (var attempt = 0; attempt < 6; attempt++)
        {
            database.Outcomes.Enqueue(new TimeoutException());
        }
        var repository = Create(database, breakDuration: TimeSpan.FromMilliseconds(500));

        await Assert.ThrowsAsync<DatabaseUnavailableException>(() =>
            repository.FindActiveDestinationByCodeAsync("abc123", default));
        await Assert.ThrowsAsync<DatabaseUnavailableException>(() =>
            repository.FindActiveDestinationByCodeAsync("abc123", default));

        var openException = await Assert.ThrowsAsync<DatabaseUnavailableException>(() =>
            repository.FindActiveDestinationByCodeAsync("abc123", default));
        Assert.Equal(TimeSpan.FromSeconds(1), openException.RetryAfter);
        Assert.Equal(6, database.Calls);

        await Task.Delay(TimeSpan.FromMilliseconds(600));

        Assert.Equal("https://db.test", await repository.FindActiveDestinationByCodeAsync("abc123", default));
        Assert.Equal(7, database.Calls);
    }

    [Fact]
    public async Task Read_retries_ef_transient_wrapper_and_recovers()
    {
        var database = new FakeRepository();
        database.Outcomes.Enqueue(EfTransientFailure());
        database.Outcomes.Enqueue(EfTransientFailure());
        var repository = Create(database);

        var result = await repository.FindActiveDestinationByCodeAsync("abc123", default);

        Assert.Equal("https://db.test", result);
        Assert.Equal(3, database.Calls);
    }

    [Fact]
    public async Task Write_transient_failure_is_not_retried()
    {
        var database = new FakeRepository();
        database.Outcomes.Enqueue(EfTransientFailure());
        var repository = Create(database);
        var entity = ShortUrl.Create("abc123", "https://new.test", "owner", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<DatabaseUnavailableException>(() => repository.InsertAsync(entity, default));

        Assert.Equal(1, database.Calls);
    }

    [Fact]
    public async Task Non_transient_failures_do_not_open_circuit()
    {
        var database = new FakeRepository();
        database.Outcomes.Enqueue(new ShortCodeConflictException());
        database.Outcomes.Enqueue(new ShortCodeConflictException());
        var repository = Create(database);
        var entity = ShortUrl.Create("abc123", "https://new.test", "owner", DateTimeOffset.UtcNow);

        await Assert.ThrowsAsync<ShortCodeConflictException>(() => repository.InsertAsync(entity, default));
        await Assert.ThrowsAsync<ShortCodeConflictException>(() => repository.InsertAsync(entity, default));

        Assert.Same(entity, await repository.InsertAsync(entity, default));
        Assert.Equal(3, database.Calls);
    }

    [Fact]
    public async Task Cancellation_propagates_and_does_not_open_circuit()
    {
        var database = new FakeRepository();
        database.Outcomes.Enqueue(new OperationCanceledException());
        database.Outcomes.Enqueue(new OperationCanceledException());
        var repository = Create(database);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.FindActiveDestinationByCodeAsync("abc123", default));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            repository.FindActiveDestinationByCodeAsync("abc123", default));

        Assert.Equal("https://db.test", await repository.FindActiveDestinationByCodeAsync("abc123", default));
        Assert.Equal(3, database.Calls);
    }

    private static ResilientPostgresShortUrlRepository Create(
        IShortUrlRepository repository,
        TimeSpan? breakDuration = null)
    {
        var options = Options.Create(new DatabaseResilienceOptions
        {
            FailureRatio = 1,
            MinimumThroughput = 2,
            SamplingDurationSeconds = 1,
            BreakDurationSeconds = 1
        });
        var pipeline = new ResiliencePipelineBuilder()
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(PostgresTransientFailureDetector.IsTransient),
                FailureRatio = options.Value.FailureRatio,
                MinimumThroughput = options.Value.MinimumThroughput,
                SamplingDuration = TimeSpan.FromSeconds(options.Value.SamplingDurationSeconds),
                BreakDuration = breakDuration ?? TimeSpan.FromSeconds(options.Value.BreakDurationSeconds)
            })
            .Build();
        var readRetryPipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(PostgresTransientFailureDetector.IsTransient),
                MaxRetryAttempts = options.Value.ReadMaxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(1),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            })
            .Build();

        return new ResilientPostgresShortUrlRepository(repository, pipeline, readRetryPipeline, options);
    }

    private static InvalidOperationException EfTransientFailure() =>
        new(
            "An exception has been raised that is likely due to a transient failure.",
            new PostgresException(
                "terminating connection due to administrator command",
                "FATAL",
                "FATAL",
                "57P01"));

    private sealed class FakeRepository : IShortUrlRepository
    {
        public Queue<Exception?> Outcomes { get; } = [];
        public int Calls { get; private set; }

        public Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken) =>
            Complete(entity);

        public Task<ShortUrl?> FindActiveAsync(string ownerId, string code, CancellationToken cancellationToken) =>
            Complete<ShortUrl?>(null);

        public Task<string?> FindActiveDestinationByCodeAsync(string code, CancellationToken cancellationToken) =>
            Complete<string?>("https://db.test");

        public Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken) =>
            Complete(true);

        private Task<T> Complete<T>(T result)
        {
            Calls++;
            if (Outcomes.TryDequeue(out var exception) && exception is not null)
            {
                throw exception;
            }

            return Task.FromResult(result);
        }
    }
}

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Infrastructure.Cache;
using UrlShortener.Infrastructure.Repositories;
using Xunit;

namespace UrlShortener.Infrastructure.Tests;

public sealed class CachingShortUrlRepositoryTests
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(300);

    [Fact]
    public async Task Hit_uses_exact_key_and_ttl_without_database()
    {
        var db = new FakeDatabase();
        var cache = new FakeCache { GetValue = "https://cached.test" };
        var result = await Create(db, cache).FindActiveDestinationByCodeAsync("abc123", default);
        Assert.Equal("https://cached.test", result);
        Assert.Equal(new[] { "cache-get" }, cache.Events);
        Assert.Equal("short-url:abc123", cache.GetKey);
        Assert.Equal(Ttl, cache.GetTtl);
        Assert.Equal(0, db.Reads);
    }

    [Fact]
    public async Task Miss_reads_database_then_sets_exact_value_and_ttl()
    {
        var db = new FakeDatabase { Destination = "https://db.test" };
        var cache = new FakeCache();
        var result = await Create(db, cache).FindActiveDestinationByCodeAsync("abc123", default);
        Assert.Equal(db.Destination, result);
        Assert.Equal(new[] { "cache-get", "db-read", "cache-set" }, cache.Events);
        Assert.Equal("short-url:abc123", cache.SetKey);
        Assert.Equal(db.Destination, cache.SetValue);
        Assert.Equal(Ttl, cache.SetTtl);
    }

    [Fact]
    public async Task Insert_collision_does_not_cache()
    {
        var db = new FakeDatabase { InsertError = new ShortCodeConflictException() };
        var cache = new FakeCache();
        await Assert.ThrowsAsync<ShortCodeConflictException>(() => Create(db, cache).InsertAsync(Entity("abc123", "https://new.test"), default));
        Assert.Equal(new[] { "db-insert" }, cache.Events);
    }

    [Fact]
    public async Task Insert_database_failure_does_not_cache()
    {
        var db = new FakeDatabase { InsertError = new InvalidOperationException("database failure") };
        var cache = new FakeCache();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(db, cache).InsertAsync(Entity("abc123", "https://new.test"), default));
        Assert.Equal(new[] { "db-insert" }, cache.Events);
    }

    [Fact]
    public async Task Insert_sets_exact_arguments_after_database()
    {
        var db = new FakeDatabase();
        var cache = new FakeCache();
        await Create(db, cache).InsertAsync(Entity("abc123", "https://new.test"), default);
        Assert.Equal(new[] { "db-insert", "cache-set" }, cache.Events);
        Assert.Equal("short-url:abc123", cache.SetKey);
        Assert.Equal("https://new.test", cache.SetValue);
        Assert.Equal(Ttl, cache.SetTtl);
    }

    [Fact]
    public async Task Update_sets_after_database_and_cache_failure_preserves_success()
    {
        var db = new FakeDatabase();
        var cache = new FakeCache { SetError = new InvalidOperationException() };
        await Create(db, cache).SaveAsync(Entity("abc123", "https://updated.test"), default);
        Assert.Equal(new[] { "db-save", "cache-set" }, cache.Events);
        Assert.Equal("short-url:abc123", cache.SetKey);
        Assert.Equal("https://updated.test", cache.SetValue);
        Assert.Equal(Ttl, cache.SetTtl);
    }

    [Fact]
    public async Task Delete_removes_exact_key_after_database_and_failure_preserves_success()
    {
        var db = new FakeDatabase();
        var cache = new FakeCache { RemoveError = new InvalidOperationException() };
        var entity = Entity("abc123", "https://deleted.test");
        entity.Delete(DateTimeOffset.UtcNow);
        await Create(db, cache).SaveAsync(entity, default);
        Assert.Equal(new[] { "db-save", "cache-delete" }, cache.Events);
        Assert.Equal("short-url:abc123", cache.RemoveKey);
    }

    [Fact]
    public async Task Update_database_failure_does_not_cache()
    {
        var db = new FakeDatabase { SaveError = new InvalidOperationException() };
        var cache = new FakeCache();
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(db, cache).SaveAsync(Entity("abc123", "https://updated.test"), default));
        Assert.Equal(new[] { "db-save" }, cache.Events);
    }

    [Fact]
    public async Task Delete_database_failure_does_not_remove_cache()
    {
        var db = new FakeDatabase { SaveError = new InvalidOperationException() };
        var cache = new FakeCache();
        var entity = Entity("abc123", "https://deleted.test");
        entity.Delete(DateTimeOffset.UtcNow);
        await Assert.ThrowsAsync<InvalidOperationException>(() => Create(db, cache).SaveAsync(entity, default));
        Assert.Equal(new[] { "db-save" }, cache.Events);
    }

    [Fact]
    public async Task Update_set_timeout_preserves_success_and_arguments()
    {
        var db = new FakeDatabase();
        var cache = new FakeCache { SetDelay = TimeSpan.FromMilliseconds(500) };
        await Create(db, cache, 20).SaveAsync(Entity("abc123", "https://updated.test"), default);
        Assert.Equal(new[] { "db-save", "cache-set" }, cache.Events);
        Assert.Equal("short-url:abc123", cache.SetKey);
        Assert.Equal("https://updated.test", cache.SetValue);
        Assert.Equal(Ttl, cache.SetTtl);
    }

    [Fact]
    public async Task Delete_remove_timeout_preserves_success_and_key()
    {
        var db = new FakeDatabase();
        var cache = new FakeCache { RemoveDelay = TimeSpan.FromMilliseconds(500) };
        var entity = Entity("abc123", "https://deleted.test");
        entity.Delete(DateTimeOffset.UtcNow);
        await Create(db, cache, 20).SaveAsync(entity, default);
        Assert.Equal(new[] { "db-save", "cache-delete" }, cache.Events);
        Assert.Equal("short-url:abc123", cache.RemoveKey);
    }

    [Fact]
    public async Task Writes_pass_caller_token_to_database_and_cancellation_prevents_cache()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var db = new FakeDatabase { SaveError = new OperationCanceledException(cts.Token) };
        var cache = new FakeCache();
        var entity = Entity("abc123", "https://updated.test");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(db, cache).SaveAsync(entity, cts.Token));
        Assert.Equal(cts.Token, db.LastToken);
        Assert.Equal(new[] { "db-save" }, cache.Events);
    }

    [Fact]
    public async Task Insert_passes_caller_token_and_cancellation_prevents_cache()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var db = new FakeDatabase { InsertError = new OperationCanceledException(cts.Token) };
        var cache = new FakeCache();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(db, cache).InsertAsync(Entity("abc123", "https://new.test"), cts.Token));
        Assert.Equal(cts.Token, db.LastToken);
        Assert.Equal(new[] { "db-insert" }, cache.Events);
    }

    [Fact]
    public async Task Caller_cancellation_propagates_without_database()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var db = new FakeDatabase();
        var cache = new FakeCache { GetError = new OperationCanceledException() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Create(db, cache).FindActiveDestinationByCodeAsync("abc123", cts.Token));
        Assert.Equal(0, db.Reads);
    }

    [Fact]
    public async Task Uncooperative_cache_timeout_falls_back_to_database()
    {
        var db = new FakeDatabase { Destination = "https://db.test" };
        var cache = new FakeCache { Delay = TimeSpan.FromMilliseconds(500) };
        var result = await Create(db, cache).FindActiveDestinationByCodeAsync("abc123", default);
        Assert.Equal(db.Destination, result);
        Assert.Equal(new[] { "cache-get", "db-read", "cache-set" }, cache.Events);
    }

    private static CachingShortUrlRepository Create(FakeDatabase db, FakeCache cache, int timeout = 100)
    {
        cache.Events = db.Events;
        return new(db, cache, Options.Create(new CacheOptions { TtlSeconds = 300, TimeoutMilliseconds = timeout }), NullLogger<CachingShortUrlRepository>.Instance);
    }
    private static ShortUrl Entity(string code, string url) => ShortUrl.Create(code, url, "owner", DateTimeOffset.UtcNow);

    private sealed class FakeDatabase : IShortUrlRepository
    {
        public string? Destination { get; init; }
        public List<string> Events { get; } = [];
        public Exception? InsertError { get; init; }
        public Exception? SaveError { get; init; }
        public CancellationToken LastToken { get; private set; }
        public int Reads { get; private set; }
        public Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken) { Events.Add("db-insert"); LastToken = cancellationToken; if (InsertError is not null) throw InsertError; return Task.FromResult(entity); }
        public Task<ShortUrl?> FindActiveAsync(string ownerId, string code, CancellationToken cancellationToken) => Task.FromResult<ShortUrl?>(null);
        public Task<string?> FindActiveDestinationByCodeAsync(string code, CancellationToken cancellationToken) { Events.Add("db-read"); Reads++; return Task.FromResult(Destination); }
        public Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken) { Events.Add("db-save"); LastToken = cancellationToken; if (SaveError is not null) throw SaveError; return Task.CompletedTask; }
    }

    private sealed class FakeCache : ICacheProvider
    {
        public List<string> Events { get; set; } = [];
        public string? GetValue { get; init; }
        public string? GetKey { get; private set; }
        public TimeSpan? GetTtl { get; private set; }
        public string? SetKey { get; private set; }
        public string? SetValue { get; private set; }
        public TimeSpan? SetTtl { get; private set; }
        public string? RemoveKey { get; private set; }
        public TimeSpan Delay { get; init; }
        public TimeSpan SetDelay { get; init; }
        public TimeSpan RemoveDelay { get; init; }
        public Exception? GetError { get; init; }
        public Exception? SetError { get; init; }
        public Exception? RemoveError { get; init; }
        public async Task<string?> GetAndRefreshAsync(string key, TimeSpan ttl, CancellationToken cancellationToken) { Events.Add("cache-get"); GetKey = key; GetTtl = ttl; if (Delay > TimeSpan.Zero) await Task.Delay(Delay); if (GetError is not null) throw GetError; return GetValue; }
        public async Task SetAsync(string key, string value, TimeSpan ttl, CancellationToken cancellationToken) { Events.Add("cache-set"); SetKey = key; SetValue = value; SetTtl = ttl; if (SetDelay > TimeSpan.Zero) await Task.Delay(SetDelay); if (SetError is not null) throw SetError; }
        public async Task RemoveAsync(string key, CancellationToken cancellationToken) { Events.Add("cache-delete"); RemoveKey = key; if (RemoveDelay > TimeSpan.Zero) await Task.Delay(RemoveDelay); if (RemoveError is not null) throw RemoveError; }
    }
}

using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Services;
using Xunit;

namespace UrlShortener.Application.Tests;

public sealed class ShortUrlManagementTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("relative/path")]
    [InlineData("ftp://example.com")]
    public async Task Invalid_destinations_are_rejected(string? url)
    { var store = new Store(); var handler = new CreateShortUrlCommandHandler(store, new Generator(), new Clock(), new Nonce(), new Builder()); await Assert.ThrowsAsync<ShortUrlValidationException>(() => handler.HandleAsync(new("owner", url!))); Assert.Empty(store.Inserted); }

    [Fact]
    public async Task Create_retries_with_same_nonce_and_five_counters()
    { var store = new Store { Conflicts = 2 }; var generator = new Generator(); var h = new CreateShortUrlCommandHandler(store, generator, new Clock(), new Nonce(), new Builder()); var result = await h.HandleAsync(new("owner", "https://example.com")); Assert.Equal("code02", result.ShortCode); Assert.Equal(new[] { 0, 1, 2 }, generator.Counters); Assert.Single(store.Inserted); }
    [Fact]
    public async Task Five_conflicts_exhaust_and_nonconflict_propagates()
    { var store = new Store { Conflicts = 5 }; var h = new CreateShortUrlCommandHandler(store, new Generator(), new Clock(), new Nonce(), new Builder()); await Assert.ThrowsAsync<ShortCodeAttemptsExhaustedException>(() => h.HandleAsync(new("o", "https://x"))); store.Conflicts = 0; store.Error = new IOException(); await Assert.ThrowsAsync<IOException>(() => h.HandleAsync(new("o", "https://x"))); }
    [Fact]
    public async Task Update_and_delete_filter_by_owner_and_are_idempotent()
    { var item = ShortUrl.Create("abc123", "https://old", "owner", DateTimeOffset.UtcNow); var store = new Store { Existing = item }; var clock = new Clock(); var update = new UpdateShortUrlCommandHandler(store, clock, new Builder()); var result = await update.HandleAsync(new("owner", "abc123", "https://new")); Assert.Equal("abc123", result!.ShortCode); Assert.Equal("owner", store.Owner); var delete = new DeleteShortUrlCommandHandler(store, clock); Assert.True(await delete.HandleAsync(new("owner", "abc123"))); Assert.True(item.IsDeleted); }

    private sealed class Store : IShortUrlStore { public List<ShortUrl> Inserted = []; public int Conflicts; public Exception? Error; public ShortUrl? Existing; public string? Owner; public Task<ShortUrl> InsertAsync(ShortUrl e, CancellationToken _) { if (Error is not null) throw Error; if (Conflicts-- > 0) throw new ShortCodeConflictException(); Inserted.Add(e); return Task.FromResult(e); } public Task<ShortUrl?> FindActiveAsync(string o, string c, CancellationToken _) { Owner = o; return Task.FromResult(Existing); } public Task SaveAsync(CancellationToken _) => Task.CompletedTask; }
    private sealed class Generator : IShortCodeGenerator { public List<int> Counters = []; public string Generate(string o, string u, string n, int c) { Counters.Add(c); return $"code0{c}"; } }
    private sealed class Clock : IShortUrlClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }
    private sealed class Nonce : IShortUrlNonce { public string Create() => "nonce"; }
    private sealed class Builder : IPublicUrlBuilder { public string Build(string c) => "http://localhost/" + c; }
}

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Application;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Services;
using Xunit;

namespace UrlShortener.Api.Tests;

public sealed class ManagementApiTests : IClassFixture<ManagementFactory>
{
    private readonly ManagementFactory factory;
    public ManagementApiTests(ManagementFactory factory) => this.factory = factory;
    private HttpClient Client(string? token = "test-token") { var c = factory.CreateClient(); if (token is not null) c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token); return c; }

    [Fact]
    public async Task Root_is_anonymous_and_missing_or_unknown_tokens_are_challenged()
    {
        Assert.Equal(HttpStatusCode.OK, (await factory.CreateClient().GetAsync("/")).StatusCode);
        using var missing = await Client(null).PostAsJsonAsync("/api/v1/short-urls", new { url = "https://example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode); Assert.Contains(missing.Headers.WwwAuthenticate, x => x.Scheme == "Bearer");
        using var unknown = await Client("unknown").PostAsJsonAsync("/api/v1/short-urls", new { url = "https://example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Invalid_url_and_code_have_problem_details()
    { using var bad = await Client().PostAsJsonAsync("/api/v1/short-urls", new { url = "/relative" }); Assert.True(bad.StatusCode == HttpStatusCode.BadRequest, await bad.Content.ReadAsStringAsync()); Assert.Equal("application/problem+json", bad.Content.Headers.ContentType?.MediaType); using var code = await Client().PutAsJsonAsync("/api/v1/short-urls/nope", new { url = "https://example.com" }); Assert.Equal(HttpStatusCode.NotFound, code.StatusCode); }

    [Fact]
    public async Task Create_update_delete_and_concealment_contract()
    {
        var c = Client(); var first = await c.PostAsJsonAsync("/api/v1/short-urls", new { url = "https://example.com/a" }); Assert.Equal(HttpStatusCode.Created, first.StatusCode); var one = await first.Content.ReadFromJsonAsync<ShortUrlRepresentation>(); Assert.NotNull(one); Assert.Equal(one!.ShortUrl, first.Headers.Location!.ToString());
        var second = await c.PostAsJsonAsync("/api/v1/short-urls", new { url = "https://example.com/a" }); var two = await second.Content.ReadFromJsonAsync<ShortUrlRepresentation>(); Assert.NotEqual(one.ShortCode, two!.ShortCode);
        var update = await c.PutAsJsonAsync($"/api/v1/short-urls/{one.ShortCode}", new { url = "https://example.com/b" }); Assert.Equal(HttpStatusCode.OK, update.StatusCode); Assert.Equal(one.ShortCode, (await update.Content.ReadFromJsonAsync<ShortUrlRepresentation>())!.ShortCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client("other-token").PutAsJsonAsync($"/api/v1/short-urls/{one.ShortCode}", new { url = "https://example.com/c" })).StatusCode);
        var del = await c.DeleteAsync($"/api/v1/short-urls/{one.ShortCode}"); Assert.Equal(HttpStatusCode.NoContent, del.StatusCode); Assert.Empty(await del.Content.ReadAsByteArrayAsync()); Assert.Equal(HttpStatusCode.NotFound, (await c.DeleteAsync($"/api/v1/short-urls/{one.ShortCode}")).StatusCode); Assert.Equal(HttpStatusCode.NotFound, (await c.PutAsJsonAsync($"/api/v1/short-urls/{one.ShortCode}", new { url = "https://example.com/d" })).StatusCode);
    }

    [Fact]
    public async Task Five_conflicts_return_generic_503()
    { factory.Store.Conflicts = 5; using var response = await Client().PostAsJsonAsync("/api/v1/short-urls", new { url = "https://example.com/exhaust" }); Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode); Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType); Assert.DoesNotContain("test-token", await response.Content.ReadAsStringAsync()); }
}

public sealed class ManagementFactory : WebApplicationFactory<Program>
{
    public TestStore Store { get; } = new();
    protected override void ConfigureWebHost(IWebHostBuilder builder) => builder.UseEnvironment("Development").ConfigureAppConfiguration((_, c) => c.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:PostgreSql"] = "Host=invalid", ["PublicBaseUrl"] = "http://localhost:8080", ["ManagementAuth:Tokens:0:Token"] = "test-token", ["ManagementAuth:Tokens:0:OwnerId"] = "owner-a", ["ManagementAuth:Tokens:1:Token"] = "other-token", ["ManagementAuth:Tokens:1:OwnerId"] = "owner-b" })).ConfigureServices(services => { services.AddSingleton<IShortUrlStore>(Store); services.AddSingleton<IShortCodeGenerator, TestGenerator>(); services.AddSingleton<IShortUrlNonce, TestNonce>(); services.AddSingleton<IShortUrlClock, TestClock>(); services.AddSingleton<IPublicUrlBuilder, TestUrlBuilder>(); });
}
public sealed class TestStore : IShortUrlStore { public int Conflicts; private readonly List<ShortUrl> rows = []; public Task<ShortUrl> InsertAsync(ShortUrl e, CancellationToken _) { if (Conflicts-- > 0) throw new ShortCodeConflictException(); rows.Add(e); return Task.FromResult(e); } public Task<ShortUrl?> FindActiveAsync(string owner, string code, CancellationToken _) => Task.FromResult(rows.SingleOrDefault(x => x.OwnerId == owner && x.ShortCode == code && !x.IsDeleted)); public Task SaveAsync(CancellationToken _) => Task.CompletedTask; }
public sealed class TestGenerator : IShortCodeGenerator { private int n; public string Generate(string _, string __, string ___, int counter) => $"abc{n++:D3}"; }
public sealed class TestNonce : IShortUrlNonce { public string Create() => "nonce"; }
public sealed class TestClock : IShortUrlClock { public DateTimeOffset UtcNow => new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero); }
public sealed class TestUrlBuilder : IPublicUrlBuilder { public string Build(string code) => "http://localhost:8080/" + code; }

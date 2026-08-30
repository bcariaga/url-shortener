using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc.Testing;
using UrlShortener.Api.Tests;
using UrlShortener.Application.Handlers.Representations;
using Xunit;

namespace Api.Tests;

public sealed class ManagementApiTests : IClassFixture<ManagementFactory>
{
    private readonly ManagementFactory factory;

    public ManagementApiTests(ManagementFactory factory)
    {
        this.factory = factory;
    }

    [Fact]
    public async Task Root_is_anonymous_and_management_requires_a_known_token()
    {
        using var root = await factory.CreateClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.NotFound, root.StatusCode);

        using var missing = await CreateClient(null).PostAsJsonAsync(
            "/api/v1/short-urls",
            new { url = "https://example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        Assert.Contains(missing.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");

        using var unknown = await CreateClient("unknown").PostAsJsonAsync(
            "/api/v1/short-urls",
            new { url = "https://example.com" });
        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
    }

    [Fact]
    public async Task Invalid_url_is_bad_request_and_invalid_route_code_is_not_found()
    {
        var insertedBefore = factory.Repository.Inserted.Count;

        using var invalidUrl = await CreateClient().PostAsJsonAsync(
            "/api/v1/short-urls",
            new { url = "/relative" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUrl.StatusCode);
        Assert.Equal("application/problem+json", invalidUrl.Content.Headers.ContentType?.MediaType);
        Assert.Equal(insertedBefore, factory.Repository.Inserted.Count);

        using var invalidCode = await CreateClient().PutAsJsonAsync(
            "/api/v1/short-urls/nope",
            new { url = "https://example.com" });
        Assert.Equal(HttpStatusCode.NotFound, invalidCode.StatusCode);
        Assert.Equal("application/problem+json", invalidCode.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Create_update_and_delete_preserve_ownership_and_concealment()
    {
        using var client = CreateClient();
        using var firstResponse = await client.PostAsJsonAsync(
            "/api/v1/short-urls",
            new { url = "https://example.com/a" });
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);
        var first = await firstResponse.Content.ReadFromJsonAsync<ShortUrlRepresentation>();
        Assert.NotNull(first);
        Assert.Equal(first.ShortUrl, firstResponse.Headers.Location?.ToString());

        using var secondResponse = await client.PostAsJsonAsync(
            "/api/v1/short-urls",
            new { url = "https://example.com/a" });
        var second = await secondResponse.Content.ReadFromJsonAsync<ShortUrlRepresentation>();
        Assert.NotNull(second);
        Assert.NotEqual(first.ShortCode, second.ShortCode);

        using var updateResponse = await client.PutAsJsonAsync(
            $"/api/v1/short-urls/{first.ShortCode}",
            new { url = "https://example.com/b" });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<ShortUrlRepresentation>();
        Assert.NotNull(updated);
        Assert.Equal(first.ShortCode, updated.ShortCode);

        using var repeatedUpdate = await client.PutAsJsonAsync(
            $"/api/v1/short-urls/{first.ShortCode}",
            new { url = "https://example.com/b" });
        Assert.Equal(HttpStatusCode.OK, repeatedUpdate.StatusCode);

        using var foreignUpdate = await CreateClient("other-token").PutAsJsonAsync(
            $"/api/v1/short-urls/{first.ShortCode}",
            new { url = "https://example.com/c" });
        Assert.Equal(HttpStatusCode.NotFound, foreignUpdate.StatusCode);

        using var deleteResponse = await client.DeleteAsync(
            $"/api/v1/short-urls/{first.ShortCode}");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);
        Assert.Empty(await deleteResponse.Content.ReadAsByteArrayAsync());

        using var repeatedDelete = await client.DeleteAsync(
            $"/api/v1/short-urls/{first.ShortCode}");
        Assert.Equal(HttpStatusCode.NotFound, repeatedDelete.StatusCode);

        using var deletedUpdate = await client.PutAsJsonAsync(
            $"/api/v1/short-urls/{first.ShortCode}",
            new { url = "https://example.com/d" });
        Assert.Equal(HttpStatusCode.NotFound, deletedUpdate.StatusCode);
    }

    [Fact]
    public async Task Five_conflicts_return_generic_service_unavailable()
    {
        factory.Repository.Conflicts = 5;

        using var response = await CreateClient().PostAsJsonAsync(
            "/api/v1/short-urls",
            new { url = "https://example.com/exhaust" });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.DoesNotContain("test-token", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Public_redirect_is_anonymous_and_preserves_exact_destination()
    {
        var entity = UrlShortener.Domain.Entities.ShortUrl.Create(
            "aZ91Kb", "https://example.com/somewhere?x=1", "owner-a", DateTimeOffset.UtcNow);
        await factory.Repository.InsertAsync(entity, CancellationToken.None);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        using var response = await client.GetAsync("/aZ91Kb");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("https://example.com/somewhere?x=1", response.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Public_redirect_conceals_invalid_unknown_and_deleted_codes()
    {
        var deleted = UrlShortener.Domain.Entities.ShortUrl.Create(
            "del123", "https://example.com/deleted", "owner-a", DateTimeOffset.UtcNow);
        deleted.Delete(DateTimeOffset.UtcNow);
        await factory.Repository.InsertAsync(deleted, CancellationToken.None);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var responses = new[] { await client.GetAsync("/nope"), await client.GetAsync("/zz9999"), await client.GetAsync("/del123") };

        var normalized = new List<string>();
        foreach (var response in responses)
        {
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
            var problem = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsObject();
            problem.Remove("traceId");
            normalized.Add(problem.ToJsonString());
        }

        Assert.All(normalized, value => Assert.Equal(normalized[0], value));
    }

    private HttpClient CreateClient(string? token = "test-token")
    {
        var client = factory.CreateClient();
        if (token is not null)
        {
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", token);
        }

        return client;
    }
}

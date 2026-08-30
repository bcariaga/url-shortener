using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Api.Tests;

public sealed class ExceptionIntegrationTests
{
    [Fact]
    public async Task Unexpected_create_failure_returns_safe_problem_and_one_error_log()
    {
        await using var factory = new ExceptionFactory();
        factory.Repository.InsertException = new InvalidOperationException("unique-secret-message");
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "test-token");

        using var response = await client.PostAsJsonAsync("/api/v1/short-urls", new { url = "https://secret.example/owner" });
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);
        var errors = factory.Logs.Where(log => log.EventId.Id == 1002).ToArray();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(500, json.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("An unexpected error occurred.", json.RootElement.GetProperty("title").GetString());
        Assert.False(string.IsNullOrWhiteSpace(json.RootElement.GetProperty("traceId").GetString()));
        Assert.DoesNotContain("unique-secret-message", body);
        Assert.DoesNotContain("secret.example", body);
        Assert.DoesNotContain("test-token", body);
        Assert.DoesNotContain("owner-a", body);
        Assert.Single(errors);
        Assert.DoesNotContain(factory.Logs, log => log.EventId.Id == 1001);
    }
}

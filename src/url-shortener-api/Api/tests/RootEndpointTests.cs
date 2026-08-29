using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using Xunit;

namespace UrlShortener.Api.Tests;

public class RootEndpointTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact] public async Task Root_returns_plain_hello_world()
    {
        using var response = await factory.CreateClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/plain", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("Hello World!", await response.Content.ReadAsStringAsync());
    }
}

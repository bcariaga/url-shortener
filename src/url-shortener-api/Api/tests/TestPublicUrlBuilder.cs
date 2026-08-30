using UrlShortener.Domain.Services;

namespace UrlShortener.Api.Tests;

public sealed class TestPublicUrlBuilder : IPublicUrlBuilder
{
    public string Build(string code) => $"http://localhost:8080/{code}";
}

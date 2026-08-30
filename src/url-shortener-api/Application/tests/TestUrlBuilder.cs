using UrlShortener.Domain.Services;

namespace UrlShortener.Application.Tests;

public sealed class TestUrlBuilder : IPublicUrlBuilder
{
    public string Build(string code) => $"http://localhost/{code}";
}

using UrlShortener.Domain.Services;

namespace UrlShortener.Api.Tests;

public sealed class TestShortCodeGenerator : IShortCodeGenerator
{
    private int sequence;

    public string Generate(
        string ownerId,
        string url,
        string nonce,
        int counter,
        int length = 6) => $"abc{sequence++:D3}";
}

using UrlShortener.Domain.Services;

namespace UrlShortener.Application.Tests;

public sealed class TestCodeGenerator : IShortCodeGenerator
{
    public List<int> Counters { get; } = [];

    public List<string> Nonces { get; } = [];

    public string Generate(
        string ownerId,
        string url,
        string nonce,
        int counter,
        int length = 6)
    {
        Counters.Add(counter);
        Nonces.Add(nonce);
        return $"code0{counter}";
    }
}

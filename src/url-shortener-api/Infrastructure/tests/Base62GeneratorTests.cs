using UrlShortener.Infrastructure.Services;
using Xunit;

namespace UrlShortener.Infrastructure.Tests;

public sealed class Base62GeneratorTests
{
    [Fact]
    public void Generates_deterministic_six_character_base62_values()
    {
        var generator = new Sha256Base62Generator();
        var first = generator.Generate("owner", "https://example.com", "nonce", 0);

        Assert.Equal(6, first.Length);
        Assert.All(
            first,
            character => Assert.Contains(
                character,
                "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ"));
        Assert.Equal(first, generator.Generate("owner", "https://example.com", "nonce", 0));
        Assert.NotEqual(first, generator.Generate("owner", "https://example.com", "nonce", 1));
    }
}

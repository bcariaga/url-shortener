using UrlShortener.Application.Handlers;
using UrlShortener.Application.Handlers.Queries;
using UrlShortener.Application.Handlers.Validators;
using UrlShortener.Domain.Entities;
using Xunit;

namespace UrlShortener.Application.Tests;

public sealed class ResolveShortUrlQueryTests
{
    [Theory]
    [InlineData("aZ91Kb", true)]
    [InlineData("short", false)]
    [InlineData("aZ91K!", false)]
    public async Task Validator_accepts_only_six_base62_characters(string code, bool valid)
    {
        var result = await new ResolveShortUrlQueryValidator().ValidateAsync(
            new ResolveShortUrlQuery { ShortCode = code });

        Assert.Equal(valid, result.IsValid);
    }

    [Fact]
    public async Task Handler_returns_destination_without_saving()
    {
        var repository = new TestShortUrlRepository
        {
            Existing = ShortUrl.Create("aZ91Kb", "https://example.com/destination", "owner", DateTimeOffset.UtcNow)
        };

        var result = await new ResolveShortUrlQueryHandler(repository).HandleAsync(
            new ResolveShortUrlQuery { ShortCode = "aZ91Kb" });

        Assert.Equal("https://example.com/destination", result);
        Assert.Equal("aZ91Kb", repository.LastPublicCode);
        Assert.Equal(0, repository.SaveCount);
    }

    [Fact]
    public async Task Handler_returns_null_when_code_is_missing()
    {
        var repository = new TestShortUrlRepository { Existing = null };

        var result = await new ResolveShortUrlQueryHandler(repository).HandleAsync(
            new ResolveShortUrlQuery { ShortCode = "aZ91Kb" });

        Assert.Null(result);
    }
}

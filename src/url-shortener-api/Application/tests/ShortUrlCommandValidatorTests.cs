using UrlShortener.Application.Handlers.Commands;
using UrlShortener.Application.Handlers.Validators;
using Xunit;

namespace Application.Tests;

public sealed class ShortUrlCommandValidatorTests
{
    [Theory]
    [InlineData("")]
    [InlineData("relative/path")]
    [InlineData("ftp://example.com")]
    [InlineData("https:///missing-host")]
    public async Task Create_rejects_invalid_destination_urls(string url)
    {
        var result = await new CreateShortUrlCommandValidator().ValidateAsync(new CreateShortUrlCommand { OwnerId = "owner", Url = url });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(CreateShortUrlCommand.Url));
    }

    [Theory]
    [InlineData("http://example.com")]
    [InlineData("https://example.com/path?query=value#fragment")]
    public async Task Create_accepts_absolute_http_destinations(string url)
    {
        var result = await new CreateShortUrlCommandValidator().ValidateAsync(new CreateShortUrlCommand { OwnerId = "owner", Url = url });
        Assert.True(result.IsValid);
    }

    [Fact]
    public async Task Create_rejects_destinations_longer_than_2048_characters()
    {
        const string prefix = "https://example.com/";
        var result = await new CreateShortUrlCommandValidator().ValidateAsync(new CreateShortUrlCommand { OwnerId = "owner", Url = prefix + new string('a', 2049 - prefix.Length) });
        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task Create_accepts_a_2048_character_destination()
    {
        const string prefix = "https://example.com/";
        var result = await new CreateShortUrlCommandValidator().ValidateAsync(new CreateShortUrlCommand { OwnerId = "owner", Url = prefix + new string('a', 2048 - prefix.Length) });
        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("short")]
    [InlineData("abc12-")]
    [InlineData("abcdefg")]
    public async Task Update_rejects_non_base62_six_character_codes(string code)
    {
        var result = await new UpdateShortUrlCommandValidator().ValidateAsync(new UpdateShortUrlCommand { OwnerId = "owner", ShortCode = code, Url = "https://example.com" });
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateShortUrlCommand.ShortCode));
    }

    [Fact]
    public async Task Delete_accepts_a_base62_six_character_code()
    {
        var result = await new DeleteShortUrlCommandValidator().ValidateAsync(new DeleteShortUrlCommand { OwnerId = "owner", ShortCode = "aZ019x" });
        Assert.True(result.IsValid);
    }
}

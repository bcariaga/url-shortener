using UrlShortener.Domain.Services;

namespace UrlShortener.Api.Tests;

public sealed class TestShortUrlClock : IShortUrlClock
{
    public DateTimeOffset UtcNow =>
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

using UrlShortener.Domain.Services;

namespace UrlShortener.Application.Tests;

public sealed class TestClock : IShortUrlClock
{
    public DateTimeOffset UtcNow =>
        new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
}

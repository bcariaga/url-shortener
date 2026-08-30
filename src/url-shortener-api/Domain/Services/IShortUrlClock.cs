namespace UrlShortener.Domain.Services;

public interface IShortUrlClock
{
    DateTimeOffset UtcNow { get; }
}


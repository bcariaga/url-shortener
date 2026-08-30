using UrlShortener.Domain.Services;

namespace UrlShortener.Infrastructure.Services;

public sealed class SystemShortUrlClock : IShortUrlClock { public DateTimeOffset UtcNow => DateTimeOffset.UtcNow; }

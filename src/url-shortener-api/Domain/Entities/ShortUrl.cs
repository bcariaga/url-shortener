using UrlShortener.Domain.Telemetry;
using UrlShortener.Domain.Exceptions;

namespace UrlShortener.Domain.Entities;

public sealed class ShortUrl
{
    private ShortUrl() { }
    public long Id { get; private set; }
    public string ShortCode { get; private set; } = "";
    public string LongUrl { get; private set; } = "";
    public string OwnerId { get; private set; } = "";
    public bool IsDeleted { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static ShortUrl Create(string code, string url, string owner, DateTimeOffset now)
    {
        using var activity = ActivitySources.ShortUrl.StartActivity(nameof(Create));
        if (string.IsNullOrWhiteSpace(code)) throw new RequiredShortUrlValueException("shortCode");
        if (string.IsNullOrWhiteSpace(url)) throw new RequiredShortUrlValueException("url");
        if (string.IsNullOrWhiteSpace(owner)) throw new RequiredShortUrlValueException("owner");

        return new ShortUrl
        {
            ShortCode = code,
            LongUrl = url,
            OwnerId = owner,
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Update(string url, DateTimeOffset now)
    {
        using var activity = ActivitySources.ShortUrl.StartActivity(nameof(Update));
        if (IsDeleted) throw new InvalidShortUrlStateException("update");
        if (string.IsNullOrWhiteSpace(url)) throw new RequiredShortUrlValueException("url");
        if (url != LongUrl)
        {
            LongUrl = url;
            UpdatedAt = now;
        }
    }

    public void Delete(DateTimeOffset now)
    {
        using var activity = ActivitySources.ShortUrl.StartActivity(nameof(Delete));
        if (IsDeleted) throw new InvalidShortUrlStateException("delete");
        IsDeleted = true;
        UpdatedAt = now;
    }
}

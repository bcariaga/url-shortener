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
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Short code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL is required.", nameof(url));
        if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("Owner is required.", nameof(owner));

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
        if (IsDeleted) throw new InvalidOperationException("A deleted short URL cannot be updated.");
        if (string.IsNullOrWhiteSpace(url)) throw new ArgumentException("URL is required.", nameof(url));
        if (url != LongUrl)
        {
            LongUrl = url; UpdatedAt = now;
        }
    }

    public void Delete(DateTimeOffset now)
    {
        if (IsDeleted) throw new InvalidOperationException("A deleted short URL cannot be deleted again.");
        IsDeleted = true;
        UpdatedAt = now;
    }
}

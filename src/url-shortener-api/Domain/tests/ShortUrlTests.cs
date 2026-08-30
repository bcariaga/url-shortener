using UrlShortener.Domain.Entities;
using Xunit;

namespace UrlShortener.Domain.Tests;

public sealed class ShortUrlTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_identity_lifecycle_and_timestamps()
    {
        var item = ShortUrl.Create("abc123", "https://example.com", "owner", Now);

        Assert.Equal(0, item.Id);
        Assert.False(item.IsDeleted);
        Assert.Equal(Now, item.CreatedAt);
        Assert.Equal(Now, item.UpdatedAt);
    }

    [Theory]
    [InlineData("", "https://example.com", "owner")]
    [InlineData("abc123", "", "owner")]
    [InlineData("abc123", "https://example.com", "")]
    public void Create_rejects_missing_values(string code, string url, string owner) =>
        Assert.Throws<ArgumentException>(() => ShortUrl.Create(code, url, owner, Now));

    [Fact]
    public void Update_is_idempotent_or_changes_destination()
    {
        var item = ShortUrl.Create("abc123", "https://old", "owner", Now);

        item.Update("https://old", Now.AddDays(1));
        Assert.Equal(Now, item.UpdatedAt);

        item.Update("https://new", Now.AddDays(2));
        Assert.Equal("https://new", item.LongUrl);
        Assert.Equal(Now.AddDays(2), item.UpdatedAt);
    }

    [Fact]
    public void Delete_is_logical_and_cannot_repeat_or_update()
    {
        var item = ShortUrl.Create("abc123", "https://old", "owner", Now);

        item.Delete(Now.AddDays(1));

        Assert.True(item.IsDeleted);
        Assert.Equal(Now.AddDays(1), item.UpdatedAt);
        Assert.Throws<InvalidOperationException>(() => item.Delete(Now.AddDays(2)));
        Assert.Throws<InvalidOperationException>(() => item.Update("https://new", Now.AddDays(2)));
    }
}

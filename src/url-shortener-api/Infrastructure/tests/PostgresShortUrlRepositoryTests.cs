using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.Entities;
using UrlShortener.Infrastructure.Repositories;
using Xunit;

namespace UrlShortener.Infrastructure.Tests;

public sealed class PostgresShortUrlRepositoryTests
{
    [Fact]
    public async Task FindActiveByCode_filters_exact_code_and_deleted_rows()
    {
        var connection = Environment.GetEnvironmentVariable("URL_SHORTENER_TEST_CONNECTION");
        if (string.IsNullOrWhiteSpace(connection))
        {
            return;
        }

        var code = $"{Guid.NewGuid():N}"[..6];
        var deletedCode = $"{Guid.NewGuid():N}"[..6];
        var options = new DbContextOptionsBuilder<UrlShortenerDbContext>().UseNpgsql(connection).Options;
        await using var context = new UrlShortenerDbContext(options);
        await context.Database.MigrateAsync();
        var active = ShortUrl.Create(code, "https://example.com/active", "test", DateTimeOffset.UtcNow);
        var deleted = ShortUrl.Create(deletedCode, "https://example.com/deleted", "test", DateTimeOffset.UtcNow);
        deleted.Delete(DateTimeOffset.UtcNow);
        context.ShortUrls.AddRange(active, deleted);
        await context.SaveChangesAsync();

        try
        {
            var repository = new EFShortUrlRepository(context);
            var result = await repository.FindActiveByCodeAsync(code, CancellationToken.None);
            var deletedResult = await repository.FindActiveByCodeAsync(deletedCode, CancellationToken.None);

            Assert.Equal("https://example.com/active", result?.LongUrl);
            Assert.Null(deletedResult);
            Assert.Equal(EntityState.Detached, context.Entry(result!).State);
        }
        finally
        {
            await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM short_urls WHERE short_code IN ({code}, {deletedCode})");
        }
    }
}

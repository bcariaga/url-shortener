using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;

namespace Api.Tests;

public sealed class TestShortUrlRepository : IShortUrlRepository
{
    private readonly List<ShortUrl> rows = [];

    public int Conflicts { get; set; }
    public Exception? InsertException { get; set; }

    public IReadOnlyList<ShortUrl> Inserted => rows;

    public Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken)
    {
        if (InsertException is not null)
        {
            throw InsertException;
        }
        if (Conflicts > 0)
        {
            Conflicts--;
            throw new ShortCodeConflictException();
        }

        rows.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<ShortUrl?> FindActiveAsync(
        string ownerId,
        string code,
        CancellationToken cancellationToken) =>
        Task.FromResult(rows.SingleOrDefault(entity =>
            entity.OwnerId == ownerId
            && entity.ShortCode == code
            && !entity.IsDeleted));

    public Task<string?> FindActiveDestinationByCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult(rows.SingleOrDefault(entity => entity.ShortCode == code && !entity.IsDeleted)?.LongUrl);

    public Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken) => Task.CompletedTask;
}

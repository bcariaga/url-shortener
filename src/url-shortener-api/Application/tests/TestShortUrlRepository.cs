using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;

namespace UrlShortener.Application.Tests;

public sealed class TestShortUrlRepository : IShortUrlRepository
{
    public List<ShortUrl> Inserted { get; } = [];

    public int Conflicts { get; set; }

    public Exception? Error { get; set; }

    public ShortUrl? Existing { get; set; }

    public string? LastOwner { get; private set; }

    public int SaveCount { get; private set; }

    public Task<ShortUrl> InsertAsync(
        ShortUrl entity,
        CancellationToken cancellationToken)
    {
        if (Error is not null)
        {
            throw Error;
        }

        if (Conflicts > 0)
        {
            Conflicts--;
            throw new ShortCodeConflictException();
        }

        Inserted.Add(entity);
        return Task.FromResult(entity);
    }

    public Task<ShortUrl?> FindActiveAsync(
        string ownerId,
        string code,
        CancellationToken cancellationToken)
    {
        LastOwner = ownerId;
        return Task.FromResult(Existing);
    }

    public Task SaveAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

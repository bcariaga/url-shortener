using UrlShortener.Domain.Entities;

namespace UrlShortener.Domain.Repositories;

public interface IShortUrlRepository
{
    Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken);
    Task<ShortUrl?> FindActiveAsync(string ownerId, string code, CancellationToken cancellationToken);
    Task<ShortUrl?> FindActiveByCodeAsync(string code, CancellationToken cancellationToken);
    Task SaveAsync(CancellationToken cancellationToken);
}

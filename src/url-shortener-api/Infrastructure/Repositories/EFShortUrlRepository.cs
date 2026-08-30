using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;

namespace UrlShortener.Infrastructure.Repositories;

public sealed class EFShortUrlRepository(UrlShortenerDbContext dbContext) : IShortUrlRepository
{
    public async Task<ShortUrl> InsertAsync(
        ShortUrl entity,
        CancellationToken cancellationToken)
    {
        dbContext.ShortUrls.Add(entity);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return entity;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException
            {
                SqlState: "23505",
                ConstraintName: "ux_short_urls_short_code"
            })
        {
            dbContext.Entry(entity).State = EntityState.Detached;
            throw new ShortCodeConflictException();
        }
    }

    public Task<ShortUrl?> FindActiveAsync(
        string ownerId,
        string code,
        CancellationToken cancellationToken) =>
        dbContext.ShortUrls.SingleOrDefaultAsync(
            entity => entity.OwnerId == ownerId
                && entity.ShortCode == code
                && !entity.IsDeleted,
            cancellationToken);

    public Task SaveAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}

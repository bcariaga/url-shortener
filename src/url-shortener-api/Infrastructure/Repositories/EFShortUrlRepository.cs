using Microsoft.EntityFrameworkCore;
using UrlShortener.Application;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;

namespace UrlShortener.Infrastructure.Repositories;

public sealed class EFShortUrlRepository(UrlShortenerDbContext db) : IShortUrlRepository
{
    public async Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken ct)
    {
        db.ShortUrls.Add(entity);
        try
        {
            await db.SaveChangesAsync(ct);
            return entity;
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is Npgsql.PostgresException
            {
                SqlState: "23505",
                ConstraintName: "ux_short_urls_short_code"
            })
        {
            db.Entry(entity).State = EntityState.Detached;
            throw new ShortCodeConflictException();
        }
    }
    public Task<ShortUrl?> FindActiveAsync(string ownerId, string code, CancellationToken ct) =>
        db.ShortUrls.SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.ShortCode == code && !x.IsDeleted, ct);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
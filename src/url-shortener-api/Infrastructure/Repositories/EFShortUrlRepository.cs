using Microsoft.EntityFrameworkCore;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Infrastructure.Telemetry;

namespace UrlShortener.Infrastructure.Repositories;

public sealed class EFShortUrlRepository(UrlShortenerDbContext dbContext) : IShortUrlRepository
{
    public async Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.EfShortUrlRepository.StartActivity(nameof(InsertAsync));
        try
        {
            dbContext.ShortUrls.Add(entity);

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

    public async Task<ShortUrl?> FindActiveAsync(
        string ownerId,
        string code,
        CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.EfShortUrlRepository.StartActivity(nameof(FindActiveAsync));
        return await dbContext.ShortUrls.SingleOrDefaultAsync(
            entity => entity.OwnerId == ownerId
                && entity.ShortCode == code
                && !entity.IsDeleted,
            cancellationToken);
    }

    public async Task<string?> FindActiveDestinationByCodeAsync(string code, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.EfShortUrlRepository.StartActivity(nameof(FindActiveDestinationByCodeAsync));
        return await dbContext.ShortUrls.AsNoTracking()
            .Where(entity => entity.ShortCode == code && !entity.IsDeleted)
            .Select(entity => entity.LongUrl)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken)
    {
        using var activity = ActivitySources.EfShortUrlRepository.StartActivity(nameof(SaveAsync));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}

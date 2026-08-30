using UrlShortener.Application.Handlers.Commands;
using UrlShortener.Application.Handlers.Representations;
using Mediary.Core;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;

namespace UrlShortener.Application.Handlers;

public sealed class UpdateShortUrlCommandHandler(
    IShortUrlRepository repository,
    IShortUrlClock clock,
    IPublicUrlBuilder urls) : IRequestHandler<ShortUrlRepresentation?, UpdateShortUrlCommand>
{
    public async Task<ShortUrlRepresentation?> HandleAsync(UpdateShortUrlCommand command)
    {
        var entity = await repository.FindActiveAsync(
            command.OwnerId,
            command.ShortCode,
            CancellationToken.None);
        if (entity is null)
        {
            return null;
        }

        entity.Update(command.Url, clock.UtcNow);
        await repository.SaveAsync(entity, CancellationToken.None);

        return new(entity.ShortCode, urls.Build(entity.ShortCode), entity.LongUrl);
    }
}

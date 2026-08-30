using Mediary.Core;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;

namespace UrlShortener.Application.Handlers;

public sealed class CreateShortUrlCommandHandler(
    IShortUrlRepository repository,
    IShortCodeGenerator generator,
    IShortUrlClock clock,
    IPublicUrlBuilder urls) : IRequestHandler<ShortUrlRepresentation, CreateShortUrlCommand>
{
    private readonly int MAX_ATTEMPTS = 5;
    public async Task<ShortUrlRepresentation> HandleAsync(CreateShortUrlCommand command)
    {
        var nonce = Guid.NewGuid().ToString("N");
        for (var counter = 0; counter < MAX_ATTEMPTS; counter++)
        {
            var shortUrlCode = generator.Generate(command.OwnerId, command.Url, nonce, counter);
            var entity = ShortUrl.Create(shortUrlCode, command.Url, command.OwnerId, clock.UtcNow);
            try
            {
                await repository.InsertAsync(entity, CancellationToken.None);
                return new(entity.ShortCode, urls.Build(entity.ShortCode), entity.LongUrl);
            }
            catch (ShortCodeConflictException)
            {
                if (counter == MAX_ATTEMPTS - 1)
                {
                    throw new ShortCodeAttemptsExhaustedException();
                }
            }
        }
        throw new ShortCodeAttemptsExhaustedException();
    }
}
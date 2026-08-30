using UrlShortener.Application.Exceptions;
using UrlShortener.Application.Handlers.Commands;
using UrlShortener.Application.Handlers.Representations;
using Mediary.Core;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;
using UrlShortener.Application.Telemetry;

namespace UrlShortener.Application.Handlers;

public sealed class CreateShortUrlCommandHandler(
    IShortUrlRepository repository,
    IShortCodeGenerator generator,
    IShortUrlClock clock,
    IPublicUrlBuilder urls) : IRequestHandler<ShortUrlRepresentation, CreateShortUrlCommand>
{
    private const int MaxAttempts = 5;

    public async Task<ShortUrlRepresentation> HandleAsync(CreateShortUrlCommand command)
    {
        using var activity = ActivitySources.CreateShortUrl.StartActivity(nameof(HandleAsync));
        var nonce = Guid.NewGuid().ToString("N");
        for (var counter = 0; counter < MaxAttempts; counter++)
        {
            var shortUrlCode = generator.Generate(
                command.OwnerId,
                command.Url,
                nonce,
                counter);
            var entity = ShortUrl.Create(
                shortUrlCode,
                command.Url,
                command.OwnerId,
                clock.UtcNow);

            try
            {
                await repository.InsertAsync(entity, CancellationToken.None);
                return new(entity.ShortCode, urls.Build(entity.ShortCode), entity.LongUrl);
            }
            catch (ShortCodeConflictException) when (counter == MaxAttempts - 1)
            {
                throw new ShortCodeAttemptsExhaustedException();
            }
            catch (ShortCodeConflictException)
            {
            }
        }

        throw new ShortCodeAttemptsExhaustedException();
    }
}

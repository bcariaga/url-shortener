using UrlShortener.Application.Handlers.Commands;
using Mediary.Core;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;
using UrlShortener.Application.Telemetry;

namespace UrlShortener.Application.Handlers;

public sealed class DeleteShortUrlCommandHandler(
    IShortUrlRepository repository,
    IShortUrlClock clock) : IRequestHandler<bool, DeleteShortUrlCommand>
{
    public async Task<bool> HandleAsync(DeleteShortUrlCommand command)
    {
        using var activity = ActivitySources.DeleteShortUrl.StartActivity(nameof(HandleAsync));
        var entity = await repository.FindActiveAsync(
            command.OwnerId,
            command.ShortCode,
            CancellationToken.None);
        if (entity is null)
        {
            return false;
        }

        entity.Delete(clock.UtcNow);
        await repository.SaveAsync(entity, CancellationToken.None);

        return true;
    }
}

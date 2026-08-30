using Mediary.Core;
using UrlShortener.Application.Handlers.Queries;
using UrlShortener.Domain.Repositories;
namespace UrlShortener.Application.Handlers;
public sealed class ResolveShortUrlQueryHandler(IShortUrlRepository repository) : IRequestHandler<string?, ResolveShortUrlQuery>
{
    public async Task<string?> HandleAsync(ResolveShortUrlQuery query) =>
        await repository.FindActiveDestinationByCodeAsync(query.ShortCode, CancellationToken.None);
}

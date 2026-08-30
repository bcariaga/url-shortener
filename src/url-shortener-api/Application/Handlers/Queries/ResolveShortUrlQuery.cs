using Mediary.Core;
namespace UrlShortener.Application.Handlers.Queries;
public sealed class ResolveShortUrlQuery : IQuery<string?>
{
    public required string ShortCode { get; init; }
}

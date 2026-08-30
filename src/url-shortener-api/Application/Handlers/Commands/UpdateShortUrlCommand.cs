using Mediary.Core;
using UrlShortener.Application.Handlers.Representations;

namespace UrlShortener.Application.Handlers.Commands;

public sealed class UpdateShortUrlCommand : ICommand<ShortUrlRepresentation?>
{
    public required string OwnerId { get; init; }
    public required string ShortCode { get; init; }
    public required string Url { get; init; }
}

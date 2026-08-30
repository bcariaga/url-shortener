using Mediary.Core;

namespace UrlShortener.Application.Handlers.Commands;

public sealed class CreateShortUrlCommand : ICommand<ShortUrlRepresentation>
{
    public required string OwnerId { get; init; }
    public required string Url { get; init; }
}
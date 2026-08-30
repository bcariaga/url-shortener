using Mediary.Core;

namespace UrlShortener.Application.Handlers.Commands;

public sealed class DeleteShortUrlCommand : ICommand<bool>
{
    public required string OwnerId { get; init; }
    public required string ShortCode { get; init; }
}

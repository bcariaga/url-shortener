namespace UrlShortener.Domain.Exceptions;

public sealed class InvalidShortUrlStateException(string operation) : DomainException($"A deleted short URL cannot be {operation}d.", "short_url_invalid_state")
{
    public string Operation { get; } = operation;
}

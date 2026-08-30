namespace UrlShortener.Application.Handlers.Representations;

public sealed record ShortUrlRepresentation(string ShortCode, string ShortUrl, string Url);

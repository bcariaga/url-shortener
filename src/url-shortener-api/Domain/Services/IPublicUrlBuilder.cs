namespace UrlShortener.Domain.Services;

public interface IPublicUrlBuilder
{
    string Build(string shortCode);
}

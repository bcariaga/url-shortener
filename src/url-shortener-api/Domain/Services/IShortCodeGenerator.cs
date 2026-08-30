namespace UrlShortener.Domain.Services;

public interface IShortCodeGenerator
{
    string Generate(string ownerId, string url, string nonce, int counter, int length = 6);
}

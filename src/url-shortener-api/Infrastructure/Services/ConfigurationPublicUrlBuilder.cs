using Microsoft.Extensions.Configuration;
using UrlShortener.Domain.Services;
namespace UrlShortener.Infrastructure.Services;

public sealed class ConfigurationPublicUrlBuilder(IConfiguration configuration) : IPublicUrlBuilder
{
    public string Build(string code) => $"{configuration["PublicBaseUrl"]?.TrimEnd('/')}/{code}";
}

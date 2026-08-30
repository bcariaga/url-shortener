using Microsoft.Extensions.Configuration;
using UrlShortener.Domain.Services;
using UrlShortener.Infrastructure.Telemetry;
namespace UrlShortener.Infrastructure.Services;

public sealed class ConfigurationPublicUrlBuilder(IConfiguration configuration) : IPublicUrlBuilder
{
    public string Build(string code)
    {
        using var activity = ActivitySources.PublicUrlBuilder.StartActivity(nameof(Build));
        return $"{configuration["PublicBaseUrl"]?.TrimEnd('/')}/{code}";
    }
}

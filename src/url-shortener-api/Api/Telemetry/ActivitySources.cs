using System.Diagnostics;
using OpenTelemetry.Trace;
using UrlShortener.Api.Controllers;
namespace UrlShortener.Api.Telemetry;

public static class ActivitySources
{
    public static readonly ActivitySource PublicRedirect = new(nameof(PublicRedirectController));
    public static readonly ActivitySource ShortUrls = new(nameof(ShortUrlsController));

    public static TracerProviderBuilder AddApiActivitySources(this TracerProviderBuilder builder) =>
        builder
            .AddSource(PublicRedirect.Name)
            .AddSource(ShortUrls.Name);
}

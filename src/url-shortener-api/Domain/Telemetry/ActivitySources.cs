using System.Diagnostics;
using OpenTelemetry.Trace;
using UrlShortener.Domain.Entities;

namespace UrlShortener.Domain.Telemetry;

public static class ActivitySources
{
    public static readonly ActivitySource ShortUrl = new(nameof(Entities.ShortUrl));

    public static TracerProviderBuilder AddDomainActivitySources(this TracerProviderBuilder builder) =>
        builder.AddSource(ShortUrl.Name);
}

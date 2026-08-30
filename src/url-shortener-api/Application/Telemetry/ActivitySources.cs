using System.Diagnostics;
using OpenTelemetry.Trace;
using UrlShortener.Application.Handlers;

namespace UrlShortener.Application.Telemetry;

public static class ActivitySources
{
    public static readonly ActivitySource CreateShortUrl = new(nameof(CreateShortUrlCommandHandler));
    public static readonly ActivitySource UpdateShortUrl = new(nameof(UpdateShortUrlCommandHandler));
    public static readonly ActivitySource DeleteShortUrl = new(nameof(DeleteShortUrlCommandHandler));
    public static readonly ActivitySource ResolveShortUrl = new(nameof(ResolveShortUrlQueryHandler));

    public static TracerProviderBuilder AddApplicationActivitySources(this TracerProviderBuilder builder) =>
        builder
            .AddSource(CreateShortUrl.Name)
            .AddSource(UpdateShortUrl.Name)
            .AddSource(DeleteShortUrl.Name)
            .AddSource(ResolveShortUrl.Name);
}

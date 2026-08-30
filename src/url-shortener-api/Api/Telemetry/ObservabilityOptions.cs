namespace UrlShortener.Api.Telemetry;

public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool TracingEnabled { get; init; } = true;
}

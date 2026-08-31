namespace UrlShortener.Infrastructure.Resilience;

public sealed class DatabaseResilienceOptions
{
    public const string SectionName = "DatabaseResilience";

    public int ConnectionTimeoutSeconds { get; set; } = 3;
    public int CommandTimeoutSeconds { get; set; } = 5;
    public int ReadMaxRetryAttempts { get; set; } = 2;
    public int ReadRetryDelayMilliseconds { get; set; } = 100;
    public double FailureRatio { get; set; } = 0.5;
    public int SamplingDurationSeconds { get; set; } = 10;
    public int MinimumThroughput { get; set; } = 5;
    public int BreakDurationSeconds { get; set; } = 15;
}

namespace UrlShortener.Infrastructure.Resilience;

public static class PostgresResiliencePipeline
{
    public const string Name = "postgresql";
    public const string ReadRetryName = "postgresql-read-retry";
}

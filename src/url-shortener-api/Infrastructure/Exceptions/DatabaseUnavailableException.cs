namespace UrlShortener.Infrastructure.Exceptions;

public sealed class DatabaseUnavailableException(TimeSpan? retryAfter, Exception innerException)
    : Exception("The database is temporarily unavailable.", innerException)
{
    public const string ErrorCode = "database_unavailable";

    public TimeSpan? RetryAfter { get; } = retryAfter;
}

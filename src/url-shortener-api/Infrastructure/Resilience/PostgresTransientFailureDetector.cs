using Npgsql;

namespace UrlShortener.Infrastructure.Resilience;

public static class PostgresTransientFailureDetector
{
    public static bool IsTransient(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return false;
        }

        if (exception is TimeoutException or NpgsqlException { IsTransient: true })
        {
            return true;
        }

        return exception.InnerException is not null
            && IsTransient(exception.InnerException);
    }
}

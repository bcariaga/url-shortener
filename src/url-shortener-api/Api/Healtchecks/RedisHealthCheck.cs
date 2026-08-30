using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace UrlShortener.Api.Healtchecks;

public sealed class RedisHealthCheck(IRedisHealthProbe probe) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await probe.PingAsync(cancellationToken);
            return HealthCheckResult.Healthy();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            return new HealthCheckResult(HealthStatus.Degraded, exception: exception);
        }
    }
}

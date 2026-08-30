using StackExchange.Redis;

namespace UrlShortener.Api.Healtchecks;

public sealed class StackExchangeRedisHealthProbe(IConnectionMultiplexer multiplexer) : IRedisHealthProbe
{
    public Task PingAsync(CancellationToken cancellationToken)
    {
        return multiplexer.GetDatabase().PingAsync().WaitAsync(cancellationToken);
    }
}

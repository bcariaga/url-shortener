namespace UrlShortener.Api.Healtchecks;

public interface IRedisHealthProbe
{
    Task PingAsync(CancellationToken cancellationToken);
}

using System.Diagnostics.CodeAnalysis;

namespace UrlShortener.Infrastructure.Tests;

[ExcludeFromCodeCoverage]
internal sealed class ConnectionStringsSection : EmptySection
{
    public string? RedisEndpoint { get; }

    public ConnectionStringsSection(string? redisEndpoint = null) : base("ConnectionStrings")
    {
        RedisEndpoint = redisEndpoint;
    }

    public override string? this[string key]
    {
        get => key == "PostgreSql"
            ? "Host=localhost;Database=test;Username=test;Password=test"
            : key == "Redis" ? RedisEndpoint : null;
        set { }
    }
}

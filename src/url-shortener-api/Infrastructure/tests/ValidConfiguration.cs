using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace UrlShortener.Infrastructure.Tests;

[ExcludeFromCodeCoverage]
internal sealed class ValidConfiguration : EmptyConfiguration
{
    public string? CacheTimeout { get; set; }
    public string? CacheTtl { get; set; }
    public string? RedisEndpoint { get; set; }
    public override string? this[string key]
    {
        get => key switch
        {
            "ConnectionStrings:PostgreSql" => "Host=localhost;Database=test;Username=test;Password=test",
            "Cache:TimeoutMilliseconds" => CacheTimeout,
            "Cache:TtlSeconds" => CacheTtl,
            _ => null
        };
        set { }
    }

    public override IConfigurationSection GetSection(string key) =>
        key == "ConnectionStrings"
            ? new ConnectionStringsSection(RedisEndpoint)
            : base.GetSection(key);
}

using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace UrlShortener.Infrastructure.Tests;

[ExcludeFromCodeCoverage]
internal sealed class ValidConfiguration : EmptyConfiguration
{
    public override string? this[string key]
    {
        get => key == "ConnectionStrings:PostgreSql"
            ? "Host=localhost;Database=test;Username=test;Password=test"
            : null;
        set { }
    }

    public override IConfigurationSection GetSection(string key) =>
        key == "ConnectionStrings"
            ? new ConnectionStringsSection()
            : base.GetSection(key);
}

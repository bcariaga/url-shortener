using System.Diagnostics.CodeAnalysis;

namespace UrlShortener.Infrastructure.Tests;

[ExcludeFromCodeCoverage]
internal sealed class ConnectionStringsSection : EmptySection
{
    public ConnectionStringsSection() : base("ConnectionStrings")
    {
    }

    public override string? this[string key]
    {
        get => key == "PostgreSql"
            ? "Host=localhost;Database=test;Username=test;Password=test"
            : null;
        set { }
    }
}

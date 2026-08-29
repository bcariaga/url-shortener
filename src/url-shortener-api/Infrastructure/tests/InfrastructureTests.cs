using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;

namespace UrlShortener.Infrastructure.Tests;

public class InfrastructureTests
{
    [Fact]
    public void Missing_connection_string_fails() => Assert.Throws<InvalidOperationException>(
        () => new ServiceCollection().AddInfrastructure(new EmptyConfiguration()));

    [Fact]
    public void Valid_connection_string_registers_npgsql_context()
    {
        var services = new ServiceCollection().AddInfrastructure(new ValidConfiguration());
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<UrlShortenerDbContext>();

        Assert.Equal("Npgsql.EntityFrameworkCore.PostgreSQL", context.Database.ProviderName);
    }

    [ExcludeFromCodeCoverage]
    private class EmptyConfiguration : IConfiguration
    {
        public virtual string? this[string key] { get => null; set { } }
        public IEnumerable<IConfigurationSection> GetChildren() => [];
        public IChangeToken GetReloadToken() => new Microsoft.Extensions.Primitives.CancellationChangeToken(new CancellationToken(true));
        public virtual IConfigurationSection GetSection(string key) => new EmptySection(key);
    }
    [ExcludeFromCodeCoverage]
    private class EmptySection(string key) : EmptyConfiguration, IConfigurationSection
    {
        public string Key => key; public string Path => key; public string? Value { get => null; set { } }
    }

    [ExcludeFromCodeCoverage]
    private sealed class ValidConfiguration : EmptyConfiguration
    {
        public override string? this[string key] { get => key == "ConnectionStrings:PostgreSql" ? "Host=localhost;Database=test;Username=test;Password=test" : null; set { } }
        public override IConfigurationSection GetSection(string key) => key == "ConnectionStrings" ? new ConnectionStringsSection() : base.GetSection(key);
    }

    [ExcludeFromCodeCoverage]
    private sealed class ConnectionStringsSection : EmptySection
    {
        public ConnectionStringsSection() : base("ConnectionStrings") { }
        public override string? this[string key] { get => key == "PostgreSql" ? "Host=localhost;Database=test;Username=test;Password=test" : null; set { } }
    }
}

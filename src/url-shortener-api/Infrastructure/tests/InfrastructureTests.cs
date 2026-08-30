using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Microsoft.Extensions.Primitives;
using System.Diagnostics.CodeAnalysis;
using UrlShortener.Domain.Entities;

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

    [Fact]
    public void Short_url_model_has_required_postgres_contract()
    {
        var options = new DbContextOptionsBuilder<UrlShortenerDbContext>()
            .UseNpgsql("Host=localhost;Database=test;Username=test;Password=test")
            .Options;
        using var context = new UrlShortenerDbContext(options);
        var entity = context.Model.FindEntityType(typeof(ShortUrl))!;

        Assert.Equal("short_urls", entity.GetTableName());
        var id = entity.FindProperty(nameof(ShortUrl.Id))!;
        Assert.Equal("id", id.GetColumnName());
        Assert.Equal("bigint", id.GetColumnType());
        Assert.True(id.ValueGenerated == Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAdd);
        var shortCode = entity.FindProperty(nameof(ShortUrl.ShortCode))!;
        var longUrl = entity.FindProperty(nameof(ShortUrl.LongUrl))!;
        var ownerId = entity.FindProperty(nameof(ShortUrl.OwnerId))!;
        var isDeleted = entity.FindProperty(nameof(ShortUrl.IsDeleted))!;
        var createdAt = entity.FindProperty(nameof(ShortUrl.CreatedAt))!;
        var updatedAt = entity.FindProperty(nameof(ShortUrl.UpdatedAt))!;
        Assert.Equal("short_code", shortCode.GetColumnName());
        Assert.Equal("long_url", longUrl.GetColumnName());
        Assert.Equal("owner_id", ownerId.GetColumnName());
        Assert.Equal("is_deleted", isDeleted.GetColumnName());
        Assert.Equal("created_at", createdAt.GetColumnName());
        Assert.Equal("updated_at", updatedAt.GetColumnName());
        Assert.Equal("character varying(6)", shortCode.GetColumnType());
        Assert.Equal(6, shortCode.GetMaxLength());
        Assert.Equal(2048, longUrl.GetMaxLength());
        Assert.Equal(256, ownerId.GetMaxLength());
        Assert.Equal("boolean", isDeleted.GetColumnType());
        Assert.Equal(false, isDeleted.GetDefaultValue());
        Assert.Equal("timestamp with time zone", createdAt.GetColumnType());
        Assert.Equal("timestamp with time zone", updatedAt.GetColumnType());

        var indexes = entity.GetIndexes().ToArray();
        var unique = Assert.Single(indexes, index => index.IsUnique);
        Assert.Equal("ux_short_urls_short_code", unique.GetDatabaseName());
        Assert.Equal("short_code", unique.Properties.Single().GetColumnName());
        Assert.Equal([nameof(ShortUrl.ShortCode)], unique.Properties.Select(property => property.Name));
        Assert.DoesNotContain(indexes, index => index.Properties.Any(property => property.Name == nameof(ShortUrl.OwnerId)));
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

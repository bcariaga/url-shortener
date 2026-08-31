using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using UrlShortener.Domain.Entities;
using UrlShortener.Infrastructure.Cache;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Infrastructure.Resilience;
using Xunit;

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
        var connection = new NpgsqlConnectionStringBuilder(context.Database.GetConnectionString());
        Assert.Equal(3, connection.Timeout);
        Assert.Equal(5, connection.CommandTimeout);
    }

    [Fact]
    public void Missing_redis_uses_noop_provider()
    {
        using var provider = new ServiceCollection().AddInfrastructure(new ValidConfiguration()).BuildServiceProvider();
        Assert.IsType<NoOpCacheProvider>(provider.GetRequiredService<ICacheProvider>());
    }

    [Fact]
    public void Postgres_circuit_pipeline_is_shared_across_request_scopes()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(new ValidConfiguration())
            .BuildServiceProvider();
        using var firstScope = provider.CreateScope();
        using var secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider.GetRequiredKeyedService<ResiliencePipeline>(PostgresResiliencePipeline.Name);
        var second = secondScope.ServiceProvider.GetRequiredKeyedService<ResiliencePipeline>(PostgresResiliencePipeline.Name);
        var firstReadRetry = firstScope.ServiceProvider.GetRequiredKeyedService<ResiliencePipeline>(PostgresResiliencePipeline.ReadRetryName);
        var secondReadRetry = secondScope.ServiceProvider.GetRequiredKeyedService<ResiliencePipeline>(PostgresResiliencePipeline.ReadRetryName);

        Assert.Same(first, second);
        Assert.Same(firstReadRetry, secondReadRetry);
        Assert.IsType<CachingShortUrlRepository>(
            firstScope.ServiceProvider.GetRequiredService<Domain.Repositories.IShortUrlRepository>());
    }

    [Fact]
    public async Task Unreachable_redis_resolves_provider_graph_within_cache_budget()
    {
        var config = new ValidConfiguration { RedisEndpoint = "127.0.0.1:1" };
        using var provider = new ServiceCollection().AddLogging().AddInfrastructure(config).BuildServiceProvider();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var cache = provider.GetRequiredService<ICacheProvider>();
        var repository = provider.GetRequiredService<UrlShortener.Domain.Repositories.IShortUrlRepository>();
        stopwatch.Stop();
        Assert.IsType<RedisCacheProvider>(cache);
        Assert.NotNull(repository);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Invalid_cache_options_fail_when_resolved()
    {
        var config = new ValidConfiguration { CacheTimeout = "0", CacheTtl = "-1" };
        using var provider = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<CacheOptions>>().Value);
    }

    [Fact]
    public void Invalid_database_resilience_options_fail_when_resolved()
    {
        var config = new ValidConfiguration { DatabaseMinimumThroughput = "1" };
        using var provider = new ServiceCollection().AddInfrastructure(config).BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() =>
            provider.GetRequiredService<IOptions<Resilience.DatabaseResilienceOptions>>().Value);
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
}

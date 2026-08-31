using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using StackExchange.Redis;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;
using UrlShortener.Infrastructure.Cache;
using UrlShortener.Infrastructure.Repositories;
using UrlShortener.Infrastructure.Resilience;
using UrlShortener.Infrastructure.Services;

namespace UrlShortener.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services
            .AddPostgres(configuration)
            .AddRedis(configuration);

        services.AddScoped<EFShortUrlRepository>();

        services.AddScoped(sp =>
            new ResilientPostgresShortUrlRepository(
                sp.GetRequiredService<EFShortUrlRepository>(),
                sp.GetRequiredKeyedService<ResiliencePipeline>(PostgresResiliencePipeline.Name),
                sp.GetRequiredKeyedService<ResiliencePipeline>(PostgresResiliencePipeline.ReadRetryName),
                sp.GetRequiredService<IOptions<DatabaseResilienceOptions>>()));

        services.AddScoped<IShortUrlRepository>(sp =>
            new CachingShortUrlRepository(
                    realRepository: sp.GetRequiredService<ResilientPostgresShortUrlRepository>(),
                    cache: sp.GetRequiredService<ICacheProvider>(),
                    options: sp.GetRequiredService<IOptions<CacheOptions>>(),
                    logger: sp.GetRequiredService<ILogger<CachingShortUrlRepository>>()
                ));
        services.AddSingleton<IShortCodeGenerator, Sha256Base62Generator>();
        services.AddSingleton<IShortUrlClock, SystemShortUrlClock>();
        services.AddSingleton<IPublicUrlBuilder, ConfigurationPublicUrlBuilder>();
        return services;
    }

    private static IServiceCollection AddRedis(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<CacheOptions>().Configure(o =>
        {
            if (int.TryParse(configuration["Cache:TtlSeconds"], out var ttl)) o.TtlSeconds = ttl;
            if (int.TryParse(configuration["Cache:TimeoutMilliseconds"], out var timeout)) o.TimeoutMilliseconds = timeout;
            if (int.TryParse(configuration["Cache:ConnectionTimeoutMilliseconds"], out var connectionTimeout)) o.ConnectionTimeoutMilliseconds = connectionTimeout;
        })
        .Validate(o =>
            o.TtlSeconds > 0 &&
            o.TimeoutMilliseconds > 0 &&
            o.ConnectionTimeoutMilliseconds > 0, "Cache policy values must be positive")
        .ValidateOnStart();

        var redisConnection = configuration.GetConnectionString("Redis");
        if (string.IsNullOrWhiteSpace(redisConnection))
            services.AddSingleton<ICacheProvider, NoOpCacheProvider>();
        else
        {
            services.AddSingleton<IConnectionMultiplexer>(sp =>
            {
                var connectionTimeoutMilliseconds = sp.GetRequiredService<IOptions<CacheOptions>>().Value.ConnectionTimeoutMilliseconds;
                return ConnectionMultiplexer.Connect(
                    new ConfigurationOptions
                    {
                        EndPoints = { redisConnection },
                        AbortOnConnectFail = false,
                        ConnectTimeout = connectionTimeoutMilliseconds,
                        SyncTimeout = connectionTimeoutMilliseconds,
                        AsyncTimeout = connectionTimeoutMilliseconds
                    });
            });
            services.AddSingleton<ICacheProvider, RedisCacheProvider>();
        }
        return services;
    }

    private static IServiceCollection AddPostgres(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("PostgreSql");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Required configuration key 'ConnectionStrings:PostgreSql' is missing.");
        }

        var resilienceOptions = ReadDatabaseResilienceOptions(configuration);
        services
            .AddOptions<DatabaseResilienceOptions>()
            .Configure(options =>
            {
                options.ConnectionTimeoutSeconds = resilienceOptions.ConnectionTimeoutSeconds;
                options.CommandTimeoutSeconds = resilienceOptions.CommandTimeoutSeconds;
                options.ReadMaxRetryAttempts = resilienceOptions.ReadMaxRetryAttempts;
                options.ReadRetryDelayMilliseconds = resilienceOptions.ReadRetryDelayMilliseconds;
                options.FailureRatio = resilienceOptions.FailureRatio;
                options.SamplingDurationSeconds = resilienceOptions.SamplingDurationSeconds;
                options.MinimumThroughput = resilienceOptions.MinimumThroughput;
                options.BreakDurationSeconds = resilienceOptions.BreakDurationSeconds;
            })
            .Validate(options =>
                    options.ConnectionTimeoutSeconds > 0 &&
                    options.CommandTimeoutSeconds > 0 &&
                    options.ReadMaxRetryAttempts > 0 &&
                    options.ReadRetryDelayMilliseconds > 0 &&
                    options.FailureRatio is > 0 and <= 1 &&
                    options.SamplingDurationSeconds > 0 &&
                    options.MinimumThroughput >= 2 &&
                    options.BreakDurationSeconds > 0,
                "Database resilience policy values are invalid.")
            .ValidateOnStart();

        var connectionStringBuilder = new NpgsqlConnectionStringBuilder(connectionString)
        {
            Timeout = resilienceOptions.ConnectionTimeoutSeconds,
            CommandTimeout = resilienceOptions.CommandTimeoutSeconds
        };

        services.AddDbContext<UrlShortenerDbContext>(options =>
            options.UseNpgsql(connectionStringBuilder.ConnectionString));

        services.AddResiliencePipeline(PostgresResiliencePipeline.Name, builder =>
            builder.AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(PostgresTransientFailureDetector.IsTransient),
                FailureRatio = resilienceOptions.FailureRatio,
                SamplingDuration = TimeSpan.FromSeconds(resilienceOptions.SamplingDurationSeconds),
                MinimumThroughput = resilienceOptions.MinimumThroughput,
                BreakDuration = TimeSpan.FromSeconds(resilienceOptions.BreakDurationSeconds)
            }));

        services.AddResiliencePipeline(PostgresResiliencePipeline.ReadRetryName, builder =>
            builder.AddRetry(new RetryStrategyOptions
            {
                ShouldHandle = new PredicateBuilder()
                    .Handle<Exception>(PostgresTransientFailureDetector.IsTransient),
                MaxRetryAttempts = resilienceOptions.ReadMaxRetryAttempts,
                Delay = TimeSpan.FromMilliseconds(resilienceOptions.ReadRetryDelayMilliseconds),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true
            }));

        return services;
    }

    private static DatabaseResilienceOptions ReadDatabaseResilienceOptions(IConfiguration configuration)
    {
        var options = new DatabaseResilienceOptions();
        var section = DatabaseResilienceOptions.SectionName;

        if (int.TryParse(configuration[$"{section}:ConnectionTimeoutSeconds"], out var connectionTimeout)) options.ConnectionTimeoutSeconds = connectionTimeout;
        if (int.TryParse(configuration[$"{section}:CommandTimeoutSeconds"], out var commandTimeout)) options.CommandTimeoutSeconds = commandTimeout;
        if (int.TryParse(configuration[$"{section}:ReadMaxRetryAttempts"], out var readMaxRetryAttempts)) options.ReadMaxRetryAttempts = readMaxRetryAttempts;
        if (int.TryParse(configuration[$"{section}:ReadRetryDelayMilliseconds"], out var readRetryDelay)) options.ReadRetryDelayMilliseconds = readRetryDelay;
        if (double.TryParse(configuration[$"{section}:FailureRatio"], NumberStyles.Float, CultureInfo.InvariantCulture, out var failureRatio)) options.FailureRatio = failureRatio;
        if (int.TryParse(configuration[$"{section}:SamplingDurationSeconds"], out var samplingDuration)) options.SamplingDurationSeconds = samplingDuration;
        if (int.TryParse(configuration[$"{section}:MinimumThroughput"], out var minimumThroughput)) options.MinimumThroughput = minimumThroughput;
        if (int.TryParse(configuration[$"{section}:BreakDurationSeconds"], out var breakDuration)) options.BreakDurationSeconds = breakDuration;

        return options;
    }
}

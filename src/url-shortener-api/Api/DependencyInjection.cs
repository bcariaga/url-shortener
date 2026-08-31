using Microsoft.AspNetCore.Authentication;
using UrlShortener.Application;
using UrlShortener.Infrastructure;
using UrlShortener.Api.Auth;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Telemetry;
using UrlShortener.Api.Healtchecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using UrlShortener.Application.Telemetry;
using UrlShortener.Domain.Telemetry;
using UrlShortener.Infrastructure.Telemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Logs;
using UrlShortener.Api.Errors;

namespace UrlShortener.Api;

public static class DependencyInjection
{
    public static IServiceCollection ConfigureApi(this WebApplicationBuilder builder)
    {
        var services = builder.Services;

        services.AddControllers();
        services.AddProblemDetails();
        services.AddExceptionHandler<KnownExceptionHandler>();
        services.AddExceptionHandler<UnexpectedExceptionHandler>();
        services.AddOptions(builder);

        services
            .AddAuthentication("Bearer")
            .AddScheme<AuthenticationSchemeOptions, TokenAuthHandler>("Bearer", _ => { });

        services.AddAuthorization();

        services.AddApplication();
        services.AddInfrastructure(builder.Configuration);

        ConfigureHealthChecks(builder, services);
        ConfigureObservability(builder, services);

        return services;
    }

    private static void ConfigureObservability(WebApplicationBuilder builder, IServiceCollection services)
    {
        var tracingEnabled = builder.Configuration.GetValue("Observability:TracingEnabled", true);
        var openTelemetry = services
            .AddOpenTelemetry()
            .ConfigureResource(r => r.AddService("url-shortener-api"));
        if (tracingEnabled)
        {
            openTelemetry.
                WithTracing(t =>
                    t.AddAspNetCoreInstrumentation(o => o.Filter = c => !c.Request.Path.StartsWithSegments("/health"))
                        .AddApiActivitySources()
                        .AddApplicationActivitySources()
                        .AddDomainActivitySources()
                        .AddInfrastructureActivitySources());
        }
        openTelemetry.WithMetrics(m => m.AddAspNetCoreInstrumentation().AddRuntimeInstrumentation());
        if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            builder.Logging.AddOpenTelemetry(o =>
            {
                o.IncludeScopes = true;
                o.IncludeFormattedMessage = true;
                o.AddOtlpExporter();
            });
            if (tracingEnabled)
                services.ConfigureOpenTelemetryTracerProvider(t => t.AddOtlpExporter());

            services.ConfigureOpenTelemetryMeterProvider(m => m.AddOtlpExporter());
        }
    }

    private static void ConfigureHealthChecks(WebApplicationBuilder builder, IServiceCollection services)
    {
        var healthTimeout = builder.Configuration.GetValue<int?>("HealthChecks:TimeoutSeconds") ?? 2;
        if (healthTimeout <= 0) throw new InvalidOperationException("HealthChecks:TimeoutSeconds must be positive.");

        services.AddHealthChecks()
            .AddDbContextCheck<UrlShortenerDbContext>(
                "postgresql",
                tags: ["ready"]);
        if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Redis")))
        {
            services.AddSingleton<IRedisHealthProbe, StackExchangeRedisHealthProbe>();
            services.AddHealthChecks().AddCheck<RedisHealthCheck>(
                "redis",
                failureStatus: HealthStatus.Degraded,
                tags: ["ready"],
                timeout: TimeSpan.FromSeconds(healthTimeout));
        }
        services.Configure<HealthCheckServiceOptions>(options =>
        {
            foreach (var registration in options.Registrations)
            {
                if (registration.Tags.Contains("ready"))
                {
                    registration.Timeout = TimeSpan.FromSeconds(healthTimeout);
                }
            }
        });
    }

    private static IServiceCollection AddOptions(this IServiceCollection services, WebApplicationBuilder builder)
    {
        services
            .AddOptions<ObservabilityOptions>()
            .Bind(builder.Configuration.GetSection(ObservabilityOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services
            .AddOptions<ManagementAuthOptions>()
            .Bind(builder.Configuration.GetSection("ManagementAuth"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services
            .AddOptions<PublicUrlOptions>()
            .Bind(builder.Configuration)
            .Validate(
                options => PublicUrlOptions.IsValid(options.PublicBaseUrl),
                "PublicBaseUrl must be an absolute HTTP or HTTPS URL.")
            .ValidateOnStart();

        return services;
    }

}

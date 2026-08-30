using Microsoft.AspNetCore.Authentication;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using OpenTelemetry.Logs;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using StackExchange.Redis;
using UrlShortener.Application;
using UrlShortener.Application.Telemetry;
using UrlShortener.Domain.Telemetry;
using UrlShortener.Infrastructure;
using UrlShortener.Infrastructure.Telemetry;
using UrlShortener.Api.Telemetry;
using UrlShortener.Api.Auth;
using UrlShortener.Api.Configuration;
using UrlShortener.Api.Healtchecks;
using UrlShortener.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services
    .AddOptions<ObservabilityOptions>()
    .Bind(builder.Configuration.GetSection(ObservabilityOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<ManagementAuthOptions>()
    .Bind(builder.Configuration.GetSection("ManagementAuth"))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services
    .AddOptions<PublicUrlOptions>()
    .Bind(builder.Configuration)
    .Validate(
        options => PublicUrlOptions.IsValid(options.PublicBaseUrl),
        "PublicBaseUrl must be an absolute HTTP or HTTPS URL.")
    .ValidateOnStart();
builder.Services
    .AddAuthentication("Bearer")
    .AddScheme<AuthenticationSchemeOptions, TokenAuthHandler>("Bearer", _ => { });
builder.Services.AddAuthorization();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
var healthTimeout = builder.Configuration.GetValue<int?>("HealthChecks:TimeoutSeconds") ?? 2;
if (healthTimeout <= 0) throw new InvalidOperationException("HealthChecks:TimeoutSeconds must be positive.");
builder.Services.AddHealthChecks()
    .AddDbContextCheck<UrlShortenerDbContext>(
        "postgresql",
        tags: ["ready"]);
if (!string.IsNullOrWhiteSpace(builder.Configuration.GetConnectionString("Redis")))
{
    builder.Services.AddSingleton<IRedisHealthProbe, StackExchangeRedisHealthProbe>();
    builder.Services.AddHealthChecks().AddCheck<RedisHealthCheck>(
        "redis",
        failureStatus: HealthStatus.Degraded,
        tags: ["ready"],
        timeout: TimeSpan.FromSeconds(healthTimeout));
}
builder.Services.Configure<HealthCheckServiceOptions>(options =>
{
    foreach (var registration in options.Registrations)
    {
        if (registration.Tags.Contains("ready"))
        {
            registration.Timeout = TimeSpan.FromSeconds(healthTimeout);
        }
    }
});
var tracingEnabled = builder.Configuration.GetValue("Observability:TracingEnabled", true);
var openTelemetry = builder.Services
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
        builder.Services.ConfigureOpenTelemetryTracerProvider(t => t.AddOtlpExporter());

    builder.Services.ConfigureOpenTelemetryMeterProvider(m => m.AddOtlpExporter());
}

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Api.Errors").LogError(new EventId(1002, "UnexpectedApiFailure"), "Unexpected API failure. TraceId={TraceId}", Activity.Current?.TraceId.ToString());
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";
    await Results.Problem(statusCode: 500, title: "An unexpected error occurred.", extensions: new Dictionary<string, object?> { ["traceId"] = Activity.Current?.TraceId.ToString() }).ExecuteAsync(context);
}));
app.UseRouting();
app.UseMiddleware<FlowLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false, ResponseWriter = HealthResponseWriter.Write });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready"), ResponseWriter = HealthResponseWriter.Write });

app.Run();

public partial class Program;

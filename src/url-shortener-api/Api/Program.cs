using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UrlShortener.Api;
using UrlShortener.Api.Healtchecks;
using UrlShortener.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureApi();

var app = builder.Build();

app.UseExceptionHandler();

app.UseRouting();
app.UseMiddleware<FlowLoggingMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false,
    ResponseWriter = HealthResponseWriter.Write
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = HealthResponseWriter.Write
});

app.Run();

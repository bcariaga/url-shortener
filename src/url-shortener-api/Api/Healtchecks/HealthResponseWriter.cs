using System.Text.Json;
using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace UrlShortener.Api.Healtchecks;

public static class HealthResponseWriter
{
    public static async Task Write(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = report.Status == HealthStatus.Unhealthy ? 503 : 200;
        var checks = report.Entries
            .OrderBy(x => x.Key)
            .Select(x => new
            {
                name = x.Key,
                status = x.Value.Status.ToString(),
                durationMilliseconds = Math.Round(x.Value.Duration.TotalMilliseconds, 1)
            });
        await JsonSerializer.SerializeAsync(context.Response.Body, new { status = report.Status.ToString(), checks });
    }
}

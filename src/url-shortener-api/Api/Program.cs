using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using UrlShortener.Api;
using UrlShortener.Api.Healtchecks;
using UrlShortener.Api.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.ConfigureApi();

var app = builder.Build();

app.UseExceptionHandler(errorApp => errorApp.Run(static async context =>
{
    context.RequestServices.GetRequiredService<ILoggerFactory>()
        .CreateLogger("Api.Errors")
        .LogError(new EventId(1002, "UnexpectedApiFailure"), "Unexpected API failure. TraceId={TraceId}", Activity.Current?.TraceId.ToString());
    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
    context.Response.ContentType = "application/problem+json";

    await Results.Problem(
        statusCode: 500,
        title: "An unexpected error occurred.",
        extensions: new Dictionary<string, object?>
        {
            ["traceId"] = Activity.Current?.TraceId.ToString()
        }).ExecuteAsync(context);
}));

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


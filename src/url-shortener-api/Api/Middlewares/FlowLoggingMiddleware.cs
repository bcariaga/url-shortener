using Microsoft.AspNetCore.Mvc.Controllers;

namespace UrlShortener.Api.Middlewares;

public sealed class FlowLoggingMiddleware(
    RequestDelegate next,
    ILogger<FlowLoggingMiddleware> logger)
{
    private static readonly EventId FlowCompleted = new(1001, "FlowCompleted");

    public async Task InvokeAsync(HttpContext context)
    {
        var action = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>()?.ActionName;
        var operation = action switch
        {
            "Create" => "create",
            "Update" => "update",
            "Delete" => "delete",
            "Get" when context.Request.RouteValues.ContainsKey("shortCode") => "resolve",
            _ => null
        };
        if (operation is null)
        {
            await next(context);
            return;
        }

        var code = context.Request.RouteValues.TryGetValue("code", out var codeValue)
            ? codeValue?.ToString()
            : context.Request.RouteValues.TryGetValue("shortCode", out var shortCodeValue)
                ? shortCodeValue?.ToString()
                : null;
        await next(context);
        var outcome = Outcome(context.Response.StatusCode);
        logger.LogInformation(
            FlowCompleted,
            "Short URL flow completed. Operation={Operation} Outcome={Outcome} StatusCode={StatusCode} ShortCode={ShortCode}",
            operation,
            outcome,
            context.Response.StatusCode,
            code);
    }

    private static string Outcome(int status) => status switch
    {
        >= 200 and <= 302 => "success",
        404 => "not_found",
        400 => "validation_error",
        401 or 403 => "unauthorized",
        503 => "capacity_exhausted",
        _ => "error"
    };
}

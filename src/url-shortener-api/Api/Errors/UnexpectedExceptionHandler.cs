using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace UrlShortener.Api.Errors;

public sealed class UnexpectedExceptionHandler(ILogger<UnexpectedExceptionHandler> logger, IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(new EventId(1002, "UnexpectedApiFailure"), exception, "Unexpected API failure. TraceId={TraceId}", System.Diagnostics.Activity.Current?.TraceId.ToString());
        var problem = new ProblemDetails { Status = 500, Title = "An unexpected error occurred." };
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = 500;
        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
        return true;
    }
}

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Exceptions;
using UrlShortener.Domain.Exceptions;

namespace UrlShortener.Api.Errors;

public sealed class KnownExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (statusCode, title, detail, code) = exception switch
        {
            ShortCodeAttemptsExhaustedException capacity => (503, "Short URL capacity temporarily unavailable.", capacity.Message, ShortCodeAttemptsExhaustedException.ErrorCode),
            RequiredShortUrlValueException required => (400, "Invalid short URL data.", required.Message, required.Code),
            InvalidShortUrlStateException state => (409, "Short URL state conflict.", state.Message, state.Code),
            _ => (0, null, null, null)
        };

        if (statusCode == 0) return false; //unhandled here, catch in next middleware

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? httpContext.TraceIdentifier;
        httpContext.Response.StatusCode = statusCode;

        await problemDetails.WriteAsync(new ProblemDetailsContext { HttpContext = httpContext, ProblemDetails = problem });
        return true;
    }
}

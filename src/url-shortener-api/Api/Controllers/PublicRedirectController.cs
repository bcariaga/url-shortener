using FluentValidation;
using Mediary.Dispatcher;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Telemetry;
using UrlShortener.Application.Handlers.Queries;
namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("")]
public sealed class PublicRedirectController(IRequestDispatcher dispatcher, IValidator<ResolveShortUrlQuery> validator) : ControllerBase
{
    [HttpGet("{shortCode}")]
    public async Task<IActionResult> Get(string shortCode)
    {
        using var activity = ActivitySources.PublicRedirect.StartActivity(nameof(Get));
        var query = new ResolveShortUrlQuery
        {
            ShortCode = shortCode
        };

        if (!(await validator.ValidateAsync(query)).IsValid) return NotFoundError();

        var destination = await dispatcher.DispatchAsync<string?, ResolveShortUrlQuery>(query);

        return destination is null ? NotFoundError() : Redirect(destination);
    }
    private ObjectResult NotFoundError() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Short URL not found.");
}

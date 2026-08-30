using FluentValidation;
using Mediary.Dispatcher;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application.Handlers.Queries;
namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("")]
public sealed class PublicRedirectController(IRequestDispatcher dispatcher, IValidator<ResolveShortUrlQuery> validator) : ControllerBase
{
    [HttpGet("{shortCode}")]
    public async Task<IActionResult> Get(string shortCode)
    {
        var query = new ResolveShortUrlQuery
        {
            ShortCode = shortCode
        };

        if (!(await validator.ValidateAsync(query)).IsValid) return NotFoundError();

        var destination = await dispatcher.DispatchAsync<string?, ResolveShortUrlQuery>(query);

        return destination is null ? NotFoundError() : Redirect(destination);
    }
    private IActionResult NotFoundError() => Problem(statusCode: StatusCodes.Status404NotFound, title: "Short URL not found.");
}

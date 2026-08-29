using Mediary.Dispatcher;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Application;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("")]
public sealed class HomeController(IRequestDispatcher mediator) : ControllerBase
{
    [HttpGet]
    [Produces("text/plain")]
    public async Task<IActionResult> Get(CancellationToken cancellationToken) =>
        Content(await mediator.DispatchAsync<string, HelloWorldQuery>(new HelloWorldQuery()), "text/plain");
}

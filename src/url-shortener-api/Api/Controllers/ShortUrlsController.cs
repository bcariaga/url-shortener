
using FluentValidation;
using Mediary.Dispatcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using UrlShortener.Application;
namespace UrlShortener.Api.Controllers;

[ApiController, Route("api/v1/short-urls"), Authorize]
public sealed class ShortUrlsController(IRequestDispatcher dispatcher, IValidator<CreateShortUrlCommand> createShortUrlCommandValidator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateShortUrlCommand request)
    {
        var validationResult = createShortUrlCommandValidator.Validate(request);
        if (!validationResult.IsValid) return BadRequest(CreateErrorFromValidationResult(validationResult));

        try
        {
            var result = await dispatcher.DispatchAsync<ShortUrlRepresentation, CreateShortUrlCommand>(
                new(
                    Owner(),
                    request.Url));

            return Created(result.ShortUrl, result);
        }
        catch (ShortCodeAttemptsExhaustedException)
        {
            return Problem(statusCode: 503, title: "Short URL capacity temporarily unavailable.");
        }
    }

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, UrlRequest request)
    {
        if (!Valid(request.Url)) return ValidationProblem("The URL must be an absolute HTTP or HTTPS URL of 2,048 characters or fewer.");
        if (!Code(code)) return NotFoundProblem();
        var result = await dispatcher.DispatchAsync<ShortUrlRepresentation?, UpdateShortUrlCommand>(new(Owner(), code, request.Url!));
        return result is null ? NotFoundProblem() : Ok(result);
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        if (!Code(code)) return NotFoundProblem();
        return await dispatcher.DispatchAsync<bool, DeleteShortUrlCommand>(new(Owner(), code)) ? NoContent() : NotFoundProblem();
    }
    private string Owner() => User.FindFirstValue("owner_id")!;
    private static bool Code(string value) => value.Length == 6 && value.All(c => "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".Contains(c));
    private IActionResult NotFoundProblem() => Problem(statusCode: 404, title: "Short URL not found.");
    private static bool Valid(string? value) => ShortUrlValidation.IsDestination(value);
    public sealed record UrlRequest(string? Url);
    private static object CreateErrorFromValidationResult(FluentValidation.Results.ValidationResult validationResult) => new
    {
        validationResult.Errors
    };
}

using System.Security.Claims;
using FluentValidation;
using FluentValidation.Results;
using Mediary.Dispatcher;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UrlShortener.Api.Requests;
using UrlShortener.Api.Telemetry;
using UrlShortener.Application.Handlers.Commands;
using UrlShortener.Application.Handlers.Representations;

namespace UrlShortener.Api.Controllers;

[ApiController]
[Route("api/v1/short-urls")]
[Authorize]
public sealed class ShortUrlsController(
    IRequestDispatcher dispatcher,
    IValidator<CreateShortUrlCommand> createValidator,
    IValidator<UpdateShortUrlCommand> updateValidator,
    IValidator<DeleteShortUrlCommand> deleteValidator) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(UrlRequest request)
    {
        using var activity = ActivitySources.ShortUrls.StartActivity(nameof(Create));
        var command = new CreateShortUrlCommand
        {
            OwnerId = OwnerId(),
            Url = request.Url ?? string.Empty
        };
        var validationResult = await createValidator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            return ValidationError(validationResult);
        }
        var result = await dispatcher.DispatchAsync<ShortUrlRepresentation, CreateShortUrlCommand>(command);

        return Created(result.ShortUrl, result);
    }

    [HttpPut("{code}")]
    public async Task<IActionResult> Update(string code, UrlRequest request)
    {
        using var activity = ActivitySources.ShortUrls.StartActivity(nameof(Update));
        var command = new UpdateShortUrlCommand
        {
            OwnerId = OwnerId(),
            ShortCode = code,
            Url = request.Url ?? string.Empty
        };
        var validationResult = await updateValidator.ValidateAsync(command);
        if (HasErrorFor(validationResult, nameof(UpdateShortUrlCommand.ShortCode)))
        {
            return NotFoundError();
        }

        if (!validationResult.IsValid)
        {
            return ValidationError(validationResult);
        }

        var result = await dispatcher.DispatchAsync<ShortUrlRepresentation?, UpdateShortUrlCommand>(command);

        return result is null ? NotFoundError() : Ok(result);
    }

    [HttpDelete("{code}")]
    public async Task<IActionResult> Delete(string code)
    {
        using var activity = ActivitySources.ShortUrls.StartActivity(nameof(Delete));
        var command = new DeleteShortUrlCommand
        {
            OwnerId = OwnerId(),
            ShortCode = code
        };
        var validationResult = await deleteValidator.ValidateAsync(command);
        if (!validationResult.IsValid)
        {
            return NotFoundError();
        }

        var deleted = await dispatcher.DispatchAsync<bool, DeleteShortUrlCommand>(command);
        return deleted ? NoContent() : NotFoundError();
    }

    private string OwnerId() => User.FindFirstValue("owner_id")!;

    private ActionResult ValidationError(ValidationResult validationResult)
    {
        var errors = validationResult.Errors
            .GroupBy(error => error.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.ErrorMessage).ToArray());

        return ValidationProblem(new ValidationProblemDetails(errors));
    }

    private ObjectResult NotFoundError() => Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Short URL not found.");

    private static bool HasErrorFor(ValidationResult result, string propertyName) =>
        result.Errors.Any(error => error.PropertyName == propertyName);
}

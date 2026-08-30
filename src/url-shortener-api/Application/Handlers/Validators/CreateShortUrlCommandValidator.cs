using FluentValidation;
using UrlShortener.Application.Handlers.Commands;

namespace UrlShortener.Application.Handlers.Validators;

public sealed class CreateShortUrlCommandValidator : AbstractValidator<CreateShortUrlCommand>
{
    public CreateShortUrlCommandValidator()
    {
        RuleFor(command => command.OwnerId).NotEmpty();
        RuleFor(command => command.Url).ValidDestinationUrl();
    }
}

using FluentValidation;
using UrlShortener.Application.Validators;

namespace UrlShortener.Application.Handlers.Commands.Validators;

public sealed class CreateShortUrlCommandValidator : AbstractValidator<CreateShortUrlCommand>
{
    public CreateShortUrlCommandValidator()
    {
        RuleFor(command => command.OwnerId)
         .NotNull()
         .NotEmpty();
        RuleFor(command => command.Url)
         .MustBeValidUrl();
    }
}
using FluentValidation;
using UrlShortener.Application.Handlers.Commands;

namespace UrlShortener.Application.Handlers.Validators;

public sealed class UpdateShortUrlCommandValidator : AbstractValidator<UpdateShortUrlCommand>
{
    public UpdateShortUrlCommandValidator()
    {
        RuleFor(command => command.OwnerId).NotEmpty();
        RuleFor(command => command.ShortCode).ValidShortCode();
        RuleFor(command => command.Url).ValidDestinationUrl();
    }
}

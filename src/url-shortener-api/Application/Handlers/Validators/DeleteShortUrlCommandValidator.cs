using FluentValidation;
using UrlShortener.Application.Handlers.Commands;

namespace UrlShortener.Application.Handlers.Validators;

public sealed class DeleteShortUrlCommandValidator : AbstractValidator<DeleteShortUrlCommand>
{
    public DeleteShortUrlCommandValidator()
    {
        RuleFor(command => command.OwnerId).NotEmpty();
        RuleFor(command => command.ShortCode).ValidShortCode();
    }
}

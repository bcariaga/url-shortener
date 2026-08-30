using FluentValidation;
using UrlShortener.Application.Handlers.Queries;
namespace UrlShortener.Application.Handlers.Validators;
public sealed class ResolveShortUrlQueryValidator : AbstractValidator<ResolveShortUrlQuery>
{
    public ResolveShortUrlQueryValidator() => RuleFor(query => query.ShortCode).ValidShortCode();
}

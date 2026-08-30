using FluentValidation;

namespace UrlShortener.Application.Handlers.Validators;

public static class ValidationRuleExtensions
{
    private const string Base62Pattern = "^[0-9A-Za-z]{6}$";

    public static IRuleBuilderOptions<T, string> ValidShortCode<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder.NotEmpty().Matches(Base62Pattern);

    public static IRuleBuilderOptions<T, string> ValidDestinationUrl<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder.NotEmpty().MaximumLength(2048).Must(IsAbsoluteHttpUrl)
            .WithMessage("The URL must be an absolute HTTP or HTTPS URL with a host.");

    private static bool IsAbsoluteHttpUrl(string value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)) return false;
        return uri is not null && !string.IsNullOrWhiteSpace(uri.Host)
            && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
    }
}

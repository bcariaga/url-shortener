using FluentValidation;

namespace UrlShortener.Application.Validators;

public static class CustomUrlValidationExtensions
{
    private const int MaxUrlLength = 2048;

    public static IRuleBuilderOptions<T, string?> MustBeValidUrl<T>(
        this IRuleBuilder<T, string?> ruleBuilder)
    {
        return ruleBuilder.Must((rootObject, url, context) =>
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            if (url.Length > MaxUrlLength)
                return false;

            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri is null)
                return false;

            var isValidScheme = uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
                             || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase);

            var hasHost = !string.IsNullOrWhiteSpace(uri.Host);

            return isValidScheme && hasHost;
        })
        .WithMessage("'{PropertyName}' should be an URL HTTP/HTTPS up to " + MaxUrlLength + " chars.");
    }
}
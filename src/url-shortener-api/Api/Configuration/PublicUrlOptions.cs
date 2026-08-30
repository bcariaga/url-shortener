using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Api.Configuration;

public sealed class PublicUrlOptions
{
    [Required]
    [Url]
    public string? PublicBaseUrl { get; set; }

    public static bool IsValid(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri is not null
        && (uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase));
}

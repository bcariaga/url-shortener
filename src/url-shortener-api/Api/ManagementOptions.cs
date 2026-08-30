using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Api;

public sealed class ManagementAuthOptions : IValidatableObject
{
    public List<TokenOwner> Tokens { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in Tokens)
        {
            if (string.IsNullOrWhiteSpace(entry.Token)) yield return new("Each management token must be non-empty.", ["ManagementAuth:Tokens"]);
            if (string.IsNullOrWhiteSpace(entry.OwnerId)) yield return new("Each management owner id must be non-empty.", ["ManagementAuth:Tokens"]);
            if (!string.IsNullOrWhiteSpace(entry.Token) && !seen.Add(entry.Token)) yield return new("Management tokens must be unique.", ["ManagementAuth:Tokens"]);
        }
    }
}

public sealed class TokenOwner
{
    public string? Token { get; set; }
    public string? OwnerId { get; set; }
}

public sealed class PublicUrlOptions
{
    [Required, Url]
    public string? PublicBaseUrl { get; set; }

    public static bool IsValid(string? value) => Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri is not null && (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase));
}

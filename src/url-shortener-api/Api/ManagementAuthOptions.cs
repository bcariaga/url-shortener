using System.ComponentModel.DataAnnotations;

namespace UrlShortener.Api;

public sealed class ManagementAuthOptions : IValidatableObject
{
    public List<TokenOwner> Tokens { get; set; } = [];

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        var seenTokens = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in Tokens)
        {
            if (string.IsNullOrWhiteSpace(entry.Token))
            {
                yield return new ValidationResult(
                    "Each management token must be non-empty.",
                    ["ManagementAuth:Tokens"]);
            }

            if (string.IsNullOrWhiteSpace(entry.OwnerId))
            {
                yield return new ValidationResult(
                    "Each management owner id must be non-empty.",
                    ["ManagementAuth:Tokens"]);
            }

            if (!string.IsNullOrWhiteSpace(entry.Token) && !seenTokens.Add(entry.Token))
            {
                yield return new ValidationResult(
                    "Management tokens must be unique.",
                    ["ManagementAuth:Tokens"]);
            }
        }
    }
}

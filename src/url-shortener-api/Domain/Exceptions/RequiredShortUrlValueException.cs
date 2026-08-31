namespace UrlShortener.Domain.Exceptions;

public sealed class RequiredShortUrlValueException(string field) : DomainException($"The {field} value is required.", "short_url_required_value")
{
    public string Field { get; } = field;
}

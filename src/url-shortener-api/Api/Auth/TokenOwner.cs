namespace UrlShortener.Api.Auth;

public sealed class TokenOwner
{
    public string? Token { get; set; }

    public string? OwnerId { get; set; }
}

using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using UrlShortener.Api;
namespace UrlShortener.Api;
public sealed class TokenAuthHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IOptions<ManagementAuthOptions> auth) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    { Response.Headers["WWW-Authenticate"] = "Bearer"; return base.HandleChallengeAsync(properties); }

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.Ordinal) || string.IsNullOrWhiteSpace(header[7..]) || header[7..].Contains(' ')) return Task.FromResult(AuthenticateResult.NoResult());
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(header[7..]));
        foreach (var entry in auth.Value.Tokens)
        {
            if (string.IsNullOrEmpty(entry.Token) || string.IsNullOrEmpty(entry.OwnerId)) continue;
            var configuredHash = SHA256.HashData(Encoding.UTF8.GetBytes(entry.Token));
            if (CryptographicOperations.FixedTimeEquals(configuredHash, providedHash))
            {
                var identity = new ClaimsIdentity(Scheme.Name);
                identity.AddClaim(new Claim("owner_id", entry.OwnerId));
                return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
            }
        }
        return Task.FromResult(AuthenticateResult.NoResult());
    }
}

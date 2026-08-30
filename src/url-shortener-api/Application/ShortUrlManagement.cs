using Mediary.Core;
using UrlShortener.Domain.Entities;
using UrlShortener.Domain.Services;
namespace UrlShortener.Application;

public sealed record ShortUrlRepresentation(string ShortCode, string ShortUrl, string Url);
public sealed record CreateShortUrlCommand(string OwnerId, string Url) : ICommand<ShortUrlRepresentation>;
public sealed record UpdateShortUrlCommand(string OwnerId, string ShortCode, string Url) : ICommand<ShortUrlRepresentation?>;
public sealed record DeleteShortUrlCommand(string OwnerId, string ShortCode) : ICommand<bool>;
public sealed class ShortCodeConflictException : Exception;
public sealed class ShortUrlValidationException : Exception;
public interface IShortUrlStore { Task<ShortUrl> InsertAsync(ShortUrl entity, CancellationToken cancellationToken); Task<ShortUrl?> FindActiveAsync(string ownerId, string code, CancellationToken cancellationToken); Task SaveAsync(CancellationToken cancellationToken); }
public interface IShortUrlNonce { string Create(); }
public static class ShortUrlValidation
{
    public static bool IsDestination(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 2048 && Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri is not null && (uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase) || uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase)) && !string.IsNullOrWhiteSpace(uri.Host);
    public static bool IsShortCode(string? value) => value is { Length: 6 } && value.All(static c => "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ".Contains(c));
    public static void ValidateCommand(string owner, string? url, string? code = null) { if (string.IsNullOrWhiteSpace(owner) || !IsDestination(url) || (code is not null && !IsShortCode(code))) throw new ShortUrlValidationException(); }
}
public sealed class CreateShortUrlCommandHandler(IShortUrlStore store, IShortCodeGenerator generator, IShortUrlClock clock, IShortUrlNonce nonce, IPublicUrlBuilder urls) : IRequestHandler<ShortUrlRepresentation, CreateShortUrlCommand>
{
    public async Task<ShortUrlRepresentation> HandleAsync(CreateShortUrlCommand command)
    {
        ShortUrlValidation.ValidateCommand(command.OwnerId, command.Url); var value = nonce.Create();
        for (var counter = 0; counter < 5; counter++)
        {
            var entity = ShortUrl.Create(generator.Generate(command.OwnerId, command.Url, value, counter), command.Url, command.OwnerId, clock.UtcNow);
            try { await store.InsertAsync(entity, CancellationToken.None); return new(entity.ShortCode, urls.Build(entity.ShortCode), entity.LongUrl); }
            catch (ShortCodeConflictException) when (counter < 4) { }
            catch (ShortCodeConflictException) { throw new ShortCodeAttemptsExhaustedException(); }
        }
        throw new ShortCodeAttemptsExhaustedException();
    }
}
public sealed class UpdateShortUrlCommandHandler(IShortUrlStore store, IShortUrlClock clock, IPublicUrlBuilder urls) : IRequestHandler<ShortUrlRepresentation?, UpdateShortUrlCommand>
{
    public async Task<ShortUrlRepresentation?> HandleAsync(UpdateShortUrlCommand command)
    {
        ShortUrlValidation.ValidateCommand(command.OwnerId, command.Url, command.ShortCode); var entity = await store.FindActiveAsync(command.OwnerId, command.ShortCode, CancellationToken.None); if (entity is null) return null; entity.Update(command.Url, clock.UtcNow); await store.SaveAsync(CancellationToken.None); return new(entity.ShortCode, urls.Build(entity.ShortCode), entity.LongUrl);
    }
}
public sealed class DeleteShortUrlCommandHandler(IShortUrlStore store, IShortUrlClock clock) : IRequestHandler<bool, DeleteShortUrlCommand>
{
    public async Task<bool> HandleAsync(DeleteShortUrlCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.OwnerId) || !ShortUrlValidation.IsShortCode(command.ShortCode)) throw new ShortUrlValidationException(); var entity = await store.FindActiveAsync(command.OwnerId, command.ShortCode, CancellationToken.None); if (entity is null) return false; entity.Delete(clock.UtcNow); await store.SaveAsync(CancellationToken.None); return true;
    }
}

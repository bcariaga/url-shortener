using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Primitives;

namespace UrlShortener.Infrastructure.Tests;

[ExcludeFromCodeCoverage]
internal class EmptyConfiguration : IConfiguration
{
    public virtual string? this[string key]
    {
        get => null;
        set { }
    }

    public IEnumerable<IConfigurationSection> GetChildren() => [];

    public IChangeToken GetReloadToken() =>
        new CancellationChangeToken(new CancellationToken(canceled: true));

    public virtual IConfigurationSection GetSection(string key) => new EmptySection(key);
}

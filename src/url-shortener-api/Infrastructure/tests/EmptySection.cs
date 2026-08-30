using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace UrlShortener.Infrastructure.Tests;

[ExcludeFromCodeCoverage]
internal class EmptySection(string key) : EmptyConfiguration, IConfigurationSection
{
    public string Key => key;

    public string Path => key;

    public string? Value { get; set; }
}

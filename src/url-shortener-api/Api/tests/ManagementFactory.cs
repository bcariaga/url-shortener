using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;

namespace UrlShortener.Api.Tests;

public sealed class ManagementFactory : WebApplicationFactory<Program>
{
    public TestShortUrlRepository Repository { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder
            .UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:PostgreSql"] = "Host=invalid",
                    ["PublicBaseUrl"] = "http://localhost:8080",
                    ["ManagementAuth:Tokens:0:Token"] = "test-token",
                    ["ManagementAuth:Tokens:0:OwnerId"] = "owner-a",
                    ["ManagementAuth:Tokens:1:Token"] = "other-token",
                    ["ManagementAuth:Tokens:1:OwnerId"] = "owner-b"
                }))
            .ConfigureServices(services =>
            {
                services.AddSingleton<IShortUrlRepository>(Repository);
                services.AddSingleton<IShortCodeGenerator, TestShortCodeGenerator>();
                services.AddSingleton<IShortUrlClock, TestShortUrlClock>();
                services.AddSingleton<IPublicUrlBuilder, TestPublicUrlBuilder>();
            });
    }
}

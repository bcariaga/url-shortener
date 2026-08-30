using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using UrlShortener.Api.Tests;
using UrlShortener.Domain.Repositories;
using UrlShortener.Domain.Services;

namespace Api.Tests;

public sealed class ExceptionFactory : WebApplicationFactory<Program>
{
    public TestShortUrlRepository Repository { get; } = new();
    public List<CapturedLogRecord> Logs { get; } = [];

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development")
            .ConfigureAppConfiguration((_, configuration) => configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PostgreSql"] = "Host=invalid",
                ["PublicBaseUrl"] = "http://localhost:8080",
                ["ManagementAuth:Tokens:0:Token"] = "test-token",
                ["ManagementAuth:Tokens:0:OwnerId"] = "owner-a"
            }))
            .ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(Logs)))
            .ConfigureServices(services =>
            {
                services.AddSingleton<IShortUrlRepository>(Repository);
                services.AddSingleton<IShortCodeGenerator, TestShortCodeGenerator>();
                services.AddSingleton<IShortUrlClock, TestShortUrlClock>();
                services.AddSingleton<IPublicUrlBuilder, TestPublicUrlBuilder>();
            });
    }
}

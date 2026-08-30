using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace UrlShortener.Infrastructure;

public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<UrlShortenerDbContext>
{
    public UrlShortenerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<UrlShortenerDbContext>()
            .UseNpgsql("Host=localhost;Database=url_shortener;Username=url_shortener;Password=url_shortener_dev")
            .Options;
        return new UrlShortenerDbContext(options);
    }
}

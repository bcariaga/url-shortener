using Microsoft.Extensions.DependencyInjection;
using Mediary;

namespace UrlShortener.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddMediary().AddRequestHandlersFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}

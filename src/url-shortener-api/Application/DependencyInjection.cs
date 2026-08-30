using Microsoft.Extensions.DependencyInjection;

using Mediary;
using FluentValidation;


namespace UrlShortener.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services
            .AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly)
            .AddMediary()
            .AddRequestHandlersFromAssembly(typeof(DependencyInjection).Assembly);

        return services;
    }
}

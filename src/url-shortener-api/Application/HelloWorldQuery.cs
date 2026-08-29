using Mediary.Core;

namespace UrlShortener.Application;

public sealed record HelloWorldQuery : IQuery<string>;

public sealed class HelloWorldQueryHandler : IRequestHandler<string, HelloWorldQuery>
{
    public Task<string> HandleAsync(HelloWorldQuery query) => Task.FromResult("Hello World!");
}

using UrlShortener.Application;
using Xunit;

namespace UrlShortener.Application.Tests;

public class HelloWorldQueryTests
{
    [Fact] public async Task Handler_returns_hello_world() => Assert.Equal("Hello World!", await new HelloWorldQueryHandler().HandleAsync(new HelloWorldQuery()));
}

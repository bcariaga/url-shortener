# Technical specification

## Context

This is the first executable slice of the URL shortener challenge. It proves
the chosen layering, HTTP style, Application dispatch mechanism, persistence
provider registration, test layout, and local development startup without
implementing URL-shortening behavior.

The installed .NET SDK is 10.0.101. Mediary supports DI registration and
assembly-based request-handler discovery, which will be used to keep the
controller thin.

## Proposed solution

Create the following structure:

```text
src/url-shortener-api/
  UrlShortener.sln
  Directory.Build.props
  docker-compose.development.yml
  Api/
    Api.csproj
    Program.cs
    appsettings.json
    appsettings.Development.json
    tests/
      Api.Tests.csproj
  Application/
    Application.csproj
    tests/
      Application.Tests.csproj
  Domain/
    Domain.csproj
    tests/
      Domain.Tests.csproj
  Infrastructure/
    Infrastructure.csproj
    tests/
      Infrastructure.Tests.csproj
```

All projects target `net10.0`. Shared compiler settings enable nullable
reference types and implicit usings. Test projects use xUnit and the standard
.NET test SDK.

Use a classic `.sln` file for broad IDE and command-line compatibility.

Project files use the concise names shown above. Production namespaces use
`UrlShortener.Api`, `UrlShortener.Application`, `UrlShortener.Domain`, and
`UrlShortener.Infrastructure` so source identifiers retain product context.

### Production project dependencies

```text
Domain
        ^
        |
Application
        ^
        |
Infrastructure
        ^
        |
Api
```

More precisely:

- Domain references no other production project.
- Application references Domain and the Mediary package.
- Infrastructure references Application and Domain and owns EF Core/Npgsql.
- API references Application and Infrastructure and acts as the composition
  root.
- API must not reference EF Core provider types directly.

Each test project references only its corresponding production project plus
the test libraries needed for that layer. The API test project additionally
uses `Microsoft.AspNetCore.Mvc.Testing` to host the application in memory.

## Interfaces and behavior

### Application request

Add a parameterless `HelloWorldQuery` implementing Mediary's query/request
contract with a `string` response. Add `HelloWorldQueryHandler` in Application;
its asynchronous handler returns exactly:

```text
Hello World!
```

Application exposes a dependency-injection registration method that registers
Mediary and discovers handlers from the Application assembly.

### HTTP endpoint

Add `HomeController` to the API project:

- annotate it as an API controller;
- bind it to the root route `/`;
- inject Mediary's request dispatcher;
- dispatch `HelloWorldQuery` from its `GET` action;
- return `200 OK` with a `text/plain` response body of `Hello World!`.

`Program.cs` registers controllers, Application services, and Infrastructure
services, then maps controllers. It exposes a public partial `Program` type so
the integration-test host can locate the application entry point.

No business logic is placed in the controller.

## Data and state

Infrastructure contains `UrlShortenerDbContext`, derived from EF Core
`DbContext`, and a service-registration method that:

- obtains the `PostgreSql` connection string from configuration;
- fails during startup with a clear configuration error when it is absent;
- registers the context with `UseNpgsql`.

The initial context intentionally has no entity mappings because the scaffold
does not yet define domain persistence. No migration is created.

`Api/appsettings.Development.json` contains explicit, disposable local
development settings for `dotnet run`, including the PostgreSQL connection
string. These values are not suitable for production and are documented as
development-only configuration. The local launch profile and Compose API
service both select the `Development` environment.

`docker-compose.development.yml` defines:

- `postgres`: an official PostgreSQL image, a named data volume, a healthcheck,
  and explicit disposable development credentials;
- `api`: a development build of the `Api` project, the PostgreSQL connection
  string addressed to the `postgres` service, supplied through the native .NET
  `ConnectionStrings__PostgreSql` environment-key convention, an HTTP host
  port, and a health-based dependency on PostgreSQL.

No `.env` file is introduced or required. The compose file must not contain a
real credential. A development Dockerfile may be added under
`src/url-shortener-api` if required to build the API service. The selected
container port and documented host port must be consistent.

## Error cases

- Missing `ConnectionStrings:PostgreSql` prevents application startup and
  identifies the missing configuration key.
- If PostgreSQL is unhealthy, Compose does not start the dependent API service.
- The root endpoint does not access the database, but all persistence services
  must still be resolvable from the configured service provider.
- No catch-all route is introduced; unmatched routes retain normal ASP.NET Core
  behavior.

## Test strategy

Bob implements the behavioral slice with red-green-refactor evidence:

1. An Application unit test first proves that dispatch/handling of
   `HelloWorldQuery` returns exactly `Hello World!`.
2. An API integration test first proves that `GET /` returns HTTP `200`,
   `text/plain`, and the exact body, exercising controller routing and Mediary
   wiring.
3. An Infrastructure unit test proves that valid configuration registers
   `UrlShortenerDbContext` with the Npgsql provider and that missing
   configuration produces the specified startup error.
4. Domain receives its correctly located test project but no meaningless
   placeholder test because this scaffold introduces no Domain behavior.

Tests must not require a running PostgreSQL instance. The Compose smoke check
validates the real container wiring separately.

## Validation commands

Run from `src/url-shortener-api` unless noted otherwise:

```bash
dotnet restore UrlShortener.sln
dotnet build UrlShortener.sln --no-restore
dotnet test UrlShortener.sln --no-build
docker compose -f docker-compose.development.yml config
docker compose -f docker-compose.development.yml up --build --detach
curl --fail --show-error http://localhost:<documented-port>/
docker compose -f docker-compose.development.yml down
```

The HTTP smoke check must return exactly `Hello World!`. Compose teardown must
preserve the named PostgreSQL data volume.

## Explicitly deferred

- All URL-shortener domain and API behavior from `Design.md`.
- Database entities, mappings, migrations, and repository implementations.
- Authentication and ownership enforcement.
- Redis and cache-aside behavior.
- Analytics, audit events, queues, and background services.
- OpenAPI customization beyond framework defaults.
- Frontend scaffolding.
- CI/CD and production container hardening.

## Open questions

None.

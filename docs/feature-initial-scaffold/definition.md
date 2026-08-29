# Task definition

## Problem

The repository contains the URL shortener challenge and design documentation,
but it does not yet contain an executable solution. A stable initial structure
is needed before implementing URL-shortening behavior.

## Outcome

Provide a .NET 10 solution under `src/url-shortener-api` with clear API,
Application, Domain, and Infrastructure boundaries. The API can be started in a
local development environment and exposes a minimal controller-based request
that passes through Mediary and returns `Hello World!`.

## In scope

- A .NET 10 solution containing one production project for each layer: `Api`,
  `Application`, `Domain`, and `Infrastructure`.
- A matching test project under the `tests` directory of each layer.
- Controller-based ASP.NET Core routing.
- Mediary dispatch from the API layer to an Application handler.
- Entity Framework Core configured with the Npgsql PostgreSQL provider.
- A development Docker Compose environment containing the API and PostgreSQL.
- A root endpoint that demonstrates that the layer wiring works.
- Proportional automated tests and local development instructions.

## Out of scope

- Short URL creation, resolution, update, or deletion.
- Domain entities or database schema for short URLs.
- Entity Framework migrations or seed data.
- Authentication or authorization.
- Redis, caching, analytics, background processing, or a frontend.
- Production deployment configuration.

## Acceptance criteria

1. The repository contains a .NET 10 solution rooted at
   `src/url-shortener-api`.
2. Each layer has `<Layer>.csproj` at its root and a corresponding
   `tests/<Layer>.Tests.csproj` test project.
3. Project references enforce the intended layered dependency direction and
   the complete solution builds successfully.
4. `GET /` is implemented by an ASP.NET Core controller, dispatches a Mediary
   request to an Application handler, and responds with HTTP `200` and the text
   `Hello World!`.
5. Infrastructure exposes Entity Framework Core/PostgreSQL registration and
   the API consumes it through composition-root wiring.
6. `docker-compose.development.yml` starts the API and a healthy PostgreSQL
   dependency using development-only configuration.
7. The relevant automated tests pass and include behavioral coverage of the
   root HTTP endpoint and its Application handler.
8. Local `dotnet run` configuration is discoverable in
   `appsettings.Development.json`, while Compose uses matching development-only
   settings without requiring an `.env` file.

## Constraints

- Use .NET 10 and C#.
- Keep all solution code below `src/url-shortener-api`.
- Use controllers rather than minimal API route handlers.
- Use Mediary, not MediatR, for request dispatch.
- Use Entity Framework Core with PostgreSQL through Npgsql.
- Keep the scaffold small and avoid placeholder abstractions or future feature
  implementation.
- Repository documentation, code, identifiers, comments, and tests are in
  English.
- Preserve the existing user changes to `Design.md`, `AGENTS.md`, and `docs`.

## Open questions

None.

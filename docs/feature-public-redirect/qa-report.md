# QA report

Verdict: Pass

## Requirement coverage

- API integration coverage verifies anonymous `GET /{shortCode}`, redirect following disabled, HTTP 302, and exact stored `Location`.
- Invalid, unknown, and deleted codes are exercised and their normalized complete Problem Details JSON bodies are compared after removing only nondeterministic `traceId`; all return 404 with the expected media type.
- Application tests verify query validation, exact short-code propagation through the repository double, destination resolution, missing result, and zero saves.
- The concrete PostgreSQL adapter test verifies exact-code lookup, excludes deleted rows, and observes `EntityState.Detached`, proving no tracking with Npgsql.
- Existing management tests remain green; root behavior remains covered by the existing API suite. No schema changes were introduced.

## Test quality

Tests are independent at Application/API boundaries and use a real PostgreSQL 17 container for adapter semantics. The Postgres test is opt-in by connection environment, but it was explicitly enabled and executed in this review. Cleanup is scoped to generated codes in a `finally` block. Redirect behavior does not follow or fetch destinations and no owner is involved.

## Commands and results

- `docker compose -f docker-compose.development.yml up -d postgres` — passed; started only PostgreSQL, retaining the named volume.
- `URL_SHORTENER_TEST_CONNECTION=<local development connection> dotnet test Infrastructure/tests/Infrastructure.Tests.csproj -m:1 --filter FullyQualifiedName~PostgresShortUrlRepositoryTests` — passed; 1/1 test, including migration, exact active lookup, deleted exclusion, and detached-state assertion.
- `docker compose -f docker-compose.development.yml stop postgres` — passed; service stopped without removing the named volume.
- `docker compose -f docker-compose.development.yml config` — passed.
- `dotnet build UrlShortener.sln --no-restore -m:1` — passed, 0 warnings, 0 errors.
- `dotnet test UrlShortener.sln --no-build -m:1` — passed: Api 7, Application 25, Domain 6, Infrastructure 5; 43 total, 0 failed.

## Findings

No blocking findings.

## Residual risks

The full live API-over-HTTP/PostgreSQL smoke path was not run; API contract is covered by the WebApplicationFactory integration tests and persistence semantics by the real Npgsql adapter test.

# QA report

Verdict: Fail

## Scope reviewed

Reviewed the approved definition and technical specification against the current production code at `2598b21`, the API test assembly, the complete solution test run, and the development Compose configuration. No implementation or test files were changed during this review.

## Verified behavior

- The obsolete root demo has been removed. The existing API integration test verifies that `GET /` returns `404 Not Found` while management endpoints remain protected.
- `/health/live` is mapped with an empty predicate. `/health/ready` selects tagged PostgreSQL and configured Redis checks, applies bounded timeouts, and writes a sorted response without exception details.
- PostgreSQL is treated as required and Redis as degraded/optional in the registrations.
- Structured completion logging uses EventId `1001`. The centralized unexpected-exception path uses EventId `1002` and has one API integration test proving a sanitized `500` response and a single error record.
- ASP.NET Core and runtime metrics plus conditional OTLP export are registered. Compose contains the standalone Aspire Dashboard and does not make the API depend on it.

## Blocking findings

1. The approved design requires at most one semantic activity at the API flow boundary and explicitly excludes tracing from Application, Domain, repositories, and other business or persistence classes. The implementation instead starts activities in controllers, all Application handlers, the `ShortUrl` Domain entity, EF and caching repositories, Redis operations, URL building, and short-code generation. A request can therefore create several custom spans and the lower layers now depend on OpenTelemetry.
2. The specified `FlowActivityMiddleware` does not exist. `FlowLoggingMiddleware` records the completion log but does not own a single flow activity, attach the specified operation/outcome attributes, or mark that activity as failed before rethrowing an unexpected exception.
3. The current API test assembly has eight tests. It has no focused tests for liveness filtering, readiness status mapping, PostgreSQL failure, Redis degradation, Redis omission, health cancellation, activity cardinality/propagation, safe flow attributes, middleware outcome logs, or OTLP startup behavior. The previous report referred to tests and helper types that are not present in the repository.
4. The acceptance criterion requiring the Aspire Dashboard to show a request trace with one semantic flow child and a correlated log is not supported by current automated evidence and was not revalidated live in this review.

## Commands and results

Run from `src/url-shortener-api` unless stated otherwise:

- `dotnet build UrlShortener.sln --no-restore -m:1` — passed, 0 warnings and 0 errors.
- `dotnet test UrlShortener.sln --no-build -m:1` — passed: API 8, Application 24, Domain 6, Infrastructure 37; 75 total, 0 failed.
- `URL_SHORTENER_TOKEN=validation-only docker compose -f docker-compose.development.yml config --quiet` — passed without rendering the resolved token.
- `git diff --check` — passed for the current uncommitted documentation changes.

The successful build and existing suite do not prove the missing observability acceptance criteria.

## Required before Pass

- Implement the single API-boundary flow activity described by the approved specification and remove custom tracing from Application, Domain, and Infrastructure.
- Add focused tests for health behavior, flow cardinality and propagation, structured outcomes, unexpected failures, exporter gating, and missing/unreachable OTLP infrastructure.
- Run an independent QA pass against the resulting code and replace this report only when the relevant checks and live diagnostic smoke pass.

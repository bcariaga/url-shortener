# QA report

Verdict: Pass

## Requirement coverage

- Root demo removal is verified by deleted `HomeController`, HelloWorld application types and old tests, plus `Root_returns_not_found`.
- Liveness uses an empty health-check predicate and returns 200 without dependency registrations being selected.
- Readiness uses `AddDbContextCheck`, tagged checks, configured positive timeout, deterministic sorted/sanitized JSON, and maps PostgreSQL failure to 503. Integration tests cover PostgreSQL unhealthy, Redis degraded (200), and Redis omitted when unconfigured.
- Redis probing applies `WaitAsync(cancellationToken)`; focused tests cover healthy, degraded, timeout, and caller cancellation.
- Flow middleware creates only the four API activities (`create`, `update`, `delete`, `resolve`), records allowed outcomes/public code only, verifies async `Activity.Current` propagation and one activity per flow, and marks/rethrows unexpected exceptions.
- Structured logs have stable EventIds and safe named properties. Tests cover successful outcomes, expected failures, sensitive-data exclusion, and exactly one centralized unexpected-error record with safe RFC7807 response.
- Application, Domain, and Infrastructure contain no new trace, metric, timer, or performance logic. Observability remains API-boundary-only except the pre-existing cache degradation logger.
- OpenTelemetry conditionally configures traces, ASP.NET Core/runtime metrics, and logs only when an OTLP endpoint exists. Startup tests cover absent and unreachable endpoint behavior.
- Compose includes the pinned standalone Aspire Dashboard, internal OTLP gRPC endpoint, anonymous local UI, and no API dependency on dashboard health/order.

## Test quality

The added tests exercise behavior at middleware, health-check, startup, and API
integration boundaries rather than merely compiling types. Readiness tests use
controlled registrations to prove status mapping and sanitization; activity
tests use an `ActivityListener`; logging tests use a capturing provider; startup
tests verify exporter gating. New hand-written files each contain one
top-level type and the solution builds without warnings.

## Commands and results

- `dotnet restore src/url-shortener-api/UrlShortener.sln` — passed.
- `dotnet build src/url-shortener-api/UrlShortener.sln --no-restore -m:1` — passed, 0 warnings, 0 errors.
- `dotnet test src/url-shortener-api/UrlShortener.sln --no-build -m:1` — passed, 88 tests (34 API, 24 Application, 6 Domain, 24 Infrastructure).
- `docker compose -f src/url-shortener-api/docker-compose.development.yml config` — passed.
- `git diff --check` — passed.
- Live Compose smoke (`up -d --build`) — passed. PostgreSQL and Redis became healthy; `GET /health/live` returned 200; `GET /health/ready` returned 200 with both `postgresql` and `redis` Healthy; dashboard UI returned 302 from `http://localhost:18888`. `docker compose down` completed without `--volumes`, preserving the PostgreSQL volume.

## Findings

No blocking findings remain. The previous Redis cancellation, EF Core check,
readiness mapping/coverage, structured logging, activity cardinality, startup
gating, and Compose smoke gaps are addressed and independently verified.

## Residual risks

The dashboard UI was validated by HTTP response and Compose transport/config;
its rendered trace/log/metric views were not visually inspected. Long-term
retention, alerting, and production/VPS observability remain intentionally out
of scope.

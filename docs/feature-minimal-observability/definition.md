# Task definition

## Problem

The API still exposes `GET /` as a `Hello World!` wiring check. That endpoint
does not prove that the required PostgreSQL dependency is available or report
the state of the optional Redis cache.

Operational behavior is also difficult to inspect. The API does not add a
semantic flow span for create, update, delete, or public redirect; unexpected
failures do not have an API-owned structured log contract; and there is no OTLP
export path for the diagnostics already produced by ASP.NET Core and .NET.
Dozzle can display container console output, but it cannot ingest OTLP
telemetry or reconstruct traces and spans.

## Outcome

Replace the root demo with distinct liveness and readiness endpoints, add one
API-boundary activity for each URL-shortener request flow, and optionally
export structured logs, traces, and framework/runtime metrics over OTLP.

Application, Domain, and business/persistence classes remain free of tracing,
metrics, timing, and performance-measurement logic. An activity started at the
API flow boundary propagates through the asynchronous call chain, so no span is
created in each method.

The development Compose environment includes a standalone Aspire Dashboard so
the telemetry can be inspected without adopting the rest of .NET Aspire.
Console logging remains enabled, so Dozzle continues to be useful alongside the
dashboard.

## In scope

- Remove the `GET /` Hello World endpoint and its obsolete Application types
  and tests.
- Add `GET /health/live` as a process-only liveness probe.
- Add `GET /health/ready` with real PostgreSQL and Redis checks.
- Treat PostgreSQL as required and Redis as optional in readiness semantics.
- Return a small, non-sensitive JSON health report.
- Add structured `ILogger` request-outcome events at the API boundary for
  create, update, delete, and redirect, plus the existing focused cache
  degradation log and one centralized unexpected-error log.
- Add one API-owned .NET `ActivitySource` activity for each create, update,
  delete, or redirect request flow, nested below the ASP.NET Core request
  activity and propagated through the complete asynchronous flow.
- Collect standard ASP.NET Core and .NET runtime metrics without adding custom
  metric logic to Application or other business classes.
- Export traces, metrics, and structured logs through OTLP when an endpoint is
  configured; keep the API functional when no collector is configured or the
  collector is unavailable.
- Add a standalone Aspire Dashboard service to the development Compose file
  and document how to inspect telemetry locally.
- Add focused automated coverage and a live Compose validation path.

## Out of scope

- Deploying or publicly exposing an observability backend on the user's VPS.
- Persistent telemetry storage, dashboards for long-term trends, alerting, or
  an on-call pipeline.
- Prometheus, Grafana, Loki, Tempo, Jaeger, or an OpenTelemetry Collector
  topology.
- Distributed tracing across services other than this API.
- Activities or spans in Application handlers, Domain entities, repositories,
  or every method in the call chain.
- Custom business counters, histograms, timers, or other performance
  instrumentation in Application, Domain, or Infrastructure.
- Authentication or login instrumentation. This API has bearer-token
  authentication but no login operation.
- Logging request bodies, destination URLs, bearer tokens, connection strings,
  owner identifiers, or exception details in health responses.
- High-cardinality metric labels such as short code, owner, URL, route path, or
  exception message.
- Changing the existing API, authentication, cache, or persistence behavior.

## Acceptance criteria

1. `GET /` no longer returns `Hello World!` and has no replacement demo
   behavior.
2. `GET /health/live` returns `200 OK` while the ASP.NET Core process can serve
   requests and does not query PostgreSQL or Redis.
3. `GET /health/ready` performs bounded dependency checks and returns a JSON
   report containing the overall status and individual dependency statuses,
   without secrets or raw exception details.
4. Readiness returns `503 Service Unavailable` when PostgreSQL cannot be
   reached. Redis unavailability is reported as `Degraded` but does not make
   the API unready because Redis is an optional best-effort cache.
5. Each create, update, delete, and redirect request has at most one semantic
   child activity created at the API boundary. It covers the complete flow and
   no handler, Domain type, or repository starts another flow span.
6. Successful and not-found HTTP outcomes for those operations produce stable
   structured API-boundary logs; standard ASP.NET Core metrics expose request
   count, duration, route, and status without a custom business counter.
7. Unexpected request failures produce one API-owned structured error log,
   mark the relevant activity as failed, and return safe Problem Details
   without leaking implementation or credential data.
8. Telemetry never records bearer tokens, owner identifiers, destination URLs,
   connection strings, request bodies, or exception messages as custom
   attributes. A public short code may appear in structured logs and trace
   attributes.
9. Standard ASP.NET Core request traces and metrics, .NET runtime metrics, the
   API flow activities, and structured logs are exported over OTLP only when an
   OTLP endpoint is configured.
10. Missing or unavailable OTLP infrastructure never prevents startup or changes
   an API response. Existing console logs remain available to Docker and
   Dozzle.
11. Development Compose starts a standalone Aspire Dashboard that receives the
    API's OTLP telemetry and shows at least one request trace with its single
    semantic flow child, correlated structured log, and standard metrics.
12. Automated tests cover health filtering/status mapping, activity
    propagation and cardinality, structured API logs, error handling, and
    removal of the root demo; the solution builds and all relevant tests pass.

## Constraints

- Keep the implementation proportional to a system-design prototype.
- Use `ActivitySource` only at the API flow boundary. `Activity.Current`
  propagation is the context mechanism; do not pass telemetry through business
  method signatures.
- Application, Domain, and Infrastructure contain no new tracing, custom
  metrics, timers, stopwatches, or performance-measurement logic.
- Use stable, low-cardinality activity names and attributes.
- Health checks have their own short timeout and must honor request
  cancellation.
- The Aspire Dashboard is a development and short-term diagnostic tool. Its
  in-memory telemetry is not treated as production monitoring.
- Repository documentation, code, identifiers, comments, and tests remain in
  English.

## Open questions

None.

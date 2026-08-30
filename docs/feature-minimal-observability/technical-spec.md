# Technical specification

## Context

ASP.NET Core already creates a server `Activity` for each request and exposes
standard request measurements when OpenTelemetry ASP.NET Core instrumentation
is enabled. In .NET, an `Activity` represents a span. `Activity.Current` uses
the execution context and propagates automatically through normal `async` and
`await` calls, so one activity started at the API flow boundary covers the
controller, dispatcher, handler, repository, PostgreSQL, and Redis call chain.

Starting an activity in every method would not improve propagation. It would
create nested spans that repeat the same logical work and couple business code
to diagnostics. Custom child spans are justified only for a distinct operation
whose boundary is useful on its own; none is required inside the current
Application, Domain, or Infrastructure flow.

PostgreSQL is required for correct behavior. Redis is explicitly an optional,
best-effort accelerator. The health contract must preserve that availability
boundary.

The chosen local viewer is the standalone Aspire Dashboard. It accepts OTLP
from any OpenTelemetry-enabled application and displays structured logs,
traces, and metrics. It is lighter and more proportional here than introducing
a persistent Grafana/Loki/Tempo stack. Dozzle remains complementary: it reads
container console output but is not a trace backend.

Relevant references:

- [ASP.NET Core health checks and separate readiness/liveness probes](https://learn.microsoft.com/en-us/aspnet/core/host-and-deploy/health-checks?view=aspnetcore-10.0)
- [OpenTelemetry .NET](https://opentelemetry.io/docs/languages/dotnet/)
- [.NET distributed tracing concepts and automatic Activity propagation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-concepts)
- [Manual .NET instrumentation with ActivitySource](https://opentelemetry.io/docs/languages/dotnet/instrumentation/)
- [Standalone Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/aspire/fundamentals/dashboard/standalone)
- [OTLP with the standalone Aspire Dashboard](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/observability-otlp-example)

## Proposed solution

### Remove the demo endpoint

Delete `HomeController`, `HelloWorldQuery`, `HelloWorldQueryHandler`, and their
dedicated tests. Do not map a replacement endpoint at `/`; an unmatched root
request returns the framework's normal `404`.

### Health checks

Register ASP.NET Core health checks and map:

- `/health/live`: use a predicate that selects no dependency checks. A response
  proves only that the process and HTTP pipeline are alive.
- `/health/ready`: select checks tagged `ready`.

Register these readiness checks:

1. `postgresql`: call EF Core's asynchronous connectivity check through the
   Microsoft EF Core health-check integration. Tag it `ready`; failure status is
   `Unhealthy`.
2. `redis`: when `ConnectionStrings:Redis` is configured, perform a bounded
   asynchronous `PING` through the registered connection multiplexer. Tag it
   `ready`; failure status is `Degraded`. When Redis is intentionally not
   configured, omit the check because PostgreSQL-only operation is supported.

Use a dedicated configurable health timeout with a default of two seconds per
dependency. It must be positive and must not reuse the cache's 100 ms request
budget: a readiness probe diagnoses a dependency, while a normal request must
fall back quickly.

Both endpoints return `application/json` with this stable shape:

```json
{
  "status": "Healthy",
  "checks": [
    {
      "name": "postgresql",
      "status": "Healthy",
      "durationMilliseconds": 12.3
    }
  ]
}
```

Sort checks by name for deterministic output. Do not serialize health-check
descriptions, exception types/messages, stack traces, connection details, or
arbitrary health data.

Map overall `Healthy` and `Degraded` to HTTP `200`; map `Unhealthy` to HTTP
`503`. Consequently a Redis outage is visible in the body without asking an
orchestrator to remove a still-functional instance, while a PostgreSQL outage
makes it unready.

### Flow activities and propagation

Add one API-owned `ActivitySource` named `UrlShortener.Api.Flows`. A reusable
API middleware starts at most one internal activity after routing for each
matched business endpoint. It wraps authentication, controller dispatch, the
Application handler, and downstream repositories. Activity names are:

- `short_url.create`
- `short_url.update`
- `short_url.delete`
- `short_url.resolve`

The middleware uses endpoint metadata and normalized route values, not raw URL
matching, to select the activity. It records the fixed `operation` value and,
after the response, an `outcome` derived from the HTTP contract. Allowed
outcomes are `success`, `not_found`, `validation_error`, `unauthorized`,
`capacity_exhausted`, and `error`.

The activity may include a route-supplied `short_url.code` because the code is
a public resource identifier. The create flow does not inspect or buffer its
response merely to obtain the generated code. Activities must not include owner
IDs, destination URLs, bearer tokens, connection strings, request bodies,
query strings, or exception messages. On an unexpected exception, set the
activity status to `Error`, attach only the exception type as an event
attribute, and rethrow to the centralized exception handler.

Do not inject `ActivitySource`, `Activity`, OpenTelemetry types, meters,
stopwatches, timers, or telemetry collaborators into controllers, Application
handlers, Domain types, repositories, or method signatures. Downstream code is
part of the span because `Activity.Current` propagates automatically through
the asynchronous execution context.

Do not manually add database or Redis spans in this task. If their client
libraries expose supported `ActivitySource` instrumentation in the future,
OpenTelemetry may subscribe to those sources without adding observability logic
to repository methods.

### Metrics

Collect only the standard ASP.NET Core request metrics and .NET runtime
metrics. The request instruments already provide count and duration with
normalized route, method, and status dimensions, which are sufficient for the
requested basic metrics.

Do not create an Application meter, operation counter, custom histogram,
stopwatch, or performance timer. Create/update/delete/redirect breakdowns are
available from normalized HTTP routes and status codes in the standard server
metrics.

### `ILogger` placement and event contract

Use `ILogger` where it adds an operational event that traces or metrics alone
cannot explain:

- The API flow middleware logs completed create, update, delete, and redirect
  outcomes at `Information` using endpoint metadata and the final status code.
  The OpenTelemetry logging provider correlates the record with
  `Activity.Current`; do not manually duplicate trace or span identifiers as
  message properties. Handlers and controllers do not log or know about this
  concern.
- The cache repository keeps `Warning` logs for degraded Redis operations, but
  passes the exception object to `ILogger` instead of logging only
  `Exception.Message`, preserving diagnostic detail for sinks while keeping
  message-template properties safe.
- One API-level exception handler logs unexpected uncaught failures at `Error`
  and returns safe RFC 7807 Problem Details with status `500` and the current
  trace ID.
- Code-capacity exhaustion remains an expected `503` outcome and is logged at
  `Warning`, not as an unexpected error.

Do not add explicit logs for request start/end, health success on every probe,
controller entry, validation failures, or bearer authentication failures.
ASP.NET Core diagnostics already cover the HTTP request, and per-probe success
logs would create noise. Application handlers and Domain entities remain free
of `ILogger` added for this feature and all observability dependencies.

Use static message templates with named properties and stable `EventId` values.
Logs may contain `ShortCode`, `Operation`, and `Outcome`. They must not contain
the owner ID, long/destination URL, token, connection string, request body, or a
rendered exception message as a custom property. Preserve the normal console
provider so Docker and Dozzle continue to receive logs.

### OpenTelemetry collection and export

Configure OpenTelemetry in API startup with resource service name
`url-shortener-api` and collect:

- traces from ASP.NET Core and `UrlShortener.Api.Flows`;
- metrics from ASP.NET Core and the .NET runtime;
- `ILogger` records through the OpenTelemetry logging provider,
  including scopes and formatted messages.

Use the stable OpenTelemetry hosting, ASP.NET Core instrumentation, runtime
instrumentation, and OTLP exporter packages with one mutually compatible pinned
version set. Do not introduce automatic-instrumentation agents or a direct
vendor SDK.

Enable OTLP exporters only when `OTEL_EXPORTER_OTLP_ENDPOINT` is non-empty. The
standard `OTEL_EXPORTER_OTLP_PROTOCOL` setting selects gRPC or HTTP/protobuf.
Exporter retries or outages run out of band and must never change request,
health, or startup behavior. No endpoint, token, or header is stored in source
configuration.

The ASP.NET Core trace instrumentation excludes `/health/live` and
`/health/ready` so probes do not dominate traces. Standard ASP.NET Core metrics
may include the normalized health routes: the stable instrumentation does not
provide a per-path metric filter, and adding a custom filtering pipeline would
conflict with the requirement to avoid custom metric and performance logic.

### Development Compose viewer

Add one standalone `aspire-dashboard` service using a pinned Microsoft
dashboard image. Expose its UI on host port `18888`; keep its OTLP receiver on
the Compose network. Configure anonymous dashboard access only in this local
development file.

Set the API's Compose-only exporter endpoint to the dashboard's internal OTLP
gRPC address and protocol. The API must not depend on dashboard health or
startup order. Document:

- dashboard UI access at `http://localhost:18888`;
- that the dashboard is local, anonymous, in-memory, and non-production;
- how to set an OTLP endpoint when running the API directly;
- that Dozzle continues to show console logs but not traces/transactions.

Do not add the dashboard to a production/VPS deployment. A future VPS design
must first decide authentication, private network exposure, retention,
resource limits, TLS, and backups. For long-term monitoring, a persistent OTLP
backend or collector is a separate task.

## Interfaces and behavior

### HTTP

| Endpoint | Dependency checks | HTTP status |
| --- | --- | --- |
| `GET /health/live` | None | `200` while the process serves requests |
| `GET /health/ready` | PostgreSQL and configured Redis | `200` for `Healthy`/`Degraded`; `503` for `Unhealthy` |
| `GET /` | None; no route | `404` |

Existing management and redirect endpoints retain their status codes, bodies,
authentication, ownership, and validation behavior.

### Configuration

Add only the non-secret health policy setting to checked-in development
configuration:

```json
{
  "HealthChecks": {
    "TimeoutSeconds": 2
  }
}
```

OTLP uses standard environment variables and is absent from checked-in
`appsettings` files. Compose supplies its internal development value.

## Data and state

No PostgreSQL migration, Redis key, or domain-state change is required.
Telemetry is diagnostic and best effort. The standalone dashboard retains it
in memory and may evict it at limits or lose it on restart.

There is no custom metric state. Standard request metrics aggregate by
normalized HTTP dimensions. Logs and sampled traces may carry a public short
code for diagnosis, but custom telemetry never stores owner or destination
data.

## Error cases

- PostgreSQL health check fails or times out: readiness is `Unhealthy` and
  returns `503`; liveness remains `200`.
- Redis health check fails or times out: readiness is `Degraded` and returns
  `200`; normal API cache fallback remains unchanged.
- Redis is not configured: omit its check and evaluate readiness from required
  dependencies only.
- Caller cancels a health request: propagate request cancellation; do not turn
  it into a stale health result.
- OTLP endpoint is absent: no OTLP exporter is created; console logging and API
  behavior continue normally.
- Dashboard or OTLP receiver is down: export may be dropped/retried by the SDK,
  but requests and health results are unchanged.
- An operation throws unexpectedly: the API flow activity records `error`; the
  centralized handler logs once and returns safe `500` Problem Details.
- Expected validation, authentication, ownership concealment, not-found, and
  capacity outcomes preserve their current HTTP contracts.

## Test strategy

Bob works in red-green-refactor slices and retains focused tests proving:

1. The root endpoint returns `404`, and obsolete Hello World production/test
   types are removed.
2. Liveness returns `200` without invoking registered dependency checks.
3. Healthy PostgreSQL and Redis produce a deterministic `Healthy` JSON report.
4. PostgreSQL failure produces `Unhealthy` and `503` without leaking exception
   details.
5. Redis failure produces `Degraded` and `200`; missing Redis configuration
   omits the Redis entry.
6. Health checks honor the configured timeout and request cancellation.
7. The API flow middleware emits exactly one semantic child activity and one
   structured completion log for each create/update/delete/redirect
   success/not-found path.
8. An `ActivityListener` proves that the activity is current inside a test
   handler/repository reached through `async` calls, without those classes
   starting activities or receiving telemetry dependencies.
9. Unexpected handler failures mark the API flow activity as `error`, are
   rethrown, and result in one safe API `500` response/log.
10. Telemetry tests assert that forbidden owner, destination, token,
   connection, body, query, and exception-message values are absent from custom
   activity tags and structured log state.
11. Health endpoints are filtered from ASP.NET Core traces; standard request
    metrics may retain their normalized health-route measurements.
12. Startup succeeds without an OTLP endpoint and while a configured endpoint
    is unreachable.
13. Existing Domain, Application, Infrastructure, API, PostgreSQL, Redis,
    authentication, ownership, and redirect tests remain green.

Prefer an in-process `ActivityListener` and a test `ILogger` provider for
deterministic signal assertions. Automated tests must not require the Aspire
Dashboard or network access. Do not add instrumentation to a test handler or
repository merely to prove propagation; inspect `Activity.Current` from the
test double.

## Validation commands

Run serially from `src/url-shortener-api`:

```bash
dotnet restore UrlShortener.sln
dotnet build UrlShortener.sln --no-restore -m:1
dotnet test UrlShortener.sln --no-build -m:1
docker compose -f docker-compose.development.yml config
```

For live validation, preserve the PostgreSQL volume and:

1. start PostgreSQL, Redis, the API, and the Aspire Dashboard;
2. verify `/health/live` is exact HTTP `200`;
3. verify `/health/ready` reports both dependencies healthy with HTTP `200`;
4. execute create, update, redirect, delete, not-found, and one controlled error
   path without printing credentials;
5. verify the Aspire Dashboard displays a request trace with exactly one
   semantic flow child activity, its correlated structured log, and standard
   request/runtime metrics;
6. stop Redis and verify readiness reports `Degraded` with HTTP `200` while
   PostgreSQL-backed API behavior still works;
7. stop PostgreSQL and verify readiness reports `Unhealthy` with HTTP `503`
   while liveness stays `200`;
8. stop the dashboard and verify API behavior and health responses are
   unchanged;
9. tear Compose down without `--volumes`.

## Explicitly deferred

- Production/VPS dashboard deployment and ingress.
- Persistent observability storage, retention, alerts, and SLOs.
- An OpenTelemetry Collector gateway and vendor-specific exporters.
- Grafana, Loki, Tempo, Prometheus, Jaeger, or cloud telemetry services.
- Per-method spans and any manual Activity/OpenTelemetry logic in Application,
  Domain, or Infrastructure.
- Custom business counters, histograms, timers, and performance measurements.
- Database-statement and Redis-command spans.
- Trace sampling policy beyond the SDK default suitable for local development.
- Authentication/login activities; the current API has no login flow.

## Open questions

None.

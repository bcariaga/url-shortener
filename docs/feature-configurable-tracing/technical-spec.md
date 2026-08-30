# Technical specification

## Context

The API currently registers one OpenTelemetry builder containing tracing and
metrics. When an OTLP endpoint exists, it adds log, trace, and metric exporters.
`FlowActivityMiddleware` always calls its `ActivitySource` and also owns the
structured flow-completion log.

Disabling the middleware would incorrectly remove structured logs. The
configuration must therefore gate activity creation and tracing registration,
while leaving the middleware, logs, metrics, health checks, and error handling
in place.

## Proposed solution

### Configuration

Add one API-owned options type with this contract:

```csharp
public sealed class ObservabilityOptions
{
    public const string SectionName = "Observability";

    public bool TracingEnabled { get; init; } = true;
}
```

Bind the `Observability` section and validate it at startup. An absent section
uses the `true` default. A non-boolean `TracingEnabled` value is a startup
configuration error.

Add the explicit development default:

```json
{
  "Observability": {
    "TracingEnabled": true
  }
}
```

Container and VPS-style configuration may override it with:

```text
Observability__TracingEnabled=false
```

Do not add this override to development Compose; traces remain visible in the
local Aspire Dashboard by default.

### OpenTelemetry registration

Retain one OpenTelemetry builder and always configure:

- resource service name `url-shortener-api`;
- standard ASP.NET Core metrics;
- .NET runtime metrics.

Only when `TracingEnabled` is `true`, add:

- ASP.NET Core trace instrumentation with the existing health-route filter;
- source `UrlShortener.Api.Flows`.

When `OTEL_EXPORTER_OTLP_ENDPOINT` is configured:

- always register the OpenTelemetry logging provider and OTLP log exporter;
- always register the OTLP metric exporter;
- register the OTLP trace exporter only when `TracingEnabled` is `true`.

When tracing is disabled, the application must not construct a tracer provider
merely to leave it without an exporter. Logs and metrics continue through their
own providers.

### Flow middleware

Keep `FlowActivityMiddleware` in the request pipeline so the structured
operation log remains unchanged. Inject the validated observability options at
the API boundary.

For a recognized create, update, delete, or redirect flow:

1. If tracing is enabled, start the existing semantic activity and retain all
   current tags, status, exception event, and propagation behavior.
2. If tracing is disabled, do not call `ActivitySource.StartActivity` and do not
   create an activity, regardless of external listeners.
3. In both modes, invoke the downstream pipeline and emit the same successful
   structured completion log.
4. In both modes, rethrow unexpected exceptions so the centralized exception
   handler emits the single error log and safe Problem Details response.

Do not pass the tracing setting into controllers, Application handlers, Domain
types, repositories, or method signatures.

## Interfaces and behavior

| Configuration | Flow activity | ASP.NET tracing | OTLP traces | Logs | Metrics | Health |
| --- | --- | --- | --- | --- | --- | --- |
| absent | enabled | enabled | if endpoint configured | unchanged | unchanged | unchanged |
| `TracingEnabled=true` | enabled | enabled | if endpoint configured | unchanged | unchanged | unchanged |
| `TracingEnabled=false` | disabled | disabled | disabled | unchanged | unchanged | unchanged |

Changing the setting requires an API restart. No endpoint or response contract
changes.

## Data and state

No PostgreSQL, Redis, Domain, or persisted-state change is required. The
setting is non-secret configuration.

Disabling tracing stops new custom flow activities and trace export after the
configured process starts. It does not delete telemetry previously retained by
the Aspire Dashboard or another backend.

## Error cases

- Setting absent: use `TracingEnabled=true`.
- Setting explicitly `true`: preserve the existing trace behavior.
- Setting explicitly `false`: skip trace creation/collection/export while
  preserving logs, metrics, and health checks.
- Invalid boolean value: fail startup with a clear configuration error.
- OTLP endpoint absent: no exporters are configured, independent of the trace
  switch.
- OTLP endpoint unavailable: existing best-effort log/metric/trace exporter
  behavior remains non-blocking for enabled signals.

## Test strategy

Bob works in red-green-refactor slices and adds focused tests proving:

1. Missing observability configuration resolves to tracing enabled.
2. Explicit `TracingEnabled=true` preserves the existing single flow activity,
   tags, outcomes, propagation, and log event.
3. Explicit `TracingEnabled=false`, with an `ActivityListener` subscribed,
   creates zero `UrlShortener.Api.Flows` activities while still executing the
   request and emitting exactly one EventId `1001` completion log.
4. Unexpected failures with tracing disabled still produce no flow activity,
   no middleware completion/error log, and exactly one centralized EventId
   `1002` error log with safe Problem Details.
5. Startup registration with tracing disabled contains metric and log export
   behavior but no application-configured trace provider/exporter.
6. Liveness and readiness behavior is unchanged when tracing is disabled.
7. An invalid boolean setting fails startup.
8. Existing activity, readiness, logging, OTLP, API, Application, Domain,
   Infrastructure, PostgreSQL, and Redis tests remain green.

Use the existing `ActivityListener`, capturing logger/provider, startup factory,
and API test infrastructure rather than adding duplicate test frameworks.

## Validation commands

Run serially from `src/url-shortener-api`:

```bash
dotnet restore UrlShortener.sln
dotnet build UrlShortener.sln --no-restore -m:1
dotnet test UrlShortener.sln --no-build -m:1
docker compose -f docker-compose.development.yml config
```

For a proportional live smoke:

1. run the API with `Observability__TracingEnabled=false` and a configured OTLP
   endpoint;
2. execute liveness plus one create or redirect request;
3. verify responses, structured logs, and metrics remain available;
4. verify no new custom flow trace is received;
5. restart with tracing enabled and verify the flow trace appears again.

## Explicitly deferred

- A global observability switch.
- Independent log-export or metric-export switches.
- Runtime configuration reload and live tracer-provider replacement.
- Sampling controls and per-operation trace switches.
- Production/VPS deployment changes.

## Open questions

None.

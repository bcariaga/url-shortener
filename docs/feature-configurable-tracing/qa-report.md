# QA report

Verdict: Fail

## Scope reviewed

Reviewed the approved definition and technical specification against the current production code at `2598b21`, including options binding, OpenTelemetry registration, every custom `ActivitySource` call site, the API tests, and the complete solution test run. No implementation or test files were changed during this review.

## Verified behavior

- `ObservabilityOptions.TracingEnabled` defaults to `true`, development configuration sets it explicitly, and standard .NET configuration supports the `Observability__TracingEnabled` environment form.
- Startup conditionally registers the OpenTelemetry trace provider and trace exporter when tracing is enabled. Metrics, structured logging, health endpoints, authentication, and business routes remain registered independently.
- Centralized exception handling remains active and has integration coverage for a sanitized unexpected `500` response.

## Blocking findings

1. `TracingEnabled=false` prevents registration of the application-configured trace provider, but it does not prevent custom activity creation. Controllers, handlers, the Domain entity, and Infrastructure continue calling `ActivitySource.StartActivity`. An external `ActivityListener` can therefore observe those activities, violating the explicit zero-custom-activity requirement for disabled mode.
2. The `FlowActivityMiddleware` described by the specification and previous report does not exist. The current `FlowLoggingMiddleware` has no tracing switch and does not create or suppress a semantic flow activity.
3. There are no tests for absent, enabled, disabled, or invalid `TracingEnabled` configuration; no external-listener test for disabled mode; no startup-provider/exporter assertions; and no disabled-mode health or unexpected-failure coverage. `Api/tests/AssemblyInfo.cs`, the startup factory, and the activity-listener helpers cited by the previous report are absent.
4. The feature inherits the unresolved layering and activity-cardinality failures from `feature-minimal-observability`; tracing is not API-boundary-only in the current implementation.

## Commands and results

Run from `src/url-shortener-api` unless stated otherwise:

- `dotnet build UrlShortener.sln --no-restore -m:1` — passed, 0 warnings and 0 errors.
- `dotnet test UrlShortener.sln --no-build -m:1` — passed: API 8, Application 24, Domain 6, Infrastructure 37; 75 total, 0 failed.
- `URL_SHORTENER_TOKEN=validation-only docker compose -f docker-compose.development.yml config --quiet` — passed without rendering the resolved token.
- `git diff --check` — passed for the current uncommitted documentation changes.

The successful suite contains no focused configurable-tracing coverage and cannot support a `Pass` verdict for this task.

## Required before Pass

- Make the tracing switch control the single API-boundary activity creation path, including when an external listener is subscribed.
- Remove custom activity creation from Application, Domain, and Infrastructure as required by the parent observability specification.
- Add enabled/disabled/default/invalid configuration tests, provider and exporter registration tests, disabled-mode exception and health tests, and an independent QA rerun.

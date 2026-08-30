# Task definition

## Problem

The observability implementation always registers OpenTelemetry tracing and
attempts to create a semantic flow activity for each URL-shortener operation.
Although `ActivitySource` is inexpensive when it has no listeners, an operator
needs an explicit way to avoid trace sampling, processing, and export when
traces are not required.

Logging must remain independent because its volume is already controlled with
the standard `Logging:LogLevel` configuration. Health checks and basic metrics
must also remain available when tracing is disabled.

## Outcome

Add an API configuration switch named `Observability:TracingEnabled`, enabled
by default. When set to `false`, the API does not register the OpenTelemetry
tracing pipeline, does not configure an OTLP trace exporter, and does not create
custom URL-shortener flow activities.

Structured flow/error logging, standard ASP.NET Core and runtime metrics, OTLP
log/metric export, liveness, and readiness continue to work unchanged.

## In scope

- Add the boolean `Observability:TracingEnabled` setting with default `true`.
- Keep the current tracing behavior when the setting is `true`.
- Skip ASP.NET Core trace instrumentation, the custom flow `ActivitySource`
  subscription, and the OTLP trace exporter when the setting is `false`.
- Prevent `FlowActivityMiddleware` from starting a custom activity while
  tracing is disabled, even if another process-local listener subscribes to its
  `ActivitySource`.
- Keep the middleware's structured completion logs active in both modes.
- Keep OpenTelemetry logs and metrics independently controlled by the existing
  OTLP endpoint configuration.
- Document application configuration and the corresponding environment
  variable.
- Add focused automated coverage and preserve the complete existing suite.

## Out of scope

- A master switch that disables logs, metrics, health checks, or all
  observability.
- A separate configuration switch for metrics or OpenTelemetry log export.
- Dynamic runtime toggling or configuration reload without restarting the API.
- Changing log levels, log event contracts, health behavior, activity names,
  sampling policy, or OTLP endpoints.
- Removing OpenTelemetry packages from the application.
- Changes to Application, Domain, or Infrastructure business behavior.

## Acceptance criteria

1. With no explicit setting, tracing remains enabled and existing flow
   activities and trace export configuration behave as before.
2. With `Observability:TracingEnabled=true`, the API registers ASP.NET Core and
   custom flow tracing and conditionally exports traces when
   `OTEL_EXPORTER_OTLP_ENDPOINT` is configured.
3. With `Observability:TracingEnabled=false`, no custom flow activity is
   created, even when an `ActivityListener` is subscribed.
4. With tracing disabled, no OpenTelemetry tracing provider or OTLP trace
   exporter is configured by the API.
5. With tracing disabled, structured create/update/delete/redirect logs retain
   EventId `1001`, their existing safe properties, and log-level filtering.
6. With tracing disabled, the centralized error log, safe Problem Details,
   standard ASP.NET Core/runtime metrics, OTLP log/metric exporters, liveness,
   and readiness remain functional.
7. The setting can be supplied as `Observability:TracingEnabled` in application
   configuration or `Observability__TracingEnabled` in the environment.
8. Invalid boolean configuration fails startup clearly rather than silently
   selecting a mode.
9. Automated tests cover the default, explicitly enabled, disabled, and invalid
   configurations; the full solution builds and all relevant tests pass.

## Constraints

- The switch controls tracing only. `Logging:LogLevel` remains the source of
  truth for log filtering.
- No trace, metric, timer, or performance logic is added to Application,
  Domain, or Infrastructure.
- Avoid creating parallel middleware or duplicating the flow classification
  and logging contract.
- Checked-in development configuration keeps tracing enabled so the local
  Aspire Dashboard experience continues to work by default.
- Repository documentation, code, identifiers, comments, and tests remain in
  English.

## Open questions

None.

# Technical specification

## Context

The request pipeline currently configures `UseExceptionHandler` with an inline
delegate in `Program.cs`. That delegate safely handles every uncaught exception
as a 500. `ShortUrlsController.Create` separately catches
`ShortCodeAttemptsExhaustedException` and returns 503. Domain entity guards use
generic BCL exception types, so a centralized API handler cannot distinguish
their semantic category without relying on messages.

The cache-aside repository already opens an Infrastructure activity and safely
falls back to PostgreSQL for a miss, malformed value, private timeout, or cache
error. The behavior is correct, but those paths are indistinguishable in a
trace.

## Proposed solution

### Exception-handler chain

Use the built-in ASP.NET Core `IExceptionHandler` chain as the strategy
mechanism. Do not add a project-specific Strategy interface or dictionary of
delegates.

Register, in this order:

1. `KnownExceptionHandler`, responsible only for explicitly supported
   Application and Domain exceptions;
2. `UnexpectedExceptionHandler`, the final fallback for every other exception;
3. the framework Problem Details services needed by the handlers.

`Program.cs` activates the configured pipeline with `app.UseExceptionHandler()`
and contains no exception-specific delegate. Registration belongs in API
composition code.

Each handler uses `IProblemDetailsService` to write RFC 7807 responses and adds
the current distributed trace identifier as `traceId`. Application and Domain
remain unaware of HTTP.

### Typed exceptions

Application keeps the existing concrete
`ShortCodeAttemptsExhaustedException`, but gives it a safe message and stable
code `short_code_capacity_exhausted`.

Domain adds:

- `DomainException`, an abstract base carrying a stable `Code`;
- `RequiredShortUrlValueException`, carrying the safe logical field name for a
  required `shortCode`, `url`, or `owner` value;
- `InvalidShortUrlStateException`, carrying the attempted `update` or `delete`
  operation when the entity is already deleted.

`ShortUrl.Create`, `ShortUrl.Update`, and `ShortUrl.Delete` throw these concrete
Domain exceptions instead of `ArgumentException` and
`InvalidOperationException`. Exception properties use logical contract names,
not raw input values.

`ShortCodeConflictException` remains the semantic repository-port exception
used by Application to retry generated-code collisions. Infrastructure already
translates only PostgreSQL unique violations for `short_code` into this type.
It is not exposed directly by the API because the Application retry policy
either succeeds or raises `ShortCodeAttemptsExhaustedException`.

Do not create custom exceptions for Redis miss, Redis degradation, PostgreSQL
connectivity, timeouts, or arbitrary provider errors. A cache miss is not an
error, cache degradation is recovered locally, and unexpected database errors
must not become public client details.

### HTTP mapping

`KnownExceptionHandler` uses an exhaustive type switch:

| Exception | Status | Title | Code |
| --- | --- | --- | --- |
| `ShortCodeAttemptsExhaustedException` | `503` | `Short URL capacity temporarily unavailable.` | `short_code_capacity_exhausted` |
| `RequiredShortUrlValueException` | `400` | `Invalid short URL data.` | exception code |
| `InvalidShortUrlStateException` | `409` | `Short URL state conflict.` | exception code |

The response `detail` comes only from the safe message defined by the known
exception. It never copies arbitrary inner-exception or provider text. The
handler adds `code` and `traceId` extensions and marks the exception handled.

The capacity response preserves the current 503 behavior. The create
controller no longer catches it. Known capacity exhaustion is not logged as an
unexpected EventId `1002` error; the final HTTP outcome remains observable
through the existing request/flow diagnostics.

`UnexpectedExceptionHandler` preserves the existing contract:

- status `500`;
- title `An unexpected error occurred.`;
- no `detail`, known-error `code`, exception message, or inner-exception data;
- current `traceId` extension;
- exactly one `Error` record with EventId `1002` and the exception object for
  server-side diagnosis.

### Controller behavior

Remove only the capacity-exhaustion `try/catch` from `Create`. Keep FluentValidation
execution and response construction in the controllers:

- invalid create/update input remains 400 validation Problem Details;
- invalid route codes remain 404;
- unknown, deleted, and foreign-owner resources remain indistinguishable 404s;
- redirect invalid/not-found behavior remains 404.

Handlers continue returning nullable/bool results for not-found cases. They do
not throw `NotFoundException` or validation exceptions.

### Infrastructure cache trace events

Add events to the existing `CachingShortUrlRepository` activity; do not create
additional spans. Event names are stable lowercase dot-separated values:

| Condition | Activity event |
| --- | --- |
| Redis contains a value | `cache.hit` |
| Redis returns no value | `cache.miss` |
| Redis value exists but is not an absolute HTTP(S) destination | `cache.invalid_value` |
| Private read deadline expires | `cache.read.timeout` |
| Read throws and PostgreSQL fallback is used | `cache.read.error` |
| Private set/remove deadline expires | `cache.write.timeout` |
| Set/remove throws and the database result is retained | `cache.write.error` |

Read events are mutually accurate: a timeout or error must not additionally be
reported as a miss. A Redis value may produce `cache.hit` followed by
`cache.invalid_value`, because Redis contained an unusable entry and the
repository then falls back.

Error events attach only `exception.type` with the runtime type's full name.
Events do not attach the cache key, short code, destination, cached value,
owner, token, connection information, exception message, or stack trace.

Use the already-current Infrastructure activity created by the repository.
When no activity is available because tracing is disabled or unsampled, event
calls are no-ops. Preserve the existing Warning log for recovered cache
exceptions and preserve caller cancellation propagation. Private cache
timeouts remain recovered without becoming warnings or HTTP errors.

## Interfaces and behavior

Existing successful HTTP contracts and controller-produced 400/404 contracts
do not change. The known-exception response shape is:

```json
{
  "status": 503,
  "title": "Short URL capacity temporarily unavailable.",
  "detail": "A unique short code could not be generated after the allowed attempts.",
  "code": "short_code_capacity_exhausted",
  "traceId": "..."
}
```

Equivalent safe shapes apply to mapped Domain exceptions with their configured
400 or 409 status. Unexpected failures retain the existing generic 500 shape.

## Data and state

No migration, database row, cache key, cache value, TTL, or persisted state
changes. Exception codes and activity event names are diagnostic contracts.

## Error cases

- Known Application capacity exhaustion: return safe detailed 503 Problem
  Details; do not classify it as an unexpected failure.
- Known Domain required value failure: return safe detailed 400 Problem
  Details. Normal HTTP input reaches controller validation first.
- Known Domain invalid state transition: return safe detailed 409 Problem
  Details. Normal update/delete not-found handling continues to prevent this
  path for deleted persisted resources.
- PostgreSQL or unknown technical failure: log once and return generic 500.
- Redis miss: emit `cache.miss`, query PostgreSQL, and populate the cache only
  when a value exists.
- Redis malformed value: emit `cache.hit` and `cache.invalid_value`, then use
  PostgreSQL fallback.
- Redis private timeout/error: emit the matching read/write event and preserve
  current fallback or best-effort behavior.
- Caller cancellation: rethrow; do not report it as a private cache timeout.
- Tracing disabled or unsampled: no cache event is emitted and behavior is
  otherwise identical.

## Test strategy

Bob works in red-green-refactor slices and adds focused tests proving:

1. Domain guards throw the exact custom exception types with stable code and
   safe field/operation context.
2. Capacity exhaustion reaches the centralized handler and returns exact safe
   503 Problem Details with `code` and non-empty `traceId`.
3. Test-only known Domain failures exercise exact 400/409 mapping without
   exposing arbitrary values.
4. An injected unexpected persistence exception still produces exact generic
   500 Problem Details and one EventId `1002` error record without leaking its
   message, URL, owner, or token.
5. Existing validation and not-found API tests retain 400/404 behavior.
6. Cache hit, miss, invalid value, read timeout, read error, write timeout, and
   write error each produce the specified event on the existing repository
   activity.
7. Read timeout/error is not mislabeled as a miss, error events expose only the
   exception type, and no event contains cache values or destination URLs.
8. Cache fallback, database-first writes, caller cancellation, and disabled or
   absent tracing behavior remain unchanged.

Use the existing WebApplicationFactory, capturing logger, ActivityListener,
fake repository, and fake cache infrastructure rather than adding another
test framework.

## Validation commands

Run serially from `src/url-shortener-api`:

```bash
dotnet restore UrlShortener.sln
dotnet build UrlShortener.sln --no-restore -m:1
dotnet test UrlShortener.sln --no-build -m:1
```

For proportional manual validation with tracing enabled, issue the same public
redirect twice and inspect the local trace viewer: the first request should
show `cache.miss` and PostgreSQL fallback, while the second should show
`cache.hit`. Repeat with Redis unavailable to observe `cache.read.error` or
`cache.read.timeout` while the redirect still resolves through PostgreSQL.

## Explicitly deferred

- Public detail for arbitrary Infrastructure/provider exceptions.
- Custom exceptions for cache miss or recovered cache degradation.
- New cache spans, metrics, sampling controls, or semantic-convention adoption.
- Refactoring all existing activity sources or the broader observability
  architecture.
- Changing controller validation/not-found outcomes to exception control flow.

## Open questions

None.

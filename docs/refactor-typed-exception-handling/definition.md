# Task definition

## Problem

`Program.cs` currently contains the complete unexpected-exception pipeline:
logging, status selection, content type, and RFC 7807 response construction.
The API also catches `ShortCodeAttemptsExhaustedException` directly in the
create controller, while Domain invariants use generic `ArgumentException` and
`InvalidOperationException` types.

This makes exception behavior distributed and prevents the API boundary from
reliably distinguishing known Application or Domain failures from unexpected
technical failures. Infrastructure cache degradation is already resilient, but
the trace does not explain whether a redirect used a cache hit, fell through on
a miss, or recovered from a cache timeout or error.

## Outcome

Move exception translation out of `Program.cs` and controllers into the ASP.NET
Core exception-handler abstraction. Known Application and Domain exceptions
produce safe, detailed Problem Details with a stable machine-readable code;
unexpected failures retain the existing generic 500 contract and single error
log.

Keep validation errors and not-found/ownership concealment as ordinary
controller results. They are expected request outcomes, not exceptions.

Add trace events to the existing Infrastructure cache activity so a local demo
can distinguish cache hits, misses, invalid values, timeouts, and recovered
cache failures without changing request behavior.

## In scope

- Remove inline exception handling from `Program.cs`.
- Register and use API-owned ASP.NET Core exception handlers through dependency
  injection.
- Remove the `ShortCodeAttemptsExhaustedException` catch from the create
  controller.
- Replace generic Domain invariant exceptions with custom Domain exceptions
  that retain safe contextual information.
- Enrich the existing Application capacity exception with a stable error code
  and safe message.
- Map known Application and Domain exception types to safe RFC 7807 responses.
- Keep unexpected Infrastructure and other technical exceptions mapped to a
  generic 500 without exposing their messages.
- Add non-sensitive cache outcome and degradation events to the existing
  Infrastructure activity.
- Add focused tests and preserve the complete existing suite.

## Out of scope

- Converting input validation, invalid route codes, missing resources, deleted
  resources, or ownership concealment into exceptions.
- Returning database, Redis, network, connection, stack-trace, or arbitrary
  Infrastructure exception details to clients.
- Creating a custom Strategy framework or exception-to-handler registry.
- Changing authentication, endpoint routes, successful response bodies,
  persistence, cache keys, TTLs, fallback behavior, or retry behavior.
- Adding spans for each cache outcome or adopting new telemetry packages.

## Acceptance criteria

1. `Program.cs` contains only exception-handler pipeline activation and no
   logging or Problem Details response-building delegate.
2. The create controller contains no exception translation, and capacity
   exhaustion still returns `503 application/problem+json`.
3. Known Application and Domain failures return safe Problem Details containing
   `status`, `title`, `detail`, a stable `code`, and the current `traceId`.
4. Domain required-value and invalid-state failures use custom Domain exception
   types rather than `ArgumentException` or `InvalidOperationException`.
5. Existing controller validation remains `400`; invalid, unknown, deleted, and
   foreign-owner short URLs retain their existing `404` concealment contract.
6. Unexpected persistence or other technical exceptions return the existing
   generic `500` Problem Details, are logged exactly once with EventId `1002`,
   and do not expose exception messages or request secrets.
7. A cache read records a trace event identifying a hit or miss. A malformed
   cached value, private cache timeout, or recovered cache exception records a
   distinct safe event before PostgreSQL fallback.
8. Cache write/remove timeouts and recovered exceptions record distinct safe
   events while retaining best-effort behavior.
9. Cache events contain no destination URL, owner, bearer token, connection
   string, cache value, stack trace, or exception message; an error event may
   contain only the exception type.
10. When tracing is disabled or no activity is sampled, cache behavior remains
    unchanged and no alternative logging or allocation-heavy trace pipeline is
    introduced.
11. The solution builds and all relevant tests pass serially.

## Constraints

- API owns HTTP status codes and Problem Details construction.
- Application and Domain exceptions do not reference ASP.NET Core or HTTP.
- Infrastructure translates only technical failures that have an established
  semantic meaning to Application. Other technical failures remain unexpected
  or are recovered locally by the cache fallback policy.
- Preserve the existing database-first write and cache-aside resilience rules.
- Keep one declared type per hand-written C# file.
- Repository documentation, code, identifiers, comments, and tests remain in
  English.

## Open questions

None.

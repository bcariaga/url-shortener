# Task definition

## Problem

Public short-URL resolution currently reads PostgreSQL for every request. This
does not reflect the read-heavy workload described by the system design and
makes PostgreSQL absorb repeated traffic and request peaks for popular codes.

The application must benefit from Redis without making Redis part of the
availability boundary. A slow, disconnected, or failed cache must add only a
small bounded delay before the application continues with PostgreSQL.

## Outcome

Add a single-node Redis development service and a cache-aside repository proxy.
Active short URLs are cached for five minutes with sliding expiration: every
cache hit returns the cached destination and resets its remaining lifetime to
five minutes. This keeps hot links in Redis and lets cold links expire.

PostgreSQL remains the source of truth. Cache failures never prevent application
startup, public resolution, or a successful management operation.

## In scope

- Add one Redis node to the development Docker Compose environment.
- Introduce an `ICacheProvider` abstraction and a Redis implementation in
  Infrastructure.
- Introduce an `IShortUrlRepository` proxy that collaborates with the cache
  provider and the existing EF/PostgreSQL repository.
- Cache a short URL after its PostgreSQL creation succeeds.
- Replace its cached destination after its PostgreSQL update succeeds.
- Remove its cache key after its PostgreSQL logical deletion succeeds.
- Resolve public short URLs from Redis first, then PostgreSQL on a miss.
- Populate Redis after a successful PostgreSQL fallback.
- Reset a cached entry's TTL to five minutes on every cache hit.
- Make the five-minute TTL and 100 ms per-operation cache timeout configurable.
- Fall back safely when Redis is absent, disconnected, slow, malformed, or
  otherwise unavailable.
- Add focused automated coverage and update local-development documentation.

## Out of scope

- Redis clustering, Sentinel, replicas, persistence, or production deployment.
- Distributed locks, background refresh, pre-warming, or negative caching.
- A circuit breaker or retry policy beyond Redis client's normal reconnect
  behavior.
- Cache metrics, redirect analytics, rate limiting, and database read replicas.
- Caching owner-scoped management lookups.
- Changing redirect status codes, authentication, short-code generation, or the
  PostgreSQL schema.

## Acceptance criteria

1. Development Compose starts one Redis node and gives the API its Redis
   connection setting, without making API startup depend on Redis health.
2. Creating an active short URL commits it to PostgreSQL and then attempts to
   cache its destination for the configured TTL.
3. Updating an active short URL commits the new destination to PostgreSQL and
   then attempts to replace the cached value with a fresh TTL.
4. Logically deleting a short URL commits the deletion to PostgreSQL and then
   attempts to remove its cache key.
5. Resolving a cached code returns the exact cached destination without querying
   PostgreSQL and atomically resets the key's TTL to the configured TTL.
6. Resolving a cache miss queries PostgreSQL; an active result is cached with a
   fresh TTL and returned, while an unknown or deleted code remains `404` and is
   not negatively cached.
7. Every cache interaction is bounded by a configurable timeout whose default is
   100 ms.
8. A missing Redis setting, connection failure, timeout, command error, invalid
   cached value, or Redis outage is treated as a cache miss/no-op. PostgreSQL is
   still used and existing HTTP behavior is preserved.
9. Once PostgreSQL has successfully created, updated, or deleted a resource, a
   subsequent cache failure does not fail or roll back the management request.
10. Cache behavior, timeout fallback, write invalidation, and existing API
    behavior are covered by automated tests; the complete solution builds and
    all relevant tests pass.

## Constraints

- PostgreSQL is authoritative; Redis contains only disposable derived data.
- The repository proxy pattern requested for this task is the only cache entry
  point used by Application.
- Cache keys contain the short code under a fixed application namespace and no
  credentials or owner data.
- Cache errors may be logged without cached destinations, credentials, or other
  sensitive values.
- The solution remains simple and proportional to this prototype.

## Open questions

None.

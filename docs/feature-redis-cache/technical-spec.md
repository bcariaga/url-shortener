# Technical specification

## Context

The public redirect handler currently calls `IShortUrlRepository` and the
registered `EFShortUrlRepository` reads PostgreSQL directly. Management writes
use the same repository abstraction. The new cache must apply consistently to
both workloads without moving Redis concerns into Domain or Application.

The established dependency direction remains:

```text
Domain <- Application <- Infrastructure <- Api
```

PostgreSQL remains authoritative. Redis is an optional best-effort accelerator
owned entirely by Infrastructure.

## Proposed solution

### Cache abstraction

Add a small Infrastructure-owned `ICacheProvider` contract with operations
equivalent to:

```csharp
Task<string?> GetAndRefreshAsync(
    string key,
    TimeSpan ttl,
    CancellationToken cancellationToken);

Task SetAsync(
    string key,
    string value,
    TimeSpan ttl,
    CancellationToken cancellationToken);

Task RemoveAsync(string key, CancellationToken cancellationToken);
```

Implement it with StackExchange.Redis. Store only the destination URL as the
Redis string value. The proxy builds keys as `short-url:{shortCode}`; cache
values do not include owner identifiers, tokens, timestamps, or EF entities.

`GetAndRefreshAsync` must perform the read and TTL renewal atomically with
Redis `GETEX` (or an equivalent atomic server-side operation). A successful hit
therefore resets the key to the full configured TTL. A miss does not create or
refresh any key.

Redis connectivity must be lazy and reconnect-capable so application startup
does not wait for a healthy cache. Configure the client not to abort permanently
on an initial connection failure. When the Redis connection setting is absent,
register a no-op provider with miss/no-op semantics and allow the application to
start in PostgreSQL-only mode.

### Repository proxy

Rename or retain the existing EF implementation as the concrete PostgreSQL
repository, but do not register it directly as `IShortUrlRepository`. Register
it as a scoped concrete collaborator and register a new scoped
`CachingShortUrlRepository` as `IShortUrlRepository`.

The proxy owns all caching policy:

```text
Application
    -> CachingShortUrlRepository
         -> ICacheProvider
         -> EFShortUrlRepository
```

The cache provider exposes Redis primitives; it does not implement fallback or
query PostgreSQL. The proxy catches cache failures, applies the configured time
bound, logs a concise warning, and invokes the EF collaborator when required.

Public resolution needs only a destination. Replace the entity-returning public
lookup with a purpose-specific repository method equivalent to:

```csharp
Task<string?> FindActiveDestinationByCodeAsync(
    string code,
    CancellationToken cancellationToken);
```

The EF implementation projects only `LongUrl` from an exact active-code query.
The Application handler returns that destination unchanged. Owner-scoped
`FindActiveAsync` continues to query PostgreSQL and returns a tracked entity for
management mutations; it does not consult Redis.

Change `SaveAsync` to receive the mutated `ShortUrl` explicitly:

```csharp
Task SaveAsync(ShortUrl entity, CancellationToken cancellationToken);
```

The EF collaborator still saves its tracked changes. Passing the entity lets the
proxy choose the correct post-commit cache operation without retaining hidden
per-request state: set the active destination after an update, or remove the key
after logical deletion.

### Operation ordering

All write paths are database-first:

- `InsertAsync`: await the EF insert/commit, then best-effort `SET` with the
  configured TTL.
- `SaveAsync` for an active entity: await the EF commit, then best-effort `SET`
  of the current destination with the full TTL.
- `SaveAsync` for a deleted entity: await the EF commit, then best-effort
  `DEL`.

The proxy must preserve `ShortCodeConflictException` and every database error.
Only cache errors are suppressed. A cache failure after a database commit does
not change the successful management result. This can temporarily leave a stale
entry until it expires, which is preferable to making Redis part of write
availability.

Public reads use cache-aside behavior:

1. Attempt the atomic cache get-and-refresh.
2. On a valid hit, return the destination without a database query.
3. On a miss, timeout, cancellation caused by the cache time budget, connection
   error, command error, or invalid value, query PostgreSQL.
4. If PostgreSQL returns an active destination, best-effort cache it with the
   full TTL and return it.
5. If PostgreSQL returns no active destination, return `null` without negative
   caching.

No retry is added around cache commands. StackExchange.Redis may reconnect in
the background, while each application request remains bounded.

## Interfaces and behavior

### Configuration

Bind and validate non-secret cache policy settings:

```json
{
  "Cache": {
    "TtlSeconds": 300,
    "TimeoutMilliseconds": 100
  }
}
```

Both values must be positive. The default development values are 300 seconds
and 100 milliseconds. Redis uses `ConnectionStrings:Redis`; local development
uses `localhost:6379`, while the Compose API receives `redis:6379` through its
environment.

The cache operation timeout is applied by the proxy around every provider call,
including population and invalidation. Redis client connect and asynchronous
command timeouts should be aligned with this short budget where supported, but
the proxy's deadline is authoritative.

Do not log connection strings or cache values. Missing Redis configuration is
not a validation error. Invalid cache policy values are configuration errors
because they would make the bounded-fallback contract ambiguous.

### Docker Compose

Add one `redis` service based on a bounded major Redis Alpine image compatible
with `GETEX`, with a `redis-cli ping` health check and host port `6379`. No Redis
volume, replication, cluster, authentication, or persistence configuration is
required for this development cache.

The API receives its Redis connection string but must not declare Redis health
as a startup condition. Keep the existing PostgreSQL health dependency because
PostgreSQL remains required.

### Existing HTTP contract

No endpoint shape changes:

- a resolved active code remains `302 Found` with the exact stored `Location`;
- an invalid, unknown, or deleted code remains the existing concealed `404`;
- management status codes and response bodies remain unchanged.

## Data and state

Redis key and value:

```text
key:   short-url:{six-character-code}
value: exact accepted destination URL
ttl:   configured sliding TTL, 300 seconds by default
```

Redis data is disposable and may be flushed without affecting correctness. No
PostgreSQL migration or Domain entity change is required.

The database-first strategy cannot make cache and PostgreSQL atomic. If Redis
fails after an update or deletion, an older cached value may remain for at most
its current TTL. This bounded eventual consistency is an accepted resilience
trade-off for this task.

## Error cases

- Redis is unavailable at startup: the API starts and uses PostgreSQL.
- Redis setting is absent: the API starts with cache miss/no-op behavior.
- Redis get exceeds the configured timeout: query PostgreSQL.
- Cached value is absent or unusable: treat it as a miss and query PostgreSQL.
- Redis population, replacement, or deletion fails: preserve the successful
  PostgreSQL/HTTP result and log a safe warning.
- PostgreSQL fails: propagate the existing application error behavior; Redis
  must not disguise a required database write failure.
- Both Redis and PostgreSQL fail during a cache miss: preserve the existing
  database failure behavior.
- Caller cancellation is not converted into a cache failure when it represents
  cancellation of the overall operation; only the proxy's private cache
  deadline triggers database fallback.

## Test strategy

Bob works in red-green-refactor slices and retains focused tests proving:

1. A cache hit returns the cached destination, requests a TTL refresh, and never
   calls the EF collaborator.
2. A cache miss queries EF, caches an active destination with the configured TTL,
   and returns it.
3. An unknown/deleted database result returns `null` and is not negatively
   cached.
4. Cache get failure and a provider delayed past 100 ms both fall back to EF.
5. Cache population failure does not alter a successful database resolution.
6. Insert caches only after the EF insert succeeds; an insert collision or
   database failure does not populate Redis.
7. Update caches the new destination only after EF save succeeds.
8. Delete removes the cache key only after EF save succeeds.
9. Set/delete cache failures do not change successful management results.
10. The Redis provider uses atomic get-and-expiry-refresh behavior and stores
    exact values with the supplied TTL. A focused live Redis integration test
    may be opt-in when Redis is available; deterministic proxy tests must not
    require external infrastructure.
11. Dependency injection resolves the caching proxy and supports missing Redis
    configuration without startup failure.
12. Existing Application, Infrastructure, API, authentication, ownership,
    redirect, and PostgreSQL tests remain green.

Avoid timing assertions with narrow wall-clock tolerances. Prove the 100 ms
fallback with a controlled provider and a generous upper bound that catches an
unbounded wait without making the suite flaky.

## Validation commands

Run serially from `src/url-shortener-api`:

```bash
dotnet restore UrlShortener.sln
dotnet build UrlShortener.sln --no-restore -m:1
dotnet test UrlShortener.sln --no-build -m:1
dotnet ef migrations list --project Infrastructure --startup-project Api
docker compose -f docker-compose.development.yml config
```

For live validation, start Compose without deleting the PostgreSQL volume,
apply the existing migration, and verify:

1. create returns `201` and produces a Redis key with a TTL near 300 seconds;
2. waiting briefly and resolving returns the exact `302`, while the key TTL is
   reset near 300 seconds;
3. update returns `200` and the cached value changes to the new destination;
4. delete returns `204` and the key is absent;
5. stopping Redis still permits uncached create, update, delete, `302`, and
   `404` behavior through PostgreSQL, with cache delay bounded near the
   configured timeout;
6. restarting Redis allows later misses/writes to populate it again without an
   API restart.

Tear Compose down without `--volumes` so the existing PostgreSQL data volume is
preserved.

## Explicitly deferred

- Negative caching and tombstones.
- Stampede prevention, request coalescing, distributed locking, and background
  refresh.
- Circuit breakers, custom reconnect loops, and cache-specific metrics.
- Redis HA, persistence, authentication, TLS, or deployment outside local
  development.
- Strong consistency between PostgreSQL commits and Redis mutations.
- Caching owner-scoped management queries or complete Domain entities.

## Open questions

None.

# URL Shortener — System Design

## 1. Overview

The system provides a URL shortening service.

Given a long URL, the system generates a short and unique URL. When a client accesses the short URL, the system resolves it to the original URL and responds with an HTTP `302 Temporary Redirect`.

Authenticated users can create short URLs and later modify or delete URLs they own.

The system is expected to be heavily read-oriented, with an estimated **read-to-write ratio of 1000:1**. The architecture therefore prioritizes low latency and high availability for redirect operations.

---

## 2. Functional Requirements

The system must support the following operations:

- Given a long URL, generate a unique short URL.
- Given a short URL, redirect the client to the corresponding long URL.
- Return `404 Not Found` when a short URL does not exist or is no longer active.
- Allow authenticated users to create short URLs.
- Allow users to modify URLs they created.
- Allow users to delete URLs they created.
- Prevent users from modifying or deleting URLs owned by another user.
- Every creation request generates a new short-link resource, even when the same long URL has previously been shortened.

Redirects use HTTP `302 Temporary Redirect`.

A temporary redirect is preferred over a permanent redirect because requests continue reaching the service, leaving room for future traffic analytics without changing the redirect contract.

---

## 3. Non-Functional Requirements

### Short URLs

Short codes should be as compact as reasonably possible.

Allowed characters are:

```text
0-9
a-z
A-Z
```

This provides a Base62 alphabet containing 62 possible characters.

### Read-heavy workload

The expected workload is approximately:

```text
1000 reads : 1 write
```

The architecture should therefore optimize the redirect path independently from URL creation and management operations.

### High availability

Redirect operations must remain highly available.

Management operations such as URL creation, modification, or deletion can tolerate lower availability than the redirect path.

### Low latency

Resolving a short URL should introduce minimal latency before returning the redirect response.

---

## 4. API Design

There are two conceptual workloads:

```text
Public redirect traffic

GET /{shortCode}


Management API

POST   /api/v1/short-urls
PUT    /api/v1/short-urls/{shortCode}
DELETE /api/v1/short-urls/{shortCode}
```

The redirect endpoint intentionally does not use the `/api/v1` prefix.

This keeps generated URLs short and allows redirect traffic to evolve independently from management traffic.

---

### 4.1 Resolve URL

```http
GET /{shortCode}
```

Example:

```http
GET /aZ91Kb
```

If the short code exists:

```http
HTTP/1.1 302 Found
Location: https://example.com/some/very/long/url
```

If the short code does not exist or has been deleted:

```http
HTTP/1.1 404 Not Found
```

No authentication is required.

---

### 4.2 Create Short URL

```http
POST /api/v1/short-urls
Authorization: Bearer <app-token>
Content-Type: application/json
```

Request:

```json
{
  "url": "https://example.com/some/very/long/url"
}
```

Response:

```json
{
  "shortCode": "aZ91Kb",
  "shortUrl": "https://short.domain/aZ91Kb",
  "url": "https://example.com/some/very/long/url"
}
```

The application token identifies the user creating the resource.

Every `POST` creates a new short-link resource.

The service does not deduplicate destination URLs.

For example:

```text
User A
POST https://example.com/page
→ abc123

User A
POST https://example.com/page
→ kL92xz

User B
POST https://example.com/page
→ Pq81Lm
```

All three are independent resources.

---

### 4.3 Update Short URL

```http
PUT /api/v1/short-urls/{shortCode}
Authorization: Bearer <app-token>
Content-Type: application/json
```

Request:

```json
{
  "url": "https://example.com/new-destination"
}
```

`PUT` is idempotent: sending the same request multiple times results in the same final resource state.

Only the owner of the short URL may modify it.

The `shortCode` remains unchanged when the destination URL is updated.

---

### 4.4 Delete Short URL

```http
DELETE /api/v1/short-urls/{shortCode}
Authorization: Bearer <app-token>
```

Only the owner may delete the URL.

Deletion is logical rather than physically removing the database record.

The resource stores a boolean deletion marker:

```text
IsDeleted = false  -> active
IsDeleted = true   -> deleted
```

This prevents deleted short codes from being accidentally reassigned and preserves information useful for auditing.

---

## 5. Data Model

The URL entity intentionally remains simple for the scope of this exercise.

```text
ShortUrl
-----------------------------
Id
ShortCode
LongUrl
OwnerId
IsDeleted
CreatedAt
UpdatedAt
```

Example:

```text
Id: 42
ShortCode: aZ91Kb
LongUrl: https://example.com/long/url
OwnerId: user-123
IsDeleted: false
CreatedAt: 2026-08-29T00:00:00Z
UpdatedAt: 2026-08-29T00:00:00Z
```

`Id` is a PostgreSQL-generated `bigint` identity. `IsDeleted` is stored directly as a required boolean; there is no status enum or separate status table. New resources start with `IsDeleted = false`, updates advance `UpdatedAt` only when the destination changes, and deletion sets `IsDeleted = true` while preserving the short code.

`ShortCode` must have a unique constraint.

Conceptually:

```sql
UNIQUE(short_code)
```

The primary lookup performed by the redirect service is:

```text
shortCode -> longUrl
```

Therefore `shortCode` is the primary lookup key.

No uniqueness constraint exists for `LongUrl`.

Multiple short-link resources may point to exactly the same destination.

---

## 6. Short Code Generation

The system uses a hash-based strategy followed by Base62 encoding.

A short code identifies a **short-link resource**, rather than identifying the destination URL itself.

Therefore, the hash input is not based exclusively on the long URL.

Conceptually:

```text
OwnerId
   +
LongUrl
   +
CreationNonce
   +
CollisionCounter
        |
        v
      Hash
        |
        v
     Base62
        |
        v
    ShortCode
```

The Base62 alphabet is:

```text
0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ
```

Using 62 possible characters keeps generated URLs compact.

### Creation nonce

Every creation operation includes a per-creation nonce.

This ensures that repeated requests from the same user for the same destination do not deterministically produce the same short code.

For example:

```text
User A + https://example.com + nonce A
    → abc123

User A + https://example.com + nonce B
    → kL92xz
```

The nonce may be implemented using a randomly generated value such as a UUID.

The `OwnerId` is also part of the hash input, ensuring that identical URLs created by different users start from different hash inputs.

---

## 7. Collision Resolution

Hashing does not guarantee uniqueness.

The database unique constraint on `ShortCode` is the final authority for uniqueness.

The initial candidate is generated with:

```text
counter = 0
```

Conceptually:

```text
hash(ownerId + longUrl + nonce + counter)
```

The resulting value is encoded using Base62 and used as the candidate short code.

The service attempts to insert the new resource.

```text
Generate candidate
       |
       v
    INSERT
       |
       v
Unique conflict?
   /        \
 no          yes
 |            |
return      counter++
             |
             v
           retry
```

Pseudo-code:

```text
nonce = generateNonce()
counter = 0

loop:
    input = ownerId + longUrl + nonce + counter

    hash = Hash(input)

    shortCode = Base62(hash)

    try:
        insert(shortCode, longUrl, ownerId)
        return shortCode

    catch UniqueConstraintViolation:
        counter++
```

An existence check before insertion is not sufficient to guarantee uniqueness.

For example, two application instances could simultaneously generate the same candidate and both observe that it does not exist.

The database constraint prevents this race condition:

```text
Instance A                  Instance B

generate abc123             generate abc123

        \                     /
         \                   /
          INSERT           INSERT
             \             /
              \           /
           UNIQUE(short_code)
                |
          +-----+-----+
          |           |
       success      conflict
                       |
                     retry
```

---

## 8. Redirect Flow

The redirect path is the most important path in the system.

A basic implementation could resolve URLs directly from the database:

```text
Client
   |
   | GET /aZ91Kb
   v
ASP.NET API
   |
   v
Database
   |
   v
302 Redirect
```

However, because the expected workload is approximately `1000:1` reads to writes, repeatedly accessing the database for every redirect would become inefficient.

Redis is therefore introduced as a cache.

```text
Client
   |
   v
Redirect API
   |
   v
Redis
   |
   | cache miss
   v
Database
```

The system uses a cache-aside strategy.

```text
GET /aZ91Kb

1. Look up aZ91Kb in Redis.

2. If found:
      return 302.

3. If not found:
      query database.

4. Store the mapping in Redis.

5. Return 302.
```

The database remains the source of truth.

---

## 9. Cache Invalidation

Because users can modify the destination associated with a short code, cached mappings may become stale.

When a URL is modified:

```text
PUT URL
   |
   v
Update database
   |
   v
Invalidate Redis entry
```

When a URL is deleted:

```text
DELETE URL
   |
   v
Mark URL deleted
   |
   v
Invalidate Redis entry
```

The next redirect request repopulates the cache from the database.

This favors implementation simplicity over more complex cache synchronization mechanisms.

---

## 10. Availability Strategy

The highest availability requirement applies to redirects.

Application instances should therefore remain stateless so multiple instances can process requests independently.

Conceptually:

```text
                Load Balancer
                     |
          +----------+----------+
          |          |          |
        API 1      API 2      API 3
          |          |          |
          +----------+----------+
                     |
                   Redis
                     |
                 Database
```

If one application instance becomes unavailable, another instance can continue handling redirects.

At larger scale, redirect and management workloads could be separated.

```text
                     Client
                       |
             +---------+---------+
             |                   |
      Redirect Service      Management API
             |                   |
          Redis               Database
             |
          Database
```

This separation is particularly useful because both workloads have very different characteristics.

### Redirect workload

```text
Very high volume
Read-heavy
Anonymous
Latency sensitive
```

### Management workload

```text
Lower volume
Write-oriented
Authenticated
More validation
```

This allows each workload to scale independently.

---

## 11. Authentication and Authorization

Management endpoints require an application token.

The token identifies the current user.

For modification and deletion operations:

```text
AuthenticatedUser.Id == ShortUrl.OwnerId
```

must hold.

Redirect requests remain anonymous.

This distinction keeps authentication-related processing outside the redirect critical path.

---

## 12. Failure Scenarios

### Cache unavailable

If Redis becomes unavailable, the service falls back to the database.

```text
Redis unavailable
       |
       v
Database
       |
       v
302
```

Redirects continue working, although database load and latency increase.

---

### Database unavailable

Cached URLs can potentially continue resolving while their cache entries remain available.

New URLs cannot be created.

Uncached URLs cannot be resolved until database availability is restored.

Database calls are protected by an application-wide circuit breaker outside the
EF Core repository. Only transient PostgreSQL and timeout failures contribute to
the circuit. Once open, database-dependent requests fail fast with `503 Service
Unavailable`; cached redirects continue to bypass PostgreSQL. The circuit later
allows a recovery probe and closes after a successful database call.

Connection and command timeouts bound the calls used to sample database health.
Idempotent reads retry transient failures twice with short exponential backoff
and jitter. The circuit breaker observes the final read outcome after retries
are exhausted. Automatic write retries are not enabled because losing
connectivity during a commit can leave the outcome unknown and replaying a
create could duplicate its effect.

---

### Short-code collision

The database rejects the duplicate `ShortCode`.

The application increments the collision counter, generates another candidate, and retries.

---

### Application instance unavailable

Because application instances are stateless, the load balancer can route requests to another healthy instance.

---

## 13. Scaling Strategy

The implementation targets a small deployment while providing clear paths for evolution.

### Initial version

```text
        ASP.NET Core
             |
        +----+----+
        |         |
      Redis   PostgreSQL
```

### Increased traffic

```text
       Load Balancer
             |
       ASP.NET x N
             |
           Redis
             |
        PostgreSQL
```

### Larger scale

Redirect and management workloads can be separated.

```text
                 Load Balancer
                      |
          +-----------+-----------+
          |                       |
   Redirect Service         Management API
          |
     Redis Cluster
          |
   Partitioned URL Store
```

---

## 14. Potential Improvements

The following improvements are intentionally excluded from the initial implementation but represent possible evolution paths.

### Analytics and event delivery

Redirect traffic could produce visit events containing a short code, timestamp, referrer, user agent, or derived geographic information. This feature is not implemented in the take-home prototype.

If added, analytics must remain outside the critical redirect path so an unavailable analytics destination never delays or prevents a `302` response. A small deployment could begin with an in-process channel and background worker, accepting possible event loss on process termination. Stronger delivery requirements or horizontal scaling could later justify a durable broker and independent analytics workers.

Analytics and audit events should remain separate concerns: analytics describes usage, while auditing records changes to resources and their actor. Neither is required for the current prototype.

---

### Distributed short-code generation

The current hash-and-retry approach is simple and appropriate for the expected scope.

At significantly larger scale, code generation could use a distributed ID generator followed by Base62 encoding.

```text
Distributed ID Generator
          |
          v
        Base62
          |
          v
      ShortCode
```

This could reduce collision handling and database coordination.

---

### Database partitioning

If the URL mapping table grows beyond the capacity of a single database, mappings can be partitioned using the short code.

For example:

```text
hash(shortCode) % numberOfPartitions
```

Because redirects access records primarily by short code, it provides a natural shard key.

---

### Read replicas

Database read replicas could absorb cache misses and reduce load on the primary database.

Writes would continue to use the primary.

---

### Multi-layer caching

Extremely popular URLs could use an additional application-level cache.

```text
Local Memory Cache
        |
        v
      Redis
        |
        v
     Database
```

This reduces network calls for hot URLs.

---

### Negative caching

Requests for nonexistent short codes could be cached for a short period.

```text
GET /does-not-exist

Redis miss
    |
Database miss
    |
cache NOT_FOUND with short TTL
```

This can protect the database against repeated requests for invalid codes.

---

### Rate limiting

URL creation should have significantly stricter rate limits than redirect traffic.

For example:

```text
POST /api/v1/short-urls
    limited per user/IP

GET /{shortCode}
    substantially higher limits
```

---

### Abuse prevention

Public URL shorteners can be abused for:

- phishing;
- malware distribution;
- spam;
- automated URL creation.

Potential future mitigations include:

```text
Rate limiting
Domain reputation checks
URL blacklists
Abuse reporting
Administrative disabling
```

These mechanisms are outside the scope of the initial implementation.

---

### Edge / CDN redirects

At extremely high redirect volumes, popular mappings could be cached closer to users through edge infrastructure or a CDN.

```text
User
 |
 v
CDN / Edge
 |
 | cache miss
 v
Redirect Service
 |
 v
Redis
```

This would reduce both origin traffic and redirect latency.

---

## 15. Key Architectural Decisions

### 302 instead of 301

The system uses `302 Temporary Redirect`.

Permanent redirects may be aggressively cached by browsers or intermediate infrastructure, causing subsequent visits to bypass the service.

Using `302` keeps redirect requests observable by the service and permits analytics to be added later without clients bypassing the service.

---

### Short codes identify resources, not destinations

Two short URLs may point to the same destination.

```text
abc123 ──┐
         ├──> https://example.com
kL92xz ──┘
```

They remain independent resources with their own:

```text
Owner
Lifecycle
```

Changing the destination does not change the short code.

---

### No URL deduplication

Every `POST` creates a new short-link resource.

The system intentionally does not attempt to find an existing resource for the same long URL.

This keeps resource identity independent from destination identity and allows multiple links to the same destination to have independent ownership and lifecycle.

---

### Database-enforced short-code uniqueness

Application-level existence checks cannot guarantee uniqueness under concurrency.

The database `UNIQUE(short_code)` constraint is therefore the final authority.

Collisions result in regeneration and retry.

---

### Cache-aside

The database remains the source of truth while Redis accelerates the read-heavy redirect workload.

Cache entries are invalidated after modifications.

---

## 16. Scope and Trade-offs

The implementation intentionally favors simplicity over unnecessary infrastructure.

The goal is not to simulate an internet-scale deployment, but to demonstrate an architecture that can evolve toward one.

For the take-home implementation:

```text
ASP.NET Core on .NET 10
PostgreSQL with EF Core migrations
Redis cache-aside with bounded fallback
Bearer-token ownership for management operations
PostgreSQL retry and circuit-breaker resilience
Health checks, structured logs, metrics, and optional OTLP tracing
Docker Compose with an Aspire Dashboard for local diagnostics
```

provides enough infrastructure to demonstrate the core architectural decisions.

More complex components such as:

```text
Kafka
Analytics workers
Database sharding
Redis Cluster
CDN / Edge computing
Distributed ID generation
Multi-region deployment
```

are intentionally left as evolution paths rather than implemented prematurely.

The architecture should make these changes possible without requiring them for the initial system.

---

## 17. Use of AI

AI-assisted tooling was used to help refine requirements, draft technical specifications, implement focused slices, generate tests, and perform independent review. The repository's file-based workflow kept the task definition and technical specification as the authoritative scope before implementation, followed by a separate QA pass.

All generated changes were reviewed against the intended behavior and validated through builds, automated tests, integration checks, and local deployment checks. The author remains responsible for the architectural decisions, trade-offs, and submitted code.

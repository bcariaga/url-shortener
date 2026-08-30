# Technical specification

## Context

The Management API persists active and logically deleted `ShortUrl` entities
and returns public URLs formed as `/{shortCode}`. The current root endpoint uses
Mediary with `HelloWorldQuery`, while write use cases use focused commands and
handlers. This slice adds the missing read-only redirect vertical without
changing the existing data model or migration.

The established layer direction remains authoritative:

```text
Domain <- Application <- Infrastructure <- Api
              ^                         /
              +------------------------+
```

## Proposed solution

Add a focused Mediary query named `ResolveShortUrlQuery` containing the route
short code. It implements `IQuery<string?>`. Add
`ResolveShortUrlQueryHandler`, which implements Mediary's existing
`IRequestHandler<string?, ResolveShortUrlQuery>` contract, asks the Domain
repository for the active resource, and returns its stored `LongUrl` or `null`.

The `Query` name distinguishes this read from the existing state-changing
commands. `IRequestHandler` remains in the handler declaration because that is
Mediary's common handler interface for commands and queries; no parallel
dispatcher abstraction is introduced.

Add a public redirect controller with `GET /{shortCode}`. The controller builds
and validates the query, dispatches it, and translates the Application result:

- a non-null destination becomes ASP.NET Core's HTTP `302 Found` redirect;
- `null` becomes HTTP `404 Not Found` Problem Details titled
  `Short URL not found.`

The controller has no `[Authorize]` attribute and does not read an owner claim.
The existing `HomeController` continues to own `GET /`.

## Interfaces and behavior

### Application request

```csharp
public sealed class ResolveShortUrlQuery : IQuery<string?>
{
    public required string ShortCode { get; init; }
}
```

The query is placed with the Application handler contracts, alongside the
existing focused use-case structure. Its validator reuses
`ValidShortCode()` so only exactly six `0-9`, `a-z`, or `A-Z` characters are
dispatchable.

The handler performs no state transition and returns only the destination
needed by the API. It does not return an entity, ASP.NET result, status code,
or redirect type.

### Domain repository contract

Extend `IShortUrlRepository` with a purpose-specific read method equivalent to:

```csharp
Task<ShortUrl?> FindActiveByCodeAsync(
    string code,
    CancellationToken cancellationToken);
```

Keep the existing owner-scoped lookup unchanged for management mutations.
Public resolution deliberately has no owner parameter.

### Infrastructure lookup

`EFShortUrlRepository.FindActiveByCodeAsync` queries `short_urls` by exact
`ShortCode` with `IsDeleted = false`. Use `AsNoTracking()` because the result is
read-only, and rely on the existing globally unique short-code index. Do not
perform a second query to distinguish missing from deleted resources.

### HTTP endpoint

For an active code:

```http
GET /aZ91Kb

HTTP/1.1 302 Found
Location: https://example.com/some/very/long/url
```

The `Location` value is the accepted destination already stored on the entity.
The service does not fetch, normalize, validate again, or follow that URL.

For an invalid, unknown, or deleted code:

```http
HTTP/1.1 404 Not Found
Content-Type: application/problem+json
```

All three cases use the same title and response shape. Invalid route input is
rejected by the query validator before dispatch and is intentionally mapped to
`404`, not `400`, matching the existing short-code concealment behavior.

## Data and state

No migration or entity change is required. Resolution reads the existing
`short_code`, `long_url`, and `is_deleted` columns. It does not call
`SaveAsync`, update timestamps, increment counters, or create analytics data.

The direct PostgreSQL lookup is the smallest implementation for the current
prototype. The public path remains isolated behind its query/handler and
repository method so a cache can be introduced later without changing the HTTP
contract.

## Error cases

- A code that is not exactly six Base62 characters: `404 Not Found` without a
  repository lookup.
- An unknown code: `404 Not Found`.
- A logically deleted code: `404 Not Found` indistinguishable from unknown.
- A database or unexpected failure: the existing standard `500 Internal Server
  Error` handling; do not convert it to `404` or expose internal details.

No authentication challenge, `400`, `301`, `307`, or `308` response is part of
this endpoint's contract.

## Test strategy

Bob works in red-green-refactor slices and retains tests that prove:

1. The query validator accepts exactly six Base62 characters and rejects wrong
   lengths or characters.
2. The Application handler returns the stored destination for an active result,
   returns `null` when lookup misses, passes the requested code to the
   repository, and never saves state.
3. API integration returns an un-followed `302 Found` with the exact `Location`
   for an active resource, without authentication.
4. API integration returns the same `404` Problem Details contract for invalid,
   unknown, and deleted codes.
5. Infrastructure implements an exact, active-only, no-tracking lookup; focused
   evidence may use EF query inspection or the real PostgreSQL smoke flow rather
   than EF's in-memory provider.
6. Existing root and Management API tests remain green.

Test HTTP clients must disable automatic redirect following when asserting the
`302` response.

## Validation commands

Run from `src/url-shortener-api`:

```bash
dotnet restore UrlShortener.sln
dotnet build UrlShortener.sln --no-restore -m:1
dotnet test UrlShortener.sln --no-build -m:1
dotnet ef migrations list --project Infrastructure --startup-project Api
docker compose -f docker-compose.development.yml config
```

For the live PostgreSQL/HTTP smoke path, reuse the documented development
configuration, apply the existing migration, create one short URL, and request
it with redirect following disabled. Verify the exact `302` status and
`Location` header, then logically delete it and verify the same path returns
`404`. Tear Compose down without deleting its named volume.

## Explicitly deferred

- Redirect analytics or counters.
- Redis and cache invalidation.
- Read replicas, partitioning, and a separately deployed redirect service.
- Rate limiting, abuse controls, and destination safety checks.
- Removal or repurposing of the existing `GET /` response.

## Open questions

None.

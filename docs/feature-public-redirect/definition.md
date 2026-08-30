# Task definition

## Problem

The service can create, update, and logically delete short-link resources, but
the public short URLs it returns cannot yet be resolved. A client that visits a
generated short URL therefore cannot reach its destination.

## Outcome

Provide the anonymous public read path described in `Design.md`: resolving an
active six-character short code returns an HTTP `302 Temporary Redirect` to
its stored destination, while invalid, unknown, or deleted codes return the
same HTTP `404 Not Found` response.

## In scope

- Anonymous `GET /{shortCode}` resolution.
- A read-only Application query and request handler, named as a query rather
  than a command because the use case does not change state.
- Active-resource lookup by short code without an owner constraint.
- HTTP `302 Found` responses with the stored destination in the `Location`
  header.
- A uniform `404 Not Found` response for malformed, unknown, and logically
  deleted short codes.
- Automated tests and proportional validation of the redirect flow.

## Out of scope

- Redirect analytics, counters, audit records, or any other write during a
  redirect.
- Redis or another cache, cache invalidation, replication, or a separately
  deployed read service.
- Authentication or ownership checks on the public redirect route.
- Destination availability checks, URL rewriting, or following the destination.
- Changes to creation, update, deletion, short-code generation, or the database
  schema.
- Replacing the existing anonymous `GET /` health/example response.

## Acceptance criteria

1. `GET /{shortCode}` requires no authentication.
2. When `{shortCode}` identifies an active resource, the response is HTTP `302
   Found` and its `Location` header is exactly the resource's stored long URL.
3. An invalid Base62 code, an unknown code, and a logically deleted code each
   return HTTP `404 Not Found` with the same Problem Details shape and without
   exposing why resolution failed.
4. Resolving a short code does not modify the resource or persist any state.
5. Resolution does not require or apply an owner identifier; any client can
   resolve any active short URL.
6. The existing `GET /` response and authenticated Management API behavior
   remain unchanged.
7. Focused tests, the complete solution build, and the complete test suite pass.

## Constraints

- Use the existing .NET 10, controller-based ASP.NET Core, Mediary,
  Entity Framework Core, Npgsql, and API/Application/Domain/Infrastructure
  boundaries.
- Preserve the current command/handler conventions for writes while naming this
  read request as a query.
- Keep the implementation proportional to the challenge and avoid speculative
  read infrastructure.
- Repository documentation, code, identifiers, comments, and tests are in
  English.
- Preserve unrelated user changes.

## Open questions

None.

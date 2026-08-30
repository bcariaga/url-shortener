# Task definition

## Problem

The executable scaffold proves the selected .NET layers and PostgreSQL wiring,
but the service cannot yet create, change, or delete short-link resources. The
authenticated Management API described in `Design.md` is therefore not usable.

## Outcome

Provide a small, persistent Management API that lets an authenticated owner
create independent short links, update destinations without changing their
short codes, and logically delete their own links. The implementation enforces
ownership, validates destinations, and resolves code collisions through the
database uniqueness constraint.

## In scope

- `POST /api/v1/short-urls` to create a new short-link resource.
- `PUT /api/v1/short-urls/{shortCode}` to change an owned active resource.
- `DELETE /api/v1/short-urls/{shortCode}` to logically delete an owned active
  resource.
- Bearer-token authentication using an opaque token-to-owner mapping supplied
  through runtime configuration.
- Absolute HTTP/HTTPS destination validation with a 2,048-character limit.
- Six-character Base62 short codes generated from owner, destination, a
  per-creation nonce, and a collision counter.
- Bounded collision retries, with PostgreSQL's unique constraint as the final
  uniqueness authority.
- PostgreSQL persistence, an Entity Framework Core migration, ownership
  enforcement, and UTC timestamps.
- Configurable public base URL used to construct each returned `shortUrl`.
- A correctly named repository `README.md` that explains how to create a secure
  local token, associate it with an owner identifier, configure it without
  committing secrets, and call the Management API.
- Automated tests and a real PostgreSQL validation of the migration and main
  management flows.

## Out of scope

- Public redirect resolution (`GET /{shortCode}`).
- Management endpoints for listing or retrieving resources.
- Redis, cache invalidation, analytics, audit history, queues, and background
  workers.
- Rate limiting, abuse detection, administrative operations, and token-issuing
  or user-management endpoints.
- Destination deduplication or user-selected short codes.
- Physical deletion of short-link rows.
- Production deployment, secret distribution, and token rotation.

## Acceptance criteria

1. A request without a configured valid Bearer token cannot access any
   Management API endpoint and receives HTTP `401 Unauthorized`.
2. Every valid `POST`, including repeated requests from the same owner for the
   same destination, creates an independent active resource and returns HTTP
   `201 Created` with `shortCode`, `shortUrl`, and `url`.
3. Generated codes contain exactly six characters from
   `0-9`, `a-z`, and `A-Z`, and the database prevents a code from ever being
   assigned to more than one resource.
4. If insertion encounters a short-code uniqueness conflict, creation retries
   with a new collision counter; exhausting the configured finite attempt
   count returns HTTP `503 Service Unavailable` without creating a resource.
5. A valid owner can replace the destination of an active resource with
   `PUT`; the response is HTTP `200 OK`, the short code remains unchanged, and
   repeating the same request does not change persisted state.
6. A valid owner can logically delete an active resource with `DELETE` and
   receives HTTP `204 No Content`; the row and its short code remain reserved.
7. An unknown, deleted, or differently owned short code receives HTTP
   `404 Not Found` for update and delete, without revealing whether another
   owner has the resource.
8. Missing, non-string, relative, non-HTTP(S), or longer-than-2,048-character
   destinations receive HTTP `400 Bad Request` and do not change persisted
   state.
9. The returned `shortUrl` is formed from the configured public base URL and
   the generated short code without malformed or duplicate separators.
10. The EF Core migration creates the short-link table with a PostgreSQL-
    generated `bigint` primary key, a required `is_deleted` boolean, the other
    required columns, and a named unique constraint or index for `ShortCode`.
11. Automated tests cover validation, authentication, ownership, idempotent
    update, logical deletion, independent duplicate-destination creation, and
    collision retry behavior; the complete solution builds and all relevant
    tests pass.
12. A documented validation flow applies the migration to PostgreSQL and
    demonstrates the create, update, ownership rejection, and delete behavior
    against the running API.
13. `README.md` explains that owners are configuration identifiers rather than
    persisted user accounts, and provides copyable local commands to generate
    a random token, configure its token-to-owner mapping with .NET user secrets,
    start the service, and authenticate requests without exposing a real token.

## Constraints

- Use .NET 10, controller-based ASP.NET Core, Mediary, Entity Framework Core,
  Npgsql, and the existing API/Application/Domain/Infrastructure boundaries.
- Keep the implementation proportional to the take-home challenge and avoid
  introducing general-purpose repositories, event systems, or speculative
  abstractions.
- Runtime token values are secrets and must not be committed to source,
  documentation, test output, or command output.
- Repository documentation, code, identifiers, comments, and tests are in
  English.
- Preserve unrelated user changes and the already passing root endpoint.

## Open questions

None.

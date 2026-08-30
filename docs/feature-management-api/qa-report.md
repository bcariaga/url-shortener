# QA report

Verdict: Pass

## Requirement coverage

The management behavior is covered by the current implementation and tests:
bearer authentication is applied only to management
routes; configured tokens yield an owner claim; create always inserts a new
row; generated codes are six-character Base62 values; named unique conflicts
retry exactly five times and map to generic `503`; update preserves the code
and domain no-op behavior preserves `UpdatedAt`; delete is logical and
repeated/foreign/deleted mutations are concealed as `404`; destination and
route validation map to `400`/`404`; `Location` and `shortUrl` use the trimmed
configured base URL; and unexpected persistence errors are not classified as
collisions. EF metadata and the migration show PostgreSQL identity `bigint`,
`is_deleted` boolean default, UTC timestamp columns, bounded URL/owner/code
columns, and the named global unique short-code index. No owner index was added,
matching the approved decision. `README.md` documents configuration-only
owners, user-secrets token generation, a second owner, migration/startup,
Bearer calls, environment names, and secret cleanup without real credentials.

## Test quality

The tests meaningfully cover domain lifecycle and idempotence, URL validation,
deterministic generation and collision counters, five-attempt exhaustion and
unexpected-error propagation, owner-filtered mutation, authentication and
Bearer challenge, API status/body/Location behavior, duplicate destinations,
logical deletion/concealment, anonymous root access, and EF model metadata.
The API uses a narrow deterministic store double; relational uniqueness is
represented by the EF migration/model rather than an in-memory provider.

## Commands and results

- `git diff --check`: passed.
- `dotnet build --no-restore`: passed, 0 warnings and 0 errors.
- `dotnet test --no-build --no-restore --verbosity:minimal`: passed; 23 tests,
  0 failures.
- `/tmp/url-shortener-dotnet-tools/dotnet-ef migrations list --project Infrastructure --startup-project Api --no-build --no-connect`: passed and listed `20260829202832_InitialShortUrls`.
- `docker compose -f docker-compose.development.yml config` from
  `src/url-shortener-api`: passed.
- Real PostgreSQL migration/API smoke: migration applied successfully. The
  authenticated flow passed: root `200`, two creates `201` with distinct
  codes, update `200`, foreign owner `404`, delete `204`, and repeated delete
  `404`.
- PostgreSQL schema inspection passed on a fresh disposable database: physical
  columns are exactly `id:bigint`, `short_code:varchar`, `long_url:varchar`,
  `owner_id:varchar`, `is_deleted:boolean`, `created_at:timestamptz`, and
  `updated_at:timestamptz`; indexes are the primary key on `id` and
  `ux_short_urls_short_code(short_code)`.
- Persisted live state showed two positive generated identities, one deleted
  row and one active row. The disposable QA database was dropped; the corrected
  migration was applied to the development database and Compose was stopped
  without deleting its volume.

## Findings

No blocking or non-blocking product findings.

## Residual risks

The compose file intentionally supplies database settings but no management
token; live verification requires injecting the documented runtime token
configuration.

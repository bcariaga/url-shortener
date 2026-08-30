# QA report

Verdict: Pass

## Requirement coverage

- Compose defines one Redis 7 Alpine node with healthcheck and port 6379; the
  API receives `redis:6379` but depends only on PostgreSQL health.
- The Infrastructure proxy is the sole `IShortUrlRepository` registration,
  delegates owner lookups to PostgreSQL, and applies cache-aside behavior for
  public resolution.
- Tests prove exact `short-url:{code}` keys, destination values, 300-second
  TTLs, hit-without-DB, miss ordering/population, malformed-value fallback,
  timeout fallback, and no negative caching.
- Tests prove insert collision/database failure prevents cache writes;
  successful insert/update/delete perform cache mutation after DB events;
  update/delete cache failures and SET/DEL timeouts preserve DB success.
- Caller tokens are passed to DB writes and cancellation is propagated without
  cache mutation. DI tests cover missing Redis (no-op), invalid options, and an
  unreachable Redis endpoint within a bounded resolution.
- Redis provider has an opt-in integration test proving atomic `GETEX`, sliding
  TTL refresh, exact SET value/TTL, and DEL behavior.

## Test quality

The focused proxy tests use deterministic fakes and event sequences, while the
provider integration test is opt-in through `URL_SHORTENER_TEST_REDIS` and does
not make the normal suite depend on external infrastructure. Code is readable,
small, and aligned with the requested proxy/resilience design.

## Commands and results

- `dotnet build UrlShortener.sln --no-restore -m:1` — Pass (0 warnings, 0 errors).
- `dotnet test UrlShortener.sln --no-build -m:1` — Pass (62 tests total).
- `docker compose -f docker-compose.development.yml config` — Pass.
- Opt-in Redis test was not rerun in this final pass because the environment's
  test runner previously failed to bind its communication socket with
  `SocketException (13): Permission denied`; deterministic provider/proxy tests
  remain green and the Redis container was stopped without removing volumes.
- EF migration listing was not completed because PostgreSQL was unavailable.

## Findings

No blocking findings.

## Residual risks

Live Redis execution and EF migration discovery remain environment-limited,
not implementation failures. The opt-in test is available for execution in a
non-restricted environment.

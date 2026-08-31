# URL Shortener

An ASP.NET Core URL shortener backed by PostgreSQL, with Redis caching and
OpenTelemetry traces. See the [Design document](DESIGN.md) for the architecture
and trade-offs.

## Live demo

A deployed API demo is available at
[https://short.unsolo.dev](https://short.unsolo.dev).
Public short links can be opened without authentication. To create, update, or
delete short links through the Management API, request an API key at
[contact@unsolo.dev](mailto:contact@unsolo.dev) and send it as a Bearer token.

```bash
curl --fail-with-body \
  -H "Authorization: Bearer <api-key>" \
  -H 'Content-Type: application/json' \
  -d '{"url":"https://example.com"}' \
  https://short.unsolo.dev/api/v1/short-urls
```

## Run locally

### Prerequisites

- Git
- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- Docker with Docker Compose
- OpenSSL, used only to generate a local token
- Entity Framework CLI 10. Install it with
  `dotnet tool install --global dotnet-ef --version 10.0.0`, or use
  `dotnet tool update --global dotnet-ef --version 10.0.0` if an older version
  is already installed.

The commands below use Bash/zsh. Clone the repository and enter the solution
directory:

```bash
git clone https://github.com/bcariaga/url-shortener.git
cd url-shortener/src/url-shortener-api
```

Generate a local Management API token, start PostgreSQL, Redis, and the Aspire
Dashboard, then apply the database migration:

```bash
export URL_SHORTENER_TOKEN="$(openssl rand -hex 32)"
export URL_SHORTENER_OWNER_ID="local-user-a"

docker compose -f docker-compose.development.yml up -d postgres redis aspire-dashboard
dotnet ef database update --project Infrastructure --startup-project Api
```

Start the API container:

```bash
docker compose -f docker-compose.development.yml up --build -d api
```

Verify that the application is ready and create a short URL:

```bash
curl --fail http://localhost:8080/health/live
curl --fail http://localhost:8080/health/ready

curl --fail-with-body \
  -H "Authorization: Bearer $URL_SHORTENER_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"url":"https://example.com"}' \
  http://localhost:8080/api/v1/short-urls
```

The create response includes the public `shortUrl`. Open it in a browser or
request it with `curl` to receive the redirect. The local endpoints are:

| Service | URL |
| --- | --- |
| API | http://localhost:8080 |
| Liveness | http://localhost:8080/health/live |
| Readiness | http://localhost:8080/health/ready |
| Aspire Dashboard | http://localhost:18888 |

To stop the local stack without deleting PostgreSQL data:

```bash
docker compose -f docker-compose.development.yml down
unset URL_SHORTENER_TOKEN URL_SHORTENER_OWNER_ID
```

The Management API is a prototype with no `users` table or registration flow:
a user is a stable owner identifier associated with an opaque token in runtime
configuration.

### Run the API directly with .NET

To debug the API outside Docker, start only its dependencies, save the token in
.NET user secrets, apply the migration, and run the API:

```bash
export URL_SHORTENER_TOKEN="$(openssl rand -hex 32)"
export URL_SHORTENER_OWNER_ID="local-user-a"

docker compose -f docker-compose.development.yml up -d postgres redis aspire-dashboard
dotnet user-secrets --project Api set "ManagementAuth:Tokens:0:Token" "$URL_SHORTENER_TOKEN"
dotnet user-secrets --project Api set "ManagementAuth:Tokens:0:OwnerId" "$URL_SHORTENER_OWNER_ID"
dotnet ef database update --project Infrastructure --startup-project Api
dotnet run --project Api
```

The launch profile selects the Development environment and listens at
http://localhost:8080. Add a second owner with
`ManagementAuth:Tokens:1:Token` and `ManagementAuth:Tokens:1:OwnerId`. List or
remove local entries with:

```bash
dotnet user-secrets list --project Api
dotnet user-secrets remove ManagementAuth:Tokens:0:Token --project Api
```

For non-user-secrets environments, use `ManagementAuth__Tokens__0__Token` and `ManagementAuth__Tokens__0__OwnerId` (and index `1` for another owner). Configure `ConnectionStrings__PostgreSql` and `PublicBaseUrl` through the environment as appropriate; never commit populated credentials.

### Management token with Docker Compose

`.NET` user secrets from the previous section are not automatically available
inside containers. The development Compose file instead reads the management
token from `URL_SHORTENER_TOKEN` in the shell that starts Compose and maps it to
`ManagementAuth__Tokens__0__Token` inside the API container.

Compose stops with an error if `URL_SHORTENER_TOKEN` is missing. The owner ID is
not secret and defaults to `local-user-a`, so exporting
`URL_SHORTENER_OWNER_ID` is optional. Use the same token to call the protected
create, update, and delete endpoints:

```bash
curl --fail-with-body \
  -H "Authorization: Bearer $URL_SHORTENER_TOKEN" \
  -H 'Content-Type: application/json' \
  -d '{"url":"https://example.com"}' \
  http://localhost:8080/api/v1/short-urls
```

Set Bruno's secret `token` variable to the value of `URL_SHORTENER_TOKEN`.

Do not put the token in `docker-compose.development.yml`, a committed `.env`
file, command output, or documentation. Also avoid sharing the output of
`docker compose config`, because it renders the resolved environment values.

## Local Redis cache

The development Compose file starts Redis on `localhost:6379` and configures the API automatically. Redis is optional: if unavailable, the API continues with PostgreSQL. Cache entries use a sliding five-minute TTL and cache operations fall back after 100 ms. Override `Cache:TtlSeconds`, `Cache:TimeoutMilliseconds`, or `ConnectionStrings:Redis` as needed.

Optional Redis integration coverage: `URL_SHORTENER_TEST_REDIS=localhost:6379 dotnet test src/url-shortener-api/Infrastructure/tests/Infrastructure.Tests.csproj -m:1`.

## PostgreSQL resilience

PostgreSQL operations use a shared circuit breaker around the EF Core repository. Only transient Npgsql and timeout failures count toward the circuit; caller cancellation, domain failures, and short-code conflicts do not. Redis cache hits bypass the database circuit, so cached redirects can continue while PostgreSQL is unavailable. Database-dependent requests return `503 Service Unavailable` when PostgreSQL fails or the circuit is open, with `Retry-After` while calls are being short-circuited.

Connection and command timeouts, read retry attempts/delay, failure ratio, sampling window, minimum throughput, and break duration are configured under `DatabaseResilience` in `Api/appsettings.json`. Container overrides use the standard double-underscore form, for example `DatabaseResilience__BreakDurationSeconds`.

The two read operations retry transient PostgreSQL failures twice with exponential backoff and jitter before reporting one failed operation to the circuit breaker. Creates and updates are intentionally not retried because write outcomes can be ambiguous if connectivity is lost during commit.

## Local observability

The development Compose file includes the standalone Aspire Dashboard at
http://localhost:18888. It accepts OTLP from the API over the private Compose
network and keeps logs, traces, and metrics in memory; it is anonymous and for
local diagnostics only. When running the API directly, set
`OTEL_EXPORTER_OTLP_ENDPOINT` and optionally `OTEL_EXPORTER_OTLP_PROTOCOL`.

Tracing is enabled by default. Set `Observability:TracingEnabled` to `false`
(or `Observability__TracingEnabled=false` in a container environment) to stop
trace instrumentation and flow activity creation while retaining structured
logs, metrics, and health checks. `Logging:LogLevel` remains authoritative for
log filtering. Changes take effect after restarting the API.
Dozzle remains useful for container console logs, but does not display OTLP
traces or reconstruct transactions. Health probes are available at
`/health/live` and `/health/ready`.

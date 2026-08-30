# URL Shortener

See the [Design document](Design.md). The Management API is a prototype with no `users` table or registration flow: a user is a stable owner identifier associated with an opaque token in runtime configuration.

## Local management API

Run commands from `src/url-shortener-api`:

```bash
export URL_SHORTENER_TOKEN="$(openssl rand -hex 32)"
dotnet user-secrets --project Api set "ManagementAuth:Tokens:0:Token" "$URL_SHORTENER_TOKEN"
dotnet user-secrets --project Api set "ManagementAuth:Tokens:0:OwnerId" "local-user-a"
dotnet ef database update --project Infrastructure --startup-project Api
dotnet run --project Api
curl -H "Authorization: Bearer $URL_SHORTENER_TOKEN" -H 'Content-Type: application/json' -d '{"url":"https://example.com"}' http://localhost:8080/api/v1/short-urls
unset URL_SHORTENER_TOKEN
```

Add a second owner with `ManagementAuth:Tokens:1:Token` and `ManagementAuth:Tokens:1:OwnerId`. List or remove local entries with:

```bash
dotnet user-secrets list --project Api
dotnet user-secrets remove ManagementAuth:Tokens:0:Token --project Api
```

For non-user-secrets environments, use `ManagementAuth__Tokens__0__Token` and `ManagementAuth__Tokens__0__OwnerId` (and index `1` for another owner). Configure `ConnectionStrings__PostgreSql` and `PublicBaseUrl` through the environment as appropriate; never commit populated credentials.

## Local Redis cache

The development Compose file starts Redis on `localhost:6379` and configures the API automatically. Redis is optional: if unavailable, the API continues with PostgreSQL. Cache entries use a sliding five-minute TTL and cache operations fall back after 100 ms. Override `Cache:TtlSeconds`, `Cache:TimeoutMilliseconds`, or `ConnectionStrings:Redis` as needed.

Optional Redis integration coverage: `URL_SHORTENER_TEST_REDIS=localhost:6379 dotnet test src/url-shortener-api/Infrastructure/tests/Infrastructure.Tests.csproj -m:1`.

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

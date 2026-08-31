# Production deployment

GitHub Actions runs the complete solution test suite against PostgreSQL and
Redis before deployment. Pushes to `main` deploy only after a successful test
job. Manual workflow runs also execute tests first, but a failed test job does
not block the requested recovery deployment.

The workflow builds `src/url-shortener-api/Dockerfile`, transfers the exact
commit-tagged image to the VPS, runs the EF Core migration bundle, starts the
Compose services, updates the marked URL Shortener block in the shared Caddy
configuration, and verifies both public hosts.

## Network boundary

- `short_internal` is Docker-internal. PostgreSQL and Redis attach only to this
  network. API and Aspire also use it for database, cache, and OTLP traffic.
- `short_public` is shared with Caddy. Only API and Aspire attach to it. No URL
  Shortener service publishes a host port.

Caddy serves the API at `https://short.unsolo.dev` and protects
`https://aspire-short.unsolo.dev` with HTTP Basic authentication.

## Configuration

The repository requires these GitHub Actions secrets:

- `ASPIRE_USERNAME`
- `ASPIRE_PASS`
- `UNSOLODEV_SERVER_HOST`
- `UNSOLODEV_SERVER_USER`
- `UNSOLODEV_SSH_KEY`

Runtime-only application and database values live in
`/home/deploy/url-shortener/deploy/.env.runtime` on the VPS:

```dotenv
POSTGRES_DB=url_shortener
POSTGRES_USER=url_shortener
POSTGRES_PASSWORD=<generated-password>
URL_SHORTENER_TOKEN=<management-api-token>
URL_SHORTENER_OWNER_ID=braian
```

The deployment script manages `API_IMAGE` in the same file. Do not commit the
runtime file, copy the management token into GitHub, or remove the named
`short-postgres-data` volume during normal deployments.

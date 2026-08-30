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

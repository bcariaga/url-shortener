# Technical specification

## Layer correction

Application owns the accepted shape of its use-case commands. Move these types
from `Infrastructure/Validators` into an Application validators directory and
namespace:

- `CreateShortUrlCommandValidator`;
- `UpdateShortUrlCommandValidator`;
- `DeleteShortUrlCommandValidator`;
- `ValidationRuleExtensions`.

Keep one declared type per file. The validation rules and error property names
remain unchanged so the API can continue distinguishing an invalid route code
from an invalid destination.

## Dependencies and registration

Add the existing pinned `FluentValidation` and
`FluentValidation.DependencyInjectionExtensions` package references to
`Application.csproj`. Remove both from `Infrastructure.csproj`.

`Application.DependencyInjection.AddApplication` registers validators from the
Application assembly alongside Mediary handlers. Remove validator scanning and
FluentValidation imports from `Infrastructure.DependencyInjection`.

API keeps its existing `IValidator<CreateShortUrlCommand>`,
`IValidator<UpdateShortUrlCommand>`, and
`IValidator<DeleteShortUrlCommand>` constructor dependencies. No controller
logic or response mapping changes are required.

## Tests

Move `ShortUrlCommandValidatorTests` from Infrastructure tests to Application
tests and update its namespace. Preserve coverage for:

- missing, relative, unsupported-scheme, and missing-host destinations;
- valid HTTP and HTTPS destinations;
- the 2,048-character accepted boundary and 2,049-character rejection;
- invalid and valid six-character Base62 codes.

Do not duplicate validator tests across layers.

## Documentation

Update `docs/development-guidelines.md` to state:

- Application contains command validators and reusable input rules;
- Application registration exposes them to API;
- Infrastructure remains focused on implementations and anti-corruption around
  external/framework-specific concerns;
- API still executes validators and maps their results to HTTP.

## Validation

Run from the repository root:

```bash
dotnet restore src/url-shortener-api/UrlShortener.sln
dotnet build src/url-shortener-api/UrlShortener.sln --no-restore \
  --disable-build-servers -m:1 /p:UseSharedCompilation=false
dotnet test src/url-shortener-api/UrlShortener.sln --no-build --no-restore \
  --disable-build-servers -m:1 /p:UseSharedCompilation=false
git diff --check
```

Also verify statically that Application contains the validator files and
FluentValidation packages, while Infrastructure contains neither.

## Open questions

None.

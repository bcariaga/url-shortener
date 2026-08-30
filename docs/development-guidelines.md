# Development guidelines

These rules are authoritative for implementation work in this repository. Bob
must read them before changing production code or tests.

## Delivery order

1. Establish the intended layer boundaries and remove obsolete or duplicate
   implementations.
2. Complete one use-case flow at a time, from its HTTP request through its
   application handler and Domain/Infrastructure collaborators.
3. Update the affected tests after the production contract is coherent, then
   run focused tests for the slice.
4. Finish with the complete solution build and test suite.

Do not preserve an obsolete type or implementation solely to keep an old test
compiling. Adapt the test to the resulting public contract and retain the
behavioral coverage.

## Layer responsibilities

### API

- Contains controllers and HTTP-specific authentication and configuration.
- Owns the translation from expected application outcomes or exceptions to
  HTTP responses and Problem Details.
- Request models must match the data expected by the dispatched command. The
  controller adds trusted values obtained from HTTP context, such as the owner
  claim or route short code, when it constructs that command.
- Controllers must resolve and execute the relevant FluentValidation validator
  before dispatching a command. Invalid input is rejected as `400 Bad Request`
  without invoking the handler.
- Do not put business rules, persistence logic, or duplicate ad-hoc validation
  in controllers.

### Application

- Contains commands, representations, use-case exceptions, and handlers.
- Contains validators for application commands and reusable input validation rules.
- Its dependency-injection entry point registers validators so API can resolve them.
- A handler is the procedural orchestrator for one use case. Its public surface
  consists only of `HandleAsync`; private methods are allowed only when they
  materially improve readability.
- Handlers coordinate collaborators declared by Domain and implemented by
  Infrastructure. They do not depend on EF Core, ASP.NET Core, concrete clocks,
  concrete generators, or configuration APIs.
- Keep each operation in a focused command/handler pair. Do not create a
  manager containing unrelated commands, contracts, handlers, and exceptions.

### Domain

- Defines the complete shape of the business: entities, invariants, repository
  and service contracts, and domain-level outcomes needed by those contracts.
- Has no knowledge of HTTP, EF Core, `HttpClient`, configuration, or other
  infrastructure concerns.
- Domain behavior belongs on the relevant entity when it changes or protects
  that entity's state.

### Infrastructure

- Implements Domain collaborators and acts as the anti-corruption layer around
  EF Core, system time, hashing/generation, configuration, `HttpClient`, and
  other external or framework-specific details.
- Converts infrastructure-specific failures into the domain/application
  outcomes understood by handlers; framework exceptions must not leak into
  Domain.

## File and code structure

- Every hand-written C# file contains exactly one top-level class, record,
  interface, or other declared type. This rule also applies to test fixtures
  and test doubles; split them into focused files.
- File names match their declared type.
- Do not nest request models or test doubles inside controllers or test classes.
- Generated EF Core migration files are exempt because their structure is
  controlled by EF tooling.
- Prefer clear, formatted multi-line code over compressed one-line members or
  multiple statements on one line.
- Use the existing namespaces and directory structure to make the owning layer
  and use case evident.

## Validation and tests

- FluentValidation owns command input validation. Tests cover validators
  directly and prove that controllers invoke them before dispatch.
- Domain tests cover entity invariants and state transitions.
- Application tests exercise handlers with Domain collaborator test doubles;
  they do not depend on EF Core or ASP.NET Core.
- Infrastructure tests cover concrete adapters, EF mappings, and validator
  behavior where appropriate.
- API tests cover authentication, model binding, validation-to-HTTP mapping,
  application-outcome mapping, and response contracts.
- A refactor is complete only when `dotnet build` succeeds and all relevant
  tests pass without weakening the behavior specified for create, update, and
  delete.

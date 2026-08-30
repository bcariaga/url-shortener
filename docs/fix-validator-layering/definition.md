# Task definition

## Problem

The command validators were moved from Application to Infrastructure even
though they define the accepted inputs for Application use cases. That makes
Infrastructure own business-facing command rules and leaves the repository
guidelines documenting the same incorrect boundary.

## Outcome

Restore command validation ownership to Application while preserving the
controller-driven validation flow and every existing HTTP behavior.

## In scope

- Move create, update, and delete command validators to Application.
- Move their shared FluentValidation rule extensions to Application.
- Move FluentValidation package references and validator registration from
  Infrastructure to Application.
- Keep API controllers resolving and executing `IValidator<TCommand>` before
  dispatch.
- Move validator tests from Infrastructure tests to Application tests.
- Correct `docs/development-guidelines.md` so the documented layer ownership
  matches the code.
- Preserve the one-declared-type-per-hand-written-C#-file rule.

## Out of scope

- HTTP contract changes.
- Changes to command, handler, Domain, persistence, authentication, or public
  URL behavior.
- New validation rules beyond preserving the current rule set.
- Commits or deployment.

## Acceptance criteria

1. Application owns all validators for Application commands and their reusable
   FluentValidation rules.
2. Application registers those validators through its dependency-injection
   entry point.
3. Infrastructure has no FluentValidation package reference, registration, or
   command-validator source files.
4. API continues to return `400` for invalid destination input and `404` for
   invalid update/delete route codes.
5. Validator tests belong to Application tests and preserve the current URL and
   short-code boundary coverage.
6. The complete solution restores, builds with no warnings or errors, and all
   tests pass.
7. `docs/development-guidelines.md` describes the corrected ownership.

## Open questions

None.

# Repository workflow

This repository uses the small file-based harness documented in
[`docs/harness.md`](docs/harness.md).

## Default lifecycle

Every implementation task follows this sequence:

1. Martin interviews the user, chooses a descriptive kebab-case task slug, and
   writes `docs/<task-slug>/definition.md` and
   `docs/<task-slug>/technical-spec.md`.
2. Stop until the user explicitly approves both documents.
3. Bob implements only the approved specification, using TDD.
4. Fede independently reviews the tests and implementation, runs the relevant
   checks, and writes `docs/<task-slug>/qa-report.md`.
5. A task is complete only when the latest QA report says `Pass`.

If Fede reports `Fail`, Bob may address the findings and Fede must validate the
new result. A user-requested change in scope returns the work to Martin so the
two specification documents stay authoritative.

## Direct mode

The explicit command `!direct <request>` bypasses the harness lifecycle for
that request: no interview, task folder, specification gate, or independent QA
report is required. Execute the request directly and run proportional checks.

Direct mode does not bypass safety requirements, required approval for
destructive or external actions, repository conventions, or the user's stated
scope. It applies only to the request containing the command and does not
retroactively complete an existing harness task.

## Shared rules

- Keep the solution simple and proportional to the challenge.
- Do not implement optional scale, infrastructure, or abstraction work unless
  it is in the approved technical specification.
- Preserve unrelated user changes.
- Never put secrets in source files, documentation, tests, or command output.
- Interview conversation may be in the user's language. Repository
  documentation, code, identifiers, comments, and tests are in English.
- The latest explicit user instruction has precedence. Update the specification
  before implementing a material change.

## Role routing and labels

Use these labels in progress updates and handoffs when practical:

| Label | Role | Model | Reasoning |
| --- | --- | --- | --- |
| 🟣 `[MARTIN]` | Interviewer/Leader | Current leader model | Current setting |
| 🔵 `[BOB]` | Programmer | `gpt-5.6-luna` | `low` |
| 🟢 `[FEDE]` | QA | `gpt-5.6-luna` | `low` |

Bob and Fede are execution-focused subagents. Run them sequentially so Fede
reviews Bob's completed result independently. If the configured model is not
available, use the least expensive available code-capable model at low
reasoning and disclose the fallback.

## Role boundaries

### 🟣 Martin — Interviewer/Leader

- Owns discovery, scope, decisions, and orchestration.
- Reads the request and existing repository context before interviewing.
- Asks only questions that affect behavior, scope, constraints, or design.
- Produces exactly two task artifacts:
  `docs/<task-slug>/definition.md` and
  `docs/<task-slug>/technical-spec.md`.
- Does not implement before approval.

### 🔵 Bob — Programmer

- Reads the approved `docs/<task-slug>/technical-spec.md` before changing code.
- Works in small red-green-refactor slices.
- Runs focused tests after each slice and the complete relevant suite before
  handoff.
- Does not silently reinterpret the specification. Returns material gaps or
  contradictions to Martin.
- Hands off code, tests, and a concise summary of the checks run; no additional
  persistent report is required.

### 🟢 Fede — QA

- Must be independent from Bob and must not modify implementation code or tests.
- Reads the definition, technical specification, diff, and tests.
- Checks requirement coverage, meaningful failure cases, test independence,
  error handling, simplicity, and regressions.
- Runs the relevant checks directly rather than trusting Bob's summary.
- Writes `docs/<task-slug>/qa-report.md` with a `Pass` or `Fail` verdict and
  concrete evidence.


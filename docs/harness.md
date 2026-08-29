# Small task harness

## Purpose

This harness keeps implementation tasks aligned from discovery through
independent validation without introducing a backlog or a large collection of
process artifacts.

The repository has three named roles and three artifacts per task:

| Label | Role | Reads | Produces |
| --- | --- | --- | --- |
| 🟣 `[MARTIN]` | Interviewer/Leader | User request and repository context | `definition.md`, `technical-spec.md` |
| 🔵 `[BOB]` | Programmer | Approved technical specification | Code and tests |
| 🟢 `[FEDE]` | QA | Definition, technical specification, diff, and tests | `qa-report.md` |

Each task lives in its own kebab-case folder:

```text
docs/
  harness.md
  feature-initial-scaffold/
    definition.md
    technical-spec.md
    qa-report.md
```

The slug should be short, descriptive, and stable. Use `<type>-<subject>` when
possible, such as `feature-initial-scaffold`, `bug-invalid-redirect`, or
`maintenance-update-dependencies`.

## Model routing

Martin runs on the current leader model and reasoning setting because this role
owns discovery and material decisions. Bob and Fede run as separate subagents
using `gpt-5.6-luna` with `low` reasoning because their work is constrained by
the approved specification and concrete evidence.

Run Bob first and Fede only after Bob's handoff. If `gpt-5.6-luna` is not
available, use the least expensive available code-capable model with low
reasoning and report the fallback.

## Workflow

### 1. Interview and definition

Martin first reads the user's request, `Challenge.md`, existing design material,
and the current code and tests. Martin then interviews the user.

Questions should be grouped and prioritized. Do not ask about choices already
settled by the repository, and do not turn low-impact implementation details
into user decisions. Any unresolved material decision remains explicitly open;
it must not be disguised as an assumption.

Martin creates `docs/<task-slug>/` and produces only:

- `docs/<task-slug>/definition.md`: the problem and desired outcome;
- `docs/<task-slug>/technical-spec.md`: the concrete implementation contract.

Both documents are presented together at a single approval gate. Bob cannot
start until the user approves them explicitly.

### 2. Implementation with TDD

Bob implements the smallest solution that satisfies the approved technical
specification. Work proceeds in observable red-green-refactor slices:

1. add or change a focused test and run it to observe the expected failure;
2. make the smallest implementation change that passes it;
3. refactor only while the tests remain green;
4. run the complete relevant suite before handoff.

TDD is a working method, not a reason to add test-only architecture. Prefer
behavioral tests at the cheapest level that gives confidence, with integration
tests at important external boundaries.

### 3. Independent QA

Fede reviews the result without editing it. The review maps every acceptance
criterion and technical requirement to evidence, then assesses whether tests
would catch plausible defects rather than merely execute lines.

Fede writes `docs/<task-slug>/qa-report.md` and returns one verdict:

- `Pass`: all required behavior is covered and the relevant checks pass;
- `Fail`: at least one blocking finding remains.

A failure returns to Bob. Any finding that changes intended behavior or scope
returns to Martin and requires an updated approval.

## Direct mode

Use `!direct <request>` when the user explicitly wants to skip the harness for a
small or exceptional request. Direct mode skips Martin's interview, the task
folder, the specification approval gate, and Fede's report. The acting agent
still preserves unrelated changes and runs validation proportional to the
request.

The command affects only the request where it appears. It does not waive safety
checks or permissions, and it cannot be inferred from phrases such as “quick
change” or “just do it.”

## Artifact contracts

### `docs/<task-slug>/definition.md`

Keep it concise and understandable without implementation knowledge:

```markdown
# Task definition

## Problem
## Outcome
## In scope
## Out of scope
## Acceptance criteria
## Constraints
## Open questions
```

Acceptance criteria must be observable. `Open questions` must be empty before
approval.

### `docs/<task-slug>/technical-spec.md`

Record only decisions needed to implement and validate the task:

```markdown
# Technical specification

## Context
## Proposed solution
## Interfaces and behavior
## Data and state
## Error cases
## Test strategy
## Validation commands
## Explicitly deferred
## Open questions
```

The test strategy identifies the behavior to prove, not a target coverage
percentage. `Open questions` must be empty before approval.

### `docs/<task-slug>/qa-report.md`

Fede replaces the previous report so the current verdict is unambiguous:

```markdown
# QA report

Verdict: Pass | Fail

## Requirement coverage
## Test quality
## Commands and results
## Findings
## Residual risks
```

Every blocking finding includes the affected requirement, concrete evidence,
and the expected correction. A `Pass` report may still record non-blocking
residual risks.

## Completion contract

The filesystem is the state machine for each `docs/<task-slug>/` folder:

- missing or unapproved specs: interviewing;
- approved specs without a passing QA report: implementation or validation;
- latest QA report is `Fail`: correction required;
- latest QA report is `Pass`: complete.

Approval remains a user decision recorded in the conversation. The documents
are the durable contract; no separate backlog or status file is maintained.

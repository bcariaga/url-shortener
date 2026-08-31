# QA report

Verdict: Pass

## Requirement coverage

- `Program.cs` only activates `app.UseExceptionHandler()`; DI registers the known and fallback handlers.
- The create controller has no exception translation. Capacity exhaustion is centrally mapped to safe 503 Problem Details.
- Known Application/Domain exceptions expose status, title, safe detail, stable `code`, and `traceId`; unexpected failures expose only generic 500 and are logged with EventId 1002.
- Domain guards use custom exception types. Validation and not-found/ownership behavior remain controller results.
- The existing repository Activity emits all seven specified events: `cache.hit`, `cache.miss`, `cache.invalid_value`, `cache.read.timeout`, `cache.read.error`, `cache.write.timeout`, and `cache.write.error`.
- `CacheReadOutcome` prevents timeout/error paths from also emitting `cache.miss`; tests assert this for read errors. Error event tags are restricted to `exception.type`, and unsampled Activity behavior is event-free.

## Test quality

ActivityListener-based Infrastructure tests verify hit, miss, malformed value, read degradation, write timeout/error, tag safety, and unsampled behavior. Existing API, Application, Domain, and Infrastructure tests remain independent. The unexpected-error integration test verifies message, URL, owner, token, and duplicate-log non-leakage.

## Commands and results

```text
dotnet restore UrlShortener.sln                  PASS
dotnet build UrlShortener.sln --no-restore -m:1  PASS (0 warnings, 0 errors)
dotnet test UrlShortener.sln --no-build -m:1     PASS (67 tests: 7 + 24 + 6 + 30)
git diff --check                                PASS
```

## Findings

No blocking findings.

## Residual risks

A manual trace check with real Redis miss-then-hit and Redis unavailability remains useful for demo confidence. Remove operations use the same write-event path.

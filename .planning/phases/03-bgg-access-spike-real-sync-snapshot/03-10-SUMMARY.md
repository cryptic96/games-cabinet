---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 10
subsystem: sync
tags: [failure-classification, queued-answers, shrink-guard, held-back, snapshot-safety]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: coordinator, worker, runner and persisted state (03-07, 03-08), faithful parser with skipped-entry count (03-09), scripted BGG handler (03-06), signed-off measurements (03-02)
provides:
  - "BggClient failure classification: 401 Unauthorized, 429/503 Throttled, 403/3xx/other Unavailable, non-XML 200 BadAnswer, timeouts and cancellation Timeout"
  - "Bounded 202 polling through the shared pacer: BggClient.QueuedWaits (5, 10, 20, 30, 30, 30 s) and MaxRequestsPerSync = 16"
  - "ShrinkGuard (pure) and GuardDecision: empty is always held back, more than half removed is held back until the same id set repeats"
  - "SyncRunResult.HeldBack, SyncRunner.RunAsync(HeldBackRecord?, CancellationToken), held-back record stored, kept or cleared by SyncCoordinator.Complete"
  - "Operations notes: When BGG misbehaves, Showing a genuinely empty collection"
affects: [page status line and calm note, owner tools, public exposure]
tech-stack:
  added: []
  patterns:
    - "One request per method call (RequestOnceAsync) so the pacer turn ends before a poll wait and a wait is never held inside a turn"
    - "A request budget object shared by both calls of one sync"
    - "Switchable scripted BGG source for integration tests: serves entries until told to fail"
key-files:
  created:
    - Cabinet.Domain/Collection/ShrinkGuard.cs
    - Cabinet.UnitTests/Bgg/BggFailureTests.cs
    - Cabinet.UnitTests/Bgg/QueuedAnswerTests.cs
    - Cabinet.UnitTests/Bgg/BggTestKit.cs
    - Cabinet.UnitTests/Sync/ShrinkGuardTests.cs
    - Cabinet.IntegrationTests/SyncFailureTests.cs
    - Cabinet.IntegrationTests/HeldBackTests.cs
    - Cabinet.IntegrationTests/Infrastructure/SyncRounds.cs
  modified:
    - Cabinet.Repository/Bgg/BggClient.cs
    - Cabinet.Service/Sync/SyncRunner.cs
    - Cabinet.Service/Sync/SyncWorker.cs
    - Cabinet.Service/Sync/SyncCoordinator.cs
    - docs/bgg-sync.md
key-decisions:
  - "The declared total stays compared with parsed plus skipped owned entries (the 03-09 rule), not with every item element before the owned filter; the requests always carry own=1, so the two agree on real answers"
  - "A cancelled caller token ends the fetch as Timeout (as the plan states), including a shutdown during a run; it is recorded as a failed run like the earlier unavailable result"
  - "BggClient takes the TimeProvider as a required constructor argument and an optional wait schedule, so tests can run a longer schedule to reach the request budget; DI fills the time provider and leaves the schedule at its default"
  - "A held-back result leaves ConsecutiveFailures and LastSuccessUtc alone; only changed or unchanged moves the success time"
requirements-completed: [SYNC-04, SYNC-01]
duration: ~50 min
completed: 2026-10-07
status: complete
actuals:
  tokens: 45000
  tasks: 3
  commits: 3
---
# Phase 3 Plan 10: Failures and held-back results never change the cabinet Summary

**Every way a BGG sync can fail is classified and leaves the stored and shown collection untouched, queued answers are polled on the signed-off 5, 10, 20, 30 second schedule within a 16-request budget, and an empty or more-than-halved result is held back until confirmed (empty never).**

## Accomplishments
- `BggClient` examines each answer before the next request: 401 ends the run as Unauthorized with the body unread, 429 and 503 as Throttled (a `Retry-After` is ignored), 403, any redirect and every other non-success status as Unavailable, a 200 that is not `text/xml` or `application/xml` as BadAnswer, connection errors as Unavailable, an HttpClient timeout or a cancelled caller as Timeout. Parser failures (errors document, wrong root, malformed XML, a DOCTYPE) and a total mismatch are BadAnswer. A failure in the expansion call fails the whole fetch; no failure path returns a collection.
- A 202 is polled with `QueuedWaits` (5, 10, 20, 30, 30, 30 seconds, from the signed-off decision table) through the shared pacer, then ends as Queued. One request is sent per `RequestOnceAsync` call, so the pacer turn is released before every wait. `MaxRequestsPerSync = 16` is shared by both calls.
- `ShrinkGuard.Evaluate(previousCount, candidateEntryIds, heldBack)` is pure: first sync accepts, empty over shown games is always held back as `Empty`, strictly more than half removed is held back as `Shrunk` with the SHA-256 fingerprint of the sorted ids, and the same set on the next sync (any order) is accepted; a different set replaces the record.
- `SyncRunner.RunAsync(previousHeldBack, ct)` evaluates every fetched result before anything is written and returns `SyncRunResult(HeldBack, None, record)`; `SyncWorker` passes `coordinator.State.HeldBack`; `SyncCoordinator.Complete` stores the record for a held-back result (last success unmoved), clears it on changed or unchanged, and keeps it on a failure. The status payload's `heldBack` and `lastResult` therefore light up as the earlier plan prepared.
- Tests: 16 + 5 + 3 + 1 failure-class tests and 7 queued-answer tests (`Bgg` unit trait, 68 total), 16 guard tests (`Sync` unit trait), 9 + 3 failure integration tests (byte-identical `snapshot.json`, identical layout ETag, earlier `lastSyncedUtc`, no credential in the status) and 8 held-back integration tests including a restart, a failure between hold and confirmation, and the empty first sync with no being-filled note.
- `docs/bgg-sync.md`: "When BGG misbehaves" (failure table, polling rules, held-back rules) and "Showing a genuinely empty collection" (stop, delete `snapshot.json`, start).

## Task Commits
1. Task 1 (tracer): a failed sync leaves the cabinet exactly as it was: `33f16f0`
2. Task 2: queued answers are polled slowly, through the pacer, within the request budget: `d5b8c40`
3. Task 3: empty or more-than-halved results are held back until confirmed: `26acf44`

## Tracer gate
The tracer's verify (`Category=Bgg` unit tests and `Category=Sync` integration tests, which drive press, BGG answer, snapshot and layout) passed and was committed before any expansion task, and the same suites passed again after the later tasks. A parallel worktree agent is not resumed, so no mid-plan human checkpoint was raised; the check is fully automated.

## Deviations from Plan
**1. [Rule 3 - Blocking] BggClient constructor changed to a class with explicit fields**
- **Issue:** the plan wants the poll wait on an injected `TimeProvider` and a test route to the 16-request budget, which the default schedule (at most 14 requests) can never reach.
- **Fix:** the client now takes `TimeProvider` (always supplied by the existing singleton) and an optional wait schedule (default `QueuedWaits`). No other call site constructed the client directly.
- **Commit:** `33f16f0`
**2. Parser left unchanged**
- The parser already rejected a root other than `items` (which covers an errors document), prohibited DTDs and counted skipped entries, so the plan's parser checks held without edits. The total check stays in the client with the 03-09 parsed-plus-skipped rule (the project rule says to keep it).
**3. Additions beyond the plan's file list**
- `Cabinet.UnitTests/Bgg/BggTestKit.cs` (client factory, immediate pacer) and `Cabinet.IntegrationTests/Infrastructure/SyncRounds.cs` (press and wait on the fake clock, layout reading, a switchable scripted BGG) are small shared test helpers; neither touches files the parallel page plan edits.
**4. A cancelled caller is a timeout, including shutdown**
- Following the plan, any cancellation of the caller's token returns Timeout from the client, so a service stop during a run records a failed run. The previous behaviour (exception, worker maps to Unavailable) recorded a failed run too.

## Known Stubs
None.

## Threat Flags
None beyond the register: T-03-40 (content type, root, DTD, total and status checks, one integration test per answer class), T-03-41 (pure guard before any write), T-03-42 (bounded schedule, 16-request budget, stop on 401/429/503, existing 10-minute run limit), T-03-43 (categories only in logs and state; the status carries no category or count), T-03-44 (held-back record with time in `sync-state.json`, documented).

## Notes for later plans
- The page plan can now read `heldBack` and `lastResult: "heldBack"` from the status route; the calm note after 3 hours (`staleAfterSeconds`) is described in the docs and left to the page.
- A held-back answer is confirmed by any later sync, hourly or manual, including after a restart (the record is persisted).

## Self-Check: PASSED
- Files exist: all created files above and this summary.
- Commits `33f16f0`, `d5b8c40`, `26acf44` exist on the worktree branch.
- `dotnet test --solution Cabinet.slnx`: 742 passed, 0 failed; `build/lint.sh` passes.

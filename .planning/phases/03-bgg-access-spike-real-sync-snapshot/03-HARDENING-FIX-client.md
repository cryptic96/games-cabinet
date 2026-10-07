---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07T00:00:00Z
review_path: .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SECURITY.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 3: Hardening Fix Report (BGG client and sync worker)

**Scope:** the retry floor, the whole-run limit on the injected clock, and the docs, as asked by the owner.

**Verification:** run in the isolated worktree (not the main checkout) after each commit: `dotnet test --solution Cabinet.slnx` (949 tests after the last code commit, 0 failed) and `build/lint.sh repo-rules` (pass). No test waits in real time for a retry or for the limit.

## Fixed Issues

### Retry floor without Retry-After

**Status:** fixed: requires human verification (retry policy)
**Files modified:** `Cabinet.Repository/Bgg/BggClient.cs`, `Cabinet.Service/Sync/SyncWorker.cs` (the limit became public so a test can compare it), `Cabinet.UnitTests/Bgg/BggRetryTests.cs`, `Cabinet.UnitTests/Bgg/BggFailureTests.cs`, `Cabinet.UnitTests/Bgg/BggTestKit.cs`, `Cabinet.IntegrationTests/SyncFailureTests.cs`, `Cabinet.IntegrationTests/Infrastructure/SyncRounds.cs`
**Commit:** deb85e6
**Applied fix:** `BggClient.RetryFloor` (30 seconds, a named constant). A 429 or 503 whose answer carries no usable `Retry-After` (absent or unreadable) waits the floor on the injected clock before its single retry, in addition to the pacer's gap. A readable `Retry-After` is still honoured as asked, including values below the floor and zero (BGG named the wait, so it is not raised); more than 60 seconds still means no retry. Other 5xx answers keep the pacer gap. Unchanged: one retry per call, counted in the 16-request budget and skipped when the budget is spent, never for 401, 403, other 4xx or redirects.
**Tests:** both throttle statuses wait at least the floor; the retry does not start at floor minus one second and does start after it; an unreadable header counts as none; a header of 5 seconds is honoured and not raised; 500, 502 and 504 do not wait the floor; a repeated throttle is still not retried twice; and an arithmetic test shows that every queued wait, the longest retry wait for each call and all 16 pacer gaps together stay below five sixths of the whole-run limit. Existing failure tests that drive a throttle now run on a fake clock (`BggTestKit.FetchOnFakeClockAsync`), and the three integration tests that fail a sync with a throttle move the fake clock while waiting (`moveClockWhileWaiting` on `SyncRounds.PressAndWait`, whose end-of-run check became "at or after the press").
**Whole-run bound:** worst case without HTTP stalls is 250 s of queued waits, 120 s of retry waits and 80 s of gaps, about 450 s, under the 600 s limit; a stalled request is what the limit exists for.

### Whole-run limit on the injected clock

**Files modified:** `Cabinet.Service/Sync/SyncWorker.cs`, `Cabinet.UnitTests/Sync/SyncWorkerTests.cs`, `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-VALIDATION.md`
**Commit:** 2672f05
**Applied fix:** `SyncWorker` takes the `TimeProvider` (already registered in the host) and creates the limit as `new CancellationTokenSource(RunLimit, time)`, linked with the service stop token. The decision between a timeout and a stop is unchanged (a cancellation while the stop token is not cancelled is a timeout). No change to `SyncRunner.cs` or `SyncStartup.cs` was needed.
**Tests:** `A_stalled_run_is_recorded_as_a_timeout_once_the_clock_passes_the_whole_run_limit` (still running one second before the limit, recorded as a timeout at it, 10 minutes asserted) and `A_stop_is_still_not_recorded_when_the_clock_passes_the_whole_run_limit_afterwards`.
**Validation record:** the manual-only row for the limit is removed; the SYNC-04 row lists both tests and the retry-floor tests; the audit counts now say no escalations remain.

### Docs

**Files modified:** `docs/bgg-sync.md`
**Commit:** e457d01
**Applied fix:** the retry list documents the 30-second floor (no or unreadable `Retry-After`, 429 and 503 only, other 5xx keep the gap); the whole-run paragraph now says the limit runs on the same clock as the waits, that the longest possible waits fit well under it, and that a limit timeout is recorded while a service stop is not.

## Skipped Issues

None.

---

_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

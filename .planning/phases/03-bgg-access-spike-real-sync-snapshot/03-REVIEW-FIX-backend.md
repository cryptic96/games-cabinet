---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07T00:00:00Z
review_path: .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 3: Code Review Fix Report (back end)

**Source review:** .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md (Part: Back end)
**Iteration:** 1

**Scope:** WR-01, WR-03 and WR-04 only. WR-02 (owner decision pending) and all IN- items were deliberately not touched.

**Summary:**
- Findings in scope: 3
- Fixed: 3
- Skipped: 0

**Verification:** `dotnet test --solution Cabinet.slnx` (812 tests, 0 failed) and `build/lint.sh repo-rules` (pass) ran in the isolated worktree before the last fix commit. The new pacer test was confirmed to fail against the old pacer.

## Fixed Issues

### WR-01: Host shutdown (and the run limit) is recorded as a BGG timeout

**Status:** fixed: requires human verification (cancellation classification logic)
**Files modified:** `Cabinet.Repository/Bgg/BggClient.cs`, `Cabinet.Service/Sync/SyncWorker.cs`, `Cabinet.UnitTests/Bgg/BggFailureTests.cs`, `Cabinet.UnitTests/Sync/SyncWorkerTests.cs` (new)
**Commit:** 33ef7a2
**Applied fix:** The client now maps a cancellation to a timeout only when the caller's token was not cancelled, so a stop propagates. The worker lost its catch-all cancellation branch that turned a stop into an "unavailable" failure: the run limit still ends as a timeout (the linked token is cancelled, the stop token is not), and a real stop propagates to the loop's existing handler, which returns without calling the coordinator's completion, so no result, failure category or failure count is written for an interrupted run. The cooldown window stays persisted as before. The existing test that expected a timeout for a cancelled caller now expects the cancellation and no request. A new worker test proves a stop mid-run leaves the stored state untouched and that an ordinary failed run is still recorded. The run-limit path is not covered by a test, because the 10 minute limit runs on real time.

### WR-03: The request pacer measures the gap with the wall clock

**Files modified:** `Cabinet.Repository/Bgg/RequestPacer.cs`, `Cabinet.UnitTests/Bgg/RequestPacerTests.cs`
**Commit:** 64d8640
**Applied fix:** The pacer stores the monotonic timestamp at the end of each request and computes the wait from the elapsed time, capped at the configured gap. The 5 s floor, the turn-per-caller behaviour and the end-of-request measurement are unchanged. A new test steps the wall clock back an hour between two requests and proves the second still starts after the gap; it fails against the old pacer.

### WR-04: A transient read error moves the last good snapshot aside

**Files modified:** `Cabinet.Repository/Storage/SnapshotStore.cs`, `Cabinet.Repository/Storage/SyncStateStore.cs`, `Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs`, `Cabinet.UnitTests/Snapshot/SyncStateStoreTests.cs`
**Commit:** 0f92104
**Applied fix:** A read that fails with an I/O or permission error now logs one category line ("unreadable", file left in place) and returns no snapshot (or the initial state for the bookkeeping) without renaming the file. Setting aside is kept for content that was read and found malformed or too new. Both stores got a test that holds the file under an exclusive lock, loads, checks nothing was moved and one log line was written, then releases the lock and loads the real content.

## Skipped Issues

None. WR-02 and the IN- items were out of scope for this run by instruction.

---

_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

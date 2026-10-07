---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07T00:00:00Z
review_path: .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
iteration: 2
findings_in_scope: 6
fixed: 6
skipped: 0
status: all_fixed
---

# Phase 3: Code Review Fix Report (back end, second round)

**Source review:** .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md (Part: Back end)
**Iteration:** 2

**Scope:** WR-02 and IN-01 to IN-05, with the owner's decisions for this round (WR-02 "middle" option; IN-01 retry only through the pacer and the budget; IN-02 daily back-off; IN-03 bounded broadcast; IN-04 timer after the delay; IN-05 invisible-character stripping). The docs that belong to this area were updated in a separate commit.

**Summary:**
- Findings in scope: 6
- Fixed: 6
- Skipped: 0

**Verification:** run in the isolated worktree (not the main checkout) after each fix: `dotnet test --solution Cabinet.slnx` (877 tests after the last commit, 0 failed) and `build/lint.sh repo-rules` (pass). One unrelated scheduler test (`One_scheduled_request_is_made_per_interval`) failed once, during a run where the integration suite was also timing out for the retry change below; it passed in every other run and was not touched. The new interval-timer test was confirmed to fail against the old scheduler.

## Fixed Issues

### WR-02: The declared total was optional, and skipped entries were silent

**Status:** fixed: requires human verification (integrity-check logic)
**Files modified:** `Cabinet.Repository/Bgg/BggClient.cs`, `Cabinet.UnitTests/Bgg/BggDeclaredTotalTests.cs` (new)
**Commit:** e27c4fc
**Applied fix:** A collection answer with no `totalitems`, or one that does not parse, is now a bad answer. The declared total is still compared with parsed plus skipped entries, so one entry without a usable identifier does not reject a complete collection. Whenever entries were skipped the client logs the number (a warning, count only, no title or id). The client gained an optional logger parameter (registered typed-client construction picks it up). Tests: missing total rejected, unparseable total rejected, an empty collection declaring zero accepted, a skipped entry tolerated, a total that even the skipped entries do not explain rejected, the skipped count logged without titles or ids, and nothing logged for a complete answer.

### IN-05: `CleanTitle` stripped only characters below U+0020

**Files modified:** `Cabinet.Repository/Bgg/BggCollectionParser.cs`, `Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs`
**Commit:** 69fae72
**Applied fix:** Titles (and the location text, which shares the cleaner) now also lose DEL, the C1 controls, U+2028, U+2029, the bidirectional embedding and override characters U+202A to U+202E and the isolates U+2066 to U+2069, via `char.IsControl` plus those ranges. Nothing else about a title changes. Tests cover each removed character at the middle and the edges, and prove that accented text, a non-breaking space, the left and right marks, a joiner, an unassigned neighbour of the isolate range and an emoji are left alone. The test source uses escapes, never the literal characters.

### IN-01: Transient 5xx and 429 answers were not retried

**Status:** fixed: requires human verification (retry policy)
**Files modified:** `Cabinet.Repository/Bgg/BggClient.cs`, `Cabinet.UnitTests/Bgg/BggRetryTests.cs` (new), `Cabinet.UnitTests/Bgg/BggFailureTests.cs`, `Cabinet.UnitTests/Bgg/QueuedAnswerTests.cs`, `Cabinet.IntegrationTests/SyncFailureTests.cs`
**Commit:** 5d93e05
**Applied fix:** Implemented, inside the client and not as a resilience handler. A 429 or any 5xx answer is retried once per collection call. The retry is another `RequestOnceAsync` round, so it goes through the shared pacer (at least 5 s after the failed request finished) and takes a request from the per-sync budget; it is skipped when the budget is spent. A `Retry-After` header (seconds or date) adds a wait on top of the pacer's gap; a header asking for more than 60 s means no retry at all, because retrying early would ignore it and the next scheduled sync is the retry. 401, 403, other 4xx and redirects are never retried; a connection error or timeout is not retried either. The failure category of the final answer is unchanged (429 and 503 are throttled, other 5xx unavailable). Tests prove: each transient status retried once and the fetch recovering; each call having its own single retry; a repeated transient answer not retried twice; refusals and client errors never retried; the retry waiting its turn at the real pacer; `Retry-After` in seconds, as a date, at zero, at the cap and beyond the cap; the retry counting against the budget and not happening on the last request of the budget; a retry still allowed after a queued answer. Existing tests that assumed "fail at once" were updated: the refused-while-polling test now uses 401 (still stops at once), the failure theories expect the extra request, and the integration test for an expansion failure expects five requests.
**Not touched:** `SyncEndpoints.cs` needed no change; the client registration is unchanged.

### IN-04: The periodic timer started before the start-up delay

**Files modified:** `Cabinet.Service/Sync/SyncScheduler.cs`, `Cabinet.UnitTests/Sync/SyncSchedulerTests.cs`
**Commit:** 328d523
**Applied fix:** The `PeriodicTimer` is created after the start-up delay and the start-up request, so a delay longer than the interval cannot leave a tick pending beside the start-up request. The two existing start-up tests now wait for one timer (the delay) instead of two. A new test with an interval of 15 minutes and a start-up wait of up to one hour proves only the delay timer exists while waiting and the interval timer appears after the start-up request; it fails against the old scheduler.

### IN-03: The start-of-run broadcast had no time bound

**Files modified:** `Cabinet.Service/Live/LiveNotifier.cs`, `Cabinet.UnitTests/Live/HubLiveNotifierTests.cs` (new)
**Commit:** cdbd3e6
**Applied fix:** `HubLiveNotifier` waits for the hub send with a 5 second limit on an injectable `TimeProvider` (system clock when none is registered) and gives up with one warning line, so a slow or stuck connection can never delay or fail a sync; the send itself is not cancelled and any later failure of it is ignored. Tests use a hand-written hub context and a fake clock: a never-completing send is given up on exactly at the limit, a quick send logs nothing, a failing send is swallowed with only the exception type logged, and a host stop ends the wait without an exception.

### IN-02: A rejected token was retried every hour forever

**Status:** fixed: requires human verification (back-off rules)
**Files modified:** `Cabinet.Service/Sync/SyncCoordinator.cs`, `Cabinet.Service/Sync/SyncScheduler.cs`, `Cabinet.UnitTests/Sync/RejectedTokenBackoffTests.cs` (new)
**Commit:** 1426b81
**Applied fix:** The coordinator counts, in memory only, how many runs in a row ended as unauthorized. At three, timed syncs are paused: the new `TimedSyncsResumeAtUtc` is the last start plus 24 hours minus half an interval (so the tick nearest to a day later is the one that runs), and the scheduler skips hourly ticks before that moment. Any other result (success, another failure, held back) resets the count; so does a restart, because the count is not persisted, which makes "until a manual press succeeds or the service restarts" literal. A visitor's request and the start-up sync are never affected; a refused press moves the pause on. An interval of a day or more needs no pause. One warning line is logged when the pause begins. Tests (fake clock, no real BGG): not paused before three, paused at three with the exact moment, another failure resets, a success resets, a manual press still starts during the pause, a refused press moves it on, a restart clears it while the stored failure count remains, a daily interval needs none, the single log line, and the scheduler skipping 23 hours of ticks then asking at the 24th.

## Skipped Issues

None.

## Docs

`docs/bgg-sync.md` (committed as 788d50d) now documents: the declared-total rule and the skipped-entry tolerance and log; title cleaning; the single retry (pacer, `Retry-After` cap, budget, never for 401 or 403) and the updated failure table; the refused-token slow-down; the hourly timer armed after the start-up wait; the `Sync__StaleAfterHours` setting for the stale note (previously "3 hours"); and a new "Live updates" section (WebSockets and Server-Sent Events only, server-to-client pushes, `Live__MaxConnections` default 100, the 60 second status check as fallback, the 5 second push limit and the reverse-proxy requirement to pass WebSocket upgrades). It also corrects the snapshot and bookkeeping read-error wording left over from the previous round (an unreadable file is left in place, not set aside).

---

_Fixer: Claude (gsd-code-fixer)_
_Iteration: 2_

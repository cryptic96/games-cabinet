---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07
review_path: .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
iteration: 1
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 3: Code Review Fix Report (front end)

**Fixed at:** 2026-10-07
**Source review:** .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 4 (WR-05, WR-06, WR-07, WR-08; no Info items)
- Fixed: 4
- Skipped: 0

**Verification:** run in the isolated worktree, which has the same sources as the branch. `node --test build/tests/page-scripts.test.mjs` passed (40 tests), `dotnet test --solution Cabinet.slnx` passed (807 of 807) and `build/lint.sh repo-rules` passed. The new behaviour tests fail against the previous `sync.js` (4 of the 6 new tests) and pass against the fix.

## Fixed Issues

### WR-05: The button can stay on "Syncing..." until reload when a sync finishes before the 202 answer is built

**Files modified:** `Cabinet.Service/wwwroot/js/sync.js`, `Cabinet.Service/wwwroot/js/status.js`, `Cabinet.Service/wwwroot/js/cabinet.js`, `Cabinet.Service/Sync/SyncEndpoints.cs`, `Cabinet.IntegrationTests/SyncNowTests.cs`, `build/tests/page-scripts.test.mjs`
**Commit:** a0b2496 (shared with WR-08, because both change the same press function)
**Applied fix:**
- The own-press follow-up fetches the status at once, every 5 seconds, and once more at the 10-minute deadline, regardless of the live connection. The `isLiveConnected` option and its use in `cabinet.js` are gone.
- A poll generation token stops an older in-flight step from scheduling a second timer when a new press starts.
- New pure helper `ownSyncStillWaiting` in `status.js` decides whether to keep following, with `node --test` cases.
- Server side, the 202 body no longer forces `Running = true`. The coordinator sets the running flag under its lock before the answer is built, so the plain status is already consistent, and a forced flag could outrank a finished push. The existing integration test for the first press now accepts "running, or already finished" so it cannot race a sync that ends in microseconds.
- A press counts as pending only once the 202 arrives (and before its body status is applied), so a finished status in the answer ends the press immediately and a finished push that arrived earlier is picked up by the immediate follow-up fetch.
- Tests (fake page, fake clock, fake fetch): outcome without any push; answer already finished; finished push before an older answer; final fetch at the deadline and no polling after it.

### WR-06: A second live region and a passive "Loading the cabinet..." announcement

**Files modified:** `Cabinet.Service/wwwroot/js/cabinet.js`
**Commit:** d42e2a5
**Applied fix:** The loading line no longer has `role="status"`; it carries a `cabinet-loading` class instead, and `abandonRedraw` looks for that class. `#sync-note` is the only live region again.

### WR-07: The redraw guard is cleared by whichever redraw finishes first

**Files modified:** `Cabinet.Service/wwwroot/js/sync.js`
**Commit:** dbb0fcb
**Applied fix:** The guard is cleared only when it still names the redraw's own version, and a rejection from the redraw callback is caught there. The regression test (a superseded redraw must not reset the guard of its replacement) is in the WR-05/WR-08 commit with the rest of the new test harness.

### WR-08: The press request has no timeout, so one hung request locks the button and opens the note

**Files modified:** `Cabinet.Service/wwwroot/js/sync.js`, `build/tests/page-scripts.test.mjs`
**Commit:** a0b2496 (shared with WR-05)
**Applied fix:** The press fetch gets an `AbortController` aborted after 15 seconds and the abort takes the existing offline path (offline sentence, `pressing` cleared). Because the press is pending only after the 202, a status that ends another sync while the request hangs no longer writes into the note. A test covers the hang: no sentence before the timeout, an unrelated finished push writes nothing, the offline sentence after it, and the button can be pressed again.

## Notes

- The new behaviour tests import a temporary copy of the page scripts as ES modules, so the real files need no package or module configuration.
- Info items (IN-06 to IN-09) were out of scope and are untouched. `live.js` still returns `isConnected`, which nothing uses any more (part of the dead-code item).

---

_Fixed: 2026-10-07_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

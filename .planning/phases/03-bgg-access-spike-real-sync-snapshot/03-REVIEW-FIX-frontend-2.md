---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07
review_path: .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
iteration: 2
findings_in_scope: 4
fixed: 4
skipped: 0
status: all_fixed
---

# Phase 3: Code Review Fix Report (front end, round 2)

**Fixed at:** 2026-10-07
**Source review:** .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
**Iteration:** 2

**Summary:**
- Findings in scope: 4 (IN-06, IN-07, IN-08 for development.md and cabinet-layout.md only, IN-09)
- Fixed: 4
- Skipped: 0

**Verification:** run in the isolated worktree, which has the same sources as the branch. After each finding `node --test build/tests/page-scripts.test.mjs`, `dotnet test --solution Cabinet.slnx` (814 of 814) and `build/lint.sh repo-rules` passed. The node tests are 47 in the end (40 before: one removed with the dead copy string, eight new for the live connection). Two mutations of `live.js` (dropping the reset of the retry count after a stable connection, and polling while connected) each made new tests fail, then were reverted.

## Fixed Issues

### IN-06: The focus restore after a redraw can scroll the page

**Files modified:** `Cabinet.Service/wwwroot/js/cabinet.js`
**Commit:** 43540cf
**Applied fix:** The box that regains focus after a redraw is focused with `preventScroll: true`, so a quiet redraw never moves the page.

### IN-07: Dead and duplicated code

**Files modified:** `Cabinet.Service/wwwroot/js/status.js`, `Cabinet.Service/wwwroot/js/copy.js`, `Cabinet.Service/wwwroot/js/live.js`, `Cabinet.Service/wwwroot/js/cabinet.js`, `Cabinet.Service/wwwroot/js/sync.js`, `build/tests/page-scripts.test.mjs`
**Commit:** 59be738
**Applied fix:**
- Removed the unused `wholeMinutesLeft`; `waitPhrase` in copy.js is now the only implementation of the minute rounding, and the tests of the button name and the press sentence cover it.
- Removed `COPY.notSynced` (the server writes that text; an integration test covers it) and its node test.
- One `layoutUrl()` helper in cabinet.js replaces the two copies of the layout address.
- One `fetchStatus()` (exported from sync.js, answering null on any failure) is used by both cabinet.js (for the live script) and the press follow-up in sync.js, which now wraps it in `fetchAndApplyStatus`.
- `startLive` no longer returns the unused `isConnected`; the connected flag stays internal for the minute check.

### IN-08: Docs gaps and one misleading sentence (development.md and cabinet-layout.md only)

**Files modified:** `docs/development.md`, `docs/cabinet-layout.md`
**Commit:** 15be967
**Applied fix:**
- development.md lists Node.js as a prerequisite for the page script tests only and adds `node --test build/tests/page-scripts.test.mjs` to "Running the checks".
- cabinet-layout.md now says that with the switch off there is no switcher and no "Invented collection of N items" line, and that the sync status line is separate and shown with the synced collection whether or not the switch is on.
- `docs/bgg-sync.md` (the stale-hours default and the live updates section) was left to the back-end fixer, as instructed.

### IN-09: The press flow has no automated coverage (live connection part)

**Files modified:** `build/tests/page-scripts.test.mjs`
**Commit:** c63ceaa
**Applied fix:** The press flow itself was covered in the previous round. This round adds fake-page tests for `live.js`, with a fake browser client (builder, connection start, push, drop, reconnecting, reconnected), a fake clock whose `setInterval` really repeats (opt-in, so the sync block tests are unchanged) and a fake document visibility:
- no browser client: status check each minute while visible, none while hidden, and on becoming visible or online;
- the live route, the reconnect schedule handed to the client, pushed statuses applied and non-objects ignored, no minute check while connected;
- minute check while a connection has ended or is reconnecting, and one check when it is back;
- a connection that never starts is retried at 0, 0, 2 s, 12 s, 42 s, 102 s, 162 s (at once, then 2, 10, 30, 60, 60 s apart);
- a connection closed at once is retried more gently, and one that stayed up for a minute starts the schedule over;
- overlapping checks are skipped, and a failed or empty check changes nothing and does not block the next.

## Notes

- No planning references, personal data, `//` comments or innerHTML use were added.
- The fix report is committed last, separately from the fixes.

---

_Fixed: 2026-10-07_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 2_

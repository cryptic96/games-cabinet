---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 12
subsystem: frontend
tags: [sync-now, countdown, live-region, quiet-redraw, vanilla-js]
requires:
  - phase: 03-11
    provides: sync status line, hidden Sync now button, sync-note live region
  - phase: 03-08
    provides: POST /cabinet/sync (202/409/429) and GET /cabinet/status
  - phase: 03-10
    provides: failed and held-back outcomes in the status
provides:
  - Sync now press flow with button states, countdown and one outcome sentence per own press
  - Quiet in-place cabinet redraw reusable by live updates
  - Pure button-state, countdown and press-outcome functions pinned by node tests
affects: [03-14 live updates via applyStatus and onCollectionChanged]
tech-stack:
  added: []
  patterns:
    - "applyStatus(status, { ownPress }) is the single status taker; it redraws the button, the status line and asks for a cabinet redraw when the snapshot version differs from the one on screen"
key-files:
  created:
    - Cabinet.IntegrationTests/SyncButtonTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/sync.js
    - Cabinet.Service/wwwroot/js/status.js
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - build/tests/page-scripts.test.mjs
key-decisions:
  - "Version-change redraw lives in applyStatus (not only in the press flow) so any later status source, such as live broadcasts, redraws the same way; the version is remembered only once redraw() reports success, so a failed redraw is retried by the next status"
  - "A 429 without a status body (the built-in rate limiter) adopts the Retry-After seconds as the window"
  - "Polling after an accepted press runs every 5 s for at most 10 minutes; a 409 (someone else's sync) does not start polling on this page"
  - "Minutes in the accessible name and the press sentence round up (9:42 reads 10 minutes), as flagged in the plan"
requirements-completed: [SYNC-02, SYNC-05]
duration: 25min
completed: 2026-10-07
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 3 Plan 12: Sync now press flow and quiet redraw Summary

Any visitor can press "Sync now": the button shows Syncing... for everyone while a sync runs, counts down the shared window as m:ss, and the visitor's own finished sync swaps the cabinet in place (focus kept by entry id, being-filled message removed in the same step) with exactly one outcome sentence in the single live region.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | A visitor presses Sync now and follows it through to the redrawn cabinet | b4d1b79 |
| 2 | Button states, countdown and notes hold their contract at the edges | ca08a1f |

## What was built

- `status.js`: `buttonState`, `countdownText`, `wholeMinutesLeft`, `pressOutcome` (pure, no imports).
- `copy.js`: button words, the six own-press notes, `syncAgainIn`, `syncAgainName`, `youCanSyncAgain` (shared `waitPhrase` for the minute wording).
- `sync.js`: reveals the button, renders its three states with `aria-disabled` only, ticks once a second only while the tab is visible and a window is active, rewrites `aria-label` only when the minute wording changes, clears the note at the end of the window, posts to `/cabinet/sync`, handles 202/409/429/offline, polls status after its own accepted press.
- `cabinet.js`: `redraw()` fetches the layout for the current profile, swaps with one `replaceChildren`, removes `.cabinet-filling`, restores focus to the same `data-entry-id` (or leaves it on the body), stays silent on failure and waits for the tab to be visible. If it had superseded a first load still showing the loading line and then fails, that load is repeated so the line cannot stick.
- Tests: 4 server-contract tests (`SyncButtonTests`, trait `Sync`) and 5 node cases for the pure functions and copy.

## Verification

- `dotnet test --solution Cabinet.slnx`: 777 passed.
- `node --test build/tests/page-scripts.test.mjs`: 30 passed.
- `build/lint.sh`: all checks pass. Acceptance greps: one `method: 'POST'`, no `.disabled =`, no `innerHTML`/`insertAdjacentHTML`/`cssText`, no `…` in `copy.js`.
- Tracer gate: the tracer's automated verify passed before the second task; the in-browser feel check (timing, focus, swap) is deferred to the end-of-phase human verification per `human_verify_mode: end-of-phase`.

## Deviations from Plan

None - plan executed as written. Task 2 is test-first in spirit but its tests passed on first run because the pure functions were written in Task 1 as the plan's interfaces prescribe; no mismatch with the copy contract surfaced, so no fix was needed.

## Known Stubs

None.

## Deferred / for the owner

- The held-back stale note wording that names BGG is untouched (`SyncStatusText.StaleHeldBack`, `COPY.staleHeldBack`); the own-press held-back sentence `noteHeldBack` follows the copy contract, which also names BGG.
- Not browser-verified here (no real browser run); covered by the end-of-phase screenshot and human rounds.

## Self-Check: PASSED

- Files exist: sync.js, status.js, copy.js, cabinet.js, page-scripts.test.mjs, SyncButtonTests.cs.
- Commits b4d1b79 and ca08a1f exist.

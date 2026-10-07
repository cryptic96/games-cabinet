---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 11
subsystem: ui
tags: [razor-pages, vanilla-js, node-test, intl, sync-status]
status: complete

requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: status endpoint and CabinetStatus (03-08), being-filled page state (03-05), shared layout (03-04)
provides:
  - server-rendered sync status line under the title with relative time, exact local time on press or hover, and the older-sync note
  - pure page logic (status.js) and visitor strings (copy.js) tested with the plain node --test runner in CI
  - styles for the whole sync block, including the button states the sync-button plan uses
  - first-paint state carried as seven data attributes on the .sync element
affects: [sync button behaviour, live update and redraw]

actuals:
  tokens: 11800
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "One shared JSON case table asserted by both the C# first-paint formatter and the JavaScript formatter"
    - "Node test loads browser modules through data: URLs, so no package.json, module config or npm install"
    - "Server renders UTC first paint, script rewrites into local time using a server-clock offset"

key-files:
  created:
    - Cabinet.Service/Pages/SyncStatusText.cs
    - Cabinet.Service/wwwroot/js/status.js
    - Cabinet.Service/wwwroot/js/sync.js
    - build/tests/page-scripts.test.mjs
    - build/tests/fixtures/relative-time-cases.json
    - Cabinet.UnitTests/Sync/SyncStatusTextTests.cs
    - Cabinet.IntegrationTests/SyncStatusLineTests.cs
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/Pages/Index.cshtml.cs
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - .github/workflows/ci.yml
    - Cabinet.UnitTests/Prototype/SampleGenerationTests.cs

key-decisions:
  - "The older-sync note text is only rendered when it applies (empty and hidden otherwise), so the header of a normal page never carries copy that names BGG"
  - "ISO times in markup use the fixed form yyyy-MM-ddTHH:mm:ss.fffZ so every browser parses them"
  - "Both server and script share one stale rule: held back needs a last sync, otherwise age at or above the threshold"

patterns-established:
  - "sync.js initSyncStatus(root) returns applyStatus(status) and refresh(); applyStatus keeps the data attributes and the visible line in step and never writes error text"

requirements-completed: [SYNC-03, SYNC-04, SYNC-05]

duration: 40min
completed: 2026-10-07
---

# Phase 3 Plan 11: Sync status line Summary

**Server-rendered "Synced 12 minutes ago" line under the title, rewritten into the visitor's own time with an exact-time toggle and a calm older-sync note, with the pure logic tested by dependency-free `node --test` in CI.**

## Accomplishments

- `SyncStatusText` formats the first paint (relative text, UTC exact time, stale rule); `IndexModel` gains `Status` and helpers; `Index.cshtml` renders the `.sync` block, exact-time line, live note and stale note in the contract's order, only for the synced view.
- `status.js` (no imports) holds `serverOffsetMs`, `elapsedSeconds`, `isStale`; `copy.js` gains `TIME_LOCALE`, `notSynced`, `syncedAgo`, `exactTime`, `lastSynced`, `staleRecent`, `staleHeldBack`; `sync.js` wires local time, the exact-time toggle, a 30 second visible-tab refresh, and `applyStatus`.
- One relative-time case table (`relative-time-cases.json`) drives both the C# and JavaScript tests, so the two formatters cannot drift.
- CI runs `node --test build/tests/page-scripts.test.mjs` with the runner's own Node; no package.json exists.
- Edge tests: clock skew counts on the server clock, never-synced is never stale, held back needs a last sync, the header never names BGG or BoardGameGeek, the stale note has no stray digits, the relative time is `white-space: nowrap` and the exact line is not.

## Task Commits

1. Task 1 (tracer): `fd19582` feat(03-11): show visitors when the cabinet was last synced, in their own time
2. Task 2: `e93ac35` test(03-11): pin the status line at its edges

## Verification

- `node --test build/tests/page-scripts.test.mjs`: 25 passed.
- `dotnet test --solution Cabinet.slnx`: 700 passed before the final test additions; Sync-trait integration tests 33 passed, unit Sync-trait 62 passed after.
- `build/lint.sh` (repo-rules, workflows incl. actionlint and zizmor, shell, secrets, script-tests): all pass.
- Acceptance greps: `'en-NL'` once, `numeric: 'auto'` once, no `import` in status.js, one `node --test` CI line, no package.json.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] IndexModel constructor change broke an existing unit test**
- **Found during:** Task 1
- **Issue:** `IndexModel` now takes `SyncStatusService`; `SampleGenerationTests` constructed it with two arguments.
- **Fix:** built a real `SyncStatusService` over an in-memory state store in that test.
- **Files modified:** Cabinet.UnitTests/Prototype/SampleGenerationTests.cs
- **Commit:** fd19582

### Process note

- The tracer feedback gate was handled the autonomous way (re-ran the tracer's full verify: node tests, unit and integration Sync tests, lint) rather than returning an interactive checkpoint, because the project is set to batch human verification at the end of the phase and this agent will not be resumed. The visual human check (wrapping at 320px and 1440px, hover and tap) is left for the end-of-phase screenshot rounds as the plan states.

### Plan tension flagged

- The copy contract's held-back sentence ("A much smaller collection from BGG is waiting for the next sync to confirm.") names BGG, while the plan's prohibition says the stale note must not name BGG. The explicit copy contract was followed. The note is rendered only while it applies, so the header of a normal page never contains BGG. If the owner wants the held-back variant free of the name, only the two strings (`SyncStatusText.StaleHeldBack` and `COPY.staleHeldBack`) need to change.

## Known Stubs

None. The "Sync now" button is rendered `hidden` by design; the next plan reveals and wires it.

## Threat Flags

None. No new endpoints or trust boundaries; state flows through Razor-encoded `data-*` attributes and the script writes only `textContent`, `hidden`, `aria-*` and `data-*`.

## Self-Check: PASSED

- Files verified present: SyncStatusText.cs, status.js, sync.js, page-scripts.test.mjs, relative-time-cases.json, SyncStatusTextTests.cs, SyncStatusLineTests.cs.
- Commits verified: fd19582, e93ac35.

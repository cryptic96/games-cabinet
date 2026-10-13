---
phase: 06-owner-tools-persistence
plan: 23
subsystem: owner-tools
tags: [finish, settings, razor, body-class, owner-data]
requires:
  - phase: 06-owner-tools-persistence
    provides: "OwnerData.Finish, OwnerDataService, OwnerResponses, the Settings page and the pic-choices tile look (plans 06-07, 06-08, 06-22)"
provides:
  - "Finishes: allow-list, Normalize and SetFinish edit"
  - "SiteFinish: server-rendered finish-classic body class on both layouts and both ports"
  - "Settings Cabinet finish panel with one checked Classic tile and a drawn swatch"
  - "Finish handler on the settings page and the owner-finish script"
affects: [finishes phase, public cabinet layout, owner settings]
tech-stack:
  added: []
  patterns:
    - "Stored value reaches markup only through an allow-list normaliser"
    - "A tile that is already checked still posts on click, so choosing it again answers Saved"
key-files:
  created:
    - Cabinet.Domain/Owner/Finishes.cs
    - Cabinet.Service/Owner/SiteFinish.cs
    - Cabinet.Service/wwwroot/js/owner-finish.js
    - Cabinet.IntegrationTests/FinishSlotTests.cs
    - Cabinet.IntegrationTests/FinishPanelTests.cs
    - Cabinet.UnitTests/Owner/FinishesTests.cs
    - Cabinet.BrowserTests/OwnerFinishTests.cs
  modified:
    - Cabinet.Service/Owner/OwnerEndpoints.cs
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Pages/Owner/Settings.cshtml
    - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - Cabinet.Service/Pages/Shared/_OwnerLayout.cshtml
    - Cabinet.Service/wwwroot/css/owner.css
key-decisions:
  - "The panel sits first on the Settings page, ahead of Backup, as the UI contract orders the page"
  - "The tile posts on click of the radio rather than on change, because a change event never fires for the already-checked radio and the contract wants re-choosing to answer Saved"
  - "A refused or unreadable save keeps the tile checked: with one finish the checked state is always the stored state"
  - "Finish edits log the line Cabinet finish: Classic only when the stored value actually changes (heals a stored unknown value)"
patterns-established:
  - "Finishes.Normalize guards every use of the stored finish; SiteFinish never exposes the raw stored value"
requirements-completed: [OWN-02]
duration: 40min
completed: 2026-10-12
status: complete
actuals:
  tokens: 8000
  tasks: 2
  commits: 2
---

# Phase 6 Plan 23: The finish slot Summary

**Site-wide finish stored in owner data and rendered by the server as a finish-classic body class on every page of both ports, with a Cabinet finish panel holding one checked Classic tile and a CSS-drawn swatch.**

## Performance

- **Duration:** about 40 min
- **Tasks:** 2 of 2
- **Files:** 15 changed (7 created plus test files, 8 modified)

## Accomplishments

- The public cabinet, the owner pages and the owner-port cabinet all render `<body class="finish-classic">` from the first byte; no script is involved, so there is no flash.
- A stored finish that is not on the allow-list (including one that tries to break out of the attribute) or owner data that cannot be read renders as classic, and the stored text never reaches a page.
- The Settings page has a `Cabinet finish` panel first on the page: helper line, one checked radio tile `Classic` with `Warm wood, as it is today.`, a drawn 128px swatch (planked back, board with a lit edge, one small box, `aria-hidden`), and the note `Classic is the only finish for now.` The tile grid is the shared `pic-choices` grid, so further finishes only add tiles.
- The `Finish` handler goes through `OwnerResponses`. Choosing Classic again answers no-change and shows `Saved` with no Undo; a failed save keeps the tile checked and shows `Couldn't save that. Nothing changed. Try again.`; a form posted without script returns to the panel with the words for the result.

## Task Commits

1. **Task 1 (tracer): stored finish reaches every body class** - `4ca31b5` (feat)
2. **Task 2: the Cabinet finish panel** - `54a70a1` (feat)

The tracer's verify (the FinishSlotTests class) was run red first (body class temporarily returned empty: 3 of 3 failed), then green (3 of 3), before the panel was built.

## Verification

- `Cabinet.UnitTests` 2323 passed, `Cabinet.IntegrationTests` 409 passed, `Cabinet.BrowserTests` 269 passed (the whole browser project, including every earlier public cabinet test, so no visual change).
- `build/lint/checks/10-repo-rules.sh`, `40-secrets.sh` (no leaks), `50-script-tests.sh` and `node --test build/tests/*.test.mjs` (325 passed) all pass.
- Screenshots at 1440, 390 and 320 wide were looked at: the tile sits in the first of the four grid columns on desktop, fills about half the width on a phone, the swatch is 128px high, and nothing scrolls sideways.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] A script file for the panel**
- **Found during:** Task 2
- **Issue:** The plan's file list has no script, but the contract needs the failed-save row error and a Saved answer when the checked tile is chosen again, which a plain form post cannot give in place.
- **Fix:** Added `Cabinet.Service/wwwroot/js/owner-finish.js`, loaded from the Settings page only, reusing `postOwner`, `showRowStatus` and `showRowProblem`. The page still works without it through the Save finish button.
- **Files modified:** Cabinet.Service/wwwroot/js/owner-finish.js, Cabinet.Service/Pages/Owner/Settings.cshtml
- **Commit:** 54a70a1

**2. [Rule 2 - Missing critical functionality] Refusal key and words for a finish outside the allow-list**
- **Found during:** Task 1 and 2
- **Issue:** A refused finish needed a status key the response code set and the status words know, otherwise the answer would fall back to a generic save failure.
- **Fix:** Added `unknown-finish` to the response refusal keys and to `OwnerStrings.Message`, plus a `finish-set` key for a real change.
- **Commits:** 4ca31b5, 54a70a1

**3. Extra test files** - the plan names two test files; a unit test file (`FinishesTests.cs`) and a second integration file (`FinishPanelTests.cs`) were added so the tracer commit holds only the body-class tests and the panel tests land with the panel.

The panel integration tests were written before the panel but were not run red on their own; the tracer tests were.

## Known Stubs

None. The single finish is a deliberate slot, not a stub: the allow-list and the tile loop already take more entries.

## Threat Flags

None beyond the plan's threat model. Both mitigations are in place and tested: allow-list normalisation with an attribute-breaking stored value (T-06-23-01), and the antiforgery token required on the Finish post (T-06-23-02, a post without it answers 400).

## Self-Check: PASSED

- All created files exist and both commits (4ca31b5, 54a70a1) are in the log.

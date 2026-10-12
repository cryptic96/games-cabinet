---
phase: 06-owner-tools-persistence
plan: 11
subsystem: owner-locations-list
tags: [razor-pages, progressive-enhancement, vanilla-es-modules, dialog, undo, browser-tests]
requires:
  - phase: 06-07
    provides: "Owner layout with the token meta and the status region, owner.css tokens and the owner button styles"
  - phase: 06-09
    provides: "Locations page forms, OwnerResponses dual-mode answer, LocationNames rules, one-step undo"
provides:
  - "LocationEdits.Rename, Move, Delete with change-log wording, and CountLeftNotPlaced for the delete question"
  - "Your locations panel: closed disclosure once there are locations, ledger of items with name field, count, Up, Down, Delete, per-row status with Undo, add form"
  - "Handlers Rename, MoveUp, MoveDown, Delete, DeleteConfirmed; no-script confirmation page for a delete that leaves games not placed"
  - "owner-shared.js (postOwner, revision holder, row status with Undo, announcer, reload button), owner-format.js (frozen English strings and wording helpers), owner-locations.js"
affects: [pictures, settings, games-list, selection-dock]
tech-stack:
  added: []
  patterns:
    - "Script takes over real forms through the submit event and its submitter, one request at a time, so a quick second press never works from an old revision"
    - "The page revision lives in one holder and is written into every form marked data-page-revision after each scripted save, so no-script forms on the same page stay current"
    - "Server-rendered state (item, confirm, done) is only trusted after a lookup in the owner data; ids are never echoed into a fragment or redirect unchecked"
key-files:
  created:
    - Cabinet.Service/Pages/Owner/_LocationItem.cshtml
    - Cabinet.Service/Pages/Owner/LocationItemModel.cs
    - Cabinet.Service/wwwroot/js/owner-shared.js
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/js/owner-locations.js
    - Cabinet.UnitTests/Owner/LocationEditsTests.cs
    - Cabinet.BrowserTests/OwnerLocationListTests.cs
    - Cabinet.IntegrationTests/OwnerLocationListPostTests.cs
    - build/tests/owner-format.test.mjs
    - build/tests/owner-shared.test.mjs
  modified:
    - Cabinet.Domain/Owner/LocationEdits.cs
    - Cabinet.Service/Pages/Owner/Locations.cshtml
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.IntegrationTests/OwnerLocationTests.cs
key-decisions:
  - "A delete asks first only when it would leave games not placed (an expansion with its own location that goes back to following a base game elsewhere is not counted); the count shown, logged and spoken is that number"
  - "Undo is applied on the page by the inverse of the change (remove the added item, restore the name, move back, reinsert the deleted item) instead of re-fetching the list"
  - "The add handler answers JSON with the identifier of the new location so the script can build its item from a server-rendered template"
  - "After a form post without script the panel opens when the post was about a location (item or confirm in the address, or a refused name) and the status sits in that item's own row"
patterns-established:
  - "panel.dataset.enhanced marks a page whose script has wired its forms; browser tests wait for it before typing"
duration: about 4h
completed: 2026-10-12
status: complete
actuals:
  tokens: 37000
  tasks: 3
  commits: 3
---

# Phase 6 Plan 11: Your locations, the owner's own list edited on a phone Summary

**The owner adds, renames in place, reorders with Up and Down, and deletes locations on a phone: every change saves at once with a row status and a one-step Undo, a delete that would leave games not placed asks first with the count, and every action also works through plain form posts.**

## What was built

- **Edits** (`LocationEdits`): `Rename` (same rules as add, may keep its own name or change only its case, keeps identifier and place so games follow), `Move` (swap with the neighbour, no-op at the ends), `Delete` (games placed there become not placed, an expansion with an owned base goes back to following it, reports how many games ended up not placed through `OwnerChange.GamesNotPlaced`), `CountLeftNotPlaced` for the question before a delete. Log lines: `Renamed "{old}" to "{new}"`, `Moved "{name}" up|down`, `Deleted location "{name}"` with ` ({n} games now not placed)` when there are any.
- **Panel** (`_LocationItem.cshtml`, `Locations.cshtml`): `<details class="owner-panel loc-panel">` titled `Your locations ({n})`, closed once a location exists, open while empty; helper line; `<ol class="loc-list">` of items each with its own form (name field 16px/44px with `aria-label="Name of location {position}"`, a no-script `Save`, count, `Up`/`Down` with the rotated `i-chev` icon and visible words, `Delete`, accessible names `Move {name} up`, `Move {name} down`, `Delete {name}`, `aria-disabled` at the ends), an empty `role="alert"` slot for name errors and a `role="status"` row status; add form with the hint `Shown to every visitor. Avoid people's names.`; one `<dialog class="owner-dialog">`; a `<template>` the script copies for a location it adds.
- **Handlers**: `Rename`, `MoveUp`, `MoveDown`, `Delete` (no games: at once; games and no script: redirect to `?confirm={id}#loc-{id}` after a lookup in the owner data), `DeleteConfirmed`. Form posts return to the same item (`done` plus a looked-up `item`), a refused name shows inline in that item or under the add field.
- **Scripts**: `owner-shared.js` (`readToken`, `postOwner` mapping 200, 409, 422, bodyless 400 and network errors, revision holder, `showRowStatus` with Undo that drops the Undo of any other row, `showRowProblem`, `reloadButton`, `announce`); `owner-format.js` (frozen `OWNER_TEXT`, `gameCount`, `movedText`, `deleteDialogParts`, `deleteDialogBody`, `deletedText`, `refusalText`, `headingText`); `owner-locations.js` (rename on change and Enter, Escape restores, move keeps focus on the same button and announces `Moved "{name}" down. Now 2 of 3.`, delete with no games at once then focus on the next name field or the add field, delete dialog opening on `Keep it`, Escape and backdrop click cancel and return focus to Delete, add keeps focus in the cleared field, Undo, out-of-date and expired messages with Reload, failed save restores the field).
- **Styles** (`owner.css`): ledger items, flex item form that becomes two lines below 40rem (and fits 320px), summary with a CSS chevron and no motion, row status, inline error and destructive warning line (4px start borders), dialog `min(28rem, 100vw - 32px)`.

## Verification

- `Cabinet.UnitTests`: 2093 pass (16 new in `LocationEditsTests`, the whole project run).
- `Cabinet.IntegrationTests`: 334 pass (8 new in `OwnerLocationListPostTests`: JSON id on add, rename rules, moves and ends, unknown ids refused by every handler, delete asks first / `confirm-needed` / confirmed delete, no-script delete lands on the neighbour, refused rename inline at the item, hostile `confirm` and `item` never echoed).
- `Cabinet.BrowserTests` `OwnerLocationListTests`: 10 pass, with empty `ConsoleErrors` (apart from the browser's own line for an expected 4xx answer in the refusal tests) and loopback-only requests: add, rename, undo and delete with the card following the rename; dialog (focus on `Keep it`, Escape, backdrop, confirm, undo); singular wording; moves with focus and `aria-disabled` ends; duplicate name inline; disclosure states; two-line items and 44px buttons at 390 and 320 with no sideways scroll and a 60-character name; out of date, expired and failed save; the whole flow with JavaScript off.
- `node --test build/tests/*.test.mjs`: 279 pass (23 new). `build/lint/checks/10-repo-rules.sh`, `40-secrets.sh` (gitleaks over history and tree) and `50-script-tests.sh` all exit 0.
- Screenshots at 1440, 390 and 320 and the delete dialog at 1440 were rendered and looked at (not committed).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The add answer had to carry the new location's identifier**
- **Found during:** Task 1
- **Issue:** The script builds an item for a location the server just added and needs its identifier; the JSON answer had none.
- **Fix:** `OwnerResponses.For` takes an optional `createdId` and adds `id` to the JSON when given. The file is not in the plan's list.
- **Files modified:** Cabinet.Service/Owner/OwnerResponses.cs
- **Commit:** eac2b79

**2. [Rule 1 - Bug] A closed panel hid the item a no-script post had just changed**
- **Found during:** Task 2 (no-script browser test)
- **Issue:** With one or more locations the panel is closed, so after adding a location without script the list and the status were hidden behind the summary.
- **Fix:** Add, rename, move and delete now redirect with a looked-up `item` (the neighbour after a delete) and the page opens the panel for it; the existing assertion `done=saved#` for an add was loosened to `done=saved` because the redirect now also carries `item`.
- **Files modified:** Cabinet.Service/Pages/Owner/Locations.cshtml.cs, Cabinet.IntegrationTests/OwnerLocationTests.cs
- **Commit:** eac2b79

**3. [Rule 2 - Missing critical] Server-written notices went stale after a scripted change**
- **Found during:** Task 1 critic pass (screenshot showed a top `Saved. Undo` beside an `Undone.` status)
- **Issue:** The notice and Undo form the server wrote for the post that brought the browser here would offer to undo the wrong change once a script saved another one.
- **Fix:** `showRowStatus` removes the top notice and any server-rendered Undo forms before it writes.
- **Commit:** eac2b79

**4. [Rule 2 - Missing] Forms elsewhere on the page needed the new revision**
- **Issue:** The game pickers' no-script forms carry a revision; after a scripted save they would be refused as out of date.
- **Fix:** every `input[name=revision][data-page-revision]` follows `setRevision`; the script also rebuilds every game picker's options after add, rename, move, delete and undo.
- **Commit:** eac2b79

**5. [Rule 3 - Blocking] Tests could type before the script had wired the forms**
- **Fix:** the panel gets `data-enhanced` when the script is done and the browser tests wait for it.
- **Commit:** eac2b79

### Plan variations (not bugs)

- The `Delete` handler decides "asks first" by games that would become *not placed*, not by any game shown there; the dialog, status and log all use that one number.
- Extra files: `_LocationItem.cshtml`, `LocationItemModel.cs` (item markup shared by the list and the script's template) and `OwnerLocationListPostTests.cs` (handler-level tests for the threat register).
- The status for an add or a delete goes in the add form's status line (Tab reaches Undo next); rename and move use the item's own row status.
- In the no-script path the duplicate message is the generic `You already have a location with that name.` because the typed name is never echoed back through a redirect; the script path says `called "{name}"`.

### Process

- **Tracer gate:** auto mode is off, but the plan is autonomous and the tracer verify is fully automated: I ran the unit tests and the tracer browser test, saw them green and continued without a human checkpoint.
- **Red first:** the unit tests were red as compile failures (the edits did not exist). The browser tests were written after the implementation was in place, so none of them was seen red before the code; they were run red-to-green only in the sense that the first runs found real defects (panel closed, wrong counts in my fixtures, layout not wrapping on phone) that I then fixed.

## Known Limitations

- After a delete, an Undo of a delete, or an add, the counts of *other* locations are not refreshed (only the one case where an expansion with its own location here goes back to following a base game elsewhere changes them); a reload corrects it.
- The delete decision on the page uses the count rendered at load (`data-not-placed`); a sync that adds an expansion in the meantime is only caught by a reload.
- A pre-existing intermittent crash of the browser test process ("Internal CLR error", exit by abort after a test finished) shows up about once in eight runs of any browser test class, including the untouched shell tests; it is not related to this work and is not fixed here.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register. T-06-11-01 to T-06-11-05 hold: names are Razor-encoded, written by script with `textContent` only (markup-API lint over `wwwroot/js`), `dir="auto"`, control and direction characters refused; every post needs the antiforgery header or field (a bodyless 400 maps to the expired message); `item`, `confirm` and the fragment of a redirect come from a lookup in the owner data (tested with a hostile value); every change is logged and undoable; 50 locations and 60 characters are enforced server-side.

## Self-Check: PASSED

- Created files exist: `_LocationItem.cshtml`, `LocationItemModel.cs`, `owner-shared.js`, `owner-format.js`, `owner-locations.js`, `LocationEditsTests.cs`, `OwnerLocationListTests.cs`, `OwnerLocationListPostTests.cs`, `owner-format.test.mjs`, `owner-shared.test.mjs`.
- Commits 544c252, eac2b79 and fa7b877 exist on the worktree branch.

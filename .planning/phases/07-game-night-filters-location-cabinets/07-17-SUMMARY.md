---
phase: 07-game-night-filters-location-cabinets
plan: 17
subsystem: ui
tags: [filters, locations, select, per-box-judgement, browser-tests, owner-host]

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "locationId on each card and locations on the cards document (plan 07-10); chip groups and evaluate (plan 07-11); the pinned two-row bar (plan 07-15)"
provides:
  - "Location rule in filter-rules.js: judged per box, with locationChoices, effectiveLocation and UNKNOWN_LOCATION"
  - "#filter-loc select in bar row 2, filled from the cards document through textContent, hidden while the owner has no locations"
  - "Shared Cabinet.BrowserTests/Infrastructure/OwnerHost.cs (SyncAsync, StoredAsync, AttachAsync, SeedAsync, PlaceAsync, DeleteLocationAsync)"
  - "FilterLocationTests (5 browser tests)"
affects: [07-22, 07-25, filters, location-cabinets]

tech-stack:
  added: []
  patterns:
    - "Location is the one filter judged per box; search, players and time stay judged per family"
    - "The model carries the owner's locations; the chosen id is narrowed to what is offered before every pass, so a stale id is Any"
    - "The select is only rewritten when its option list differs, so an open list is not closed by a refresh"

key-files:
  created:
    - Cabinet.BrowserTests/FilterLocationTests.cs
    - Cabinet.BrowserTests/Infrastructure/OwnerHost.cs
  modified:
    - Cabinet.Service/wwwroot/js/filter-rules.js
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/filters.css
    - build/tests/filter-rules.test.mjs
    - Cabinet.BrowserTests/OwnerCabinetTests.cs
    - Cabinet.BrowserTests/KeyboardTests.cs

key-decisions:
  - "buildModel takes the owner's locations as an optional second argument and evaluate narrows the chosen location itself, so the pure rules own the 'not offered means Any' behaviour and the node tests cover it"
  - "filters.rebuild(records, locations) keeps the previous locations when called without them, so a redraw before the card data arrives does not hide the select"
  - "OwnerHost has three entry points: SyncAsync (scripted BGG then one sync, as the old nested host did), StoredAsync (a state directory the filter fixture wrote, no sync) and AttachAsync (any running host)"

patterns-established:
  - "A box the model does not hold is dimmed while any of players, time or location is chosen"

requirements-completed: [FILT-04, LOC-01, EXP-04]

duration: about 70min
completed: 2026-10-16
status: complete
actuals:
  tokens: 31000
  tasks: 2
  commits: 3
---

# Phase 7 Plan 17: "Where is it?" - the location filter Summary

**A native location select in the second bar row (Any location, the owner's places that hold a game in the owner's order, Unknown location last) that lights each box by its own place, so an expansion kept on Shelf B lights while its base in Cabinet A dims, combined with search, players and time by AND and hidden while the owner has no locations.**

## Accomplishments

- `filter-rules.js`: `locationChoices(model)` (places with at least one game in the owner's order; unknown only while some game has no place and the owner has places), `effectiveLocation(chosen, model)` (an id not offered, or unknown while not offered, is Any) and the per-box location test inside `evaluate`. `buildModel(records, locations)` carries the list. 7 new node tests.
- `Index.cshtml` and `filters.js`: a hidden label and `<select id="filter-loc" hidden>` with the Any option; the script fills the rest with `textContent` and `dir="auto"`, shows the select only while the owner has locations, resets it to Any when the chosen id is no longer offered, and Clear returns it to Any. The change handler feeds the same debounced announcement as the chips. `cabinet.js` passes the document's `locations` to `filters.rebuild`.
- `filters.css`: 44 px select, 16 px text, 12 to 14 rem wide, 1 px wall-line border, 4 px radius, wall text on transparent, same focus ring, `color-scheme: dark` with a light scheme on the walnut and birch walls.
- `OwnerHost` promoted out of `OwnerCabinetTests` into shared infrastructure and used by it again unchanged in behaviour (`CardReading` moved with it).
- `FilterLocationTests`: the select and its dimming on sample 65 (including an expansion that lights while its base dims, and no box moves), Unknown and AND with a players chip and Clear, no control without owner locations, the control appearing with the first location and leaving with the last (through `SeedAsync` and `DeleteLocationAsync`), and two copies of one game in two places on the filter fixture, each lit by its own place and each card showing `Stored in` with it (a game with no place has no such row).

## Task Commits

1. **Task 1 (tracer): choosing "Shelf B" lights exactly the boxes stored on Shelf B** - `4bda930` (feat)
2. **Task 2: shared owner host, Unknown last, AND, hidden without locations, the card's place** - `a71988b` (feat)

## Decisions Made

- **Tracer gate handled by the end-of-phase setting**, as in the earlier tracer plans: auto mode is not active, the project sets `human_verify_mode: end-of-phase`, and a parallel worktree executor is never resumed. The automated half ran and passed (the node tests and `Choosing_a_place_lights_exactly_what_stands_there`) before the second task and again with the full set; the human look at the select is left to the end-of-phase round.
- **Tracer commit holds the production half only.** `FilterLocationTests` needs the shared `OwnerHost` for three of its five tests, so the whole class was committed with the second task; the tracer's own verify ran from the working tree before the first commit.
- The bar stays at its steady 112 px from 64rem with the select added, in English and Dutch, under the normal fonts and the DejaVu configuration (`FilterBarGeometryTests`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `cabinet.js` not in the plan's file list**
- **Issue:** the select is filled from the cards document, which only `cabinet.js` receives.
- **Fix:** `filters.rebuild(filterRecords(), body.locations ?? [])`, one line.
- **Commit:** `4bda930`

**2. [Rule 1 - Bug] Existing keyboard tests assumed the time chip is the last stop in the bar**
- **Found during:** Task 2 (wider browser run)
- **Issue:** with the select after the time group, Tab from the time chip lands on the select, so two `KeyboardTests` failed.
- **Fix:** `LastControlBeforeCabinet` is now `#filter-loc` (the tests use the sample, which has locations).
- **Files modified:** `Cabinet.BrowserTests/KeyboardTests.cs`

**3. [Judgement] `DeleteLocationAsync` deletes without the collection**
- `LocationEdits.Delete` takes a snapshot only to count games left not placed for its log line; the helper passes none, which changes nothing the tests read.

**Total deviations:** 2 auto-fixed, 1 judgement. **Impact on plan:** none on scope.

## Verification

- `node --test build/tests/*.test.mjs`: 621 pass.
- `FilterLocationTests` (5) and all `Filter*` classes (29) pass with the normal fonts and with the DejaVu fontconfig.
- All `Owner*` classes (217), `KeyboardTests`, `GamesListTests`, `CabinetSmokeTests`, `CardContentTests` and `FilterKeyboardTests` (53) pass.
- `CabinetPolishTests`, `DefaultLookIdentityTests`, `PullOutTests`, `SectionRowAlignmentTests`, `LanguageSwitchTests`, `LookContainmentTests` and `LightsTests` (114) pass.
- Repo-rules, workflow and shell lint pass; the secrets step was skipped (cannot run in a linked worktree).
- Run with `DOTNET_USE_POLLING_FILE_WATCHER=1`.

## Known Stubs

None.

## Threat Flags

None. Option text goes in through `textContent` with `dir="auto"`; the chosen id is compared as text against the offered list and falls back to Any (T-07-17-01, T-07-17-02); no new endpoint or storage.

## Self-Check: PASSED

- Files found: `Cabinet.BrowserTests/FilterLocationTests.cs`, `Cabinet.BrowserTests/Infrastructure/OwnerHost.cs`, `Cabinet.Service/wwwroot/js/filter-rules.js`, `Cabinet.Service/wwwroot/js/filters.js`.
- Commits `4bda930` and `a71988b` present in the log.

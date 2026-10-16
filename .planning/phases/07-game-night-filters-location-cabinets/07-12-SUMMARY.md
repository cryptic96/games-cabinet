---
phase: 07-game-night-filters-location-cabinets
plan: 12
subsystem: owner-ui
tags: [owner-pages, per-copy, locations, bulk-move, razor, vanilla-js]
requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "SnapshotItem.VersionName (plan 04); per-copy Locations page rows, entry-keyed overlay, picker posting collectionId (plans 03 and 08)"
provides:
  - "Copy rows labelled by their edition name, else Copy k of n"
  - "LocationEdits.MoveMany(data, copies, target, titleOf, followsBase, copiesOf): moves exactly the listed copies in one change"
  - "OnPostBulkMoveAsync(revision, long[]? collectionIds, target): refuses the whole move for an entry that is not in the collection"
  - "LocationPickerModel.Hint / HintVisible and LocationGameRow.BaseCopiesDiffer: the quiet Follows the first copy of {base} line"
  - "CopiesCollectionSeed test helper: one game owned three times, one owned once, one expansion of the first"
affects: [07-13, 07-16, 07-18]
tech-stack:
  added: []
  patterns:
    - "Bulk move ticks and posts collection entries (row.dataset.entryId), never game ids"
    - "A decision for every copy of a game is split per copy (OwnerGames.SplitForEdit) before one copy is edited; PlacedCopy is the single per-copy placement step shared by Assign and MoveMany"
key-files:
  created:
    - Cabinet.IntegrationTests/Infrastructure/CopiesCollectionSeed.cs
  modified:
    - Cabinet.Domain/Owner/LocationEdits.cs
    - Cabinet.Service/Pages/Owner/Locations.cshtml
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/Pages/Owner/LocationPickerModel.cs
    - Cabinet.Service/Pages/Owner/_LocationPicker.cshtml
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.Service/wwwroot/js/owner-placing.js
    - Cabinet.UnitTests/Owner/LocationEditsTests.cs
    - Cabinet.IntegrationTests/OwnerPerCopyLocationTests.cs
    - Cabinet.IntegrationTests/OwnerBulkMovePostTests.cs
    - Cabinet.IntegrationTests/OwnerLocationListPostTests.cs
    - Cabinet.BrowserTests/OwnerPerCopyLocationTests.cs
    - Cabinet.BrowserTests/OwnerPlacingTests.cs
decisions:
  - "A copy takes its edition name as its tag only when no other copy of the same game has the same name (case-insensitive); copies that share a name keep Copy k of n so they stay distinguishable"
  - "The first-copy hint is rendered only where the page keeps it current (the Locations page asks for it with withHint); the script shows and hides it as pickers change"
  - "owner-games.js needed no change: its unplaced set was already keyed by entry id and the title search already matches each copy row on its own title"
requirements-completed: [FILT-04, LOC-05]
status: complete
metrics:
  completed: 2026-10-16
actuals:
  tokens: 16000
  tasks: 3
  commits: 3
---

# Phase 7 Plan 12: The Locations page knows every copy Summary

Copy rows are labelled by edition name, the dock's bulk move now moves exactly the ticked copies (one change, one log line, one Undo), expansions that follow a base whose copies are in different places say so, and the counts and delete confirmation count copies.

## What was built

- **Task 1 (tracer), f9f5c27:** `RowsOf` tags a copy of a game owned more than once with its `VersionName`, otherwise `Copy k of n`; the picker's accessible name already used the tag, so it reads `Location for Quiet Quarry, Quiet Quarry Deluxe edition`. Integration test `Edition_names_label_the_copy_rows`. Tracer gate: the tracer's verify had passed when committed and the owner browser suites re-ran green at the end; the run was treated as autonomous because the host marked auto mode active and `human_verify_mode` is end-of-phase (the config flags themselves read false).
- **Task 2, 003b1c2:** `MoveMany` takes `(GameId, CollectionId)` pairs plus `copiesOf`, refuses an unknown game or an entry the game does not have, and moves each listed copy through the new private `PlacedCopy` step (which the per-copy `Assign` now shares), so a decision for every copy of a game is split first and unticked copies keep their place. `OnPostBulkMoveAsync` binds `long[] collectionIds`, resolves each through the snapshot and refuses the whole move with `unknown-game` for a stranger. The checkbox value and `owner-placing.js` post the entry id. Tests: 8 unit cases (including two of three copies moved with a game-wide decision, and an unknown copy), 2 new integration cases plus the existing bulk-move cases converted to entries (including a foreign id), and browser `Ticking_two_copies_moves_only_those_copies` (copies 1 and 3 to Shelf B, copy 2 stays, Undo restores).
- **Task 3, 63bd3f9:** `LocationGameRow.BaseCopiesDiffer` (the base game's copies are not all shown in one place) feeds `LocationPickerModel.Hint` / `HintVisible`; `_LocationPicker` renders `p.owner-hint.picker-hint[data-follow-hint]` under the select; `refreshMeta` in `owner-placing.js` recomputes it live (hidden when the expansion has its own place or the base copies agree). Counts: a unit case for three copies left not placed, an integration case for the per-location counts and the delete confirmation (`1 game`, `4 games`, `4 games will become not placed.`), and browser `Counts_hint_and_search_work_per_copy` (`Not placed yet (2)`, hint text, hint hides when the expansion gets its own place, search "quarry" shows all three copies, unplaced filter shows the unplaced copy and the single-copy game). The existing `OwnerLocationListTests` already expects the per-location count to count copies (`3 games`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Copies of the same edition would carry identical tags**
- **Found during:** Task 3 (the existing browser case `Changing_the_second_copy_saves_without_a_reload` saw both synthetic copies of "Example Game 1" tagged `Example Game 1 edition`)
- **Issue:** owning the same edition twice is the usual reason for two copies; labelling both with the edition name defeats the purpose of the tag and gives two identically named pickers.
- **Fix:** an edition name is the tag only while it is unique among the game's copies (case-insensitive); duplicates keep `Copy k of n`. Test `Copies_of_the_same_edition_keep_their_copy_tags_so_they_stay_apart`.
- **Files modified:** Cabinet.Service/Pages/Owner/Locations.cshtml.cs, Cabinet.IntegrationTests/OwnerPerCopyLocationTests.cs
- **Commit:** 63bd3f9

**2. [Rule 3 - Blocking] A shared seed for a three-copy collection**
- The browser cases need one game owned three times plus an expansion, which the synthetic BGG collection does not have. Added `Cabinet.IntegrationTests/Infrastructure/CopiesCollectionSeed.cs` (not in the plan's file list) and used it from three test files.
- **Commit:** 003b1c2

**3. [Rule 2 - Missing critical] Hint kept current by script, and only where the script runs**
- A static hint would go stale as soon as the owner changes a picker. The hint is rendered only for the Locations page (`withHint: true`); the Pictures page's single picker is left as it was.

No other deviations. The plan's mention of a "to-check count" belongs to the Pictures page, which is still keyed by game and is replaced by a later plan; left untouched.

## Open risks

- **Picture edits still drop per-copy location entries** (carried over from the previous plan): `PictureEdits` still rewrites with `Where(game => game.GameId != gameId)`, so a picture edit on a multi-copy game that was placed per copy loses the other copies' places. Not touched here.
- **Pictures page single picker** still shows and places the first copy only.
- The accessible name of a copy whose edition name begins with `Copy ` has its first letter lowercased by `OwnerStrings.PickerLabelForCopy` (it was written for the copy tag). Cosmetic, left as is.

## Known Stubs

None.

## Verification

- `dotnet build Cabinet.slnx`: succeeded, 0 warnings.
- `dotnet test --project Cabinet.UnitTests` (full): 2840 passed.
- `dotnet test --project Cabinet.IntegrationTests` (full): 512 passed, 1 failed (`TestHostCleanupTests`, inotify count, ignored per host notes).
- `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.Owner*"` with the DejaVu fontconfig and polling watcher: 215 passed.
- `node --test build/tests/*.test.mjs`: 599 passed.
- `build/lint/checks/10-repo-rules.sh`, `20-workflows.sh`, `30-shell.sh`, `50-script-tests.sh` passed; the secrets step was not run (cannot run inside a linked worktree).
- `grep -c collectionIds Cabinet.Service/wwwroot/js/owner-placing.js` is 1; `grep -c VersionName Cabinet.Service/Pages/Owner/Locations.cshtml.cs` is 2.

## Threat Flags

None beyond the plan's register: every collection id of a bulk move must be an entry of the current snapshot or the whole move is refused (integration and unit tests); edition names are Razor-encoded, carry `dir="auto"` and are capped at sync.

## Self-Check: PASSED

- Created file exists: Cabinet.IntegrationTests/Infrastructure/CopiesCollectionSeed.cs.
- Commits found: f9f5c27, 003b1c2, 63bd3f9.

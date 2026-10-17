---
phase: 07-game-night-filters-location-cabinets
plan: 16
subsystem: owner-ui
tags: [owner-pages, pictures, per-copy, razor, vanilla-js]
requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "07-04 SnapshotItem.VersionName; 07-08 per-copy location pickers; 07-12 edition labels; 07-13 per-copy PictureEdits and entry-keyed overlay"
provides:
  - "PictureRows.Build: one row per collection entry with PictureRow.CollectionId and CopyLabel"
  - "CopyLabels (Domain): the one label rule (edition name when unique among the copies, else Copy k of n) shared by both owner pages"
  - "PicturesModel.GroupName(long collectionId), RowId(long), Pickers; Choose, LooksRight and Undo handlers taking collectionId, refusing an id that is not an entry of the posted game"
  - "owner-pictures.js posts collectionId with every choice, Looks right and Undo"
  - "Single-game view lists every copy with its picture row and its own location picker; LocationsModel.RowFor removed"
affects: [07-18, 07-20]
requirements-completed: [LOC-05]
status: complete
actuals:
  tokens: 19850
  tasks: 2
  commits: 2
key-files:
  created:
    - Cabinet.Domain/Owner/CopyLabels.cs
    - Cabinet.BrowserTests/OwnerPerCopyPictureTests.cs
  modified:
    - Cabinet.Domain/Owner/PictureRows.cs
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/wwwroot/js/owner-pictures.js
    - Cabinet.UnitTests/Owner/PictureRowsTests.cs
    - Cabinet.IntegrationTests/OwnerPictureChoiceTests.cs
    - Cabinet.IntegrationTests/OwnerLinkPreviewTests.cs
    - Cabinet.BrowserTests/OwnerLinkFlowTests.cs
    - Cabinet.BrowserTests/OwnerPictureTilesTests.cs
    - Cabinet.BrowserTests/OwnerPicturesPageTests.cs
    - Cabinet.BrowserTests/OwnerSingleGameTests.cs
    - Cabinet.BrowserTests/OwnerWorthALookTests.cs
    - .planning/todos/completed/2026-10-14-owner-pages-collapse-owned-versions-of-one-game.md
decisions:
  - "Picture rows are anchored `entry-{collectionId}`; the link handlers (still per game) return to the game's first copy row"
  - "The link form and its preview are offered on the first copy of each game only, because a link is still stored for the whole game until the per-copy link flow"
  - "A row's choice is the copy's own picture, else the picture that holds for every copy, matching how the overlay resolves it"
---

# Phase 7 Plan 16: The Pictures page shows and sets every copy Summary

The Pictures page now lists one row per copy of a game, labelled by edition name or `Copy k of n`; a choice, Looks right or Undo on a row is saved for that copy alone and reaches that copy's box in the public cabinet, and the single-game view shows every copy with its own picture row and location picker.

## What was built

- **Task 1 (tracer), d2019cb:** `PictureRows.Build` groups the mapper's traces by collection entry, labels copies with the new `CopyLabels.Of` (also used now by the Locations page, and `OwnerStrings.CopyTag` delegates to it), and reads each row's setting per copy. `PicturesModel` handlers take `collectionId` and refuse one that is not an entry of the posted game (`unknown-game`); the page script posts it with every request. Because `RowFor` had no remaining caller, the single-game view change (`Pickers` per copy) and the `RowFor` removal landed in this commit so the solution compiles. Tracer gate: the unit and integration cases (`Posting_a_copys_choice_changes_only_that_copy`, foreign-id refusal for Choose and LooksRight, per-copy row rendering and no-script redirect to `#entry-{id}`) passed before expansion.
- **Task 2, 706ab9e:** new `OwnerPerCopyPictureTests` (`Each_copy_has_its_own_picture_row`, `The_single_game_view_shows_every_copy`), the existing picture browser classes moved to per-copy anchors and the first copy's row, and the folded todo moved to `.planning/todos/completed/` with `git mv` and a closing line.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Domain cannot call `OwnerStrings.CopyTag`**
- **Issue:** `PictureRows` lives in Domain, the label rule lived in Service (`Locations.cshtml.cs`).
- **Fix:** moved the rule to `Cabinet.Domain/Owner/CopyLabels.cs`; `RowsOf` and `OwnerStrings.CopyTag` use it. Behaviour of the Locations page unchanged (its tests pass).
- **Commit:** d2019cb

**2. [Rule 1 - Bug] Duplicate link forms, ids and previews on copy rows**
- **Found during:** Task 2 (single-game view of a game owned twice rendered two link boxes and two previews, and tests hit strict-mode violations).
- **Issue:** the link flow still stores one link for the whole game, so a link box on every copy row would be misleading and would duplicate the preview.
- **Fix:** `PicturesModel.OffersLink(row)` is true for the first copy of each game only; field ids use the collection id. Plans 07-18 and 07-20 make the link flow per copy.
- **Commit:** 706ab9e

**3. [Rule 3 - Blocking] Link handler anchors**
- The link handlers still take a game id; their redirects now go to the game's first copy row (`entry-{first}`), and `OnPostUndoAsync` takes an optional `collectionId` (a game-wide link undo reports on the game's first copy). `OwnerLinkPreviewTests` expectations updated accordingly (file not in the plan's list).

**4. Existing browser tests (not all in the plan's list)**
- `OwnerLinkFlowTests` and `OwnerRecoveryTests`-adjacent expectations used `Example Game 1`, which the synthetic collection owns twice; row locators now take the first copy, anchors match `#entry-\d+`, a per-copy Automatic click now splits the stored decision into per-copy entries (assertions follow), and the worklist order test expects the second copy of the changed game right after the first. `OwnerLinkFlowTests.cs` was added to the touched files.

## Known Stubs

None.

## Verification

- `dotnet build Cabinet.slnx`: 0 warnings, 0 errors.
- `dotnet test --project Cabinet.UnitTests` (full): 2876 passed.
- `dotnet test --project Cabinet.IntegrationTests` (full): 528 passed, 1 failed (`TestHostCleanupTests`, inotify count under the polling watcher, ignored per host notes).
- `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.Owner*"` with the DejaVu fontconfig and polling watcher: 217 passed.
- `node --test build/tests/*.test.mjs`: 614 passed; `build/tests/check-owner-routes-test.sh`: 19 checks, 0 failures.
- `build/lint/checks/10-repo-rules.sh`, `20-workflows.sh`, `50-script-tests.sh`, `30-shell.sh` ran clean; the secrets step was not run (cannot run in a linked worktree).
- `grep -c collectionId Cabinet.Service/wwwroot/js/owner-pictures.js` is 3; no `RowFor(` other than `RowForCopy` remains; `WorthALookTests` unit class unchanged and green.

## Threat Flags

None beyond the plan's register. T-07-16-01: every picture-choice handler (Choose, LooksRight) refuses a collection id that is not an entry of the posted game (integration test covers a foreign and a missing id; revision unchanged). T-07-16-02: pages stay under `/Owner`, route check green. T-07-16-03: copy tags are Razor-encoded and carry `dir="auto"`.

## Self-Check: PASSED

- Created files exist: Cabinet.Domain/Owner/CopyLabels.cs, Cabinet.BrowserTests/OwnerPerCopyPictureTests.cs.
- Commits found: d2019cb, 706ab9e.

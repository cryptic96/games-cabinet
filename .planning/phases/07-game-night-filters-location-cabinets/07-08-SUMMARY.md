---
phase: 07-game-night-filters-location-cabinets
plan: 08
subsystem: owner-ui
tags: [owner-pages, per-copy, locations, razor, vanilla-js]
requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "Per-copy owner data, entry-keyed overlay and per-copy LocationEdits.Assign (plan 03)"
provides:
  - "Locations page: one row, copy tag and picker per collection entry, copies in collection order"
  - "OnPostAssignAsync(revision, gameId, collectionId, locationId, returnTo) refusing a foreign or missing collection id"
  - "LocationGameRow.CollectionId, CopyLabel, BaseEntryId, IsFirstCopy, Anchor; LocationsModel.RowForCopy"
  - "LocationPickerModel.CollectionId, CopyLabel, AccessibleName; hidden collectionId field in the shared picker"
  - "OwnerStrings.CopyTag, PickerLabelForCopy, FollowsFirstCopy"
  - "OwnerOverlay without game-keyed location accessors (only OwnedBaseGameId and PictureOverrideOf stay game-keyed)"
affects: [07-12, 07-13, 07-16, 07-18]
tech-stack:
  added: []
  patterns:
    - "Owner list rows keyed by data-entry-id; an expansion nests under data-base-entry-id, the base's first copy"
key-files:
  created:
    - Cabinet.IntegrationTests/OwnerPerCopyLocationTests.cs
    - Cabinet.BrowserTests/OwnerPerCopyLocationTests.cs
  modified:
    - Cabinet.Domain/Owner/OwnerOverlay.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Pages/Index.cshtml.cs
    - Cabinet.Service/Pages/Owner/Locations.cshtml
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/Pages/Owner/LocationPickerModel.cs
    - Cabinet.Service/Pages/Owner/_LocationPicker.cshtml
    - Cabinet.Service/wwwroot/js/owner-games.js
    - Cabinet.Service/wwwroot/js/owner-placing.js
    - Cabinet.Service/wwwroot/js/owner-shared.js
    - build/tests/owner-shared.test.mjs
    - build/tests/owner-pickers.test.mjs
    - Cabinet.IntegrationTests/OwnerLocationTests.cs
    - Cabinet.IntegrationTests/OwnerGamesListPageTests.cs
    - Cabinet.BrowserTests/OwnerLocationListTests.cs
decisions:
  - "A game's first copy keeps the row anchor game-{id} and picker id picker-{id}; later copies get game-{id}-{entry}; a single-copy game is unchanged, so existing links and selectors keep working"
  - "Each location's game count on the Locations page now counts copies (it matched the script's recount and the delete confirmation already)"
  - "Pictures page single picker keeps RowFor, which gives the first copy; its picker therefore places the first copy only until the pictures plan replaces it"
requirements-completed: [FILT-04, LOC-01, LOC-05]
status: complete
metrics:
  completed: 2026-10-16
actuals:
  tokens: 26000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 08: The Locations page shows and places every copy Summary

Every copy of a game is its own row on the Locations page with its own tag and picker, a picker post names the copy by collection id and moves only that copy, and nothing in the code reads a place per game any more.

## What was built

- **Task 1 (tracer), commit 86edd7d:** `LocationsModel` builds one `LocationGameRow` per snapshot entry (`Copy k of n` only when a game is owned more than once, numbered by ascending collection id). `Nested` puts an expansion under the base's first copy. `_LocationPicker` posts a hidden `collectionId` and names the select `Location for {title}, copy 2 of 3` for tagged rows. `OnPostAssignAsync` refuses a collection id that is not an entry of the posted game (and a missing one, which binds to 0) with `unknown-game`, and calls the per-copy `LocationEdits.Assign` with the game's copies in ascending order. The owner scripts post `collectionId`, key the unplaced set, nesting, search context and collator order by `data-entry-id` / `data-base-entry-id`, and `gameStates` feeds the entry ids to the recount. Tests: three integration cases and one browser case (`Changing_the_second_copy_saves_without_a_reload`), two new node cases, and the picker node test now checks the posted `collectionId`.
- **Task 2, commit 1fca64f:** the cabinet page's not-placed count counts entries with no effective location; `EffectiveLocationId`, `EffectiveLocationName`, `OwnLocationId`, `FollowsBaseGameId` are deleted from `OwnerOverlay` (with the now unused game-wide dictionary). One more integration case pins the copy count on the cabinet page. The `OwnerLocationTests` refusal case gained the foreign-collection-id refusal.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] An existing page test asserted the per-game picker name**
- **Found during:** Task 1 verification
- **Issue:** the synthetic collection owns "Example Game 1" twice, so its pickers are now named `Location for Example Game 1, copy 1 of 2` and `..., copy 2 of 2`; `OwnerGamesListPageTests.Every_game_gets_one_picker_from_the_shared_partial_that_posts_to_the_assign_handler` was expected to pass unchanged but checked the old name.
- **Fix:** that one assertion now expects both copy names. The form/class matching the plan named stayed unchanged.
- **Files modified:** Cabinet.IntegrationTests/OwnerGamesListPageTests.cs
- **Commit:** 86edd7d

**2. [Rule 1 - Bug] A browser test expected the old per-game location count**
- **Found during:** Task 1
- **Issue:** `OwnerLocationListTests` expected `.loc-count` to read `2 games` for a location holding "Example Game 1" (two copies) and its expansion, while its own delete dialog says 3. Counting copies was what the script's recount already did.
- **Fix:** the server render counts copies, the test expects `3 games`.
- **Files modified:** Cabinet.Service/Pages/Owner/Locations.cshtml.cs (`ItemsFor`), Cabinet.BrowserTests/OwnerLocationListTests.cs
- **Commit:** 86edd7d

**3. [Rule 3 - Blocking] The `OwnerLocationTests` posts moved into Task 1**
- The handler contract change makes the old posts refuse, so the existing posts carry `collectionId` in the Task 1 commit instead of Task 2 (the refusal case is in the same commit). No behaviour changed.

No other deviations.

## Open risks

- **Picture edits drop per-copy location entries (reachable now).** `Cabinet.Domain/Owner/PictureEdits.cs` (`SetChoice`, `UseLink`, `RemoveLink`, `LooksRight`) finds the game's entry with `FirstOrDefault(game => game.GameId == gameId)` and rewrites with `data.Games.Where(game => game.GameId != gameId)`, so every other entry of the game is removed. Once the owner places one copy, the game holds per-copy entries, and a picture edit on that game from the Pictures page, or the link worker that goes through the same helpers, collapses them to one entry, losing the other copies' places. The Locations page now creates such entries for the first time. Not changed here (those files belong to later plans); the plan that moves picture edits to the entry key must keep location entries intact, and until it lands a picture edit on a multi-copy game that was placed per copy is lossy.
- **Pictures page single picker.** `LocationsModel.RowFor` (used by `Pictures.cshtml.cs` `LoadSingle`) now returns the first copy's row, so that picker shows and places the first copy only. It does not drop entries; the pictures plan replaces it with one picker per copy.
- **Bulk move is still game-keyed.** The checkbox value and the bulk-move post carry the game id, and `LocationEdits.MoveMany` sets every entry of the game. Ticking one copy of a multi-copy game and moving it moves all copies; the other copies' pickers on the page stay on their old value until reload. Per-copy bulk move is owned by the plan that follows (counts and search per copy are in the same plan).

## Known Stubs

None.

## Verification

- `dotnet build Cabinet.slnx`: succeeded, 0 warnings.
- `dotnet test --project Cabinet.UnitTests` (full): 2808 passed.
- `dotnet test --project Cabinet.IntegrationTests` (full): 502 passed, 1 failed (`TestHostCleanupTests`, inotify count, ignored per host notes).
- `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.Owner*"` with the DejaVu fontconfig: 213 passed, including `OwnerPerCopyLocationTests`.
- `node --test build/tests/*.test.mjs`: 494 passed. `bash build/tests/check-owner-routes-test.sh`: 19 checks, 0 failures.
- `build/lint/checks/10-repo-rules.sh` and the workflow lint passed; the secrets step was not run (cannot run inside a linked worktree).
- Acceptance grep for the four game-keyed accessors prints 0; `grep -c collectionId Cabinet.Service/wwwroot/js/owner-games.js` is 1 or more.

## Threat Flags

None beyond the plan's register: the foreign-collection-id mitigation is in place and tested, and copy tags carry `dir="auto"` and are rendered through Razor encoding.

## Self-Check: PASSED

- Created files exist: Cabinet.IntegrationTests/OwnerPerCopyLocationTests.cs, Cabinet.BrowserTests/OwnerPerCopyLocationTests.cs.
- Commits found: 86edd7d, 1fca64f.

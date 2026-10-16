---
phase: 07-game-night-filters-location-cabinets
plan: 13
subsystem: owner-data
tags: [owner-data, pictures, per-copy, overlay, mapper, data-loss]
requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "07-03 OwnerGame.CollectionId, OwnerGames.Find/SplitForEdit/Ordered; 07-08 per-copy Locations page"
provides:
  - "OwnerOverlay.EntryPictureOverride(long collectionId) in place of the game-keyed PictureOverrideOf"
  - "SnapshotMapper picks each copy's art and hashes each entry's override into the collection version"
  - "PictureEdits.Choose(data, gameId, collectionId, copies, ...) and PictureEdits.LooksRight(data, gameId, collectionId, copies, ...)"
  - "Game-level PictureEdits forms (Choose, UseLink, RemoveLink, LooksRight) mean every copy and never drop another entry or a location"
affects: [07-16, 07-18, 07-20]
actuals:
  tokens: 12800
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns:
    - "Every PictureEdits rewrite goes through one helper (Rewrite) that replaces only the edited game's entries with the complete list and keeps all other games' entries"
key-files:
  created:
    - Cabinet.UnitTests/Owner/PictureEditsKeepCopiesTests.cs
  modified:
    - Cabinet.Domain/Owner/PictureEdits.cs
    - Cabinet.Domain/Owner/OwnerOverlay.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.UnitTests/Owner/OwnerOverlayTests.cs
    - Cabinet.UnitTests/Collection/SnapshotMapperOverlayTests.cs
    - Cabinet.IntegrationTests/OwnerPictureChoiceTests.cs
decisions:
  - "A copy's own picture setting wins even when it is Automatic (the copy opts out of a game-wide choice); a copy entry that holds only a place falls back to the game-wide picture"
  - "A game-level edit applies to every existing entry of the game and adds the game-wide entry when none exists; an entry that cannot take the edit (Link choice without a link) keeps its picture"
  - "A per-copy edit judges the change against what applies to the copy now (own picture, else game-wide) and splits the game-wide entry only when the edit changes something"
status: complete
metrics:
  completed: 2026-10-16
---

# Phase 7 Plan 13: Each copy's box is drawn from the picture chosen for that copy Summary

The overlay resolves a picture per collection entry (own choice, else the game-wide one), the mapper draws and versions each copy from it, and the picture edits gain per-copy Choose and LooksRight forms that split a game-wide entry first, while every edit path of `PictureEdits` now keeps all other entries of the game and their locations.

## What was built

- **Task 1 (tracer), commit 273e382:** `OwnerOverlay.EntryPictureOverride(collectionId)` replaces `PictureOverrideOf(gameId)` (zero remaining references); `SnapshotMapper.Explain` and `Version` use `item.CollectionId`; `PictureEdits` rewritten around one `EditEveryCopy` / `EditOneCopy` / `Rewrite` helper set; per-copy `Choose` and `LooksRight` added. Integration case `A_choice_saved_for_one_copy_changes_only_that_copys_box` passes: with three copies of "Quiet Quarry", choosing the main picture for the second copy through `OwnerDataService.MutateAsync` changes only that copy's cover on the public cards and changes the layout tag.
- **Task 2, commit 4ebd1a8:** mapper tests (each copy's art from its own override, game-wide choice applies to copies with none, a change on one copy changes only that copy and the version, the same choice on a different copy gives a different version) and the data-loss tests below.

## Data-loss risk closed (picture edits dropped other copies' places)

Before, `Choose`, `UseLink`, `RemoveLink` and `LooksRight` found the game with `FirstOrDefault(GameId)` and rewrote with `Where(GameId != gameId)`, so a picture edit on a game whose copies had their own places removed every other entry and lost those places. Now every rewrite path ends in `Rewrite`, which replaces the edited game's entries with a complete list and keeps all other entries, and the game-level forms edit every entry's picture only.

Public methods covered, each by a test in `PictureEditsKeepCopiesTests` for a game owned twice with the copies in different locations (both copies' location ids unchanged, checked on the stored entries and through `OwnerOverlay.EntryLocationId`):

- `PictureEdits.Choose` (game-level, every copy)
- `PictureEdits.Choose` (per-copy), plus a variant starting from a game-wide entry
- `PictureEdits.UseLink`
- `PictureEdits.RemoveLink`
- `PictureEdits.LooksRight` (game-level)
- `PictureEdits.LooksRight` (per-copy)
- plus a test that an edit of one game leaves another game's entries intact

Not built (later plans own them): the per-copy link flow and the Pictures page rows. `PictureRows` still reads a game's picture with `FirstOrDefault(GameId)`; it only reads, so it loses nothing, but it shows the first entry's picture until plan 07-16 moves it to the entry key. `OwnerLinkWorker` maps over entries with `Select` (keeps all entries) and was left alone.

## Deviations from Plan

### Auto-fixed Issues

None for correctness. One addition beyond the plan's file list: a new test file `Cabinet.UnitTests/Owner/PictureEditsKeepCopiesTests.cs` carries the per-method data-loss tests the orchestrator asked for.

## Known Stubs

None.

## Verification

- `dotnet build Cabinet.slnx`: succeeded, 0 warnings.
- `dotnet test --project Cabinet.UnitTests` (full): 2855 passed, 0 failed.
- `dotnet test --project Cabinet.IntegrationTests` (full): 508 passed, 1 failed (`TestHostCleanupTests`, expected under the polling watcher).
- `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.Owner*"`: 213 passed (run with the DejaVu fontconfig).
- `bash build/lint/checks/10-repo-rules.sh`: passed. The secrets step cannot run in a linked worktree and was skipped.
- `PictureOverrideOf(` references in Domain, Service, UnitTests, IntegrationTests, BrowserTests: 0.

## Threat Flags

None. No new endpoint or source: choices still use only stored, checked image records (T-07-13-01), and each entry's override is hashed into the version and unit tested (T-07-13-02).

## Self-Check: PASSED

- Created file exists: `Cabinet.UnitTests/Owner/PictureEditsKeepCopiesTests.cs`.
- Commits found: 273e382, 4ebd1a8.

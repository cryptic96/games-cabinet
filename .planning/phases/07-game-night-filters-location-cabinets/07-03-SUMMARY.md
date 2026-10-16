---
phase: 07-game-night-filters-location-cabinets
plan: 03
subsystem: owner-data
tags: [owner-data, schema, per-copy, overlay, restore]
requires: []
provides:
  - "OwnerGame.CollectionId (trailing, nullable); OwnerData.CurrentSchemaVersion = 3"
  - "OwnerGames.Find(data, gameId, collectionId) and OwnerGames.SplitForEdit(data, gameId, copies), plus OwnerGames.Ordered"
  - "OwnerOverlay per-copy places: Locations, EntryLocationId, EntryLocationName, EntryOwnLocationId, EntryFollowsBaseGameId"
  - "LocationEdits.Assign(data, gameId, collectionId, copies, locationId, title, followsBase) per copy; game-level Assign means every copy"
  - "UpgradeToCurrent chain 1 to 2 to 3; restore pair uniqueness on (game, collection entry)"
affects: [07-08, 07-09, 07-13, 07-18, 07-20]
tech-stack:
  added: []
  patterns:
    - "Decision keyed by collection entry with a game-wide fallback (null CollectionId), read only through OwnerGames.Find and the overlay"
key-files:
  created:
    - Cabinet.Domain/Owner/OwnerGames.cs
    - Cabinet.UnitTests/Owner/OwnerPerCopyRoundTripTests.cs
  modified:
    - Cabinet.Domain/Owner/OwnerData.cs
    - Cabinet.Domain/Owner/OwnerOverlay.cs
    - Cabinet.Domain/Owner/LocationEdits.cs
    - Cabinet.Repository/Storage/OwnerDataStore.cs
    - Cabinet.Repository/Storage/OwnerDataExport.cs
    - Cabinet.UnitTests/Owner/OwnerOverlayTests.cs
    - Cabinet.UnitTests/Owner/LocationEditsTests.cs
    - Cabinet.UnitTests/Owner/OwnerDataUpgradeTests.cs
    - Cabinet.UnitTests/Owner/OwnerDataStoreTests.cs
    - Cabinet.UnitTests/Owner/OwnerRestoreParserTests.cs
    - Cabinet.IntegrationTests/CardsEndpointTests.cs
    - Cabinet.IntegrationTests/OwnerCabinetTests.cs
    - Cabinet.IntegrationTests/LookRoundTripTests.cs
    - Cabinet.IntegrationTests/OwnerBackupTests.cs
    - Cabinet.IntegrationTests/OwnerLocationTests.cs
    - Cabinet.IntegrationTests/OwnerRestoreTests.cs
decisions:
  - "Owner confirmed add-alongside: OwnerGame gains an optional trailing CollectionId, null means every copy of the game"
  - "A copy's own entry wins over the game-wide entry even when its location is empty; an expansion with no place follows the base's first copy (lowest collection entry id)"
  - "Game-level Assign and MoveMany set the location on every entry of the game and add a game-wide entry when none exists, so per-copy entries are never collapsed"
status: complete
metrics:
  completed: 2026-10-16
actuals:
  tokens: 21000
  tasks: 3
  commits: 2
---

# Phase 7 Plan 03: Each copy of a game gets its own place Summary

Schema 3 of the owner data keys choices by collection entry (with a game-wide fallback), the overlay resolves a location per copy, and each copy's own location reaches its public card, with the upgrade, rollback copy, restore pair checks and a round-trip invariant pinned by tests.

## Task 1: the format decision (resolved before dispatch)

Checkpoint decision, gate `blocking-human`, already resolved by the orchestrator with the owner. Before choosing, the owner was told in plain words that option-a is what every plan of this phase is written for, and that option-b would stop execution and send plan 07-03 and plans 07-08, 07-09, 07-13, 07-18 and 07-20 back to `/gsd-plan-phase` revision before any code is written.

Owner's answer, verbatim: "A: copy key beside game (Recommended)"

Recorded as option-a (add-alongside): `OwnerGame` gains an optional trailing `CollectionId`; null means every copy. Execution continued with Tasks 2 and 3.

## What was built

- **Task 2 (tracer), commit 286f0ba:** `OwnerGame.CollectionId`, `CurrentSchemaVersion = 3`, new `OwnerGames` (`Find`, `SplitForEdit`, `Ordered`), the per-copy `OwnerOverlay` (entry-keyed accessors, `Locations`, `Effective` by collection entry, game-keyed accessors kept and resolved through the game's first copy), per-copy `LocationEdits.Assign` that splits a game-wide entry first, entry-counting `CountLeftNotPlaced`, the 1 to 2 to 3 `UpgradeToCurrent` chain, and `(game, collection entry)` pair uniqueness with positive collection ids in the restore check. The integration case `Each_copy_of_a_game_shows_its_own_place_on_the_public_card` passes: copy 2 on Shelf B, copies 1 and 3 on Cabinet A, on the public port.
- **Task 3, commit 16de151:** upgrade tests (schema-2 file loads unchanged on disk; first save keeps a schema-2 copy the earlier release lists; save refused when that copy cannot be written; schema-2 restore reports format 2), restore pair tests (differing collection ids accepted, duplicate pair, ids 0 and below and an unknown per-copy location refused), and `OwnerPerCopyRoundTripTests` (game owned once, game owned three times with a game-wide plus a per-copy entry, an older game-wide entry, an edit of one copy, the public card, a stored schema-2 file).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing tests that hard-coded the old format number moved to the new one in the Task 2 commit**
- **Found during:** Task 2 full test run
- **Issue:** the schema bump from 2 to 3 turned eight unit tests and several integration tests red (expected format 2 / "newer than 2" / "schemaVersion 3 is newer", look round trip text, backup and restore wording "reads up to 2").
- **Fix:** updated the literals (current is 3, newer is 4) in `OwnerDataUpgradeTests`, `OwnerDataStoreTests`, `OwnerRestoreParserTests` and the integration classes `LookRoundTripTests`, `OwnerBackupTests`, `OwnerRestoreTests`, `OwnerLocationTests`. The integration files were not in the plan's file list; they only carry the format literal.
- **Commit:** 286f0ba

**2. [Rule 1 - Bug] Game-level edits would have collapsed per-copy entries**
- **Found during:** Task 2 (`MoveMany` and the game-level `Assign` used `FirstOrDefault` and removed every entry of the game)
- **Fix:** both go through one private `PlacedEverywhere` that sets the location on every entry of the game and adds a game-wide entry when none exists.
- **Commit:** 286f0ba

None of the plan's behaviours changed.

## Known Stubs

None.

## Deferred Issues

- `PictureEdits` (`SetChoice`, `UseLink`, `RemoveLink`, `LooksRight`), `PictureRows`, `OwnerLinkWorker` and the Pictures page still look a game's picture decision up with `FirstOrDefault(game => game.GameId == gameId)` and rewrite by removing every entry of the game. Per-copy entries only come from the per-copy `Assign`, which no owner page calls until plan 07-08, so nothing reaches this today. Plan 07-13 moves picture edits to the entry key and must keep per-copy location entries intact when it does; if plan 07-08 lands first, a picture edit on a game that has per-copy entries would drop them. Not fixed here because those files belong to plans 07-13, 07-16 and 07-18.
- `OwnerDataFigures.PlacedGames` counts distinct games with a location (not copies); the restore summary wording follows plan 07-12.
- `TestHostCleanupTests` fails on this host because it counts inotify instances (host note); every other unit and integration test passes.

## Verification

- `dotnet build Cabinet.slnx`: succeeded, 0 warnings.
- `dotnet test --project Cabinet.UnitTests` (full): 2765 passed, 0 failed.
- `dotnet test --project Cabinet.IntegrationTests` (full): 498 passed, 1 failed (`TestHostCleanupTests`, ignored per host notes).
- Owner browser classes were not run (Playwright suites run one at a time after merge); the browser project compiles in the solution build and still uses the game-level `Assign`.
- `bash build/lint/checks/10-repo-rules.sh`: passed. The secrets step cannot run inside a linked worktree and was skipped.
- Acceptance greps: `CurrentSchemaVersion = 3` count is 1; the game-keyed accessors are called outside `OwnerOverlay.cs` only from `Cabinet.Service/Pages/Index.cshtml.cs` and `Cabinet.Service/Pages/Owner/Locations.cshtml.cs`.

## Threat Flags

None. Per-copy locations on the public card were accepted in the plan's threat register; no new endpoint or trust boundary was added.

## Self-Check: PASSED

- Created files exist: `Cabinet.Domain/Owner/OwnerGames.cs`, `Cabinet.UnitTests/Owner/OwnerPerCopyRoundTripTests.cs`.
- Commits found: 286f0ba, 16de151.

---
phase: 07-game-night-filters-location-cabinets
plan: 09
subsystem: owner-data
tags: [owner-data, expiry, per-copy, restore, hand-over]
requires: ["07-03"]
provides:
  - "OwnerExpiry.Reconcile(data, listedGameIds, listedCollectionIds, now) with the hand-over, and OwnerExpiry.IsListed"
  - "OwnerDataService.ReconcileAsync(listedGameIds, listedCollectionIds, ct); RestoreAsync takes owned game ids and owned collection entries"
  - "CollectionPublisher.ListedCopies(snapshot): collection entry to game"
affects: [07-13, 07-18]
tech-stack:
  added: []
  patterns:
    - "A choice is listed by its collection entry when it is per copy and by its game when it holds for every copy (OwnerExpiry.IsListed), shared by expiry, restore stamping and the restore preview count"
key-files:
  created: []
  modified:
    - Cabinet.Domain/Owner/OwnerData.cs
    - Cabinet.Service/Owner/OwnerDataService.cs
    - Cabinet.Service/Owner/CollectionPublisher.cs
    - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
    - Cabinet.UnitTests/Owner/OwnerExpiryTests.cs
    - Cabinet.UnitTests/Owner/OwnerDataServiceTests.cs
    - Cabinet.UnitTests/Owner/OwnerRestoreTests.cs
    - Cabinet.IntegrationTests/OwnerRestoreTests.cs
decisions:
  - "listedCollectionIds is a map from collection entry to game, not a plain set, because the hand-over has to know which game a new entry belongs to"
  - "A game with a game-wide choice never takes part in the hand-over: its new copies are already covered by that choice"
  - "A donor whose 30 days are already used up is not handed over; it is forgotten as before"
status: complete
metrics:
  completed: 2026-10-16
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 7 Plan 09: A copy that leaves and comes back keeps its place Summary

The 30-day rule now judges owner choices per copy (by collection entry, or by game for a game-wide choice), a newly listed copy takes over the kept place and picture of an absent copy of the same game, and restores stamp and count absent copies by the same rule.

## What was built

- **Task 1 (tracer), commit bb6895e:** `OwnerExpiry.Reconcile` takes the listed collection entries (entry to game map). A per-copy choice is stamped, cleared or forgotten by its entry; a game-wide one by its game. The hand-over: each absent, not yet expired per-copy choice of a game with no game-wide choice goes to the lowest listed entry of that game that has no choice of its own (donors and recipients paired in ascending entry order), unstamped, and the absent choice is dropped; the result keeps the stored order. `ReconcileAsync` and `CollectionPublisher` pass both sets. Because the restore code shares the "listed" rule and the signatures had to compile together, `RestoreAsync`/`Restored` and the Settings page (`SideOf`, owned copies) moved in this commit too.
- **Task 2, commit bc93973:** integration case `A_restored_copy_the_collection_no_longer_holds_is_stamped_and_counted` on a booted host with a synced collection: an unheld per-copy entry is stamped absent, a held per-copy entry and a game-wide entry of a held game stay unstamped, and the restore preview's "Not in your collection now" count is 1.
- Unit tests: per-copy stamp, clear and 30-day drop; game-wide listed by game; the hand-over (choices and picture, stamped donor still kept, expired donor lost, two new copies lowest first, new copy with own choice left alone, game-wide choice blocks hand-over, never across games); an end-to-end publish case (copy removed then re-added comes back to Shelf B); a writer-level case.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `listedCollectionIds` is an entry-to-game map**
- **Issue:** the plan names a set of collection ids, but the hand-over needs to know which game a new entry belongs to, which a bare set cannot say.
- **Fix:** `IReadOnlyDictionary<long, int>` (collection entry to game id) for `Reconcile`, `ReconcileAsync`, `RestoreAsync` and the Settings page, built by `CollectionPublisher.ListedCopies`.
- **Commit:** bb6895e

**2. [Rule 3 - Blocking] Task 2 service changes landed in the Task 1 commit**
- **Issue:** the restore signature change shares the listed rule and had to compile with the new `OwnerExpiry`; the existing `OwnerRestoreTests` and `OwnerDataServiceTests` call sites were updated for the new parameter in the same commit.
- **Commit:** bb6895e

**3. [Plan wording] "with a revision step"**
- The plan's service test bullet says the hand-over is written with a revision step. Housekeeping writes by design keep the revision (an existing test and the undo/stale-writer rules depend on that), so the new service test asserts the hand-over is stored, the revision and the change log are untouched.

## Known Stubs

None.

## Deferred Issues

- `TestHostCleanupTests` fails on this host because it counts inotify instances (host note); everything else passes.
- Picture edits still look a choice up by game (see the plan that moves them to the entry key); the hand-over carries a copy's picture choice with its place.

## Verification

- `dotnet build Cabinet.slnx`: 0 warnings, 0 errors.
- `Cabinet.UnitTests` full: 2818 passed.
- `Cabinet.IntegrationTests` full: 499 passed, 1 failed (`TestHostCleanupTests`, ignored per host notes).
- `Cabinet.BrowserTests` classes `Owner*`: 212 passed.
- `build/lint/checks/10-repo-rules.sh`: passed; the secrets step cannot run in a linked worktree and was skipped.
- Acceptance: `grep -c listedCollectionIds Cabinet.Domain/Owner/OwnerData.cs` is well above 2; `CollectionPublisherRaceTests` passes.

## Threat Flags

None.

## Self-Check: PASSED

- Commits found: bb6895e, bc93973.

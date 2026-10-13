---
phase: 06-owner-tools-persistence
plan: 10
subsystem: owner-data-durability
tags: [owner-data, versions, expiry, live-notification, publisher-lock, timers]
requires:
  - phase: 06-08
    provides: "OwnerDataService single writer, CollectionPublisher, OwnerDataStore, OwnerOverlay"
  - phase: 06-09
    provides: "Undo slot, OwnerTestKit, OwnerSession"
provides:
  - "Ten earlier versions of owner-data.json kept in owner-data-versions/ (copy at most hourly on the injected clock, oldest pruned, read by index only)"
  - "OwnerExpiry.Reconcile and OwnerDataService.MutateSystemAsync/ReconcileAsync: the 30-day rule on accepted snapshots, written without a revision bump, log line or download count"
  - "CollectionPublisher.Publish reconciles after publishing, outside its lock"
  - "OwnerChangeNotifier: one coalesced live notification per burst of owner saves (3 s quiet period, 15 s ceiling, only when the collection version differs)"
  - "OwnerDataSeed and OwnerPersistenceTests: owner data survives a sync round, an interleaved sync commit and a restart"
affects: [pictures, settings, backup, owner-pages]
tech-stack:
  added: []
  patterns:
    - "System writes share the owner writer's lock but are not owner changes: no revision, no log, no count, no Changed event, and they end the undo slot"
    - "Housekeeping triggered by a published snapshot runs after the publisher's lock is released, because a save holds the writer's turn and then asks for the publisher lock"
    - "A coalescing notifier on one TimeProvider timer: reset to the quiet period on each event, clamped to a ceiling after the first unannounced one"
key-files:
  created:
    - Cabinet.Service/Owner/OwnerChangeNotifier.cs
    - Cabinet.IntegrationTests/Infrastructure/OwnerDataSeed.cs
    - Cabinet.IntegrationTests/OwnerPersistenceTests.cs
    - Cabinet.UnitTests/Owner/OwnerExpiryTests.cs
    - Cabinet.UnitTests/Owner/OwnerChangeNotifierTests.cs
    - Cabinet.UnitTests/Owner/CollectionPublisherRaceTests.cs
  modified:
    - Cabinet.Domain/Owner/OwnerData.cs
    - Cabinet.Repository/Storage/OwnerDataStore.cs
    - Cabinet.Service/Owner/OwnerDataService.cs
    - Cabinet.Service/Owner/CollectionPublisher.cs
    - Cabinet.Service/Owner/OwnerEndpoints.cs
    - Cabinet.UnitTests/Owner/OwnerTestKit.cs
    - Cabinet.UnitTests/Owner/OwnerDataStoreTests.cs
    - Cabinet.UnitTests/Owner/OwnerDataServiceTests.cs
key-decisions:
  - "System writes do not raise Changed: the owner overlay never reads the 30-day stamp, so they cannot change what visitors see, and the notifier and republish stay out of housekeeping"
  - "The version copy is best effort: an IO failure while copying is logged and the save still goes ahead, so a full or read-only versions folder can never lock the owner out of saving"
  - "Publish blocks on ReconcileAsync (sync-over-async) after releasing its lock; the wait is at most one small file write and Publish has no async callers to change"
  - "The notifier remembers the last collection version it sent and resets it when a send fails, so a failed send is retried by the next burst even when the version did not move again"
patterns-established:
  - "OwnerExpiry is a pure function in the domain project, tested without a clock or a disk"
requirements-completed: [OWN-02]
duration: about 95min
completed: 2026-10-12
status: complete
actuals:
  tokens: 16000
  tasks: 3
  commits: 3
---

# Phase 6 Plan 10: Owner data that lasts - earlier versions, the 30-day rule, surviving syncs and restarts, quiet live updates Summary

**Owner data now outlives syncs, restarts and releases: ten earlier versions sit beside the file, games that left the collection are forgotten only after 30 days of accepted snapshots, a save made during a sync survives the sync's commit, and open pages hear about owner saves once per burst.**

## What was built

- **Tracer (Task 1):** `OwnerDataSeed` (writes invented owner data through the real store, usable by later browser tests) and `OwnerPersistenceTests`: a sync that renames another game leaves `owner-data.json` byte-identical and the placed game in `Cabinet A`; an owner save made while the fake BGG answer is held open survives the sync's commit and both changes are on `/cabinet/cards`; a second host on the same state directory shows the location and the sync's data without a new sync; a host started on seeded owner data shows the seeded location.
- **Earlier versions (Task 2):** `IOwnerDataStore` gained `KeepEarlierVersion`, `EarlierVersions`, `ReadEarlierVersion(index)` and `NewestEarlierVersionAt`; `EarlierVersion` record beside it. `OwnerDataStore` copies the file's bytes (readable or not) atomically to `owner-data-versions/owner-data.{yyyyMMddTHHmmssZ}.json`, keeps `KeptVersions = 10`, lists newest first from names it parses itself, reads only by index into that listing, ignores other files in the folder and removes stray temporary files when it starts. The writer keeps a copy before any write (owner change, undo, system write) when none exists or the newest is more than `VersionInterval` (one hour, injected clock) old.
- **The 30-day rule (Task 2):** `OwnerExpiry.Reconcile` (pure, in the domain project): a game the accepted collection does not list is stamped with the time of that first collection (first stamp wins), a returning game loses the stamp and keeps its location and picture choice, a game whose stamp is 30 days old is removed. `OwnerDataService.MutateSystemAsync` applies it under the writer's lock with no revision bump, no log entry, no download count, no `Changed` event, clears the undo slot, does nothing while the file is unreadable and survives a failed save. `CollectionPublisher.Publish` calls `ReconcileAsync` after releasing its own lock. Held-back results never reach `Publish`, so they never start a clock.
- **Live notification (Task 3):** `OwnerChangeNotifier` (hosted service registered in `AddCabinetOwner`): one `TimeProvider` timer, reset to 3 s on each save but never later than 15 s after the first unannounced save; on firing it compares `SyncStatusService.Current().SnapshotVersion` with the last version sent and calls `PublishAsync` only when it differs. The payload is the public status only.

## Verification

- Unit project 2102 and integration project 332 pass; `bash build/lint/checks/10-repo-rules.sh` and `bash build/lint/checks/40-secrets.sh` pass (gitleaks over history and tree, no leaks).
- Store (6 new): copy of an unreadable file, nothing kept without a file, exactly the newest 10 after 12 copies, index-only reads with junk files ignored, stray temporary files removed on start, no change to the live file. Writer (4 new): first write keeps, +30 min does not, +61 min does, no-op and refused writes keep nothing, undo keeps when due. Expiry (7): stamp, day 29, day 30; return clears and keeps location; return keeps a picture choice and the clock restarts; unreadable store untouched; no log/count/revision and undo cleared; no write when nothing changes; a stale writer is not out of date after housekeeping. Notifier (7): five saves 1 s apart give one call at +3 s after the last, saves every 2 s give calls at 15 s and 31 s, a burst that ends where it began sends nothing, a failing send is not fatal, stop and dispose are safe. Integration: a held-back empty answer twice leaves the owner file bytes unchanged and no stamp; a connected page hears one status with the new collection version and no location text three seconds after an owner save.
- **Lock check (acceptance criterion):** the interleaved integration test does NOT fail with the publisher's lock removed. Its save lands while the sync is waiting for BGG, so the sync's later build simply reads the latest owner data. I removed the lock locally three times and it stayed green. To make the criterion real I added `CollectionPublisherRaceTests` (2000 rounds of a publish racing a save, then the shown version is compared with a fresh build): it fails with the lock removed (3, 3 and 4 mismatches in three runs) and passes with the lock (five runs).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Host shutdown threw from the notifier**
- **Found during:** Task 3, full integration run (284 of 331 failed)
- **Issue:** The generic host disposes a singleton `IDisposable` hosted service and then calls `StopAsync` on it; `StopAsync` cancelled an already disposed `CancellationTokenSource`, so every factory disposal threw `ObjectDisposedException`.
- **Fix:** One `Shut` routine under the lock for both paths, idempotent, with a closed flag; a unit test covers stop after dispose.
- **Files modified:** Cabinet.Service/Owner/OwnerChangeNotifier.cs, Cabinet.UnitTests/Owner/OwnerChangeNotifierTests.cs
- **Commit:** 92de6eb

**2. [Rule 2 - Missing critical] A test that proves the publisher lock**
- **Found during:** Task 1, lock-removal check (see Verification)
- **Issue:** The plan's interleaved test cannot detect a missing lock, yet T-06-10-02 relies on the lock.
- **Fix:** Added `CollectionPublisherRaceTests` (new file, not in the plan's list).
- **Commit:** d977a10

**3. [Rule 2 - Missing critical] Version copy failures must not block saving**
- **Issue:** An IO error while copying a version inside the save's try block would have been reported as `save-failed` on every write.
- **Fix:** The copy has its own try/catch that logs the exception type and lets the save continue.
- **Commit:** d977a10

### Process

- **Tracer gate:** Auto mode is off, but the plan is autonomous and the tracer verify is fully automated, so I ran it green and continued, as instructed.
- **Red first:** The tracer's tests passed on first run (the publisher already built from the latest raw snapshot and owner data under its lock); this plan's job there was to prove it, plus the lock check above. Tasks 2 and 3 were red as compile failures (the new members and `OwnerChangeNotifier` did not exist), not as assertion failures.
- **Test helper:** `OwnerTestKit` gained `Listing(params int[] gameIds)` and the in-memory store gained `FileExists` and `KeptAt`.
- **Not run:** The browser test project was not run; this plan changes no page markup, script or stylesheet.

## Known Stubs

None.

## Threat Flags

None. The surface matches the plan's register: reconcile runs only after a published snapshot and never while unreadable (T-06-10-01); the publisher lock and the new race test cover T-06-10-02; versions are read by index from the store's own listing with server-generated names (T-06-10-03); ten versions at most one per hour and coalesced notifications (T-06-10-04); the notification carries the public status only and a test checks the received message holds no location text (T-06-10-05).

## Self-Check: PASSED

- Created files exist: OwnerChangeNotifier.cs, OwnerDataSeed.cs, OwnerPersistenceTests.cs, OwnerExpiryTests.cs, OwnerChangeNotifierTests.cs, CollectionPublisherRaceTests.cs.
- Commits 4d8da58, d977a10 and 92de6eb exist on the worktree branch.

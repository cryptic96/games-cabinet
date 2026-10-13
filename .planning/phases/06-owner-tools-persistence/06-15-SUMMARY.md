---
phase: 06-owner-tools-persistence
plan: 15
subsystem: owner-pictures
tags: [owner-data, image-pipeline, background-worker, art-cleanup, token-less-fetch]
requires:
  - phase: 06-08
    provides: "the single owner-data writer and MutateSystemAsync"
  - phase: 06-10
    provides: "CollectionPublisher that rebuilds the visitors' view when the owner data changes"
  - phase: 06-14
    provides: "the forced Link pick in the mapper and PictureLink records in OwnerOverlay"
provides:
  - "ArtRecords.FromProcessed: one builder of processed picture records for the sync and the owner"
  - "OwnerLinkFetcher.CheckAsync with LinkCheck.Ready/Failed and ProcessedLink: capped token-less fetch and processing of an owner link"
  - "OwnerLinkWorker: background fetch of links without a usable copy, wakeable and hourly"
  - "IOwnerArtReferences / OwnerDataService.ReferencedArtFiles / RequestLinkWork / LinkWorkRequested"
  - "OwnerDataService.MutateSystemAsync(announce:) for system writes that change what visitors see"
  - "LinkHealth.IsDead / AfterFailure / AfterSuccess with DeadAfterFailures and DeadAfterSpan"
affects: [pictures, settings, owner-links]
actuals:
  tokens: 14100
  tasks: 2
  commits: 4
tech-stack:
  added: []
  patterns:
    - "The art clean-up asks the owner data for the files it must keep, and does nothing at all when the owner data is unreadable (unknown is not empty)"
    - "A system write that changes what visitors see asks to be announced; housekeeping stays quiet"
key-files:
  created:
    - Cabinet.Service/Sync/ArtRecords.cs
    - Cabinet.Service/Owner/OwnerLinkFetcher.cs
    - Cabinet.Service/Owner/OwnerLinkWorker.cs
    - Cabinet.Domain/Owner/LinkHealth.cs
    - Cabinet.IntegrationTests/OwnerLinkTests.cs
    - Cabinet.UnitTests/Owner/LinkHealthTests.cs
    - Cabinet.UnitTests/Owner/OwnerLinkFetcherTests.cs
    - Cabinet.UnitTests/Owner/OwnerArtReferencesTests.cs
  modified:
    - Cabinet.Service/Sync/ArtSync.cs
    - Cabinet.Service/Sync/SyncRunner.cs
    - Cabinet.Service/Sync/SyncEndpoints.cs
    - Cabinet.Service/Owner/OwnerDataService.cs
    - Cabinet.Service/Owner/OwnerEndpoints.cs
    - Cabinet.UnitTests/Sync/ArtSyncTests.cs
    - Cabinet.IntegrationTests/Infrastructure/ScriptedImageHandler.cs
key-decisions:
  - "The worker wakes on the hourly timer, on RequestLinkWork and on every owner-data change, so an undo or a restore that removes a stored copy is repaired without waiting an hour"
  - "A failed fetch is left as it is and tried again at the next turn; failures are not recorded in the link yet, so the dead-link rule is ready (LinkHealth) but nothing calls AfterFailure until the link plan that classifies failures"
  - "A link counts as having a usable copy only when its record is Ok, has files and features, carries the current analysis version and every file is in the art directory"
  - "SyncRunner receives the owner references as a trailing optional argument; null (tests without the owner tools) keeps the old prune"
requirements-completed: [IMG-02, OWN-02]
duration: about 1h 10m
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 15: Linked pictures stay Summary

**A picture the owner linked is fetched in the background through the same credential-free picture client the sync uses, stored as a system write that every visitor's card picks up, and its files can never be removed by the art clean-up.**

## Accomplishments

- The tracer runs over real HTTP: owner data seeded with a Link choice and no record is picked up by `OwnerLinkWorker` right after start, the scripted image host sees exactly one request for it with no Authorization header and no Cookie header, the record lands in the owner data with its files in the art directory, and `/cabinet/cards` shows the link's picture for that game.
- A link that failed first and is served later is fetched when `RequestLinkWork` is called; the stored record is announced through `Changed`, the publisher rebuilds, and the card cover and the cards entity tag change.
- A sync round with every art file aged well past the clean-up grace keeps the link's files and removes a file nothing refers to. With the owner-data file unreadable the same round removes nothing and the worker makes no request. A host with background work off fetches no link.
- Mutation check: with the union of the owner references removed from `SyncRunner.Prune`, two of the five integration tests fail, so the prune protection is genuinely tested.
- `ArtRecords.FromProcessed` is the single builder of processed picture records; `ArtSync.Store` uses it and a parity test proves the sync's stored record equals the shared builder's for the same picture. All earlier `ArtSync` tests are unchanged.
- `LinkHealth` is the pure dead-link rule (two failed checks at least a day apart; one success resets) and the worker stores its records through `LinkHealth.AfterSuccess`.

## Task Commits

1. **Task 1 red:** `96617f4` test: failing tests for linked pictures fetched in the background and kept by the clean-up
2. **Task 1 green (tracer):** `f873ad3` feat: fetch linked pictures in the background and keep their files through the clean-up
3. **Task 2 red:** `b344722` test: failing tests for the dead-link rule
4. **Task 2 green:** `fafed1f` feat: one rule for when a linked picture counts as dead

The tracer's verify (unit `ArtSyncTests`, integration `OwnerLinkTests`, full integration project) was green before Task 2 started. The red commits fail at compile time because the types they use did not exist yet.

## Verification

- `ArtSyncTests`, `OwnerLinkFetcherTests`, `OwnerArtReferencesTests`, `LinkHealthTests`: all pass. `OwnerLinkTests`: 5 passed.
- Full unit project and full integration project pass with no failures.
- `10-repo-rules.sh`, `40-secrets.sh` (no leaks found) and `50-script-tests.sh` pass. No JavaScript was touched.
- `grep -c "ReferencedArtFiles" Cabinet.Service/Sync/SyncRunner.cs` is 1.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] System writes could not announce themselves**
- **Found during:** Task 1
- **Issue:** the plan says the publisher republishes through `Changed`, but `MutateSystemAsync` never raised `Changed`, so a stored link record would not have reached visitors until the next sync.
- **Fix:** `MutateSystemAsync` gained an optional `announce` argument; the worker passes true, housekeeping keeps the default of staying quiet. Revision, change log and the change count are still untouched.
- **Files modified:** `Cabinet.Service/Owner/OwnerDataService.cs`
- **Commit:** `f873ad3`

### Plan adjustments

- `Cabinet.Service/Sync/SyncEndpoints.cs` was edited (not in the plan's file list) to hand `IOwnerArtReferences` to the runner; the runner is built there and the owner publisher is already resolved in the same place.
- `Cabinet.IntegrationTests/Infrastructure/ScriptedImageHandler.cs` records whether a request carried a Cookie header (new trailing member `HasCookie`), because the plan's "no cookie" assertion needed it.
- Two unit test files beyond the plan's list (`OwnerLinkFetcherTests`, `OwnerArtReferencesTests`) cover the fetcher categories, the referenced-files rules and the announcing system write.
- The clean-up test ages the art files explicitly instead of advancing the fake clock: moving the clock past the grace also fires the scheduled sync and would race the manual sync press.
- `OwnerLinkWorker` does not record failed checks yet: the plan leaves classification of failures to the next link plan, so a failing link is simply retried at every turn (hourly, on a wake, after an owner change) at one paced request each.

### TDD note

Both red commits fail to compile rather than fail an assertion, since the types under test were new. Each behaviour was proven by the green run, and the prune protection was additionally proven by removing it and watching the tests fail.

## Authentication gates

None.

## Known Stubs

None. `LinkHealth.AfterFailure` and `IsDead` are tested but not called by production code yet; the link plan that classifies failures and the pictures page are their callers.

## Threat Flags

None beyond the plan's register. T-06-15-01 (token to the image host) is mitigated by the token-less typed client and tested by asserting no Authorization and no Cookie on the recorded request; T-06-15-02 by the referenced set and the skip while unreadable; T-06-15-03 by the existing byte, pixel, redirect and time caps and the shared pacer, one link at a time; T-06-15-04 by the address check inside the system write; T-06-15-05 by logging counts and exception types only (the fetcher logs nothing).

## Issues Encountered

None that blocked the plan.

## Self-Check: PASSED

- Created files exist: `ArtRecords.cs`, `OwnerLinkFetcher.cs`, `OwnerLinkWorker.cs`, `LinkHealth.cs`, `OwnerLinkTests.cs`, `LinkHealthTests.cs`, `OwnerLinkFetcherTests.cs`, `OwnerArtReferencesTests.cs`.
- Commits `96617f4`, `f873ad3`, `b344722`, `fafed1f` are present in the branch history.

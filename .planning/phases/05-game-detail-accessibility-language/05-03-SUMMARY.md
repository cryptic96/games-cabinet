---
phase: 05-game-detail-accessibility-language
plan: 03
subsystem: collection-version
tags: [version, etag, cards, sync]
requires: [05-02, 05-04]
provides:
  - "SnapshotMapper.Version(items, rules, snapshot): collection version that includes card-visible fields"
  - "EntityTags.MatchesIfNoneMatch shared 304 helper"
affects: [layout endpoint, cards endpoint, sync commit]
key-files:
  created:
    - Cabinet.Service/Hosting/EntityTags.cs
  modified:
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - Cabinet.Service/Cards/CardsEndpoint.cs
    - Cabinet.UnitTests/Collection/SnapshotMapperTests.cs
    - Cabinet.IntegrationTests/CardsEndpointTests.cs
    - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
decisions:
  - "Card fields are hashed with unit/record separator characters so names containing the pipe cannot collide"
  - "Stored snapshot schema untouched; only the derived version changes (one redraw after deploy)"
status: complete
metrics:
  tasks: 2
  commits: 2
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
requirements: [DET-02]
---

# Phase 5 Plan 3: Synced cards stay fresh Summary

The collection version now folds in everything the detail card shows, so a sync that only changes details replaces the running view and serves fresh cards without a restart; both data routes share one 304 helper.

## What was built

- `SnapshotMapper.Version(items, rules, snapshot)`: the existing per-item line plus year, location, player counts, play times, minimum age, weight, average, designers, mechanics and expanded game ids. The two older overloads are unchanged. `CollectionState.FromSnapshot` uses the new one, so `SyncRunner.Commit` sees details-only changes.
- Enrichment time, ranked rating, details version and non-series families do not change the version.
- `EntityTags.MatchesIfNoneMatch` replaces the private copies in the layout and cards endpoints.
- The cards route is in the strict-policy test list (OK sample, OK synced, 404 unknown profile).

## Tests

- Unit: 7 new `SnapshotMapperTests` (designers, mechanics, year/location, counts/times/ratings, expanded games, ignored fields, shape/stability). `SnapshotMapperTests` 18 pass, `ArtMappingTests` 23 pass.
- Integration: a details-only replace changes both the cards and layout tags and serves the new designers; wildcard and weak `If-None-Match` on the cards route answer 304. `CardsEndpointTests` 14, `ContentSecurityPolicyTests` 18, `LayoutEndpointTests` 18 pass.
- `build/lint/checks/10-repo-rules.sh` passes.

## Commits

- b348cbf: feat(05-03): fold card-visible fields into the collection version
- 65234c6: refactor(05-03): share one If-None-Match helper and cover the cards route with the policy test

## Deviations from Plan

- The wildcard and weak-tag 304 tests (planned for task 2) were committed with task 1 in `CardsEndpointTests.cs`, since they share the file edit; they pass against both the old and the shared helper.
- TDD red commit was not separate: tests and implementation were committed together in task 1.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

Files exist (EntityTags.cs and the modified files), commits b348cbf and 65234c6 are in the log.

---
phase: 07-game-night-filters-location-cabinets
plan: 14
subsystem: layout-and-endpoints
tags: [locations, layout, caching, endpoint, etag]
requires: ["07-02", "07-10"]
provides:
  - "LocationLayouts.Build(items, snapshot, book, design, options) returning LocationLayout(Id, Name, Layout) per location, owner order, Unknown last, empty locations left out"
  - "LocationLayouts.Serialize(layoutTag, locations) and the LocationLayoutsDocument shape { layout, layoutVersion, palette, locations: [{ id, name, sections }] }"
  - "CollectionState.LocationLayoutsFor(design, options) cached per design name inside the immutable state"
  - "LayoutCache.GetByLocation(sample, design) for invented samples; CachedLayout.ByLocationTag"
  - "GET /cabinet/layout-by-location (LocationLayoutEndpoint.Route / Handle)"
  - "docs/cabinet-layout.md: Location cabinets section"
affects: [07-22]
tech-stack:
  added: []
  patterns: ["Lazy per-design cache on the immutable collection state", "Re-run the pure engine on a per-location subset", "Entity tag derived from the big layout's tag so the page can detect mixed versions"]
key-files:
  created:
    - Cabinet.Domain/Layout/LocationLayouts.cs
    - Cabinet.Service/Layout/LocationLayoutEndpoint.cs
    - Cabinet.IntegrationTests/LocationLayoutEndpointTests.cs
    - Cabinet.UnitTests/Layout/LocationLayoutsTests.cs
  modified:
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Service/Layout/LayoutCache.cs
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - docs/cabinet-layout.md
decisions:
  - "No engine change: each location is a separate CabinetLayoutEngine.Build call on that location's items, with expansions re-paired through ExpansionPairing.Pair on the snapshot filtered to the location's entries"
  - "An owner with no locations gets an empty locations list (not just Unknown); with locations and no placed game only Unknown remains"
  - "The by-location ETag is the big layout's ETag with -by-location before the closing quote; the layout member of the document is that big tag, computed without building the big layout"
  - "The shared ETag formatting was pulled into CollectionState.LayoutTag and LayoutCache.TagOf so both payloads name one string"
status: complete
metrics:
  completed: 2026-10-16
actuals:
  tokens: 11500
  tasks: 2
  commits: 2
---

# Phase 7 Plan 14: One cabinet per storage location, built and cached on the server Summary

The server now draws one stable cabinet per storage location that holds games (owner's order, "Unknown location" last, empty locations omitted), caches the payload once per collection version and profile, and serves it at `/cabinet/layout-by-location` with the layout endpoint's hygiene (allow-listed profile and sample, ETag, no-cache, 304, no CORS).

## What was built

- **Task 1 (tracer), commit e2f3269:** `LocationLayouts` (pure, Domain), `CollectionState.LocationLayoutsFor`, `LayoutCache.GetByLocation`, `LocationLayoutEndpoint` mapped in `MapCabinetLayout`, and `LocationLayoutEndpointTests` (12 tests): fixture order and Unknown last, expansion stored apart drawn in its own location with its base title, revalidation (layout tag equality, `-by-location"` suffix, 304, stale tag, no CORS, no-cache), sample answer, 404 for bad profiles, empty list before the first sync and for an owner without locations, member-name allow-list, identical bytes on repeat.
- **Task 2, commit 14062fe:** `LocationLayoutsTests` (15 tests) and the "Location cabinets" section of `docs/cabinet-layout.md`.

## Verification

- `dotnet build Cabinet.slnx`: 0 warnings, 0 errors.
- `Cabinet.UnitTests` in full: 2853 passed; the `Category=Layout` trait (798 tests, goldens included, run without the update switch) passed and no golden file changed, so the combined cabinet is byte-identical.
- `Cabinet.IntegrationTests` in full: 519 passed, 1 failed: `TestHostCleanupTests` (the known inotify-count test, ignored per the host notes with the polling watcher on).
- Browser classes `CabinetSmokeTests`, `SectionRowAlignmentTests`, `DefaultLookIdentityTests` with the DejaVu fontconfig: 10 passed.
- `build/lint/checks/10-repo-rules.sh` passed. The secrets step was skipped (cannot run in a linked worktree).

## Deviations from Plan

**1. [Clarification] The fixture's "Lent out" location holds no game**
- The tracer behaviour bullet lists Cabinet A, Shelf B and Lent out as the cabinets. `FilterFixture` places no game in Lent out (documented in the fixture), and the plan's own truth says an empty location gets no cabinet. The test therefore asserts Cabinet A, Shelf B, Unknown, with Lent out absent.

**2. [Tracer gate, interactive run] Human checkpoint not raised**
- Auto mode is off, so the tracer gate would normally stop for a human-verify. The plan is `autonomous: true`, the config sets `human_verify_mode: end-of-phase`, and this ran as a wave worktree agent, so I re-ran the tracer's automated `<verify>` end to end instead (build plus the fixture test: passed) and continued. The working slice is reviewable through the endpoint tests.

**3. [Test design] Expansion stored apart is asserted by `isExpansion` and `baseTitle`, not by the `orphanExpansion` kind**
- A three-game location is below the few-games threshold, so the engine draws the orphan expansion face out (`cover`). The assertion checks the invariants that matter (it is an expansion, it carries the base title, no family) instead of the pose.

No other deviations.

## Known Stubs

None.

## Threat Flags

None. The new endpoint is the surface the plan's threat model covers: lazy per-version caching and 304s (T-07-14-01), only drawn fields with no CORS and a member-name test (T-07-14-02), allow-listed `sample` and `profile` never echoed (T-07-14-03).

## Self-Check: PASSED

- FOUND: Cabinet.Domain/Layout/LocationLayouts.cs, Cabinet.Service/Layout/LocationLayoutEndpoint.cs, Cabinet.IntegrationTests/LocationLayoutEndpointTests.cs, Cabinet.UnitTests/Layout/LocationLayoutsTests.cs
- FOUND commits: e2f3269, 14062fe

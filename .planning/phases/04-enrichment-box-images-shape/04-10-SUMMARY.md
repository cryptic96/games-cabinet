---
phase: 04-enrichment-box-images-shape
plan: 10
subsystem: collection-mapping
tags: [box-shape, size-estimate, orientation, pose-height, art-settings]
requires:
  - phase: 04-08
    provides: "ArtRules, ArtVerdicts, ArtChooser, SnapshotMapper rules overload"
  - phase: 04-06
    provides: "GameDetails and EnrichmentSync"
provides:
  - "BoxShape.Resolve: real sizes, then a flat cover's shape, then an estimate, then a default"
  - "SizeEstimate: banded size classes with hysteresis and a model version"
  - "CabinetItem.PoseHeightMm: verdict-free pose height read by Orientation"
  - "Art:ShapeMarginPercent and Art:OrientFromCover settings"
affects: [04-11, layout, cabinet-rendering]
tech-stack:
  added: []
  patterns:
    - "Pose decided from a height that never reads picture analysis"
    - "Stored estimate class with half-point hysteresis and model version"
key-files:
  created:
    - Cabinet.Domain/Collection/BoxShape.cs
    - Cabinet.Domain/Collection/SizeEstimate.cs
    - Cabinet.UnitTests/Collection/BoxShapeTests.cs
    - Cabinet.UnitTests/Collection/SizeEstimateTests.cs
    - Cabinet.UnitTests/Layout/PoseStabilityTests.cs
    - Cabinet.IntegrationTests/TrueProportionsTests.cs
  modified:
    - Cabinet.Domain/Collection/BoxFromVersion.cs
    - Cabinet.Domain/Collection/GameDetails.cs
    - Cabinet.Domain/Collection/ArtRules.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Domain/Layout/CabinetItem.cs
    - Cabinet.Domain/Layout/Orientation.cs
    - Cabinet.Service/Sync/EnrichmentSync.cs
    - Cabinet.Service/Layout/ArtSettings.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.UnitTests/Collection/BoxFromVersionTests.cs
    - Cabinet.UnitTests/Layout/OrientationTests.cs
    - Cabinet.UnitTests/Layout/ArtSettingsTests.cs
    - docs/bgg-sync.md
key-decisions:
  - "Disagreement test uses a 1e-9 tolerance so a ratio difference exactly at the margin keeps the real sizes despite floating point"
  - "A rebuilt front outside the plausibility bounds keeps the real sizes as they are"
  - "Non-positive weight, play time or player count count as unknown for the estimate"
  - "ArtRules fingerprint now includes the shape margin and orientation switch so a settings change changes the collection version"
patterns-established:
  - "Verdict-free pose height: layout decisions read PoseHeightMm, drawn size reads Box"
requirements-completed: [IMG-03]
duration: 40min
completed: 2026-10-07
status: complete
actuals:
  tokens: 16400
  tasks: 3
  commits: 3
---

# Phase 4 Plan 10: True box proportions Summary

**Boxes take real dimensions, a flat cover's shape, a stable weight/time/players estimate, or a default, while how a box stands is decided from a verdict-free pose height.**

## Accomplishments

- `BoxShape.Resolve` implements the size chain with the disagreement margin (default 12 percent, tolerance-safe at the boundary), area- and depth-preserving rebuild, landscape orientation from a flat landscape cover (`Art:OrientFromCover`), and a fall-back to the real sizes when a rebuilt front would be implausible.
- `SizeEstimate` gives five classes (compact to extra large) with base and expansion dimensions, half-point scores, hysteresis of half a point beyond the stored band, and a model version that forces a clean re-evaluation. `EnrichmentSync` stores the class on new details, passing the previous class.
- `CabinetItem.PoseHeightMm` is filled by the mapper (real height, else estimate, never a cover) and `Orientation` reads it for the oversize rule, the size-weighted cover chance and lie-flat eligibility. Existing layouts and goldens are unchanged.
- `PoseStabilityTests` flips every verdict (four rule sets, 65 and 400 samples, both profiles, every strategy) and proves poses never move while drawn boxes do. A mutation check (Orientation reading the drawn box) makes it fail.
- docs/bgg-sync.md has a "Box sizes" section.

## Task Commits

1. Task 1 (tracer): `a3dac2a` - size chain, estimates, pose height, settings, shape and proportion tests
2. Task 2: `a17af7f` - hysteresis stored by the details step, estimate tests
3. Task 3: `edc8c06` - pose stability, orientation and settings tests, docs

Tracer gate: the tracer's verify (build, shape tests, proportions tests, Layout category with no golden re-recorded) passed, so expansion continued without a checkpoint per the owner's decision.

## Verification

- `unshare -rn sh -c 'ip link set lo up; dotnet test --solution Cabinet.slnx'`: 1513 passed, 0 failed, no network.
- `build/lint.sh`: all checks pass (repo-rules, workflows, shell, secrets, script-tests).
- `node --test build/tests/page-scripts.test.mjs`: 92 passed.

## Deviations from Plan

**1. [Rule 1 - Plan error] `SyntheticBggCollection.Create(12)` yields only 5 entries**
- **Found during:** Task 1 (`TrueProportionsTests`)
- **Issue:** The plan assumed `Create(12)` gives a 12-entry collection with the edge-case game `Quiet Quarry`; the collection only offers sizes 0, 1, 5, 65 and 400, so 12 clamps to 5 and the game is absent.
- **Fix:** The test uses `Create(65)` with `Layout:FewGamesThreshold` set to 100, so every top-level box faces out, which is what the plan needed.
- **Files modified:** Cabinet.IntegrationTests/TrueProportionsTests.cs
- **Commit:** a3dac2a

**2. [Rule 3 - Interface] `EnrichmentSync` does not look up an item kind**
- The plan said to take the kind from the first collection item with the game id. `SizeEstimate.Assign` takes no kind (the class is kind-independent; kind only picks the dimensions in `Dimensions`), so no lookup was needed.

**3. [Order] Task 2 tests written after the implementation**
- The size class implementation landed with Task 1 (the tracer needs it), so Task 2's tests were written against existing code and passed first time rather than failing first. They pin the drift cases literally (2.74 and 2.76, 59 and 61).

**4. [Rule 2] `GameDetails.SameAs` compares the new fields**
- Added `EstimatedSize` and `EstimateModelVersion` to the comparison so the sync's change detection sees a changed class.

## Known Stubs

None.

## Threat Flags

None. No new network endpoints, auth paths or trust boundaries; the plan's mitigations (plausibility bounds, hysteresis with model version, startup validation naming the key) are all implemented and tested.

## Notes for the owner's review

- Class dimensions and score bands are starting values from research. The size source per game (`BoxShape.Resolve(...).Source`) is available for the review sheet.
- The "bars total at most about 11 percent of the box width" overflow figure follows from the 12 percent margin (a wider box than the cover gives 0.12/1.12, about 10.7 percent); a box narrower than the cover gives 12 percent of the box height. No new test pins this figure here, as fitting was covered by the earlier fitting plan.

## Self-Check: PASSED

All created files exist and all three commits are in the log.

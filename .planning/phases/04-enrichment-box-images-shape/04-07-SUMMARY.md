---
phase: 04-enrichment-box-images-shape
plan: 07
subsystem: layout-and-cabinet-rendering
tags: [layout-engine, readability-floors, css, container-queries, accessibility]
requires:
  - phase: 04-02
    provides: "Placement art, generated-cover helper, cover CSS"
provides:
  - "One-line floors and two-line thresholds per section design"
  - "Placement.ShowBaseLine (JSON showBaseLine) decided from millimetres"
  - "Layout version 10 with re-recorded goldens"
  - "Whole-line steps on generated cover titles"
  - "Apron-shaped plinth opening"
  - "Marker accessible name that starts with its visible text"
affects: [box-art review plans, detail dialog]
tech-stack:
  added: []
  patterns:
    - "Layout decides second-line visibility from millimetres; the page reads a strict boolean"
    - "@property length registration so size containers inside a section cannot change the unit"
key-files:
  created:
    - Cabinet.UnitTests/Layout/ThinBoxTests.cs
  modified:
    - Cabinet.Domain/Layout/ReadabilityFloor.cs
    - Cabinet.Domain/Layout/SectionDesign.cs
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Domain/Layout/CabinetLayout.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/wwwroot/css/cabinet.css
    - build/tests/page-scripts.test.mjs
    - docs/cabinet-layout.md
    - Cabinet.UnitTests/Layout (SectionDesignTests, PhoneProfileTests, FamilyLayoutTests, Golden files)
key-decisions:
  - "Phone one-line floor is 37 mm but the 59 mm tap floor still governs through MinBoxThicknessMm; showBaseLine compares the drawn size against the thresholds, not the floors"
  - "A thin orphan or upright is drawn at max(real thickness, floor); a layer on desktop now bottoms out at 34 mm instead of 40"
  - "Pages treat showBaseLine as a strict === true, so a missing or non-boolean value drops the second line and sets data-lines=1"
patterns-established:
  - "Placement trailing optional values stay null (omitted from JSON) for kinds they do not apply to"
requirements-completed: [CAB-03, IMG-03]
duration: ~45min
completed: 2026-10-07
status: complete
actuals:
  tokens: 49000
  tasks: 3
  commits: 3
---

# Phase 4 Plan 07: Thin boxes at true thickness and box-look polish Summary

Thin upright expansions, orphan boxes and layers are drawn at their real thickness down to a one-line floor (34 mm desktop, 59 mm phone tap floor), the engine says per placement whether the second line fits (`showBaseLine`, layout version 10), and generated covers, the plinth arch and the "+N more" name got their folded-in polish.

## What was built

- **Task 1 (tracer), commit b29b2f2:** `ReadabilityFloor.OneLineLabelPx = 15`; `SectionDesign` gains `TwoLineUprightWidthMm` and `TwoLineOrphanHeightMm`, while `MinOrphanHeightMm`, `MinUprightExpansionWidthMm` and `MinLayerHeightMm` now hold the one-line floors (34 desktop, 37 phone). `Placement.ShowBaseLine` is set for upright expansions (`width >= TwoLineUprightWidthMm`) and orphan boxes (`height >= TwoLineOrphanHeightMm`) and absent elsewhere. `LayoutVersion` is 10. `render.js` draws the second line only for `showBaseLine === true` on those two kinds and otherwise sets `data-lines="1"`; CSS gives those boxes the spine or flat-box title style. `ThinBoxTests` covers the desktop and phone cases from the plan plus sample-wide invariants.
- **Task 2, commit 459b88b:** Floor constants and expectations in the phone, family and design tests moved to the new derivations (64, 71, 80, 89 now pinned as thresholds), `Validate` rejects a threshold below its floor, goldens re-recorded at version 10 and re-run without the update switch. `docs/cabinet-layout.md` describes the one-line floors, thresholds, `showBaseLine` and why the recorded layouts changed.
- **Task 3, commit a42d508:** `copy.moreName` now reads `+{N} more expansion(s) for {base}`. Covers are size containers with four `@container` height steps (4, 3, 2, 1 lines at 64, 50, 36 px). `--u` is registered with `@property` (`<length>`). The plinth uses the apron recipe (`--arch-rise` 40, `--arch-radius` 110, alpha top 0.92 and bottom 0.72, `--arch-lit` 0.14, rounded top, three inset shadows) and the retired shade token is gone.

## Verification

- `dotnet test --solution Cabinet.slnx --no-restore`: 1213 passed, 0 failed.
- `node --test build/tests/page-scripts.test.mjs`: 86 passed.
- `build/lint.sh`: all checks pass (repo-rules, workflows, shell, secrets, script-tests).
- Tracer gate: the tracer's verify (build, `ThinBoxTests`, page script tests) passed end to end, so expansion tasks proceeded per the owner's decision.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing page-script cases relied on the old kind-based second line**
- **Found during:** Task 1
- **Issue:** Existing orphan placements in `page-scripts.test.mjs` had no `showBaseLine`, so with the strict flag they lost their second line.
- **Fix:** Added `showBaseLine: true` to the orphan fixtures that assert the sub-line.
- **Files modified:** `build/tests/page-scripts.test.mjs`
- **Commit:** b29b2f2

**2. [Rule 3 - Blocking] Layout tests pinned the old minimums as drawn sizes**
- **Found during:** Task 2
- **Issue:** Family tests expected a 52 or 60 mm deep upright to be drawn at the old 64 mm minimum.
- **Fix:** They now expect the real thickness (52, 60) and the test for the least width was renamed to say "real width".
- **Files modified:** `Cabinet.UnitTests/Layout/FamilyLayoutTests.cs`
- **Commit:** 459b88b

**3. [Plan note] `moreName` already had a singular form**
- The plan describes the old name as `{N} more expansions for {base}`; the file already had a `1 more expansion for` branch. Only the leading plus was added.

No architectural changes. No auth gates.

## Known Stubs

None.

## Threat Flags

None. `showBaseLine` is read as a strict boolean and every title still goes through `textContent`; the CSP integration test passes.

## Notes for review rounds

- The line-step and arch behaviours are CSS-only and covered here by stylesheet text assertions; the browser rounds should confirm the label lies inside the plate at 390 px and 1440 px and that registering `--u` moved no geometry.

## Self-Check: PASSED

- Commits b29b2f2, 459b88b, a42d508 exist on the worktree branch.
- `Cabinet.UnitTests/Layout/ThinBoxTests.cs` and the re-recorded goldens exist; `layoutVersion: 10` appears once in `layout-version.txt`; `arch-shade-alpha` appears 0 times in `cabinet.css`.

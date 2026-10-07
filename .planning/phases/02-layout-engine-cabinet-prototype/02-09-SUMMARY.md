---
phase: 02-layout-engine-cabinet-prototype
plan: 09
subsystem: layout-engine
tags: [piles, lie-flat, upright-expansions, stack-order, stability, css]
status: complete
requires:
  - phase: 02-layout-engine-cabinet-prototype
    provides: expansion families, stack layout, orphan boxes, classic furniture finish (plans 01 to 05)
provides:
  - "Piles of flat boxes ordered widest first, thicker lower among equal widths, no overhang"
  - "Lie-flat fallback before a new section opens, with the Layout:LieFlatBeforeNewSection server setting"
  - "Upright expansionSpine placements beside the base game, with the room reserved when the base is placed"
  - "Expansion stacks drawn thickest at the bottom"
  - "Layout version 5"
affects: [phone section design, golden layouts, owner review rounds]
tech-stack:
  added: []
  patterns:
    - "The width helpers StandingWidthMm and UprightWidthMm are shared by the engine's split and the arrangement, so the room reserved is the room drawn"
    - "New box kinds reuse existing rules through :is() selectors; no light or shade layer and no z-index"
key-files:
  created: []
  modified:
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Domain/Layout/LayoutOptions.cs
    - Cabinet.Domain/Layout/Orientation.cs
    - Cabinet.Domain/Layout/LayoutMember.cs
    - Cabinet.Domain/Layout/CabinetLayout.cs
    - Cabinet.Domain/Layout/SectionDesign.cs
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Service/Layout/LayoutSettings.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.UnitTests/Layout/LayoutAssertions.cs
    - Cabinet.UnitTests/Layout/ShelfMixTests.cs
    - Cabinet.UnitTests/Layout/LayoutSettingsTests.cs
    - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
    - Cabinet.UnitTests/Layout/FamilyStabilityTests.cs
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
    - docs/cabinet-layout.md
key-decisions:
  - "A game lying flat as the lie-flat fallback is a new member pose only inside the placement loop; Orientation.Decide is untouched, so the chosen pose still depends on the game alone"
  - "The upright room always counts the stack column, so an upright never gives way when a thin expansion arrives later"
  - "Every family expansion is scaled to the design limits on entry with the same clamp as a plain box; the upright decision is taken on the original depth first"
requirements-completed: [CAB-01, CAB-02, CAB-04, CAB-05, EXP-01, EXP-03]
metrics:
  tasks: 3
  commits: 3
actuals:
  tokens: 25700
  tasks: 3
  commits: 3
---

# Phase 2 Plan 09: Box poses and stacking order Summary

Piles go widest first with no overhang, big boxes lie flat instead of opening a mostly empty section (switchable), thick expansions stand upright beside their base game with a line naming it, and expansion stacks go thickest at the bottom; the 65 sample now needs 2 sections instead of 3 and the 400 sample 7 instead of 9.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Big boxes lie flat in piles that go largest at the bottom | 37bf3ae |
| 2 | A thick expansion stands upright beside its base game | ed57013 |
| 3 | Expansion stacks go thickest at the bottom; final measurement and visual check | 67670e6 |

Tracer gate: auto mode is off, but the project defers human visual checks to the end-of-phase batch, so the tracer was verified automatically before expanding: tests green, the measurements below (layout version 5, fewer sections than before, setting off identical to before), and a crop of a sorted pile. Then the expansion work followed.

## Section counts and placements (desktop)

Measured from the running app with a scratch script; files `measure-*.txt` are in the review folder. Covers are counted among top-level boxes (cover, spine, flatBox and orphanExpansion placements). "Big boxes flat" are flatBox placements at least 200 mm wide and more than 40 mm tall.

| State | Sample | Sections | Empty cubbies per section (of 17) | Counts per kind | Big boxes flat | Uprights | Covers of top-level |
| ----- | ------ | -------- | --------------------------------- | --------------- | -------------- | -------- | ------------------- |
| Before (version 4) | 65 | 3 | 1, 12, 16 | spine 29, cover 15, orphan 3, flat 5, layer 10, marker 1 | 0 | 0 | 15 of 52 (28.8%) |
| Before (version 4) | 400 | 9 | 0 x6, 7, 12, 15 | spine 215, cover 76, orphan 10, flat 41, layer 45, marker 3 | 0 | 0 | 76 of 342 (22.2%) |
| After Task 1 | 65 | 2 | 1, 12 | spine 29, cover 13, orphan 3, flat 7, layer 10, marker 1 | 2 | 0 | 13 of 52 (25.0%) |
| After Task 1 | 400 | 7 | 0 x6, 6 | spine 209, cover 62, orphan 10, flat 61, layer 45, marker 3 | 20 | 0 | 62 of 342 (18.1%) |
| Task 1, setting off | 65 | 3 | 1, 12, 16 | identical to before | 0 | 0 | 15 of 52 |
| Task 1, setting off | 400 | 9 | 0 x6, 7, 12, 15 | identical to before | 0 | 0 | 76 of 342 |
| After Task 2 | 65 | 2 | 0, 12 | spine 29, cover 13, orphan 3, flat 7, layer 9, upright 2, marker 1 | 2 | 2 | 13 of 52 |
| After Task 2 | 400 | 7 | 0 x6, 7 | spine 209, cover 60, orphan 10, flat 63, layer 41, upright 9, marker 3 | 22 | 9 | 60 of 342 (17.5%) |
| Final (Task 3, only draw order changed) | 65 | 2 | 0, 12 | as after Task 2 | 2 | 2 | 13 of 52 (25.0%) |
| Final (Task 3) | 400 | 7 | 0 x6, 7 | as after Task 2 | 22 | 9 | 60 of 342 (17.5%) |

Comparison with the planning prototype: every number matches exactly (before, after Task 1, setting off identical to before, final). No difference to explain.

## What was built

- **Piles.** The grouping of flat members into piles is unchanged; each pile is then ordered from the floor up by drawn length descending, drawn thickness descending, slot order. Piles stay flush left and the pile width is the bottom box's length.
- **Lie flat before a new section.** When a plain game fits no cubby of the existing sections as chosen, and `Layout:LieFlatBeforeNewSection` is on and the few-games look is off, it is tried lying flat across the existing sections in reading order; only then does a section open. Families and orphans never use it. The setting is a fifth `LayoutOptions` parameter (default true), read and validated by `LayoutSettings` (`true` or `false` in any letter case, otherwise a message naming the key), committed in `appsettings.json`, in the options fingerprint, in the layout guide and in its env example.
- **Upright expansions.** `Orientation.UprightExpansionMinDepthMm` 50, `MaxUprightExpansions` 2, `StandsUpright`; `SectionDesign.MinUprightExpansionWidthMm` 64 on desktop; `PlacementKind.ExpansionSpine`; `LayoutMember.Uprights` and `HasFamily`. The engine splits a family's expansions in collection order into uprights and stack, with the room counted as `Limits.MaxWidthMm` minus the base's standing width minus the stack column. The arrangement places base, uprights, then the stack column (only when something lies in it).
- **Renderer and CSS.** `render.js` splits the old helper into `namesBaseInName` and `hasBaseLine`; an `expansionSpine` is named with `layerName` and gets the `expansionFor` sub-line (no new COPY string). `cabinet.css` applies the spine rules through `:is()` and adds three small rules (row-reverse, title size, vertical sub-line). The stylesheet still has exactly two z-index rules, the furniture hooks are untouched.
- **Stack order.** `PlaceStack` keeps `StackLayout.Layout` over the stacked expansions in collection order and draws the shown ones thickest first, ties in collection order; the marker stays on top. `StackLayout.cs` is unchanged (last touched by the families plan).

## Existing tests changed

- `ShelfMixTests`: the cover share test now uses top-level boxes as denominator; "flat boxes only come from small or thin games" now runs with the setting off and is joined by a setting-on case; the oversize-only test and the eleven/twelve/thirteen strategy test run with the setting off, because both assert that each game stands exactly as chosen, which the lie-flat rule deliberately changes.
- `LayoutSettingsTests`: the defaults record carries the fifth value; the fingerprint list includes the new setting.
- `FamilyLayoutTests`: the family coverage test counts uprights; the layer-order test now expects thickest first and excludes uprights; the layer-height test uses a family with two uprights so the 70 mm cap is still reached by a stacked expansion; the one-expansion test caps the arrival's depth below 50 mm; the "comes before its base" test accepts an upright; the marker-count reason now allows the two uprights; the few-games count excludes uprights.
- `FamilyStabilityTests`: restructured as the plan specifies (kept, narrowed, added, exception, broadened tests; the full-stack marker test appends a thin expansion).
- `LayoutAssertions`: adds the pile invariant, upright adjacency and width room, and the stack order invariant to every layout check.
- `LayoutEndpointTests`: the 65 sample must contain `expansionSpine` with `familyId` and `baseTitle`.

## Verification

- `dotnet test --solution Cabinet.slnx`: 307 passed, 0 failed. `bash build/lint.sh` passes (repo-rules, workflows, shell, secrets, script-tests). `node --check` passes on every script; the markup-string API grep and the Domain forbidden-API grep find nothing. No intermittent test failure was seen.
- Scratch browser check at 1440 px on both samples (script `check.mjs` in the review folder): (a) every upright holds a sub-line starting `Expansion for `, an accessible name containing `, expansion for `, is at least 29 px wide and both lines compute at least 12 px; (b) no pile overhangs (0.5 px tolerance); (c) layer heights never grow going up and the marker is on top; (d) computed `z-index` is `auto` on uprights, flat boxes, layers and orphans. PASS on all four for both samples; no console message, no page error, no failed request, no horizontal scroll.
- Review screenshots in `<scratch>/02-09-shots/`:
  - `final65-1440.png` and `final400-1440.png` (full page at 1440, scale 2), with crops `final65-1440-crop1.png` (cubby with an upright), `crop2` (cubby with the "+N more" marker and a thickest-first stack), `crop3` (a sorted pile), `crop4` (a stack of layers), and the same four for `final400-1440-crop1..4.png`; `final65-report.json` and `final400-report.json`.
  - Per-task passes: `t1-65-1440.png`, `t1-65-1440-crop1.png` (sorted pile), `t2-65-1440*.png`; `measure-before.txt`, `measure-after-piles.txt`, `measure-setting-off.txt`, `measure-after-uprights.txt`, `measure-after.txt`; `check.mjs`.
- Note on the shoot helper reports: `$HOME/.claude/skills/senior-frontend/scripts/shoot.mjs` reports one 404 console error and an aborted request for the phone layout in every pass, including the pass before any expansion work. The phone profile answers 404 until the phone section exists, and the helper's full-page capture briefly narrows the viewport so the page asks for it. The separate `check.mjs` pass, which takes no screenshot, saw no console message and no failed request at 1440.

## Critic notes (taste calls for the owner)

- A short upright (box 150 to 200 mm tall) cuts both lines hard: "Quayra ..." with "Expansio..." in `final65-1440-crop2.png`. The full text is in the tooltip and accessible name. A taller minimum or a one-line variant for short ones are options.
- The 64 mm upright minimum makes a 50 to 63 mm deep expansion look a little thicker than it is (same trade as orphan boxes).
- Piles of several big boxes lying flat appear in the 400 sample (`final400-1440-crop3.png`); the widest box at the bottom reads naturally, but how many big boxes lie flat per cubby (22 in total) is a taste call.
- The realised cover share drops from 28.8 to 25.0 percent (65 sample) and from 22.2 to 17.5 percent (400 sample) of top-level boxes. Both are still inside the 15 to 35 percent band. The setting trades this against 1 to 2 more mostly empty sections.
- Counting the stack column's room beside every upright gives fewer uprights beside wide covers (2 in the 65 sample, 9 in the 400 sample), as planned.

## Deviations from Plan

None - plan executed exactly as written. One small addition: `docs/cabinet-layout.md` also notes that a stack shows the earliest arrivals and that a new arrival in a full stack only raises the marker number.

## Issues Encountered

- The requirements EXP-01 wording ("thin sideways spine") is left for the owner, as the plan flagged; no requirements file was edited here.
- `STATE.md` and `ROADMAP.md` were not touched (orchestrator-owned).

## Known Stubs

None.

## Threat Flags

None beyond the plan's register. T-02-26: at most two uprights, never wider than `Limits.MaxWidthMm`, one pass over the family. T-02-27: at most one extra first-fit scan per game that fits nowhere as chosen. T-02-28: the setting accepts only `true` or `false` (settings tests) and is in the fingerprint. T-02-29: upright text goes through `COPY` and `textContent` only (grep clean). T-02-SC: Playwright 1.63.0 was reused from a scratch directory outside the repository; no scratch path appears in `git status`.

## Self-Check: PASSED

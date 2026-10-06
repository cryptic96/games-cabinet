---
phase: 02-layout-engine-cabinet-prototype
plan: 05
subsystem: layout-engine
tags: [expansions, families, stack-layout, orphans, stability, css]
status: complete
requires:
  - phase: 02-layout-engine-cabinet-prototype
    provides: engine, cubby arrangement, generated covers, classic furniture finish, layout cache (plans 01 to 04)
provides:
  - "StackLayout: pure, monotone choice of visible expansion layers and the hidden count"
  - "Family-aware engine: parent resolution (lowest-id owned base), lookahead column reservation, orphan detection"
  - "Fixed-width stack column beside a base game with layers from the floor up and a +N more chip"
  - "Orphan expansion boxes with a second line naming the base game, and orphan covers in the few-games look"
  - "Entry clamp and BoxLimits derived from the anchor cubby"
  - "Samples with families, a two-parent expansion and orphans; NextExpansion and NextOrphanExpansion"
  - "Stability tests for appends and the documented first-expansion exception"
affects: [phone section design, real collection data, owner review rounds]
tech-stack:
  added: []
  patterns:
    - "A base game carries its whole family into first-fit, so arrival order never strands an expansion"
    - "Column width is fixed per design, so a growing stack only changes layer count and never pushes neighbours"
    - "New box kinds reuse the flat-box rules through :is() selectors; no own light or shade layer and no z-index"
key-files:
  created:
    - Cabinet.Domain/Layout/StackLayout.cs
    - Cabinet.UnitTests/Layout/StackLayoutTests.cs
    - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
    - Cabinet.UnitTests/Layout/FamilyStabilityTests.cs
  modified:
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/LayoutMember.cs
    - Cabinet.Domain/Layout/SectionDesign.cs
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Domain/Samples/SyntheticCollections.cs
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.UnitTests/Layout/LayoutAssertions.cs
    - Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs
    - Cabinet.UnitTests/Layout/ShelfMixTests.cs
    - Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
key-decisions:
  - "LayoutVersion raised from 3 to 4 in the first commit of this plan (families change the layout JSON and placement); the orphan and test commits keep 4"
  - "Orphans in the few-games look face out as covers carrying their base title; otherwise they always lie flat as orphan boxes whatever the cover share says"
  - "Orientation is decided from the original box and only then clamped, so the clamp never changes how a game stands"
  - "A flat base game that gets a family stands as a spine instead (the pose is the only thing that changes)"
  - "Popularity-weighted family choice in the 400 sample (weight 1 + 8 x size squared) so a few games gather big families like a real collection; a plain skew gave families of one or two"
requirements-completed: [EXP-01, EXP-02, EXP-03, CAB-05]
metrics:
  tasks: 3
  commits: 3
actuals:
  tokens: 47000
  tasks: 3
  commits: 3
---

# Phase 2 Plan 05: Expansion families Summary

Expansions now stand as thin sideways layers in a fixed-width column right of their base game (collapsing into a "+N more" chip when the shelf is full), orphan expansions lie as their own two-line boxes, and the column is reserved when the base is placed so arrival order never strands an expansion.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Expansions lie in a stack beside their base game, with "+N more" when it is full, end to end | d2365b5 |
| 2 | Expansions without an owned base appear on their own, labelled with the game they expand | eec2db5 |
| 3 | Adding games and expansions keeps the cabinet still, except for the documented first-expansion case | 5061cb0 |

Tracer gate: auto mode is off, but the project defers human visual checks to the end-of-phase batch (same as the previous slice), so the tracer was verified end to end automatically before any expansion work: the tracer's tests green, `GET /cabinet/layout?sample=65&profile=desktop` carrying `expansionLayer` and `moreMarker` with `moreCount` 3, and a browser pass (marker text `+3 more`, accessible name `3 more expansions for ...`, no console output, no horizontal scroll). Then the orphan work followed.

## What was built

- **Stack layout.** `StackLayout.Layout` returns the largest visible count not above `min(count, max)` whose layers plus the marker (only when something is left out) fit the cubby height. Integer only; once the marker shows, more expansions only raise the hidden count (tested for three layer heights and across a seeded append).
- **Engine.** Parents are owned `ItemKind.Base` items; an expansion names several owned bases joins the lowest BggId; an expansion naming an expansion, itself or an unowned game is an orphan (so no recursion and no cycles). Families keep (CollectionId, BggId) order. The few-games count uses only top-level units (bases and orphans). When the same base is owned twice, its first entry takes the family. A base carries its whole current family into first-fit.
- **Entry clamp.** `BoxLimits` (from the tallest, then widest, cubby: 460 x 400 on desktop, 270 for a family base so the 190 column still fits, depth 150) scales a front down with integer maths, compares `W*maxH` with `H*maxW` to pick the limiting side, and floors every side at 10 mm.
- **Arrangement.** A family slot is the base slot plus a 190 mm column, fit checked with the column but with no dependence on the expansion count. Layers are clamped to 40 to 70 mm by depth; the first layer sits on the floor, layers touch, the marker (40 mm, empty label, `moreCount`) sits on top. Orphan boxes join the flat columns, are `max(depth, 80)` tall and carry `baseTitle`; orphan covers carry it too.
- **Renderer and CSS.** `render.js` composes every expansion string through `COPY` (`layerName`, `moreLabel`, `moreName`, `expansionFor`) and writes it with `textContent`; markers skip the palette custom properties. Layers reuse the flat-box rules via `:is()`; the marker is the accent chip (`--wood-light` on `--ink`, 600, 12px); orphans stack title and a 400-weight 12px sub-line, one line each with ellipsis, title first when space is short (`safe center`). No `z-index` was added and the three decorative furniture elements and `data-board` / `data-row-end` hooks are untouched.
- **Samples.** `Random(seed, count, expansionPercent = 0)` (zero draws no extra values, so older samples are unchanged), `NextExpansion`, `NextOrphanExpansion`. The 65 sample is 49 bases and 16 expansions: families of 9, 2 and 1, one expansion for two owned games, three orphans (one naming a 58-plus character base title) and one expansion ordered before its base. The 400 sample has about 72 expansions in families of up to 10 and about 10 orphans.

## Section counts (desktop)

| Sample | Before | After |
| ------ | ------ | ----- |
| 65 | 3 sections; empty cubbies 0, 11, 16 | 3 sections; empty cubbies 1, 12, 16 |
| 400 | 10 sections | 9 sections; empty cubbies 0 x6, 7, 12, 15 |

The samples were regenerated, so this is not like for like. The earlier observation still holds: with face-out covers the 65 sample spills into a mostly empty section 2 and section 3 (one game) because the leftover games are big or tall and do not fit the 260/300/330 mm rows. Family columns make that slightly worse for tall bases; no tuning was attempted. It remains a section-design matter for the owner review loop.

## Verification

- `dotnet test --solution Cabinet.slnx`: 272 passed, 0 failed (unit tests in the Layout category: 216 including the 200-seed loops, about 15 s). `bash build/lint.sh` passes (repo-rules, workflows, shell, secrets, script-tests). `node --check` passes on every script; the markup-string API grep finds nothing.
- Browser (scratch Playwright, app on a private port, `default-src 'self'` injected, 1440 px): no console output, no failed request, no horizontal scroll; every orphan has a `.placement-sub` starting `Expansion for ` and a rendered height of 39.3 px (at least 36); layers 19.6 px tall at the least with 12 px labels; the marker is 12 px; `z-index` is `auto` on layers, markers and orphans.
- Review screenshots (outside the repository): `<scratch>/02-05-shots/` holds `final65-1440-full.png`, `final65-1440-crop-stack.png`, `final65-1440-crop-marker.png`, `final65-1440-crop-orphan.png` (plus `crop-orphan2` and `crop-orphan3`), the same set for the 400 sample as `final400-*`, the two JSON reports, and the earlier per-task passes in `t1` and `t2`.

## Critic notes (taste calls for the owner)

- Narrow orphan boxes (box height 150 to 300 mm, about 70 to 140 px wide at 1440) cut the sub-line to "Expansion fo…"; the full text is in the tooltip and accessible name. A wider minimum orphan width or a two-line sub-label are options.
- Layer label size varies between 12 and 14 px by layer height, as the flat-box rule specifies; thin layers read slightly smaller than thick ones.
- Layer, marker and orphan text sits in the shade like every box; marker contrast was already covered by the existing palette test.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Coverage check moved earlier**
- **Found during:** Task 1
- **Issue:** the "every item stands exactly once" assertion counted all placements by game id, which layers and markers break; Task 1 could not go green with it.
- **Fix:** the extended `AssertValid` (top-level, layers and marker counts, same-cubby check) was written in Task 1 instead of Task 3. Task 3 added the stability tests and sample tests.
- **Files modified:** `Cabinet.UnitTests/Layout/LayoutAssertions.cs`
- **Commit:** d2365b5

**2. [Rule 1 - Bug] Earlier tests that assumed the old composition**
- **Issue:** the entry clamp made "a box taller than the cubby is rejected" untrue, so that test now uses a game too deep for every cubby (the safety net still fires) and two new tests pin the scaling; the flat-stack test now counts orphan boxes in the stacks they share; the layout endpoint test counts items as placements plus each marker's hidden count and now asserts the three new kinds.
- **Files modified:** `CabinetLayoutEngineTests.cs`, `ShelfMixTests.cs`, `LayoutEndpointTests.cs`
- **Commits:** d2365b5, eec2db5, 5061cb0. The integration test fix landed in the last commit, so the Task 1 commit alone leaves one integration test failing (65 placements expected, 63 found); the unit tests of that commit are green.

**3. Task 2 code split.** To keep per-task commits meaningful, the orphan handling in the engine, arrangement and `LayoutMember` (`BaseTitle`, orphan pose rules) was held back from the Task 1 commit and added in the Task 2 commit.

Total deviations: 2 auto-fixed, 1 note. Documentation check: `docs/cabinet-layout.md` already states the first-expansion exception ("The first expansion for a game that had none reserves its stack beside the base game. That family may move, and later cubbies may shift") and matches the implemented behaviour; it was not edited.

## Issues Encountered

- A first draw of the 400 sample used a mild skew and gave families of one or two (no marker); replaced by popularity-weighted choice, which gives families up to 10 and three markers.
- The phone profile still answers 404, as planned; no phone section was added.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register. T-02-17: `StackLayout` is linear in the family size and capped by the setting, and the column width is fixed. T-02-18: all expansion text goes through `COPY` and `textContent` (grep clean). T-02-19: only base games can be parents, so cycles and self-references become orphans (covered by a test).

## Self-Check: PASSED

---
phase: 02-layout-engine-cabinet-prototype
plan: 01
subsystem: layout-engine
tags: [layout, cabinet, determinism, razor, es-modules, css-container-units, synthetic-data]
requires: []
provides:
  - "Pure first-fit CabinetLayoutEngine over CabinetItem and SectionDesign (LayoutVersion 1)"
  - "GET /cabinet/layout?sample=&profile= with allowlisted samples and profiles"
  - "Vanilla ES module renderer (cabinet.js, render.js, copy.js) and cabinet.css"
  - "Seeded synthetic collections including the edge sample"
  - "Layout invariant and append-stability test suite (trait Category=Layout)"
affects: [shelf-mix, families, box-look, phone-design, real-collection-feed]
tech-stack:
  added: []
  patterns:
    - "Layout is a pure function of the collection; no state between runs"
    - "Cubby arrangement depends only on the member set, so an append rearranges one cubby"
    - "Geometry reaches the DOM only via style.setProperty; text only via textContent"
    - "Container query units defined on a child (--u) scale one layout to any width"
key-files:
  created:
    - Cabinet.Domain/Layout/CabinetItem.cs
    - Cabinet.Domain/Layout/CabinetLayout.cs
    - Cabinet.Domain/Layout/LayoutJson.cs
    - Cabinet.Domain/Layout/SectionDesign.cs
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Domain/Layout/SplitMix64.cs
    - Cabinet.Domain/Samples/SyntheticCollections.cs
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
    - Cabinet.IntegrationTests/CabinetPageTests.cs
    - Cabinet.UnitTests/Layout/LayoutAssertions.cs
    - Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs
    - Cabinet.UnitTests/Layout/SplitMix64Tests.cs
    - Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs
  modified:
    - Cabinet.Service/Program.cs
    - Cabinet.Service/Pages/Index.cshtml
key-decisions:
  - "Desktop design numbers kept as specified (interior 1200 x 1730, frame 20); cubby count derived from the design, not hard-coded"
  - "Edge sample titles written with unicode escapes so source files stay ASCII"
  - "Unit assertions that use pattern matching are factored into helper methods because FluentAssertions Contain takes expression trees"
requirements-completed: [CAB-01, CAB-04, CAB-05]
duration: n/a
completed: 2026-10-05
status: complete
actuals:
  tasks: 2
  commits: 3
---

# Phase 2 Plan 1: Layout engine tracer and determinism contract Summary

A pure C# first-fit layout engine turns an invented collection into a wooden cabinet of irregular cubbies, served as JSON and drawn by vanilla ES modules, with the determinism contract (ordering, purity, single-cubby appends) pinned by tests.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Invented 65-item collection drawn as spines in wooden cubbies, end to end | 773bd3f |
| 2 | Same collection gives the same cabinet; appending a game changes at most one cubby | 88d5fb4 |

Task 1 gate: the owner viewed the running tracer in a browser and approved it, noting it looks plain for now. That is expected, because palette, covers and detailing arrive in later work.

## What was built

- Domain: input and output models, section design data (`SectionDesigns.Desktop`), `CubbyArrangement` (members stand upright, packed left, bottom aligned, equal widths fit), `CabinetLayoutEngine.Build` (order by collection id then game id, first cubby in reading order that still fits, new section only when none can, `InvalidOperationException` when an item fits no empty section), `LayoutJson`, `SplitMix64`.
- Samples: `0`, `1`, `5`, `12`, `65`, `400` generated from fixed seeds with invented syllable titles, plus `edge`: 14 games with a very long title, colon and dash subtitles, a single 35-letter word, katakana, an emoji, Hebrew letters, a combining diaeresis and a blank title.
- Service: `/cabinet/layout` returns 404 outside the sample and profile allowlists; `Index.cshtml` mounts `#cabinet` with an external module script and stylesheet only.
- Front end: `render.js` builds DOM with `createElement`, `style.setProperty` and `textContent`; `copy.js` holds every visitor-facing string.
- Tests: 21 integration tests (endpoint allowlists, fingerprinted assets, no inline style or script bodies, 65 placements) and 58 layout unit tests: validity for all seven samples, empty and one-game cabinets, monotonic section counts, byte-identical JSON under 10 seeded shuffles, tie ordering, duplicate rejection, exact-fit placement, rejection of an item that fits nothing, first-game append, and 200 seeded appends of 120 games each (at most one cubby changes; a new section leaves existing sections untouched). SplitMix64 outputs were pinned after an independent Python computation (seed 0: `E220A8397B1DCDAF`, `6E789E6AA1B965F4`, `06C45D188009454F`).

## Deviations from Plan

**1. [Rule 1 - Bug] Plan said 18 cubbies; the design has 17**
- **Found during:** Task 1
- **Issue:** The plan states the desktop design has 18 cubbies, but its own rows (3+4+3+4+3) give 17, as does the research table. The plan's integration-test and browser-check wording ("18 cubbies", "multiple of 18") therefore could not hold.
- **Fix:** Design numbers kept exactly as specified (interior height 1730). Tests derive the count from `SectionDesigns.Desktop.Cubbies.Count`; nothing hard-codes 18.
- **Files modified:** `Cabinet.IntegrationTests/LayoutEndpointTests.cs`, `Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs`
- **Commit:** 773bd3f, 88d5fb4

**2. [Rule 2 - Missing critical] Extra integration tests beyond the plan** (Task 1): a test that the page has no inline style attributes or inline script bodies, a check that 65 placements are emitted, and more 404 cases, to enforce the markup constraints stated in the plan.

**3. Test-first note (Task 2):** the engine already existed from Task 1, so the new tests passed on first run once the `edge` sample existed; they act as characterisation tests for the contract rather than a red-then-green cycle. No engine defect was exposed, so the engine was not changed in Task 2.

## Verification

- `dotnet build Cabinet.slnx`: 0 warnings, 0 errors.
- `dotnet test --solution Cabinet.slnx`: 101 passed, 0 failed (21 integration, 80 unit including 58 under the Layout trait).
- 200-seed append-stability test runs in about 2 seconds.
- `grep -c 'Trait("Category", "Layout")'` in the engine tests: 11.
- Negative greps (markup-string APIs in scripts; runtime random, hash, clock, floating types in Domain layout and samples) find nothing.
- `bash build/lint/checks/10-repo-rules.sh` passes (run directly, not through the container wrapper).
- Task 1 headless-browser check at 1440x900: 65 placements, 34 cubbies (two sections of 17), no console errors, no horizontal scroll.

## Known Stubs

None. Spines only, with no palette or covers yet, is the planned scope for this slice, not a stub.

## Threat Flags

None beyond the plan's register. The layout endpoint is allowlisted (404 otherwise), sends no CORS headers, and the health mapping is untouched.

## Issues Encountered

None remaining.

## Self-Check: PASSED

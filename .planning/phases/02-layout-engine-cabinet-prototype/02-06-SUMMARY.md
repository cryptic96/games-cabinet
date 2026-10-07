---
phase: 02-layout-engine-cabinet-prototype
plan: 06
subsystem: layout-engine
tags: [phone, readability-floors, section-validation, oversize-boxes, golden-layouts]
status: complete
requires:
  - phase: 02-layout-engine-cabinet-prototype
    provides: expansion families, box poses, classic furniture finish, layout delivery (plans 01 to 05 and 09)
provides:
  - "Phone section design (14 cubbies across 640 mm) served for profile=phone"
  - "ReadabilityFloor integer helper and floors derived from the real gutter, frame and furniture side space"
  - "SectionDesign.Validate, run before every build and over every shipped design"
  - "Oversize boxes in the 65 and 400 samples"
  - "Recorded layouts, 400-game digests and a version-bump guard with opt-in re-recording"
  - "Layout version 6"
affects: [owner review rounds, tuning of section designs]
tech-stack:
  added: []
  patterns:
    - "Floors are applied inside the shared width and height helpers of the arrangement, so room reserved equals room drawn"
    - "Derived values are written through ReadabilityFloor.Millimetres in the design data, never typed as results"
key-files:
  created:
    - Cabinet.Domain/Layout/ReadabilityFloor.cs
    - Cabinet.UnitTests/Layout/PhoneProfileTests.cs
    - Cabinet.UnitTests/Layout/SectionDesignTests.cs
    - Cabinet.UnitTests/Layout/LayoutGoldenTests.cs
    - Cabinet.UnitTests/Layout/Golden (13 files)
  modified:
    - Cabinet.Domain/Layout/SectionDesign.cs
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Domain/Samples/SyntheticCollections.cs
    - Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs
    - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
    - docs/cabinet-layout.md
key-decisions:
  - "Validate gained two rules beyond the plan (the deepest spine plus the stack column fits the anchor cubby; the thickest flat box fits the anchor cubby) so the engine's 'fits no empty section' guard is unreachable with a valid design"
  - "MinBoxThicknessMm defaults to 1 and SmallestRenderedWidthPx to 304 on the record; shipped designs set both explicitly"
  - "Layout version raised from 5 to 6 because the desktop 34 mm box floor and the oversize sample boxes change desktop output"
requirements-completed: [CAB-07, CAB-01, CAB-04, CAB-05, CAB-06]
metrics:
  tasks: 3
  commits: 3
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 2 Plan 06: Phone section, readability floors and recorded layouts Summary

Phones now get their own narrow 14-cubby section design with floors derived from the real 304 px section width plus furniture side space (spine and layer floor 59 mm, orphan height 89 mm, upright width 71 mm), every design validates itself before the engine uses it, oversize boxes are scaled into the limits, and recorded layouts with a version-bump guard lock the arrangement.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Phone-width screen shows a narrow, taller cabinet with readable, tappable boxes | 0b942ec |
| 2 | Any box size lands in the cabinet and every design checks itself | 1c9cc71 |
| 3 | Every arrangement change is a reviewed diff with a raised layout version | d3d26c3 |

Tracer gate: auto mode is off, but the project defers human visual checks to the end-of-phase batch, so the tracer was verified automatically before expanding: tests green and the browser check below passed at 320 and 390 px for both big samples, then Tasks 2 and 3 followed.

## Derived values

| Value | Input | Result |
| ----- | ----- | ------ |
| Phone rendered width | 640 + 2x20 + 2x32 | 744 mm |
| Desktop rendered width | 1200 + 2x20 + 2x32 | 1304 mm |
| Phone `MinBoxThicknessMm` | (24, 304, 744) | 59 |
| Phone `MinOrphanHeightMm` | (36, 304, 744) | 89 |
| Phone `MinUprightExpansionWidthMm` | (29, 304, 744) | 71 |
| Desktop `MinOrphanHeightMm` | (36, 592, 1304) | 80 |
| Desktop `MinUprightExpansionWidthMm` | (29, 592, 1304) | 64 (equals the value typed in earlier) |
| Desktop `MinBoxThicknessMm` | starting value | 34 |

Phone design: 14 cubbies (rows of 2, 3, 2, 3, 2, 2), interior height 2060 mm, anchor cubby 420 x 400 so limits are 420 wide, 400 tall, 230 for a family base, depth 150. Sections needed: 65 sample 2 desktop / 3 phone, 400 sample 7 desktop / 12 phone.

## What was built

- `ReadabilityFloor.Millimetres` with integer ceiling division (argument checks), plus `TapTargetPx` 24, `TwoLineLabelPx` 36 and `TwoLineSpinePx` 29.
- `SectionDesign`: `FurnitureSideMm` (32, documented as equal to the stylesheet side allowance), `OuterWidthMm`, `RenderedWidthMm`, `SmallestRenderedWidthPx`, `MinBoxThicknessMm`, and `Validate()` which lists every problem in one `InvalidOperationException` (rows naming their position, anchor narrower than its height, family base room, box floor above layer height, floors above the shortest cubby, plus the two extra rules).
- `CubbyArrangement`: floors applied in `StandingWidthMm` (now takes the design), `UprightWidthMm`, flat-box height (before the pile sort), layer heights and marker height; orphan height is the largest of depth, orphan minimum and box floor.
- Engine calls `design.Validate()` first; the entry clamp already behaved as specified (verified by the new tests).
- Samples: two oversize base games in 65 and six in 400 (fronts such as 430 x 420, 520 x 380, 700 x 410), counts and composition unchanged.
- Golden tests with `CABINET_UPDATE_GOLDENS=1` re-recording that refuses while the layout changed and the version did not; the failure message names the file, the first differing line and the command. Changing the desktop stack column from 190 to 180 was tried: three tests failed with the raise-the-version message and the switch refused to record; reverted (`git diff --quiet Cabinet.Domain` clean).
- Docs: "Section designs and readability floors" and "Recorded layouts" sections.

## Verification

- `dotnet test --solution Cabinet.slnx`: 368 passed, 0 failed. `bash build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests pass. The Domain forbidden-API grep finds nothing; `ReadabilityFloor.Millimetres` appears 5 times in `SectionDesigns.cs`; the CI workflow never mentions the switch; the golden directory holds exactly 13 files.
- Golden eyeball check: `desktop-65.json` and `phone-65.json` both hold all seven placement kinds (cover, spine, flatBox, orphanExpansion, expansionLayer, expansionSpine, moreMarker); desktop has 3 big boxes lying flat, phone 4.
- Browser check (scratch Playwright 1.63.0, app on 127.0.0.1:6330, script `phone-check.mjs`), 65 and 400 samples at 320 and 390 px: requests only `profile=phone`; one section per row; no horizontal scroll (document width equals viewport); every `button.placement` at least 24 x 24 px; every label and sub-line at least 12 px; nothing painted outside a section box horizontally or vertically (top and plinth unclipped); no console message and no failed request. `contrast-check.mjs`: worst label contrast under the shade is 5.02:1 at both widths.
- The 404 for `profile=phone` that the senior-frontend `shoot.mjs` reported in earlier passes is gone: the 65 sample at 1440 and 390 shows no console error, page error or failed request.
- Intermittent test: one run of the whole solution saw `Cabinet.IntegrationTests.CabinetPageTests.Page_links_a_fingerprinted_cabinet_stylesheet_that_is_served` fail with a `SocketException` stack (a connection error, raised while the unit-test project ran at the same time). It passed in five isolated reruns and in every later full run. Output was not kept beyond the exception type.

## Review screenshots (Critic pass at phone width)

In `<scratch>/02-06-shots/`: `final65-390.png` (full page, scale 2), `final400-390.png` (full page), `final65-320.png` (full page, scale 2), `final65-1440.png` (full page), crops of phone cubbies `final65-390-crop-upright.png` (cubby with an upright expansion and a stack with "+4 more"), `final65-390-crop-stack.png` (same cubby), `final65-390-crop-pile.png` (a pile with an orphan on top of it), `final65-390-crop-orphan.png`, plus `phone-check.mjs`, `contrast-check.mjs`, `shots.mjs`, and `skill/skill65-*.png` with the helper report.

Critic notes (taste calls for the owner):
- The 59 mm spine floor makes phone spines clearly wider than the real boxes (a 25 mm box reads 59 mm); the 65 sample's second section has several nearly empty cubbies. Both are the accepted readability trade.
- An upright expansion shows "Expansio..." on its sub-line at phone width; the full text is in the accessible name.
- The phone scroll length at 400 games is 12 sections; the plan flagged this as acceptable until the owner says otherwise.

## Deviations from Plan

**1. [Rule 3 - Blocking] Existing test designs made valid.** Validation before every build made twelve existing tests fail on hand-built designs (interior wider than the row, cubbies narrower than their height). Designs were corrected in `FamilyLayoutTests` and `CabinetLayoutEngineTests` with the same intent. The test "a game that fits no empty section is rejected instead of looping" was removed because the added validation rules make that situation unreachable with a valid design; rejection of designs that would allow it is covered in `SectionDesignTests`.

**2. [Rule 2 - Missing critical] Two extra validation rules** (deepest spine plus stack column fits the anchor cubby; thickest flat box fits the anchor cubby), as described under key decisions.

**3. Layout version raised to 6.** The plan said to raise nothing, but the later note says to bump when desktop output changes, and it did (34 mm desktop floor, oversize samples). Goldens were recorded once, at version 6.

**4. Task order.** `Validate()` and the engine call landed in the first commit with the phone design, because the engine calls it before placement and the first run already needed valid test designs; Task 2 added the tests and the sample changes.

## Issues Encountered

None beyond the intermittent test above. `STATE.md` and `ROADMAP.md` were not touched (orchestrator-owned).

## Known Stubs

None.

## Threat Flags

None beyond the plan's register. T-02-20: the entry clamp and the unreachable guard are covered by `SectionDesignTests`. T-02-21: `Validate()` runs before every build and over every shipped design. T-02-22: recorded layouts plus the version guard (verified by the temporary stack-column change).

## Self-Check: PASSED

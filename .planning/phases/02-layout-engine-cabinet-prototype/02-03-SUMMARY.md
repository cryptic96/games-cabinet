---
phase: 02-layout-engine-cabinet-prototype
plan: 03
subsystem: ui
tags: [palette, generated-covers, labels, furniture-finish, css, contrast]
status: complete
requires:
  - phase: 02-layout-engine-cabinet-prototype
    provides: layout engine, covers and flat stacks, review page and layout cache (plans 01, 02 and 04)
provides:
  - "Twelve placeholder tones with fixed text colours, picked per game from its own id, carried in the layout JSON"
  - "Generated covers: palette background, six hash-chosen patterns, solid title plate; permanent fallback for missing box art"
  - "Spine and flat-box label shortening in text elements with the full title kept for the accessible name and tooltip"
  - "Classic furniture finish in CSS over finish-neutral hooks (three aria-hidden elements per section, board and row-end attributes on cubbies)"
  - "Shade cap constants and contrast tests for every tone and the marker chip under the furniture's shade"
affects: [phone section design, families and expansion stacks, real box art, owner review rounds]
tech-stack:
  added: []
  patterns:
    - "Colours and geometry reach the page only through custom properties set from the renderer; the stylesheet owns every look"
    - "Furniture is painted by a handful of container elements and their pseudo-elements, never per box, at most six background layers per surface"
    - "Grain, tone and figure variation come from stable indices, never from a random source"
key-files:
  created:
    - Cabinet.Domain/Layout/SpinePalette.cs
    - Cabinet.Domain/Layout/SpineLabel.cs
    - Cabinet.UnitTests/Layout/SpinePaletteTests.cs
    - Cabinet.UnitTests/Layout/SpineLabelTests.cs
  modified:
    - Cabinet.Domain/Layout/CabinetLayout.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Domain/Layout/SectionDesign.cs
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/css/cabinet.css
key-decisions:
  - "Layout version bumped from 2 to 3 in the commit that added the palette to the JSON (and covers the label shortening that followed in the same plan), so a cached ETag can never serve the old shape; no test pinned the number, the endpoint test reads the constant"
  - "The shade cap stays at 20 percent: every tone and the marker chip pass 4.5:1 under it, and the 30 percent case fails for the Rust pair, which proves the check can fail"
  - "The arched opening of the plinth is a rounded pseudo-element with an inset shade rather than a radial gradient, because a gradient cannot give both a soft edge and a tight curve on a very flat ellipse"
  - "The cover title plate is centred and grows beyond 40 percent of the cover height when a title needs more lines, so small covers never cut a line in half"
requirements-completed: [CAB-01, CAB-02, CAB-04]
metrics:
  tasks: 3
  commits: 3
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 2 Plan 03: Box look and classic furniture finish Summary

Boxes now carry per-game palette tones, generated covers and shortened labels, and the cabinet is drawn as the owner's chosen classic piece of wooden furniture in pure CSS, with every label measured at 4.5:1 or better under the furniture's shade.

## Tasks

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Boxes take their tones and generated cover faces from their own id | 6fea66f |
| 2 | Spine and flat-box labels shorten readably without losing the full title | 87854a5 |
| 3 | The cabinet reads as a classic piece of wooden furniture | d8504a4 |

Tracer gate: auto mode is off and the project verifies human checks at the end of the phase, so the tracer was verified end to end automatically (unit and integration tests, the layout JSON carrying a 12-entry palette, and a browser pass) and the owner's visual review is left to the end-of-phase batch.

## What was built

- **Palette and covers.** `SpinePalette.Tones` holds the twelve contract tones in order; `ToneFor` and `PatternFor` use the stable hash with the existing tone and pattern salts, so a game's look depends only on its own id (tested across two collections that share games). The layout JSON carries the table; the renderer applies the chosen entry as `--bg` and `--fg`. Covers get `data-pattern` (stripes, chevrons, dots, rings, diagonal, plain), a pattern layer at 14 percent of the text colour with a tile of 24 mm, and a solid title plate holding a label clamped to four lines. Spines and flat boxes get end rules, every box the light inset edge and contact shadow, and hover lifts 6 mm over 120 ms (transform only, off under reduced motion).
- **Labels.** `SpineLabel.Shorten` trims, cuts at the earlier of a colon-space or spaced dash when something precedes it, then at a budget counted in text elements with an ellipsis; the budget never drops below 3. Spines use their height and flat boxes their width, divided by `SectionDesign.LabelCharPitchMm` (14 on desktop). Covers keep the full title as label; `Title` is never altered. Labels use `dir="auto"` plus `unicode-bidi: plaintext`.
- **Furniture.** `SpinePalette.ShadeColour` (`#140a04`) and `MaxShadePercent` (20) are tested with an independent contrast helper. The renderer adds `div.section-top`, `div.section-trim` and `div.section-base` (aria-hidden, empty) per section, `data-board` (section index times 2 plus the shelf rank, modulo 6) and `data-row-end` on cubbies. The stylesheet reserves the full section height (moulding, plinth, floor margin) in the aspect ratio and draws: vertical-grain uprights with a lit arris; side boards that read 32 mm thick, left lit, right darker with a wall shadow; shelf boards with continuous horizontal grain, a 1 px lit front edge, a darker underside and six stable tones with their own figure offsets; planked back with grooves, lips, alternate planks, cast shadow and side-wall shade in every cubby, empty ones included; a moulded top with four bands, a cast shadow and a rail; a darker plinth with two feet, an arched opening and a soft floor shadow with contact lines under the feet. Grain is three stripe periods (15, 23, 37 mm), a flat-sawn figure layer and a tone sweep; inside sections narrower than 400 px the finest stripe is dropped and the alpha halved. z-index appears exactly twice (shade 1, focused box 2).

## Verification

- `dotnet test --solution Cabinet.slnx`: 219 passed, 0 failed (unit 175, integration 44). `bash build/lint.sh repo-rules` passes. `node --check` passes on every script. The greps for markup-string APIs, compositing hints, 3D transforms, `url(` and filters find nothing; `git log` for the Task 3 commit shows none of the engine, JSON or design files touched.
- Browser check (scratch Playwright 1.63.0, chromium headless shell, Development, `default-src 'self'` added to the document response, phone layout request rewritten to the desktop design because the phone design does not exist yet):
  - (a) no console error or warning, no page error, no failed request and no horizontal scroll at 1440 and 390 px on the 65, 400, 0 and edge samples.
  - (b) first section height equals width times (top + section height + plinth + floor margin) over (section width + 2 times the side allowance): deviations of 0.004 px at 1440 and 0.014 px at 390.
  - (c) the top and the plinth rectangles lie inside the section rectangle with at least 1.7 px to spare on each side at 390 and 2.9 px at 1440; the crops show the overhang, the feet and the arch, and the floor shadow ending softly.
  - (d) per-label contrast with the shade measured at each label's top edge: lowest 5.00 (1440) and 4.95 (390) on the 65 sample, 4.94 and 4.89 on the 400 sample. The lowest tone is Rust, the next Teal; all twelve tones are printed per run and none is below 4.5.
  - (e) after Tab to the first box its computed z-index is 2 and the crop shows the full 2 px ring over the shelf, side board and shade.
  - (f) at 390 px the grain is soft with no moiré; the finest stripe is dropped there.
  - Every in-cabinet label has a computed size of at least 12 px.
- Review screenshots live outside the repository; nothing from them is committed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Cover titles on small covers lost lines**
- **Found during:** Task 3 visual check
- **Issue:** The plate was a fixed 40 percent of the cover height, so on the smallest covers four lines of twelve pixel text did not fit and a line was cut through its middle.
- **Fix:** The plate is centred with a minimum height of 40 percent and grows to fit up to four lines; the clamp and plate overflow stay as backstops.
- **Files modified:** `Cabinet.Service/wwwroot/css/cabinet.css`
- **Commit:** 87854a5 (padding and label height) and d8504a4 (centred growing plate)

**2. [Rule 1 - Defect] Plinth opening looked like a smear instead of an arch**
- **Found during:** Task 3 visual check
- **Issue:** An elliptical radial gradient over a 1300 mm wide, 30 mm tall opening fades over tens of millimetres at its ends, so the opening read as a blurred ellipse, not an arch between two feet.
- **Fix:** The opening is a rounded pseudo-element with an inset shade and a faint lit lip, drawn by the plinth.
- **Files modified:** `Cabinet.Service/wwwroot/css/cabinet.css`
- **Commit:** d8504a4

**3. Plan note on the version bump.** The plan text for the finish says the layout version stays as the previous slice ended; the slice itself bumped it once (2 to 3) with the palette. No later commit in this plan changes the version or the layout shape.

Total deviations: 2 auto-fixed, 1 note. No scope changes. The plan files list for Task 1 also included the engine and arrangement files, which were edited as planned.

## Issues Encountered

- A fullpage screenshot in the scratch script made the page re-run its load (the viewport change fires the profile query), so the scratch script waits for the rendered cabinet after each fullpage shot and measures crops in a tall viewport. Nothing in the repository was affected.
- No intermittent test failure was seen in this plan (the suite was green on every run).

## Known Stubs

None. Palette tones are placeholders by design: art-derived colours will replace entries through the same two custom properties, and they must be validated as hex before reaching the page when that work arrives.

## Threat Flags

None beyond the plan's register. Labels and titles reach the DOM only through `textContent` and attribute setters, the full title stays in `aria-label` and `title`, and the screenshots hold invented data and stay outside the repository.

## Self-Check: PASSED

- Created files exist: `SpinePalette.cs`, `SpineLabel.cs`, `SpinePaletteTests.cs`, `SpineLabelTests.cs`.
- Commits `6fea66f`, `87854a5` and `d8504a4` exist on the branch.

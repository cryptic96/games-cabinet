---
phase: 04-enrichment-box-images-shape
plan: 03
subsystem: domain
tags: [colour, oklab, contrast, art-detection, chooser, tdd]
requires: []
provides:
  - "RgbColour, SpineColour (PairFor, IsValidPair, PassesUnderShade, ContrastRatio, MeetsMinimum, LightnessOf) in Cabinet.Domain.Layout"
  - "ArtFeatures, ArtThresholds (Default, Fingerprint), ArtVerdict, ArtVerdicts (Classify, Score), ArtPick, ArtChooser.Choose in Cabinet.Domain.Collection"
affects: [04-04, 04-05, 04-06]
tech-stack:
  added: []
  patterns: ["pure Domain decision functions over plain values", "OKLab lightness nudge with gamut-fit chroma shrink", "verdict derived at read time from stored features"]
key-files:
  created:
    - Cabinet.Domain/Layout/RgbColour.cs
    - Cabinet.Domain/Layout/Oklab.cs
    - Cabinet.Domain/Layout/SpineColour.cs
    - Cabinet.Domain/Collection/ArtChoice.cs
    - Cabinet.UnitTests/Layout/SpineColourTests.cs
    - Cabinet.UnitTests/Collection/ArtChoiceTests.cs
  modified: []
key-decisions:
  - "SpineColour.MeetsMinimum(double) is public so the inclusive 4.5:1 rule can be tested (no real colour lands on exactly 4.5)"
  - "Oklab gamut check allows 0.0005 on encoded channels so pure black and pure white are reachable and the search always terminates with a result"
requirements-completed: [CAB-03, IMG-01]
status: complete
duration: ~35 min
completed: 2026-10-07
actuals:
  tokens: 9600
  tasks: 3
  commits: 3
---

# Phase 4 Plan 03: Spine colour pair and art choice Summary

Pure Domain functions that turn an art colour into a legible spine pair (OKLab lightness nudge, white or black text at 4.5:1 under the 20 percent furniture shade) and choose between the owned edition's picture and the main picture from stored detector features.

## What was built

- `SpineColour.PairFor` keeps the extracted colour whenever white (preferred) or black text passes at both 0 and 20 percent shade. Otherwise it moves OKLab lightness in 0.005 steps, lighter before darker at equal step, holding hue and chroma and shrinking chroma in 3 percent steps only to fit sRGB. The 200-step bound reaches pure black and white, so a result is guaranteed.
- `SpineColour.IsValidPair` accepts only lowercase `#rrggbb`, white or black text and a pair passing the two-end check, so a damaged stored pair falls back to the palette tone.
- `ArtVerdicts.Classify` (degenerate, then flat, then 3D, else unsure, all inclusive), `ArtVerdicts.Score`, `ArtThresholds.Default` (0.97, 0.15, 0.93, 0.40, 0.97) with an invariant-culture `Fingerprint`, and `ArtChooser.Choose` implementing the 16-row table.

## Verification

- Grid of 17 levels per channel (4,913 colours): every pair passes at both shade ends; at least 70 percent keep their own background; largest lightness change is at most 0.07; hue stays within 5 degrees for nudged colours with chroma above 0.05.
- `dotnet test --solution Cabinet.slnx --no-restore`: 1069 passed, 0 failed. `dotnet build Cabinet.slnx` clean with warnings as errors. `build/lint.sh repo-rules` passes.
- Tracer gate: the tracer commit was verified and approved by the owner before Tasks 2 and 3.

## Deviations from Plan

### Owner-approved

**1. [Score bound for two 3D fixture rows] Score floor 0.6 instead of 0.8**
- **Found during:** Task 3 (writing the score tests)
- **Issue:** The plan says `Score` is at least 0.8 for the 3D fixture rows. With the specified formula `clamp(0.5 * (1 - fill) / 0.25 + 0.5 * corner2 / 0.7, 0, 1)`, the near-black and transparent-PNG rows (fill 0.88, corner2 0.58) score 0.5 * 0.12 / 0.25 + 0.5 * 0.58 / 0.7 = 0.24 + 0.414, about 0.65. The white row (0.78, 0.72) scores about 0.95 and the grey row (0.80, 0.69) about 0.89.
- **Fix:** The formula is unchanged, as the owner decided. The white and grey rows assert at least 0.8; the near-black and transparent-PNG rows assert at least 0.6.
- **Files modified:** Cabinet.UnitTests/Collection/ArtChoiceTests.cs
- **Commit:** 8e13dc9

### Interface additions

**2. [Rule 3 - Blocking] Public `SpineColour.MeetsMinimum(double ratio)`**
- **Issue:** The Domain project has no `InternalsVisibleTo`, and no real colour gives exactly 4.5:1, so the "exactly 4.5 passes" rule could not be tested through `PassesUnderShade`.
- **Fix:** Extracted the inclusive comparison into a small public method used by `PassesUnderShade`; the owner accepted it as an interface addition.
- **Commit:** d799913

### Test coverage notes

- The "lighter wins at equal step" ordering is guaranteed by the loop order (direction +1 before -1) and covered by the purity and determinism test. No test constructs a tie directly, because the internal search is not exposed.
- The "both white and black pass" case has no instances under the shade on the grid; the tests assert that white is chosen whenever white passes, and that black is chosen when only black passes (a light grey).

## Known Stubs

None.

## Threat Flags

None. T-04-14 (damaged stored pair) is mitigated by `IsValidPair`; T-04-15 (nudge loop) by the bounded 200-step search with a guaranteed in-gamut end point, exercised over the whole grid.

## Self-Check: PASSED

- Created files exist: RgbColour.cs, Oklab.cs, SpineColour.cs, ArtChoice.cs, SpineColourTests.cs, ArtChoiceTests.cs.
- Commits d799913, bb732a7, 8e13dc9 exist on the worktree branch.

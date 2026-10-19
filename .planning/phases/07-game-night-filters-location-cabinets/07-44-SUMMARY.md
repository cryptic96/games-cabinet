---
phase: 07-game-night-filters-location-cabinets
plan: 44
subsystem: ui
tags: [filter-dock, build-review, screenshots, critic, focus-ring, empty-state, browser-tests]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The finished rail and panel rework (07-35 to 07-43): dock, push and fly-over, rail state, phone Filters button, geometry, contrast, rings, access"
provides:
  - "07-REVIEW-ROUND-2.md (FINAL): M1 to M15, L1, R1 to R7, P1, T1, C and N items, Critic findings D1 to D3 and K1 to K4, 'Look at these first' (14 frames) and 'Your questions' with recommended answers"
  - "ui-refs/build-round-2/: 156 synthetic frames with frames.tsv (loopback url, synthetic source, viewed), eight of them re-shot after the fixes"
  - "07-BUILD-CRITIC-2.md (orchestrator's Critic pass, committed in Task 2)"
  - "Three Critic fixes: rail Clear zero-match fill, filling note centred over the empty cabinet, view segment ring kept whole beside the chosen segment"
affects: [07-45, filters]

tech-stack:
  added: []
  patterns:
    - "A highlight that a more specific dock rule clears is restated at the dock rule's specificity, and the browser test reads the painted colour with ToHaveCSSAsync, not only the marker attribute"
    - "A focus ring that crosses a filled neighbour gets a wall-coloured box-shadow band under the ring and 2px beyond it, on a control already lifted with z-index"

key-files:
  created:
    - .planning/phases/07-game-night-filters-location-cabinets/07-44-SUMMARY.md
  modified:
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.BrowserTests/FilterDockRailTests.cs
    - Cabinet.BrowserTests/FilterDockMemoryTests.cs
    - .planning/phases/07-game-night-filters-location-cabinets/07-REVIEW-ROUND-2.md
    - .planning/phases/07-game-night-filters-location-cabinets/ui-refs/build-round-2/ (8 frames re-shot)

key-decisions:
  - "D2 is not a regression: v0.9.0 rendered the filling note at centre 344px against the cabinet's 720px at 1440, identical to the reviewed build; fixed anyway in site.css, which owns the note's rule and the page column outside the dock"
  - "D3 uses a 6px box-shadow spread rather than the Critic's literal 4px: 4px only reaches the ring's outer edge, 6px gives the 2px wall-coloured gap outside the ring the Critic described"
  - "K1 to K4 are not implemented; they are labelled taste calls in the document with the Critic's recommendation (K1 = L1, recommended A: keep the push from 1440)"
  - "The version question is left for the release, not asked in the review document"

patterns-established:
  - "Re-shoot only the frames a fix changes, through the same screenshot script with a file-name filter, so the manifest rows keep the script-written url and source and the viewed column is cleared until each frame is looked at"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: about 4.5 hours over three tasks and the orchestrator's checkpoint
completed: 2026-10-18
actuals:
  tokens: 21000
  tasks: 3
  commits: 6
---

# Phase 7 Plan 44: Build round 2 - the rail and panel on synthetic data, measured, Critic-checked Summary

**The whole rework ran green at one commit, 156 synthetic frames cover every review-matrix item, the push was measured (longest task 72 ms), and after the orchestrator's senior-frontend Critic pass three defects were fixed with tests and re-shot: the rail's Clear now paints its inverted fill at zero matches, the filling note stands centred over the empty cabinet, and the focused view segment keeps its whole ring beside the chosen segment. The review document is final, with labelled questions and recommended answers.**

## Performance

- **Duration:** about 4.5 hours (Task 1 about 3.5 hours, the Critic checkpoint, Task 3 about 1 hour)
- **Tasks:** 3 of 3 (Task 2 is the orchestrator's checkpoint)
- **Files modified:** 4 product and test files, the review document, 8 re-shot frames

## Reviewed and final commits

- **Reviewed commit:** `3de48e4aec9297d858b125d1faee5785b221f988` (the full suite ran green on it; 148 of the 156 frames are of it).
- **Final code commit:** `d869435` (after the three fixes; the eight re-shot frames show the fixed stylesheets).

## Accomplishments

- **Task 1 (tracer, `2f97c81`):** full suite green at the reviewed commit (unit 2943, integration 541, node 763, browser 779 in normal fonts and 779 under DejaVu with the WebKit trait excluded, lint all five checks, owner routes). 156 frames for M1 to M15 on samples 65 and 400 and the local synthetic set, all loopback, all viewed; P1 longest task 72 ms (no finding); the draft document with M, L1, R, P1, T1, C and N items.
- **Task 2 (orchestrator, `92b3cff`):** the orchestrator's own senior-frontend Critic pass over all 156 frames: three defects (D1 to D3) and four taste calls (K1 to K4) in `07-BUILD-CRITIC-2.md`.
- **Task 3:** D1 to D3 fixed within START and ROUND values with tests, eight frames re-shot and viewed, the document made FINAL with Critic findings, "Look at these first" and "Your questions".

## Critic defects

| Defect | Fix | Test | Commit | Re-shot |
|---|---|---|---|---|
| D1, rail Clear not filled at zero matches (AM-4, FIXED rule; the dock rule `html[data-filter-dock] .filter-rail-clear` out-specified `.filter-rail-clear[data-highlighted]`) | `html[data-filter-dock] .filter-rail-clear[data-highlighted]` restates the inverted fill, border and colour in the dock block of `filters.css` | `FilterDockRailTests.Zero_matches_and_Clear_on_the_rail` now reads the computed background (`ToHaveCSSAsync`) against `--room-text`; failed before the fix, passes after | `31fabd6` | `M2-rail-zero-brown-day-1440-en`, `M2-rail-zero-birch-lit-1440-en` |
| D2, filling note off-centre (centre 344 px against the cabinet's 720 px at 1440). **Not a regression:** v0.9.0, run in a throwaway worktree on a never-synced host, measured the same 344 against 720 | `.cabinet-filling { margin-inline: auto; }` in `site.css` | New theory `FilterDockMemoryTests.The_filling_note_stands_centred_over_the_empty_cabinet` (1440 and 390, within 3 px of the section's centre); failed at 1440 before the fix (344 vs 720), passes after. `A_page_without_a_cabinet_has_no_dock` now measures the collapsed column on `main` instead of the note | `f517e55` | the four `M15-filling-*` |
| D3, view segment ring lost where it crosses the chosen segment | `box-shadow: 0 0 0 6px var(--room, var(--wood-dark))` on `.filter-view-segment:has(input:focus-visible)` in `filters.css` | `FilterFocusRingTests`, `FilterAccessTests` and `LookChromeContrastTests` rerun green | `d869435` | `M10-focus-view-segment-brown-day-1440-en-crop`, `M10-focus-view-segment-birch-day-1440-en-crop` |

## Task Commits

1. **Task 1: the whole rework runs green; frames, measurement, copy table** - `2f97c81` (docs)
2. **Task 2: orchestrator's senior-frontend Critic pass** - `92b3cff` (docs, orchestrator)
3. **Task 3: Critic defects fixed, document final** - `31fabd6` (fix, D1), `f517e55` (fix, D2), `d869435` (fix, D3), plus this SUMMARY and the final document (docs)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] D2's owning file is `site.css`, not one of the plan's two stylesheets**
- **Found during:** Task 3, D2
- **Issue:** the plan lists `filters.css` and `cabinet.css`; the filling note's rule (`.cabinet-filling`) and the page column outside the dock (`main`) live in `site.css`. A page that has never synced has no dock, so the dock grid in `filters.css` does not apply.
- **Fix:** one declaration (`margin-inline: auto`) and a doc comment on the existing rule in `site.css`.
- **Commit:** `f517e55`

**2. [Approved test-file additions] `FilterDockRailTests.cs` and `FilterDockMemoryTests.cs`**
- The orchestrator approved adding the rail test (D1, computed background at zero matches) and the class that owns the empty and filling states (D2) to this task's files. `FilterDockMemoryTests` is the only browser class that drives the never-synced page. Its existing empty-page theory measured the note's left edge as the proof that the dock column collapsed; with the note now centred that proof moved to `main`'s left edge, which states the same claim directly.

**3. [Rule 1 - Bug] D3 spread is 6px, not the Critic's literal 4px**
- The Critic asked for a 2px wall-coloured gap outside the ring and wrote `box-shadow: 0 0 0 4px`. With a 2px offset and a 2px ring, a 4px spread ends at the ring's outer edge and gives no gap outside it; 6px does. Verified in the re-shot crops (the ring is whole on all four sides on Brown and Birch).

**Total deviations:** 3, all within the Critic's intent and the orchestrator's approvals. No FIXED rule changed; no taste call was implemented.

## Verification Runs

All browser runs with `DOTNET_USE_POLLING_FILE_WATCHER=1`, one suite at a time, WebKit trait excluded, after `dotnet build`; DejaVu = `FONTCONFIG_FILE` pointing at the scratch DejaVu fontconfig. All on the final code commit `d869435` with the fixes in place.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| Red check before the fixes: `Zero_matches_and_Clear_on_the_rail`, `The_filling_note_stands_centred_over_the_empty_cabinet` | 2 failed as expected (390 case passed: the note already spans the phone column) | n/a |
| `FilterDockRailTests` (7), `FilterDockMemoryTests` (13), `FilterFocusRingTests` (2), `FilterAccessTests` (26), `DefaultLookIdentityTests` (5) right after the fixes | n/a | all pass |
| `node --test build/tests/*.test.mjs` | 763 / 763 pass | n/a |
| `Cabinet.UnitTests` (whole project) | 2943 / 2943 pass | n/a |
| `Cabinet.UnitTests` `LookChromeContrastTests` | 71 / 71 pass | n/a |
| Browser `Filter*` | 198 / 198 pass | 198 / 198 pass |
| Browser `Look*` | 191 / 191 pass | 191 / 191 pass |
| Browser `DefaultLookIdentityTests` | 5 / 5 pass | 5 / 5 pass |
| `bash build/lint.sh` (repo rules, workflows, shell, secrets, script tests) | all five PASS | n/a |
| Plan Task 3 `<verify>` (node, contrast, repo rules, document and manifest checks) | exit 0 | n/a |

Task 1's gate numbers at the reviewed commit are in `07-REVIEW-ROUND-2.md` ("Automated results"). The integration suite and the whole browser suite were not rerun after the fixes (the orchestrator's post-merge gate runs the whole browser suite alone).

## Frames for the owner (14, all checked to exist)

1. `M1-rail-rest-brown-day-1440-en.png`
2. `M1-rail-rest-birch-day-1440-en.png`
3. `M2-rail-filtering-brown-day-1440-en-crop.png`
4. `M2-rail-zero-brown-day-1440-en.png` (re-shot, D1)
5. `M3-push-rest-brown-day-1440-en.png`
6. `M3-push-filtering-birch-day-1440-en.png`
7. `M4-flyover-filtering-brown-day-1280-en.png`
8. `M4-flyover-shadow-edge-brown-day-1280-en-crop.png`
9. `M5-push-s400-brown-day-1440-en.png`
10. `M5-label-smallest-spine-push-s400-brown-day-1440-en-crop.png`
11. `M5-label-smallest-cover-push-s400-brown-day-1440-en-crop.png`
12. `M10-focus-view-segment-birch-day-1440-en-crop.png` (re-shot, D3)
13. `M13-phone-filtering-brown-day-390-en.png`
14. `M15-filling-brown-day-1440-en.png` (re-shot, D2)

All under `.planning/phases/07-game-night-filters-location-cabinets/ui-refs/build-round-2/`. Nothing was sent to the owner by this executor.

## Known Stubs

None.

## Threat Flags

None. T-07-44-01: every re-shot frame shows only the sample collections or the never-synced host on 127.0.0.1 and was looked at before `viewed` was written; the v0.9.0 comparison screenshot stayed in the scratchpad. T-07-44-02: contrast, ring, access, geometry, structure and identity tests reran after the fixes. T-07-44-03: K1 to K4 and L1 go to the owner as labelled questions; only defects were fixed.

## Self-Check: PASSED

- FOUND: `Cabinet.Service/wwwroot/css/filters.css`, `Cabinet.Service/wwwroot/css/site.css`, `Cabinet.BrowserTests/FilterDockRailTests.cs`, `Cabinet.BrowserTests/FilterDockMemoryTests.cs`, `07-REVIEW-ROUND-2.md`, `07-BUILD-CRITIC-2.md`, the 14 owner frames
- FOUND: commits `2f97c81`, `92b3cff`, `31fabd6`, `f517e55`, `d869435`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)

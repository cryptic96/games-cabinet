---
phase: 07-game-night-filters-location-cabinets
plan: 30
subsystem: ui
tags: [review-round, screenshots, critic, search-hint, owner-pages, playwright]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "the whole filter, location-cabinet and owner-page build of plans 07-01 to 07-29"
provides:
  - "164 synthetic frames of the review matrix with a manifest (frames.tsv), each shot on loopback and looked at"
  - "07-REVIEW-ROUND-1.md, FINAL: matrix items M1 to M12, tuning values R-1 to R-8, the phone speed check, every new text in both languages, Critic findings D1 to D4 and K1 to K6, and the owner's questions by label"
  - "07-BUILD-CRITIC.md: the orchestrator's senior-frontend Critic pass"
  - "Phone search hint fix, owner-page chip and baseline fixes"
affects: [07-31, 07-32, 07-34, filters, owner-pages]

key-files:
  created:
    - .planning/phases/07-game-night-filters-location-cabinets/07-REVIEW-ROUND-1.md
    - .planning/phases/07-game-night-filters-location-cabinets/07-BUILD-CRITIC.md
    - .planning/phases/07-game-night-filters-location-cabinets/ui-refs/build-round/
  modified:
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/img/icons.svg
    - Cabinet.BrowserTests/FilterSlimBarTests.cs

key-decisions:
  - "The found-box outline colour (D1) was not changed: the earlier contracts fix it, so it goes to the owner as a defect awaiting a yes with the exact change"
  - "The narrow phone search field shows a magnifier instead of a clipped hint, only while a filter is in use"

patterns-established:
  - "A screenshot manifest check (loopback address, synthetic source, viewed attestation, no duplicates, count equals files) gates every frame before it is staged"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: about 2 sessions
completed: 2026-10-17
actuals:
  tokens: 14000
  tasks: 3
  commits: 8
---

# Phase 7 Plan 30: The build round, Critic-checked, with the owner's questions Summary

**164 synthetic frames of every reviewed state, a phone-speed measurement, a side-by-side text table and a Critic-checked review document with four defects (three fixed, one awaiting the owner's yes) and six taste calls, all reduced to 16 questions answerable by label.**

## Accomplishments

- **Task 1 (`e331a6d`, reviewed commit `f235f7a`):** 163 frames covering the review matrix at 1440 and 390 px in English and Dutch on several walls, including lit evenings, forced colours and reduced motion, all on synthetic data (samples 65 and 400, the edge sample and a local synthetic set with invented places). `frames.tsv` lists each frame with its loopback address, synthetic source and the `viewed` attestation. The phone speed check (400 sample, 4x slowed processor): median frame 16.7 ms filtering and not filtering (ratio 1.00); the first keystroke and first chip tap are the heavy frames, left for the device round. The draft review document was written.
- **Task 2 (`780320b`):** the orchestrator's senior-frontend Critic pass over all 163 frames: defects D1 to D4, taste calls K1 to K6, "Frames viewed: 163 of 163".
- **Task 3:** fixes for D2, D3 and D4 within the contract, the M2 close crop, 18 re-shot frames (all viewed again), and the document finished as FINAL with "Critic findings" and "Your questions".

## Task Commits

1. **Task 1: frames, manifest, speed check, draft document** - `e331a6d` (docs)
2. **Task 2: Critic pass** - `780320b` (docs)
3. **Task 3: fixes and the final document**
   - D2 phone search hint - `ed5239a` (fix), `8d5435b` (fix: hidden by opacity so forced colours cannot bring it back)
   - D3 and D4 owner pages - `3152cbc` (fix)
   - M2 close crop and 18 re-shot frames - `fb82b40` (docs)
   - Final document and this summary - the docs commit that follows

(`06a2b34` is the pause note written between sessions.)

## Critic defects

| Label | Outcome |
|---|---|
| D1 found and reduced-motion open box outlined in the wall's text colour, invisible on Walnut | Not changed: the colour is FIXED by an earlier contract (accent list "exhaustive, FIXED" names the wall text colour; the location contract's Show on shelf reuses `2px solid var(--room-text)`; an older contract's "same ring" wording contradicts what shipped). Listed in the document as awaiting the owner's yes, with the exact change (`outline: 3px solid var(--wood-light); outline-offset: 2px` and the existing ink halo) and the contrast test extension. Evidence frames: `M7-show-on-shelf-outline-found-walnut-lit-1440-en.png` against `M7-show-on-shelf-found-brown-day-1440-en.png`. |
| D2 phone search hint cut mid-word | Fixed (`ed5239a`, `8d5435b`): in the narrow compact filtering state the hint steps aside and a self-hosted magnifier marks the field; the label stays; new `FilterSlimBarTests` case at 320 to 430 px in both languages and in forced colours |
| D3 "Same as ..." chip wrapped in its border | Fixed (`3152cbc`) |
| D4 copy label above the title baseline | Fixed (`3152cbc`) |

Taste calls K1 to K6 are in the document with the Critic's recommendations (K1 keep A, K2 B, K3 A, K4 "8+ players", K5 A, K6 leave); K4 differs from the draft's N5 and N5 now points to it, K6 agrees with N11.

## Deviations from Plan

### Approved file-list extensions

**1. [Rule 3 - Blocking] Files outside the plan's list were touched for the Critic's defects**
- **Found during:** Task 3
- **Issue:** D3 and D4 live in `owner.css`; D2 needed the magnifier in `icons.svg`, a hook in `Index.cshtml` and a browser test.
- **Fix:** extended the file list with the orchestrator's approval: `owner.css` (D3, D4), `Index.cshtml` and `icons.svg` (D2), `FilterSlimBarTests.cs` (D2 test); `filters.css` as planned.
- **Commits:** `ed5239a`, `8d5435b`, `3152cbc`

### Left for the owner

**2. D1 was not fixed** because the colour is FIXED in earlier contracts; it needs the owner's yes. `cabinet.css` is unchanged.

**Total deviations:** 1 approved file-list extension, 1 item deliberately left for the owner. **Impact on plan:** the document carries the choice; nothing is hidden.

## Verification

Gate at `06a2b34` (after the fixes):

- Build with warnings as errors: 0 warnings.
- Unit tests, integration tests, page script (node) tests: green; the full `build/lint.sh` (repo rules, workflows, shell, secrets history, script tests): green.
- Browser suite: 729 passed, 0 failed (DejaVu fontconfig as CI draws it, WebKit trait excluded), on a re-run after one host stall that timed out 16 unrelated tests at the same moment.
- Task 3 verify block, run at the final commit with polling file watcher: `node --test build/tests/*.test.mjs` 705 passed; `LookChromeContrastTests` and `DimmedBoxContrastTests` 63 passed; `10-repo-rules.sh` passed; Critic file checks passed (`## Defects`, `## Taste calls`, `Frames viewed:`); manifest checks passed (164 rows, 164 PNGs, no duplicates, every file present, every address loopback, every source synthetic, every frame `viewed`).

## Frames to send with the document (all under `ui-refs/build-round/`)

M2-players4-time60-family-brown-day-1440-en-crop, M5-slim-filtering-brown-day-390-en, M5-slim-filtering-brown-day-390-nl, M5-sheet-open-walnut-lit-390-en, M7-show-on-shelf-outline-found-walnut-lit-1440-en (D1), M7-show-on-shelf-found-brown-day-1440-en, M6-by-location-brown-day-1440-en (K1), M8-cross-fade-mid-brown-day-1440-en (K2), M10-locations-twice-owned-paper-day-390-en, M10-pictures-twice-owned-paper-day-1440-en, M3-veil-06-s65-birch-day-1440-en (K3), M1-bar-stuck-birch-day-1440-en.

## Known Stubs

None.

## Threat Flags

None. Every frame was shot on loopback against synthetic data, looked at before staging, and the repository rules and secrets scan pass (T-07-30-01); the contrast, geometry and structure tests passed after each change (T-07-30-02).

## Self-Check: PASSED

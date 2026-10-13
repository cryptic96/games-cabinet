---
phase: 06-owner-tools-persistence
plan: 28
subsystem: owner-tools-review
tags: [review-round, documentation, css, owner-port, playwright]
requires:
  - phase: 06-27
    provides: plate, strip and contrast checks at the owner address
  - phase: 06-02
    provides: owner data storage and backup the guides describe
provides:
  - the whole phase green locally before the owner saw anything
  - a labelled review round on synthetic data (document plus 49 frames), the owner's answer recorded verbatim, and the chosen changes applied and shown again
  - README and guides that describe the owner tools, the backup by download and restore, and where locations come from
affects: [06-30]
tech-stack:
  added: []
  patterns:
    - the danger colour is reserved for destructive buttons and the warning line above them, enforced by a stylesheet test
    - a banner text for the narrowest phones lives next to the compact one in the same copy helpers, switched by a media query
key-files:
  created:
    - .planning/phases/06-owner-tools-persistence/06-REVIEW-ROUND-1.md
    - .planning/phases/06-owner-tools-persistence/06-REVIEW-ROUND-1-FE.md
    - .planning/phases/06-owner-tools-persistence/ui-refs/round-1/
    - .planning/todos/pending/2026-10-13-check-link-while-link-worker-is-busy.md
  modified:
    - README.md
    - docs/development.md
    - docs/deploy.md
    - docs/bgg-sync.md
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/js/owner-settings.js
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Pages/Shared/_OwnerBanner.cshtml
    - build/tests/owner-css.test.mjs
    - build/tests/owner-format.test.mjs
    - Cabinet.BrowserTests/OwnerCabinetTests.cs
    - Cabinet.BrowserTests/OwnerPictureTilesTests.cs
    - Cabinet.BrowserTests/OwnerPlacingTests.cs
    - Cabinet.BrowserTests/OwnerRestoreTests.cs
    - Cabinet.BrowserTests/OwnerSettingsTests.cs
    - Cabinet.BrowserTests/OwnerWorthALookTests.cs
    - Cabinet.IntegrationTests/OwnerBackupTests.cs
key-decisions:
  - "The owner approved the round as shown and asked for the senior front-end pass first; of its findings FE1 to FE10 were all applied, with L11 shortened, L12 tightened, L13 as the two-line dock, T1 the dog-ear kept, W1 kept"
  - "FE8 changes the copy contract slightly (the line under the tiles is hidden while a row shows a note); recorded as an owner-approved change"
  - "FE10 was already half true (the header grid centres its cells), so the fix aligns the title to the toggle's row below 40rem rather than adding the centring rule"
  - "The banner text for the narrowest phones is its own helper (tinyBanner, OwnerStrings.CountNotDownloaded) instead of a truncation tweak"
patterns-established:
  - "A frame looked at after a change is part of the change: two defects (an empty banner button at 320 and a word broken in half at 320) were found only by looking at the re-shot frames"
status: complete
actuals:
  tokens: 60000
  tasks: 4
  commits: 12
---

# Phase 6 Plan 28: Review round 1 and the owner tools guides Summary

The owner tools passed a local review round on synthetic data, the owner approved it after a senior front-end pass, and every change that pass raised (FE1 to FE10, plus L11 shorten, L12 tighten and the L13 two-line dock) is applied, tested and shown again in `after-` frames.

## What was built

- Task 1: the whole phase green locally (unit 2389, integration 442, browser 314, node 339, lint, deploy and route script tests), `06-REVIEW-ROUND-1.md` with the fourteen review-matrix items, T1, W1 and P1 to P3, and 49 synthetic frames in `ui-refs/round-1/`. The Critic pass found and fixed two defects first: a placed game's place was drawn as the dashed not-placed chip (`6013c52`, F1) and the Settings file button was a small grey browser button (F2).
- Task 2: `README.md`, `docs/development.md`, `docs/deploy.md` and `docs/bgg-sync.md` describe the owner tools, running the owner port locally against the fake BGG, the backup by download and restore including a fresh container, and that locations now come from the owner pages (`d405e9f`).
- Task 3: the owner's decision checkpoint; the owner's answer is recorded verbatim in the review document with the per-label resolution (`50a87dd`).
- Task 4: the front-end pass was applied.
  - FE1 (`b2c9cea`): the danger colour is used only by `.owner-btn-danger` and `.warning-line`; link errors, the invalid link field, refused-save notices and backup and restore notices use the paper-label treatment; link errors are 16px. A stylesheet test enforces it.
  - FE2 to FE10, L11, L13 (`20bf776`): tile captions stay beside their radios; the phone strip is 156px (was about 270); the narrowest-phone banner says `23 not downloaded` and its full-bleed button is square; the done dock is a message line then `Undo` and `Dismiss` side by side (89px, was about 108); focus-only headings show no ring; the card ends on a blank ruled line under Edit; the line under the tiles is hidden while a row shows a note; the empty link tile has one dashed frame; the phone title shares the toggle's line.
  - `e5466a4`: two defects found by looking at the re-shot frames (see Deviations).
  - Re-shot frames and the "After your answer" section of the review document (`ea48073`); the slow-link `Check link` quirk is a todo (`e7b2906`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Banner button label hidden at 320px**
- **Found during:** Task 4, looking at the re-shot banner frame
- **Issue:** the new rule that swaps the compact text for the narrowest-phone text also hid the button's own short label, leaving an empty dark block
- **Fix:** the rule is scoped to the banner text; the browser test now asserts the button label is visible
- **Files modified:** `Cabinet.Service/wwwroot/css/owner.css`, `Cabinet.BrowserTests/OwnerSettingsTests.cs`
- **Commit:** `e5466a4`

**2. [Rule 1 - Bug] `Automatic` broke mid-word at 320px**
- **Found during:** Task 4, looking at the re-shot 320 row
- **Issue:** with captions no longer wrapping under the radio (FE2), the label had too little room and broke inside the word
- **Fix:** below 22.5rem the tile padding is 6px and the caption gap 4px; the browser test asserts the label stays on one line
- **Files modified:** `Cabinet.Service/wwwroot/css/owner.css`, `Cabinet.BrowserTests/OwnerPictureTilesTests.cs`
- **Commit:** `e5466a4`

### Files outside the plan's list

The plan listed `owner.css`, `OwnerStrings.cs`, `owner-format.js`, `copy.js` and `owner-format.test.mjs`. The front-end fixes also touched `owner-settings.js` (banner text lookup for the third text), `_OwnerBanner.cshtml` (the narrowest-phone span), `owner-css.test.mjs` (stylesheet tests) and the browser and integration tests named above. `copy.js` was not needed: W1 was kept as built and the new banner copy belongs to the owner pages, whose strings live in `OwnerStrings.cs` and `owner-format.js`.

## Known Stubs

None.

## Threat Flags

None. The re-shot frames are synthetic (fake BGG, invented titles, `Cabinet A`, `Shelf B`, `Lent out`); the only addresses visible are the loopback stand-in for a picture host and the build id.

## Verification

Run after the last commit: `dotnet build Cabinet.slnx` (0 warnings), unit 2389 passed, integration 442 passed, browser 316 passed, node 346 passed, `10-repo-rules.sh`, `40-secrets.sh` (including the working-tree scan) and `50-script-tests.sh` clean. Every process started for the re-shoot (fake BGG, picture-host stand-in, the cabinet) was stopped; the rig lived in the scratchpad and is not committed.

## Self-Check: PASSED

Commits `50a87dd`, `e7b2906`, `b2c9cea`, `20bf776`, `e5466a4` and `ea48073` exist; the after- frames named in the review document exist in `ui-refs/round-1/`; `STATE.md` and `ROADMAP.md` are untouched.

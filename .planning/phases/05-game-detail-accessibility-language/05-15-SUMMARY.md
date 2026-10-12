---
phase: 05-game-detail-accessibility-language
plan: 15
subsystem: cabinet-ui
tags: [review, docs, css, browser-tests]
requires: ["05-03", "05-13", "05-14"]
provides:
  - "first owner review round with synthetic screenshots and the English and Dutch string table"
  - "README and guides describing the card, the keyboard model, the games list and both languages"
  - "swap rows that keep one-line titles on their first ruling on a phone, with a browser test over every text line"
  - "card without the punched hole"
affects: [card.css, card-view.js, site.css, README.md, docs]
tech-stack:
  added: []
  patterns: ["text-line boxes compared against the 28 px ruling grid in a browser test", "button rows as a grid packed to the start"]
key-files:
  created:
    - .planning/phases/05-game-detail-accessibility-language/05-REVIEW-ROUND-1.md
    - .planning/phases/05-game-detail-accessibility-language/05-LANGUAGE-TABLE.md
    - .planning/todos/pending/2026-10-11-uneven-desktop-sections-layout-pass.md
  modified:
    - Cabinet.Service/wwwroot/css/card.css
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.Service/wwwroot/js/card-view.js
    - Cabinet.BrowserTests/CardLookTests.cs
    - Cabinet.BrowserTests/CardContentTests.cs
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
    - build/tests/card-css.test.mjs
    - README.md
    - docs/cabinet-layout.md
    - docs/development.md
decisions:
  - "The Dutch site name stays Spellenkast"
  - "The punched hole is dropped from the card"
  - "An expansion with partly owned bases lists only the owned base, as before"
  - "Uneven desktop sections are accepted for this release and logged as a todo"
  - "A swap row is a grid packed to the start so a button's vertical centring cannot push a one-line title off its ruling"
metrics:
  duration: "two sessions with the owner's review between them"
  completed: 2026-10-11
status: complete
actuals:
  tokens: 60000
  tasks: 4
  commits: 9
---

# Phase 5 Plan 15: Review round, docs and the owner's choices Summary

The full local suite ran green before the owner saw anything, the owner reviewed a synthetic round of the card, keyboard model and both languages, and every answer is now applied: the punched hole is gone, one-line expansion row titles sit on their ruling on a phone, and a layout todo records the uneven desktop sections.

## What was done

1. **Tracer, full suite and review round** (`25c3216`, helper fix `6477f4a`). Build, the non-browser .NET suite, every node test, the browser suite and the repository lint were green on reviewed commit `6477f4a`. A review round of synthetic screenshots (invented samples and the fake BGG with invented locations) and a side-by-side English and Dutch string table were written to the phase folder.
2. **Docs** (`6d318a8`). README, the cabinet layout guide and the development guide describe the card, the keyboard model, the games list and both languages in plain words, with how to check both languages, reduced motion and invented locations locally.
3. **Owner's answer** ("approve, L3 yes"), recorded verbatim in the review document with an "After your answer" section marking each label done or unchanged with its commit (`ccd1c35`).
4. **Choices applied**:
   - D2 drop the hole (`dbc812f`): removed from the card script, the stylesheet and its unused colour variable; the stylesheet test and the browser tests now pin that the card has none.
   - L3 (`067fe16`): the swap row is now `display: grid; align-content: start`, so the text stays on the first ruling while the 56 px tap area, desktop rows and wrapped titles are unchanged. Base rows on an expansion card share the class and the fix. New browser tests check every text line of every swap row against the 28 px ruling grid at 390 and 1440 px in English and Dutch, over the 400-item sample (the base game with most expansions, its first and its longest-titled expansion card). They failed before the change (every one-line title started 21 px below a ruling and was 22 px tall) and pass after.
   - D4: todo `2026-10-11-uneven-desktop-sections-layout-pass.md` (committed with `ccd1c35`).
   - D1, D3, D5, L1, L2, L4 to L8 and T1 to T6: unchanged, as approved. `Spellenkast` was already the Dutch title and heading and is pinned by the integration and browser tests.
5. **Re-shot** the changed items at 390 px with synthetic data into `ui-refs/round-1/` (files start with `after-`). The new phone shots were looked at: one-line titles sit next to their chip above the ruling, wrapped titles fill both rulings.

## Verification (final)

- `dotnet build Cabinet.slnx`: 0 warnings, 0 errors
- non-browser .NET tests: 2193 passed
- browser tests: 130 passed, run twice
- `node --test build/tests/*.test.mjs`: 238 passed
- `bash build/lint.sh`: all five groups PASS

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Screenshot helper resized the window while a card was open**
- **Found during:** Task 1 (browser suite with the screenshot folder set)
- **Issue:** A full-page capture resized the viewport while a dialog was open, so focus was lost and the card moved; four browser tests failed.
- **Fix:** The shared helper saves the visible window while a dialog is open. Test code only.
- **Files modified:** `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs`
- **Commit:** `6477f4a`

**2. [Review finding correction] The phone expansion row diagnosis changed after the owner's look**
- The first proposal was to move the chip to the text; the owner's look at the Dutch phone shot showed the real defect: a one-line title was vertically centred by the button, so a ruling cut through the letters. The cause is the browser's button content centring in a two-ruling row, not the chip. The first automated ruling check covered row blocks only (that each row sits on the 28 px grid), not where the text sits inside a row, which is why it passed. The new test checks text line boxes against the grid.

## Open items for the release round

The owner hand checks stay open (they need a deployed site and real devices):

- C1: Back on real Android and iOS phones closes the card, then leaves the site.
- C2: the sheet drag on a real phone (25% close threshold, 0.6 px/ms flick, rotation with a card open).
- C3: the feel of arrow-key movement over the cabinet with the 65 and 400 collections.
- C4: a screen-reader walk of the cabinet group and the games list in both languages.
- C5: the turn animation in Firefox and Safari (a fade is the accepted fallback).

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

Commits `6477f4a`, `25c3216`, `6d318a8`, `dbc812f`, `067fe16` and `ccd1c35` exist; the review document, the language table, the todo and the `after-` screenshots are on disk.

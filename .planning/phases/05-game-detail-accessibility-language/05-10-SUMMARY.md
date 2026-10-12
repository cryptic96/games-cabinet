---
phase: 05-game-detail-accessibility-language
plan: 10
subsystem: games-list
tags: [accessibility, screen-reader, skip-link, keyboard, language]
requires:
  - phase: 05
    provides: "card dialog with focus return to the opener (05-06), the cabinet as one tab stop (05-08), list strings in both languages (05-04, 05-05)"
provides:
  - "orderEntries(records, language): A to Z entries with owned expansions nested under every owned base game (node-tested)"
  - "entryName(record, copy, baseTitle): the announced name of an entry in English and Dutch"
  - "titleOnlyEntries(placements, copy): the list built from the drawn boxes before the card data arrives"
  - "initGamesList({ section, copy, language, openCard }) with showTitles, showCards and focusStart"
  - "the skip link and the visually hidden games list in the page, a fixed panel while focus is inside it"
affects: [05-11, 05-12, 05-13, 05-14, 05-15, 05-16]
tech-stack:
  added: []
  patterns:
    - "list buttons are reused by entry id on every redraw, so an opener stays connected and focus stays on the same entry"
    - "an unchanged list shape only rewrites the button text"
    - "clip-pattern hiding while focus is outside, a fixed panel on :focus-within"
key-files:
  created:
    - Cabinet.Service/wwwroot/js/games-list.js
    - build/tests/games-list.test.mjs
    - Cabinet.BrowserTests/GamesListTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/site.css
    - build/tests/page-scripts.test.mjs
    - Cabinet.IntegrationTests/CreditTests.cs
key-decisions:
  - "A blank title sorts after every named title, and the entries are ordered by the raw BGG title, so the untitled text never decides the position"
  - "A nested expansion is named for the base game it sits under, so an expansion of two owned bases reads correctly under each"
  - "The list shows every owned entry from the card data, including expansions the cabinet hides behind a marker; the title-only list can only show what is drawn"
requirements-completed: [A11Y-02]
duration: 75min
completed: 2026-10-10
status: complete
actuals:
  tokens: 14300
  tasks: 2
  commits: 2
---

# Phase 5 Plan 10: A games list for screen readers, with a skip link Summary

**A skip link, the first thing Tab reaches, leads into an A to Z games list behind the cabinet: each entry announces title, year, players, minutes and weight in the page language, opens the same card, and gets focus back when the card closes.**

## Accomplishments

- Task 1 (tracer): `games-list.js` holds the pure ordering (`Intl.Collator` with numeric and base sensitivity, then year with missing last, then entry id), the nesting of expansions under every owned base, the entry names and the title-only list, plus the DOM part. The page gets `a.skip-link` before the header and `section.games-list` with the `All games` / `Alle spellen` heading before the cabinet. `cabinet.js` shows titles after every draw, the facts when the card data arrives, and sends the skip link to the first entry (or the heading when there is none). The list is clipped away while focus is outside and becomes a fixed `--wood-dark` panel on `:focus-within`; the skip link shows only while focused. Entries are `tabindex="-1"` buttons with `aria-haspopup="dialog"`.
- Task 2: the arrow keys, Home and End are handled on the list from the start of Task 1 (the code was written in one piece), so Task 2 added the proof: node tests of nesting, 400-record stability, Dutch names and the DOM part on a fake page (focus kept across the title-to-cards swap, buttons reused, text-only update on an unchanged shape); the cabinet page harness gained tag selectors, an optional list and skip link, and optional card data; 8 more browser tests (keys, Tab leaving the list, no card data, empty cabinet, 400 games, never hidden from assistive technology, phone skip link, Dutch page).

## Task Commits

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | The skip link leads to an A to Z list whose entries open the card and get focus back (tracer) | c94d650 |
| 2 | The list moves with arrow keys, survives missing data, empty and huge collections, and both languages | b16005e |

## Verification

- `node --test build/tests/*.test.mjs`: 201 pass (games-list 26, page-scripts 119).
- `dotnet test --project Cabinet.BrowserTests`: 72 pass (GamesListTests 12, DutchGamesListTests 1, KeyboardTests and CardOpenTests unchanged).
- `dotnet test --project Cabinet.IntegrationTests`: 271 pass.
- `bash build/lint/checks/10-repo-rules.sh`: passes.
- Tracer gate: the tracer's browser tests passed end to end before the expansion task. The project verifies by hand at the end of the phase (`human_verify_mode: end-of-phase`), so no interim human checkpoint was raised, as in the earlier plans.
- Still for the end-of-phase hand check: a screen reader walk (tab to the skip link, listen down five entries, open one card and close it) in both languages.
- Screenshots `games-list-panel-desktop` and `skip-link-phone` are saved when `CABINET_SCREENSHOT_DIR` is set.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The page's own-origin reference test refused the skip link**
- **Found during:** Task 1 (integration tests)
- **Issue:** `CreditTests` requires every `href` and `src` to start with `/` apart from the credit link; the skip link's `href="#games-list-start"` is an in-page anchor.
- **Fix:** in-page anchors are excluded from that check; every other reference is still held to the own-origin rule.
- **Files modified:** Cabinet.IntegrationTests/CreditTests.cs
- **Commit:** c94d650

**2. [Rule 1 - Plan detail] The 400-game list has more entries than the cabinet has boxes**
- **Found during:** Task 2
- **Issue:** the plan's browser check expects the entry count to equal the distinct non-marker box ids. With the card data in, the list holds all 400 owned games, while the cabinet hides six expansions behind `+N more` markers.
- **Fix:** the test asserts the list holds 400 distinct entries and every drawn box. The title-only list (before the card data, or without it) still equals the drawn boxes, as the plan says.
- **Commit:** b16005e

**3. [Plan detail] Selectors in the markup and the fake page**
- The list is found by `.games-list-heading` and `.games-list-items` classes on the heading and the `ul` (plus `role="list"` on every `ul`, so list semantics survive `list-style: none` in Safari), and the cabinet page harness learned plain tag selectors. No product change beyond the classes.

**4. [Plan detail] Arrow keys landed in Task 1**
- `games-list.js` was written whole, so the key handler is in the Task 1 commit; Task 2 holds its proof.

## Known Stubs

None.

## Threat Flags

None. Entry names are composed as strings and set through `textContent`; no markup API is used.

## Self-Check: PASSED

- Created files exist: games-list.js, games-list.test.mjs, GamesListTests.cs.
- Commits c94d650 and b16005e exist on the worktree branch.

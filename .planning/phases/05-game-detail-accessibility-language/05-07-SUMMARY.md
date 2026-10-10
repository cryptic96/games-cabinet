---
phase: 05-game-detail-accessibility-language
plan: 07
subsystem: card-content
tags: [card, format, i18n, css, browser-tests]
requires:
  - phase: 05
    provides: "card dialog and card view (05-06), card records (05-02), copy tables (05-04, 05-05)"
provides:
  - "format.js: playersText, playTimeText, weightBand, oneDecimal, ratingText, ageText, factsFor, hasAnyDetail (pure, no imports)"
  - "card strip, stored-in group, rating, designers and mechanics rows in card-view.js"
  - "card.css rules for the strip, label-column groups and mechanics list on 28 px lines"
affects: [05-08, 05-09, 05-10, 05-11, 05-12]
tech-stack:
  added: []
  patterns:
    - "pure text logic in a dependency-free module, loaded in Node through a data URL"
    - "separator drawn by CSS with empty alternative text"
key-files:
  created:
    - Cabinet.Service/wwwroot/js/format.js
    - build/tests/format.test.mjs
    - Cabinet.BrowserTests/CardContentTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/card-view.js
    - Cabinet.Service/wwwroot/css/card.css
key-decisions:
  - "A strip with no entries but other details known (for example only a rating) draws neither strip nor note; the note shows only when none of the seven details is known or the card data failed"
  - "Mechanics are separated by a middle dot drawn after each item (non-breaking space plus dot) with a real space between items, so a wrapped line ends with the dot instead of starting with it"
  - "Dutch browser tests set the language cookie rather than the browser locale, since the page language comes from the cookie first"
requirements-completed: [DET-02, I18N-01]
duration: 40min
completed: 2026-10-10
status: complete
actuals:
  tokens: 21000
  tasks: 2
  commits: 2
---

# Phase 5 Plan 07: The card tells you everything about the game Summary

**The card now shows the game-night strip (players, time, weight word with number, minimum age), the location, rating, designers and mechanics, each only when known, in English and Dutch number formats, with the quiet note when nothing is known.**

## Accomplishments

- `format.js` holds all text decisions as pure functions: ranges with an en dash, one number when both ends are equal, zero and negative values treated as missing, weight bands decided on the shown one-decimal value (1.4 Light, 1.5 Medium-light, 4.4 Medium-heavy, 4.5 Heavy), decimals in the page language (`7,8 / 10`, `zwaarte 2,4 / 5`).
- `card-view.js` builds, in contract order: strip or quiet note, `Stored in` group, meta rows (rating, designers, mechanics), then the BGG link. All BGG strings go through `textContent`; locations and designers carry `dir="auto"` with no `lang`; the mechanics list carries `lang="en"`, `role="list"` and no cap.
- `card.css`: 28 px lines, two-column strip (one column at 40rem and below), 24 px decorative icons hanging at the line start, 9rem (7rem on phone) label column with labels aligned to the first line, 14 px mechanics.
- Eight browser tests (full card in order, no-details note, location presence and absence, singular and plural labels, Dutch words and comma with `Mechanismen` fitting its column, Dutch phone card, mixed-script cards without sideways scroll, ten cards never saying `0 min` or unknown).

## Task Commits

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Pure card text logic and its node tests (18) | 8dee9bd |
| 1 and 2 | Card strip, location, meta rows, CSS and browser tests | 893c7c5 |

## Verification

- `node --test build/tests/*.test.mjs`: 152 pass (format 18).
- `dotnet test --project Cabinet.BrowserTests`: 33 pass (CardContentTests 8).
- `build/lint/checks/10-repo-rules.sh`: passes.
- Screenshots `card-full-desktop`, `card-nodetails-desktop` and `card-full-phone-nl` were reviewed by the executor: strip reads at a glance, quieter rows follow, Dutch labels wrap cleanly. The hand check over both languages and scripts at 1440 and 390 px is left to the end-of-phase review.
- Tracer gate: the tracer's checks passed end to end before expansion; no interim human checkpoint, as in earlier plans of this phase.

## Deviations from Plan

**1. [Plan detail] Tests written after the implementation**
- The pure module and its node tests were written together and the browser tests after the view, so there is no separate failing-test commit.

**2. [Plan detail] Two commits for two tasks**
- Task 2's behaviour (partial records, zeros, nothing known) was implemented inside the same view code as Task 1 and its tests were added to the same files, so Task 1's view, CSS and browser tests and Task 2's additions share one commit.

**3. [Plan detail] Dutch page set by cookie**
- The plan names `Locale = "nl-NL"`; the browser tests set the `lang` cookie instead because a locale applies to a whole test class and the cookie is what the server reads first. Number formats come from the page language copy, not the browser locale.

**4. [Rule 1 - Bug] Mechanics separator lost its trailing space and wrapped to the line start**
- **Found during:** screenshot review of Task 1
- **Fix:** the dot is drawn after each item and a text space sits between items.
- **Commit:** 893c7c5

**5. [Rule 1 - Bug] Label cell stretched to the value's height**
- **Found during:** Dutch browser test (`Mechanismen` measured two lines tall)
- **Fix:** `align-self: start` on `dt`.
- **Commit:** 893c7c5

**6. [Plan detail] Rating-missing browser test dropped**
- The samples always give a rating together with details, so such a test would have been vacuous; missing ratings are covered by node tests.

## Known Stubs

None. The expansion rows, the swap and the punched hole belong to later plans.

## Threat Flags

None. Every BGG string reaches the page through `textContent`; directions are isolated with `dir="auto"` and `unicode-bidi: plaintext`; the location row shows only when the server sent a location, and tests use invented sample locations.

## Self-Check: PASSED

- Created files exist: format.js, format.test.mjs, CardContentTests.cs.
- Commits 8dee9bd and 893c7c5 exist on the worktree branch.

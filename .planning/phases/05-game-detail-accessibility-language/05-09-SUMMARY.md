---
phase: 05-game-detail-accessibility-language
plan: 09
subsystem: card-expansions-and-look
tags: [card, expansions, swap, css, contrast, browser-tests]
requires:
  - phase: 05
    provides: "card dialog (05-06), card content (05-07), keyboard (05-08), card records with expansions and bases (05-02)"
provides:
  - "owned-expansion rows on a base card, base rows on an expansion card, plain lines for unowned or unknown bases"
  - "swapTo(entryId) and openCard(..., { atExpansions }) in detail.js; showsOtherGame() for the pull-out plan"
  - "index-card stylesheet finish: gap rule, tap rows, hover tint, ink ring, punched hole"
  - "card-css, CardContrastTests, CardExpansionTests, CardLookTests (desktop and phone classes)"
affects: [05-10, 05-11, 05-12]
tech-stack:
  added: []
  patterns:
    - "swap rebuilds only the header and ruled area, so paper, dim and close button never fade"
    - "ruling proof: browser tests measure every block of the ruled area as a multiple of 28 px over mixed scripts"
key-files:
  created:
    - Cabinet.BrowserTests/CardExpansionTests.cs
    - Cabinet.BrowserTests/CardLookTests.cs
    - Cabinet.UnitTests/Configuration/CardContrastTests.cs
    - build/tests/card-css.test.mjs
  modified:
    - Cabinet.Service/wwwroot/js/card-view.js
    - Cabinet.Service/wwwroot/js/detail.js
    - Cabinet.Service/wwwroot/css/card.css
    - Cabinet.BrowserTests/CardContentTests.cs
key-decisions:
  - "Group spacing is one rule (.card-ruled > * + * margin-block-start: one ruling); the per-group bottom margins were removed"
  - "The strip's value and label spans take line-height 0 so the entry line is exactly one ruling (they measured 29 px with two font sizes)"
  - "The link is a block anchor with the icon inline after the text, so a longer translation wraps with the icon at its end and the whole row is the tap area"
  - "Look scenarios live in an abstract class run by a desktop class (mouse) and a phone class (touch) so the pointer media query is real in both"
requirements-completed: [DET-02]
duration: 75min
completed: 2026-10-10
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 2
---

# Phase 5 Plan 09: Owned expansions, the swap and the index-card look Summary

**A base game's card lists every owned expansion as a whole-row button that swaps the card in place, an expansion's card links back to its owned bases, `+N more` opens at the expansions, and the card finishes as the ruled index card with proven 28 px rhythm, tap sizes and contrast.**

## Accomplishments

- `card-view.js`: expansion records start with a bases group (one owned base: a `base-row` button `Expansion for {base}`; several: heading plus one row per base in game id order; unowned: plain line; unknown: the single word). Base records get a `card-owned` group (heading `tabindex="-1"` plus one `exp-row` per owned expansion, no cap) between `Stored in` and the meta rows. Chips take `--chip` through `setProperty` only for `^#[0-9a-f]{6}$`. Titles go through `textContent` with `dir="auto"`. A decorative `card-hole` is the last child.
- `detail.js`: `swapTo` fades header and ruled area over `--swap-fade` (read from the token), rebuilds them from the other record, scrolls the card to the top, focuses the new title, pushes no history step. `openCard(..., { atExpansions })` scrolls the owned-expansions heading to the top and focuses it. The marker click now passes `atExpansions`. `showsOtherGame()` is exposed for the pull-out plan's plain-fade close.
- `card.css`: one blank ruled line between groups, `exp-row`/`card-link` at `--hit-rows` rulings with hover and active tint, link icon after the text, punched hole, swap fade, bottom padding replaced by the hole block.
- Tests: 10 stylesheet checks (node), 4 contrast checks from the real `site.css` tokens, 6 expansion browser tests, 7 look scenarios run at 1440 px with a mouse and at 390 px with touch (mixed scripts over every edge-sample card, longest title, family, two-base, unowned-base, Dutch, row heights and overlap, ink ring, hole). The whole browser suite passes (79).

## Task Commits

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 (tracer) | Owned expansion rows, swap, link back | d8ffed4 |
| 2 | Index-card look, ruling proof, contrast and tap-size tests | 478f5b6 |

## Verification

- `node --test build/tests/*.test.mjs`: 183 pass (card-css 10).
- `dotnet test --project Cabinet.UnitTests --filter-class "*CardContrastTests"`: 4 pass.
- `dotnet test --project Cabinet.BrowserTests`: 79 pass (CardExpansionTests 6, CardLookTests plus PhoneCardLookTests 14, BackButtonTests 7, CardContentTests 8).
- `build/lint/checks/10-repo-rules.sh`: passes.
- Tracer gate: the tracer's checks passed end to end before the second task; no interim human checkpoint, as in earlier plans (human verification is set to end-of-phase).
- Screenshots `card-expansions-desktop`, `card-expansion-two-bases-desktop`, `card-look-desktop-en`, `card-look-phone-nl`, `card-look-edge-phone` were saved and the desktop ones reviewed by the executor. The hand comparison against the reference renders (cover mounts, red rule, hole) is left to the end-of-phase review. The hole sits below the fold of the scrolled card in the desktop screenshots, so its look still needs that review (drop rule applies).

## Deviations from Plan

**1. [Rule 1 - Bug] Strip entries measured 29 px instead of 28 px**
- **Found during:** Task 2 ruling test
- **Issue:** the 16 px value and 14 px label inline in one line made the line box a pixel taller, breaking the whole-ruling rule.
- **Fix:** `line-height: 0` on `.fact .v` and `.fact .l`; the entry's own 28 px line decides the height. No `flow-root` lever was needed for the mixed scripts.
- **Commit:** 478f5b6

**2. [Rule 3 - Blocking] Existing content-order test no longer held**
- **Issue:** the card-content test asserted the exact child order of the ruled area; the owned group and the hole are now children.
- **Fix:** the assertion ignores those two classes. File outside the plan's list.
- **Commit:** d8ffed4, 478f5b6

**3. [Plan detail] Marker test uses the 400-item sample**
- The 65-item sample draws no marker on the desktop profile; the marker test uses the sample the other marker tests use, at a 520 px high window so the card can scroll the heading to the top.

**4. [Plan detail] Chip fallback is the neutral wood colour**
- A link carries no palette index, so a missing or invalid chip uses the neutral cover colour; the server already sends the palette tone for games without art colour.

**5. [Plan detail] Requirements file not touched**
- DET-02 completion is left to the orchestrator's shared-file write after the wave.

## Known Stubs

None.

## Threat Flags

None. Titles reach the page through `textContent`; the chip colour is validated before `setProperty`; a swap only shows entries the card data (or the drawn box) holds.

## Self-Check: PASSED

- Created files exist: CardExpansionTests.cs, CardLookTests.cs, CardContrastTests.cs, card-css.test.mjs.
- Commits d8ffed4 and 478f5b6 exist on the worktree branch.

---
phase: 05-game-detail-accessibility-language
plan: 06
subsystem: card-dialog
tags: [card, dialog, history, focus, css, icons, copy]
requires:
  - phase: 05
    provides: "cards route (05-02), page language and copy tables (05-04, 05-05), browser test base (05-01)"
provides:
  - "initCardDialog: one native modal dialog, open, close, whenClosed, focus return, outside tap, history step"
  - "buildCard: the card DOM from a record, text only"
  - "createHistoryStep, sourceEntry, recordFromPlacement, indexPlacements (pure, node-tested)"
  - "card.css: paper card, phone sheet, close button, cover frame, generated cover, scroll lock"
  - "icons.svg sprite (seven symbols) addressed through data-icons on the dialog"
  - "card strings in English and Dutch; paper, rhythm and motion tokens on :root"
affects: [05-07, 05-08, 05-09, 05-10, 05-11, 05-12, 05-13, 05-14, 05-15, 05-16]
tech-stack:
  added: []
  patterns:
    - "card data fetched after every draw (low priority), kept in a map, replaced wholesale, discarded when its layout tag differs from the layout on screen"
    - "history step as a pure state machine over an injected history and timers"
    - "outside tap needs both pointerdown and click on the dialog frame"
key-files:
  created:
    - Cabinet.Service/wwwroot/js/detail.js
    - Cabinet.Service/wwwroot/js/card-view.js
    - Cabinet.Service/wwwroot/js/card-flow.js
    - Cabinet.Service/wwwroot/css/card.css
    - Cabinet.Service/wwwroot/img/icons.svg
    - build/tests/card-flow.test.mjs
    - Cabinet.BrowserTests/CardOpenTests.cs
    - Cabinet.BrowserTests/BackButtonTests.cs
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
    - build/tests/page-scripts.test.mjs
    - build/tests/copy-parity.test.mjs
key-decisions:
  - "A tap on the marker opens the base game's card by entry id; the marker stays the opener for focus return, and the base box is found by sourceEntry when a later step needs it"
  - "When a re-open waits 150 ms and the pop never comes, the pending pop is given up on, so a late pop closes the card instead of leaving the page"
  - "The card header is a header element, so the page-wide header rules are reset on it"
requirements-completed: [DET-01, DET-02]
duration: 95min
completed: 2026-10-10
status: complete
actuals:
  tokens: 20300
  tasks: 3
  commits: 4
---

# Phase 5 Plan 06: A tap opens the card, and every way of closing it works Summary

**Any box opens a native modal paper card (a bottom sheet on phones) from card data already in memory, in English or Dutch, and Escape, the close button, an outside tap and Back each close it once with focus back on the box and no dead history step.**

## Accomplishments

- Task 1 (tracer): a delegated click on the cabinet opens `dialog.card-dialog` with the card of that entry; focus goes to `h2#card-title`; on close focus returns to the opener, re-found by entry id and kind after a redraw. `cabinet.js` fetches `/cabinet/cards` after every successful draw with low priority under the same supersede counter, refetches once when the cards' layout tag differs from the layout response's `ETag`, and falls back to a record built from the drawn placement (with the quiet note) when the data is missing.
- Task 2: icon sprite and `data-icons` (file-versioned by the server), all card and games-list strings in both languages (parity test extended with the contract text), the paper, rhythm and motion tokens on `:root`, `card.css` (paper card, framed cover with corner mounts, edge-colour fill, generated cover with hidden plate text, sticky 44 px close button, phone sheet with grip, scroll lock, forced-colours border), pointer cursor on boxes, sprite added to the policy test.
- Task 3 (TDD): `createHistoryStep` state machine (11 node tests), one history step per open, outside tap requires both press and release on the frame, a re-open within 150 ms waits for the pending pop, stale step cleared on load. Seven browser tests cover each close path and the address.

## Task Commits

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | Clicking a box opens its card from data already in memory (tracer) | 51dd167 |
| 2 | Paper, cover frame, close button and sheet shape, both languages | bf705e9 |
| 3 (RED) | Failing test for the card's history step | 1a3a4a2 |
| 3 (GREEN) | Back closes the card, no dead step after any other close | 0b0970d |

## Verification

- `node --test build/tests/*.test.mjs`: 134 pass (card-flow 11, copy parity 11, page scripts 112).
- `dotnet test --project Cabinet.BrowserTests`: 25 pass (CardOpenTests 12, BackButtonTests 7, others unchanged).
- `dotnet test --project Cabinet.IntegrationTests` with `*CabinetPageTests` (20), `*LivePageTests` (2) and `*ContentSecurityPolicyTests` (19): pass.
- `git add -N . && bash build/lint/checks/10-repo-rules.sh`: passes.
- Tracer gate: the tracer's browser tests passed end to end before expansion; the project verifies by hand at the end of the phase, so no interim human checkpoint was raised (same as the cards-data plan). Screenshots of the desktop card and the phone sheet were reviewed by the executor; the card matches the direction B reference in structure (paper, ruling, red double rule, framed cover, round close button).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The page-wide `header` rules broke the card header**
- **Found during:** Task 2 (first screenshot)
- **Issue:** `site.css` styles every `header` (grid columns, `header > *` spanning all columns, bottom margin), so the cover filled the whole card width.
- **Fix:** `.card-head` resets the margin and `.card-head > *` the column placement.
- **Files modified:** Cabinet.Service/wwwroot/css/card.css
- **Commit:** bf705e9

**2. [Rule 3 - Blocking] Razor page has no `Context`**
- **Issue:** `Context.Request.PathBase` does not compile in a Razor page.
- **Fix:** `Request.PathBase`.
- **Commit:** bf705e9

**3. [Rule 1 - Plan expectation not attainable] A reload with a card open leaves one stale entry**
- **Found during:** Task 3
- **Issue:** the plan expects one Back after a reload with a card open to reach the previous site. The step pushed before the reload is an entry the browser cannot remove without a navigation, so `replaceState(null)` clears its state but the entry stays: one Back lands on the same address with no card, a second Back leaves.
- **Fix:** the test asserts what the browser can do (no card, state cleared, address unchanged, first Back stays on the address with no card, second Back leaves). Removing the entry with `history.back()` would reload the earlier document, which is worse.
- **Files modified:** Cabinet.BrowserTests/BackButtonTests.cs
- **Commit:** 0b0970d

**4. [Rule 1 - Test race] Closing then pressing Back at once races the page's own Back**
- The browser tests wait until the card step is gone (`history.state` is null) before the next Back, as a person cannot press Back within a frame. Not a product change.

**5. [Plan detail] Marker test uses sample 400**
- Sample 65 draws no "+N more" marker; sample 400 does, so the marker test opens that sample.

**6. [Plan detail] Copy keys added in two steps**
- The four keys the first slice needs (`close`, `bggLink`, `newTabHint`, `noDetails`) were added in Task 1, the rest in Task 2, so the parity test stayed green at every commit.

**7. [Plan detail] Test gutter**
- With the page behind locked, `scrollbar-gutter: stable` reserves a classic scrollbar's width, so on desktop browsers the sheet is 15 px narrower than the window; the geometry tests allow that. Phones have overlay scrollbars.

## Known Stubs

None. The ruled area holds only the quiet note and the BGG link; the game-night strip, location, expansions, rating, designers, mechanics and the punched hole belong to later plans in this phase, and the strings for them already exist in both languages.

## Threat Flags

None. The only new request is the same-origin cards route; the BGG link is a fixed host plus digits-only id with `rel="noopener"`, text goes through `textContent`, covers must match the stored-art path, and no request goes to BGG when a card opens (browser tests assert every request host is the test host).

## Self-Check: PASSED

- Created files exist: detail.js, card-view.js, card-flow.js, card.css, icons.svg, card-flow.test.mjs, CardOpenTests.cs, BackButtonTests.cs.
- Commits 51dd167, bf705e9, 1a3a4a2 and 0b0970d exist on the worktree branch.

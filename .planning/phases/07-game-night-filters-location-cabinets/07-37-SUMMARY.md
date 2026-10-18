---
phase: 07-game-night-filters-location-cabinets
plan: 37
subsystem: ui
tags: [filter-rail, filter-panel, rail-state, phone-filters-button, copy, container-query]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "The dock markup and look from plan 07-35: rail buttons with data-label, the hidden two-line count and rail Clear, the panel count slot, and the filled, unavailable and highlighted styles"
provides:
  - "showRail() in filters.js: data-filled, data-unavailable, value in each rail button's aria-label and title, the two-line count, the rail Clear (highlighted at zero), the active suffix and title of both three-line buttons"
  - "The panel count line names the collection at rest from 64rem (gamesTotal); the panel's text Clear is hidden while nothing is filtered"
  - "The rail Clear clears every filter but the view and moves focus to the rail's Search button"
  - "copy.js railName(label, value), railCountOf(total), gamesTotal(total); filtersButton is now the plain word 'Filters'"
  - "The phone Filters button: #i-menu before the word, .filter-open-count filled pill (aria-hidden), the 30rem container rule that hides the word while filtering"
  - "FilterDockRailTests (7 tests); FilterSlimBarTests re-measured and its two desktop tests replaced"
affects: [07-36, 07-38, 07-41, 07-42, 07-43, filters]

tech-stack:
  added: []
  patterns:
    - "Rail names are composed in the script from each button's server-rendered data-label plus the value, written with setAttribute only"
    - "The slim bar is a named size container (filter-slim) only inside the below-64rem media block, never on the dock"

key-files:
  created:
    - Cabinet.BrowserTests/FilterDockRailTests.cs
  modified:
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/js/copy.js
    - build/tests/fixtures/filter-copy-cases.json
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/filters.css
    - Cabinet.BrowserTests/FilterSlimBarTests.cs
    - Cabinet.BrowserTests/FilterSheetTests.cs

key-decisions:
  - "filtersButton became a plain string ('Filters') rather than a function that ignores its argument; the fixture cases with arguments still read the word because the copy test returns plain strings as they are"
  - "A card-based rail button is never filled while the card details are off, enforced in railValue() as well as by the reset state"
  - "The slim bar's 30rem rule uses a named container (container: filter-slim / inline-size) inside the existing below-64rem media block, so the dock never becomes a containing block"

patterns-established:
  - "Rail state is rewritten in the same task as every count update (showTally calls showRail), and again when the card details come back"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05]

duration: about 1 session
completed: 2026-10-18
actuals:
  tokens: 16000
  tasks: 3
  commits: 4
---

# Phase 7 Plan 37: The folded rail says what is filtered, the panel counts the games, and the phone's Filters button gets the three-line symbol Summary

**A filter in use now fills its rail button and puts its value in the button's name and tooltip ("Players: 4", "Locatie: Cabinet A"). The rail also shows a two-line count and an icon Clear while filtering. The panel's count line reads "N games" at rest. The phone's Filters button has the three-line symbol and a filled count, and the word steps aside on a narrow bar.**

## Performance

- **Tasks:** 3 of 3
- **Files:** 8 (1 created, 7 modified)

## Accomplishments

- **Rail state:** a group that is not on Any gets `data-filled` (background equals the computed `--room-text`). Its `aria-label` and `title` become `{label}: {value}`, where the value is the chip's visible text, the option text of the chosen location (owner text, set with `setAttribute`) or the typed query. Other groups keep their server label.
- **Rail count and Clear:** while filtering the rail shows the number on line 1 and `of N` / `van N` on line 2, plus the icon Clear. At zero matches the number is 0 and the Clear is highlighted. The rail Clear clears search, players, time and location but keeps the view. It moves focus to the rail's Search button and announces "Filters cleared, N games".
- **Three-line buttons:** both are named "Filters · 1 active" / "Filters · 1 actief" through the hidden suffix, and their `title` carries the same full name.
- **Panel count line:** from 64rem it reads "N games" in muted weight 400 at rest, "11 of N games" in strong weight 600 while filtering, and "No games match" at zero. "1 of N games" was checked too. The panel's text Clear is `hidden` at rest and highlighted at zero. Below 64rem the slim count keeps its short form and stays empty at rest.
- **Card details failed:** the Players, Max. minutes and Location rail buttons carry `data-unavailable`, are dashed and never filled, even with a stored value. The note sits 8px under the panel's count line, and search still fills the Search button.
- **No locations:** there is no rendered Location rail button, View group or Location group.
- **Long text:** a 50-character synthetic location name appears whole in the Location button's name and tooltip. The Dutch "van N" line on the 400 sample is never clipped and never breaks inside a word, in plain and forced colours.
- **Phone Filters button:** `#i-menu` (20px) sits before the word with an 8px gap. While filtering, a filled 20px pill (`--room` on `--room-text`, `aria-hidden`) follows the word. On a slim bar narrower than 30rem the word is visually hidden while filtering (still in the name), so the button is 88px. At rest and at 768px the word shows. In forced colours the pill gets a `ButtonText` outline.
- **Copy:** `railName`, `railCountOf` and `gamesTotal` are in both tables, and `filtersButton` is now the word alone. There are 20 new fixture cases (`stack: "js"`), the two `filtersButton` cases were updated, and the parity test is unchanged and green.

## Task Commits

1. **Task 1 (tracer): a chosen player count fills and names the rail's Players button.** Commit `0447c15` (feat).
2. **Task 2: every rail state and the panel's count line.** Commit `c2b4b7e` (feat).
3. **Task 3: the phone Filters button's symbol and filled count; the slim bar tests.** Commit `05e1f40` (feat).

## Files Created/Modified

- `Cabinet.Service/wwwroot/js/filters.js`: `chipText`, `railValue`, `showRail`, `countText`; the rail Clear handler; the pill in `showFiltersButton`; `enableCardFilters` rewrites the tally
- `Cabinet.Service/wwwroot/js/copy.js`: the three new functions in both tables; `filtersButton` is a plain string
- `build/tests/fixtures/filter-copy-cases.json`: rail names, rail count and rest count cases in both languages
- `Cabinet.Service/Pages/Index.cshtml`: `#i-menu` and `span.filter-open-count[aria-hidden][hidden]` inside `button.filter-open`
- `Cabinet.Service/wwwroot/css/filters.css`: the button's gap and symbol, the pill, its forced-colours outline, and the `filter-slim` container with the 30rem rule
- `Cabinet.BrowserTests/FilterDockRailTests.cs`: 7 tests
- `Cabinet.BrowserTests/FilterSlimBarTests.cs`: one-row case re-measured; tablet case added; the two desktop tests replaced; hint fit check now compares the search container against 9.75rem; scroll recorder tracks removals
- `Cabinet.BrowserTests/FilterSheetTests.cs`: the two button-text assertions follow the new copy (see deviations)

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `FilterSheetTests.Filters_opens_a_live_sheet_and_Show_closes_it` asserted the old visible text**
- **Found during:** Task 3
- **Issue:** the test expected `.filter-open-text` to read "Filters · 1" and the suffix to read " active". The plan moves the number to the pill, so the test would have joined the failure list.
- **Fix:** it now expects "Filters", the pill "1" and the suffix " · 1 active". The accessible-name assertion ("Filters · 1 active") is unchanged.
- **Files modified:** `Cabinet.BrowserTests/FilterSheetTests.cs`. This file is also in plan 07-36's list, which runs in parallel. The edit is three lines in one method, so a merge conflict, if any, is small.
- **Commit:** `05e1f40`

**2. [Rule 1 - Bug] The rail's synthetic test data**
- **Found during:** Task 2
- **Issue:** `SyntheticBggCollection.Create(5)` lists two copies of one game as separate cards, and placing a game places every copy. So placing `Cards[0]` and `Cards[1]` left no unplaced game and no "Unknown location" option.
- **Fix:** the test places the second and third distinct games and asserts that at least three exist.
- **Commit:** `c2b4b7e`

### Plan notes

- `filtersButton` is a plain string, not a one-argument function (key decision above). `copy-parity.test.mjs` needed no new sample arguments: `railName` (2), `railCountOf` (1) and `gamesTotal` (1) are covered by the existing samples. That file was not modified.
- The listener test runs its desktop part at 1280 × 600, because the 65 sample at 1280 × 900 scrolls only 189px now that the top bar is gone.
- **U1 measurement** (throwaway probe, not committed). At rest the slim bar is one 60px row at 320, 360 and 390 in English and Dutch under both fonts. While filtering it is one row at 390 in both languages under both fonts, and at 360 except Dutch under DejaVu, where the status group wraps. The old "Filters · 1" button was about 9px wider than the new 88px one, so that case already wrapped before this plan; this change only narrows the row. At 320 the status wraps by design (`At_320_the_status_wraps_and_nothing_overflows`, unchanged, passing).
- Tracer gate: auto mode is off, but `human_verify_mode` is `end-of-phase`. The tracer's verify was re-run end to end (normal fonts and DejaVu, both passing) and the plan continued, as plan 07-35 did.

## Verification Runs

All with `DOTNET_USE_POLLING_FILE_WATCHER=1`, one suite at a time, after `dotnet build`.

| Run | Normal fonts | DejaVu fontconfig |
|-----|--------------|-------------------|
| `node --test build/tests/*.test.mjs` | 726 / 726 pass | n/a |
| `Cabinet.UnitTests` (whole project, includes `FilterCopyTests`, `SiteTextTests`, the Look CSS tests) | 2933 / 2933 pass | n/a |
| `FilterDockRailTests` (7) | pass | pass |
| `FilterSlimBarTests` (10) | pass | pass |
| `FilterDockGeometryTests` (3) | pass | pass |
| `FilterSheetTests` (17) | 15 pass; 2 fail, both red-list entries owned by 07-36 | same 2 |
| `build/lint/checks/10-repo-rules.sh` | pass | n/a |

## Red-List Delta

Full `Cabinet.BrowserTests` run, normal fonts, after all three commits: **741 total, 649 passed, 91 failed, 1 skipped.** Against the tracer's 733 / 639 / 93: there are 8 new tests (7 rail, 1 tablet), all passing, and 2 listed failures are fixed. **No failure was added.** The 91 failing names are exactly the 07-35 red list without the two below.

**Fixed (owned by this plan):**
- `FilterSlimBarTests.On_a_desktop_the_text_Clear_stays_and_the_count_keeps_its_words`, replaced by `On_a_desktop_the_panel_has_the_text_Clear_and_the_rail_the_two_line_count` (passes)
- `FilterSlimBarTests.The_full_bar_never_slides_and_the_listener_follows_the_width`, replaced by `The_dock_never_slides_and_the_listener_exists_only_on_a_phone` (passes)

**Still failing, listed in the red list (the same names, per class):** FilterAccessTests 9, FilterAlternateTitleTests 6, FilterBarGeometryTests 3, FilterBarTests 8, FilterDimTests 9, FilterFocusRingTests 3, FilterKeyboardTests 6, FilterListTests 4, FilterLocationTests 4, FilterLocationViewTests 4, FilterPersistenceTests 9, FilterSearchTests 7, FilterSheetTests 2, FilterShowOnShelfTests 5, FilterViewSwitchTests 9, KeyboardTests 2, OwnerCabinetTests 1.

The `FilterBarTests` entries the red list tags [07-37, 07-41] still fail. They reach the chips in a top bar at desktop widths. `FilterBarTests.cs` is in plan 07-41's file list and not in this plan's, so it was not edited here; the rail behaviour those tests touch is covered by `FilterDockRailTests`.

## Known Stubs

None. Every rail, panel and phone-button element this plan owns is wired to the live state.

## Threat Flags

None. Owner location names and the typed query reach the rail only through `setAttribute('aria-label' | 'title')`, and the count lines only through `textContent` (T-07-37-01). There is no new endpoint, storage or network path. The strict CSP is unchanged; the tests set colours through CSSOM properties only.

## Self-Check: PASSED

- FOUND: `Cabinet.BrowserTests/FilterDockRailTests.cs`
- FOUND: commits `0447c15`, `c2b4b7e`, `05e1f40`
- STATE.md and ROADMAP.md untouched (the orchestrator owns them)

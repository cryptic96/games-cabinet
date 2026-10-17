---
phase: 07-game-night-filters-location-cabinets
plan: 06
subsystem: copy
tags: [i18n, copy, ci, node-test, xunit]
requires:
  - phase: 07-01
    provides: SiteText filter labels, copy.js filterCount/noMatch/filtersCleared
provides:
  - full filter and location copy contract in English and informal Dutch (SiteText.cs, copy.js)
  - shared string fixture read by a C# test and a node test
  - browser-filters CI job
affects: [07-07, later filter and location UI plans]
tech-stack:
  added: []
  patterns:
    - one JSON case table with a stack field (js, cs, both) read by both test stacks
key-files:
  created:
    - build/tests/fixtures/filter-copy-cases.json
    - build/tests/filter-copy.test.mjs
    - Cabinet.UnitTests/Language/FilterCopyTests.cs
  modified:
    - Cabinet.Service/Language/SiteText.cs
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.UnitTests/Language/SiteTextTests.cs
    - build/tests/copy-parity.test.mjs
    - .github/workflows/ci.yml
decisions:
  - "Noun in a count follows the total (1 of 1 game, 0 of 14 games); group headings in the games list use the matching count while filtering"
  - "Heading helpers are module-level functions in copy.js, not this-based, so a destructured call still works"
metrics:
  tasks: 3
  commits: 3
status: complete
actuals:
  tokens: 10300
  tasks: 3
  commits: 3
---

# Phase 7 Plan 06: Every string in both languages, pinned by one fixture, and a CI job for the filter tests Summary

Every label, count, heading and announcement of the filter and location work now exists in English and informal Dutch, the phrases both the server and the page show are pinned by one shared fixture of 94 cases, and the filter browser tests have their own `browser-filters` CI job.

## What was built

- **Tracer (suffixes):** `PlayersSuffix` / `TimeSuffix` in `SiteText` and `playersSuffix` / `timeSuffix` in `copy.js` (singular for 1, open-ended form for 8, plural otherwise). The fixture and both test halves landed with it. The tracer's verify (node and C# tests) was re-run after the commit and passed.
- **Full copy contract:** `SiteText` gained `ViewLegend`, `ViewAll`, `ViewByLocation`, `PlayersLegend`, `TimeLegend`, `AnyChoice`, `LocationLabel`, `AnyLocation`, `UnknownLocation`, `FiltersButton`, `FiltersActiveSuffix`, `SheetTitle` and the suffix formats. `copy.js` gained `filterCountShort`, `viewAllAnnounce`, `viewLocationAnnounce`, `locationViewFailed`, `showGames`, `filtersButton`, `filtersButtonName`, `anyLocation`, `unknownLocation`, `locationHeadingCount`, `listGroupHeading`, `listStoredIn`, `listUnknownEntry`, `listUnknownGroupHeading`, `unknownLocationHeading`, `showOnShelfName`. The existing close and card-details failure strings are reused.
- **Fixture and tests:** `filter-copy-cases.json` (94 cases, both languages, zero/one/many forms; the suffixes, `anyLocation` and `unknownLocation` are stack `both`, the rest `js`). `filter-copy.test.mjs` also asserts the unknown-location heading equals the name plus `locationHeadingCount`, and that the list's unknown group heading follows `listGroupHeading`. `copy-parity.test.mjs` samples every new arity and now fails if any function arity has no sample.
- **CI:** `browser-filters` job copied from `browser-rest` running `Cabinet.BrowserTests.Filter*`; `browser-rest` excludes the same prefix; the comment now says five jobs. `FilterSearchTests` already matches the prefix and moves to the new job. actionlint and zizmor pass.

## Verification

- `node --test build/tests/*.test.mjs`: 591 passed.
- Full `Cabinet.UnitTests`: 2810 passed (with the polling file watcher set).
- `build/lint/checks/10-repo-rules.sh` and `20-workflows.sh` pass. The secrets step was not run (cannot run in a linked worktree).
- Mutation check of the tracer: changing the Dutch "8 of meer spelers" to a wrong value in `copy.js` failed the node test; the same change in `SiteText.cs` failed the C# test; both reverted.
- Browser classes were not run: no existing string changed, only strings were added.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] SiteTextTests needed two names added to its same-in-both-languages list**
- **Found during:** Task 2
- **Issue:** `FiltersButton` and `SheetTitle` are "Filters" in both languages by the copy contract, and the existing test requires every label to differ unless listed.
- **Fix:** Added both names to `SameInBothLanguages` in `Cabinet.UnitTests/Language/SiteTextTests.cs` (not in the plan's file list).
- **Commit:** cd9357b

**2. Tracer feedback gate run automatically, not as a stop**
- Auto mode is off, but the project's human-verify mode is end-of-phase and the tracer's verify is fully automated, so the gate re-ran the verify (node and C# tests) and continued rather than returning a checkpoint to a parallel wave.

## Known Stubs

None.

## Threat Flags

None. The new CI job copies the pinned actions and least permissions of `browser-rest`.

## Commits

- 3f22c69: feat(07-06): pin the players and time chip suffixes with one shared copy fixture
- cd9357b: feat(07-06): add the whole filter and location copy in English and Dutch
- a8dc50a: ci(07-06): run the filter browser tests in their own job

## Self-Check: PASSED

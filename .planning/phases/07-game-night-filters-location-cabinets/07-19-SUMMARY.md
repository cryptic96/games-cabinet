---
phase: 07-game-night-filters-location-cabinets
plan: 19
subsystem: search
tags: [search, alternate-names, names-index, filters, endpoint]
status: complete

requires:
  - phase: 07
    provides: "GameDetails.AlternateNames kept by the sync (plan 04), filter-rules.js / filters.js / cabinet.js (plans 01 to 17)"
provides:
  - "SearchNames.Build / Serialize and SearchNamesDocument (owned games' other titles, keyed by game id)"
  - "GET /cabinet/search-names with content-hash ETag, 304, sample support, no cross-origin headers"
  - "CollectionState.NamesIndex and SearchNamesCache for samples"
  - "withAlternates(model, names) in filter-rules.js and a lazy, low-priority names fetch in filters.js"
  - "Invented other titles on about a third of the sample games"
affects: []

actuals:
  tokens: 14300
  tasks: 2
  commits: 2

tech-stack:
  added: []
  patterns:
    - "Content-hash tagged JSON document cached per collection state, mirroring the cards document"
    - "Pure withAlternates enrichment of the filter model, so matching stays in the browser and node-testable"
    - "Index fetched once on first focus or input, refetched only when the layout entity tag changes"

key-files:
  created:
    - Cabinet.Domain/Collection/SearchNames.cs
    - Cabinet.Service/Search/SearchNamesEndpoint.cs
    - Cabinet.Service/Search/SearchNamesCache.cs
    - Cabinet.BrowserTests/FilterAlternateTitleTests.cs
    - Cabinet.UnitTests/Collection/SearchNamesTests.cs
    - Cabinet.IntegrationTests/SearchNamesEndpointTests.cs
  modified:
    - Cabinet.Domain/Samples/SampleCardDetails.cs
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - Cabinet.Service/wwwroot/js/filter-rules.js
    - Cabinet.Service/wwwroot/js/filters.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - build/tests/filter-rules.test.mjs

key-decisions:
  - "A failed names request is remembered and not retried per keystroke; the next attempt comes only after the collection changes"
  - "On a collection change the held names stay in use until the new ones arrive (refetched at once only when the field has text), so a live redraw never makes other-title matches vanish for a moment"
  - "A name is dropped only when ordinal-equal to a title the collection shows for that game (any copy); case differences are kept because search folds them anyway"
  - "rebuild takes the layout entity tag as a third argument; the second argument stays the owner's locations"

requirements-completed: [FILT-01]

coverage:
  - id: D1
    description: "Typing a game's other title lights its family once the names arrive; title matches still light in the same pass; the join is quiet"
    requirement: FILT-01
    verification:
      - kind: browser
        ref: "Cabinet.BrowserTests/FilterAlternateTitleTests.cs"
        status: pass
      - kind: unit
        ref: "build/tests/filter-rules.test.mjs"
        status: pass
  - id: D2
    description: "The names travel in a separate lazily fetched document: owned games only, own title dropped, ETag and 304, sample honoured, no cross-origin headers, silent failure"
    requirement: FILT-01
    verification:
      - kind: unit
        ref: "Cabinet.UnitTests/Collection/SearchNamesTests.cs"
        status: pass
      - kind: integration
        ref: "Cabinet.IntegrationTests/SearchNamesEndpointTests.cs"
        status: pass
      - kind: browser
        ref: "Cabinet.BrowserTests/FilterAlternateTitleTests.cs"
        status: pass
  - id: D3
    description: "After a live redraw that changes the collection the names are fetched again on the next use"
    requirement: FILT-01
    verification:
      - kind: browser
        ref: "Cabinet.BrowserTests/FilterAlternateTitleTests.cs"
        status: pass
---

# Phase 7 Plan 19: A Dutch or other title finds the game too Summary

Owned games' other titles, kept by the details sync, now travel in a minimal `/cabinet/search-names` index that the page fetches once on first use of the search field and matches in the browser, so typing an edition's title lights its family with no BGG call and no relay of BGG data.

## What was built

- **Names index (tracer).** `SearchNames.Build` keeps, for games the snapshot owns, the cleaned alternate names that differ from any title shown for the game; games are in identifier order, so the same data always gives the same bytes. `SearchNamesEndpoint` serves it with a content-hash `names-` ETag, `Cache-Control: no-cache`, 304 on a match, the sample honoured through the catalog, an empty `{"names":{}}` before the first sync, and no cross-origin headers. `CollectionState.NamesIndex` caches it per collection state; `SearchNamesCache` caches it per allowlisted sample.
- **Page.** `withAlternates` adds folded other titles to the haystack of every family holding a game with that id (pure, does not change the model it is given). `filters.js` fetches the index once, at low priority, on the first focus or input of the field; on arrival it rebuilds the haystacks and re-applies the pass without anything visible, and announces the count only if it changed. A failure is silent and not retried until the collection changes. `cabinet.js` hands the layout's entity tag to `filters.rebuild`, which makes the next use fetch again after a live redraw changed the collection.
- **Samples.** About a third of the games with details get one or two invented other titles (accented and Dutch-like, none from a real game).

## Verification

- `node --test build/tests/*.test.mjs`: 624 passed.
- `Cabinet.UnitTests`: 2893 passed (full project). `Cabinet.IntegrationTests`: 540 of 541 passed; the one failure is `TestHostCleanupTests`, which cannot pass with the polling watcher the host requires.
- Browser: `FilterAlternateTitleTests` (6) passed, and all `Filter*` classes with `KeyboardTests`, `GamesListTests`, `CabinetSmokeTests`, `CardSyncTests` and `LanguageSwitchTests` passed (82 of 82 on two full runs; one earlier full run under load had a single failure that did not repeat on two reruns of the same set).
- Repo rules, workflow and shell lint checks and the script tests pass; the secrets check was skipped (cannot run in a linked worktree).
- Tracer feedback gate: the tracer's verify (node tests, browser build and the first browser test) was re-run end to end after the commit and passed; human review is deferred to end of phase by the project's verify mode.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] `rebuild` signature**
- **Found during:** Task 1
- **Issue:** The plan names `rebuild(records, { layoutEtag })`, but the second argument of `rebuild` is already the owner's locations list.
- **Fix:** The entity tag goes in a third argument, `rebuild(records, locations, { layoutEtag })`; `cabinet.js` passes it from both the layout draw and the card data load.
- **Files modified:** `Cabinet.Service/wwwroot/js/filters.js`, `Cabinet.Service/wwwroot/js/cabinet.js`
- **Commit:** 54782ca

**2. [Refinement] Held names kept across a collection change**
- **Found during:** Task 1
- **Issue:** Dropping the index outright on a layout tag change would make other-title matches vanish for a moment while a typed search is active.
- **Fix:** The index is marked unasked (a new generation, so an old in-flight answer is ignored) but the held names keep serving until the new ones arrive; a refetch starts at once only when the field has text.
- **Commit:** 54782ca

Otherwise the plan was executed as written.

## Known Stubs

None.

## Threat Flags

None. The names endpoint is the planned surface; it carries only the owned games' cleaned other titles, no other field, no cross-origin headers, and nothing reaches the data source from a visit.

## Self-Check: PASSED

- Commits 54782ca and e22b0c6 exist on the worktree branch.
- All listed files exist, `/cabinet/search-names` appears in `SearchNamesEndpoint.cs`, and `withAlternates` is exported from `filter-rules.js`.

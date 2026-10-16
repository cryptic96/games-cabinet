---
phase: 07-game-night-filters-location-cabinets
plan: 18
subsystem: owner-ui
tags: [owner-pages, pictures, picture-link, per-copy, razor, vanilla-js]
requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "07-13 per-copy PictureEdits and entry-keyed overlay; 07-16 per-copy Pictures page rows"
provides:
  - "PictureEdits.UseLink(data, gameId, collectionId, copies, url, record, title, ownedUrl, mainUrl) and PictureEdits.RemoveLink(data, gameId, collectionId, copies, title)"
  - "LinkPreview.Build(snapshot, gameId, collectionId, rules, record)"
  - "StagedLink.CollectionId and LinkPreviewModel.CollectionId; previews keyed pic-preview-{collectionId}"
  - "Check link, Use this picture, Remove link, Discard preview and Undo name the copy's collection entry; each refuses a collection id that is not an entry of the posted game"
  - "The link form is offered on every copy row"
affects: [07-20]
requirements-completed: [LOC-05]
status: complete
actuals:
  tokens: 40000
  tasks: 2
  commits: 2
key-files:
  modified:
    - Cabinet.Domain/Owner/PictureEdits.cs
    - Cabinet.Domain/Owner/LinkPreview.cs
    - Cabinet.Service/Owner/StagedItems.cs
    - Cabinet.Service/Pages/Owner/LinkPreviewModel.cs
    - Cabinet.Service/Pages/Owner/_LinkPreview.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.Service/wwwroot/js/owner-link.js
    - Cabinet.IntegrationTests/OwnerLinkTests.cs
    - Cabinet.IntegrationTests/OwnerLinkPreviewTests.cs
    - Cabinet.IntegrationTests/OwnerCheckLinkSlotTests.cs
    - Cabinet.UnitTests/Owner/LinkPreviewTests.cs
    - Cabinet.UnitTests/Owner/RemoveLinkTests.cs
    - Cabinet.UnitTests/Owner/StagedItemsTests.cs
    - Cabinet.UnitTests/Owner/PictureEditsKeepCopiesTests.cs
    - Cabinet.BrowserTests/OwnerLinkFlowTests.cs
decisions:
  - "The game-level UseLink and RemoveLink forms keep their signatures (every copy) but no page handler calls them any more; the link handlers all take a copy"
  - "Discard preview with a foreign collection id is refused; with a valid copy but a token staged for another copy it does nothing and leaves that preview in place"
  - "The no-script Undo form in the notice still carries only the game, so it reports on the game's first copy; scripted Undo posts the copy"
---

# Phase 7 Plan 18: A picture link belongs to one copy Summary

Checking a pasted picture link, previewing it, using it, removing it and undoing now all name one copy's collection entry, so a link for copy 2 never touches copy 1, and the link form appears on every copy row of the Pictures page.

## What was built

- **Task 1 (tracer), c98bece:** `PictureEdits` gained per-copy `UseLink` and `RemoveLink` built on the existing `EditOneCopy` helper (a game-wide entry is split first, every other entry and every place is kept). `LinkPreview.Build` takes the collection id, builds its overlay with a per-copy entry and picks the trace by collection id. `StagedLink`, `LinkPreviewModel`, the preview partial, the link form and `owner-link.js` carry `collectionId`; preview sections are `pic-preview-{collectionId}`. `PicturesModel` link handlers (Check link, Use, Remove, Discard) refuse a collection id that is not an entry of the posted game, before anything is fetched or staged; Use also refuses a token staged for another copy (`link-expired`). The first-copy-only restriction (`OffersLink`, `_firstEntries`) is gone and redirects return to the copy's own row. Tracer gate: `A_link_used_for_one_copy_draws_only_that_copy` and `Every_link_handler_refuses_a_foreign_copy` pass.
- **Task 2, 782b1d5:** per-copy cases in `PictureEditsKeepCopiesTests` (UseLink and RemoveLink, including from a game-wide entry, and a refused unusable record), `RemoveLinkTests`, `LinkPreviewTests` (second copy's box), `StagedItemsTests` (two copies of one game staged apart); the preview and slot integration tests post `collectionId`; a browser test that a link pasted on the second copy row is used for that copy alone.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing tests broken by the per-copy contract**
- `OwnerLinkFlowTests` (browser): the three no-script cases and the Remove case ran against a game owned twice, where every copy row now has a link form (strict-mode violations on `summary`, `Picture link`) and the log texts name the copy (`(copy 1 of 2)`). Locators are scoped to the first copy row and expectations follow the copy-named log text. `OwnerLinkPreviewTests` log expectation updated the same way. Added files beyond the plan's list: `Cabinet.IntegrationTests/OwnerLinkPreviewTests.cs` was listed; `Cabinet.BrowserTests/OwnerLinkFlowTests.cs` and `Cabinet.UnitTests/Owner/PictureEditsKeepCopiesTests.cs` were not.
- **Commit:** 782b1d5

## Known Stubs

None.

## Verification

- `dotnet build Cabinet.slnx`: 0 warnings, 0 errors.
- `dotnet test --project Cabinet.UnitTests` (full): 2884 passed; the one failure was my own new second-copy preview test (wrong size expectation), fixed and re-run green with the link, remove, staged and keep-copies classes (44 passed).
- `dotnet test --project Cabinet.IntegrationTests` (full): 530 passed, 2 failed: `TestHostCleanupTests` (inotify count under the polling watcher, ignored per host notes) and `OwnerLinkPreviewTests.Using_the_preview_stores_...` (log text now names the copy), fixed; the three link integration classes re-run: 38 passed.
- `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.Owner*"` (DejaVu fontconfig, polling watcher): 218 passed after the locator fixes (first run: 213 passed, 4 failed in `OwnerLinkFlowTests`, all fixed).
- `node --test build/tests/*.test.mjs`: 614 passed.
- `bash build/lint/checks/10-repo-rules.sh`: passed. The secrets step was not run (cannot run in a linked worktree).

## Threat Flags

None beyond the plan's register. T-07-18-01: every link handler refuses a collection id that is not an entry of the posted game (integration test covers Check link, Use, Remove, Discard and a missing id; revision unchanged, nothing fetched, a foreign post leaves another copy's preview in place). T-07-18-02: the fetcher, allow-listed host, caps and credential-free client are untouched; only the key the staged result is stored under changed.

## Self-Check: PASSED

- Commits found: c98bece, 782b1d5.
- Modified files exist: Cabinet.Domain/Owner/PictureEdits.cs, Cabinet.Service/Pages/Owner/Pictures.cshtml.cs, Cabinet.Service/wwwroot/js/owner-link.js, Cabinet.IntegrationTests/OwnerLinkTests.cs.

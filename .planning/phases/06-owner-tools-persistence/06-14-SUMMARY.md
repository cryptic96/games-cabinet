---
phase: 06-owner-tools-persistence
plan: 14
subsystem: owner-pictures
tags: [overlay, snapshot-mapper, collection-version, razor-page, owner-data, browser-tests]
requires:
  - phase: 06-03
    provides: "ArtVerdictKind and the art verdicts the box-shape chain relies on"
  - phase: 06-08
    provides: "OwnerData.Games[].Picture, the single writer and the overlay"
  - phase: 06-10
    provides: "CollectionPublisher that applies the overlay when it builds the state"
  - phase: 06-13
    provides: "Owner page patterns, shared cover helper and the per-row status conventions"
provides:
  - "OwnerOverlay.PictureOverrideOf: the owner's non-automatic picture choice per game (kind, stored link record, link address)"
  - "ArtPick.OwnerLink and MappedItemTrace.PickedByOwner"
  - "SnapshotMapper.Explain, ToCabinetItems and Version overloads that take the overlay; the forced pick and the choice in the version"
  - "PictureEdits.Choose: source kind, checked flag, seen addresses, link kept, change-log wording"
  - "Pictures page on the owner port with one row per synced game, four choices and Save picture; handlers Choose and Undo"
affects: [pictures, settings]
actuals:
  tokens: 14500
  tasks: 2
  commits: 4
tech-stack:
  added: []
  patterns:
    - "A choice is stored as a source kind that follows BGG, never as a pinned address; the choice and the link address are folded into the version line of only the games that have one, so a collection with no choices keeps its version"
    - "A forced pick bypasses the flat-cover detector for the picture drawn but not for the box shape: only a flat chosen cover may shape a box, an unsure one may only turn it"
key-files:
  created:
    - Cabinet.Domain/Owner/PictureEdits.cs
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.IntegrationTests/OwnerPictureChoiceTests.cs
    - Cabinet.UnitTests/Collection/SnapshotMapperOverlayTests.cs
    - Cabinet.BrowserTests/OwnerPicturesPageTests.cs
  modified:
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Domain/Collection/ArtChoice.cs
    - Cabinet.Domain/Owner/OwnerOverlay.cs
    - Cabinet.Domain/Owner/OwnerData.cs
    - Cabinet.Domain/Owner/LocationEdits.cs
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Service/Review/ReviewSheetModel.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/Pages/Owner/Locations.cshtml.cs
    - Cabinet.Service/wwwroot/css/owner.css
key-decisions:
  - "Choosing Link without a stored link is refused (no-link); choosing Owned edition or Main picture while BGG offers no picture of that kind is refused (no-picture); both are in the fixed refusal set"
  - "The choice is parsed by name only, so a number that happens to be a valid enum value is refused as an unknown picture"
  - "PictureOverride carries the link address next to the stored record, because the version line needs the address and a failed link has a record that cannot be drawn"
  - "Switching to Automatic keeps the game's setting while it is checked or holds a link; it is dropped only when it would be empty"
  - "The unavailable marker on a choice is written as an explicit true or false, since an empty attribute still matches a bare attribute selector"
requirements-completed: [IMG-02]
duration: about 1h 20m
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 14: Pictures tracer Summary

**An owner's choice of Owned edition, Main picture or back to Automatic on the owner port's Pictures page now changes the face-out cover, the card picture, the spine colour and the collection version for every visitor, through a forced pick in the mapper that keeps the box-shape rules intact.**

## Performance

- **Duration:** about 1h 20m
- **Completed:** 2026-10-12
- **Tasks:** 2 (the tracer and the mapper rules task), plus a browser check the phase brief asked for
- **Files changed:** 17 (1253 insertions, 29 deletions)

## Accomplishments

- The tracer runs end to end over real HTTP: a post to the Choose handler with `MainPicture` changes that game's cover URL on `/cabinet/cards`, its colour pair, and both entity tags; `Automatic` restores the earlier cover; the change log reads `Picture of {title}: main picture` then `Picture of {title}: back to Automatic`.
- `OwnerOverlay` holds a `PictureOverride` only for non-automatic settings. `SnapshotMapper.Explain`, `ToCabinetItems` and `Version` have overloads taking the overlay and the existing overloads delegate with `OwnerOverlay.None`, so every golden layout file is unchanged.
- The forced pick lives in `Trace`: Owned edition and Main picture draw that record when usable, Your link draws the stored link record as `ArtPick.OwnerLink`, and anything unusable falls back to exactly what the plain mapper draws. The chosen picture's verdict still feeds `BoxShape.Resolve`, so a chosen flat cover may shape a box with no BGG size, a chosen 3D shot never does and an unsure one may only turn the box.
- The choice and the link address join the version line of the games that have one, so choosing the picture Automatic already shows still produces a new version, new ETags and fresh caches; an overlay of only Automatic settings gives the same version as no overlay.
- `PictureEdits.Choose` stores the kind, marks the game checked for any kind but Automatic, records the two candidate addresses seen, keeps a stored link and its record when switching away, returns `OwnerChange.None` when nothing changes and refuses unavailable choices.
- The Pictures page (owner port only, 404 on the public port) lists every synced game once with a native radio group of four choices (inert when a kind has nothing), the cover the cabinet draws now, and a `Save picture` button that works without script; Undo goes through the same writer.
- Looked at in a real browser at 1440 and 390 px (screenshots in the session scratchpad): paper rows, four columns on desktop and two on phone, every target at least 44 px, no sideways scroll, unavailable choices dashed.

## Task Commits

1. **Task 1 red:** `8a6368c` test: failing tests for choosing a picture on the owner port
2. **Task 1 green (tracer):** `8597d52` feat: owner picture choice reaches every visitor's cabinet and card
3. **Task 2:** `77fb3e0` test: forced pick keeps the box-shape chain and counts in the version
4. **Browser check:** `a514ad2` test: browser check of the pictures page at desktop and phone width

The tracer's own verify (integration class `OwnerPictureChoiceTests`) was green before Task 2 started, so the slice was proven before anything was built on it.

## Verification

- `OwnerPictureChoiceTests`: 7 passed. `SnapshotMapperOverlayTests`: 10 passed. `OwnerPicturesPageTests` (browser): 3 passed.
- Full unit project 2141 passed, full integration project 355 passed, golden layout files unchanged.
- `10-repo-rules.sh`, `40-secrets.sh` (no leaks) and `50-script-tests.sh` pass. No JavaScript was touched, so the node tests were not needed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] An empty attribute styled every available choice as unavailable**
- **Found during:** the browser check (screenshots showed dashed tiles on available choices)
- **Issue:** the page wrote the unavailable marker as a null-valued conditional attribute, which rendered as an empty attribute on a label and matched the bare attribute selector.
- **Fix:** the marker is now written as an explicit `true` or `false` and the stylesheet and test select on `true`; the browser test asserts that disabled choices are dashed and enabled ones solid.
- **Files modified:** `Cabinet.Service/Pages/Owner/Pictures.cshtml`, `Cabinet.Service/wwwroot/css/owner.css`, `Cabinet.BrowserTests/OwnerPicturesPageTests.cs`
- **Commit:** `a514ad2`

**2. [Rule 2 - Missing critical functionality] Refusal keys for choices that have nothing behind them**
- **Found during:** Task 1
- **Issue:** the plan names strict enum binding and unknown game refusal but a post for Your link with no stored link, or for a kind BGG offers nothing for, would otherwise store a choice that can never be drawn.
- **Fix:** `Choose` refuses with `no-link` or `no-picture`; both joined the fixed refusal set of `OwnerResponses` with owner-facing words in `OwnerStrings`. The handler parses the choice by name only.
- **Files modified:** `Cabinet.Domain/Owner/PictureEdits.cs`, `Cabinet.Service/Owner/OwnerResponses.cs`, `Cabinet.Service/Owner/OwnerStrings.cs`, `Cabinet.Service/Pages/Owner/Pictures.cshtml.cs`
- **Commit:** `8597d52`

### Plan adjustments

- `LocationEdits.IsEmpty` and `LocationsModel.CoverOf` became `internal` so the pictures code reuses them instead of copying them.
- `PictureOverride` carries a third member, the link address, because the version line needs it; the plan listed two.
- `MappedItemTrace.PickedByOwner` is a trailing optional member (default false) so existing constructions keep compiling.
- The synthetic collection already held every review case the plan lists (owned edition and main differ, a 3D owned edition, a game with no owned-edition picture, a game with no usable picture, expansions with one, several and no owned base), so `SyntheticArtCollection.cs` was not changed; a test now asserts each case exists and will fail if one is removed.

### TDD note

The Task 1 tests were committed red first (405 and 404 answers, then green). The Task 2 mapper tests were written after the mapper work that Task 1 needed and passed on their first run, so they record the rules rather than driving them; no mapper case failed, so no mapper fix was needed in Task 2. This is a gap against "red first" for those bullets.

## Authentication gates

None.

## Known Stubs

None. The page is deliberately the no-script slice: saving at once on change, tiles with pictures, marks, Looks right and the link disclosure belong to the plans that follow. `PictureLink` data already round-trips through the stored owner data and the overlay, so Your link becomes selectable as soon as a later plan stores a link.

## Threat Flags

None beyond the plan's register. The new `POST /owner/pictures?handler=Choose` and `Undo` posts are covered by it: antiforgery is enforced (a post without the token is answered 400, tested in JSON and form shape), the choice is parsed by name only, unknown game ids are refused, titles are Razor-encoded with `dir="auto"`, and the version test proves the caches cannot show the old picture.

## Issues Encountered

None that blocked the plan.

## Self-Check: PASSED

- Created files exist: `PictureEdits.cs`, `Pictures.cshtml`, `Pictures.cshtml.cs`, `OwnerPictureChoiceTests.cs`, `SnapshotMapperOverlayTests.cs`, `OwnerPicturesPageTests.cs`.
- Commits `8a6368c`, `8597d52`, `77fb3e0`, `a514ad2` are present in the branch history.

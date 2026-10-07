---
phase: 04-enrichment-box-images-shape
plan: 08
subsystem: sync
tags: [box-art, colour, chooser, settings, renderer, snapshot]
status: complete

requires:
  - phase: 04-enrichment-box-images-shape
    provides: "Paced picture step and art cache (02), spine colour pair and chooser (03), art analysis (04), signed-off art check outcome (05), game details with main picture address (06), thin boxes and layout version 10 (07)"
provides:
  - "Two candidate pictures per game (owned edition and main picture), each distinct address downloaded and measured once"
  - "ImageRecord stores detector features, main colour, colour pair, four edge colours and the analysis version"
  - "ArtRules (Default, Fingerprint) and startup-validated Art settings; the verdict and the chosen picture are derived at read time"
  - "SnapshotMapper.ToCabinetItems(snapshot, rules) and Version(items, rules); CollectionState.FromSnapshot(snapshot, rules)"
  - "colour on every placement except the marker and art.edges on art covers in the layout JSON"
  - "render.js colourOf: strict validation before --bg and --fg are set; edge properties dropped when a picture fails to load"
  - "Operator documentation: Choosing the picture and the spine colour"
affects: [04-09, 04-10]

actuals:
  tokens: 21500
  tasks: 3
  commits: 3

tech-stack:
  added: []
  patterns:
    - "Store raw measurements per picture; derive the verdict and the choice at read time from settings"
    - "The rules fingerprint leads the collection version, so a threshold change redraws and nothing else does"
    - "Analysis version on each stored picture; raising the constant re-measures every picture over the following syncs"

key-files:
  created:
    - Cabinet.Domain/Collection/ArtRules.cs
    - Cabinet.Service/Layout/ArtSettings.cs
    - Cabinet.IntegrationTests/ArtChoiceTests.cs
    - Cabinet.UnitTests/Collection/ArtMappingTests.cs
    - Cabinet.UnitTests/Layout/ArtSettingsTests.cs
  modified:
    - Cabinet.Domain/Collection/CollectionSnapshot.cs
    - Cabinet.Domain/Collection/SnapshotMapper.cs
    - Cabinet.Domain/Layout/CabinetItem.cs
    - Cabinet.Domain/Layout/CabinetLayout.cs
    - Cabinet.Domain/Layout/CubbyArrangement.cs
    - Cabinet.Repository/Images/ArtProcessor.cs
    - Cabinet.Repository/Storage/SnapshotStore.cs
    - Cabinet.Service/Sync/ArtSync.cs
    - Cabinet.Service/Sync/SyncRunner.cs
    - Cabinet.Service/Sync/SyncStartup.cs
    - Cabinet.Service/Sync/SyncEndpoints.cs
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - Cabinet.Service/Collection/CollectionStore.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.Service/wwwroot/js/render.js
    - build/tests/page-scripts.test.mjs
    - docs/bgg-sync.md
    - Cabinet.IntegrationTests/BoxArtTests.cs
    - Cabinet.UnitTests/Collection/SnapshotMapperArtTests.cs
    - Cabinet.UnitTests/Sync/BggSettingsTests.cs
    - Cabinet.UnitTests/Images/ArtProcessorTests.cs

key-decisions:
  - "The main picture of a game is the one its details name, else the collection's item-level picture; one shared helper (SnapshotMapper.MainPictureUrl) feeds both the sync and the mapper so they never disagree"
  - "An Ok record with no features counts as not usable at read time and is due again at sync time, so snapshots written before this plan show generated covers until the next sync measures them"
  - "A stored picture that is re-fetched because its analysis version is old and then fails is replaced by a failed record (retried after the usual wait) rather than kept half-valid"
  - "Percent settings convert with a plain division by 100, so the committed defaults equal ArtThresholds.Default exactly and the collection version does not move for default settings"

patterns-established:
  - "Images kept across collection fetches must include every address a stored game's details name, not only the collection items' own addresses"

requirements-completed: [IMG-01, CAB-03, SYNC-07]

duration: 1 session
completed: 2026-10-07
---

# Phase 4 Plan 08: Art choice and art colours Summary

**Each game's two candidate pictures are downloaded and measured once in the sync; a flat main cover replaces a slanted owned-edition shot at read time from `Art:*` settings, and every box of the game takes the chosen picture's stored colour pair, with the stored edge colours filling the bars around fitted art.**

## What was built

- Sync (D-01): `ArtSync` offers the owned edition's picture, then the main picture (details address, else the item-level picture), each distinct address once. An `Ok` record is stored with files, features, main colour (hex), `SpineColour.PairFor` pair, four edge colours and `ArtProcessor.AnalysisVersion`. A stored `Ok` record is due again when it has no features or carries another analysis version.
- Processor: `ArtProcessor.Process` runs `ArtAnalysis.Analyse` on the decoded bitmap and returns `Done(Variants, Facts)`.
- Read time (D-02, D-03, D-10 to D-12): `SnapshotMapper.ToCabinetItems(snapshot, rules)` classifies each usable record with the thresholds, chooses with `ArtChooser.Choose`, and the chosen record supplies `Art` (with edges only when all four parse) and `Colour` (only when `SpineColour.IsValidPair`). The generated cover gives neither. `Version(items, rules)` starts with the rules fingerprint and adds colour pair and edge colours per item.
- Layout: `CabinetItem.Colour` and `Placement.Colour` (JSON `colour`) are copied onto every placement kind and every stack layer; the marker gets none.
- Settings: `ArtSettings` reads the four whole-number percentages with ranges, refuses a photographed-box fill limit above the flat fill minimum naming both keys, and is registered with the layout settings so a typo stops startup. Defaults live in an `Art` section of `appsettings.json`.
- Renderer: `colourOf` accepts only a lowercase `#rrggbb` background with a `#ffffff` or `#000000` title; anything else falls back to the palette tone; the marker ignores it. A picture that fails to load becomes the generated cover with the colours kept and the four edge properties removed.
- Docs: new section "Choosing the picture and the spine colour" in `docs/bgg-sync.md` with the rules, the colour rule and the settings table.

## Decision-table rows (signed-off art check outcome)

No row changed code or defaults:

- (a) the owned edition's picture stays read from `version/item/image` (present on 84 percent of base items, 80 percent of expansions); the main picture and the item-level picture cover the rest.
- (b) the item-level picture stays the placeholder for the main picture until details arrive.
- (c) the details call needs no type parameter, so `BggThingClient` and the request-shape assertion are untouched.
- (e) `Images:AllowedHosts` keeps `cf.geekdo-images.com`.
- (g) both caps (12 MB, 36 megapixels) hold with a wide margin.
- (i) `Images:DownloadGapMilliseconds` stays 1000.

Row (b) is flagged for the owner's review round: the 11 items without a version picture were never compared against the details call's main picture, so what their item-level picture is remains inferred.

## Task commits

1. Task 1 (tracer): `229a782` feat(04-08): a slanted owned edition gives way to its flat main cover and every box takes its art colour. Tracer verify re-run end to end after the commit (build, `ArtChoiceTests`, page-script tests) and passed, so the expansion tasks went ahead per the owner's decision.
2. Task 2: `0c31c6f` test(04-08): pin the read-time picture choice, colours, edges, settings and processor facts
3. Task 3: `a269307` docs(04-08): page paints art colours safely and the picture-choice rules are documented

## Verification

- `unshare -rn` offline run of `dotnet test --solution Cabinet.slnx`: 1455 passed, 0 failed (no network).
- `node --test build/tests/page-scripts.test.mjs`: 92 passed.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all pass.
- No HTTP client was added or changed, so every test host still routes BGG and picture traffic through the scripted transports; the offline run proves it.
- Acceptance greps: `ArtChooser.Choose` once in `SnapshotMapper.cs`, `SpineColour.PairFor` once in `ArtSync.cs`, `"Art"` once in `appsettings.json`, the docs heading once and `Art:FlatMinFillPercent` present.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Pictures named only by a game's details were dropped on the next collection fetch**
- **Found during:** Task 1, reading `SyncRunner`
- **Issue:** `StillNamedImages` kept only records whose address an item names, so a main picture named only by the details would be forgotten at the start of every sync and downloaded again.
- **Fix:** it now also keeps the main picture address of every still-owned game's details.
- **Files modified:** `Cabinet.Service/Sync/SyncRunner.cs`
- **Commit:** 229a782

**2. [Rule 1 - Bug] Content comparison ignored the new record fields**
- **Issue:** `SameRecord` compared only files and status, so a re-measured picture would not have been committed.
- **Fix:** it also compares features, main colour, colour pair, edges and analysis version.
- **Commit:** 229a782

**3. [Rule 3 - Blocking] Existing tests and the test fake assumed the old shapes**
- `SnapshotMapperArtTests` records now carry features (a record without features is not usable by design); `BoxArtTests` per-run cap case asserts count and non-overlap instead of exact addresses, since each game now offers two pictures; `SyncStartup` takes the rules, so `BggSettingsTests` passes `ArtRules.Default`; the page-script fake element gained `style.removeProperty`.
- **Commit:** 229a782

**4. [Rule 2 - Missing critical] Required stored fields for the new records**
- `SnapshotStore`'s required table also lists `PaletteTone` and `ArtEdges` members, so a damaged colour record is treated as a malformed file like every other damaged record.
- **Commit:** 229a782

**5. [Rule 2 - Missing critical] A failed picture must not keep its edge bars**
- The error handler in `render.js` now removes the four edge properties as well as the art attributes, matching the contract of "no edge bars" on a failed picture.
- **Commit:** 229a782

Also added beyond the plan's list: the public helper `SnapshotMapper.MainPictureUrl`, shared by the sync and the mapper.

### TDD note
The tracer implemented the behaviour before tasks 2 and 3 wrote their tests, so those tests passed on first run (apart from one setting-range case that exposed a test-data mistake, fixed in the test). They still pin every row of the choice table, the equal-address case and the threshold flip.

## Issues and consequences for the owner

- Snapshots written before this release hold `Ok` records without features. They are unusable for the choice and due again, so after the release covers fall back to generated ones until the next syncs measure them. The first sync also downloads both candidates for every game, about double the usual number, spread over several runs by the per-run limit (default 80).
- If a stored picture is due again only because the analysis version moved and the re-download then fails, its record is replaced by a failed record and the game shows a generated cover until the retry wait (24 hours by default) has passed.
- Row (b) of the art check is inferred, not measured (see above).

## Known Stubs

None.

## Threat Flags

None. The colour and edge values reach CSS only after server validation (`SpineColour.IsValidPair`, `RgbColour.TryParseHex`) and a second strict check in the renderer; the layout JSON names no foreign host (asserted in the integration test).

## Self-Check: PASSED

- Created files present: `ArtRules.cs`, `ArtSettings.cs`, `ArtChoiceTests.cs` (integration), `ArtMappingTests.cs`, `ArtSettingsTests.cs`.
- Commits `229a782`, `0c31c6f` and `a269307` exist on the worktree branch.

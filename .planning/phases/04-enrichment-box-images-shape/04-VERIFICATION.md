---
phase: 04-enrichment-box-images-shape
verified: 2026-10-09T00:00:00Z
status: passed
score: 5/5 roadmap success criteria verified (5/5 requirement IDs satisfied)
behavior_unverified: 0
overrides_applied: 0
re_verification: false
gaps: []
warnings:
  - id: W1
    severity: major (owner-accepted, logged as todo)
    item: "Review CR-01: ImageDownloader catches only HttpRequestException and timeouts; an IOException or similar during the picture body read escapes ArtSync (no per-picture catch) and stops the whole picture step on every run"
    todo: ".planning/todos/pending/2026-10-08-picture-pipeline-robustness-from-phase-4-review.md"
  - id: W2
    severity: minor
    item: "The picture choice falls back to the main picture only when the main picture is flat; a 3D owned-version picture with an unsure or 3D main picture keeps the owned version's picture. This is narrower than the literal wording of the criterion and the requirement."
  - id: W3
    severity: minor (owner-accepted)
    item: "Known wrong picks on the real collection (rows 8, 13, 15, 44 and others) are deferred to the owner image overrides"
deferred:
  - truth: "Wrong or other-edition picks on the real collection are corrected per game"
    addressed_in: "Phase 6"
    evidence: "Phase 6 success criterion 1: the owner can override any game's image with the owned version's image, the base game's image, or a BGG gallery image; candidates are listed in .planning/todos/pending/2026-10-08-owner-image-overrides-candidates.md"
---

# Phase 4: Enrichment, Box Images & Shape Verification Report

**Phase Goal:** Every game in the real cabinet shows its real box art (or a spine coloured from that art) at true proportions, with complete BGG details behind it, all served from the site itself.
**Verified:** 2026-10-09
**Status:** passed (with three warnings, none blocking)
**Re-verification:** No, initial verification (an earlier attempt was stopped and wrote nothing)

## Automated checks run by the verifier

| Check | Command | Result |
| --- | --- | --- |
| Build | `MSBUILDDISABLENODEREUSE=1 dotnet build Cabinet.slnx` | Build succeeded, 0 Warning(s), 0 Error(s) |
| Tests, offline | `unshare -rn sh -c 'ip link set lo up; dotnet test --solution Cabinet.slnx --no-build'` | 1958 total, 1958 succeeded, 0 failed, 0 skipped (unit 1m22s, integration 12s) |
| Page scripts | `node --test build/tests/page-scripts.test.mjs` | 92 tests, 92 pass, 0 fail |
| Lint | `build/lint.sh` | PASS repo-rules, workflows, shell, secrets, script-tests |
| Hygiene | grep for `//` comments in tracked C# and for planning identifiers outside `.planning/` | none found (the only matches are the rule text in `.claude/CLAUDE.md` itself) |

`dotnet build-server shutdown` was run afterwards; no background process of this verification is left. The server was not contacted.

## Goal Achievement

### Observable Truths (ROADMAP success criteria)

| # | Truth | Status | Evidence |
| --- | --- | --- | --- |
| 1 | After a sync every game carries player count, play time, weight, designers, mechanics, minimum age and BGG rating, and every owned expansion knows its base game(s), so expansions sit beside their owned base games | VERIFIED | `BggThingParser.ReadDetails` reads min/max players, playing time, min/max play time, min age, `averageweight`, `average`, `bayesaverage`, designers, mechanics, inbound expansion links and family links; `BggThingClient` calls `thing?id=..&stats=1` in batches of at most 20. `EnrichmentSync` is called from `SyncRunner` and commits after each answer; `EnrichmentPlanner` fetches new games first, then outdated versions, then a weekly refresh. `ExpansionPairing.Pair` picks the owned base game (lowest collection id) and is carried through `SnapshotMapper` into `CabinetItem.ExpansionOf`. Integration tests (`EnrichmentTests`, `FamilyCoverTests`) and unit tests pass. Deployed: 62 of 62 games have details, 15 expansion layers on desktop (04-16-SUMMARY counts); the owner approved the deployed look. |
| 2 | Box art is downloaded during sync, downscaled and served from the site; a visitor's browser never requests an image from BGG | VERIFIED | `ArtSync` (sync-only, paced, allowlisted host, byte and pixel caps, no credentials in `ImageDownloader`) writes resized WebP via `ArtCache`; `SnapshotMapper.ArtOf` maps files to `ArtFitting.RequestPath` own-origin URLs. No `geekdo` string in service code or pages. Integration tests `BoxArtTests` and `ArtChoiceTests` assert the layout JSON contains no BGG or image-host address. CSP is `default-src 'self'`, and the local rounds record "every request on the page's own origin". Deployed: 98 of 98 picture records ok, 0 third-party picture addresses in the desktop and phone layouts, browser audit 20 of 20 including "every request on the page's own origin" (04-16-SUMMARY). |
| 3 | Each box uses the owned version's image when it is a flat cover and falls back to the base game's main image when it looks like a 3D shot, checked against the owner's real Dutch editions | VERIFIED (see W2, W3) | `ArtChooser.Choose` returns the version image when flat, the main image when the version is unusable or the version is not flat and the main is flat. `ArtVerdicts.Classify` derives the verdict from stored `ArtFeatures` (cut-out, tight crop, fill, corners); `ArtAnalysis` and `BackdropMask` measure them at analysis version 3. `SnapshotMapper.Trace` applies the choice and rules at read time. The real-edition check ran: the art check (shape-only, owner signed off), then three owner review rounds on the real collection, with the detector refitted twice from the real failures. Round 3 owner answer: "done", flat covers first kept; the final deployed look "looks right" (see Owner confirmations). Residual wrong picks are owner-accepted and deferred (W3). |
| 4 | Box proportions come from the owned version's real BGG dimensions, else a flat cover's aspect ratio, else a realistic default; a 3D shot's outline is never used, so boxes visibly differ | VERIFIED | `BoxShape.Resolve`: real dimensions (`BoxFromVersion.TryMap`, inches to mm, plausibility ranges) first, rebuilt from the cover only when they clearly contradict a flat one; no real size gives the flat cover's ratio on an estimated size (`SizeEstimate`); otherwise the estimate or `BoxFromVersion.DefaultFor`. `SnapshotMapper.Trace` passes the cover to `BoxShape` only when the chosen verdict is `Flat`, and an `Unsure` picture can only turn a box landscape, so a 3D picture never shapes or turns a box. Round 3 sheet: sizes from real size 38, cover shape 27, estimate 0, default 0 of 65. Layout pins and the density tests pass. |
| 5 | Each spine takes its colour from its box art (ignoring plain backgrounds) and shows the title legibly with readable contrast | VERIFIED | `ArtAnalysis.MainColour` uses `BackdropMask` to exclude plain and transparent backdrops; `SpineColour.PairFor` keeps the art colour when white or black text reaches 4.5:1 both unshaded and under the strongest furniture shade, else moves only OKLab lightness; `IsValidPair` guards stored pairs; `render.js` `colourOf` validates the pair client-side and sets `--bg` and `--fg`. Spine labels in `SpineLabel`; unit tests cover the contrast contract. The deployed audit passes "text at least 12 px" and tap size; the synthetic round-3 screenshot shows art-coloured spines with legible titles. |

**Score:** 5/5 truths verified.

### Plan must-haves

All 23 plans' artifact lists were checked with `verify.artifacts`. Passed: 04-01 to 04-10, 04-14, 04-16, 04-19, 04-21 (all artifacts). Five plans reported a "missing pattern" that is not a defect:

| Plan | Reported | Actual state |
| --- | --- | --- |
| 04-11 | `/fake-art/` pattern absent from `FakeBggServer.cs` | The path constant `ArtPath = "/fake-art/"` lives in `Cabinet.FakeBgg/BggXml.cs` and the server uses it |
| 04-12 | `SKTypeface.FromFile` absent from `ReviewSheet.cs` | The sheet takes typefaces by parameter; `ReviewSheetCommand` loads the DejaVu fonts or `--font` (exit code `NoFont`) |
| 04-17 | `AnalysisVersion = 2` | Superseded by plan 04-19: `ArtProcessor.AnalysisVersion = 3` |
| 04-20 | `LayoutVersion = 13` | Superseded by the series fix: `CabinetLayoutEngine.LayoutVersion = 14` |
| 04-13, 04-22 | the tool failed on a directory artifact path | The files exist (`ui-refs/round-1..3`, six synthetic screenshots each) |

Plans 04-15, 04-18 and 04-23 are owner-gated operations with no file artifacts; their outcomes are covered by the owner confirmations and the live counts below.

### Required Artifacts

| Artifact | Status | Details |
| --- | --- | --- |
| `Cabinet.Repository/Bgg/BggThingParser.cs`, `BggThingClient.cs` | VERIFIED | Substantive, wired from `EnrichmentSync` through the source factory; XML hardened (DTD prohibited, size cap) |
| `Cabinet.Domain/Collection/EnrichmentPlanner.cs`, `ExpansionPairing.cs`, `GameDetails.cs` | VERIFIED | Pure, tested, used by `EnrichmentSync` and `SnapshotMapper` |
| `Cabinet.Repository/Images/ImageDownloader.cs`, `ArtProcessor.cs`, `ArtAnalysis.cs`, `BackdropMask.cs`, `ArtCache.cs` | VERIFIED | Wired from `ArtSync`; see W1 for one missing catch |
| `Cabinet.Service/Sync/ArtSync.cs`, `EnrichmentSync.cs`, `SyncRunner.cs` | VERIFIED | Both steps run from the sync with deadlines and per-run limits |
| `Cabinet.Domain/Collection/ArtChoice.cs`, `ArtRules.cs`, `BoxFromVersion.cs`, `BoxShape.cs`, `SizeEstimate.cs`, `SnapshotMapper.cs` | VERIFIED | The read-time choice and shape pipeline, used by `CollectionStore` through `CollectionState.FromSnapshot` |
| `Cabinet.Domain/Layout/SpineColour.cs`, `SpinePalette.cs`, `SpineLabel.cs`, `ReadabilityFloor.cs`, `ArtFitting.cs` | VERIFIED | Colour pair and label rules; wired through the layout JSON into `render.js` |
| `Cabinet.Domain/Layout/CabinetLayoutEngine.cs`, `SeriesGrouping.cs`, `CubbyArrangement.cs` | VERIFIED | Layout version 14; the series fix is covered by `SeriesLayoutTests`, `SeriesInvariantTests` and `FamilyStabilityTests`, all green at the committed cover share of 33 |
| `Cabinet.FakeBgg/*` and `Cabinet.Service/Review/*` | VERIFIED | Fake image host with synthetic art; review sheet command with its tests |
| `Cabinet.Service/wwwroot/js/render.js`, `css/cabinet.css` | VERIFIED | 92 page-script tests pass |

### Key Link Verification

| From | To | Status | Details |
| --- | --- | --- | --- |
| `SyncRunner` | `EnrichmentSync`, `ArtSync` | WIRED | Both optional steps run and commit through the shared commit path |
| `ArtSync` image records | `SnapshotMapper.Trace` | WIRED | Records carry features, colour pair and edges; the mapper derives verdict and pick at read time |
| `SnapshotMapper` | `BoxShape.Resolve` | WIRED | Cover passed only for a flat verdict |
| `SnapshotMapper` | layout engine, `LayoutJson` | WIRED | `ArtImage`, `Colour`, `SeriesFamilies` flow into the layout and the collection version hash |
| layout JSON | `render.js` | WIRED | Own-origin art URLs, validated colour pair |
| `ExpansionPairing` | `CabinetItem.ExpansionOf` | WIRED | Expansions stand beside their paired base game |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
| --- | --- | --- | --- | --- |
| Layout art URLs | stored `ImageRecord.Files` | `ArtSync` downloads, `ArtCache` files | Yes (98 ok records, 188 WebP files, 5.2 MB deployed) | FLOWING |
| Spine colours | `ImageRecord.Colour` | `ArtAnalysis` of the stored picture | Yes (analysis version 3 on 98 of 98) | FLOWING |
| Box sizes | `SnapshotItem.Dimensions`, `GameDetails.EstimatedSize` | collection call with version data, details call | Yes (real size 38, cover shape 27 of 65) | FLOWING |
| Expansion pairing | `GameDetails.ExpandsGames` | details call inbound links | Yes (62 of 62 games with details) | FLOWING |

### Behavioral Spot-Checks

The offline suite of 1958 tests was run once in full; the named behaviours (`EnrichmentTests`, `BoxArtTests`, `ArtChoiceTests`, `SeriesTests`, `FamilyCoverTests`, and the layout and detector unit tests) are part of that passing run. No server was started and no state was changed.

### Probe Execution

SKIPPED: the phase declares no `probe-*.sh` scripts.

### Requirements Coverage

Every ID in the five plan-level lists was cross-referenced with `REQUIREMENTS.md`. No requirement mapped to Phase 4 is orphaned.

| Requirement | Source plans | Description | Status | Evidence |
| --- | --- | --- | --- | --- |
| SYNC-06 | 04-01, 04-05, 04-06, 04-14, 04-16, 04-20, 04-23 | Details, ratings and expansion-to-base links from BGG | SATISFIED | Truth 1; 62 of 62 games enriched on the server, details version 1, family links stored |
| SYNC-07 | 04-01, 04-02, 04-05, 04-08, 04-11, 04-13, 04-14, 04-16, 04-18, 04-23 | Box art downloaded, downscaled, served from the site; browsers never load BGG images | SATISFIED | Truth 2 (see W1 for a latent robustness defect) |
| IMG-01 | 04-01, 04-03, 04-04, 04-05, 04-08, 04-11, 04-12, 04-13, 04-15, 04-16, 04-17, 04-18, 04-19, 04-23 | Owned version's image when flat, else the main image | SATISFIED | Truth 3; see W2 and W3 |
| IMG-03 | 04-07, 04-09, 04-10, 04-12, 04-13, 04-15, 04-16, 04-17, 04-18 | Box proportions from real size, then cover ratio, then default; 3D outline never used | SATISFIED | Truth 4 |
| CAB-03 | 04-03, 04-04, 04-07, 04-08, 04-12, 04-13, 04-15, 04-16, 04-17, 04-19 | Spine title legible, colour from box art, plain backgrounds ignored | SATISFIED | Truth 5 |

Plans in this phase also list EXP-01, EXP-03, CAB-04, CAB-05 and CAB-07 (04-20 to 04-22). These are already marked complete under Phase 2 in `REQUIREMENTS.md`; the phase kept them intact (layout, series and density tests pass).

Bookkeeping still to do at close-out (not a defect of the code): the checkboxes of SYNC-06, SYNC-07, IMG-01, IMG-03 and CAB-03 in `REQUIREMENTS.md` are still unticked and the traceability table still says Pending; the roadmap phase 4 entry is still unticked.

### Anti-Patterns Found

| File | Pattern | Severity | Impact |
| --- | --- | --- | --- |
| `Cabinet.Repository/Images/ImageDownloader.cs` | Body read catches only `HttpRequestException` and timeouts; no deadline on the body read | Warning (W1) | A mid-download failure stops the picture step on every run and starves later pictures. Logged as a todo by the owner. |
| `Cabinet.Domain/Collection/BoxShape.cs` | `FromCover` has no plausibility check | Info | An extreme cover ratio could give a degenerate box; logged (review WR-02) |
| `Cabinet.Domain/Collection/SnapshotMapper.cs` | `Explain` keeps the first entry sharing a collection id, so the result depends on source order | Info | Logged (review WR-03); does not affect the real collection |
| `Cabinet.Domain/Collection/EnrichmentPlanner.cs` | A game the source never describes is requested again in every run | Info | Logged (review WR-04) |
| Debt markers (`TBD`, `FIXME`, `XXX`) | none found in files changed by this phase (grep of tracked source) | none | |

The other review findings (13 warnings and 17 info in all, in `04-REVIEW.md`) are logged as four todos dated 2026-10-08 by the owner's choice; none contradicts a success criterion on the deployed data.

### Owner confirmations (recorded, not re-asked)

| Item | Confirmed | Source |
| --- | --- | --- |
| Review of the real collection, rounds 1 to 3 | Round 3 answer "done": cover share 33, families face out from 2 expansions, flat covers first, no Art overrides | 04-15-SUMMARY; owner chat via the orchestrator |
| Final deployed cabinet on v0.5.1 | "looks right", 2026-10-08. This covers the desktop arrangement change after the series fix that 04-16-SUMMARY flagged as not yet seen | `.continue-here.md`; owner chat via the orchestrator |
| Picture choice | "Flat covers first"; other-edition rows logged for the owner image overrides | `.planning/todos/pending/2026-10-08-owner-image-overrides-candidates.md` |
| Phone's single-box last section | Accepted | 04-15-SUMMARY |
| A family inside a series may keep its "+N more" marker | Accepted | 04-16-SUMMARY |
| Code review findings (1 critical, 13 warnings) | Logged as todos instead of fixed in this phase | `.planning/todos/pending/2026-10-08-*.md` |
| Live server state | v0.5.1 healthy, review drop-in removed, 98 of 98 pictures ok, 62 of 62 games with details, nothing waiting, 0 BGG image-host links in layouts, browser audit 20 of 20, self-check 21 of 21 | 04-16-SUMMARY (not re-read from the server by this verification) |

Real-collection screenshots were deliberately never committed (privacy rule), so the real-collection look is evidenced only by the summaries and the owner's statements. The committed `ui-refs/round-1..3` screenshots are synthetic and were spot-checked (round 3 desktop shows art-coloured spines, flat covers, apron and legible titles).

### Human Verification Required

None. Every item that needs the owner was answered by the owner (above). No behaviour-dependent truth in this phase is left without test or owner evidence.

## Warnings

- **W1 (major, owner-accepted):** the critical review finding CR-01 is a real, current defect in `ImageDownloader.ReadAnswerAsync` (no `IOException` catch, no body deadline) combined with the lack of a per-picture catch in `ArtSync`. It does not break the goal on the deployed data (98 of 98 pictures ok) but a single bad download can stop the picture step on every run. It is logged as a major todo and "ships in the next release". Recommendation: schedule it before the go-public work, because a public site with a changing collection will meet it eventually.
- **W2 (minor):** the fallback in `ArtChooser` requires the main picture to be flat. If the owned version's picture is a 3D shot and the main picture is unsure or 3D, the 3D shot is kept as the face-out picture (its outline is still never used for shape). Roadmap criterion 3 and IMG-01 say the main image is used when the version image looks 3D. The owner reviewed and kept the rule ("flat covers first"), so this is recorded rather than failed.
- **W3 (minor, owner-accepted):** known wrong picks on the real collection (rows 8, 13, 15 and the other-edition main picture of row 44, plus the 23 rows that show the main picture while an owned-version picture exists) are listed for the owner image overrides in a later phase (deferred, see frontmatter).

## Gaps Summary

No gaps. All five success criteria and all five requirement IDs are met in the code, the automated checks are green (build 0 errors, 1958 of 1958 tests, 92 of 92 page-script tests, lint all PASS), and the owner confirmed the deployed result. Three non-blocking warnings are recorded above.

---

_Verified: 2026-10-09_
_Verifier: Claude (gsd-verifier)_

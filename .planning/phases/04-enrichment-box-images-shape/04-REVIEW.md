---
phase: 04-enrichment-box-images-shape
reviewed: 2026-10-08T00:00:00Z
depth: standard (tests at quick)
files_reviewed: 152
status: issues_found
findings:
  critical: 1
  warning: 13
  info: 17
  total: 31
parts:
  - 04-REVIEW-part-domain.md (26 files, 0 critical, 4 warning, 6 info)
  - 04-REVIEW-part-services.md (49 files, 1 critical, 4 warning, 6 info)
  - 04-REVIEW-part-tests.md (77 files, 0 critical, 5 warning, 5 info)
files_reviewed_list:
  - Cabinet.Domain/Collection/ArtChoice.cs
  - Cabinet.Domain/Collection/ArtRules.cs
  - Cabinet.Domain/Collection/BoxFromVersion.cs
  - Cabinet.Domain/Collection/BoxShape.cs
  - Cabinet.Domain/Collection/CollectionSnapshot.cs
  - Cabinet.Domain/Collection/EnrichmentPlanner.cs
  - Cabinet.Domain/Collection/ExpansionPairing.cs
  - Cabinet.Domain/Collection/GameDetails.cs
  - Cabinet.Domain/Collection/SizeEstimate.cs
  - Cabinet.Domain/Collection/SnapshotMapper.cs
  - Cabinet.Domain/Layout/ArtFitting.cs
  - Cabinet.Domain/Layout/CabinetItem.cs
  - Cabinet.Domain/Layout/CabinetLayout.cs
  - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
  - Cabinet.Domain/Layout/CubbyArrangement.cs
  - Cabinet.Domain/Layout/LayoutMember.cs
  - Cabinet.Domain/Layout/LayoutOptions.cs
  - Cabinet.Domain/Layout/Oklab.cs
  - Cabinet.Domain/Layout/Orientation.cs
  - Cabinet.Domain/Layout/ReadabilityFloor.cs
  - Cabinet.Domain/Layout/RgbColour.cs
  - Cabinet.Domain/Layout/SectionDesign.cs
  - Cabinet.Domain/Layout/SectionDesigns.cs
  - Cabinet.Domain/Layout/SeriesGrouping.cs
  - Cabinet.Domain/Layout/SpineColour.cs
  - Cabinet.Domain/Samples/SyntheticCollections.cs
  - Cabinet.FakeBgg/BggXml.cs
  - Cabinet.FakeBgg/Cabinet.FakeBgg.csproj
  - Cabinet.FakeBgg/FakeBggProgram.cs
  - Cabinet.FakeBgg/FakeBggServer.cs
  - Cabinet.FakeBgg/SyntheticArt.cs
  - Cabinet.FakeBgg/SyntheticBggCollection.cs
  - Cabinet.IntegrationTests/ArtChoiceTests.cs
  - Cabinet.IntegrationTests/BackgroundSyncTests.cs
  - Cabinet.IntegrationTests/BoxArtTests.cs
  - Cabinet.IntegrationTests/CollectionFidelityTests.cs
  - Cabinet.IntegrationTests/EnrichmentTests.cs
  - Cabinet.IntegrationTests/FakeBggServerTests.cs
  - Cabinet.IntegrationTests/FamilyCoverTests.cs
  - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
  - Cabinet.IntegrationTests/Infrastructure/NoWaitPacer.cs
  - Cabinet.IntegrationTests/Infrastructure/RealNetworkGuard.cs
  - Cabinet.IntegrationTests/Infrastructure/RecordedRequestKinds.cs
  - Cabinet.IntegrationTests/Infrastructure/ScriptedImageHandler.cs
  - Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs
  - Cabinet.IntegrationTests/Infrastructure/SyncRounds.cs
  - Cabinet.IntegrationTests/LayoutEndpointTests.cs
  - Cabinet.IntegrationTests/LiveCapRefusalTests.cs
  - Cabinet.IntegrationTests/LocalArtTests.cs
  - Cabinet.IntegrationTests/OversizedAnswerTests.cs
  - Cabinet.IntegrationTests/RealNetworkGuardTests.cs
  - Cabinet.IntegrationTests/SecretsStayServerSideTests.cs
  - Cabinet.IntegrationTests/SeriesTests.cs
  - Cabinet.IntegrationTests/SyncFailureTests.cs
  - Cabinet.IntegrationTests/SyncNowTests.cs
  - Cabinet.IntegrationTests/SyncPipelineTests.cs
  - Cabinet.IntegrationTests/TrueProportionsTests.cs
  - Cabinet.Repository/Bgg/BggCollectionParser.cs
  - Cabinet.Repository/Bgg/BggThingClient.cs
  - Cabinet.Repository/Bgg/BggThingParser.cs
  - Cabinet.Repository/Bgg/RequestPacer.cs
  - Cabinet.Repository/Images/ArtAnalysis.cs
  - Cabinet.Repository/Images/ArtCache.cs
  - Cabinet.Repository/Images/ArtProcessor.cs
  - Cabinet.Repository/Images/ArtUrl.cs
  - Cabinet.Repository/Images/BackdropMask.cs
  - Cabinet.Repository/Images/ImageDownloader.cs
  - Cabinet.Repository/Images/SubjectShape.cs
  - Cabinet.Repository/Storage/SnapshotStore.cs
  - Cabinet.Service/Collection/ArtFiles.cs
  - Cabinet.Service/Collection/CollectionStore.cs
  - Cabinet.Service/Layout/ArtSettings.cs
  - Cabinet.Service/Layout/LayoutEndpoint.cs
  - Cabinet.Service/Layout/LayoutSettings.cs
  - Cabinet.Service/Program.cs
  - Cabinet.Service/Review/ReviewSheet.cs
  - Cabinet.Service/Review/ReviewSheetCommand.cs
  - Cabinet.Service/Review/ReviewSheetModel.cs
  - Cabinet.Service/Sync/ArtSync.cs
  - Cabinet.Service/Sync/EnrichmentSettings.cs
  - Cabinet.Service/Sync/EnrichmentSync.cs
  - Cabinet.Service/Sync/ImageSettings.cs
  - Cabinet.Service/Sync/SyncEndpoints.cs
  - Cabinet.Service/Sync/SyncRunner.cs
  - Cabinet.Service/Sync/SyncStartup.cs
  - Cabinet.Service/appsettings.json
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Service/wwwroot/js/copy.js
  - Cabinet.Service/wwwroot/js/render.js
  - Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs
  - Cabinet.UnitTests/Bgg/BggThingClientTests.cs
  - Cabinet.UnitTests/Bgg/BggThingParserTests.cs
  - Cabinet.UnitTests/Bgg/RequestPacerTests.cs
  - Cabinet.UnitTests/Collection/ArtChoiceTests.cs
  - Cabinet.UnitTests/Collection/ArtMappingTests.cs
  - Cabinet.UnitTests/Collection/BoxFromVersionTests.cs
  - Cabinet.UnitTests/Collection/BoxShapeTests.cs
  - Cabinet.UnitTests/Collection/EnrichmentPlannerTests.cs
  - Cabinet.UnitTests/Collection/ExpansionPairingTests.cs
  - Cabinet.UnitTests/Collection/SizeEstimateTests.cs
  - Cabinet.UnitTests/Collection/SnapshotMapperArtTests.cs
  - Cabinet.UnitTests/Collection/SnapshotMapperTests.cs
  - Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs
  - Cabinet.UnitTests/Configuration/CommittedLogLevelTests.cs
  - Cabinet.UnitTests/FakeBgg/BggXmlTests.cs
  - Cabinet.UnitTests/Images/ArtAnalysisTests.cs
  - Cabinet.UnitTests/Images/ArtCacheTests.cs
  - Cabinet.UnitTests/Images/ArtProcessorTests.cs
  - Cabinet.UnitTests/Images/ArtUrlTests.cs
  - Cabinet.UnitTests/Images/ImageDownloaderTests.cs
  - Cabinet.UnitTests/Images/ReviewCaseVerdictTests.cs
  - Cabinet.UnitTests/Images/SyntheticArtTests.cs
  - Cabinet.UnitTests/Layout/ArtFittingTests.cs
  - Cabinet.UnitTests/Layout/ArtSettingsTests.cs
  - Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs
  - Cabinet.UnitTests/Layout/DensityTests.cs
  - Cabinet.UnitTests/Layout/DesktopDensityTests.cs
  - Cabinet.UnitTests/Layout/FakeCollectionItems.cs
  - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
  - Cabinet.UnitTests/Layout/FamilyStabilityTests.cs
  - Cabinet.UnitTests/Layout/LayoutAssertions.cs
  - Cabinet.UnitTests/Layout/LayoutDensity.cs
  - Cabinet.UnitTests/Layout/LayoutSettingsTests.cs
  - Cabinet.UnitTests/Layout/OrientationTests.cs
  - Cabinet.UnitTests/Layout/PhoneProfileTests.cs
  - Cabinet.UnitTests/Layout/PoseStabilityTests.cs
  - Cabinet.UnitTests/Layout/SectionDesignTests.cs
  - Cabinet.UnitTests/Layout/SeriesInvariantTests.cs
  - Cabinet.UnitTests/Layout/SeriesLayoutTests.cs
  - Cabinet.UnitTests/Layout/ShelfMixTests.cs
  - Cabinet.UnitTests/Layout/SpineColourTests.cs
  - Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs
  - Cabinet.UnitTests/Layout/ThinBoxTests.cs
  - Cabinet.UnitTests/Review/ReviewFixture.cs
  - Cabinet.UnitTests/Review/ReviewSheetCommandTests.cs
  - Cabinet.UnitTests/Review/ReviewSheetModelTests.cs
  - Cabinet.UnitTests/Review/ReviewSheetTests.cs
  - Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs
  - Cabinet.UnitTests/Sync/BggSettingsTests.cs
  - Cabinet.UnitTests/Sync/EnrichmentSettingsTests.cs
  - Cabinet.UnitTests/Sync/ImageSettingsTests.cs
  - README.md
  - build/bgg-access-check.py
  - build/tests/bgg-access-check-test.sh
  - build/tests/page-scripts.test.mjs
  - deploy/provision.d/10-packages.sh
  - deploy/tests/provision-logic-test.sh
  - docs/bgg-access-check.md
  - docs/bgg-sync.md
  - docs/cabinet-layout.md
  - docs/development.md
  - docs/review-sheet.md
---

# Phase 4 code review

The phase changed 152 files outside `.planning/`. The review was split into three parallel reviewers by area; this file merges their reports.

## Overview

- **Critical (1):** services CR-01. A picture body-read failure (for example a connection drop mid-download) escapes `ImageDownloader`, aborts the rest of that run's picture step and leaves the failing picture first in line on every later run.
- **Warnings (13):**
  - domain: a continuing family can draw up to twice the stack cap; a flat cover with an extreme aspect gives an implausible box; `SnapshotMapper.Explain` depends on source order; a game the source never describes is re-requested every run.
  - services: no deadline on the picture body read; review-sheet 3D counts omit the marked kinds; long verdicts overrun their column; the review-sheet command can rename a bad snapshot.
  - tests: loosened phone-versus-desktop and family checks; the network-guard tests use client names; real-clock timing; the re-measure test lacks a control.
- **Security surface:** held up (token scope, download allowlist and caps, `/art` path safety, XML hardening, CSP-safe page scripts, access-check output guard).
- **Hard rules:** no `//` comments, planning references or personal data in any reviewed file.

# Part: Domain and layout

(The finding IDs below are scoped to this part, for example the domain WR-01 and the services WR-01 are different findings.)
## Phase 4: Code Review Report (domain part)

**Reviewed:** 2026-10-08
**Depth:** standard (plus a throwaway seeded fuzz harness kept outside the repository)
**Files Reviewed:** 26
**Status:** issues_found

## Summary

The domain code is in good shape. Beyond reading every file, I compiled the domain sources into a scratch console project
(outside the repo) and fuzzed the engine, the colour code and the box shaping with random collections, random layout
options and hostile boxes. What held up:

- No exceptions, no out-of-bounds placement and no overlaps in about 1,000 layouts across both designs. Boxes with zero,
  negative or `int.MaxValue` sizes, duplicate game ids and blank titles are all handled.
- Every owned base game is placed exactly once. For every owned expansion, layers plus marker counts add up exactly.
- The layout is independent of input order, since shuffled input gives byte-identical JSON.
- A family never ends more than one cubby from its base game, and the neighbour is always on the same shelf row.
- Every series gap I found (62 of 2,418 series) is also a gap when that series is laid out alone in an empty cabinet. That
  matches the documented "forced gap" exception.
- `SpineColour.PairFor` returned a valid pair for 300,000 random colours and never needed the black-on-white fallback.
- The `TitleKey` helper is culture independent (checked under tr-TR).
- The hard rules hold: no `//` comments, no planning references, no personal data, and the sample titles are synthetic.

The defects below are therefore about contract drift and edge cases, not crashes.

## Warnings

### WR-01: Continuing a family into the next cubby breaks `Layout:ExpansionStackMax`

**File:** `Cabinet.Domain/Layout/CubbyArrangement.cs:309-364` (also `CabinetLayoutEngine.cs:476-503`)
**Issue:** `SplitStack` and `PlaceContinuedColumn` each run `StackLayout.Layout(..., options.ExpansionStackMax)` on their own,
so each column may show up to `ExpansionStackMax` layers. The setting is documented as "the most expansions drawn in one
stack" (`docs/cabinet-layout.md:135`), but a family that continues next door can draw up to twice that. In my fuzz of
`SyntheticCollections.Random` with random options, 223 families showed more layers than the cap, across 500 collections on both
designs. Examples: cap 1 showed 2 layers, cap 3 showed 6, and cap 6 showed 9 and 10.

There is a second effect. When the cap, not the shelf height, is what hides expansions, `SplitStack` still reports a split.
For example, with 9 expansions, cap 6 and a shelf that fits 8 layers, the family spends the whole next cubby to show the
3 expansions the cap was meant to hide. The cap is defeated and a neighbouring cubby is consumed for no reason.

**Fix:** carry a layer budget into the second column and refuse to split when the cap is the limit.
```csharp
// SplitStack
var own = StackLayout.Layout(heights, cubby.HeightMm, 0, options.ExpansionStackMax).Visible;
return own == 0 || own >= options.ExpansionStackMax ? null : (own, [.. family.Expansions.Skip(own)]);

// LayoutMember: add `public int? StackBudget { get; init; }`, set by ColumnFor to ExpansionStackMax - own.
// PlaceContinuedColumn and PlaceStack then pass Math.Min(options.ExpansionStackMax, member.StackBudget ?? int.MaxValue)
// to StackLayout.Layout, so the marker still counts everything left over.
```
This changes layouts, so raise `LayoutVersion` and re-record the golden layouts. Otherwise reword the setting in the docs
to "per column".

### WR-02: A flat cover with an extreme aspect ratio produces a degenerate box

**File:** `Cabinet.Domain/Collection/BoxShape.cs:125-132` (also `:80-83`)
**Issue:** `Rebuilt` rejects results whose front sides fall outside 50..700 mm via `IsPlausible`. `FromCover`, the path
taken when there are no real dimensions, has no such guard. `shorter = round(longer * ratio)` is never checked. A 30x1701
flat picture gives a 5x300 box. A 2587x73 picture gives 260x7. Zero widths also occur. I ran 200,000 random inputs through
`BoxShape.Resolve`: 1,815 boxes had a side under 10 mm and 75 had a side of 0 or less.

The engine's `Clamp` raises the sides to 10 mm, so nothing crashes. The `CabinetItem` and the collection version still carry
the absurd size, and the cabinet draws a 10 mm-wide "cover". A banner-shaped picture (an in-box ad strip or a wide
promo shot) judged "flat" would hit this.

**Fix:** guard `FromCover` the same way as `Rebuilt` and fall back to the estimate's own shape when the result is implausible.
```csharp
var front = Oriented(shorter, longer, cover, rules);
return IsPlausible(front.WidthMm) && IsPlausible(front.HeightMm)
    ? new BoxDimensions(front.WidthMm, front.HeightMm, estimate.DepthMm)
    : estimate;
```
Make `Resolve` report `BoxSource.Estimate` or `Default` in that case, not `CoverShape`.

### WR-03: The "same cabinet in any source order" guarantee fails for entries that share a collection id

**File:** `Cabinet.Domain/Collection/SnapshotMapper.cs:74-79`
**Issue:** `Explain` groups by `CollectionId` and keeps `entry.FirstOrDefault(Expansion) ?? entry.First()`. When one entry
id appears with two different games, or twice as an expansion, the survivor is whichever item the source listed first.
That contradicts the doc on line 35, "the same collection in any source order gives the same cabinet". The following
`.ThenBy(item => item.GameId)` is dead, because after grouping each `CollectionId` occurs only once.

BGG entry ids are unique, so this needs odd input. But the code and the docs both claim order independence, and the rule
is cheap to make true.

**Fix:**
```csharp
.Select(entry => entry
    .OrderByDescending(item => item.Kind == ItemKind.Expansion)
    .ThenBy(item => item.GameId)
    .First())
.OrderBy(item => item.CollectionId)
```
Drop the redundant `ThenBy`.

### WR-04: Games the source never describes are requested again in every run

**File:** `Cabinet.Domain/Collection/EnrichmentPlanner.cs:49-62` (stored by `Cabinet.Service/Sync/EnrichmentSync.cs:75-86`)
**Issue:** An id with no stored `GameDetails` counts as "fresh". `EnrichmentSync` only stores ids that come back in the
answer. A game BGG omits from a `thing` answer (a removed or merged entry, say) therefore stays "fresh" forever. It is
re-requested at the front of every hourly plan and permanently takes a slot in the first batch of up to 20 ids. Games
whose details are outdated are handled the same way, because they never become current. This repeats a BGG call every
run for as long as the collection holds such a game, which cuts against the "cache aggressively" etiquette. It also
quietly shrinks the useful size of the first batch.

**Fix:** remember the attempt. Either store a minimal `GameDetails` for undescribed ids with `EnrichedAtUtc = now` and the
current `DetailsVersion`, so the normal weekly refresh cadence applies, or give the planner a "last tried" map and treat
an id tried within `RefreshAfter` as not due. Cover it with a planner test for "id absent from the answer is not requested
again within `RefreshAfter`".

## Info

### IN-01: Public members used only by tests, and a stored field nothing reads

**File:** `Cabinet.Domain/Collection/SnapshotMapper.cs:229`, `BoxFromVersion.cs:32`, `Layout/SpineColour.cs:103`, `Layout/SeriesGrouping.cs:19`, `Collection/ArtChoice.cs:16`
**Issue:**
- `SnapshotMapper.DefaultBox` only forwards to `BoxFromVersion.DefaultFor` and is called only by tests.
- `BoxFromVersion.Map` is called only by tests (production uses `TryMap`).
- `SpineColour.LightnessOf` is called only by tests.
- `SeriesGrouping.SeriesFamilyPrefixes` is public but has no outside user.
- `ArtFeatures.SidesTouched` is measured and stored but read by neither `Classify` nor `Score`, so it is dead data.

**Fix:** remove them, or mark the helpers internal. For `SidesTouched`, either use it or stop persisting it. Changing
the stored shape needs a schema decision, so at least note it as "reserved".

### IN-02: `ArtRules.Fingerprint` formats integers with the current culture

**File:** `Cabinet.Domain/Collection/ArtRules.cs:27`
**Issue:** The interpolated `{ShapeMarginPercent}` and `{UnsureLandscapeMarginPercent}` use the current culture, while every
other fingerprint and version string in the domain is explicitly invariant. Positive integers render the same everywhere
today, and the service range-checks both values to positive numbers. The risk is only consistency, if a negative value is
ever allowed.

**Fix:** `string.Create(CultureInfo.InvariantCulture, $"{Thresholds.Fingerprint}#{ShapeMarginPercent}#{OrientFromCover}#{UnsureLandscapeMarginPercent}")`.

### IN-03: Title keys link two copies of one game, the family rule does not

**File:** `Cabinet.Domain/Layout/SeriesGrouping.cs:76-83`
**Issue:** A shared series family only counts when at least two distinct games carry it (line 76). A shared title key counts
for any two items (line 81), so two copies of one game, with different entry ids, become a "series" of two. That is
probably harmless, since the copies stand together, but it is inconsistent with the family rule and with the doc on lines
9-14.

Related: two copies with a blank title are not joined, yet they share the same `BggId` and so the same `SeriesAnchor`.
`CubbyArrangement.Order` then merges their blocks (`GroupBy(SeriesAnchor)`).

**Fix:** decide which behaviour is intended. Either apply `Distinct().Count() >= 2` on `BggId` to title carriers too, or
document that duplicate copies stand together by design.

### IN-04: `SectionDesign.Validate` does not check `MaxSpineHeightMm`

**File:** `Cabinet.Domain/Layout/SectionDesign.cs:177-208`
**Issue:** `MaxSpineHeightMm` is not in the positive-size list. A design with 0 would make `CoverStrategy.OversizeOnly` face
every box out without any complaint. The shipped designs are fine.

**Fix:** add `("maximum spine height", MaxSpineHeightMm)` to the `sizes` array.

### IN-05: Sample names and sizes are written out twice

**File:** `Cabinet.Domain/Samples/SyntheticCollections.cs:26-27,58-66,120`
**Issue:** `ReviewSampleName` and `LargeSampleName` exist as constants, but `SampleSizes` still uses the literals `"400"`
and `[ReviewSampleName] = 65`, and `SampleNames` repeats all of them as literals. Adding or renaming a sample means touching
three places, and a mismatch would silently drop a name from the visitor-facing list.

**Fix:** build `SampleNames` from `SampleSizes.Keys` plus `EdgeSampleName`, and use the constants as dictionary keys.

### IN-06: `BoxShape` summary contradicts itself

**File:** `Cabinet.Domain/Collection/BoxShape.cs:27-35`
**Issue:** The type summary says an unsure picture "never shapes anything" and that "a photographed box never shapes or
turns a box". Four sentences later it says an unsure picture "only turns one when it is clearly landscape". The code is
right and the prose is repetitive and easy to misread.

**Fix:** collapse the summary to one statement per picture type: flat covers shape and turn, unsure pictures only turn
when clearly landscape, 3D shots do nothing.

---

_Reviewed: 2026-10-08_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

# Part: Services, pipeline, scripts and docs

(The finding IDs below are scoped to this part, for example the domain WR-01 and the services WR-01 are different findings.)
## Phase 4: Code Review Report (services, pipeline and ops part)

**Reviewed:** 2026-10-08
**Depth:** standard
**Files Reviewed:** 49
**Status:** issues_found

## Summary

The security-critical surface holds up. The token is attached only by `BggAuthHandler`, and that handler is wired only
onto the collection and details clients. The picture client uses a separate handler chain with no auth handler,
`AllowAutoRedirect = false` and a per-hop policy check (https, default port, no user info, exact host). `/art` is served
through a `PhysicalFileProvider` rooted at the art directory, with `.webp` as the only mapped type. The BGG thing parser
prohibits DTDs, removes the resolver and caps the document size. `render.js` builds everything with `textContent` and
`style.setProperty`, validates the picture path, fit, sizes and colours, and never uses `innerHTML`. The access-check
script only talks to `boardgamegeek.com` and the one known image host, and guards its report before printing it. No
planning references, `//` comments or personal data were found in the listed files.

The failure-isolation story for the picture step, which the code and docs both promise, is not complete. One
unhandled exception type from the download path aborts the whole picture step and repeats on every run. The review-sheet
command also has a counting bug and a layout bug, and it can mutate live state.

## Critical Issues

### CR-01: A body-read failure from one picture aborts the whole picture step on every run (not isolated)

**File:** `Cabinet.Repository/Images/ImageDownloader.cs:163-170,192-211` and `Cabinet.Service/Sync/ArtSync.cs:80,159`
**Issue:** `FetchOnceAsync` catches only `HttpRequestException` and a non-requested `OperationCanceledException`. The body
is streamed with `HttpCompletionOption.ResponseHeadersRead`, so a connection reset, a truncated body or a bad
`Content-Encoding` surfaces during `body.ReadAsync` as `IOException` (in .NET 7 and later `HttpIOException` derives from
`IOException`, not from `HttpRequestException`). A broken gzip stream surfaces as `InvalidDataException`. A malformed
`Location` surfaces as `UriFormatException` at line 157. None of these is caught in the downloader, and `ArtSync.FetchAsync`
has no catch either. The exception reaches the generic catch at `SyncRunner.cs:270`, which logs "Box pictures stopped
early" and ends the step.

Consequences:
1. No `ImageRecord` is written for the offending address, so it is still "due".
2. Pictures are walked in a deterministic order (`CollectionId`, then `GameId`). The same picture is therefore tried first
   on every run, fails the same way and blocks every picture after it. A single flaky file permanently starves the rest of
   the collection of art.
3. Up to nine fetched and stored pictures since the last commit are lost (files were written to disk, records were not
   committed), and `Prune` is skipped.

This contradicts the stated contract in `ArtSync` and `SyncRunner` ("A picture that goes wrong is only recorded; it never
fails the run") and in `docs/bgg-sync.md`.

**Fix:**
```csharp
catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or UriFormatException)
{
    return (ArtDownload.Failed("unavailable"), null);
}
```
Also wrap the per-picture body of the loop in `ArtSync.RunAsync` so any non-cancellation exception from
`source.DownloadAsync` or `ArtProcessor.Process` records an `ImageStatus.Failed` record for that address and continues:
```csharp
try { images[address] = await FetchAsync(source, address, counts, cancellationToken); }
catch (Exception exception) when (exception is not OperationCanceledException)
{
    counts.Failed++;
    images[address] = new ImageRecord(address, ImageStatus.Failed, time.GetUtcNow());
}
```

## Warnings

### WR-01: A slow picture body has no deadline; the 10-minute run limit then fails the whole run and drops uncommitted records

**File:** `Cabinet.Repository/Images/ImageDownloader.cs:152,192-211`, `Cabinet.Service/Sync/SyncEndpoints.cs:93`, `Cabinet.Service/Sync/SyncWorker.cs:48-58`
**Issue:** `HttpClient.Timeout` (60 s) only bounds the time to the response headers when `ResponseHeadersRead` is used. The
body loop is bounded only by the caller's token. A host that sends headers and then dribbles bytes (or stalls) holds the
picture pacer lease and the run until `SyncWorker.RunLimit` (10 minutes) cancels it. The six-minute `ExtrasDeadline`
only gates the start of a download, so it does not help. When the limit fires, `OperationCanceledException` propagates
out of `FetchPicturesAsync` (its catch excludes cancellation), and `RunOnceAsync` reports `SyncResult.Failed` with
`SyncFailure.Timeout`. This happens even though the collection was already stored and shown, and it loses the up-to-nine
uncommitted picture records. `ArtSync`'s doc comment ("a slow host cannot hold the run") and `docs/bgg-sync.md` ("a slow
host cannot hold a sync back") are therefore not true for slow bodies.
**Fix:** Give each download its own deadline that covers the body read:
```csharp
using var perFile = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
perFile.CancelAfter(DownloadTimeout);
```
Pass `perFile.Token` to `SendAsync` and `ReadAsync`, and map a `perFile`-only cancellation to `ArtDownload.Failed("timeout")`.
In `ArtSync.RunAsync`, commit pending records in a `finally` so a cancelled run keeps what it fetched.

### WR-02: The review-sheet summary line undercounts 3D verdicts

**File:** `Cabinet.Service/Review/ReviewSheetCommand.cs:250-255` (with `ReviewSheetModel.cs:377-384`)
**Issue:** `Count(rows, "3D shot")` compares with `==`, but `VerdictWord` also produces `"3D shot, cut-out"` and `"3D shot,
tight crop"`. Those rows are counted in none of the four buckets (flat, 3D shot, unsure, no verdict), so the printed
counts do not add up to the number of games. `docs/review-sheet.md` promises "how many verdicts ... of each kind".
**Fix:** Count by prefix for the 3D bucket, for example
`rows.Count(row => row.Verdict.StartsWith("3D shot", StringComparison.Ordinal))`, or expose the verdict as an enum on
`ReviewRow` and count that.

### WR-03: Long verdict text overruns the next column on the review sheet

**File:** `Cabinet.Service/Review/ReviewSheet.cs:313,43-44`
**Issue:** The verdict is drawn unclipped in a 150 px column in 17 px bold. `"3D shot, tight crop"` and `"3D shot,
cut-out"` measure well over 150 px, so they are drawn across the start of the "Chosen" column, and the chosen text is
painted over them. These are the two verdicts the review most needs to read.
**Fix:** Wrap the verdict onto two lines (split at the comma) or widen `VerdictWidth` and shrink another column. A clip
rectangle with `Ellipsised(...)` would also be acceptable.

### WR-04: The "read-only" review-sheet command can rename the live collection file

**File:** `Cabinet.Service/Review/ReviewSheetCommand.cs:82`, `Cabinet.Repository/Storage/SnapshotStore.cs:93-116`
**Issue:** The operator command calls `SnapshotStore.Load()`, which moves `snapshot.json` to `snapshot.json.bad` when the
file fails to parse, is malformed or has a newer schema than the running binary. Running the command against the live
state directory (as the docs instruct, as the service user) can therefore set aside the production collection, for
example when the installed binary is older than the one that wrote the file, or `--state` points at a live directory
during a deploy window. A command documented as reading the collection should not change it.
**Fix:** Add a non-mutating load for read-only callers (a `Load(setAside: false)` overload, or a separate `Peek()`), and
use it here. At minimum, document the side effect in `docs/review-sheet.md`.

## Info

### IN-01: The decode is not actually scaled for formats without scaled decoding, so a 36-megapixel cap means a ~144 MB transient buffer

**File:** `Cabinet.Repository/Images/ArtProcessor.cs:42-45,98-109`
**Issue:** `Decode` relies on `GetScaledDimensions`. Skia returns the full size for codecs without scaled decode (PNG in
particular), so `SKBitmap.Decode` allocates width x height x 4 bytes, up to 144 MB at the default `MaxMegapixels = 36`.
The class doc ("the decode itself is scaled so a large picture never fills memory") is only true for JPEG and WebP. On the
small shared host this is a transient spike inside the public site's process.
**Fix:** Soften the doc comment and the docs, or lower the default pixel cap, or decode PNG with a sub-sampled approach.

### IN-02: `docs/bgg-sync.md` misstates the stored widths for mid-size pictures

**File:** `docs/bgg-sync.md` ("Box art" section: "a smaller picture keeps its own width"), `Cabinet.Repository/Images/ArtProcessor.cs:62-64,111-129`
**Issue:** The code gives a 240 px file only for a source between 240 and 480 px wide; only a source narrower than 240 px
keeps its own width. The doc suggests any picture under 480 px keeps its width.
**Fix:** Say "a picture narrower than 480 pixels gets only the 240 pixel file, and one narrower than 240 pixels keeps its
own width".

### IN-03: Unused member `BackdropMask.Build`

**File:** `Cabinet.Repository/Images/BackdropMask.cs:29-30`
**Issue:** `Build` has no caller anywhere in the repository (`Analyse` is used directly).
**Fix:** Remove it.

### IN-04: A response with no Content-Type is accepted as a picture

**File:** `Cabinet.Repository/Images/ImageDownloader.cs:180-185`
**Issue:** The type check is `mediaType is not null && !StartsWith("image/")`, so a missing header skips the check. The
doc comment says a body that is "not a picture" is refused. Decoding catches junk later, so this is low impact, but the
check is weaker than described, and it spends the full download before the decode refuses it.
**Fix:** Refuse a missing media type too, or note in the comment that the decoder is the real gate.

### IN-05: Inconsistent image-host suffix matching in the access-check script

**File:** `build/bgg-access-check.py:1254` (`classify_redirect_host`) versus `:929` (`classify_image_url`)
**Issue:** `classify_redirect_host` uses `host.endswith("geekdo-images.com")`, which also matches an unrelated host such as
`evilgeekdo-images.com`. `classify_image_url` correctly requires a dot boundary. Classification output only, and no request
is ever sent to such a host, but the two should agree.
**Fix:** Use `host == IMAGE_HOST_SUFFIX or host.endswith("." + IMAGE_HOST_SUFFIX)` in both places.

### IN-06: Review-sheet command edge cases: ignored option, uncaught path errors, split surrogate pairs

**File:** `Cabinet.Service/Review/ReviewSheetCommand.cs:176-184,229-247`, `Cabinet.Service/Review/ReviewSheet.cs:430-445`
**Issue:**
- `--bold-font` is silently ignored when `--font` is not given.
- `TryWrite` catches only `IOException` and `UnauthorizedAccessException`, so an `--out` value with invalid path
  characters (`ArgumentException` / `NotSupportedException`) ends in an unhandled-exception stack trace instead of exit
  code 5.
- `Ellipsised` truncates by UTF-16 code unit, so a title with an astral character (emoji) can be cut through a surrogate
  pair and draw a replacement glyph.
**Fix:** Reject `--bold-font` without `--font` as a usage error, add `ArgumentException or NotSupportedException` to the
catch, and step back one char when the cut lands on a low surrogate.

---

_Reviewed: 2026-10-08_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

# Part: Tests

(The finding IDs below are scoped to this part, for example the domain WR-01 and the services WR-01 are different findings.)
## Phase 4: Code Review Report (test quality)

**Reviewed:** 2026-10-08
**Depth:** quick (pattern scans over all 77 files, then full reads of the network guard, harness, factory and the new integration tests, plus the diff of every pre-existing file the phase touched)
**Files Reviewed:** 77
**Status:** issues_found

## Summary

The suite is in good shape on the hard rules. No `//` comments, no planning references (requirement keys, plan or phase numbers, planning document names) in names or strings, no personal data (every address is `example.org`, `example.com`, `bgg.example.org`, or a loopback port; usernames and tokens are `sentinel-*` placeholders), and no captured BGG responses (pictures are drawn in code). Integration hosts cover all three program HTTP clients through `RealNetworkGuard`, which hooks the `IHttpMessageHandlerBuilderFilter` and so covers every client built through `IHttpClientFactory`. The program creates no `HttpClient` outside the factory (checked by search), so the guard has no gap today. Time is on `FakeTimeProvider` almost everywhere, and ports are picked with a retry on bind collisions.

The weaknesses are in tests that were loosened while the design changed, and in a few places where a test proves less than its name says. None of them is a correctness blocker.

## Warnings

### WR-01: Phone-versus-desktop section count assertion loosened from strict to non-strict in three places

**File:** `Cabinet.UnitTests/Layout/PhoneProfileTests.cs:176`, `Cabinet.IntegrationTests/LayoutEndpointTests.cs:67`, `Cabinet.UnitTests/Layout/ShelfMixTests.cs:215-229`
**Issue:** The phase changed `BeGreaterThan(desktop sections)` to `BeGreaterThanOrEqualTo` in the unit test and in the endpoint test, and renamed them to "at least as many sections". With `>=`, a phone profile that degrades into the desktop layout (equal counts) now passes, so these tests no longer prove the narrow design is a taller cabinet, which was the reason they existed. Nothing in the tests records why equality became acceptable. In `ShelfMixTests` the 65-game case of "lying flat before a new section needs fewer sections than switching it off" went from `BeLessThan` to `BeLessThanOrEqualTo`, so for that size the feature is no longer shown to do anything. Only the 400-game case keeps the strict check.
**Fix:** Keep the strict check wherever the current design still satisfies it, and loosen only the sizes where it genuinely does not. For example, parametrise the phone comparison by collection size and assert `BeGreaterThan` for the large samples, and add the reason to the `because` text:
```csharp
phone.Sections.Count.Should().BeGreaterThan(desktop.Sections.Count, "the phone design is narrower, so a large collection needs more sections");
```
If the small sample really ties, assert the exact tie explicitly for that sample instead of `>=` for all.

### WR-02: "Whole family stands in one cubby" weakened to "at most two cubbies" without checking the continuation is adjacent

**File:** `Cabinet.UnitTests/Layout/SectionDesignTests.cs:157-200`
**Issue:** Three tests replaced `ContainSingle("the whole family stands in one cubby")` with `HaveCountLessThanOrEqualTo(2, ...)`. The new bound passes for two cubbies in different sections, or two cubbies far apart on the same shelf, so it no longer pins the design rule that a family continues into the neighbouring cubby. `SeriesTests` has a neighbour check, but these family-limit tests do not.
**Fix:** Assert adjacency as well as the count, for example reuse the neighbour check from `SeriesTests.AssertStandTogether` (same section, cubby indexes consecutive) in a shared helper in `LayoutAssertions`.

### WR-03: Network guard tests build clients by string name, so they do not prove the program's own clients are guarded

**File:** `Cabinet.IntegrationTests/RealNetworkGuardTests.cs:15-74`
**Issue:** `ProgramClients` holds `nameof(ICollectionSource)` and friends, and the test calls `CreateClient(name)`. `IHttpClientFactory.CreateClient` with an unknown name returns a default client, and the guard covers default clients too. If the program ever registers a client under a different name (or a fourth client appears), the theory still passes, because it never touches the program's registration. The test therefore verifies the guard, not the coverage claim in its summary. Also the first theory case is not `await using`: if either assertion before `DisposeAsync` fails, the factory (ports, temp storage) leaks, and a second `DisposeAsync` then throws a different error that hides the first failure.
**Fix:** Resolve the real typed clients (`GetRequiredService<ICollectionSource>()` and the two other sources) and have them make a call, or assert the registered client names from `IOptionsMonitor<HttpClientFactoryOptions>` against the three expected names. Wrap the factory in `try`/`finally` or catch the expected dispose exception inside an `await using` scope.

### WR-04: Real-time waits that are long or time-sensitive

**File:** `Cabinet.IntegrationTests/LocalArtTests.cs:21-26,60-66`, `Cabinet.IntegrationTests/FakeBggServerTests.cs:208-216`, `Cabinet.UnitTests/FakeBgg/BggXmlTests.cs:433`
**Issue:** `LocalArtTests` downloads, analyses and resizes about a hundred pictures over real loopback HTTP and waits up to 90 s of real time, which the test itself documents as depending on machine load; it then runs in parallel with the rest of the suite. `Slow_scenario_holds_back_the_answer` measures `DateTimeOffset.UtcNow` against a 300 ms delay with a 250 ms lower bound, so it is sensitive to timer behaviour and adds a fixed 300 ms. The `Task.Delay(250 ms)` race in the handler delay test is a negative-timing check that can only ever slow the suite, not catch a regression faster. These are the only places where a loaded machine can turn a pass into a fail or a timeout.
**Fix:** For the slow-scenario test, expose the delay through the fake's `TimeProvider` (as `ScriptedBggHandler` already does) and advance a `FakeTimeProvider`. Keep `LocalArtTests` but put it in its own non-parallel collection (or its own trait that CI can run alone) so the 90 s budget is not shared with the rest of the suite.

### WR-05: Stale-analysis test has no control showing the re-download is caused by the stale version

**File:** `Cabinet.IntegrationTests/ArtChoiceTests.cs:117-145`
**Issue:** The test rewrites `analysisVersion` in the stored snapshot to the previous number, starts a second host and asserts `secondImages.Requests.Should().NotBeEmpty()`. The second host is a new process state with a new image handler and a new clock, so a host that simply ignored its stored snapshot and downloaded everything again would pass. The sibling test in `BoxArtTests` proves an unchanged second sync sends no picture requests, but only on the same host and not across a restart.
**Fix:** Add the control to this test: restart a host on the unmodified storage first and assert it requests nothing, then tamper and assert it requests every stored picture:
```csharp
secondImages.Requests.Should().BeEmpty("pictures measured by the current analysis are not fetched again");
```

## Info

### IN-01: `fit` assertion accepts every possible value

**File:** `Cabinet.IntegrationTests/BoxArtTests.cs:45`
**Issue:** `art.GetProperty("fit").GetString().Should().BeOneOf("width", "height", "exact")` lists the full set of fit kinds, so it only proves the property exists. For the 600 by 800 picture on a 240 by 320 cover, `exact` is the expected value.
**Fix:** Assert the one value the scripted picture must produce, as `TrueProportionsTests` does.

### IN-02: Sample-kind test and endpoint test no longer exercise the "more" marker through the served layout

**File:** `Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs:183`, `Cabinet.IntegrationTests/CollectionFidelityTests.cs` (kinds list), `Cabinet.IntegrationTests/LayoutEndpointTests.cs:31`
**Issue:** `moreMarker` was dropped from the "review sample shows every kind" expectation and from the served-kinds expectation. Unit coverage in `FamilyLayoutTests` remains, so this is not a gap in the engine, but nothing now proves the marker survives serialisation to the layout JSON. `LayoutEndpointTests:31` still reads `moreCount` for markers, which can no longer occur in that data.
**Fix:** Add one integration case with a family above the limit that expects a `moreMarker` placement with `moreCount` in the served JSON.

### IN-03: Redundant poll in the local art test can hide a partial run

**File:** `Cabinet.IntegrationTests/LocalArtTests.cs:63-66`
**Issue:** After `PressAndWait` returns, the test also waits until any placement has art. Picture work runs inside the sync run, so the extra wait should be a no-op; if it ever is not, the following exact `bare` equality would be racing the downloads rather than checking the result.
**Fix:** Drop the second `WaitUntil`, or assert the run's final state before reading the layout.

### IN-04: Small test-code hygiene items

**File:** `Cabinet.IntegrationTests/FakeBggServerTests.cs:323`, `Cabinet.IntegrationTests/EnrichmentTests.cs:~150`, `Cabinet.IntegrationTests/BoxArtTests.cs:~245`, `Cabinet.UnitTests/Bgg/BggThingClientTests.cs:65,196,208`
**Issue:** `SKCodec.Create(new SKMemoryStream(bytes))` is not disposed. The `failing` flag in the details-failure test is a plain local captured by a lambda that the server thread reads while the test thread writes it (works in practice, but is a data race by definition); a `volatile`-style holder or `Interlocked` flag would be correct. `HttpClient` instances over scripted handlers in `BggThingClientTests` are never disposed. The picture-deadline test hard-codes `TimeSpan.FromMinutes(7)` to exceed a deadline defined elsewhere, so a change to that default breaks the test with no pointer to why.
**Fix:** Dispose the codec and stream, use a small `StrongBox<bool>`/`Volatile` wrapper, and derive the 7-minute step from the setting (or name a constant referencing it).

### IN-05: Several tests pin non-default settings to isolate older behaviour

**File:** `Cabinet.UnitTests/Layout/ShelfMixTests.cs:88,171,219-220`, `Cabinet.UnitTests/Layout/SectionDesignTests.cs:20`, `Cabinet.UnitTests/Layout/FamilyStabilityTests.cs:31-32`, `Cabinet.UnitTests/Layout/PhoneProfileTests.cs:34`, `Cabinet.IntegrationTests/FamilyCoverTests.cs:21-25`, `Cabinet.IntegrationTests/TrueProportionsTests.cs:32-37`
**Issue:** The phase added `CoverFromExpansions: 0` to existing layout tests so the new default (2) does not change their expectations, and `FamilyCoverTests` pins `CoverSharePercent` and `FewGamesThreshold` to 0 (the test name says "whatever the cover share" but proves one share, zero). `TrueProportionsTests` pins `FewGamesThreshold` to 100 so every game faces out, which makes its `kind == "cover"` assertion hold by the pin rather than by the rule under test; only the shape ratio and fit assertions are meaningful there. The default rule is still exercised in `OrientationTests` (values 2 and 3) and `FamilyLayoutTests`, so this is a note on test naming and scope, not a gap. The "within five percentage points" size-mix ranges and the `BeInRange(55, 90)` expansion counts in `SyntheticCollectionsTests` are wide, but they are deterministic seeded samples, so they are loose rather than flaky.
**Fix:** Rename `FamilyCoverTests` to say "with the share at zero", add a second case at a non-zero share if the claim is meant to be general, and drop the `kind == "cover"` assertion from the pinned tests or pin the setting through a named constant that says why.

---

_Reviewed: 2026-10-08_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: quick_

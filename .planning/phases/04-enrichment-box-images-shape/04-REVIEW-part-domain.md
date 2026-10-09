---
phase: 04-enrichment-box-images-shape
reviewed: 2026-10-08T00:00:00Z
depth: standard
files_reviewed: 26
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
findings:
  critical: 0
  warning: 4
  info: 6
  total: 10
status: issues_found
---

# Phase 4: Code Review Report (domain part)

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

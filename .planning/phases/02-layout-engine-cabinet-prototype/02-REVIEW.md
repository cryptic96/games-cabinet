---
phase: 02-layout-engine-cabinet-prototype
reviewed: 2026-10-06T00:00:00Z
depth: standard
files_reviewed: 48
files_reviewed_list:
  - Cabinet.Domain/Layout/CabinetItem.cs
  - Cabinet.Domain/Layout/CabinetLayout.cs
  - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
  - Cabinet.Domain/Layout/CubbyArrangement.cs
  - Cabinet.Domain/Layout/LayoutJson.cs
  - Cabinet.Domain/Layout/LayoutMember.cs
  - Cabinet.Domain/Layout/LayoutOptions.cs
  - Cabinet.Domain/Layout/Orientation.cs
  - Cabinet.Domain/Layout/ReadabilityFloor.cs
  - Cabinet.Domain/Layout/SectionDesign.cs
  - Cabinet.Domain/Layout/SectionDesigns.cs
  - Cabinet.Domain/Layout/SpineLabel.cs
  - Cabinet.Domain/Layout/SpinePalette.cs
  - Cabinet.Domain/Layout/SplitMix64.cs
  - Cabinet.Domain/Layout/StableHash.cs
  - Cabinet.Domain/Layout/StackLayout.cs
  - Cabinet.Domain/Samples/SyntheticCollections.cs
  - Cabinet.Service/Layout/LayoutCache.cs
  - Cabinet.Service/Layout/LayoutEndpoint.cs
  - Cabinet.Service/Layout/LayoutSettings.cs
  - Cabinet.Service/Pages/Index.cshtml
  - Cabinet.Service/Pages/Index.cshtml.cs
  - Cabinet.Service/Program.cs
  - Cabinet.Service/Prototype/SampleCatalog.cs
  - Cabinet.Service/appsettings.json
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Service/wwwroot/css/site.css
  - Cabinet.Service/wwwroot/js/cabinet.js
  - Cabinet.Service/wwwroot/js/copy.js
  - Cabinet.Service/wwwroot/js/render.js
  - Cabinet.IntegrationTests/CabinetPageTests.cs
  - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
  - Cabinet.IntegrationTests/LayoutEndpointTests.cs
  - Cabinet.UnitTests/Layout/CabinetLayoutEngineTests.cs
  - Cabinet.UnitTests/Layout/FamilyLayoutTests.cs
  - Cabinet.UnitTests/Layout/FamilyStabilityTests.cs
  - Cabinet.UnitTests/Layout/LayoutAssertions.cs
  - Cabinet.UnitTests/Layout/LayoutGoldenTests.cs
  - Cabinet.UnitTests/Layout/LayoutSettingsTests.cs
  - Cabinet.UnitTests/Layout/OrientationTests.cs
  - Cabinet.UnitTests/Layout/PhoneProfileTests.cs
  - Cabinet.UnitTests/Layout/SectionDesignTests.cs
  - Cabinet.UnitTests/Layout/ShelfMixTests.cs
  - Cabinet.UnitTests/Layout/SpineLabelTests.cs
  - Cabinet.UnitTests/Layout/SpinePaletteTests.cs
  - Cabinet.UnitTests/Layout/SplitMix64Tests.cs
  - Cabinet.UnitTests/Layout/StableHashTests.cs
  - Cabinet.UnitTests/Layout/StackLayoutTests.cs
  - Cabinet.UnitTests/Layout/SyntheticCollectionsTests.cs
  - Cabinet.UnitTests/Layout/TrimmedSectionTests.cs
  - README.md
  - docs/cabinet-layout.md
findings:
  critical: 0
  warning: 4
  info: 7
  total: 11
status: issues_found
---

# Phase 2: Code Review Report

**Reviewed:** 2026-10-06
**Depth:** standard
**Files Reviewed:** 48 (the listed files; the project instructions file was read as context)
**Status:** issues_found

## Summary

The layout engine, the endpoint and the page are in good shape. No blockers were found. The areas called out in the
request were checked specifically and held up.

Checked and clean:

- **Endpoint input handling.** `sample` and `profile` are only ever used as keys into fixed allowlists (exact,
  case-sensitive, ordinal). A multi-value query (`?sample=a&sample=b`) binds to a comma-joined string and falls through to
  404. The cache holds at most 7 samples x 2 designs, so a visitor cannot grow it. The ETag is built only from the layout
  version, a hex fingerprint and allowlisted names, so no header injection is possible. The `If-None-Match` handling
  (weak comparison, `*`, malformed header) is correct. No CORS headers are sent.
- **No reflection of user input into markup.** The page only emits the resolved allowlist name, through Razor
  attribute encoding. The JavaScript builds everything with `createElement`, `textContent` and `dataset`, with no
  `innerHTML`, `insertAdjacent*` or `eval`. The layout JSON uses the default encoder, which escapes `<`, `>`, `&`, `'`
  and all non-ASCII characters.
- **Strict-CSP compatibility of the page assets.** There are no inline `style` attributes, inline scripts, event
  attributes or `data:` URLs. `element.style.setProperty` is a CSSOM write and is allowed under `style-src 'self'`.
  (Nothing actually sends a CSP header, though: see WR-02.)
- **Determinism rules in `Cabinet.Domain/Layout`.** No `System.Random`, `GetHashCode`, `DateTime`, `double`, `float`,
  `decimal`, `Math.Round` or culture-sensitive comparison. All hashing is FNV-1a plus the SplitMix64 finisher over
  `ulong`. Ordering is by explicit keys with ordinal comparers. `Dictionary` and `HashSet` are only used for lookup,
  never enumerated for output.
- **Hard rules.** A search of every reviewed non-fixture file found no planning references (phase, plan, wave,
  requirement or decision IDs, planning document names, `.planning`), no `//` comments in C#, JS or Razor, no
  `/* */` comments outside `/** */` blocks, and no personal data. The Traefik example, which was outside the review
  list but is relevant to WR-02, uses RFC 5737 and `example.com` placeholders.

The four warnings are one real cost-amplification flaw on the public endpoint, a CSP that is asserted by tests but
never delivered, a silent-failure path in the engine, and a sizing inconsistency for base games with expansions. The
remaining items are minor.

Not reviewed in depth: `FamilyLayoutTests.cs`, `FamilyStabilityTests.cs`, `PhoneProfileTests.cs`, `ShelfMixTests.cs` and
`SectionDesignTests.cs` were skimmed for flaky patterns (sleeps, clocks, unseeded randomness, skipped tests: none found)
rather than read line by line.

## Warnings

### WR-01: The layout endpoint and the page regenerate the whole sample collection on every request, even for cache hits and 304s

**File:** `Cabinet.Service/Layout/LayoutEndpoint.cs:43-47`, `Cabinet.Service/Prototype/SampleCatalog.cs:36-41`, `Cabinet.Service/Pages/Index.cshtml.cs:32`
**Issue:** The endpoint guard calls `SyntheticCollections.TryGetSample(sample, out var items)` and then never uses
`items`. Because `TryGetSample` builds the collection from scratch each time (title generation, a unique-title
retry loop and the family and oversize assembly; 400 items for the largest sample), every request to `/cabinet/layout`
pays that cost, including requests answered from `LayoutCache` and requests answered 304. `LayoutCache.Build` then
calls `TryGetSample` a second time on a miss. `IndexModel.OnGet` does the same through `SampleCatalog.ItemCount`, once
per page view, only to print a count. This is an unauthenticated, unlimited public endpoint on a low-power host, so the
cache that was designed to make a request cheap is bypassed by the validation step in front of it, and the `out var items`
variable is dead code. (The project's stack notes also call for a general rate limit on public endpoints, and none is
registered, which makes the per-request cost more relevant.)
**Fix:** Validate with the allowlist only, and keep the item count where it is already computed.

```csharp
if (!catalog.Enabled
    || !catalog.IsKnown(sample)
    || !SectionDesigns.TryGet(profile, out var design))
{
    return Results.NotFound();
}
```

Expose the count from the cache (for example `CachedLayout` carries `ItemCount`, set in `Build` from `items.Count`) or
memoise it in `SampleCatalog` in a `Lazy<IReadOnlyDictionary<string, int>>`, so `IndexModel` stops regenerating the
collection.

### WR-02: A strict CSP is relied on and tested for, but no Content-Security-Policy header is sent by the app or by the proxy example

**File:** `Cabinet.Service/Program.cs:56-96`, `deploy/traefik/cabinet.yml.example:32-37`, `Cabinet.IntegrationTests/CabinetPageTests.cs:75-85`
**Issue:** The page, the stylesheet and the scripts were built to be CSP-clean, and a test asserts there are no inline
styles or scripts "because a strict content security policy blocks them". Nothing ever delivers that policy. `Program.cs`
adds no response headers, and the only header middleware in the repository (the Traefik example) sets HSTS, nosniff,
frame-deny and referrer policy but no `Content-Security-Policy`. The site is public, and it will render real game titles
from an external source once the collection is connected. Without the header, the CSP-clean markup gives no protection
against an injection. A later change that adds an inline style or script would pass every test and also work in the
browser, so the regression the test is meant to catch would never surface.
**Fix:** Send the policy and test it. The first line below is the whole policy that the current assets need (the box art
is not yet loaded from anywhere else):

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.ContentSecurityPolicy =
        "default-src 'self'; script-src 'self'; style-src 'self'; img-src 'self'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'";
    await next();
});
```

Add an integration test that asserts the header is present on `/` and that it contains neither `unsafe-inline` nor
`unsafe-eval`. Alternatively add it as a `customResponseHeaders` entry in the Traefik example and say so in the setup
guide. Either way, record where the policy lives.

### WR-03: A failed re-arrangement is silently turned into an empty cubby, hiding lost games

**File:** `Cabinet.Domain/Layout/CabinetLayoutEngine.cs:429-434`
**Issue:** `ToLayoutSection` re-runs `CubbyArrangement.TryArrange(...)` for every cubby when it builds the output and
substitutes `?? []` when the result is `null`. Placement already proved that each cubby arrangement succeeds, so a `null`
here can only mean an invariant broke (for example a future change to the arrangement rules that makes the first and
second call differ, or a design edit that passes `Validate` but fails here). The result is a cabinet that renders
without a game, or with a whole family missing, and no error anywhere. The layout tests count placements, but only for
the recorded samples, and a real collection would never be checked. Failing loudly is consistent with how the rest of
the engine treats unplaceable items (it throws `InvalidOperationException`).
**Fix:**

```csharp
var placements = CubbyArrangement.TryArrange(
        context.Design, cubby, entry.members, context.Options, context.OrderSalt(sectionIndex, entry.cubbyIndex))
    ?? throw new InvalidOperationException(
        $"Cubby {cubby.Index} of section {sectionIndex} accepted its games during placement but cannot arrange them.");
```

As a side effect, `LayoutCache`'s `Lazy<CachedLayout>` (default `ExecutionAndPublication` mode) caches a thrown exception
permanently, so such a bug would turn into a 500 for that sample until restart. That is acceptable for a bug that
should never happen, but it is worth one sentence in the type's summary.

### WR-04: A base game that has expansions is scaled by the cover-width limit even when it stands as a spine

**File:** `Cabinet.Domain/Layout/CabinetLayoutEngine.cs:251` (and `Clamp`, lines 306-330)
**Issue:** `ToMember` clamps a base game with `Clamp(item.Box, limits, expansions.Count > 0)`. With `hasFamily` set, the
width limit becomes `MaxFamilyBaseWidthMm` (the anchor cubby width minus the stack column: 270 mm on both designs). The
width limit only matters for a game that faces out, because a spine is drawn at its depth. When a family's base game
stands as a spine and its box front is wider than 270 mm, `width` and `height` are scaled down together, so its spine is
shorter than the same box would be without expansions. For example, a 300 x 400 mm box on the desktop design would be
drawn 360 mm tall as a family base and 400 mm tall alone. Many real boxes are roughly 300 mm wide, and games with
expansions are exactly the popular ones, so a shelf will show games with expansions systematically shorter than their
neighbours of the same real height. This also contradicts the design principle that how a game stands is decided from
that game alone, and it affects the recorded goldens if fixed (hence a layout version bump).
**Fix:** Apply the family width limit only when the pose is a cover.

```csharp
var hasWideFront = expansions.Count > 0 && pose == BoxPose.Cover;
var baseItem = item with { Box = Clamp(item.Box, limits, hasWideFront) };
```

Raise `LayoutVersion`, re-record the goldens, and add a test with a wide spine-posed family base game.

## Info

### IN-01: Layout ETag is not tied to the data that produced the layout

**File:** `Cabinet.Service/Layout/LayoutCache.cs:39`
**Issue:** The ETag is `"{LayoutVersion}-{options fingerprint}-{sample}-{design name}"`. It does not change when
`SectionDesigns`, `SyntheticCollections`, `SpinePalette` or the label rules change without a version bump, so browsers
revalidating with `no-cache` would keep receiving 304 for a stale body after a deploy. The goldens catch most of these for
the small samples and a digest of the large one, so in practice a version bump is forced. Label shortening
(`SpineLabel`) is also based on `StringInfo` text-element segmentation and `string.Trim`, which follow the runtime's
Unicode tables. A .NET upgrade could change the labels of titles with unusual characters (emoji sequences, combining marks)
without any golden changing. That affects the real collection once it is connected, not the invented ones.
**Fix:** Derive the ETag from a hash of the serialised JSON (the body is already built once per entry): `"{version}-{hash16}"`.
That makes the tag correct by construction and removes the dependence on remembering to bump the version.

### IN-02: Settings parsers treat whitespace differently

**File:** `Cabinet.Service/Layout/LayoutSettings.cs:56`
**Issue:** `ReadWholeNumber` parses with `NumberStyles.AllowLeadingSign` only, so `" 30"` or `"30 "` is rejected, while
`ReadSwitch`, `ReadStrategy` and `SampleCatalog.FromConfiguration` all trim first. The failure is loud (the app refuses to
start and names the key), so this is only an inconsistency, but a value written with a stray space in the env file will
fail for numbers and pass for everything else.
**Fix:** Trim before parsing: `int.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)`.

### IN-03: Conflicting and dead duplicate rules between the two stylesheets

**File:** `Cabinet.Service/wwwroot/css/site.css:94-111`, `Cabinet.Service/wwwroot/css/cabinet.css:413-435`
**Issue:** `.cabinet-retry`, `.cabinet-message` and the focus outline are defined in both files. For `.cabinet-retry`
the rules conflict (`padding: 8px 16px` and `font-size: 14px` against `padding: 0 16px` and `font: inherit`), so the visible
result depends on the link order in `Index.cshtml`, which the tests do pin. The `color` in cabinet.css's `.cabinet-message`
is dead, because `main .cabinet-message` in site.css has higher specificity. The unscoped `header`, `nav`, `footer` and `p`
selectors in site.css will also reach any element added later.
**Fix:** Keep each rule in one file (message and retry styles in cabinet.css, since only the cabinet uses them) and scope
the element selectors to the page chrome.

### IN-04: The visible text of the "+N more" marker is not part of its accessible name

**File:** `Cabinet.Service/wwwroot/js/render.js:63-76` and `Cabinet.Service/wwwroot/js/copy.js:43-45`
**Issue:** The marker's visible text is `+3 more` but its `aria-label` is `3 more expansions for <base>`. The visible
text is not contained in the accessible name, which fails the label-in-name rule that speech-input users rely on
(saying "click plus three more" will not match). Separately, every placement is a focusable `<button>` that does nothing
yet, so keyboard users tab through every box (hundreds in the largest sample) before reaching the footer, with no skip link.
**Fix:** Make the marker's name start with the visible text, for example `+3 more expansions for <base>`. Until the
buttons do something, consider rendering placements as non-interactive elements, or add a skip link past the cabinet.

### IN-05: Prototype switch defaults differ between code and shipped configuration

**File:** `Cabinet.Service/appsettings.json:24-26`, `Cabinet.Service/Prototype/SampleCatalog.cs:55-65`
**Issue:** `SampleCatalog.FromConfiguration` treats a missing `Prototype:Enabled` as off, but the committed
`appsettings.json` turns it on. A release installed without an env override therefore serves invented collections
publicly. This is documented in `docs/cabinet-layout.md` and the README ("Current state"), so it looks deliberate, but
confirm it is intended for this release. The invented pages have no `noindex` marker, so search engines may index
fictional collections.
**Fix:** If it is intended, add `<meta name="robots" content="noindex" />` while `PrototypeEnabled` is true; otherwise ship
`Prototype:Enabled=false` in `appsettings.json` and enable it in development settings only.

### IN-06: Sample names and sizes are declared in several places that must be kept in step by hand

**File:** `Cabinet.Domain/Samples/SyntheticCollections.cs:24-27,50-58,81`
**Issue:** `SampleSizes` (a dictionary of literals, including `["400"]` next to a `LargeSampleName` constant that is
used elsewhere), `SampleNames` (a second list) and `ReviewSampleName`/`LargeSampleName` all describe the same set. Adding or
renaming a sample in one place without the others yields a name that appears in the switcher but answers 404, or the reverse.
There is a test in `SyntheticCollectionsTests`, but the structure invites drift. `SampleCatalog.Label` also infers the label
from "is it a number", so the edge sample is "any non-numeric name".
**Fix:** Derive `SampleNames` from `SampleSizes.Keys` plus the edge name, and use the constants as dictionary keys.

### IN-07: Integration test factory picks free ports with a check-then-bind race

**File:** `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs:104-110`
**Issue:** `GetFreeLoopbackPort` binds a listener to port 0, reads the port and closes it, then Kestrel binds the same
port later. Another process, or another factory instance running in parallel (two ports per instance, many test classes),
can take it in between, producing an occasional "address already in use" failure that is hard to reproduce. Also,
`Dispose(bool)` and `DisposeAsync` both stop and dispose `_realHost`, so disposal runs twice on some paths.
**Fix:** Let Kestrel bind port 0 and read the actual addresses from `IServerAddressesFeature` after start, and make the
disposal idempotent (null the field after disposing).

---

_Reviewed: 2026-10-06_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

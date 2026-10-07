# Phase 4: Enrichment, Box Images & Shape - Research

**Researched:** 2026-10-07
**Domain:** BGG `thing` enrichment, server-side image pipeline (SkiaSharp), 3D-shot detection, art-derived spine colour, true-proportion boxes, ASP.NET Core static serving, vanilla-JS cabinet renderer
**Confidence:** MEDIUM-HIGH (pipeline, colour maths and engine changes HIGH; BGG version-image field and CDN behaviour MEDIUM until one shape check runs on the server; detector thresholds are starting values by design)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Picking the box image**
- **D-01: Two candidates per game.** The two candidates are: the owned version's image; the game's own main BGG image. For an expansion, "main image" means the expansion's own main image from `thing`, never its parent game's image. A detector decides whether the version image is a flat front cover or a 3D/perspective product shot (a slanted box on a plain background). Research confirms where the version image comes from: the collection item's `image` under `version=1`, or the version element itself.
- **D-02: When the detector is unsure, lean to the main image.** Accepted cost: some flat Dutch covers are replaced by the main (often English or original) cover until the owner overrides them. The threshold is a tuning value, set during the review (D-04).
- **D-03: When both candidates look like 3D shots, show the owned version's 3D shot, not a generated cover.** The reason to swap to the main image is to get a flat cover. When the main image is not flat either, the owner's own edition is the more truthful picture. The same applies when the main image is missing. The generated cover from the layout phase stays the fallback only for a game with no usable image at all: missing, failed to download, or undecodable. **Orientation never depends on image analysis.** Whether a box faces out is still decided by the layout settings and the game alone (layout D-06). A changed image or verdict never flips a box between cover and spine.
- **D-04: The owner checks the picks on a review sheet.** After a real sync on the server, Claude builds a contact sheet of every game. Each row shows both candidate images, the detector's verdict and score, and the image that was chosen. Claude sends it as images. It is never committed, and no title, image or score from the owner's collection enters the repository or `.planning/`. The owner marks wrong picks, and the detector is tuned until the owner is happy overall. Any committed tooling that builds the sheet holds no personal data and runs on the server, like the access-check script in the sync phase. This is how roadmap success criterion 3 ("checked against the owner's real Dutch editions") is met.
- **D-05: Leftover misses wait for owner overrides.** After tuning, a few wrong picks are accepted and fixed later with the owner's image overrides (IMG-02). There is no stopgap override list, and the phase does not require zero misses.

**Fitting art to the box**
- **D-06: Never crop.** Box art is downscaled only. It is fitted whole inside the box front, and any leftover space is filled with a colour taken from the art. No trimming of plain margins, no cropping to fill, no stretching, and nothing drawn over the art. A 3D shot shown face-out (D-03) is fitted the same way. The box front keeps the box's own shape (D-07), never the outline of the slanted box in the shot. Reversible - presentation only.
- **D-07: Real sizes win, unless they clearly contradict a flat cover.** The order (IMG-03) is: 1. the owned version's dimensions, which pass the existing plausibility check; 2. otherwise a flat cover's shape; 3. otherwise a realistic default. BGG sizes are entered by users and are sometimes swapped or belong to another printing. When the front shape from the sizes differs from a flat cover's aspect ratio by more than a clear margin, the sizes are treated as wrong. The cover's shape is then used at the sizes' scale, with their depth. The margin is a tuning value. Research picks a starting value, and the review confirms it. A 3D shot's shape is never used for any of this.
- **D-08: Games without sizes get an estimated size from their own data.** The estimate is a size class from weight, play time and player count: heavier and longer games get bigger, deeper boxes, and small card games stay small. It is clamped to realistic bounds. A flat cover, when there is one, sets the shape. Measured coverage (access check in the sync phase): about 30% of base games and 40% of expansions have no sizes. Stability constraint: the estimate must not change the cabinet on small drift. Weight and play time are refreshed weekly (D-17), and a game whose weight moves slightly must not change box size and rearrange a cubby. Use coarse classes or rounded inputs so only a real change moves a box. When no flat cover and no sizes exist, the realistic default per kind applies, refined by the same size class.
- **D-09: Thin boxes keep their true thickness down to a one-line minimum.** The floor: a box is drawn at its real thickness down to the width that one line of title text needs, and never below the 24 px tap target. Below two lines' room: the second line (for example "Expansion for ...") is dropped, and the spine shows the title alone, ellipsised when needed. What it replaces: today's two-line minimums (phone spines at least 59 mm wide; upright expansions at least 64 mm on desktop and 71 mm on phone). Consequences: derived minimums in the section designs and readability floor change, and so does the layout version; the thick-expansion threshold (50 mm, a starting value) may need retuning; the family accessible name still carries the full text. Folded polish items resolved here: the cropped fourth-line ellipsis on small phone covers and the truncated two-line upright expansions.

**Spine look from the art**
- **D-10: One solid colour per spine.** The art's main colour fills the spine, with the title on it. The structure is the same as today's palette spines, with no accent bands and no strip of art (a strip would crop, see D-06).
- **D-11: True to the art.** The extracted colour is used as it is. Only its lightness is nudged, and only when needed, so the title reaches readable contrast. Plain backgrounds around product shots are ignored when picking the colour (CAB-03): white, near-white or uniform borders and backdrops must not win. The colour is computed once per image during sync and stored, never computed in the browser.
- **D-12: Title text is black or white,** whichever reaches at least 4.5 to 1 on the spine colour. This must still hold under the strongest furniture shade the classic finish may lay over a box (`SpinePalette.MaxShadePercent`, 20%), as the placeholder palette does today. The placeholder palette stays the colour source for games with no usable art.
- **D-13: The default cover share is picked from screenshots.** The owner sees screenshots of the real collection at two or three cover shares (for example about 1 in 5, 1 in 4 and 1 in 3) and picks one. The pick becomes the committed default of `Layout:CoverSharePercent` in appsettings. It stays a server setting with an env-file override and a restart. Strategy and the other layout settings are unchanged unless the review suggests otherwise.

**Expansions and enrichment**
- **D-14: An expansion that expands several owned base games sits beside the first owned one,** meaning the lowest collection id. This is the same tie-break the engine uses for duplicate copies, so adding another base game later never moves it. The detail phase can list it on every owned base game's card.
- **D-15: Big-box or collector editions do not hide anything.** If the owner owns an edition that BGG says "contains" expansions, and also has a contained expansion marked as owned, both boxes are drawn. Every owned collection entry is a physical box on the shelf. No suppression logic is built for "contains" links.
- **D-16: Follow BGG's type for standalone expansions.** An item that comes back in the base-games collection call is a base game with its own place. It may face out, even when BGG also links it as an expansion of another game. Only items BGG types as expansions (the expansions call) pair up beside a base game.
- **D-17: New games get their details at once; everything else is refreshed about weekly.** New games: a newly added game gets its `thing` details and its art downloaded in the same sync that first sees it. Existing games: refreshed about once a week, spread over the hourly runs, so a normal hourly sync stays at the two collection calls plus at most a small, bounded number of `thing` calls. `thing` calls: at most 20 ids per call, at least 5 seconds apart, and never with `versions=1`. Images: downloaded only when a game is new or its image URL changed. Failures: a failed enrichment or image download never fails the sync and never holds the collection back. The game still shows from collection data (generated cover, placeholder colour, last known details), and the next run retries.

### Claude's Discretion
- **Detector approach:** signals such as plain or uniform corners and borders, a non-rectangular dominant outline, skewed edges and background share. It runs on the downscaled image during sync with SkiaSharp. Tune it against synthetic test images (generated flat covers and slanted boxes on plain backgrounds, never real images in the repository), then confirm it on the owner's collection via the review sheet (D-04).
- **Images:** variant widths (for example two widths for phone and desktop); WebP quality; file naming with a content hash for immutable caching; the cache directory under the state directory; discarding originals after downscaling; evicting images no longer referenced; byte and pixel caps before decode; the allowlisted image host (research confirms BGG's current CDN host). Images are fetched only from URLs found in BGG responses, over HTTPS, by a client that never carries the token, serialised with a polite gap. Visitors never trigger a fetch or a resize, and there is no resize endpoint.
- **Colour extraction:** the algorithm (histogram buckets, saturation weighting, background exclusion), which colour fills leftover space when fitting art (D-06), and how lightness is nudged for contrast (D-11, D-12).
- **Size class (D-08):** the model and clamping bounds, the disagreement margin (D-07) and their starting values.
- **Snapshot shape:** the schema version bump; which `thing` fields are stored; store both BGG's average rating and its ranked (Bayesian) rating, and the detail phase picks which to show; no long descriptions are needed for DET-02; per-game enrichment timestamps for the weekly refresh; the image choice and verdict. An older release reading a newer file treats it as no snapshot and resyncs, never crashes.
- **Request budget:** raise or split the per-sync request budget (`BggClient.MaxRequestsPerSync` is 16 today) so a first sync of a few hundred games can enrich in bounded steps over several runs; decide how the weekly refresh is spread across runs; decide what a fresh empty start does (enrich in batches over a few runs, showing games as they are enriched).
- **Orphan expansion labels:** an orphan expansion (base not owned) is now labelled with its base game's title from the `thing` link ("Expansion for {base}"). When it names several bases, pick one deterministically. The interim "Expansion" label from the sync phase remains only for an expansion whose details have not arrived yet.
- **Phone cabinet density (folded todo):** pick an approach from the todo's options and show it in the review round: let a cover or flat box choose an earlier row; trim earlier sections with a documented stability exception; leave the rest to per-location cabinets in the filters phase. Real box sizes change the packing anyway, so measure density again after they land.
- **Review flow:** follow the layout phase's pattern. Iterate locally against the fake BGG with synthetic images, and send screenshots. On the server, build the review sheet (D-04) and the cover-share screenshots (D-13) from the real collection. Then cut a release, which the owner approves, and the owner checks the deployed cabinet on desktop and phone. Run a UI design step for this phase, using the owner's senior-frontend skill.

### Folded Todos
- Box look polish for the box images phase (folded in full): fourth-line ellipsis cropped on the smallest phone covers; short upright expansions truncate both lines (D-09); expansions 50 to 63 mm deep drawn 64/71 mm wide (D-09); phone spines at least 59 mm (D-09); cover share revisit (D-13); plinth arch reads as soft shadow plus general polish.
- Phone cabinet density (folded in full): when a big cover forces a second phone section, the first section can keep several empty rows (only the last section is trimmed); the 400-game sample takes 12 phone sections. Any new stability exception must be documented and tested as its own case.
- Cabinet accessibility notes (partly folded): NOW only the "+N more" marker's accessible name must contain its visible text ("+N more"). The roving tabindex / one-tab-stop composite stays with the detail phase (A11Y-01).

### Deferred Ideas (OUT OF SCOPE)
- Owner image overrides (IMG-02): owner-tools phase. Wrong picks left after tuning wait for these (D-05).
- Arrow-key navigation / one tab stop for the cabinet: detail phase (A11Y-01).
- Showing the enriched details (detail card, which rating to display, expansions listed on every owned base game's card): detail phase.
- Straightening 3D shots into flat covers (CABX-04, v2): would conflict with D-06's no-modification stance.
- Reviewed, not folded: selectable cabinet finishes and lit-cubbies toggle (unscheduled); size live and sync limits for go-public (hardening phase); drop actionlint label entry (CI housekeeping).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SYNC-06 | Each game enriched: player count, play time, weight, designers, mechanics, min age, BGG rating; expansions know their base game(s) | `thing` field map and parse rules (Standard Stack, Code Examples); pairing rule that satisfies D-14/D-16 without an engine change; enrichment scheduler (Pattern 2); snapshot v2 shape (Pattern 1) |
| SYNC-07 | Box art downloaded during sync, downscaled, served from the site; browsers never load BGG images | Image pipeline (Pattern 3): allowlist, caps, scaled decode, WebP, hashed names, static file provider; no-BGG-URL-in-served-output test |
| IMG-01 | Version image when flat; main image when it looks 3D | Detector features and thresholds from a working prototype (Pattern 4); chooser truth table (Pattern 5); store raw features, derive verdict at read time |
| IMG-03 | Proportions: real dimensions, else flat cover ratio, else realistic default; 3D outline never used | Shape chain with orientation-insensitive disagreement margin, estimate with hysteresis (Pattern 6); art-fit computed where final placement size is known |
| CAB-03 | Spine colour from the art, backdrops ignored, legible title at 4.5:1 | Backdrop mask by border clusters, saturation-weighted bucket, OKLCH nudge with measured grid statistics (Pattern 7) |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Treat these with the authority of locked decisions.

- **No planning references outside `.planning/`.** Requirement keys, decision IDs, phase/plan/wave numbers and planning document names must not appear in code, `///` docs, strings, log and exception messages, test names, scripts, config, README or `docs/`. Commit messages are the only exception. (Every example below is written without them.)
- **Comments:** `///` XML doc summaries only in C#; no `//` comments. JS uses `/** ... */` blocks only. If a line needs a comment, rename or extract.
- **Public repository, no personal data:** no BGG username or token, real domains/hostnames, homelab IPs, real storage-location names, recorded BGG responses, or real collection titles/images/scores in code, docs, fixtures, screenshots or commit messages. All fixtures synthetic, including generated test images. Review sheets and cover-share screenshots are sent as images and never committed.
- **Never commit to `main`;** branch `milestone/v1-games-cabinet` is current. Commits use the noreply identity.
- **BGG etiquette:** only the background sync talks to BGG; the API token goes only to the API host (never to the image CDN); "Powered by BGG" credit stays on every page; no `versions=1` on `thing`; at least 5 s between API calls; never relay raw BGG data.
- **Build:** `TreatWarningsAsErrors` + lock files (`RestorePackagesWithLockFile`); `.slnx`; projects Domain / Repository / Service (+ FakeBgg tool, UnitTests, IntegrationTests). Frontend: Razor Pages shell, vanilla ES modules, modern CSS, no Node toolchain in the repo, strict CSP (`default-src 'self'`).
- **Skill:** `senior-frontend` is configured for planner/executor/UI agents (hand-built UI, strict CSP, no inline styles).

## Summary

This phase needs **no new packages**. SkiaSharp 4.153.1 and its Linux native assets are already in the lock files and were proven on the server by the image smoke check. Everything else is new code on top of existing seams: a `thing` client next to `BggClient`, a snapshot schema bump, an image pipeline in `Cabinet.Repository/Images`, a static file provider for the cache, additive layout JSON, a layout-version bump (9 to 10) with re-recorded goldens, and a renderer branch for art covers.

Four findings change the plan more than any others. (1) **The sync rebuilds the snapshot from the fetched items alone** (`SyncRunner` constructs `new CollectionSnapshot(..., collection.Items)`), so enrichment must be carried over from the previous snapshot by key or the next hourly run wipes it. (2) **`RequestPacer` raises any gap below 5 s to 5 s**, so images need their own pacer (a 5 s gap would make a 130-image first run take 11 minutes and a 400-game first run over an hour). (3) **SkiaSharp with `NoDependencies` native assets cannot draw text from `SKTypeface.Default`** (verified: zero pixels drawn, empty family name) and **the cabinet container has no fonts at all**, so the review-sheet tool needs an explicit TTF file. (4) **The best design is to store raw per-candidate facts** (URL, served files, detector features, art colour, edge colours) for both images and derive verdict, choice and shape at read time from settings. Then every tuning round in the review (threshold, margin) is a restart, not a re-download.

A working prototype (scratch, not committed) validated the detector features and the colour/contrast maths. On synthetic images, flat covers give foreground fill 0.99 to 1.00 with empty corners at or below 0.07, while 3D shots give fill 0.78 to 0.89 and the second-emptiest corner at 0.58 to 0.72. Backdrop detection by **border colour clusters** (not by flood-fill drift) is required, because a drift-based flood leaks through any smooth gradient and flags a full-bleed cover as backdrop. The OKLCH nudge reproduces the approved contract's grid numbers (mean 0.0076, max 0.065; 76 percent need no nudge, the contract says 77).

**Primary recommendation:** Build, in order, (a) a shape-only server check of the version-image field, CDN behaviour and fonts (the sync phase's access-check pattern), (b) the Domain contracts (snapshot v2, pairing, shape chain, contrast/nudge maths), (c) the enrichment and image pipeline with per-candidate raw facts, (d) the engine floors/art-fit/density work with a version bump and re-recorded goldens, (e) the renderer, then the review rounds. Derive choice and shape at read time; never store the verdict as the only truth.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| `thing` enrichment, weekly refresh scheduling | API / Backend (sync job) | Database / Storage (snapshot) | Only the background sync talks to BGG; results persist in the snapshot |
| Expansion pairing (D-14, D-16) | Domain (pure mapper) | Storage | Pure function of the snapshot; engine consumes `ExpansionOf` |
| Image download, decode, resize, WebP encode | API / Backend (sync job, Repository) | Database / Storage (cache dir) | Untrusted bytes, caps, native codec; never per visitor |
| 3D detector features, art colour, edge colours | API / Backend (Repository, at sync) | Storage | Computed once per image, stored; the browser never analyses pixels |
| Verdict, chosen image, box shape, size estimate | Domain (pure, read time) | Settings | Derived from stored raw facts plus tunable settings; deterministic |
| Spine colour contrast pair, nudge maths | Domain (pure) | Repository (extraction feeds it) | Pure maths, testable without Skia; also used to validate stored pairs on read |
| Art fit (`width`/`height`/`exact`) | Domain (engine, at placement) | Browser (renders it) | Only the engine knows a placement's final millimetres after clamping |
| Floors (one-line minimums), `showBaseLine`, density | Domain (engine + `SectionDesigns`) | Browser (renders it) | Layout is a pure versioned function; the browser draws rectangles |
| Serving image files | CDN / Static (Kestrel file provider, own origin) | API | Immutable hashed files under the state directory; no resize endpoint |
| Art cover markup, fallback swap, label-in-name | Browser / Client (`render.js`) | CSS | Dumb renderer: custom properties and `textContent` only (strict CSP) |
| Review sheet and cover-share captures | Operator tooling (server-side mode of the service, local scratch Playwright) | none | Never public, never committed |

## Standard Stack

### Core

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| SkiaSharp | 4.153.1 | Decode (scaled), resize, WebP encode, pixel analysis, review-sheet drawing | Already the project's image library and proven on the server [VERIFIED: Cabinet.Repository/Cabinet.Repository.csproj `<PackageReference Include="SkiaSharp" Version="4.153.1" />`] |
| SkiaSharp.NativeAssets.Linux.NoDependencies | 4.153.1 | Native Skia for `linux-x64` | Same file; keeps the release self-contained [VERIFIED: same csproj] |
| ASP.NET Core `StaticFileMiddleware` + `PhysicalFileProvider` | 10.0.x (framework) | Serve the cache directory as immutable static files | Built in; `MapStaticAssets` only covers build-time assets [VERIFIED: Cabinet.Service/Program.cs line 92 `app.MapStaticAssets();`] |
| System.Text.Json, System.Xml.Linq | framework | Snapshot, `thing` parsing | Same patterns as `BggCollectionParser` and `SnapshotStore` |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 4.0.1, FluentAssertions 8.11.0, Microsoft.Extensions.TimeProvider.Testing 10.10.0 | pinned | Unit tests | All new tests [VERIFIED: Cabinet.UnitTests.csproj] |
| Microsoft.AspNetCore.Mvc.Testing 10.0.12 | pinned | Integration tests | Sync-to-layout-to-static-file flows [VERIFIED: Cabinet.IntegrationTests.csproj] |
| Node built-in test runner | node 24 on dev box | `build/tests/page-scripts.test.mjs` | `render.js` unit cases (no npm packages) |
| Playwright (npm, scratch only) | 1.63.0 | Review screenshots, DOM geometry checks, CSP/requests audit | Developer-run from a scratch directory, never added to the repo, as in the layout phase [VERIFIED: `npm view playwright version` = 1.63.0 this session] |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Own backdrop/silhouette detector | An ML or edge-detection library | A new native or model dependency for a decision the owner reviews by eye; the prototype separates the synthetic classes cleanly with ~80 lines |
| Border-cluster backdrop mask | Flood fill with a drift tolerance from the seed colour | Rejected after prototyping: any smooth gradient is reachable along iso-colour lines from the border, so a full-bleed gradient cover became 72 to 77 percent "backdrop" |
| Verdict stored as the only truth | Raw features stored, verdict derived at read time | Derived-at-read costs nothing and makes each review tuning round a restart instead of a re-download |
| Review sheet from the service executable (server) | Build locally from copied cache | The decision says the tool runs on the server; local copy is the fallback if fonts cannot be installed |

**Installation:** none. No package is added or upgraded. `Cabinet.FakeBgg` needs a SkiaSharp reference (already resolved in the lock graph) only if the fake image host generates its pictures in code; an alternative is a small encoder in the unit-test project that the fake loads (see Open Question 5).

**Version verification:** [VERIFIED: Cabinet.Repository/packages.lock.json] `"resolved": "4.153.1"` for both SkiaSharp packages; [VERIFIED: local scratch project restored and ran against `SkiaSharp` assembly 4.153.0.0 / package 4.153.1].

## Package Legitimacy Audit

No new external packages are installed in this phase.

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| SkiaSharp | NuGet | already in the project since the first phase | n/a (existing) | github.com/mono/SkiaSharp | not re-checked: the `package-legitimacy` seam supports npm, pypi and crates only | Existing dependency, locked and in use |
| SkiaSharp.NativeAssets.Linux.NoDependencies | NuGet | same | n/a (existing) | github.com/mono/SkiaSharp | same | Existing dependency, locked and in use |

**Packages removed due to [SLOP] verdict:** none.
**Packages flagged as suspicious [SUS]:** none.
Playwright (npm) is scratch tooling outside the repository and was cleared in the layout phase; `npm view playwright version` returned 1.63.0 today.

## Architecture Patterns

### System Architecture Diagram

```
 hourly timer / "sync now"
          |
          v
 SyncWorker (single flight, RunLimit 10 min)
          |
          v
 [1] BGG collection x2 (existing, version=1)  --> items + versionImageUrl + mainImageUrl(fallback)
          |  shrink guard
          v
 [2] MERGE with previous snapshot (carry enrichment + image records by gameId / url)
          |  commit #1: collection visible at once (generated covers, palette, interim labels)
          v
 [3] thing batches (<=20 ids, >=5 s apart, no versions=1)
          |  new games first, then oldest-refreshed older than 7 days (bounded per run)
          |  commit #2..: details, expansion links, main image url
          v
 [4] image candidates per game: A = version image, B = main image (thing.image)
          |  polite pacer (own gap), https + allowlisted host, byte cap, codec pixel cap
          v
     SKCodec scaled decode -> 480 / 240 WebP (hash-named) -> features + art colour + edge colours
          |  commit #3..: image records (raw facts only)
          v
     snapshot.json (schema 2)   +   <state>/art/{hash}-{w}.webp
          |
          v  (read time, pure, deterministic)
 SnapshotMapper: pairing (D-14/D-16) -> verdict from features+settings -> chosen image (table)
          -> shape chain (sizes | cover | estimate | default) -> CabinetItem(+Art, +Colour)
          |
          v
 CabinetLayoutEngine v10 (floors, showBaseLine, art fit, density) -> layout JSON per profile
          |
          v
 GET /cabinet/layout  --> render.js (<img class="cover-art"> same origin; spines coloured via --bg/--fg)
 GET /art/{hash}-{w}.webp  --> StaticFiles(PhysicalFileProvider(<state>/art)), immutable
```

### Recommended Project Structure

```
Cabinet.Domain/
  Collection/
    CollectionSnapshot.cs        # schema 2: Games, ImageRecords, per-item version image url
    GameDetails.cs               # details record (players, time, weight, ratings, designers, mechanics, links)
    ArtChoice.cs                 # verdict thresholds + chooser table (pure)
    BoxShape.cs                  # shape chain: sizes | cover | estimate | default (replaces BoxFromVersion callers)
    SizeEstimate.cs              # size class with hysteresis
    ExpansionPairing.cs          # D-14 / D-16 -> ExpansionOf
    SnapshotMapper.cs            # extended; Version hash covers art, colour, box, pairing
  Layout/
    SpineColour.cs               # contrast ratio, shade blend, OKLCH nudge, pair validation (pure)
    ArtFit.cs                    # width | height | exact from final mm and pixel size
    (CabinetItem, Placement, SectionDesigns, ReadabilityFloor, CabinetLayoutEngine extended)
Cabinet.Repository/
  Bgg/BggThingParser.cs, BggThingClient (or BggClient second interface)
  Images/
    ImageDownloader.cs           # allowlist, https, redirects, byte cap, own pacer
    ImageAnalyzer.cs             # codec cap, scaled decode, variants, features, colour, edges
    ImageCache.cs                # hash names, atomic write, prune with grace
Cabinet.Service/
  Sync/ (SyncRunner orchestration, EnrichmentPlanner, settings)
  Images/ArtEndpoint.cs          # StaticFiles registration for /art
  Review/ReviewSheet.cs          # operator mode (arg on the service executable), no routes
  wwwroot/js/render.js, css/cabinet.css
Cabinet.FakeBgg/                 # thing data + synthetic images + fake image host
```

### Pattern 1: Snapshot schema 2 stores raw facts, normalised

**What:** Keep `SnapshotItem` close to today and add two dictionaries, so a refreshed collection never loses what was learned.

```
CollectionSnapshot { SchemaVersion=2, CapturedAtUtc, Items[], Games{gameId -> GameDetails}, Images{urlKey -> ImageRecord} }
SnapshotItem  += VersionImageUrl?            (from collection version/item/image; may be absent)
                 MainImageUrl?               (from collection item image; fallback until thing arrives)
GameDetails   = MinPlayers?, MaxPlayers?, PlayingTime?, MinPlayTime?, MaxPlayTime?, Weight?, MinAge?,
                Average?, BayesAverage?, Designers[<=N], Mechanics[<=N], ExpandsGames[(id,title)] (inbound only),
                MainImageUrl?, EnrichedAtUtc, SizeClass?, SizeModelVersion
ImageRecord   = SourceUrl, Files{240: name,w,h ; 480: name,w,h}, Features{backdropShare, fill, corner1, corner2, sides, aspect},
                ArtColour{background,text}, Edges{top,right,bottom,left}, Status(ok|failed|undecodable), AttemptedAtUtc
```

**Why:** `SyncRunner` today builds the snapshot from the fetch alone [VERIFIED: Cabinet.Service/Sync/SyncRunner.cs line 83 `var snapshot = new CollectionSnapshot(CollectionSnapshot.CurrentSchemaVersion, _time.GetUtcNow(), collection.Items);`], so the run must merge previous `Games` and `Images` by key before saving. Keying images by source URL means "image changed" is just "key absent", and a failed record with `AttemptedAtUtc` gives a bounded retry.

**Compatibility rules (already how the store works):** [VERIFIED: Cabinet.Repository/Storage/SnapshotStore.cs lines 124-125] required properties are only `["schemaVersion", "capturedAtUtc", "items"]` and `["collectionId", "gameId", "title", "kind"]`, so new members must be optional and nullable (the options set `RespectNullableAnnotations = true`). A v1 file therefore loads into v2 with no details. A newer file read by an older release is renamed aside: [VERIFIED: SnapshotStore.cs line 14 `public const string SetAsideFileName = "snapshot.json.bad";`], i.e. an older release resyncs from nothing. Raise `CurrentSchemaVersion` from 1 to 2 [VERIFIED: CollectionSnapshot.cs line 35 `public const int CurrentSchemaVersion = 1;`].

### Pattern 2: Enrichment scheduling (bounded, progressive, never fails the sync)

**What:** After the collection is committed, run a planner that returns `thing` batches: first every game with no `GameDetails`, then games whose `EnrichedAtUtc` is older than 7 days, oldest first, capped.

- Defaults to propose (all validated at startup like `LayoutSettings`): `MaxThingRequestsPerRun` 25 (500 games on a fresh start), `RefreshBatchesPerRun` 1 (20 games per hourly run is 3,360 per week of capacity, so a 65-game collection refreshes in about 4 runs and the first-sync cohort spreads itself with no jitter), `RefreshAfterDays` 7.
- `MaxRequestsPerSync = 16` stays the budget for the two collection calls [VERIFIED: BggClient.cs line 23 `public const int MaxRequestsPerSync = 16;`]; `thing` gets its own budget. The existing `RequestBudget` is private to `BggClient`.
- Commit after each batch (save snapshot then replace the in-memory view, same order as today) so a timeout loses at most one batch. A failed batch is logged by category only and retried next run.
- The run limit is 10 minutes [VERIFIED: SyncWorker.cs line 26 `public static readonly TimeSpan RunLimit = TimeSpan.FromMinutes(10);`] and a cancelled run is recorded as a timeout failure. Give enrichment its own deadline (for example 6 minutes after the collection commit) and stop cleanly before the limit; a stop at the deadline is not a failure.
- Timing: 65 games is 4 `thing` calls (about 25 s). 400 games is 20 calls (about 2 minutes). Images dominate (Pattern 3).
- `thing` parameters: `thing?id=...&stats=1`. No `versions=1`. No `type` filter (default returns any type) [ASSUMED: the wiki page was not reachable; the shape check must include one expansion id].

**Fields to read** (from third-party fixtures of the live API, [CITED: github.com/MatthewThompson/arnak test_data/game/game_expansion.xml], MEDIUM): `item/@type`, `@id`, `thumbnail`, `image` (element text, padded with whitespace in pretty-printed fixtures, so trim), `minplayers|maxplayers|playingtime|minplaytime|maxplaytime|minage` (`@value`), `link[@type='boardgamedesigner'|'boardgamemechanic']/@value`, `link[@type='boardgameexpansion'][@inbound='true']` (`@id`, `@value`) on an expansion, and `statistics/ratings/average|bayesaverage|averageweight` (`@value`). Observed link types in the fixtures: `boardgameaccessory`, `boardgameartist`, `boardgamecategory`, `boardgamedesigner`, `boardgameexpansion`, `boardgamefamily`, `boardgamemechanic`, `boardgamepublisher`. A base game has outbound `boardgameexpansion` links (no `inbound`) that can number in the hundreds [VERIFIED: 03-SPIKE-OUTCOME.md call F `child element link: in 1 items, attribute names: id 302, type 302, value 302`], so read links by type and cap what is stored; never store the outbound list.

**Parse rules:** treat `0` as "unknown" for `minage`, `averageweight`, `bayesaverage`, and a rating that is not numeric; keep gaps as null (4 of 15 expansions lack play-time attributes [VERIFIED: 03-SPIKE-OUTCOME.md "in the expansion answers 4 of 15 items lack the playing time and play-time range attributes"]). Clean every BGG string with the existing cleaner [VERIFIED: BggCollectionParser.cs line 86 `public static string CleanTitle(string title)`]. Use the same hardened XML reader settings as the collection parser (DTDs prohibited, `XmlResolver = null`, 20M character cap).

### Pattern 3: Image pipeline (download, decode, variants, facts)

1. **Candidates per game:** A = `SnapshotItem.VersionImageUrl`, B = `GameDetails.MainImageUrl` (from `thing/image`), falling back to the collection item's top-level `image` until `thing` arrives. When A equals B there is one candidate.
2. **URL normalisation:** trim; accept `https://host/...` and protocol-relative `//host/...` (older records use `//cf.geekdo-images.com/images/pic<N>.jpg`, [CITED: github.com/syllant/bggcli tests/resources/collection.xml]); reject any other scheme; compare the host to an exact allowlist (`Images:AllowedHosts`, default `cf.geekdo-images.com`). [CITED: github.com/MatthewThompson/arnak test_data, MEDIUM] Current-style URLs look like `https://cf.geekdo-images.com/<id>__original/img/<sig>=/0x0/filters:format(jpeg)/pic<N>.jpg`; the path is signed, so a size variant cannot be requested by editing it. Always download the original and downscale locally.
3. **Client:** a second typed `HttpClient` with no auth handler, `AllowAutoRedirect = false` (follow at most 3 redirects manually, each re-checked against scheme and allowlist), `UseCookies = false`, honest `User-Agent` reused from `BggTransport.UserAgent`, `MaxResponseContentBufferSize` cap, `HttpCompletionOption.ResponseHeadersRead` so `Content-Length` can refuse early.
4. **Own pacer, not the BGG pacer:** [VERIFIED: RequestPacer.cs line 32 `_gap = gap < BggOptions.MinimumRequestGap ? BggOptions.MinimumRequestGap : gap;`] the shared pacer would force 5 s. Generalise it with a floor parameter or add an image pacer; register separately. Starting gap 1 s [ASSUMED: STACK.md says a 1 s gap is enough for the CDN; unverified]. First sync estimate with two candidates per game: 65 games is about 130 downloads (about 3 minutes); 400 games is about 800 (about 20 minutes), so cap per run (`MaxImageDownloadsPerRun`, start 80) and finish over several runs. Games show immediately with generated covers; each commit upgrades them.
5. **Decode safely:** [VERIFIED: scratch run against SkiaSharp 4.153.1] `SKCodec.Create(stream)` reads dimensions without decoding and returns null for garbage; `codec.GetScaledDimensions(scale)` plus `SKBitmap.Decode(codec, info)` gives a JPEG DCT-scaled decode (4000x3000 JPEG to 1000x750 in 31 ms versus an 80 ms and 45 MB full decode; process peak 192 MB in the test). Enforce `MaxPixels` (start 36 million) and `MaxBytes` (start 12 MB) from `codec.Info` and the response length before decoding; decode as `SKColorType.Rgba8888` with `SKAlphaType.Unpremul` so alpha PNGs analyse correctly (a plain `SKBitmap.Decode(bytes)` of an alpha PNG returned `Premul`).
6. **Variants:** resize with the already-proven `SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear)` [VERIFIED: ImageSmoke.cs line 126] to 480 and 240 wide (never upscale: if the source is narrower, emit only what fits and say so in the record), encode WebP quality 80 [VERIFIED: ImageSmoke.cs line 22 `private const int WebpQuality = 80;`]. [VERIFIED: scratch run] lossy WebP preserves alpha (a transparent corner stayed alpha 0). File name `{first 16 hex of SHA-256 of the encoded bytes}-{width}.webp` under `<state>/art/`, written with `AtomicJsonFile`-style temp-then-move. Discard originals after analysis.
7. **Facts per image** (computed on a 96 px analysis copy of the 480 variant so re-analysis never needs the original): detector features, art colour pair, four edge colours (alpha-weighted mean of the outer 2 percent per side, at least 2 px; if less than half the strip is opaque, fall back to the spine colour, which is still "a colour taken from the art"), served pixel sizes.
8. **Failure never fails the sync:** HTTP errors, caps, undecodable bytes, timeouts mark the record `failed` or `undecodable` with `AttemptedAtUtc`; retry after a backoff (for example 24 h, then weekly). The game shows its generated cover and palette colour.
9. **Serve:** `app.UseStaticFiles(new StaticFileOptions { FileProvider = new PhysicalFileProvider(artDirectory), RequestPath = "/art", OnPrepareResponse = ... })` with `Cache-Control: public, max-age=31536000, immutable`; create the directory before building the provider (`PhysicalFileProvider` throws if missing). [VERIFIED: scratch run] `FileExtensionContentTypeProvider` maps `.webp` to `image/webp`. Use `/art`, not `/img`, because `wwwroot/img` is the credit logo's folder. The CSP needs no change [VERIFIED: ContentSecurityPolicy.cs lines 12-13 `"default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'"`].
10. **Prune** unreferenced files only after a successful commit and only if older than a grace period (7 days by file time). After a rollback to an older release the snapshot is set aside; a naive prune on the next upgrade would delete every cached file.

### Pattern 4: 3D-shot detector (features measured on a 96 px copy)

Prototype result (synthetic fixtures generated in code; not committed). Backdrop mask: collect the outer 1 px ring; quantise to 4 bits per channel; keep clusters holding at least 10 percent of the ring; if kept clusters cover less than 55 percent of the ring there is **no backdrop**; seed the mask from ring pixels within tolerance 18 (RGB distance, transparent pixels match transparent) of a kept cluster mean and 4-connected flood into pixels within tolerance of any kept cluster mean.

| Fixture (synthetic) | backdrop share | fill | corner1 | corner2 | sides touched |
|---|---|---|---|---|---|
| flat cover 3:4, full bleed | 0.00 | 1.00 | 0.00 | 0.00 | 4 |
| flat wide banner | 0.00 | 1.00 | 0.00 | 0.00 | 4 |
| flat cover in thick white frame | 0.35 | 1.00 | 0.00 | 0.00 | 0 |
| flat cover in thin black frame | 0.07 | 0.99 | 0.07 | 0.07 | 2 |
| all-white cover | 0.99 | 1.00 | (degenerate) | | |
| 3D box on white | 0.59 | 0.78 | 0.83 | 0.72 | 0 |
| 3D box on grey gradient | 0.61 | 0.80 | 0.77 | 0.69 | 0 |
| 3D box on near black | 0.65 | 0.88 | 0.69 | 0.58 | 0 |
| 3D box, transparent PNG | 0.65 | 0.88 | 0.69 | 0.58 | 0 |

`fill` = non-backdrop pixels divided by their bounding box; `corner1/2` = the largest two backdrop shares among the four corner squares (20 percent of the shorter bbox side) of that bounding box. **Starting verdict rules (tuning values in settings, applied at read time to the stored features):** flat when `fill >= 0.97` and `corner1 <= 0.15`; 3D when `fill <= 0.93` and `corner2 >= 0.40`; otherwise unsure. A degenerate picture (backdrop share at least 0.97) is flat (nothing to confuse). Show a continuous score on the review sheet, for example `score = clamp(0.5 * (1 - fill) / 0.25 + 0.5 * corner2 / 0.7, 0, 1)` (about 0 for flat, about 0.85 for the synthetic 3D shots).

**Known blind spots to expect in review:** (a) a flat cover that is a solid colour with a round or irregular motif has the same silhouette as a product shot (prototype: near-black and green covers with a circle read as non-rectangular); it lands in 3D or unsure and leans to the main image, which D-02 accepts. (b) "Almost flat" renderings (front face squarely visible with a thin spine and top edge) have fill about 0.93 to 0.97 and land in unsure, which also leans to the main image. (c) Real backdrops have JPEG noise, vignettes and soft shadows that synthetic fixtures lack; the tolerance 18 and cluster shares are unproven on real photos. The review sheet is the instrument; features are stored so thresholds can change without re-downloading.

### Pattern 5: Chooser (verdict derived at read time)

Verdict per candidate: `flat | unsure | 3D | none` (none = missing, failed or undecodable).

| A (owned version) | B (main) | Chosen | Why |
|---|---|---|---|
| flat | any | A | Owner's edition, a flat cover |
| unsure or 3D | flat | B | D-01, D-02 |
| unsure or 3D | unsure or 3D | A | D-03 (owner's 3D shot beats invented art; when both are unsure there is no known flat cover) |
| unsure or 3D | none | A | D-03 "main image missing" |
| none | usable (any verdict) | B | Only usable picture |
| none | none | generated cover | Last resort |

A and B being the same URL is one candidate with one verdict. The chosen image supplies art, spine colour and edge colours. **Shape source:** only a chosen image with verdict `flat` may set a box shape (D-07); `unsure` and `3D` never do.

**Pose must not read the verdict, directly or through the box size (D-03).** [VERIFIED: Orientation.cs lines 107-121 and 126-137] `Orientation.Decide` takes `item.Box`, and both the cover chance (`CoverChanceBasisPoints(share, SizeClassOf(item.Box))`) and the flat eligibility (`CanLieFlat(item.Box)`) read it. [VERIFIED: Orientation.cs lines 38-41] the size classes switch at `SmallBelowHeightMm = 200` and `StandardBelowHeightMm = 320`. A cover-ratio rebuild of the box (Pattern 6) changes `HeightMm`, so a changed verdict could cross a class boundary and flip a box between cover and spine, which D-03 forbids. Therefore give `CabinetItem` a second, verdict-free size for the pose decision (for example `PoseHeightMm`, taken from real sizes, else the estimate, else the default, never from a cover) and have `Orientation` read that; the drawn `Box` may use the cover shape. Test: for every sample, switching any game's verdict leaves its pose unchanged.

### Pattern 6: Shape chain (IMG-03) and size estimate (D-08)

Order: (1) real dimensions via the existing mapper (inch factor, plausibility bounds), (2) flat cover shape, (3) estimate or default.

- **Existing mapper values** [VERIFIED: Cabinet.Domain/Collection/BoxFromVersion.cs lines 12-24]: `MillimetresPerUnit = 25.4`, `MinFrontMm = 50`, `MaxFrontMm = 700`, `MinDepthMm = 5`, `MaxDepthMm = 300`; defaults line 53 `kind == ItemKind.Expansion ? new BoxDimensions(200, 260, 40) : new BoxDimensions(225, 300, 60)`.
- **Disagreement test (D-07), orientation-insensitive:** `sizesRatio = min(w,h)/max(w,h)`, `coverRatio` likewise; sizes are "wrong" when `abs(sizesRatio - coverRatio) / coverRatio > 0.12` (starting value, setting `Images:ShapeMargin`). Then keep the sizes' **area** and depth and rebuild the front from the cover ratio (`h = sqrt(area / ratio)`), which is also robust to swapped width and length. Test: 6.3 x 8.27 in (ratio 0.76) agrees with a 0.75 cover; a 0.62 cover with the same sizes does not.
- **Orientation (Open Question 1):** the current convention makes the longer front side the standing height [VERIFIED: BoxFromVersion.cs lines 42-43 `var height = Math.Max(first, second); var width = Math.Min(first, second);`], measured at 97 percent consistent with BGG's length/width [VERIFIED: 03-SPIKE-OUTCOME.md "34 of the 35 base items"]. A landscape flat cover would then sit in a portrait box with large bars. Recommended: when the chosen image is flat and landscape, let the cover orient the front (width the longer side). This is a mapper-level decision that does not touch the engine; confirm with the owner in the review.
- **Estimate (no sizes):** score from stored inputs; classes and bands are starting values, to be tuned on the sheet. Proposed front-height classes from the measured ranges [VERIFIED: 03-SPIKE-OUTCOME.md base medians 6.3 x 8.27 x 2.09 in, ranges width 3.7 to 12.52, length 4.53 to 17.01, depth 0.79 to 7.56]: compact about 100 x 140 x 30 mm, small 150 x 200 x 45, standard 225 x 300 x 60 (today's default), large 300 x 300 x 75, extra large 300 x 420 x 90. Score = weight band (unknown 1, under 1.5 is 0, 1.5 to 2.5 is 1, 2.5 to 3.5 is 2, 3.5 and up is 3) + play-time band (30 or less 0, up to 60 1, up to 120 2, longer 3) + player band (up to 2 is 0, up to 4 is 1, more is 2). **Stability:** store the assigned class in `GameDetails.SizeClass`; at refresh keep it unless the new score is more than one band-fraction (say 0.5 of a point) beyond the boundary (hysteresis), and bump `SizeModelVersion` to force a clean re-evaluation after a model change. Test: weight 2.74 to 2.76 and play time 59 to 61 do not change the box; a real change does. With a flat cover, class gives the longer side and the cover ratio gives the other. Expansions keep their smaller defaults refined by the same class (expansion weight is often 0, so default applies).
- **Where fit is computed:** the engine scales boxes on entry keeping proportions [VERIFIED: CabinetLayoutEngine.cs lines 164-194 `Clamp`], so art fit (`width` when the art is relatively wider than the box front, `height` when narrower, `exact` within about 1 percent) must be computed from the placement's final millimetres, at `Place(...)`, not in the mapper.

### Pattern 7: Spine colour and contrast (CAB-03)

- **Extraction:** exclude the backdrop mask (Pattern 4). If fewer than 3 percent of pixels remain, use the overall mean of the opaque pixels (so a white cover gets a light grey or cream spine, never an error or the palette). Otherwise bucket remaining opaque pixels to 4 bits per channel, score each bucket by the pixel count of its 3x3x3 neighbourhood times `(0.25 + 0.75 * mean saturation)`, take the best bucket and use the **mean of the pixels in that neighbourhood**. Prototype results: red box on white gives about (189, 30, 40), blue on grey gives (40, 90, 169), gold on near-black gives (218, 168, 29), transparent-PNG green box gives (59, 148, 69): recognisable, not toned down.
- **Contrast pair:** [VERIFIED: Cabinet.UnitTests/Layout/SpinePaletteTests.cs] the shade blend is `channel * (100 - percent) / 100 + shadeChannel * percent / 100` on gamma-encoded 0 to 255 channels, with the shade `#140a04` at up to 20 percent, applied to both background and text, WCAG luminance with the sRGB breakpoint 0.04045. A pair passes when the ratio is at least 4.5 at shade 0 and at shade 20 percent. [VERIFIED: scratch run] checking 0, 5, 10, 15 and 20 percent gave identical results to checking only the two ends for all 4,913 grid colours, so the two-end check is sufficient.
- **Nudge algorithm (verified by running it):** try white then black text; if neither passes, convert to OKLab, hold hue and chroma, move OKLCH lightness in 0.005 steps up and down alternately, take the smallest move that makes either text colour pass; if the result is out of sRGB gamut, shrink chroma by 3 percent steps until it fits. Over the 17-step grid (0, 16, ..., 240, 255 per channel; 4,913 colours): **white or black: 3,746 colours (76 percent; the contract says 77 percent) need no nudge, mean nudge 0.0076, maximum 0.065, none unfixable.** With white or `--ink` the maximum is 0.090 (mean 0.0149), which is why pure black is the dark text. Example: (51, 153, 51) passes with black at no shade (5.75) but not under the full shade, so it becomes (64, 164, 63), a 0.035 lift. Tests should assert "at least 70 percent need no nudge" and "maximum nudge at most 0.07" rather than exact percentages.
- **Pure maths lives in Domain** (`SpineColour`), so the same function validates stored pairs on read: a pair that is not six-digit hexadecimal or fails the two-end check is ignored and the palette tone is used.
- **Edge colours** are fill colours, not text-bearing, so they need no contrast rule.

### Pattern 8: Expansion pairing without an engine change (D-14, D-16)

[VERIFIED: CabinetLayoutEngine.cs lines 388-393] the engine's `FamilyIndex` picks the parent as the **lowest game id** among `ExpansionOf` entries that are owned (`.Where(firstBaseById.ContainsKey).Order()`). D-14 wants the **lowest collection id**. Resolve in the mapper, so the engine and its goldens keep their tie-break: for an expansion entry, take the linked base game ids from `GameDetails.ExpandsGames`, keep those that are owned as `ItemKind.Base` entries in the snapshot (D-16: only entries from the base call), pick the owned base with the lowest collection id among all its entries, and pass **only that one** `BaseGameRef(id, title)`. When none is owned, pass all linked refs so the engine names the lowest game id for the orphan label ("Expansion for {base}"), matching the contract. Adding a later base game (higher collection id) cannot change an existing choice. Do not read `boardgamecompilation` or any non-`boardgameexpansion` link (D-15).

### Pattern 9: Engine changes (one deliberate version bump)

- Bump `LayoutVersion` from 9 to 10 [VERIFIED: CabinetLayoutEngine.cs line 18 `public const int LayoutVersion = 9;`] and re-record goldens under `Cabinet.UnitTests/Layout/Golden/` with `CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"` [VERIFIED: LayoutGoldenTests.cs lines 21-23].
- One-line floors: add `OneLineLabelPx = 15` beside the existing 24, 36, 29 [VERIFIED: ReadabilityFloor.cs lines 10, 13, 16 `TapTargetPx = 24`, `TwoLineLabelPx = 36`, `TwoLineSpinePx = 29`]. Desktop one-line floor is already `DesktopMinBoxThicknessMm = 34` [VERIFIED: SectionDesigns.cs line 22]. Today's upright and orphan minimums derive from the two-line pixels (`MinOrphanHeightMm`, `MinUprightExpansionWidthMm` at lines 46-47 and 77-78); change the **minimums to the one-line floor** and keep the two-line values as **thresholds** for a new `ShowBaseLine` flag on `Placement` (computed from millimetres, deterministic). Layers: `MinLayerHeightMm = 40` desktop [VERIFIED: SectionDesign.cs line 59 `public int MinLayerHeightMm { get; init; } = 40;`; SectionDesigns.cs lines 41 and 72 `MinLayerHeightMm = 40,` on both designs], target 34 desktop and 59 phone (59 is already the tap-floor).
- `SectionDesign.Validate` has checks that name these values [VERIFIED: SectionDesign.cs lines 222-233, 249-254] (`tallestFloor`, `thickestFlat` use `MinOrphanHeightMm`); lowering the orphan minimum changes what they assert, so update or add tests for the new derived values.
- `CabinetItem` gains the verdict-free pose size (Pattern 5) and `Orientation` reads it instead of `Box.HeightMm`; this is also a deliberate part of the version bump.
- New placement data (additive, null omitted by `LayoutJson`): `Colour`, `Art`, `ShowBaseLine`. `Placement` is a 15-parameter positional record [VERIFIED: CabinetLayout.cs lines 44-59], so every constructor call site (`Place`, the layer and marker `new Placement(...)` in `CubbyArrangement`) must be touched; prefer adding optional trailing parameters with defaults.
- Collection `Version` hash [VERIFIED: SnapshotMapper.cs lines 49-51 hashes `CollectionId|BggId|Kind|Title|Box.WidthMm|HeightMm|DepthMm`] must also hash `ExpansionOf`, chosen art file names, colour pair, edge colours and the settings fingerprint (verdict thresholds, shape margin).
- Layout cache key already includes `LayoutVersion`, options fingerprint, collection version and profile [VERIFIED: CollectionStore.cs line 79]; add the art-settings fingerprint to the version hash, not to the ETag.

### Pattern 10: Phone density (measure before building)

A scratch measurement used the real `CabinetLayoutEngine` with a collection shaped like the measured spike data (30 percent of base and 40 percent of expansion boxes at the default size, the rest sampled through the measured ranges; synthetic):

| Sample | Phone sections | Empty rows in non-last sections | Desktop sections |
|---|---|---|---|
| 65, all default sizes | 3 | 2 | 2 |
| 65, spike-shaped sizes | 3 | 0 | 2 |
| 400, all default sizes | 19 | 26 | 10 |
| 400, spike-shaped sizes | 13 | 1 | 8 |

So real sizes alone remove most empty rows and cut phone sections; the cause of the empty rows is mostly the oversized default box (225 x 300 x 60, with phone rows of 260 mm that cannot take it). **Plan:** make the measurement a test-support helper first (non-last empty rows and section counts on the 65 and 400 samples with a size mix), land real sizes, measure, and only then pick the approach: (1) retune phone row heights (data only), (2) trim empty trailing rows of earlier phone sections (a new stability exception that must be documented and tested as its own case), (3) leave to per-location cabinets. The acceptance in the contract (no non-last section with more than one empty row; fewer than 12 sections for 400) is plausible with real sizes alone; do not build exception (2) unless the measurement fails it.

### Pattern 11: Renderer (strict CSP preserved)

- Art cover: `<img class="cover-art">` created with `createElement`, `src`, `width`, `height`, `alt = ''`, `loading = 'lazy'`, `decoding = 'async'`, `draggable = false` as properties; the failure path is `addEventListener('error', swapToGenerated, { once: true })`. Edge colours reach CSS only through `style.setProperty('--edge-t', ...)`. Existing code already follows this style [VERIFIED: render.js lines 161-171 use `style.setProperty('--x', ...)` and `button.style.setProperty('--bg', ...)`]. Do not set `data-pattern` on art covers so the pattern `::before` does not draw.
- Colour: prefer `placement.colour` when present and valid (`#rrggbb`), else the palette tone [VERIFIED: render.js lines 166-171 current tone application]. Revalidate shape of values in JS defensively; the server already validated.
- `+N more` name: copy change `+{N} more expansions for {base}` and `+1 more expansion for {base}`; keep the visible `+{N} more` [VERIFIED: render.js lines 100-102 `copy.moreName(placement.moreCount, baseTitle)`; the copy function lives in `copy.js`].
- `showBaseLine`: renderer draws the second line only when the layout says so (today `hasBaseLine` decides by kind alone [VERIFIED: render.js lines 76-78]).
- Tests: extend `build/tests/page-scripts.test.mjs`, which already imports `render.js` and exercises `renderCabinet` with a fake DOM [VERIFIED: page-scripts.test.mjs lines 1395-1403].

### Anti-Patterns to Avoid

- **Verdict, choice or shape as the only stored truth:** retuning would need a re-download. Store raw facts.
- **Flood fill with drift from the seed colour:** leaks through gradients (prototype failure).
- **Rebuilding the snapshot from the fetch alone** (existing behaviour): drops enrichment on the next run.
- **Using the BGG pacer or `BggAuthHandler` for the CDN:** wrong gap floor; the token must not travel (the handler only attaches it for `boardgamegeek.com` over HTTPS [VERIFIED: BggTransport.cs lines 57-59 `string.Equals(uri.IdnHost, BggOptions.ApiHost, StringComparison.OrdinalIgnoreCase))`], but the image client should not register it at all).
- **`SKTypeface.Default` for text:** draws nothing with the NoDependencies native assets.
- **Obsolete SkiaSharp 4 APIs under warnings-as-errors:** `SKCanvas.DrawBitmap(SKBitmap, float, float, SKPaint)` and `SKPath.MoveTo/LineTo/Close` produced CS0618 here; use `DrawBitmap(bitmap, SKRect, SKSamplingOptions)` and `SKPathBuilder` + `Detach()` (both verified to compile and run).
- **Browser-side colour or shape analysis, canvas reads, `style` attributes, inline handlers:** forbidden by the renderer constraints.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Image decode with size guard | Manual header parsing | `SKCodec.Create` + `codec.Info` + `GetScaledDimensions` + `SKBitmap.Decode(codec, info)` | Built-in, DCT-scaled, null on garbage [VERIFIED: scratch run] |
| WebP encode with alpha | Custom encoder | `SKImage.Encode(SKEncodedImageFormat.Webp, 80)` | Alpha preserved [VERIFIED: scratch run] |
| Serving cache files | Custom endpoint reading files | `UseStaticFiles` + `PhysicalFileProvider` + `OnPrepareResponse` | Range requests, ETag, path-traversal safety |
| Atomic writes | Ad hoc temp files | Existing `AtomicJsonFile.WriteAtomically` (generalise for binary) | Same crash-safety as the snapshot [VERIFIED: AtomicJsonFile.cs lines 14-37] |
| XML reading | New reader settings | Copy the `BggCollectionParser` reader settings and `CleanTitle` | DTD prohibited, size cap, control-character stripping already tested |
| Time-based tests | Real delays | `FakeTimeProvider` + `NoWaitPacer` + `ScriptedBggHandler` | Existing harness [VERIFIED: SyncHarness.cs, ScriptedBggHandler.cs] |
| Colour space maths | A colour library | About 60 lines (sRGB to OKLab and back, gamut shrink) in `SpineColour` | Verified against the contract's grid; avoids a dependency |
| Review image drawing | HTML + browser on the server | SkiaSharp canvas with `SKTypeface.FromFile(<ttf>)` | The server has no browser and no fonts |

**Key insight:** every hard part here is either already a framework feature (scaled decode, static files, WebP) or a small pure function with a measurable contract (contrast, nudge, fit, disagreement margin). Put the pure parts in Domain so they run in milliseconds in the existing test runner.

## Common Pitfalls

### Pitfall 1: Enrichment is lost on the next sync
**What goes wrong:** the hourly run replaces the snapshot with a fresh collection and every game reverts to a generated cover.
**Why:** `SyncRunner` builds the snapshot from the fetched items only.
**How to avoid:** merge `Games` and `Images` from the previous snapshot by key before saving; test "second run with unchanged BGG answers keeps details and images and makes zero `thing` and image requests".
**Warning signs:** the version hash changes every run; request counts never drop to the two collection calls.

### Pitfall 2: Pacer floor makes images take an hour
**What goes wrong:** reusing `RequestPacer` forces 5 s per image.
**How to avoid:** a separate pacer with its own floor (start 1 s); unit-test the gap with `FakeTimeProvider`.

### Pitfall 3: Text draws nothing in the review sheet
**What goes wrong:** `SKTypeface.Default` has an empty family and rendered zero pixels in the scratch run; the server also has no fonts (read-only probe: no `/usr/share/fonts/truetype`, no `fc-list`).
**How to avoid:** the tool loads an explicit TTF (`SKTypeface.FromFile`), searches known paths, and exits with a clear message when none exists. Decide in Open Question 2 how the font gets onto the server. Verified locally that `FromFile` on DejaVu Sans works.

### Pitfall 4: The backdrop leaks into the picture
**What goes wrong:** gradient art is classed as backdrop, so spine colour and detector go wrong.
**How to avoid:** border-cluster mask with a minimum ring share; unit tests with full-bleed gradient, framed, transparent and all-white fixtures.

### Pitfall 5: Solid-colour covers lose their field colour
**What goes wrong:** a cover that is one colour with a small logo has that colour excluded as "backdrop" and the spine takes the logo's colour (prototype: green field with a darker circle gave the circle's green).
**How to avoid:** accepted limitation to watch on the sheet; if it shows up, the lever is to exclude a chromatic backdrop only when the verdict is not flat (Open Question 4).

### Pitfall 6: Transparent PNG product shots
**What goes wrong:** many 3D shots are PNG with transparent surrounds (`filters:format(png)` appears in the live URLs). Edge colours of a transparent strip are undefined and the fit shows nothing behind the art.
**How to avoid:** decode Unpremul; treat alpha under about 8 percent as backdrop; edge colours alpha-weighted with the spine-colour fallback; WebP keeps alpha.

### Pitfall 7: D-14 versus the engine's tie-break
**What goes wrong:** passing every linked base to the engine picks the lowest game id, not the lowest collection id.
**How to avoid:** the mapper passes only the chosen owned base (Pattern 8); test that adding a base with a lower game id and a higher collection id leaves the expansion where it was.

### Pitfall 8: A snapshot rollback deletes the image cache
**What goes wrong:** older release sets the v2 file aside; the next upgrade prunes every file as unreferenced.
**How to avoid:** prune only after a successful commit and only files older than a grace period; keep file names content-hashed so a rebuilt record can find them.

### Pitfall 9: Enrichment times out the run
**What goes wrong:** a 400-game first run exceeds the 10-minute limit and is recorded as a timeout failure although progress was made.
**How to avoid:** per-run budgets and an enrichment deadline shorter than the run limit; commit per batch; a deadline stop is a normal end.

### Pitfall 10: Cover pattern or inset edge paints over art
**What goes wrong:** `.placement[data-kind="cover"]::before` pattern and the 1px light inset shadow would draw over or under the picture.
**How to avoid:** no `data-pattern` on art covers, `data-art` rule that drops the `::before` and the inset shadow; the cubby shade stays the only overlay (owner confirmed).

### Pitfall 11: A verdict change flips a box between cover and spine
**What goes wrong:** the cover-ratio rebuild changes the box height, the size class crosses 200 or 320 mm, and the pose probability changes; a tuning round then rearranges the cabinet and breaks D-03.
**How to avoid:** a verdict-free pose size on `CabinetItem` (Pattern 5) and a test that flipping verdicts leaves every pose and every cubby arrangement unchanged.

### Pitfall 12: Phone one-line floor misread
**What goes wrong:** expecting thinner phone base spines. The phone one-line floor is the 24 px tap target (59 mm at the narrowest phone), which equals today's minimum.
**How to avoid:** the contract states the honest effect: only phone upright expansions (71 to 59 mm), orphans (89 to 59 mm) and layers change; desktop gains most.

## Code Examples

Examples follow the repository rules (`///` only, no planning references). Items marked verified were compiled and run against SkiaSharp 4.153.1 in a scratch project.

### Bounded, scaled decode (verified)

```csharp
/// <summary>Reads the pixel size without decoding, refuses oversized pictures, then decodes at roughly the wanted width.</summary>
public static SKBitmap? DecodeBounded(byte[] bytes, long maxPixels, int wantedWidth)
{
    using var codec = SKCodec.Create(new SKMemoryStream(bytes));

    if (codec is null || (long)codec.Info.Width * codec.Info.Height > maxPixels)
    {
        return null;
    }

    var scale = Math.Min(1f, (float)wantedWidth * 2 / codec.Info.Width);
    var size = codec.GetScaledDimensions(scale);
    var info = new SKImageInfo(size.Width, size.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);

    return SKBitmap.Decode(codec, info);
}
```

### Contrast pair under the furniture shade (verified against the existing palette test arithmetic)

```csharp
/// <summary>Whether text on a background reaches 4.5 to 1 with no shade and under the strongest shade.</summary>
public static bool PassesUnderShade(double[] background, double[] text)
{
    return new[] { 0.0, SpinePalette.MaxShadePercent / 100.0 }
        .All(alpha => Ratio(Blend(background, alpha), Blend(text, alpha)) >= 4.5);
}
```

### Lightness nudge (verified; grid result in Pattern 7)

```csharp
/// <summary>Moves OKLCH lightness by the smallest 0.005 step, either way, that lets white or black text pass; chroma shrinks only to stay in gamut.</summary>
public static double[] Nudge(double[] rgb)
{
    if (PassesWithEither(rgb))
    {
        return rgb;
    }

    var (lightness, a, b) = Oklab.From(rgb);
    var chroma = Math.Sqrt((a * a) + (b * b));
    var hue = Math.Atan2(b, a);

    for (var step = 1; step <= 200; step++)
    {
        foreach (var direction in new[] { 1, -1 })
        {
            var candidate = Oklab.ToGamut(lightness + (direction * step * 0.005), chroma, hue);

            if (candidate is not null && PassesWithEither(candidate))
            {
                return candidate;
            }
        }
    }

    return rgb;
}
```

### Art fit, computed where the final box size is known

```csharp
/// <summary>How the picture meets the box front: width when bars sit above and below, height when they sit left and right.</summary>
public static ArtFit For(int boxWidthMm, int boxHeightMm, int artWidthPx, int artHeightPx)
{
    var box = boxWidthMm / (double)boxHeightMm;
    var art = artWidthPx / (double)artHeightPx;

    if (Math.Abs(box - art) / art <= 0.01)
    {
        return ArtFit.Exact;
    }

    return art > box ? ArtFit.Width : ArtFit.Height;
}
```

### Static serving of the cache (verified mapping of `.webp`)

```csharp
/// <summary>Serves the image cache from the state directory under /art as immutable files.</summary>
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(artDirectory),
    RequestPath = "/art",
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "public, max-age=31536000, immutable";
        context.Context.Response.Headers.XContentTypeOptions = "nosniff";
    },
});
```

### `thing` request shape

```text
GET thing?id=<up to 20 ids joined by commas>&stats=1
```

Never add `versions=1`, `comments`, `ratingcomments`, `videos` or `marketplace`.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ImageSharp recommended in project-level research | SkiaSharp 4.153.1 | earlier phase decision | The stack research and CLAUDE.md still name ImageSharp; this phase follows the decision and the lock file |
| `SKCanvas.DrawBitmap(bitmap, x, y, paint)` | `DrawBitmap(bitmap, SKRect, SKSamplingOptions)` | SkiaSharp 4.x | CS0618 is an error here |
| `SKPath.MoveTo/LineTo/Close` | `SKPathBuilder` then `Detach()` | SkiaSharp 4.x | Same |
| `SKFilterQuality` | `SKSamplingOptions` | SkiaSharp 3.x | Already used by `ImageSmoke` |
| Version image fields via `versions=1` on `thing` | `version=1` on the collection call | project rule | Per-edition image and dimensions arrive with the owned version only |

**Deprecated/outdated:** the two-line minimums in `SectionDesigns` as floors (they become two-line thresholds); `--arch-shade-alpha` (retired by the design contract).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The collection's version element carries the owned edition's own `image` (`version/item/image`), and the item-level `image` is the game's main image | Pattern 3 | If the item-level image is actually the version image, candidate B must come only from `thing/image` (the design already prefers that). Wave 0 shape check settles it |
| A2 | The image CDN needs no special `User-Agent` or `Referer`, and a 1 s gap is acceptable | Pattern 3 | Downloads fail (403/429) or the CDN throttles; the failure path leaves generated covers. Wave 0 check measures status codes and headers |
| A3 | `thing` without a `type` parameter returns expansions as well as base games | Pattern 2 | Expansion ids come back empty and stay orphans. Wave 0 check includes one expansion id |
| A4 | A change of the 20-id cap or of `stats=1` behaviour has not happened since the project research | Pattern 2 | Batches fail with "too many items"; batch size becomes a setting |
| A5 | BGG originals are at most a few MB and 36 MP | Pattern 3 | Caps refuse legitimate images; caps are settings and the check prints sizes |
| A6 | Typical 480 px WebP at quality 80 is tens of KB | Pattern 3 | Disk and page weight estimates off; the review reports the real bytes |
| A7 | The starting detector tolerances (18, 55 percent, 10 percent) and verdict thresholds (0.97/0.15, 0.93/0.40) suit real photos | Pattern 4 | More unsure or wrong verdicts; settings plus stored features make retuning cheap |
| A8 | Size-class values and bands are plausible | Pattern 6 | Boxes look wrong in review; tuning values, hysteresis protects stability |
| A9 | Installing a small font package on the container is acceptable | Open Question 2 | Review sheet cannot draw text on the server; fallback is building the sheet locally from a copy of the cache |
| A10 | `boardgamecompilation` is BGG's "contains" relation | Pattern 8 | None for this phase: the pairing reads only `boardgameexpansion` links with `inbound="true"` |

## Open Questions

1. **Landscape fronts.**
   - Known: the mapper forces the longer front side to be the standing height, and a 97 percent of items have length not smaller than width. Many real box fronts are landscape.
   - Unclear: whether the owner wants landscape covers drawn wide.
   - Recommendation: orient the front from a flat cover when there is one (Pattern 6), otherwise keep the convention; show both in the first review round.
2. **Fonts for the review sheet on the server.**
   - Known: no TrueType fonts and no `fc-list` on the container; `SKTypeface.Default` draws nothing.
   - Unclear: installing a font package via provisioning versus vendoring a freely licensed TTF versus building the sheet on a copy of the cache locally.
   - Recommendation: add a small font package to the provisioning package list and make the tool search known paths with a clear error. It is one line and keeps the tool server-side as decided.
3. **Version image truth and CDN behaviour.**
   - Recommendation: a first, shape-only server check in the style of the existing access-check script that prints only counts and class names: how many items carry `version/item/image`; how many items have item-level `image` equal to version image (as a count); URL host and path-form classes (signed `__original`, older `/images/pic`, protocol-relative); `thing` answers for one base and one expansion id (does the expansion return, inbound link count); CDN status codes, content types, byte sizes and `Content-Length` presence for a handful of downloads, with and without a `User-Agent`; whether any downloaded picture exceeds the caps. Never print a URL or title.
4. **Solid-colour covers versus backdrop exclusion.** Review-sheet item; lever described in Pitfall 5.
5. **Fake image host and Development-only host override.** The fake BGG currently writes `https://example.org/...` image URLs [VERIFIED: Cabinet.FakeBgg/BggXml.cs lines 176, 244 and 278, e.g. `new XElement("image", $"https://example.org/images/{item.ObjectId}.jpg")`]. Local review needs URLs that point at the fake itself over HTTP on loopback, so the allowlist and the HTTPS rule need a Development-only override (the same pattern as `Bgg:BaseUri`, honoured only in Development [VERIFIED: BggSettings.cs lines 35 and 42]) and `BggXml` needs the fake's own base address passed in. Generating pictures needs SkiaSharp in `Cabinet.FakeBgg` (lock file change) or a generator shared from the unit-test project.
6. **Variant selection per cover.** The contract wants at least 1.5 times the CSS width and at most 480 px. Options: one URL per profile chosen by the server from the cover's width in millimetres at the smallest rendered width, or both URLs with `srcset` and a server-computed `sizes`. Recommendation: both widths in the layout JSON and `srcset`; verify at 390 and 1440 px in scratch Playwright and report total bytes.
7. **Per-run budgets.** Starting values in Patterns 2 and 3 are proposals; the owner's real run decides.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes | 10.0.112 (`global.json`) | none needed |
| Node | page script tests | yes | v24.19.0 | none needed |
| Playwright (npm, scratch) | review screenshots | registry reachable, not installed in the repo | 1.63.0 | install in a scratch directory as in the layout phase |
| SkiaSharp native on the server | image pipeline | yes (proven by the image smoke check in an earlier release) | 4.153.1 | none needed |
| Outbound HTTPS from the container to the image CDN | image download | yes: root request answered HTTP 400 through CloudFront in 0.2 s; the output firewall policy is accept | n/a | none needed |
| Fonts on the container | review sheet text | **no** (no truetype directory, no `fc-list`) | n/a | install a font package; or build the sheet locally from a copy of the cache |
| Container resources | pipeline concurrency | 1 CPU, about 1 GB RAM (about 0.6 GB reclaimable), about 6 GB free disk | n/a | keep decode serial; scaled decode peaks about 190 MB in the scratch test |
| `dotnet` ASP.NET runtime on the server | host | yes (existing release runs) | n/a | none needed |

[VERIFIED: read-only probe over SSH this session; the output firewall chain is `policy accept` in deploy/nftables/cabinet.nft.in, and the unit has `StateDirectory=cabinet` with `ProtectSystem=strict` in deploy/systemd/cabinet.service.]

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** fonts (see Open Question 2).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.1 on the Microsoft Testing Platform runner; FluentAssertions 8.11.0; Node built-in test runner for page scripts |
| Config file | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`), `Cabinet.slnx` |
| Quick run command | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Enrichment"` (and `Images`, `Layout`, `Snapshot`) |
| Full suite command | `dotnet test --solution Cabinet.slnx --no-restore` then `node --test build/tests/page-scripts.test.mjs` then `build/lint.sh` |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SYNC-06 | `thing` parser: designers, mechanics, min age, play time gaps, ratings, 0-as-unknown, expansion inbound links only, caps, entity-clean text, hostile XML (DTD, oversize) | unit | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Enrichment"` | Wave 0 |
| SYNC-06 | Client: 20-id batching, at least the gap between calls, 429/5xx retry once, auth header only to the API host, no `versions=1` in any request | unit (scripted handler) | same filter | Wave 0 |
| SYNC-06 | Planner: new first, refresh older than 7 days oldest first, per-run cap, unchanged second run makes zero calls | unit | same filter | Wave 0 |
| SYNC-06 | Pairing: lowest collection id among owned bases, standalone expansion stays base, orphan names lowest game id, "contains" links ignored, stability when a base is added | unit | `Category=Snapshot` | Wave 0 |
| SYNC-06 | Snapshot v2 round trip, v1 file loads, newer file set aside, enrichment survives a refreshed collection | unit | `Category=Snapshot` | extend `SnapshotStoreTests.cs` |
| SYNC-06 | End to end: fake BGG to snapshot to layout JSON shows expansions beside bases; failed `thing` batch leaves the collection visible | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj` | Wave 0 (`EnrichmentPipelineTests.cs`) |
| SYNC-07 | Downloader: https only, protocol-relative accepted, host allowlist exact, redirect re-check, byte cap, `Content-Length` refusal, token never sent, own gap | unit | `Category=Images` | Wave 0 |
| SYNC-07 | Analyzer: pixel cap before decode, undecodable bytes, alpha PNG, variant sizes and no upscale, hashed names, atomic write, prune with grace | unit | `Category=Images` | Wave 0 (`ImageSmokeTests.cs` exists as a base) |
| SYNC-07 | Served output contains no BGG or CDN host in layout JSON or page; `/art/...` served with `immutable`; a browser never requests a foreign host (scratch Playwright audit) | integration + scratch | integration project; scratch script | Wave 0 |
| IMG-01 | Detector on generated fixtures (flat, framed, banner, white, near-black, green, 3D on white, grey, black, transparent, undecodable) gives the expected verdict | unit | `Category=Images` | Wave 0 (`SyntheticArt` generator) |
| IMG-01 | Chooser table, every row; flipping any verdict leaves every pose and cubby arrangement unchanged (pose size is verdict-free) | unit | `Category=Enrichment` | Wave 0 |
| IMG-03 | Shape chain order, disagreement margin both sides, orientation-insensitive ratio, area-preserving rebuild, 3D never used | unit | `Category=Snapshot` | extend `BoxFromVersionTests.cs` / new `BoxShapeTests.cs` |
| IMG-03 | Estimate hysteresis: small drift in weight, time and players leaves the box unchanged; real change moves it | unit | same | Wave 0 |
| IMG-03 | Engine: floors and `showBaseLine` from millimetres, art fit values, goldens re-recorded, layout version file updated | unit (golden) | `CABINET_UPDATE_GOLDENS=1 ... --filter-trait "Category=Layout"` once, then plain | extend existing |
| CAB-03 | Contrast: 17-step grid passes at both ends after nudge; at least 70 percent need none; max nudge at most 0.07; invalid stored pair ignored on read | unit | `Category=Layout` | Wave 0 (`SpineColourTests.cs`) |
| CAB-03 | Extraction: red on white gives red; thick white frame ignored; transparent PNG; all-white gives overall mean; full-bleed gradient is not backdrop | unit | `Category=Images` | Wave 0 |
| CAB-03, IMG-01 | Renderer: art cover markup (`img` props, no text, `data-art`, `data-fit`), error swap to generated cover keeping colours, `--bg/--fg` from valid colour, ignored when invalid, `+N more` name starts with `+N more`, no `style` attribute | node unit | `node --test build/tests/page-scripts.test.mjs` | extend |
| All (visual) | Art fit with bars, thin spines, phone density, plinth, generated-cover line steps | manual + scratch Playwright DOM geometry | scratch script (not in CI) | owner review |

### Sampling Rate
- **Per task commit:** the trait-filtered unit command for the area touched (under 30 s) and `node --check` / page-script tests for JS work.
- **Per wave merge:** `dotnet test --solution Cabinet.slnx --no-restore`, `node --test build/tests/page-scripts.test.mjs`, `build/lint.sh`.
- **Phase gate:** full suite green, CI green on the pull request, fake-BGG screenshot round reviewed, then the server round (sheet, cover-share images), then owner approval of the deployed release.

### Wave 0 Gaps
- [ ] Server shape and CDN check script (committed, prints no URLs, titles or secrets) plus its self-test
- [ ] `Cabinet.UnitTests/Images/SyntheticArt.cs` generator (flat, framed, banner, white, near-black, mid-green, 3D on white, grey gradient, black, transparent, undecodable bytes)
- [ ] `Cabinet.UnitTests/Enrichment/` (parser, client, planner, pairing, chooser, shape, estimate)
- [ ] `Cabinet.UnitTests/Layout/SpineColourTests.cs`, art fit and floor cases in the layout tests
- [ ] `Cabinet.IntegrationTests/EnrichmentPipelineTests.cs` with a fake image host
- [ ] Fake BGG: `thing` data (designers, mechanics, ratings, links, images), version images, a fake image host, Development-only host override in the settings
- [ ] Density measurement helper
- [ ] Page-script cases for art covers and the marker name

## Security Domain

`security_enforcement` is on (ASVS level 2 in config).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (no accounts) | n/a |
| V3 Session Management | no | n/a |
| V4 Access Control | yes (limited) | the review sheet is an operator mode with no route; image cache served read-only from one directory |
| V5 Input Validation | yes | untrusted XML (DTD prohibited, caps), untrusted image bytes (codec size read before decode, byte and pixel caps), untrusted URLs (https, exact host allowlist), BGG text cleaned and rendered with `textContent` |
| V6 Cryptography | no new use | SHA-256 for content-hash names only (not a security control) |
| V12 Files and Resources | yes | hashed names only (no user-chosen path segments), `PhysicalFileProvider` confines to the cache directory, atomic writes, prune only inside it |
| V13 API and Web Service | yes | outbound calls only to the API host and the allowlisted CDN host; the token never travels to the CDN |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| SSRF through an image URL taken from an answer | Spoofing / Elevation | https only, exact host allowlist, redirects re-checked (max 3), no URL from request input; consider blocking private address ranges at connect time in the hardening phase |
| Decompression bomb or huge image | Denial of service | read size from `SKCodec.Info`, refuse above `MaxPixels`, refuse by `Content-Length` and streamed byte cap, serial decode, scaled decode |
| Hostile XML (entities, huge documents) | Tampering / DoS | same reader settings as the collection parser |
| Token leak to the CDN | Information disclosure | separate client with no auth handler; test that no image request carries `Authorization` |
| Stored text injection (designer, mechanic, base titles) | Tampering | `CleanTitle`, length and count caps, `textContent` only, `dir="auto"` as today |
| Path traversal through cache names | Tampering | names are generated hashes; static provider confines paths |
| Cache poisoning or stale art | Tampering | content-hashed immutable names; a changed image gets a new name |
| Personal data in logs or artefacts | Information disclosure | log counts and categories only (no URLs, titles, ids); review sheet outputs outside the repository |
| MIME sniffing of served files | Tampering | `X-Content-Type-Options: nosniff` on `/art` (global headers are the hardening phase's job) |

## Sources

### Primary (HIGH confidence)
- Repository code read this session (paths and line ranges cited inline): `Cabinet.Domain/Collection/*`, `Cabinet.Domain/Layout/*`, `Cabinet.Repository/Bgg/*`, `Cabinet.Repository/Storage/*`, `Cabinet.Repository/Images/ImageSmoke.cs`, `Cabinet.Service/Sync/*`, `Cabinet.Service/Program.cs`, `Cabinet.Service/Layout/*`, `Cabinet.Service/wwwroot/js/render.js`, `Cabinet.FakeBgg/*`, test projects and `build/tests/page-scripts.test.mjs`
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SPIKE-OUTCOME.md` (measured BGG facts)
- Local scratch runs against SkiaSharp 4.153.1 (codec, scaled decode, alpha WebP, obsolete API warnings, `SKTypeface` behaviour), against `Cabinet.Domain` (density measurement), and standalone maths (contrast and OKLCH nudge grid); ASP.NET `FileExtensionContentTypeProvider` mapping
- Read-only probe of the cabinet container (fonts, memory, disk, CDN reachability)

### Secondary (MEDIUM confidence)
- [github.com/MatthewThompson/arnak test_data (collection and game XML)](https://github.com/MatthewThompson/arnak) live-API fixtures: version `item` children (`thumbnail`, `image`, links, `name`, `width`, `length`, `depth`, `weight`), image URL forms, `thing` link types, `statistics/ratings/bayesaverage`, `averageweight`
- [github.com/syllant/bggcli tests/resources/collection.xml](https://github.com/syllant/bggcli) older protocol-relative image URLs
- `.planning/research/ARCHITECTURE.md`, `PITFALLS.md` (Pitfall 8, 9), `STACK.md` section on images

### Tertiary (LOW confidence)
- BGG wiki and forum pages (403 to automated fetch this session): the 20-id cap, `type` default and exact `thing` parameter semantics rest on earlier project research and fixtures, not on a page read today

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new packages; library behaviour verified by running it
- Architecture: HIGH for Domain/Repository/Service seams (read the code), MEDIUM for BGG field truth (A1, A3) until the shape check runs
- Detector and colour: MEDIUM-HIGH, features and maths validated on synthetic data, real-photo thresholds are tuning values
- Pitfalls: HIGH, most reproduced here

**Research date:** 2026-10-07
**Valid until:** 2026-11-06 (BGG behaviour and CDN rules can change without notice; re-run the shape check if a sync starts failing)

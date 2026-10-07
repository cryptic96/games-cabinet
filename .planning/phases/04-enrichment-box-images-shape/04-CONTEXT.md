# Phase 4: Enrichment, Box Images & Shape - Context

**Gathered:** 2026-10-07
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase turns the real but plain cabinet from the sync phase into one that looks like the owner's shelf. It covers:

- **Enrichment through `thing`:** every game carries player count, play time, weight, designers, mechanics, minimum age and BGG rating. Every owned expansion knows which base game or games it expands, so expansions move beside their owned base games in the deployed cabinet.
- **Box art:** downloaded during sync, downscaled and served from the site itself. A visitor's browser never requests anything from BGG.
- **Image choice:** the owned version's image when it is a flat front cover, otherwise the game's main image (D-01 to D-03). This is checked against the owner's real Dutch editions (D-04).
- **True box proportions:**
  1. the owned version's real dimensions;
  2. otherwise a flat cover's shape with an estimated size;
  3. otherwise a realistic default.

  A 3D shot's outline is never used.
- **Spine colour:** each spine takes its colour from its box art, ignoring plain backgrounds around product shots, with a legible title.
- **Folded taste items:**
  - the box-look polish carried over from the layout prototype;
  - phone cabinet density;
  - the "+N more" accessible-name fix.

Requirements: SYNC-06, SYNC-07, IMG-01, IMG-03, CAB-03.

Not in this phase:

- **Showing the details:** the pull-out and detail card, keyboard arrow navigation and the accessible list (the detail phase). The data is stored now and shown there.
- **Owner image overrides** (IMG-02), owner tools, owner-data storage and backups (the owner-tools phase). Until then, wrong image picks are accepted (D-05).
- **Filters and per-location cabinets** (the filters phase).
- **Selectable cabinet finishes** and the lit-cubbies toggle (stays unscheduled).
- **Public exposure, rate limits and resource caps** (the hardening phase).

</domain>

<decisions>
## Implementation Decisions

### Picking the box image
- **D-01: Two candidates per game.**
  - The two candidates are:
    - the owned version's image;
    - the game's own main BGG image.
  - For an expansion, "main image" means the expansion's own main image from `thing`, never its parent game's image.
  - A detector decides whether the version image is a flat front cover or a 3D/perspective product shot (a slanted box on a plain background).
  - Research confirms where the version image comes from: the collection item's `image` under `version=1`, or the version element itself.
- **D-02: When the detector is unsure, lean to the main image.**
  - Accepted cost: some flat Dutch covers are replaced by the main (often English or original) cover until the owner overrides them.
  - The threshold is a tuning value, set during the review (D-04).
- **D-03: When both candidates look like 3D shots, show the owned version's 3D shot, not a generated cover.**
  - The reason to swap to the main image is to get a flat cover. When the main image is not flat either, the owner's own edition is the more truthful picture.
  - The same applies when the main image is missing.
  - The generated cover from the layout phase stays the fallback only for a game with no usable image at all: missing, failed to download, or undecodable.
  - **Orientation never depends on image analysis.** Whether a box faces out is still decided by the layout settings and the game alone (layout D-06). A changed image or verdict never flips a box between cover and spine. The owner chose "3D shot anyway" over "avoid facing it out" partly for this reason.
- **D-04: The owner checks the picks on a review sheet.**
  - **What the sheet shows:** after a real sync on the server, Claude builds a contact sheet of every game. Each row shows:
    - both candidate images;
    - the detector's verdict and score;
    - the image that was chosen.
  - **How it reaches the owner:** Claude sends it as images. It is never committed, and no title, image or score from the owner's collection enters the repository or `.planning/`.
  - **Tuning:** the owner marks wrong picks, and the detector is tuned until the owner is happy overall.
  - Any committed tooling that builds the sheet holds no personal data and runs on the server, like the access-check script in the sync phase.
  - This is how roadmap success criterion 3 ("checked against the owner's real Dutch editions") is met.
- **D-05: Leftover misses wait for owner overrides.**
  - After tuning, a few wrong picks are accepted and fixed later with the owner's image overrides (IMG-02).
  - There is no stopgap override list, and the phase does not require zero misses.

### Fitting art to the box
- **D-06: Never crop.**
  - Box art is downscaled only. It is fitted whole inside the box front, and any leftover space is filled with a colour taken from the art.
  - No trimming of plain margins, no cropping to fill, no stretching, and nothing drawn over the art.
  - This is the cautious reading of BGG's "you may not modify the data in any way". The owner chose it over trimming plain borders or small crops.
  - A 3D shot shown face-out (D-03) is fitted the same way. The box front keeps the box's own shape (D-07), never the outline of the slanted box in the shot.
  - — **Reversibility:** reversible — presentation only; fitting happens at render time or in the downscaled variant, and the stored original choice does not change.
- **D-07: Real sizes win, unless they clearly contradict a flat cover.**
  - The order (IMG-03) is:
    1. the owned version's dimensions, which pass the existing plausibility check;
    2. otherwise a flat cover's shape;
    3. otherwise a realistic default.
  - BGG sizes are entered by users and are sometimes swapped or belong to another printing. When the front shape from the sizes differs from a flat cover's aspect ratio by more than a clear margin, the sizes are treated as wrong. The cover's shape is then used at the sizes' scale, with their depth.
  - The margin is a tuning value. Research picks a starting value, and the review confirms it.
  - A 3D shot's shape is never used for any of this.
- **D-08: Games without sizes get an estimated size from their own data.**
  - The estimate is a size class from weight, play time and player count: heavier and longer games get bigger, deeper boxes, and small card games stay small.
  - It is clamped to realistic bounds. A flat cover, when there is one, sets the shape.
  - **Measured coverage** (access check in the sync phase): about 30% of base games and 40% of expansions have no sizes.
  - **Stability constraint:** the estimate must not change the cabinet on small drift. Weight and play time are refreshed weekly (D-17), and a game whose weight moves slightly must not change box size and rearrange a cubby. Use coarse classes or rounded inputs so only a real change moves a box.
  - When no flat cover and no sizes exist, the realistic default per kind applies, refined by the same size class.
- **D-09: Thin boxes keep their true thickness down to a one-line minimum.**
  - **The floor:** a box is drawn at its real thickness down to the width that one line of title text needs, and never below the 24 px tap target.
  - **Below two lines' room:** the second line (for example "Expansion for …") is dropped, and the spine shows the title alone, ellipsised when needed.
  - **What it replaces:** today's two-line minimums, which make thin boxes look thicker than they are:
    - phone spines at least 59 mm wide;
    - upright expansions at least 64 mm on desktop and 71 mm on phone.
  - **Consequences:**
    - Derived minimums in the section designs and readability floor change, and so does the layout version.
    - The thick-expansion threshold (50 mm, a starting value) may need retuning.
    - The family accessible name still carries the full text.
  - **Folded polish items** resolved here: the cropped fourth-line ellipsis on small phone covers and the truncated two-line upright expansions.

### Spine look from the art
- **D-10: One solid colour per spine.** The art's main colour fills the spine, with the title on it. The structure is the same as today's palette spines, with no accent bands and no strip of art (a strip would crop, see D-06).
- **D-11: True to the art.**
  - The extracted colour is used as it is. Only its lightness is nudged, and only when needed, so the title reaches readable contrast.
  - Plain backgrounds around product shots are ignored when picking the colour (CAB-03): white, near-white or uniform borders and backdrops must not win.
  - The colour is computed once per image during sync and stored, never computed in the browser.
- **D-12: Title text is black or white,** whichever reaches at least 4.5 to 1 on the spine colour.
  - This must still hold under the strongest furniture shade the classic finish may lay over a box (`SpinePalette.MaxShadePercent`, 20%), as the placeholder palette does today.
  - The placeholder palette stays the colour source for games with no usable art.
- **D-13: The default cover share is picked from screenshots.**
  - The owner sees screenshots of the real collection at two or three cover shares (for example about 1 in 5, 1 in 4 and 1 in 3) and picks one.
  - The pick becomes the committed default of `Layout:CoverSharePercent` in appsettings. It stays a server setting with an env-file override and a restart, as decided in the layout phase. The owner confirmed during this discussion that the setting exists.
  - Strategy and the other layout settings are unchanged unless the review suggests otherwise.

### Expansions and enrichment
- **D-14: An expansion that expands several owned base games sits beside the first owned one,** meaning the lowest collection id. This is the same tie-break the engine uses for duplicate copies, so adding another base game later never moves it. The detail phase can list it on every owned base game's card.
- **D-15: Big-box or collector editions do not hide anything.**
  - If the owner owns an edition that BGG says "contains" expansions, and also has a contained expansion marked as owned, both boxes are drawn.
  - Every owned collection entry is a physical box on the shelf, as with two copies showing as two boxes (sync phase D-19).
  - No suppression logic is built for "contains" links.
- **D-16: Follow BGG's type for standalone expansions.**
  - An item that comes back in the base-games collection call is a base game with its own place. It may face out, even when BGG also links it as an expansion of another game.
  - Only items BGG types as expansions (the expansions call) pair up beside a base game.
- **D-17: New games get their details at once; everything else is refreshed about weekly.**
  - **New games:** a newly added game gets its `thing` details and its art downloaded in the same sync that first sees it.
  - **Existing games:** refreshed about once a week, spread over the hourly runs, so a normal hourly sync stays at the two collection calls plus at most a small, bounded number of `thing` calls.
  - **`thing` calls:** at most 20 ids per call, at least 5 seconds apart, and never with `versions=1`.
  - **Images:** downloaded only when a game is new or its image URL changed.
  - **Failures:** a failed enrichment or image download never fails the sync and never holds the collection back. The game still shows from collection data (generated cover, placeholder colour, last known details), and the next run retries.

### Claude's Discretion
- **Detector approach:** signals such as plain or uniform corners and borders, a non-rectangular dominant outline, skewed edges and background share. It runs on the downscaled image during sync with SkiaSharp. Tune it against synthetic test images (generated flat covers and slanted boxes on plain backgrounds, never real images in the repository), then confirm it on the owner's collection via the review sheet (D-04).
- **Images:**
  - variant widths (for example two widths for phone and desktop);
  - WebP quality;
  - file naming with a content hash for immutable caching;
  - the cache directory under the state directory;
  - discarding originals after downscaling;
  - evicting images no longer referenced;
  - byte and pixel caps before decode;
  - the allowlisted image host (research confirms BGG's current CDN host).

  Images are fetched only from URLs found in BGG responses, over HTTPS, by a client that never carries the token, serialised with a polite gap. Visitors never trigger a fetch or a resize, and there is no resize endpoint.
- **Colour extraction:** the algorithm (histogram buckets, saturation weighting, background exclusion), which colour fills leftover space when fitting art (D-06), and how lightness is nudged for contrast (D-11, D-12).
- **Size class (D-08):** the model and clamping bounds, the disagreement margin (D-07) and their starting values.
- **Snapshot shape:**
  - the schema version bump;
  - which `thing` fields are stored;
  - store both BGG's average rating and its ranked (Bayesian) rating, and the detail phase picks which to show;
  - no long descriptions are needed for DET-02;
  - per-game enrichment timestamps for the weekly refresh;
  - the image choice and verdict.

  An older release reading a newer file treats it as no snapshot and resyncs, never crashes.
- **Request budget:**
  - Raise or split the per-sync request budget (`BggClient.MaxRequestsPerSync` is 16 today) so a first sync of a few hundred games can enrich in bounded steps over several runs.
  - Decide how the weekly refresh is spread across runs.
  - Decide what a fresh empty start does (enrich in batches over a few runs, showing games as they are enriched).
- **Orphan expansion labels:** an orphan expansion (base not owned) is now labelled with its base game's title from the `thing` link ("Expansion for {base}"). When it names several bases, pick one deterministically. The interim "Expansion" label from the sync phase remains only for an expansion whose details have not arrived yet.
- **Phone cabinet density (folded todo):** pick an approach from the todo's options and show it in the review round:
  - let a cover or flat box choose an earlier row;
  - trim earlier sections with a documented stability exception;
  - leave the rest to per-location cabinets in the filters phase.

  Real box sizes change the packing anyway, so measure density again after they land.
- **Review flow:** follow the layout phase's pattern.
  - Iterate locally against the fake BGG with synthetic images, and send screenshots.
  - On the server, build the review sheet (D-04) and the cover-share screenshots (D-13) from the real collection.
  - Then cut a release, which the owner approves, and the owner checks the deployed cabinet on desktop and phone.

  Run a UI design step for this phase (box-look polish tuning, art fitting, spine colour, thin-box text), using the owner's senior-frontend skill.

### Folded Todos
- **Box look polish for the box images phase** (`.planning/todos/pending/2026-10-06-box-look-polish-for-the-box-images-phase.md`), folded in full. These are taste calls carried over from the layout prototype's approval:
  - The fourth-line ellipsis is cropped on the smallest phone covers. Generated covers become the fallback.
  - Short upright expansions truncate both text lines (D-09).
  - Expansions 50 to 63 mm deep are drawn 64 mm wide on desktop and 71 mm on phone (D-09).
  - Phone spines are at least 59 mm wide (D-09).
  - The cover share needs revisiting once real art shows (D-13).
  - The plinth arch reads as a soft shadow more than an arch (`--arch-shade-alpha` and the feather stops in `cabinet.css`), plus general cabinet polish.

  These go into this phase's UI design step as a tuning register.
- **Phone cabinet density** (`.planning/todos/pending/2026-10-06-phone-cabinet-density-for-the-filters-phase.md`), folded in full, though it was tagged for the filters phase. The owner pulled it forward because real sizes re-pack the cabinet and the phone review happens now anyway.
  - The problems: when a big cover forces a second phone section, the first section can keep several empty rows, because only the last section is trimmed. The 400-game sample takes 12 phone sections.
  - The approach is Claude's discretion (above), shown in review. Any new stability exception must be documented and tested as its own case, like the layout phase's exceptions.
- **Cabinet accessibility notes** (`.planning/todos/pending/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md`), **partly folded**:
  - **Now:** the "+N more" marker's accessible name must contain its visible text ("+N more"), for label-in-name and speech input.
  - **Not now:** the roving tabindex / one-tab-stop composite stays with the detail phase's keyboard work (A11Y-01), because it belongs with the pull-out and detail card. The todo stays open for that part.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` §"Phase 4: Enrichment, Box Images & Shape": goal, success criteria, research focus. The research focus covers:
  - version dimension coverage and units;
  - expansion shapes;
  - image CDN behaviour;
  - 3D-shot detection;
  - spine colour extraction with readable contrast.
- `.planning/REQUIREMENTS.md`:
  - **In scope:** SYNC-06, SYNC-07, IMG-01, IMG-03, CAB-03.
  - **Must leave room for:** DET-02 (the fields the detail card shows), IMG-02 (owner overrides: version image, main image, BGG gallery link) and FILT-02/03, EXP-04 (player count and play time used by filters).
  - **Out of Scope table:** no hotlinking and no per-visitor BGG calls.
- `.planning/PROJECT.md` §Context "BGG data" and §Key Decisions: about 65 owned items, owned versions selected, Dutch version images often 3D shots, the no-database decision.

### Measured BGG facts
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SPIKE-OUTCOME.md`:
  - **Dimensions:** version dimensions are on 35 of 50 base items and 9 of 15 expansions, in inches (factor 25.4), with length as the standing height. They only come back when the selected version is requested.
  - **Missing stats:** play-time attributes are missing on 4 of 15 expansions, so the data must tolerate gaps.
  - **`thing` shape:** `link` elements, `minage`, polls; no total-items attribute.
  - **Charset:** an odd charset parameter, so decode from the document itself.
  - **Duplicates:** one duplicate object id.

### Prior decisions
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-CONTEXT.md`:
  - D-14: interim box sizes, and the cabinet rearranges when real sizes land (accepted);
  - D-15: the interim "Expansion" label until pairing;
  - D-19: one box per collection entry, with expansions beside the first copy;
  - D-20: the fake BGG and the scripted handler;
  - the carried-forward BGG client rules: pacing, 202 loop, 401 handling, token only to the API host, synthetic fixtures, BGG text rendered as text only.
- `.planning/phases/02-layout-engine-cabinet-prototype/02-CONTEXT.md`:
  - D-05 and D-06: cover share and strategy as server settings, with orientation depending only on the game and settings;
  - D-08: the generated cover as the permanent fallback;
  - D-09, D-12, D-19 and D-23: stability rules and their documented exceptions, and thick expansions upright;
  - D-20: the classic furniture finish and its contrast cap;
  - D-21 and D-22: piles and lying flat.
- `.planning/phases/02-layout-engine-cabinet-prototype/02-UI-SPEC.md`: palette, contrast rules, furniture finish, readability minimums. This phase's UI design step updates the tuning register.
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-UI-SPEC.md`: the status line, empty state and credit placement this phase must not disturb.
- `.planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-CONTEXT.md` D-17: SkiaSharp 4.153.1 is the image library. It supersedes the ImageSharp recommendation in the stack research and in `.claude/CLAUDE.md`.

### Research
- `.planning/research/ARCHITECTURE.md`:
  - §"Enrichment with `thing`": 20-id batches, expansion mapping through inbound `boardgameexpansion` links, the dimension fallback chain, the refresh cadence;
  - §"Incremental vs full refresh";
  - §"Last-good snapshot and failure behaviour": partial enrichment is committed progressively, and image failure never fails a sync.
- `.planning/research/PITFALLS.md`:
  - Pitfall 5 (collection data model traps);
  - Pitfall 6 (terms: no modification, attribution);
  - Pitfall 8 (images: hotlinking, oversized originals, layout shift, no resize endpoint);
  - Pitfall 9 (mobile image memory);
  - Pitfall 12 (BGG text as markup).
- `.planning/research/FEATURES.md` §"Expansions" (edge-case table; D-14 to D-16 settle the cases for this project) and §"Shelf realism" (sizing, spine colour, face-out share).
- `.planning/research/STACK.md` §5 "Images": the pipeline shape and dominant-colour approach. The library choice is superseded by SkiaSharp, see above.
- `.claude/CLAUDE.md`:
  - hard rules: no planning references outside `.planning/`, `///`-only comments, no personal data, synthetic fixtures only;
  - §"BGG API Access Rules": token only to the API host, no `versions=1` on `thing`, credit on every page, no relaying.

### Folded todos
- `.planning/todos/pending/2026-10-06-box-look-polish-for-the-box-images-phase.md`
- `.planning/todos/pending/2026-10-06-phone-cabinet-density-for-the-filters-phase.md`
- `.planning/todos/pending/2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md` (the "+N more" name fix only)

### Operator docs to update (no planning references)
- `docs/cabinet-layout.md`: layout settings table (the cover share default may change, D-13).
- `docs/bgg-sync.md`: enrichment, refresh cadence, image cache.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Cabinet.Domain/Collection/BoxFromVersion.cs`:
  - inch-to-millimetre mapping (25.4);
  - longer front side becomes the standing height;
  - plausibility bounds (front 50 to 700 mm, depth 5 to 300 mm);
  - per-kind defaults (base 225×300×60, expansion 200×260×40).

  This is the first step of the D-07/D-08 chain. The chain extends here: cover shape and size class.
- `Cabinet.Domain/Collection/CollectionSnapshot.cs`: `SnapshotItem` and `VersionDimensions`, schema version 1. Enrichment fields, image choice, art colour and enrichment timestamps extend it with a schema bump.
- `Cabinet.Domain/Collection/SnapshotMapper.cs`: maps the snapshot to `CabinetItem`. Today it passes `ExpansionOf` empty; pairing (D-14, D-16) fills it. Its collection `Version` hash must include anything newly drawn (art colour, image identity, box size), so redraws and caches follow changes.
- `Cabinet.Domain/Layout/CabinetItem.cs`: `ExpansionOf` is already a list of `BaseGameRef`, so multi-base expansions fit (D-14).
- `Cabinet.Domain/Layout/SpinePalette.cs`: per-game tone from a fixed table, with a 4.5:1 text guarantee under `MaxShadePercent` 20%. It was documented from the start so colours from real art can replace an entry with no page change. It stays the fallback for games without art (D-12).
- `Cabinet.Domain/Layout/ReadabilityFloor.cs` and `SectionDesigns.cs`: the two-line minimums (`TwoLineSpinePx` 29, `TwoLineLabelPx` 36, `TapTargetPx` 24). D-09 introduces a one-line minimum.
- `Cabinet.Repository/Images/ImageSmoke.cs`: SkiaSharp decode, resize and WebP encode at quality 80, already proven on the server. It is the starting point for the image pipeline.
- `Cabinet.Repository/Bgg/BggClient.cs`, `BggCollectionParser.cs`, `RequestPacer.cs`, `BggTransport.cs`:
  - the paced, classified BGG client with a per-sync request budget (`MaxRequestsPerSync` = 16) that `thing` calls must share or extend;
  - the parser does not read `image` or `thumbnail` yet.
- `Cabinet.FakeBgg/`:
  - the local fake BGG already has a `/xmlapi2/thing` endpoint (20-id cap);
  - extend it with synthetic `thing` data (links, weight, designers, mechanics) and synthetic images served from a fake image host, for local review and integration tests;
  - `Testing/ScriptedBggHandler.cs` scripts failures.
- `Cabinet.Service/wwwroot/js/render.js`: draws generated covers (palette colour, pattern, title plate) and spines from layout data through custom properties and `textContent`. Real art goes in as a same-origin `<img>` with known dimensions (no layout shift), with the generated cover as the fallback.

### Established Patterns
- **The layout is a pure, versioned function:** `CabinetLayoutEngine.LayoutVersion` is 9 today, guarded by golden files under `Cabinet.UnitTests/Layout/Golden/`. Real sizes, one-line minimums and density changes are a deliberate version bump with re-recorded goldens (synthetic samples only).
- **Settings are validated at startup:** `Cabinet.Service/Layout/LayoutSettings.cs` checks the `Layout` keys and names the bad one, and `CommittedConfigurationTests` pins the committed appsettings. New image or enrichment settings follow the same pattern.
- **Stored files:**
  - written atomically, JSON under the state directory (`Cabinet.Repository/Storage/AtomicJsonFile.cs`, `SnapshotStore.cs`, `StorageLocation.cs`);
  - an unreadable or newer file is treated as no snapshot;
  - the image cache sits beside the snapshot in the same directory.
- **The app's own CSP:** `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` sets `default-src 'self'`. Same-origin images need no CSP change, and the page must stay free of violations.
- **Comments:** `///` XML docs only in C#, `/** */` blocks only in JS.
- **Builds:** warnings-as-errors with lock files.
- **Fixtures:** synthetic only. Test images are generated in code, never captured.

### Integration Points
- `Cabinet.Service/Sync/SyncRunner.cs` and `SyncWorker.cs`: enrichment and image download run inside the single-flight sync job after the collection fetch and the shrink guard. A partial enrichment is committed, and failures never fail the sync (D-17).
- `Cabinet.Service/Program.cs`:
  - Register a second typed `HttpClient` for images with no token, HTTPS only and an allowlisted host.
  - Serve the image cache directory as static files with long immutable caching. `MapStaticAssets` covers only build-time assets, so the runtime cache needs its own file provider.
- `Cabinet.Service/Layout/LayoutCache.cs` and `LayoutEndpoint.cs`: the layout JSON gains per-game art colour, image URL and image dimensions. The cache key follows the snapshot version.
- `deploy/systemd/cabinet.service` (`StateDirectory=cabinet`, `ProtectSystem=strict`): the image cache must live under the state directory. Check that outbound HTTPS to the image CDN host is allowed from the container.
- `Cabinet.Service/wwwroot/css/cabinet.css`: covers with real art, fitting with a colour fill (D-06), the one-line spine style (D-09) and the plinth arch polish.

</code_context>

<specifics>
## Specific Ideas

- **3D shots:** the owner would rather see a real 3D product shot of their own edition than a generated cover. Real art beats invented art, but a slanted shot is only used when no flat cover is available.
- **Thin boxes:** they should look as thin as they really are, as long as a one-line title still fits.
- **Spines:** a red box gets a red spine. Colours stay recognisable rather than toned down.
- **Cover share:** the owner wants to choose the cover share by looking at their real collection, and wants it to stay a setting they can change afterwards.
- **Physical shelf:** everything the owner marked owned on BGG is a physical box on the shelf (big-box editions included). BGG's own typing decides what counts as a game or an expansion.

</specifics>

<deferred>
## Deferred Ideas

- **Owner image overrides** (IMG-02): planned for the owner-tools phase. Wrong picks left after tuning wait for these (D-05).
- **Arrow-key navigation / one tab stop for the cabinet:** the remaining part of the accessibility todo. It stays with the detail phase (A11Y-01).
- **Showing the enriched details** (detail card, which rating to display, expansions listed on every owned base game's card): the detail phase.
- **Straightening 3D shots into flat covers** (CABX-04, v2): unchanged. It would conflict with D-06's no-modification stance, so check BGG's rule first.

### Reviewed Todos (not folded)
- `2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`: a new capability (several finishes plus a toggle and a way to choose). It needs its own discussion; it stays unscheduled, possibly hosted by the settings-page idea for the owner-tools phase.
- `2026-10-07-size-live-and-sync-limits-for-go-public.md`: belongs to the hardening phase (limits sized from real load).
- `2026-10-07-drop-actionlint-label-entry-when-supported.md`: CI housekeeping, unrelated to this phase.

</deferred>

---

*Phase: 04-enrichment-box-images-shape*
*Context gathered: 2026-10-07*

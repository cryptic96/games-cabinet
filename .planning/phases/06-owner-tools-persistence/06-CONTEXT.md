# Phase 6: Owner Tools & Persistence - Context

**Gathered:** 2026-10-12
**Status:** Ready for planning

<domain>
## Phase Boundary

From the home network or VPN only, the owner can:

- override any game's box picture (IMG-02);
- define an ordered list of storage locations and assign them to games (LOC-03, LOC-04).

The Phase 3 access check proved that BGG's private location field cannot be read with the token. Roadmap criterion 4 is therefore closed by building the editor, not by recording "not needed".

Owner tools sit behind their own port and hostname and are unreachable from the public route (OWN-01). The owner data survives syncs, restarts and releases, and can be exported and restored (OWN-02).

Also in this phase, folded from todos:
- the override worklist from the Phase 4 server review;
- the three open picture-pipeline review items (WR-02, WR-03, WR-04);
- the review-sheet fixes;
- the **slot** for a site-wide finish setting on the owner pages, with classic as its only choice.

**Not in this phase:**
- **The finishes themselves.** Six more finishes, per-finish walls, lit cubbies, "follow day and night" and cubby depth become an inserted phase straight after this one (D-30 to D-36).
- **Showing locations to visitors, filters and per-location cabinets** (Phase 7). The card's location row from Phase 5 already appears as soon as a game has a location.
- **Go-public** (Phase 8). The public router keeps today's LAN/VPN allow-list until the go-public checklist passes. The owner router keeps it permanently.

</domain>

<decisions>
## Implementation Decisions

The UI decisions were reviewed through the owner's senior-frontend skill during this discussion. Every choice was kept, and the owner accepted all refinements (R1 to R11) and ideas (I-A, I-B) in `06-FE-REVIEW.md`. They are folded into the decisions below.

### Access gate
- **D-01: Owner tools have their own port and their own hostname.**
  - Owner endpoints and pages are mapped **only** on a second app port. The public port (5080) does not have them at all, so a request through the public route gets 404, not 403.
  - A second Traefik router with its own hostname points at the owner port, behind the same LAN + VPN `ipAllowList` the whole site uses today, with TLS as usual.
  - The firewall template admits the new port from the Traefik address only, like 5080.
  - **Two independent walls:** one wrong Traefik rule still cannot expose the owner tools.
  - **Pattern to follow:** the loopback ops port (`OpsEndpoint`, `UseHealthChecks(path, port)`), where endpoints are bound to a port.
  - — **Reversibility:** costly — moving to a path-based gate later touches the Traefik config on the proxy host, the firewall template, the app's endpoint mapping and the security tests.
- **D-02: The app checks the client address as well.**
  - On the owner port it accepts only clients whose address, taken from `X-Forwarded-For` as resolved by the existing forwarded-headers setup, lies in the configured home and VPN ranges. Anything else gets 403.
  - A forwarded address is believed only from the configured known proxy.
  - The ranges live in the server-side env file only, like `ReverseProxy__KnownProxies__0`. `cabinet.env.example` carries RFC 5737 placeholders.
  - **The owner's prerequisite:** fill in the ranges, add a DNS name for the owner host, and add the second router on the Traefik host.
- **D-03: Network location is the only lock; no password.**
  - The owner tools admit exactly the LAN and VPN subnets that reach the site today.
  - The owner accepted that guests on the same LAN subnet could edit. The stakes are low, and every change is logged (D-27) and exportable.
- **D-04: The owner hostname serves the normal cabinet plus the owner pages.**
  - The public pages and endpoints are available on both ports. Owner-only endpoints exist on the owner port only.
- **D-05: Owner pages are English only.**
  - They use English whatever the language toggle says, and the EN · NL toggle is hidden on them (R1).
  - Exception: the card's Edit link follows the card's language (D-07).
- **D-06: The owner address can never be mistaken for the public site (R1).**
  - Owner pages reuse the site's shell, tokens and wood-and-paper look, and carry a small "Owner tools" plate in the header.
  - The cabinet on the owner address shows a thin strip saying the same.
- **D-07: Each card gets an Edit link at the owner address (R8).**
  - It is the card's last line, below the BGG link: a quiet text link with a pencil icon from the site's own icon sprite.
  - It reads "Edit" or "Bewerken", following the card's language, and jumps to that game's row on the owner pages.
  - It is rendered on the owner address only. The public card is unchanged.
- **D-08: Unplaced games carry a faint corner tag in the owner view (I-B).**
  - On the owner address only, unplaced games are visible while browsing. The public cabinet is unchanged.

### Picture overrides
- **D-09: Only direct picture links on the allowlisted image host are accepted.**
  - That host is `cf.geekdo-images.com`, the same one the sync already fetches from (`Images:AllowedHosts`).
  - Links are validated with the existing `ArtUrl.Canonical` and `ArtSourcePolicy`.
  - **Gallery page links** (`boardgamegeek.com/image/…`) are refused with a short how-to: open the picture in BGG's gallery and choose "Copy image address". Resolving a page link would need BGG's private website API, which BGG's terms say is not licensed.
  - The app cannot verify that a picture belongs to that game. That is the owner's responsibility.
- **D-10: Fetch, preview, then confirm.**
  - A pasted link is fetched once by the server, with the token-less image client and the same caps as the sync. This is the image host, not the BGG API, so the "only the sync talks to BGG" rule holds.
  - The picture is processed with the normal pipeline.
  - **The preview (R4)** shows it as a face-out box **and** as a spine with its colour and title, side by side at cabinet scale.
  - "Use this" applies it.
  - **Errors are reported inline:** a non-BGG host, a page link, a failed fetch, an undecodable picture, or a picture too large or too extreme in shape.
- **D-11: An override applies everywhere, and the box-shape rules stay.**
  - The chosen picture becomes the face-out cover, the card picture and the source of the spine colour. The flat-cover detector never overrules an owner's pick.
  - **Box proportions keep the box-images chain:**
    1. BGG's real version size;
    2. else the chosen picture's shape, but only when it reads as a flat cover;
    3. else the estimate.

    A 3D shot's outline never shapes a box.
  - Neighbouring boxes may shift once when a shape changes.
- **D-12: The choice is stored as a source kind, not a pinned address.**
  - The kinds are "Automatic", "Owned edition", "Main picture" and "Your link" (the link's address).
  - **When BGG replaces a picture:** "Owned edition" and "Main picture" follow BGG's current picture of that kind. The row goes back into "worth a look", marked "picture changed on BGG".
- **D-13: A link that stops working keeps its last stored copy.**
  - When a linked picture later fails to fetch, the processed copy already stored keeps showing, so nothing changes for visitors.
  - The row is marked "link no longer works" and goes back into "worth a look".
  - The stored copy is dropped only when the owner changes or clears the override.
  - Stored files of a linked override must not be swept by any art clean-up while the override exists.
- **D-14: The pictures page opens on "worth a look".**
  - **The default view shows:**
    - games where the automatic pick is not the owned edition's picture (the 23 rows of the override worklist);
    - games with a missing or broken picture;
    - rows marked by D-12 and D-13.
  - One switch shows all games A to Z.
  - **"Looks right" records "keep automatic, reviewed" (R2),** so the list counts down ("14 of 23 to check"). A later change by BGG to that game's pictures puts the row back.
  - Overridden games carry a small "yours" mark.
- **D-15: Each game row is a real radio group (R3).**
  - It is a `<fieldset>` with radio tiles for "Automatic", "Owned edition", "Main picture" and "Your link".
  - **Each tile shows** the processed picture the cabinet would use, at equal height in its true proportions, with a small tag for the detector's verdict (for example "reads as a 3D shot").
  - Picking an existing candidate saves at once, with a short "Saved · Undo".
  - Only a new link goes through the preview (D-10).
  - Both candidates are already downloaded and measured for every game (`ArtSync.Candidates`), so the page fetches nothing new to show them.

### Locations
- **D-16: The locations form a flat, ordered list.**
  - Names are free text, for example "Cabinet A", "Shelf B" or "Lent out". A room and a spot go into one name ("Shelf B, left").
  - The order the owner sets becomes the order of the per-location cabinets in the filters phase.
  - Games follow a rename because they refer to a location by a stable id, not by its name.
  - **Location names are public** (visitors see them from the filters phase on). The owner pages say so near the name field ("visible to every visitor; avoid people's names").
  - — **Reversibility:** costly — moving to rooms holding spots later changes the owner-data schema, the owner pages, and the filters phase's location cabinets and filter.
- **D-17: Editing the list (R6).**
  - Rename in place.
  - Reorder with up and down buttons (keyboard and phone friendly, no drag library).
  - Deleting a location that still holds games asks first, with the count ("4 games will become not placed"). Those games become unplaced and are never moved silently.
- **D-18: Expansions follow their base game unless set.**
  - An expansion takes its base game's location and moves with it.
  - The owner may give it its own location, for example a big-box expansion on another shelf. "Same as base game" puts it back.
  - An expansion without an owned base needs its own location.
  - An expansion that expands several owned base games follows the first base in a stable order, unless set. The exact rule is Claude's discretion; keep it deterministic.
- **D-19: Assigning in bulk with ticked rows and "Move to…".**
  - The locations page lists every game A to Z with a small cover, its current location and a picker per row. Expansions are nested under their base and show "same as base".
  - **Ticking rows** slides up a sticky bottom bar ("3 selected · Move to…") that opens a native list of locations (R5).
  - **The rows:** real checkboxes with tap targets of at least 44 px, a search box to find a game by name, and a "Not placed yet" filter.
  - Ticking a base game does not tick its expansions; they follow it anyway.
  - The page is designed to work on a phone while standing at the shelf.
- **D-20: Owner data for a game that left the collection is kept 30 days.**
  - Owner data is keyed by BGG game id.
  - When an **accepted** snapshot (not a held-back one) no longer lists a game, its location and picture choice are kept for 30 days and come back if the game returns.
  - After 30 days they are dropped automatically.
  - The 30 days are measured from the first accepted snapshot without the game, on the injected `TimeProvider`.

### Owner data and backups
- **D-21: The owner data lives in one JSON file in the state directory. There is no database.**
  - It sits next to `snapshot.json` and is written atomically with the existing `AtomicJsonFile`.
  - It carries a schema version: a reader refuses a version newer than it knows, as the snapshot store does.
  - The last few earlier versions are kept beside it, so a wrong edit or a wrong restore can be undone by hand. The count is Claude's discretion.
  - It holds the locations list, the assignments, the picture choices, "reviewed" marks, timestamps for the 30-day rule, the last-download time and the change log.
  - The shared database server and SQLite were considered and rejected for the reasons recorded in the sync phase (D-01 there).
  - — **Reversibility:** reversible — the file sits behind a small store interface like the snapshot. Moving to SQLite later is one implementation plus an import of the JSON.
- **D-22: The owner data survives syncs, restarts and releases.**
  - A sync never writes the owner-data file. Owner data is applied on top of the snapshot when the collection state is built.
  - Releases leave the state directory untouched (as today). The file's schema version guards rollbacks.
- **D-23: Export and import are the only backup.**
  - There is no Proxmox backup job and no nightly pull. The owner pages offer "Download owner data", a readable JSON file, and "Restore from file".
  - That restore is also how a fresh container gets its data back.
  - **Owner's choice, with the risk accepted:** the backup is only as fresh as the last download.
- **D-24: The download reminder is the button (R7).**
  - The owner pages show the last download time. When there are changes since then, a quiet banner reads "3 changes since your last download", with the Download button inside it.
  - After a download it turns into "Downloaded today at 21:14".
  - It never appears on the public site.
- **D-25: A restore replaces the data after a preview.**
  - The uploaded file is checked first: its shape, schema version, sizes, and that it is an owner-data file.
  - The owner then sees what it holds next to what is there now ("12 locations, 41 placed games, 9 picture choices") and confirms.
  - The current data is kept as an earlier version first.
  - Picture links in the file are fetched again in the background, under the same caps.
  - There is no merging.
- **D-26: Owner changes reach open pages at once, coalesced.**
  - Owner data becomes part of the collection version. Location is already in `SnapshotMapper.Version` and the card records, and the picture choice must be added. The layout and card caches and the ETags therefore refresh.
  - The existing live channel (`LiveNotifier`) tells open pages, which redraw quietly as after a sync and wait for an open card to close (detail phase D-06).
  - A burst of edits is coalesced into one notification a few seconds after the last save.
- **D-27: A small change log on the owner pages (I-A).**
  - It shows the last 20 changes with their times, for example "Picture: your link" or "Moved 3 games to Shelf B".
  - It is the same record the download banner counts. It is owner data and never public.

### Proving the wall
- **D-28: Automated tests and a server check.**
  - **The tests prove that:**
    - owner routes do not exist on the public port;
    - the owner port refuses client addresses outside the ranges;
    - a forged `X-Forwarded-For` from anyone but the known proxy is ignored;
    - the public pages still work on both ports.
  - **On the deployed release,** a scripted check goes through both Traefik routers: the owner host works from home, and owner paths on the public host answer 404.
  - The internet-side probe stays in the go-public phase.

### Finish setting slot (this phase)
- **D-29: The owner pages get a finish section with classic as its only choice.**
  - The setting is site-wide, chosen by the owner, and rendered server-side so there is no flash.
  - Its stored value and the server-side class on the page exist now. The inserted finishes phase adds the other choices and the light switches.

### For the inserted finishes phase (recorded here, built there)
- **D-30: The owner picks the finish site-wide.** There is no visitor switcher for now.
- **D-31: All seven finishes ship.** They are:
  - classic (B, exists);
  - modern birch plywood (A);
  - walnut display (C);
  - walnut done properly;
  - oak on legs;
  - library;
  - plywood with painted backs.

  Descriptions are in `.planning/todos/pending/2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`.
- **D-32: Each finish brings its own wall** (page backdrop). Header, status line and footer are contrast-checked on each wall.
- **D-33: The extras are three owner settings.** Each combines with any finish:
  - a "Lit cubbies" on/off switch;
  - a **"Follow day and night"** switch (the owner's addition);
  - a cubby-depth setting (flat, shaded or deep).
- **D-34: "Follow day and night" uses the visitor's own clock.**
  - With it on, the lights are lit only at night by the visitor's browser clock, at fixed hours (for example 19:00 to 07:00, tunable). With it off, the lights follow the on/off switch.
  - No location data is used.
  - **No flash (R10):** the page is drawn first, and the lights then come on with a slow fade of about 2 s. A page left open across the boundary lights up the same way. Reduced motion switches instantly.
  - Lighting is a few large gradient layers per cubby, under the 4.5:1 label-contrast check on every spine colour.
- **D-35: The phase starts with a mock-up round (R9).**
  - All seven finishes are mocked on the real cabinet first, as static pages with synthetic content at desktop and phone width with crops. The owner approves or adjusts each one before it is built.
  - The owner shares the old mockup screenshots if they contain only synthetic content.
- **D-36: Finishes are chosen by sight (R11).**
  - The setting shows each finish as a small rendered swatch (a cropped cubby corner), with the light switches under it.

### Folded todos (this phase)
- **D-37: The override worklist** (`2026-10-08-owner-image-overrides-candidates.md`).
  - Its 23 rows are the first real override session after release, starting with the rows seen showing another edition, another language or a generic picture.
  - It drives the "worth a look" default (D-14).
  - The detector settings and the "flat covers first" rule stay as they are.
- **D-38: The picture-pipeline leftovers** (`2026-10-08-picture-pipeline-robustness-from-phase-4-review.md`, items still open). Each fix comes with a failing test first:
  - **WR-04:** record details attempts, so a game the details source never describes backs off instead of being asked for every hour.
  - **WR-02:** a plausibility check in `BoxShape.FromCover`, so an extreme aspect ratio can't give a box side of a few millimetres or zero.
  - **WR-03:** make `SnapshotMapper.Explain` independent of source order for entries sharing a collection id.
- **D-39: The review sheet stays and gets its fixes** (`2026-10-08-review-sheet-fixes-from-phase-4-review.md`).
  - It remains the whole-collection tool for tuning the detector. The pictures page is for per-game choices.
  - **Fixes, each with a test:**
    - verdict counts by kind that add up, including the marked kinds;
    - a clipped or wrapped verdict column;
    - truly read-only snapshot loading, so it never renames to `.bad`;
    - `--bold-font` without `--font`, caught path errors and unsplit surrogate pairs.
- **D-40: Selectable finishes** (`2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`).
  - It was folded, then scheduled as its own inserted phase straight after this one (D-29 to D-36).
  - The todo stays open until that phase closes it.

### Claude's Discretion
- **Port and hostname plumbing:**
  - the owner port number and its Kestrel endpoint name;
  - how endpoints are bound to it (`RequireHost("*:port")` or equivalent);
  - how the Razor owner pages are restricted to it;
  - the range-check middleware or filter, and its configuration key names;
  - startup validation that refuses to run with an owner port but no ranges (fail closed);
  - the Traefik example file gaining a second router with placeholders;
  - the firewall template rule for the new port;
  - `cabinet-selfcheck` coverage.
- **CSRF and safety on owner endpoints:**
  - owner form posts use antiforgery tokens;
  - an upload size cap for restore;
  - every owner write goes through one serialised store (single writer, like the sync).
- **The exact "worth a look" rule:** which verdict and source combinations qualify, and how "reviewed" is invalidated by a picture change.
- **How many earlier versions of the owner-data file are kept,** their naming, and the change log's exact wording.
- **The coalescing delay** for live notifications (a few seconds), and how it interacts with a sync running at the same moment.
- **Owner page layout details** within the decisions above. Follow the senior-frontend skill in Builder mode under the strict CSP: no inline styles or scripts, text through `textContent`, vanilla ES modules.
- **Review flow:** follow the earlier phases.
  - Run a UI design step for the owner pages, iterate locally against the fake BGG and synthetic samples, and send screenshots at desktop and phone widths.
  - Cut a release, which the owner approves after the draft check.
  - Run the server check (D-28).
  - The owner does the first override and location session on the deployed site.
- **Synthetic data:** all fixtures, samples and screenshots use invented location names and synthetic games. Real location names never enter the repository.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Scope and requirements
- `.planning/ROADMAP.md` (Phase 6 section): goal, the owner prerequisite (address ranges in server-side config) and success criteria.
- `.planning/REQUIREMENTS.md`: IMG-01, IMG-02, IMG-03, LOC-01 to LOC-05, OWN-01, OWN-02, SEC-04 (owner tools unreachable from the internet).
- `.planning/PROJECT.md`: the Context section ("Database (after research)", "Location access check") and the Key Decisions table.

### Front-end review of this discussion
- `.planning/phases/06-owner-tools-persistence/06-FE-REVIEW.md`: the decisions kept (K1 to K10), refinements R1 to R11 and ideas I-A and I-B, all accepted.

### Earlier decisions this phase builds on
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SPIKE-OUTCOME.md`: no private location from BGG, so the location tools are built.
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-CONTEXT.md`: D-01 (local files, and why the shared database server was rejected) and the deferred "public settings page" idea.
- `.planning/phases/04-enrichment-box-images-shape/04-CONTEXT.md`:
  - the picture choice rules ("flat covers first");
  - the box-shape chain;
  - D-05 (leftover misses wait for overrides);
  - the image host allowlist.
- `.planning/phases/04-enrichment-box-images-shape/04-ART-CHECK-OUTCOME.md`: what the detector measures and how picks were judged.
- `.planning/phases/05-game-detail-accessibility-language/05-CONTEXT.md`:
  - D-06 (a sync waits for an open card);
  - D-13 (the location row appears only with a location);
  - D-21 and D-22 (language toggle and cookie);
  - card content.
- `.planning/phases/05-game-detail-accessibility-language/05-UI-SPEC.md`: tokens, icon sprite and card structure the owner pages and the Edit link reuse.
- `.planning/phases/02-layout-engine-cabinet-prototype/02-UI-SPEC.md`: the cabinet's design contract, including the finish hooks the finish slot attaches to.

### Folded todos
- `.planning/todos/pending/2026-10-08-owner-image-overrides-candidates.md`: the 23-row override worklist.
- `.planning/todos/pending/2026-10-08-picture-pipeline-robustness-from-phase-4-review.md`: open items WR-02, WR-03 and WR-04.
- `.planning/todos/pending/2026-10-08-review-sheet-fixes-from-phase-4-review.md`
- `.planning/todos/pending/2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`: finish descriptions and open design questions for the inserted finishes phase.

### Deployment and operations
- `deploy/traefik/cabinet.yml.example`: today's single router and the `cabinet-lan-only` allow-list. The owner router is added beside it.
- `deploy/nftables/cabinet.nft.in` and `deploy/provision.d/50-firewall.sh`: the default-drop firewall. The new port is admitted from the Traefik address only.
- `deploy/cabinet.env.example`: placeholders only. The owner ranges are added here as RFC 5737 placeholders.
- `deploy/systemd/cabinet.service`: `StateDirectory=cabinet` and `ProtectSystem=strict`. The owner-data file lives in the state directory.
- `docs/deploy.md`, `docs/lxc-setup.md`, `docs/review-sheet.md`, `docs/development.md`: user-facing docs that need the owner host, backups and owner tools added, with no planning references.

### External
- BGG XML API terms and usage page (see the project `CLAUDE.md`): private website APIs are not licensed (the reason for D-09), no modification of BGG data, and the token is never sent to the image host.

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Cabinet.Service/Hosting/OpsEndpoint.cs` and the `UseHealthChecks("/health", opsPort, …)` call in `Program.cs`: the existing pattern for an endpoint bound to its own port, validated at startup.
- `Program.cs` forwarded-headers setup (`ReverseProxy:KnownProxies`, `ForwardLimit = 1`): the source of the trusted client address for the range check (D-02).
- `Cabinet.Repository/Storage/AtomicJsonFile.cs`, `StoredJson.cs`, `StrictEnumConverter.cs`, `SnapshotStore.cs` and `SyncStateStore.cs`: atomic, schema-versioned JSON stores with corrupt-file handling. The owner-data store follows them.
- `Cabinet.Service/Collection/StorageLocation.cs`: resolves the state directory (`Storage:Directory`, `STATE_DIRECTORY`, or `.cabinet-state` in Development).
- `Cabinet.Repository/Images/ArtUrl.cs` (`Canonical`, `ArtSourcePolicy`), `ImageDownloader.cs`, `ArtProcessor.cs` and `ArtCache.cs`: link validation, the capped token-less fetch, processing and storage for the preview fetch (D-10).
- `Cabinet.Service/Sync/ArtSync.cs` (`Candidates`): both the owned edition's picture and the main picture are already fetched and measured per game.
- `Cabinet.Domain/Collection/ArtChoice.cs` (`ArtVerdicts.Classify`, `Score`): the verdict tags on the picture tiles (D-15).
- `Cabinet.Domain/Collection/SnapshotMapper.cs`:
  - `Version` already includes each entry's `Location`;
  - `MainPictureUrl`;
  - the place where owner data is applied on top of the snapshot.
- `Cabinet.Domain/Cards/CardRecords.cs`: the card already carries `Location`.
- `Cabinet.Service/Live/LiveNotifier.cs` and `CabinetHub.cs`: the live "collection changed" broadcast reused for owner saves (D-26).
- `Cabinet.Service/Language/SiteText.cs`, `SiteLanguage.cs` and `wwwroot/js/copy.js`: the Edit link's two strings (D-07). The owner pages themselves stay English.
- `Cabinet.Service/wwwroot/img/icons.svg`: the icon sprite for the pencil icon.
- `Cabinet.Service/Review/ReviewSheet*.cs`: the review sheet to fix (D-39).
- `Cabinet.FakeBgg` and `Cabinet.Domain/Samples`: synthetic collections and art for local iteration and tests, including invented locations.

### Established Patterns
- **Single writer:** the sync is single-flight through a channel. Owner writes go through one serialised store, in the same spirit.
- **Versioned caches:** layout and card caches are keyed on the collection version and ETags (`CollectionStore`, `LayoutCache`, `CardCache`). Owner data must feed the version.
- **Strict CSP** (`ContentSecurityPolicy.cs`): `default-src 'self'`, no inline styles or scripts. Owner pages follow it.
- **Comments:** `///` XML doc only. No planning references outside `.planning/`.
- **Tests:**
  - unit tests on xunit.v3 and FluentAssertions;
  - integration tests on `CabinetWebApplicationFactory` with Kestrel ports (`TestHostPortTests`, `LoopbackListener`);
  - Playwright browser tests in their own CI job;
  - `node --test` for page scripts (`build/tests/*.test.mjs`);
  - `FakeTimeProvider` for time rules (D-20).

### Integration Points
- `Program.cs`: the second Kestrel endpoint, the owner endpoint mapping restricted to it, the range-check middleware, and owner Razor pages.
- `CollectionStore`: the collection state is built from snapshot plus owner data, and the version includes picture choices.
- `ArtSync`: honour linked overrides (fetch and keep their files), never sweep an override's stored files, and record a dead link without dropping the copy (D-13).
- `Index.cshtml` and card scripts: the Edit link and the owner strip, on the owner address only.
- `deploy/` (Traefik example, firewall template, env example, selfcheck) and `docs/`.

</code_context>

<specifics>
## Specific Ideas

- **The first real use after release:** work through the 23 worklist rows, the ones seen showing another edition, another language or a generic picture first. Then do a first pass of locations at the shelf on a phone.
- **The owner asked twice to confirm the wall,** and confirmed:
  - the cabinet goes public only in the go-public phase;
  - the owner tools stay behind today's LAN + VPN allow-list permanently, on their own hostname and port.
- **"Follow day and night" was the owner's own addition** during the discussion. It is meant as a charm: the cabinet's lights come on in the evening.

</specifics>

<deferred>
## Deferred Ideas

- **The finishes phase** (inserted straight after this one): six more finishes, per-finish walls, lit cubbies, "follow day and night", cubby depth, and the mock-up round. Decisions are recorded above (D-30 to D-36).
- **A visitor finish switcher:** possible later, on top of the owner's site-wide choice (D-30).
- **The public settings page** (sync phase idea): not needed now. Owner settings live on the owner pages, and "sync now" and the language toggle stay where they are.
- **Automated backups** (a Proxmox backup job, or a nightly pull by another home machine): considered and declined for now. Export and import are the backup (D-23). Revisit if the owner data grows or a backup target appears.

### Reviewed Todos (not folded)
- `2026-10-07-size-live-and-sync-limits-for-go-public.md`: go-public phase.
- `2026-10-11-uneven-desktop-sections-layout-pass.md`: a layout pass, not owner tools.
- `2026-10-12-android-back-check-on-deployed-cabinet.md`: an open check from the detail phase, done on the deployed site.
- `2026-10-12-screen-reader-check-of-games-list.md`: an open check from the detail phase, done on the deployed site.
- `2026-10-08-sharpen-tests-from-phase-4-review.md`: test hygiene, unrelated to owner tools.
- `2026-10-07-drop-actionlint-label-entry-when-supported.md`: CI tooling.

</deferred>

---

*Phase: 06-owner-tools-persistence*
*Context gathered: 2026-10-12*

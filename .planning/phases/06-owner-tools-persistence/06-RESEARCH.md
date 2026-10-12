# Phase 6: Owner Tools & Persistence - Research

**Researched:** 2026-10-12
**Domain:** ASP.NET Core 10 two-port access gating, a file-backed owner-data store layered over a sync-owned snapshot, a same-origin picture-override pipeline, Razor Pages forms enhanced by vanilla ES modules under a strict CSP, and the deploy surface that carries a second port.
**Confidence:** HIGH for the access gate, antiforgery, store layering and deploy surface (read from code and exercised in scratch builds this session); MEDIUM for the front-end behaviours that only a browser test can pin down and for the decisions listed under "Open Questions".

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

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
- **D-31: All seven finishes ship:**
  - classic (B, exists);
  - modern birch plywood (A);
  - walnut display (C);
  - walnut done properly;
  - oak on legs;
  - library;
  - plywood with painted backs.

  Descriptions are in `.planning/todos/pending/2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`.
- **D-32: Each finish brings its own wall** (page backdrop). Header, status line and footer are contrast-checked on each wall.
- **D-33: The extras are three owner settings that combine with any finish:**
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

### Deferred Ideas (OUT OF SCOPE)

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
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| IMG-02 | The owner can override any game's image with the owned version's image, the base game's image, or a specific BGG gallery image given by link (only BGG image links accepted) | "Pattern 4: picture override pipeline" (link policy, in-memory preview staging, apply, dead-link marking, prune protection); "Pattern 3: overlay" (forced pick in the mapper); gaps found in `ArtDownload` and `ArtProcessor` outcomes; the link URL shape and the page-link refusal rule |
| LOC-03 | The owner can define their own list of storage locations | "Pattern 2: owner data" (stable ids, ordered list, 50 / 60 limits, delete-to-unplaced, undo); "Pattern 5: forms" |
| LOC-04 | The owner can assign one of those locations to each game | "Pattern 3: overlay" (effective location with expansion following its base, via the pairing the layout already uses); bulk move; the settled-value select; the sticky dock and move dialog |
| OWN-01 | Owner tools work only from the home network or VPN; the public internet can never reach owner screens or endpoints | "Pattern 1: the owner port and its two walls" (LocalPort, metadata, range check behind forwarded headers, fail-closed validation, path-trick tests); deploy surface (nftables, Traefik example, env example, selfcheck, scripted route check) |
| OWN-02 | Owner-entered data survives syncs, restarts and new releases and is backed up | "Pattern 2: owner data" (atomic file, schema version, earlier versions, unreadable state), "Pattern 3" (applied on top of the snapshot, never merged into it), export and restore with preview, 30-day expiry on `TimeProvider` |
</phase_requirements>

## Summary

The phase adds one new writer (the owner-data store), one new overlay (owner data applied on top of the sync's snapshot when the collection state is built) and one new surface (a second Kestrel port with its own pages). The cleanest shape found is: `OwnerData` records in Domain, an atomic schema-versioned file store in Repository that follows `SnapshotStore`, and in Service a single serialised `OwnerDataService`, a `CollectionPublisher` that is the only code that builds and replaces `CollectionState`, owner Razor Pages under `Pages/Owner/`, and a small access gate. The sync keeps writing only `snapshot.json`; owner data never enters it. Every path that today calls `CollectionState.FromSnapshot` (sync commit, start-up, unreadable-snapshot retry) must go through the publisher so owner changes and sync commits cannot overwrite each other's state.

The access gate rests on `HttpContext.Connection.LocalPort`, not on the Host header. Microsoft's routing documentation states that `RequireHost` and `HttpRequest.Host` "are subject to potential spoofing by clients" and recommends checking `ConnectionInfo.LocalPort`; the existing ops endpoint already does that through `UseHealthChecks(path, port)`, whose port predicate is `c.Connection.LocalPort == port`. A scratch build in this session confirmed that endpoint metadata set by a Razor Pages folder convention, by an attribute on a `PageModel` and by `WithMetadata` on a minimal endpoint is all visible to a middleware after `UseRouting`, that such endpoints answer 404 on the public port and 200 on the owner port, and that a spoofed `Host: x:5991` header on the public port changes nothing. The second wall (client address from `X-Forwarded-For`, believed only from the known proxy) uses `System.Net.IPNetwork`, which has pitfalls verified below (a name clash with the obsolete `Microsoft.AspNetCore.HttpOverrides.IPNetwork`, `/0` accepted, host bits silently normalised, mapped-address behaviour).

Four places where the approved specification and the code disagree, or leave a hole, need a decision before planning is final; they are listed under "Open Questions" and in the Assumptions Log: the base game an expansion follows (layout pairing uses the lowest collection id, the spec says lowest BGG id), the sync-writes-owner-data rule versus the 30-day clock and dead-link marking, staged previews held in memory instead of as files, and two seams (`ArtDownload`, `ArtProcessor`) that cannot yet produce the specified error messages.

**Primary recommendation:** Build the gate and the owner-data store first and prove them with tests (they are the two costly-to-reverse pieces), publish all collection state through one `CollectionPublisher`, keep owner data out of `snapshot.json`, use `LocalPort` plus endpoint metadata for routing and `System.Net.IPNetwork` for ranges, enable the owner endpoint only when its ranges and a known proxy are configured (fail closed), and treat every owner write as a mutate-under-lock on an immutable document with a revision number that also drives "out of date" and Undo.

**Suggested decomposition for the planner** (the planner owns the final waves):

1. Wave 0: test infrastructure (the factory gains an owner port and owner ranges; an owner browser-test base class), plus failing tests for the three folded domain fixes and the review-sheet fixes.
2. Wave 1, independent: (a) the three folded fixes and the review-sheet fixes; (b) the access gate, configuration validation and the deploy surface; (c) the owner-data model, store, export format and restore validation.
3. Wave 2: overlay, publisher and forced pick in the mapper; location service and endpoints; picture service (choices, link pipeline, link worker).
4. Wave 3: owner pages, owner stylesheet and modules, Edit link, strip and corner tag, settings and backup, finish slot, coalesced live notification.
5. Wave 4: docs, scripted route check, browser tests, release and the owner's deployed-site session.

## Project Constraints (from .claude/CLAUDE.md)

Treated with the authority of locked decisions. [VERIFIED: .claude/CLAUDE.md read this session]

- No planning references outside `.planning/`: no requirement keys, decision ids, review-finding ids, phase or plan numbers, planning file names, in code, comments, strings, test names, docs, scripts, config. Git commit messages are the only exception. The repository lint enforces this (`build/lint/checks/10-repo-rules.sh`, below).
- Comments: `///` XML doc summaries only in C#. No `//` comments. JavaScript uses `/** ... */` blocks only and no `//` anywhere, including inside regular expressions and strings (the lint pattern is `(^|[^:])//`).
- Public repository: no personal data. Location names in fixtures, tests, docs and screenshots are placeholders ("Cabinet A", "Shelf B", "Lent out"); hosts are `example.com` or `example.org`; addresses are RFC 5737 (`192.0.2.0/24`, `198.51.100.0/24`, `203.0.113.0/24`). Test data is synthetic. No recorded BGG responses.
- Never commit to `main`; all work on the milestone branch; commits use the noreply identity.
- BGG etiquette: only the sync talks to the BGG API; the token is sent only to the BGG API host and never to the image host. The owner's link fetch goes to the image host through the existing token-less `IArtSource` client and must be covered by a test that asserts no Authorization header.
- Strict CSP stays: `default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'` [VERIFIED: Cabinet.Service/Hosting/ContentSecurityPolicy.cs:12-13, quoted]. No inline script or style, no `blob:` or `data:` image sources.
- The lint also bans markup-parsing DOM APIs in page scripts (`innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `.cssText`) [VERIFIED: build/lint/checks/10-repo-rules.sh JS_MARKUP_API_PATTERN] and treats these as planning references: `A11Y|CAB|DET|EXP|FILT|I18N|IMG|LOC|NIGHT|OPS|OWN|SEC|SHARE|SYNC` followed by `-NN`, `D-NN`, `phase N`, `plan|wave|milestone N`, `CR|WR|IN|BL-NN`, `NN-NN` plan numbers, and the planning file names. Executors must write test names such as "a preview that ran past its expiry is refused", never "WR-04 ...".
- Project skill: the senior-frontend skill is configured for planner and executor (`.planning/config.json` agent_skills); the owner pages follow it in Builder mode.
- Comments rule applies to Razor files too: no `//` in `@code` blocks or script text.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Owner-only exposure (port, hostname, ranges) | Frontend server (Kestrel listener and middleware) | CDN / edge (Traefik router and allow-list) and the host firewall | Two independent walls inside the app (listener port, client range) plus two outside it (Traefik allow-list, nftables). Only the app can decide per request which port a connection arrived on |
| Owner data persistence, revision, undo, change log | API / backend (single serialised writer) | Database / storage (one JSON file with earlier versions) | One writer keeps edits, restore and expiry from racing; the file follows the snapshot store's atomic pattern |
| Applying owner data to the cabinet (location text, forced picture) | API / backend (publisher and mapper) | — | The layout and card caches and ETags are keyed on the collection version built there; the browser never merges owner data itself |
| Fetching and processing a pasted picture link | API / backend (image client, processor, cache) | CDN (the image host is the only outbound target) | The server is the only party allowed to talk to the image host; previews are served from the app's own origin |
| Preview staging (pending link or pending restore) | API / backend (in-memory, expiring) | — | Short-lived, small, never needs to survive a restart; no filesystem path is built from request input |
| Owner pages, forms, status lines, undo affordance | Frontend server (Razor Pages, antiforgery) | Browser (vanilla ES modules that enhance real forms) | Pages work without script through real posts; script only avoids reloads |
| Settled-value select, dock, move dialog, find-as-you-type | Browser | — | Pure interaction logic; the pure text and counting logic lives in `owner-format.js` so `node --test` covers it |
| Unplaced corner tag, Edit link | Browser (from card data) | Frontend server (writes `data-edit-href` and the strip on the owner port only) | The shared layout and card data stay identical for every visitor; tags derive from the card data's `location` being empty |
| Backup (export) and restore | API / backend | Browser (file input) | Validation of an uploaded file is a server concern: size, shape, schema version, limits, link policy |
| Live refresh after an owner save | API / backend (`ILiveNotifier`, coalesced) | Browser (existing live module) | The existing channel and redraw-on-new-version logic already do the rest |
| Deploy: firewall rule, Traefik router, env example, selfcheck, route check | Host / deploy scripts | Owner's Traefik host (not in this repository) | The repository ships templates and checks; the real router and DNS entry are owner actions |

## Standard Stack

### Core

No new packages. Every capability needed is in the shared framework, the packages already referenced, or the browser. [VERIFIED: Cabinet.Service.csproj, Cabinet.Repository.csproj, Cabinet.UnitTests.csproj, Cabinet.IntegrationTests.csproj read this session]

| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core / .NET | 10.0.112 SDK (`global.json` pins `10.0.112`, `rollForward: latestFeature`) | Kestrel, Razor Pages, antiforgery, routing, `TimeProvider` | The stack is fixed; `System.Net.IPNetwork`, `RandomNumberGenerator.GetHexString`, `TimeProvider.CreateTimer` and `JsonDocumentOptions.MaxDepth` were exercised in a scratch project on 10.0.112 |
| SkiaSharp (+ NativeAssets.Linux.NoDependencies) | 4.153.1 | Decode, resize, WebP encode, measure | Already used by `ArtProcessor`. Note: `.claude/CLAUDE.md` and the stack notes name ImageSharp; the code uses SkiaSharp, and the owner pipeline reuses `ArtProcessor` unchanged |
| System.Text.Json via `StoredJson.CreateOptions` | built in | Strict, schema-checked stored files (required properties, strict enum names, nullable annotations) | The two existing stores use it; the owner store follows them |
| ASP.NET Core Antiforgery | built in | CSRF protection for every owner post | Razor Pages validate tokens with built-in filters and need no middleware [CITED: learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0] |
| SignalR hub via `ILiveNotifier` | built in | Push the changed collection version to open pages | Already wired; `status.Current().SnapshotVersion` is what the page compares |

### Supporting

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| xunit.v3 | 4.0.2 | Unit and integration tests | All C# tests (the stack notes say 4.0.1; the csproj says 4.0.2) [VERIFIED: Cabinet.UnitTests.csproj `<PackageReference Include="xunit.v3" Version="4.0.2" />`] |
| FluentAssertions | 8.11.0 | Assertions | As existing tests |
| Microsoft.Extensions.TimeProvider.Testing | 10.10.0 | `FakeTimeProvider` for the 30-day rule, expiry of previews, rotation interval, coalescing delay | Time rules |
| Microsoft.AspNetCore.Mvc.Testing | 10.0.12 | `CabinetWebApplicationFactory` base | Integration tests on real Kestrel ports |
| Microsoft.Playwright.Xunit.v3 | per `Cabinet.BrowserTests.csproj` | Browser tests (Chromium headless shell 1243 is installed locally) | Owner page flows, CSP console check, 44px targets |
| node `--test` | Node 24 locally | Pure page-script logic | `owner-format.js`, copy parity for the two new bilingual strings |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| `LocalPort` plus endpoint metadata | `RequireHost("*:5082")` | Matches the client-controlled Host header, which Traefik forwards as the hostname with no port, so it would not even match behind the proxy; and Microsoft documents it as spoofable. Rejected |
| `LocalPort` plus endpoint metadata | A custom `MatcherPolicy` that invalidates owner candidates on the public port | Makes the endpoints truly non-existent on the public port, but is more code to test; the metadata middleware gives the same 404 and is testable with a `DefaultHttpContext`. Rejected for now |
| Owner data in `snapshot.json` | Separate file | Rejected: the sync owns and rewrites the snapshot; mixing would let a sync commit lose an owner edit |
| SQLite | JSON file | Already decided and recorded; the store sits behind an interface so the move stays cheap |

**Installation:** none. `dotnet restore Cabinet.slnx --locked-mode` must still pass, so a change that adds a package without updating the `packages.lock.json` files fails CI.

## Package Legitimacy Audit

No external packages are installed by this phase. `gsd-tools query package-legitimacy check` was therefore not run; there is nothing to verify and nothing is tagged `[ASSUMED]` for registry reasons. [VERIFIED: the csproj files listed above]

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none added) | — | — | — | — | — | — |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

## Architecture Patterns

### System Architecture Diagram

```
 Home / VPN browser                                        Public visitor (later phase)
        |                                                          |
        v  https://owner.example.org                               v  https://cabinet.example.com
 [Traefik router "cabinet-owner"]                         [Traefik router "cabinet"]
   ipAllowList LAN+VPN (permanent)                          ipAllowList LAN+VPN (until go-public)
        |  X-Forwarded-For: <client>                                |
        v                                                           v
 [nftables: tcp 5082 from Traefik IP only]                [nftables: tcp 5080 from Traefik IP only]
        |                                                           |
        v                                                           v
 +--------------------------- one Cabinet.Service process (one pipeline) -------------------------+
 | Kestrel endpoint "Owner" :5082          Kestrel endpoint "Web" :5080      "Ops" 127.0.0.1:5081 |
 |        \                                      |                                   |             |
 |         +-------------------+-----------------+-----------------------------------+            |
 |                             v                                                                   |
 |  UseContentSecurityPolicy -> UseForwardedHeaders (KnownProxies, ForwardLimit=1)                 |
 |        -> OWNER GATE A: LocalPort == ownerPort && client not in ranges -> 403 bare text         |
 |        -> UseHealthChecks("/health", opsPort) -> UseCabinetArt (/art) -> UseRouting             |
 |        -> OWNER GATE B: endpoint has OwnerOnly metadata && LocalPort != ownerPort -> 404        |
 |        -> Razor Pages (cabinet "/" on both ports; Pages/Owner/* only on the owner port)         |
 |           minimal endpoints (/cabinet/layout, /cabinet/cards, /cabinet/sync, hub)               |
 +--------------------------------------------------------------------------------------------------+
        |  owner POST (antiforgery header or field, revision)               ^ reads
        v                                                                   |
 [OwnerDataService: SemaphoreSlim(1)] --mutate(immutable OwnerData)--> [OwnerDataStore]
        |                 |                                              owner-data.json (+ versions/)
        |                 +--> [change log, revision, undo slot (in memory)]
        v
 [CollectionPublisher: lock] <-- snapshot commits (SyncRunner, SyncStartup) ---- snapshot.json
        |  CollectionState.FromSnapshot(snapshot, rules, overlay)
        v
 [CollectionStore.Replace] --> layout/card caches + ETags (keyed by Version) --> /cabinet/layout, /cabinet/cards
        |
        +--> [OwnerChangeNotifier: coalesce ~3 s] --> ILiveNotifier.PublishAsync(status.Current()) --> open cabinet pages redraw
 Owner link fetch:  owner POST -> IArtSource (token-less, ArtSourcePolicy, caps) -> ArtProcessor -> in-memory preview (15 min)
                    "Use this" -> ArtCache.Write + owner-data (link + ImageRecord) -> publisher
 Background:  OwnerLinkWorker (restored / undone / weekly re-check of links; marks dead links, keeps the copy)
```

### Recommended Project Structure

New files only (names are proposals; the planner owns them). Layering mirrors the existing split: pure models in Domain, file stores in Repository, orchestration and HTTP in Service.

```
Cabinet.Domain/
  Owner/
    OwnerData.cs                 records: OwnerData, OwnerLocation, OwnerGame, PictureSetting, PictureLink, ChangeEntry; PictureChoice enum
    OwnerOverlay.cs              pure: (snapshot, ownerData) -> effective locations + forced pictures; effective-location rule
    OwnerChangeText.cs           pure change-log wording
  Collection/
    ArtVerdictKind.cs            one mapping from (verdict, features) to the five verdict kinds; shared by the review sheet and the pictures page
Cabinet.Repository/
  Storage/
    OwnerDataStore.cs            atomic file, schema check, earlier versions, unreadable state (never renames the file)
    OwnerDataExport.cs           export document + strict restore parser (size, depth, limits, link policy)
Cabinet.Service/
  Owner/
    OwnerAccess.cs               options, startup validation, range check, the two gate middlewares, OwnerOnlyAttribute
    OwnerDataService.cs          single writer: mutate, revision check, undo slot, change log, versions rotation, system mutations (expiry, link state)
    CollectionPublisher.cs       the only place that builds CollectionState; lock; used by sync, start-up and owner writes
    OwnerPictureService.cs       choices, worth-a-look rule, link check and preview staging, apply, remove
    OwnerLinkWorker.cs           hosted service: fetch restored or undone links, weekly re-check, dead-link marking
    StagedItems.cs               in-memory, expiring, capped store for previews and restore checks
    OwnerChangeNotifier.cs       coalescing timer over ILiveNotifier
    OwnerStrings.cs              English string table for markup
  Pages/Owner/                   Pictures.cshtml(.cs), Locations.cshtml(.cs), Settings.cshtml(.cs), _OwnerLayout.cshtml, partials
  Pages/Shared/_Footer.cshtml    footer + credit shared by both layouts
  wwwroot/css/owner.css          linked at the owner address only
  wwwroot/js/owner-*.js          owner-shared, owner-pictures, owner-locations, owner-settings, owner-format (pure), owner-cabinet (applies data-unplaced from card data); kept under wwwroot/js so the markup-API lint covers them (see Pattern 6 and Pitfall 11)
```

### Pattern 1: the owner port and its two walls (OWN-01)

**What:** One process, three Kestrel endpoints (`Web` 5080, `Ops` loopback 5081, new `Owner`), one pipeline. The pipeline decides per request by `Connection.LocalPort`.

**Verified facts**
- The ops endpoint already uses this: `UseHealthChecks("/health", opsPort, ...)` compiles to `app.MapWhen(predicate, ...)` with the predicate `(port == null || c.Connection.LocalPort == port) && ...` [CITED: raw.githubusercontent.com/dotnet/aspnetcore/main/src/Middleware/HealthChecks/src/Builder/HealthCheckApplicationBuilderExtensions.cs, fetched this session].
- Microsoft's routing page: APIs relying on the Host header, "including `HttpRequest.Host` and `RequireHost`, are subject to potential spoofing by clients", and it recommends "checking the port via `HttpContext.Connection` (`ConnectionInfo.LocalPort`)" [CITED: learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0, Host matching section].
- Scratch build on SDK 10.0.112 (two Kestrel ports, one pipeline): the owner folder convention `AddFolderApplicationModelConvention("/Owner", m => m.EndpointMetadata.Add(new OwnerOnlyAttribute()))`, an `[OwnerOnly]` attribute on a `PageModel`, and `.WithMetadata(new OwnerOnlyAttribute())` on a minimal endpoint were all found by `ctx.GetEndpoint()?.Metadata.GetMetadata<OwnerOnlyAttribute>()` in a middleware after `UseRouting`; every owner path answered 404 on the public port and 200 on the owner port, an unknown path 404 on both, and `Host: x:5991` sent to the public port still gave 404. [VERIFIED: scratch run this session]
- Defaults of `ForwardedHeadersOptions` on this SDK: `KnownProxies` = `::1`, `KnownIPNetworks` = `127.0.0.0/8` (so a loopback client is trusted as a proxy). `KnownNetworks` is obsolete in .NET 10 and, with `TreatWarningsAsErrors`, using it breaks the build; use `KnownIPNetworks` if ever needed [VERIFIED: scratch run; CITED: learn.microsoft.com/en-us/dotnet/api/microsoft.aspnetcore.builder.forwardedheadersoptions].
- The existing setup: `options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;`, `options.ForwardLimit = 1;`, known proxies from `ReverseProxy:KnownProxies`, `app.UseForwardedHeaders();` after `app.UseContentSecurityPolicy();` [VERIFIED: Cabinet.Service/Program.cs:60-72 and 82-83, quoted].

**Wall 1, listener (and routing):** all owner endpoints and pages carry `OwnerOnly` metadata; a middleware after `UseRouting` answers an empty 404 when metadata is present and `LocalPort != ownerPort`. Plain `Results.NotFound()` style empty 404, the same bytes an unknown path gives (no status-code pages are configured). Add a second, independent prefix check for everything under `/owner` (static assets included) before static files, comparing the first path segment case-insensitively, and pin both with the path-trick tests listed in the Validation section.

**Wall 2, client range:** a middleware immediately after `UseForwardedHeaders()` and before the ops health check, static files and routing. On a request whose `LocalPort == ownerPort`: normalise `RemoteIpAddress` (`IsIPv4MappedToIPv6` then `MapToIPv4()`), deny (403, bare `text/plain` `Not available from this address.`, `Cache-Control: no-store`) when it is null or outside every range. It applies to the whole owner port, including the cabinet at the owner address, the live hub and static files (D-04, D-02).

**Configuration (proposals):** `Kestrel:Endpoints:Owner:Url` (for example `http://0.0.0.0:5082`; the endpoint name `Owner`), `Owner:AllowedRanges` (env `Owner__AllowedRanges__0`, `__1`, ...). Keep the owner endpoint out of the committed `appsettings.json` and document it in `cabinet.env.example` with RFC 5737 placeholders, so a release deploys safely before the owner has prepared the ranges and DNS; add the endpoint and a loopback range set to `appsettings.Development.json` only. Startup validation `OwnerAccess.FromConfiguration` (a sibling of `OpsEndpoint.FromConfiguration`): owner endpoint configured implies (1) at least one range, (2) at least one `ReverseProxy:KnownProxies` entry, (3) the port differs from the `Web` and `Ops` ports, (4) every range parses; otherwise throw `InvalidOperationException` naming the key. An owner endpoint absent means the owner tools do not exist (all owner endpoints 404 everywhere). A deploy with the key set but no ranges fails the health check and rolls back, which is the intended fail-closed behaviour.

**IPNetwork pitfalls, all [VERIFIED: scratch run on SDK 10.0.112]:**

| Input or call | Result | Consequence |
|---------------|--------|-------------|
| `IPNetwork` unqualified in a file that imports `System.Net` and `Microsoft.AspNetCore.HttpOverrides` (as `Program.cs` does) | compile error CS0104, ambiguous between `Microsoft.AspNetCore.HttpOverrides.IPNetwork` and `System.Net.IPNetwork` | Write `System.Net.IPNetwork` in full or use an alias |
| `IPNetwork.TryParse("192.0.2.5/24", ...)` | true; stored as `192.0.2.0/24` | Host bits are silently dropped; accept and normalise, or reject, but decide on purpose |
| `TryParse("0.0.0.0/0")` and `("::/0")` | true | A `/0` makes the whole check a no-op; reject prefix length 0 at startup |
| `TryParse(" 192.0.2.0/24")`, trailing space, `"192.0.2.0"` (no prefix), `/33`, `/129`, `"/24"` | false | Trim each configured entry before parsing; for a bare address either reject with a clear message or treat as `/32` or `/128` |
| `TryParse("192.0.2.0/024")` | true, prefix 24 | harmless |
| `Parse("192.0.2.0/24").Contains(::ffff:192.0.2.7)` | true | `Contains` handles a mapped address against a plain IPv4 network |
| `Parse("::ffff:192.0.2.0/120").Contains(192.0.2.7)` | false | A mapped-form range in configuration never matches plain IPv4; reject mapped-form ranges at startup |
| IPv4 network `Contains(::1)` | false | A v6 loopback client needs a v6 range such as `::1/128` |

**Behind Traefik:** Traefik forwards the Host header as the hostname with no app port [ASSUMED: standard reverse-proxy behaviour, not re-tested here], and by default appends the connecting address to `X-Forwarded-For` unless `forwardedHeaders.notAppendXForwardedFor` is set; it trusts incoming `X-Forwarded-*` only from `forwardedHeaders.trustedIPs` [CITED: doc.traefik.io/traefik/reference/install-configuration/entrypoints/; the page does not say what is done with untrusted incoming values]. With `ForwardLimit = 1` the app reads only the last entry, which is the one Traefik appended, so entries a client prepends are irrelevant. Operational caveat for the owner's Traefik entrypoint: do not set `notAppendXForwardedFor` and do not list the clients' network in `trustedIPs`. A forwarded address is believed only when the connecting peer is in `KnownProxies` (or the loopback defaults); otherwise the headers are ignored and `RemoteIpAddress` stays the peer [CITED: learn.microsoft.com forwarded-headers behaviour from the .NET 8 unknown-proxies change, search result this session]. Misconfiguration hazard: if `KnownProxies` is wrong, `RemoteIpAddress` becomes the Traefik address, which normally lies inside the LAN range, and the second wall silently degrades to the first. Mitigations: validation (2) above, a selfcheck probe through loopback (below), and the documented single value shared with the firewall (`CABINET_TRAEFIK_IP`).

**Logging:** log the denial as a category only (no client address, consistent with the sync code's "counts and categories only" rule).

**Tests the wall needs** (details in Validation Architecture): endpoint-metadata coverage test (every endpoint whose route starts with `/owner` carries `OwnerOnly`); public port 404 for each owner route and for the path-trick list; owner port 403 outside the ranges and for a null remote address; the factory's loopback peer is a trusted proxy by default, so a test can set `X-Forwarded-For` to simulate Traefik; the "forged header from an untrusted peer" case needs a peer that is not trusted, which real sockets in the factory cannot provide, so build a small `TestServer` or `WebApplication` host from the same extension methods and set `RemoteIpAddress` and `LocalPort` per request (`TestServer.SendAsync(Action<HttpContext>)`).

### Pattern 2: the owner-data store (OWN-02, D-21 to D-27)

**What:** one JSON document, immutable in memory, mutated only under one lock, written atomically, with earlier versions.

**Layering and files**
- `owner-data.json` in the state directory next to `snapshot.json` [VERIFIED: SnapshotStore.FileName = "snapshot.json", Cabinet.Repository/Storage/SnapshotStore.cs:12; the state directory comes from `StorageLocation.Resolve` with key `Storage:Directory` and the service manager's `STATE_DIRECTORY`, Cabinet.Service/Collection/StorageLocation.cs]. Earlier versions in `<state>/owner-data-versions/owner-data.<yyyyMMddTHHmmssZ>.json` (a subdirectory, because `AtomicJsonFile.RemoveStrayTemporaryFiles` only cleans the directory it is given and only `.*.tmp` files).
- Write with `AtomicJsonFile.WriteAtomically` (temp file in the same directory, flush to disk, `File.Move(overwrite: true)`) [VERIFIED: Cabinet.Repository/Storage/AtomicJsonFile.cs].
- Read with `StoredJson.CreateOptions(requiredProperties)` (camel-case, `RespectNullableAnnotations`, strict enum names) and a `schemaVersion` check that refuses a newer version, as `SnapshotStore.Parse` does [VERIFIED: SnapshotStore.cs Parse: `if (snapshot.SchemaVersion > CollectionSnapshot.CurrentSchemaVersion) return SetAside("newer schema")`].
- Difference from the snapshot store (required by the probe resolution): a file that is damaged or newer is never renamed or overwritten. The store reports a state `Unreadable(reason)`, every owner write is refused until a restore, and the file stays where it is. `SnapshotStore.SetAside` renames to `.bad`, which must not be copied.
- Do not rely on `HasUnreadStoredCollection` style flags alone: expose `OwnerDataState` as `Ready(OwnerData)` or `Unreadable(reason, earlierVersions)`.

**Document shape (proposal, schema version 1).** A `kind` marker identifies "this is an owner-data file" for restore.

```json
{
  "kind": "games-cabinet-owner-data",
  "schemaVersion": 1,
  "revision": 42,
  "savedAtUtc": "2030-01-15T21:14:00Z",
  "finish": "classic",
  "locations": [ { "id": "3f9c2a7e", "name": "Cabinet A" } ],
  "games": [
    { "gameId": 1001, "locationId": "3f9c2a7e", "absentSinceUtc": null,
      "picture": { "choice": "link", "checked": true, "seenOwnedUrl": "https://img.example.org/a.jpg", "seenMainUrl": null,
                   "link": { "url": "https://img.example.org/b.jpg", "record": null, "failedChecks": 0, "failedSinceUtc": null } } }
  ],
  "lastDownloadAtUtc": null,
  "changesSinceDownload": 0,
  "changes": [ { "atUtc": "2030-01-15T21:14:00Z", "text": "Added location \"Cabinet A\"" } ]
}
```
(The values above are illustrative placeholders, not read from code.) Notes:
- Locations are an ordered array with a stable server-generated id (`RandomNumberGenerator.GetHexString(8 or 32, lowercase: true)`, verified to exist on this SDK); a rename changes only `name`. Games are an array sorted by `gameId` (deterministic, diff-friendly, duplicates detectable) keyed by the BGG game id as D-20 says.
- `picture.choice` is one of automatic, owned edition, main picture, link (StrictEnumConverter names). `checked` records "Looks right" or an explicit choice. `seenOwnedUrl` and `seenMainUrl` are the candidates' source addresses at the moment of resolution; a later difference is the "Picture changed on BGG" mark. Compare source addresses, not stored file names: file names are a hash of the re-encoded bytes, so a SkiaSharp upgrade would otherwise flag every row [VERIFIED: ArtProcessor.Encode builds `$"{hash}-{width}.webp"` from `SHA256.HashData(bytes)` of the encoded bytes, Cabinet.Repository/Images/ArtProcessor.cs].
- `link.record` holds the processed `ImageRecord` (files, features, colour pair, edges) of the stored copy, so the owner file is self-contained and the sync-owned snapshot is never written. The export omits `record` (derived data), keeps the link address, and the restore re-fetches (D-25).
- `changes`: store more than the 20 shown (suggest a cap of 200) and keep `changesSinceDownload` as an uncapped counter, because the banner counts in full (UI contract). Undo removes the entry and decrements the counter together.
- Earlier versions: keep the newest N=10 (the UI contract's suggestion), rotate by time, not by edit: before a write, if the newest earlier version is older than one hour on the injected `TimeProvider`, copy the current file to a new version file first; always force a copy before a restore, a schema upgrade and when replacing an unreadable file. Rotating on every edit would make ten versions cover only ten clicks.

**Single serialised writer**
- `OwnerDataService` holds a `SemaphoreSlim(1, 1)`; every write is `MutateAsync(Func<OwnerData, MutationResult>)`: load current, apply a pure function, validate, rotate versions if due, write atomically, swap the in-memory reference, call the publisher, schedule the coalesced notification. The lock also serialises restore and the link worker's system writes, so nothing interleaves.
- Mutations carry the client's `revision`. A mismatch is the "This page is out of date" answer (HTTP 409 JSON, or the no-script page). A bad or missing antiforgery token is Kestrel's 400 (verified below) and maps to "This page has expired".
- **Undo** is a swap back to the previous immutable document, valid only while `current.revision == revisionOfThatChange` and nothing else has been written (the undo slot is one reference in memory; a restart clears it, which matches "stays until another change or reload"). The undo produces a new, higher revision. Any other write, including a download, clears the slot. This also removes the log entry and lowers the banner count with no extra code. `Remove link` followed by Undo restores the link address and its record; if the stored files were pruned in between, the link worker fetches them again (same path as a restore).

**Expiry (D-20):** `ReconcileAsync(ownedGameIds, now)` called by the publisher after each commit of an accepted snapshot: stamp `absentSinceUtc` for owner games not in the snapshot (first stamp wins), clear it for games that returned, delete entries whose stamp is 30 days old on the injected `TimeProvider`. Guards: do nothing before the first accepted snapshot (`CollectionState.HasSynced`), and never when the owner data is unreadable. A held-back snapshot never reaches the store, so it cannot start the clock. A restore stamps the clock from the restore moment (UI contract resolution 13).

**Download banner and export:** `lastDownloadAtUtc` and `changesSinceDownload` update when the export is produced; the export document itself carries the values as they will be after the download (so a restore resets the banner from the file, as the UI contract says).

### Pattern 3: applying owner data on top of the snapshot (D-22, D-26)

**What exists** [VERIFIED by reading Cabinet.Service/Collection/CollectionStore.cs, Cabinet.Domain/Collection/SnapshotMapper.cs, Cabinet.Domain/Cards/CardRecords.cs]:
- `CollectionState.FromSnapshot(snapshot, rules)` builds items with `SnapshotMapper.ToCabinetItems` and the version with `SnapshotMapper.Version(items, rules, snapshot)`; layouts and cards are built lazily per design and keyed by `Version` in their ETags (`$"\"{CabinetLayoutEngine.LayoutVersion}-{options.Fingerprint}-collection-{Version ?? "empty"}-{design.Name}\""`).
- The card's location is `stored?.Location` from `SnapshotItem.Location`, and `SnapshotMapper.Version` already hashes `entry?.Location` in `CardText`. `DrawnText` hashes the item's art variant URLs and colours, so a different chosen picture already changes the version.
- Callers of `CollectionState.FromSnapshot`: `SyncStartup` (line 54), `SyncRunner.Commit` (line 208) and `SyncRunner.StoredCollectionStillUnreadable` (line 319), and `ReviewSheetModel` calls `SnapshotMapper.Explain` directly (line 60) [VERIFIED: grep this session].

**Recommended change**
1. Domain: `OwnerOverlay` (pure) built from `(CollectionSnapshot, OwnerData)`: a location text per collection entry (own location, else for an expansion with an owned base the base's effective location, else none) and a `PictureOverride` per game id (choice plus the link's `ImageRecord`). `SnapshotMapper.Explain/ToCabinetItems/Version` gain overloads taking the overlay. Forced pick: `owned` and `main` select that candidate when its record is usable, otherwise fall back to the automatic pick; `link` selects the link's record. The box-shape rule is unchanged: only a chosen picture that reads as a flat cover shapes the box, and an unsure landscape one may only turn it, whatever the owner picked (D-11) [VERIFIED: `BoxShape.Resolve` is called with `chosenVerdict == ArtVerdict.Flat ? widest : null` and the unsure variant, SnapshotMapper.cs Trace].
2. Location through the effective snapshot: build `effectiveSnapshot = snapshot with { Items = items with Location }` and feed that to `CardRecords.Build` and `SnapshotMapper.Version`, so card text and version pick the owner's location up with no new field. Keep `CollectionState.Snapshot` as the raw snapshot, because `SyncRunner` compares against it (`baseline = _stored ?? _collection.Current.Snapshot`) and a sync must never see overlaid items as "changed".
3. Service: `CollectionPublisher` with a lock is the only code that calls `FromSnapshot` and `CollectionStore.Replace`. Its entry points: `Publish(snapshot)` (sync commit, start-up, unreadable retry) and `Republish()` (an owner write: current raw snapshot plus the new owner data). Inject it into `SyncRunner` and `SyncStartup` in place of building states themselves. Without the lock, an owner write and a sync commit read-modify-write the store concurrently and one wins by accident.
4. `ArtCache.Prune` needs the owner link files as referenced names. Today `SyncRunner.Prune` builds `referenced` only from `snapshot.Images` [VERIFIED: SyncRunner.cs Prune]; add an extra provider (for example a `Func<IEnumerable<string>>` passed to the runner) and skip pruning entirely while the owner data is unreadable, otherwise a damaged owner file would let the next sync delete every linked picture older than the 7-day grace (D-13).
5. Explicit "picture choice in the version": D-26 asks for it, but the version already changes exactly when the drawn output changes. Keeping it derived avoids a needless one-time cache refresh; pin it with a test (changing a choice changes the version iff something drawn changes; an overlay that changes nothing keeps the version). This is an Open Question for the planner.
6. The pictures page's candidate tiles can be built from `SnapshotMapper.Explain(rawSnapshot, rules)` traces (`VersionImage`, `MainImage`, verdicts, `Pick`) plus `snapshot.Images[url].Status` to tell "BGG has none" (no address) from "won't load" (record not Ok) and "fetching" (no record yet). One shared mapping `ArtVerdictKind` (flat, unsure, 3D, cut-out, tight crop) serves the tile tags and the review sheet; add the test that fails for a kind with no string.

**Effective location and expansions.** Resolve the base through `ExpansionPairing.Pair(snapshot)`, the pairing the layout already uses to stand an expansion beside its base: it returns the owned base whose earliest collection entry has the lowest collection id, and only when no base is owned does it return the full expands list (so "has an owned base" must be checked against the owned base set, not against the list length) [VERIFIED: Cabinet.Domain/Collection/ExpansionPairing.cs]. See Open Question 1: the UI contract says "lowest BGG id"; the card's `BasesOf` orders by BGG id, the layout by collection id.

**Live notification (D-26).** `status.Current()` already carries `SnapshotVersion = collection.Version`, and the page redraws when it differs from the version on screen (`shouldRedraw` in status.js) [VERIFIED: Cabinet.Service/Sync/CabinetStatus.cs, wwwroot/js/status.js:111-115]. So after `Republish()` an owner save needs only `live.PublishAsync(status.Current(), ct)`, coalesced: `OwnerChangeNotifier` keeps one `TimeProvider.CreateTimer` (verified available), resets it on each save to about 3 seconds (suggest also a 15 second ceiling so a long edit session still refreshes), and on firing publishes only if the version differs from the last published one. A sync that runs at the same time publishes its own statuses; a duplicate is harmless because the page compares versions.

### Pattern 4: the picture override pipeline (IMG-02, D-09 to D-15)

**Link policy (what to accept and refuse, in this order).** Reuse `ArtUrl.Canonical` and `ArtSourcePolicy.Allows`; classify before fetching so each refusal has its own message.

| Step | Test | Result |
|------|------|--------|
| 1 | blank | "Paste a picture link first." |
| 2 | `ArtUrl.Canonical(text)` is null (not http or https, relative, over 2048 characters) | "That isn't a web address." |
| 3 | host is `boardgamegeek.com` or any `*.boardgamegeek.com` (case-insensitive, whatever the path) | the gallery page-link message with the "Copy image address" how-to. Resolving a page link would need BGG's private website API, which is not licensed (D-09) |
| 4 | `ArtSourcePolicy.Allows(uri)` is false | "That link isn't from BGG's picture host", naming the allowed host from `Images:AllowedHosts`. `Allows` demands https, the default port, no user information and an exact host match, so `cf.geekdo-images.com.evil.example`, a trailing-dot FQDN, a port or `user@host` are all refused [VERIFIED: Cabinet.Repository/Images/ArtUrl.cs ArtSourcePolicy.Allows] |
| 5 | fetch through the token-less `IArtSource` | see mapping below |
| 6 | `ArtProcessor.Process(bytes, limits)` | see mapping below |

**URL shape.** Current BGG picture addresses look like `https://cf.geekdo-images.com/<id>__original/img/<sig>/0x0/filters:format(png)/pic<N>.png`, with `__thumb` and other size words in the first path segment and a signed middle segment, so a size cannot be changed by editing the path [CITED: github.com/MatthewThompson/arnak test_data/game/game_expansion.xml, fetched this session: thumbnail shape `.../<id>__thumb/img/<hash>/fit-in/200x150/filters:strip_icc()/pic6253876.png`, image shape `.../<id>__original/img/<hash>/0x0/filters:format(png)/pic6253876.png`; the same shape is recorded in the earlier picture research]. Which variant the gallery's "Copy image address" yields (`__imagepage`, `__original` and so on) is [ASSUMED] and does not matter: the policy checks the host, and decodability decides the rest. Do not validate the path beyond the host. Do not fetch any BGG page from the app.

**Fetch result mapping and the two gaps found.**
- `ImageDownloader` returns `ArtDownload.Fetched(bytes)`, `Turned(reason)` with reasons `host`, `redirect`, `type`, `size`, or `Missed(reason)` with `status`, `unavailable`, `timeout` [VERIFIED: Cabinet.Repository/Images/ImageDownloader.cs]. The specified copy needs two things it does not carry: the HTTP status (to say "That picture isn't there" for 404 versus "couldn't fetch") and the size of a too-large picture (`({size} MB; the limit is {limit} MB)`). Either extend `Missed` and `Turned` with an optional status code and an optional announced size (additive; `ArtSync` ignores them), or drop `{size}` from the copy and show only the limit. Open Question 4.
- `ArtProcessor.Process` returns `Done`, `Unreadable` or `Turned("pixels")`. A header shape beyond ten to one also comes out as `Unreadable` (via `ArtAnalysis.HasUsableShape`), so "very long and thin" cannot be told from "not a picture". Add a small public inspect helper that reads the header (`SKCodec`) and reports pixels and shape before `Process`, rather than changing `Process`'s outcome (existing tests and `ArtSync` map `Unreadable` to the `Undecodable` status).
- The sync's timeout comes from the singleton `ArtLimits` (`Images:DownloadTimeoutSeconds`, 5 to 60, default 30). The owner flow wants about 15 seconds: wrap the call in a linked `CancellationTokenSource` and map a cancellation that is not the request's own to the timeout message; do not build a second downloader. The shared `IImagePacer` serialises owner and sync downloads (1 second gap by default), so an owner check may wait behind one sync download; that is fine and keeps the CDN polite.
- Resolve `IArtSource` through the same `Func<IArtSource>` seam the sync uses (a typed client registered with `AddHttpClient<IArtSource, ImageDownloader>` and `BggTransport.CreatePrimaryHandler`, which sets `AllowAutoRedirect = false` and `UseCookies = false`) so pooled connections follow handler lifetime and no auth handler is attached [VERIFIED: Cabinet.Service/Sync/SyncEndpoints.cs, Cabinet.Repository/Bgg/BggTransport.cs].

**Preview staging: in memory, not files (deviation from the UI contract's wording, same intent).** The contract says "a staged processed file served by the site (a short-lived same-origin URL)" and "staged files are swept after 15 minutes". Both goals (CSP forbids `blob:` and `data:`; nothing lingers) are met more simply by holding the processed variants in a small expiring in-memory store and serving them from `GET /owner/preview/{token}`: the token is `RandomNumberGenerator.GetHexString(32, lowercase: true)`, the handler checks `^[0-9a-f]{32}$` and looks the token up in a dictionary, so no filesystem path is ever built from request input (no traversal surface), nothing needs sweeping, and a restart simply expires everything. Cap the store (suggest 8 entries, one per game, 15 minutes on `TimeProvider`), respond with `Results.File(bytes, "image/webp")`, `Cache-Control: no-store` and `X-Content-Type-Options: nosniff`, 404 for unknown or expired tokens. If the planner prefers files, the directory must be a fixed subdirectory of the state directory, names generated server-side, validated with the same pattern, and wiped at start-up. The same store type serves the pending restore check (below).

**Applying "Use this".** Write the variants with `ArtCache.Write(EncodedArt)` (it validates the name against `^[0-9a-f]{16}-[0-9]{1,4}\.webp\z` [VERIFIED: ArtCache.cs:91]) first, then mutate the owner data (set the link address and the `ImageRecord`, choice link, checked, `seen*` addresses, change-log entry), then publish. Files first is safe against the prune: it skips any file whose last write is within the grace period [VERIFIED: ArtCache.Prune `File.GetLastWriteTimeUtc(file) >= cutoff`]. Build the `ImageRecord` exactly as `ArtSync.Store` does (files widest first, features, main colour, `SpineColour.PairFor`, edges, `ArtProcessor.AnalysisVersion`); extract that into one helper used by both so the two cannot drift.

**Stored copy, dead links, restored links (D-13).** Run a small hosted `OwnerLinkWorker` (a wake-up channel plus an hourly `PeriodicTimer` on `TimeProvider`) that, for each owner link, fetches when there is no usable record (restore, undo of a removal), when the record's `AnalysisVersion` differs from `ArtProcessor.AnalysisVersion` (otherwise a future analysis bump leaves link records stale, as the sync handles for its own records), when a stored file is missing from the cache, or when a weekly re-check is due. A failed re-check keeps the record and its files, increments `failedChecks` and sets `failedSinceUtc`; mark "Link no longer works" after two failed checks at least a day apart so one CDN blip does not put a row back into "worth a look"; a later success clears it. These writes are system mutations through the same single writer (no change-log entry, not counted by the banner). Because it only ever reads `OwnerData` and `ArtCache` it needs no change to `ArtSync`.

**Worth a look (the rule the UI contract fixes, expressed on the data).** For each game, with `v` = usable owned-edition record, `m` = usable main record, `pick` = the automatic pick from `Explain`:
1. candidate when `v` exists and `pick != VersionImage` (the override worklist rule; games with no owned-edition picture are not candidates for this reason);
2. candidate when the chosen picture is missing or its record is not Ok (no usable art, or "won't load");
3. candidate when the choice follows BGG (owned, main, or automatic that was resolved) and the current candidate address differs from the stored `seenOwnedUrl` or `seenMainUrl` ("Picture changed on BGG");
4. candidate when the link is marked dead.
Resolved = `checked` is true and none of 3 or 4 arose after it. The counter is `unresolved / candidates`. Resolving (Looks right or an explicit choice) rewrites `seen*` with the current addresses.

### Pattern 5: forms, antiforgery, JSON versus redirect, revision and undo

**Razor Pages handlers, thin, over one service.** Each page is a `PageModel` with named handlers (`OnPostChooseAsync`, `OnPostAddLocationAsync`, ...); each calls `OwnerDataService`/`OwnerPictureService` and returns either JSON (when `Accept` contains `application/json`, which `fetch` sets) or a post-redirect-get to the same row (`#game-{id}`) for the no-script path. Both branches were exercised in a scratch Razor Page this session: header-token JSON post returned the JSON result, form-field post returned a 302 back to the page. Redirect targets are fixed (`RedirectToPage` with the known anchor), never a request value. Minimal endpoints would have meant writing the form-binding and the no-script fallback twice.

**Antiforgery facts [VERIFIED: scratch Razor Pages app, SDK 10.0.112]:** the default header name is `RequestVerificationToken`, the default form field `__RequestVerificationToken`, the cookie is `HttpOnly` with `SameSite=Strict`, and its `SecurePolicy` default is `None`. A POST without a token answered 400 with no body; a header token answered 200 for a JSON body and for an `Accept: application/json` post; a form-field token answered with the redirect; a valid token without the cookie answered 400. Razor Pages validate automatically and need no `UseAntiforgery` middleware; the documented minimal-endpoint path only validates endpoints that bind form data [CITED: learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0]. Set `Cookie.SecurePolicy = Always` outside Development and Testing, give the cookie a stable name, and deliver the token to script as `<meta name="request-verification-token" content="@Af.GetAndStoreTokens(HttpContext).RequestToken">` (no inline script) while forms keep the hidden field the form tag helper emits. Data Protection keys: the scratch run logged "User profile is available. Using '/home/.../.aspnet/DataProtection-Keys' as key repository; keys will not be encrypted at rest"; the unit sets `Environment=HOME=/var/lib/cabinet` and `StateDirectory=cabinet` [VERIFIED: deploy/systemd/cabinet.service:12-13], so keys persist in the state directory across restarts and releases and tokens do not all expire at each release. Map a 400 from validation to the "This page has expired" message in the script (the answer carries no body).

**Revision and undo.** Every mutating request carries the `revision` it was rendered with; the service compares under the lock and answers 409 when stale. Responses return the new revision, the row's new state, the status text and whether Undo is available; script keeps the page's revision current from each response, so rapid edits in one tab never conflict and a second tab does. Undo posts `undo` with the revision of the change it reverts and gets the same 409 treatment ("Can't undo because something changed after this.").

**Size limits.** Apply a small default body limit to owner posts (64 KB) and a specific `[RequestSizeLimit]` on the restore handler (the UI contract's 2 MB), and read `IFormFile` once into a bounded buffer. A request-size breach is answered 413 by Kestrel when the body is read.

**Restore (OWN-02, D-25).** Two steps with a pending-check store identical to the picture preview store. Step 1: parse with `JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 16 })` (a deeper document throws `JsonReaderException`, verified), read `kind` and `schemaVersion` first (newer than known gives the "newer format" message; older is migrated), then deserialize strictly with `StoredJson.CreateOptions` and validate: the file is an owner-data file; at most 50 locations; names trimmed, 1 to 60 characters, unique case-insensitively, no control characters and no bidirectional override or isolate characters; ids unique and matching the id pattern; every assignment references an existing location; at most a few thousand game entries; each link address passes `ArtUrl.Canonical` and `ArtSourcePolicy.Allows` (a bad link fails the file with "holds more than this site accepts" rather than silently dropping data). Return the summary and the comparison with the live data and stage the parsed document under a token. Step 2: apply under the lock: force an earlier-version copy of the current file (or of the unreadable bytes), replace the document, bump the revision, stamp the 30-day clocks from now, wake the link worker, publish. "Use this version" for an earlier version reads from a server-generated list of version files (an index into an enumeration of the version directory, never a request-supplied name) and goes through the same check and confirm.

### Pattern 6: front end under the strict CSP

- **Settled-value select.** In Chromium 153 on Linux (the Playwright browser installed here), three `ArrowDown` presses on a focused closed `<select>` produced `input:A change:A input:B change:B input:C change:C`; `Alt+ArrowDown` produced nothing [VERIFIED: scratch Playwright run this session]. Firefox behaves like the other browsers on Windows and Linux and macOS opens the popup on arrow keys [CITED: WordPress Trac ticket 40925 and jQuery ticket 14186 via web search, 2011 to 2017; current Firefox not re-tested]. Implementation: on `change`, if a `keydown` on that select happened in the last ~100 ms, debounce 600 ms (reset on each change); flush at once on `focusout`, `Enter` and `Tab`; a change with no preceding key (pointer or touch commit, which fires once) flushes immediately; flush with `fetch(..., { keepalive: true })` on `visibilitychange` to hidden (a `sendBeacon` cannot carry the antiforgery header). Pin it with a browser test: focus, press `ArrowDown` three times, wait past 600 ms, assert exactly one request and one new change-log entry.
- **Dialogs.** Native `<dialog>` with `showModal()`; `Escape` cancels natively; handle backdrop clicks by `event.target === dialog` plus a bounds check on the click point (a click on the dialog's own padding also targets the dialog); put `autofocus` on the safe button (`Keep it`, first location button) and assert `document.activeElement` in a browser test, because the focus-delegation behaviour is [ASSUMED] from the HTML dialog focusing steps and must be verified in the target browser; return focus explicitly to the opener on close.
- **Dock.** A `hidden` attribute is `display: none`, so a slide-in needs the attribute removed, a forced reflow, then a `data-state` change that carries the `translate` transition; under `prefers-reduced-motion: reduce` skip the transition. The list needs bottom padding `calc(80px + env(safe-area-inset-bottom))` as the contract says.
- **Where owner scripts live matters for the lint.** The markup-API ban (`innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write`, `.cssText`) is applied only to tracked files under `Cabinet.Service/wwwroot/js/` [VERIFIED: build/lint/checks/10-repo-rules.sh PAGE_SCRIPT_PATTERN `^Cabinet\.Service/wwwroot/js/` and its call on `page_script_files`]. Keep the owner modules as `Cabinet.Service/wwwroot/js/owner-*.js` (and `wwwroot/css/owner.css`, as the UI contract names them), or extend the pattern and its self-test; do not create a second script folder that silently escapes the ban. The `//` ban covers every tracked `.js` and `.mjs`.
- **Reuse.** `card-view.js` builds the card; add the Edit line only when the dialog carries `data-edit-href` (written by the server on the owner port only). `copy.js` gets the two bilingual strings in both tables; `build/tests/copy-parity.test.mjs` already checks equal keys and kinds in both tables, plain dots only and informal Dutch. The owner pages' English strings live in one frozen object in `owner-format.js` or a sibling, not in `copy.js` (which is bilingual by design). `cabinet.js` already holds the card records; have it dispatch a generic `CustomEvent` on the document after the cards arrive and after each redraw (it must name no owner path), and let `owner-cabinet.js`, loaded only at the owner address, set `data-unplaced` on boxes whose card `location` is empty. A redraw recreates box elements, so re-apply after every redraw event.
- **Pure logic for `node --test`:** change counts and singular or plural, the `today at 21:14` day words, "{n} of {m} to check", position announcements, result counts, the worth-a-look counter wording. Load modules the way `build/tests/page-scripts.test.mjs` does (read the file, import through a base64 data URL), which needs no package or module config.
- **Times.** The server cannot know the owner's zone. Render `<time datetime="...">` with a UTC fallback text (reuse the `SyncStatusText.ExactUtc` idea) and let `owner-format.js` rewrite it to `today at 21:14` style in the browser's zone using the existing `en-NL` locale trick from `copy.js` [VERIFIED: `const TIME_LOCALE = 'en-NL';` in wwwroot/js/copy.js:5]. Open Question 6.

### Pattern 7: deploy surface (D-01, D-28)

| File | Change |
|------|--------|
| `deploy/nftables/cabinet.nft.in` | Add `tcp dport 5082 ip saddr @CABINET_TRAEFIK_IP@ accept` beside the 5080 rule and update the header comment. Same token, no new provisioning variable |
| `deploy/tests/render-templates-test.sh` (lines 159 to 167) | The test asserts `exactly two tcp dport rules exist` and `no rule mentions the loopback ops port` (5081) [VERIFIED: those lines read this session]; change the count to three, add the 5082 assertion, keep the 5081 one |
| `deploy/provision.d/50-firewall.sh` | Only the final log line mentions the app port; update the wording |
| `deploy/traefik/cabinet.yml.example` | Add router `cabinet-owner` (`Host(\`owner.example.com\`)`, same `cabinet-lan-only` allow-list and `cabinet-security-headers`, `tls.certResolver: letsencrypt`) and service `cabinet-owner` to `http://192.0.2.20:5082`; say in the header that the owner router keeps its allow-list permanently even when the public router later drops it. The same test file has Traefik example assertions to extend |
| `deploy/cabinet.env.example` | Placeholders only: `Kestrel__Endpoints__Owner__Url=http://0.0.0.0:5082`, `Owner__AllowedRanges__0=192.0.2.0/24`, `Owner__AllowedRanges__1=198.51.100.0/24`, with a note that `ReverseProxy__KnownProxies__0` must be the Traefik address |
| `deploy/bin/cabinet-selfcheck` | `check_listeners` (currently asserts 5080 listens and 5081 is loopback only) gains the owner port; add loopback probes: a forged `X-Forwarded-For` outside the ranges to the owner port gives 403, an address from the first configured range gives 200 on `/`, and `/owner/pictures` on the public port gives 404. The probes work because the loopback peer is a trusted proxy by default [VERIFIED: default `KnownIPNetworks` = `127.0.0.0/8` in scratch run]. Update the stubs in `deploy/tests/sandboxing-test.sh` (lines 309 to 318) |
| scripted route check (new) | Run from the owner's home machine, not on the server: `build/check-owner-routes.sh` with the two hostnames from the environment (never committed): owner host `/`, `/owner/pictures`, `/owner/settings` answer 200 and the cabinet carries the owner plate; public host `/owner/pictures` and `/owner/preview/<32 zeros>` answer 404 and `/` carries no owner path. Test it with a stub `curl` the way the other script tests do (`build/tests/*-test.sh` run in the lint check `50-script-tests.sh`). Keep it out of `deploy/bin` so the installer's drift comparison of installed scripts is not affected |
| Docs | `docs/lxc-setup.md` (port table lines 18 to 19, the Traefik and DNS steps), `docs/deploy.md`, `docs/development.md` (how to run the owner port locally against the fake BGG), `docs/review-sheet.md`, `docs/bgg-sync.md` settings table; no planning references |
| `deploy/systemd/cabinet.service` | No change: `StateDirectory=cabinet` and `ProtectSystem=strict` already cover the new files; no new write path |

Rollback and release behaviour: the state directory is untouched by a release; an older release does not read `owner-data.json` (the owner tools simply do not exist there); a newer file schema read by an older build is the `Unreadable` state, never a rename. If a deploy sets the owner endpoint without ranges the app refuses to start, the health check fails and the installer rolls back.

### Folded fixes: where they live

| Item | Where and what (each with a failing test first) |
|------|---|
| Record details attempts | `EnrichmentPlanner.Plan` treats every id without details as "fresh" every run [VERIFIED: Cabinet.Domain/Collection/EnrichmentPlanner.cs `var fresh = ids.Where(id => !known.ContainsKey(id))`]; `EnrichmentSync` ignores games the answer leaves out. Add an optional `DetailAttempts` (game id to time) to `CollectionSnapshot` (additive; leave `CurrentSchemaVersion = 2` so an older release can still read the file, `StoredJson` skips unknown members by default), have `EnrichmentSync` stamp requested ids that came back absent, have the planner skip a fresh id whose attempt is younger than a retry interval, and teach `SyncRunner.ContentChanged` and `StillOwnedGames` about the new property |
| `BoxShape.FromCover` plausibility | `Rebuilt` already returns null when a side is outside `BoxFromVersion.MinFrontMm..MaxFrontMm` via `IsPlausible`; `FromCover` has no such check [VERIFIED: BoxShape.cs]. Apply the same check and fall back to the estimate with `BoxSource.Estimate` (or `Default`). The bounds are `public const int MinFrontMm = 50;` and `public const int MaxFrontMm = 700;` [VERIFIED: BoxFromVersion.cs:15,18], and a header shape up to `public const int MaxAspectRatio = 10;` [VERIFIED: ArtAnalysis.cs:27] reaches `FromCover`, so a one-to-ten cover on a 300 mm estimate gives a 30 mm side, below the minimum: that is the failing test |
| `SnapshotMapper.Explain` order independence | `GroupBy(CollectionId)` then `entry.FirstOrDefault(Kind == Expansion) ?? entry.First()` depends on source order among entries sharing a collection id [VERIFIED: SnapshotMapper.cs Explain]. Order each group explicitly (expansion first, then `GameId`, then title ordinally). `CardRecords.BuildContext` uses first-wins `TryAdd` by collection id too; check it in the same change |
| Review sheet | Verdict text `3D shot`, `3D shot, cut-out`, `3D shot, tight crop` at `ReviewSheetModel.cs:122-124` and an exact-match count; derive counts by kind from the shared `ArtVerdictKind`. `ReviewSheetCommand` calls `SnapshotStore.Load()`, which renames bad files to `.bad` [VERIFIED: SnapshotStore.SetAside]: add a read-only load and assert the file is untouched. Also `--bold-font` without `--font`, caught path errors, surrogate-safe truncation |
| Finish slot | Owner data holds `finish` (allow-list: `classic`); `<body class="finish-classic">` is written by both layouts on both ports; an unknown stored value falls back to classic; no visual change |

## Anti-Patterns to Avoid

- **Matching the port through the Host header** (`RequireHost`, `Request.Host.Port`): client-controlled, and behind Traefik the header carries no app port. Use `Connection.LocalPort`.
- **Trusting `RemoteIpAddress` without a known proxy:** with an empty or wrong `ReverseProxy:KnownProxies` the proxy's own address becomes the client and the second wall opens. Fail closed at startup.
- **Copying `SnapshotStore.SetAside` for the owner file:** it renames a damaged or newer file to `.bad`. The owner file is the only copy of hand-entered data; never rename or overwrite it until the owner restores.
- **Merging owner data into `snapshot.json`** or into the sync's `_stored` baseline: a sync commit then loses owner edits or sees overlaid items as changed on every run.
- **Two code paths that build `CollectionState`:** race between an owner write and a sync commit. One publisher with a lock.
- **Per-keystroke saves from a native select** and a native select inside the dock for bulk moves.
- **Building a filesystem path from a request value** for previews, earlier versions or art files. Tokens and indexes are looked up in server-side collections.
- **Putting owner scripts outside `wwwroot/js/`** and thereby outside the markup-API lint.
- **Writing `D-NN`, requirement keys, `phase N` or planning file names** in code, tests, docs or comments (lint failure).
- **Reading the owner's address or location names into logs** (no client addresses, no names; counts and categories only).

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| CIDR membership | a bit-mask parser | `System.Net.IPNetwork` (fully qualified) | Handles v4 and v6 prefixes; the pitfalls (`/0`, whitespace, mapped forms) are listed above and covered by startup validation |
| Forwarded client address | reading `X-Forwarded-For` yourself | the existing `UseForwardedHeaders` with `KnownProxies` and `ForwardLimit = 1` | Trust only from the known proxy; last entry only |
| CSRF | a custom token scheme | built-in antiforgery (header `RequestVerificationToken`) | Verified behaviour above; Data Protection keys persist in the state directory |
| Atomic file replace | write then rename by hand | `AtomicJsonFile.WriteAtomically` | Temp file in the same directory, flush to disk, `File.Move(overwrite: true)` |
| Strict stored JSON | ad hoc validation | `StoredJson.CreateOptions` plus explicit limit checks | Required members, strict enum names, nullable annotations |
| Random tokens and ids | `Guid` or `Random` | `RandomNumberGenerator.GetHexString(n, lowercase: true)` | Cryptographic, lowercase hex that fits the validation regex |
| Debounce and expiry timers | `Task.Delay` loops | `TimeProvider.CreateTimer` and `PeriodicTimer(…, timeProvider)` | Tests drive them with `FakeTimeProvider` |
| Picture fetch, policy, caps | a second HTTP client | `IArtSource` (`ImageDownloader`), `ArtSourcePolicy`, `ArtLimits` | Redirect re-checks, byte cap, time limit, no credentials already tested |
| Decode, resize, measure | a new image routine | `ArtProcessor.Process` | Pixel and shape caps and exception containment already there |
| Cover and spine rules in the preview | a CSS lookalike | the cabinet's own `.placement` markup and the engine's `SpineLabel` and `SpineColour` | The preview cannot drift from the real cabinet |
| Live refresh | a second channel | `ILiveNotifier` with `status.Current()` | The page already redraws when the version differs |
| Modal, focus trap, Escape | a custom overlay | native `<dialog>` | The card dialog already uses it |
| Natural sorting and search folding | custom comparers | `Intl.Collator('en', { numeric: true, sensitivity: 'base' })` | Matches the contract |
| Verdict wording | per-page switches | one `ArtVerdictKind` mapping | One string per kind, one test |

**Key insight:** every hard part of this phase (atomic persistence, strict parsing, capped and policy-checked fetching, versioned caches, live refresh) already exists in a tested form; the work is composing them behind one lock and two gates, and the risk is in the seams (publisher, prune, proxy trust), not in new algorithms.

## Common Pitfalls

### Pitfall 1: the second wall silently reduces to the first
**What goes wrong:** `RemoteIpAddress` is the Traefik address (normally inside the LAN range) when forwarded headers are not applied, so every request on the owner port passes the range check.
**Why it happens:** `KnownProxies` empty or mistyped; headers from unknown peers are ignored.
**How to avoid:** fail startup when the owner endpoint is configured and no known proxy is set; selfcheck probes with a forged `X-Forwarded-For` through loopback; one documented value shared with the firewall.
**Warning signs:** a forged `X-Forwarded-For` from a probe still gets 200.

### Pitfall 2: tests that believe a forged header is ignored
**What goes wrong:** the test factory's client connects from loopback, which is a trusted proxy by default, so a forged `X-Forwarded-For` is honoured and "ignored from non-proxies" cannot be shown on real sockets.
**How to avoid:** test the untrusted-peer case on a small `TestServer` host built from the same extension methods, with `RemoteIpAddress` and `LocalPort` set per request through `server.SendAsync(context => ...)`. Verified in a scratch host (`Microsoft.AspNetCore.TestHost` 10.0.12, `KnownProxies` cleared and set to one address): a request from the known proxy with `X-Forwarded-For: 203.0.113.9` gave `remote=203.0.113.9 local=5082`; the same header from another peer gave `remote=<the peer> local=5082`; `KnownIPNetworks.Clear()` removes the loopback default. The app itself only adds to the defaults, so loopback stays a trusted proxy in production; that is what makes the selfcheck probe possible, and a deliberate choice to keep or clear.

### Pitfall 3: unqualified `IPNetwork`
**What goes wrong:** CS0104 where `System.Net` and `Microsoft.AspNetCore.HttpOverrides` are both imported (`Program.cs` is such a file) [VERIFIED].
**How to avoid:** `System.Net.IPNetwork` in full or an alias; never the obsolete `ForwardedHeadersOptions.KnownNetworks` (use `KnownIPNetworks`).

### Pitfall 4: the prune deletes override files
**What goes wrong:** `ArtCache.Prune` keeps only names in `snapshot.Images`; a linked copy older than the 7-day grace is deleted at the next sync. During an unreadable owner file the same happens to every link.
**How to avoid:** add the owner link file names to the referenced set; skip pruning while the owner data is unreadable; a unit test with a link file older than the grace and a sync round.

### Pitfall 5: a sync loses an owner write (or the reverse)
**What goes wrong:** two code paths build and replace `CollectionState` from stale inputs.
**How to avoid:** `CollectionPublisher` with one lock; `CollectionState.Snapshot` stays raw; test: interleave an owner save with a sync commit on a fake clock and assert both survive.

### Pitfall 6: a native select saving per arrow key
Verified above. Debounce plus flush; one browser test.

### Pitfall 7: a dock that cannot slide
`hidden` is `display: none`; transitions do not run from it. Remove the attribute, force reflow, then switch the state.

### Pitfall 8: antiforgery in tests and development
`SecurePolicy = Always` breaks plain-http test hosts and local runs; apply it only in Production. A failed validation is a bodyless 400, so the script cannot read a message; map the status.

### Pitfall 9: server-rendered times in the wrong zone
The server has no viewer zone; render UTC text inside `<time datetime>` and let the script localise (Open Question 6).

### Pitfall 10: the owner link check waits behind the sync
The shared image pacer serialises downloads; a check during a long sync waits one gap. Use a call-level 15 second limit and a clear "Checking..." state; do not add a second pacer.

### Pitfall 11: owner static files are reachable by URL on the public port
`MapStaticAssets` serves `wwwroot` on both ports, so `/js/owner-pictures.js` and `/css/owner.css` answer 200 on the public port. They hold only route strings. The contract that matters (and that a test should pin) is that the public HTML, the shared scripts, `/cabinet/layout` and `/cabinet/cards` mention no owner path. If the owner wants the files hidden too, add the metadata via a convention on `MapStaticAssets()` or a prefix guard, extend the lint pattern, and test the path tricks; this is a hardening choice, not a requirement.

### Pitfall 12: the 30-day clock and an empty or failed sync
Expiry must not run before the first accepted snapshot, on a held-back result, or while the owner data is unreadable; the clock starts at the first accepted snapshot that lacks the game and is cleared when the game returns.

### Pitfall 13: link records go stale at the next analysis bump
`ArtProcessor.AnalysisVersion` (3 today [VERIFIED: ArtProcessor.cs]) is raised when measurements change; the sync reprocesses its own records, the worker must reprocess the owner's.

### Pitfall 14: the factory's port tuple
`CabinetWebApplicationFactory` picks and exposes only `(Public, Ops)` ports and `TestHostPortTests` call `PickFreeLoopbackPorts` and the tuple-returning constructor [VERIFIED: files read this session]. Adding an owner port changes those internal signatures and their tests; extend them deliberately (a third port, owner endpoint and ranges written into the in-memory configuration, `CreateOwnerClient()`), and extend `CabinetPageTest` with an `OwnerPort`.

### Pitfall 15: `.cshtml` and test strings are linted too
Planning-reference patterns apply to every tracked file, so a Razor comment, a test name or an exception message with a decision id or `phase 6` fails the lint.

### Pitfall 16: location names are public the moment they are assigned
`CardRecord.Location` is already in the public `/cabinet/cards` payload and renders on the card as soon as it is set. Names are therefore treated as public data: the owner pages say so, validation rejects control and bidirectional override characters, and every render uses `textContent` or Razor encoding.

### Pitfall 17: Undo across tabs and downloads
The undo slot is one in-memory reference tied to a revision; any other write (including a download) clears it. Show "Can't undo because something changed after this." on a mismatch and change nothing.

## Code Examples

Shapes only; names are proposals. They follow the repository's comment rules (`///` in C#, `/** */` in JavaScript, no `//` anywhere).

### The two gates and the metadata marker

```csharp
using IpNetwork = System.Net.IPNetwork;

/// <summary>Marks an endpoint or page that exists only on the owner port.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class OwnerOnlyAttribute : Attribute;

/// <summary>The owner port and the client ranges, read once at startup.</summary>
public sealed record OwnerAccessOptions(int? Port, IReadOnlyList<IpNetwork> Ranges)
{
    /// <summary>Whether the request arrived on the owner listener.</summary>
    public bool IsOwnerPort(HttpContext context) => Port is { } port && context.Connection.LocalPort == port;

    /// <summary>Whether the client address, as resolved by the forwarded-headers middleware, lies in a configured range.</summary>
    public bool Allows(IPAddress? client)
    {
        if (client is null)
        {
            return false;
        }

        var address = client.IsIPv4MappedToIPv6 ? client.MapToIPv4() : client;

        return Ranges.Any(range => range.Contains(address));
    }
}

/// <summary>Wall two: on the owner port, only configured ranges get through; everything else gets a bare 403.</summary>
public static IApplicationBuilder UseOwnerClientRange(this IApplicationBuilder app) =>
    app.Use(async (context, next) =>
    {
        var options = context.RequestServices.GetRequiredService<OwnerAccessOptions>();

        if (options.IsOwnerPort(context) && !options.Allows(context.Connection.RemoteIpAddress))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.Headers.CacheControl = "no-store";
            await context.Response.WriteAsync("Not available from this address.");

            return;
        }

        await next(context);
    });

/// <summary>Wall one: an owner-only endpoint answers an empty 404 on any other port.</summary>
public static IApplicationBuilder UseOwnerOnlyRoutes(this IApplicationBuilder app) =>
    app.Use(async (context, next) =>
    {
        var options = context.RequestServices.GetRequiredService<OwnerAccessOptions>();
        var ownerOnly = context.GetEndpoint()?.Metadata.GetMetadata<OwnerOnlyAttribute>() is not null;

        if (ownerOnly && !options.IsOwnerPort(context))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;

            return;
        }

        await next(context);
    });
```

Registration (all three marker styles were exercised in the scratch build): `options.Conventions.AddFolderApplicationModelConvention("/Owner", model => model.EndpointMetadata.Add(new OwnerOnlyAttribute()))`, `[OwnerOnly]` on a `PageModel`, and `.WithMetadata(new OwnerOnlyAttribute())` on a minimal endpoint. Order: `UseContentSecurityPolicy`, `UseForwardedHeaders`, `UseOwnerClientRange`, the ops health check, `UseCabinetArt`, `UseRouting`, `UseOwnerOnlyRoutes`, then the maps.

### One mutation under the single writer

```csharp
/// <summary>Applies one pure change to the owner data under the lock, then writes, publishes and schedules the notification.</summary>
public async Task<OwnerResult> MutateAsync(int expectedRevision, Func<OwnerData, OwnerChange> change, CancellationToken cancellationToken)
{
    await _gate.WaitAsync(cancellationToken);

    try
    {
        if (_state is not OwnerDataState.Ready ready)
        {
            return OwnerResult.Unreadable();
        }

        if (ready.Data.Revision != expectedRevision)
        {
            return OwnerResult.OutOfDate();
        }

        var applied = change(ready.Data);
        var next = applied.Data with { Revision = ready.Data.Revision + 1, SavedAtUtc = _time.GetUtcNow() };

        _store.Save(next, rotateVersions: _versions.IsDue(_time.GetUtcNow()));
        _state = new OwnerDataState.Ready(next);
        _undo = new UndoSlot(next.Revision, ready.Data);
        _publisher.Republish();
        _notifier.Schedule();

        return OwnerResult.Saved(next, applied.Status);
    }
    finally
    {
        _gate.Release();
    }
}
```

### Dual-mode Razor handler

```csharp
/// <summary>Chooses a picture for a game; answers JSON to script and a redirect to the same row otherwise.</summary>
public async Task<IActionResult> OnPostChooseAsync(ChooseInput input, CancellationToken cancellationToken)
{
    var result = await _pictures.ChooseAsync(input.GameId, input.Choice, input.Revision, cancellationToken);

    return Request.Headers.Accept.ToString().Contains("application/json", StringComparison.OrdinalIgnoreCase)
        ? new JsonResult(result.ToJson()) { StatusCode = result.HttpStatus }
        : RedirectToPage(null, null, $"game-{input.GameId}");
}
```

### Settled-value select (browser)

```javascript
/**
 * Saves a select's value once it has settled: pointer and touch commits save at once, keyboard stepping waits for a pause,
 * and leaving the control or pressing Enter or Tab flushes whatever is pending.
 * @param {HTMLSelectElement} select The select.
 * @param {(value: string) => Promise<void>} save Posts the value.
 * @param {number} settleMs How long keyboard stepping must pause before saving.
 */
export function saveSettledValue(select, save, settleMs = 600) {
  const KEY_WINDOW_MS = 100;
  let lastKeyAt = Number.NEGATIVE_INFINITY;
  let timer = null;
  let pending = false;

  const flush = () => {
    window.clearTimeout(timer);
    timer = null;

    if (pending) {
      pending = false;
      save(select.value);
    }
  };

  select.addEventListener('keydown', (event) => {
    lastKeyAt = event.timeStamp;

    if (event.key === 'Enter' || event.key === 'Tab') {
      flush();
    }
  });
  select.addEventListener('change', (event) => {
    pending = true;
    window.clearTimeout(timer);

    if (event.timeStamp - lastKeyAt <= KEY_WINDOW_MS) {
      timer = window.setTimeout(flush, settleMs);
    } else {
      flush();
    }
  });
  select.addEventListener('focusout', flush);
}
```

### Expiry on the injected clock (test shape)

```csharp
[Fact]
public async Task A_game_that_left_the_collection_is_forgotten_after_thirty_days()
{
    var time = new FakeTimeProvider(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));
    var service = OwnerDataTestKit.CreateService(time, ownedGameIds: [1001]);
    await service.AssignAsync(gameId: 1001, locationName: "Cabinet A");

    await service.ReconcileAsync(ownedGameIds: [], time.GetUtcNow());
    time.Advance(TimeSpan.FromDays(29));
    await service.ReconcileAsync(ownedGameIds: [], time.GetUtcNow());
    service.Current.Games.Should().ContainSingle();

    time.Advance(TimeSpan.FromDays(1));
    await service.ReconcileAsync(ownedGameIds: [], time.GetUtcNow());
    service.Current.Games.Should().BeEmpty();
}
```

### Browser test shape for the keyboard rule (Playwright .NET)

```csharp
await picker.FocusAsync();
await Page.Keyboard.PressAsync("ArrowDown");
await Page.Keyboard.PressAsync("ArrowDown");
await Page.Keyboard.PressAsync("ArrowDown");
await Task.Delay(900, TestContext.Current.CancellationToken);
saves.Should().Be(1);
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| `ForwardedHeadersOptions.KnownNetworks` with `Microsoft.AspNetCore.HttpOverrides.IPNetwork` | `KnownIPNetworks` with `System.Net.IPNetwork` | .NET 10 (the old members are obsolete) | `TreatWarningsAsErrors` turns use of the old members into a build break; the ambiguity error appears when both namespaces are imported |
| Per-environment hand-written CIDR parsing | `System.Net.IPNetwork` (`Parse`, `TryParse`, `Contains`) | .NET 8 | Use it, with the startup validation above |
| Antiforgery middleware for every app | Razor Pages and MVC filters validate on their own; `UseAntiforgery` is for minimal endpoints that bind forms | .NET 8 | No middleware needed for the owner pages |
| Test hosts without real sockets | `WebApplicationFactory` with Kestrel (already built here) | already in the repo | The owner port can be tested on a real listener |

**Deprecated or outdated in this repository's own notes:** `.claude/CLAUDE.md` still names `SixLabors.ImageSharp` as the image library; the code uses SkiaSharp 4.153.1 (no change needed for this phase).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The "sync never writes the owner-data file" rule means the sync never overwrites owner data from its own copy; the 30-day clock and dead-link marks are written by the single owner-data writer on the sync's behalf (publisher-triggered reconcile, background link worker) | Pattern 2, Pattern 4 | If the owner meant no sync-triggered owner-file writes at all, the clock needs another home (for example the snapshot) and the dead-link mark needs the owner file read-only from the sync path |
| A2 | An expansion with several owned bases follows the base the layout pairing picks (lowest collection id), not the lowest BGG id the UI contract states | Pattern 3 | Visible only for an expansion of several owned bases; a mismatch between where the expansion stands and which location it takes |
| A3 | Holding staged previews and the pending restore check in memory (instead of as short-lived files) satisfies the contract's intent | Pattern 4 | The contract's wording says files; if files are required, add a fixed staging directory, server-generated names, a pattern check and a start-up wipe |
| A4 | The owner endpoint is optional and absent from committed `appsettings.json`; production enables it through the env file | Pattern 1 | If the owner prefers a committed endpoint, a release without the ranges in place fails its health check and rolls back |
| A5 | Earlier versions: keep 10, rotate by time (at most one per hour on the injected clock) plus forced copies before a restore or schema change | Pattern 2 | A different count or trigger changes how far back a wrong edit can be recovered |
| A6 | "Link no longer works" after two failed weekly-or-daily checks at least a day apart, not after one failure | Pattern 4 | One blip could otherwise put rows back into "worth a look" |
| A7 | Owner modules stay under `wwwroot/js` and `wwwroot/css` and are reachable by URL on the public port; hiding them is optional hardening | Pattern 6, Pitfall 11 | If hiding is required, the lint pattern and a convention on `MapStaticAssets` need changes and tests |
| A8 | The "Copy image address" variant of a BGG gallery picture (`__imagepage`, `__original`, ...) is not verified; the host check plus decodability is what matters | Pattern 4 | If a gallery yields a non-image page on the image host, the "not a readable picture" message shows instead of a clearer one |
| A9 | Firefox on desktop steps a closed select with change events like Chromium does (not re-tested); macOS opens the popup | Pattern 6 | The settled-value rule is safe either way (immediate flush for a single commit) |
| A10 | `autofocus` on the safe dialog button receives initial focus under `showModal()` in the target browsers | Pattern 6 | Needs the browser test; otherwise move focus explicitly after `showModal()` |
| A11 | Picture choice need not be hashed explicitly into the collection version, because the drawn output and the card text already change it | Pattern 3 | If the owner wants the explicit field, add it; the only cost is one cache refresh |
| A12 | `System.Text.Json` as configured by `StoredJson` ignores unknown members, so an additive optional snapshot property does not break an older release | Folded fixes | If wrong, a rollback after the details-attempts field would set the snapshot aside and force a full refetch |

## Open Questions

1. **Which base does an expansion follow when it expands several owned bases?**
   - What we know: `ExpansionPairing` (the layout) chooses the base whose earliest entry has the lowest collection id; `CardRecords.BasesOf` orders by BGG id; the UI contract and D-18 say "lowest BGG id, the same order the card uses".
   - What is unclear: which the owner wants when the two differ.
   - Recommendation: follow the pairing the layout uses, so the expansion's location equals the location of the base it stands next to and the later per-location cabinets keep them together; update the contract sentence.
2. **Who writes the 30-day clock and dead-link marks?** (A1)
   - Recommendation: the single owner-data writer, triggered by the publisher after accepted commits and by the link worker; no change-log entry, not counted by the banner. Confirm the wording of the "sync never writes" rule.
3. **Enable the owner endpoint by default or by env only?** (A4)
   - Recommendation: env only, so a release can deploy before the owner's DNS and Traefik work is done; document the three prerequisites in the release notes.
4. **Error copy that needs data the downloader does not return.**
   - What we know: `ArtDownload` carries a category word only; the copy wants the HTTP status (404) and the size of a too-large picture.
   - Recommendation: extend `Missed` and `Turned` with optional status and announced size (additive), or reduce the two messages to what is known.
5. **Preview staging in memory or on disk?** (A3) Recommendation: memory.
6. **Time zone of server-rendered times.** Recommendation: UTC text in `<time datetime>` as the no-script fallback, localised by script; or a configured display zone if the owner prefers.
7. **Explicit picture choice in the version?** (A11) Recommendation: derive; pin with a test.
8. **Should the owner data export include the change log?** The contract's restore preview lists locations, placed games and picture choices only. Recommendation: include it, so a restore keeps the history and the banner state.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build and all C# tests | yes | 10.0.112 | none needed |
| Node.js | `node --test build/tests/*.test.mjs` | yes | v24.19.0 | none needed |
| Chromium headless shell (Playwright cache) | browser tests | yes | build 1243 (browser reports 153.0.8010.12) | CI installs it in its own job |
| Docker | `build/lint.sh` (shellcheck, zizmor, gitleaks, actionlint containers) | yes | present at /usr/bin/docker | run the lint in CI |
| Fake BGG (`Cabinet.FakeBgg`) and synthetic samples | local iteration and tests | yes (in repo) | n/a | none needed |
| Traefik, nftables, the Proxmox host, DNS | real deployment and the scripted route check | not part of this machine | n/a | Tests use rendered templates and stubbed `curl` and `ss`; the real router, DNS name and firewall reload are owner actions |

**Missing dependencies with no fallback:** none for development.
**Missing dependencies with fallback:** the real Traefik router and the owner DNS name cannot be exercised from this machine; the deployed-release check (D-28) runs after the release, from the owner's network.

Baseline measured this session: `dotnet test --project Cabinet.UnitTests` passed 1918 of 1918 in about 75 seconds; `node --test build/tests/*.test.mjs` passed 250 of 250 in under a second. [VERIFIED: runs this session]

## Validation Architecture

### Test Framework

| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.2 with FluentAssertions 8.11.0 (unit and integration), Microsoft.Playwright.Xunit.v3 (browser), Node built-in test runner (page scripts), bash scripts with stubs (deploy and release scripts) |
| Config file | `global.json` (`test.runner: Microsoft.Testing.Platform`), per-project csproj; no separate xunit config |
| Quick run command | `./Cabinet.UnitTests/bin/Debug/net10.0/Cabinet.UnitTests -class "*OwnerData*"` after a build (simple filters `-class`, `-method`, `-namespace` verified to work; 45 tests ran in 0.4 s), plus `node --test build/tests/*.test.mjs` |
| Full suite command | `dotnet test --project Cabinet.UnitTests`, `dotnet test --project Cabinet.IntegrationTests`, `node --test build/tests/*.test.mjs`, `dotnet test --project Cabinet.BrowserTests`, `build/lint.sh`, and the script tests the CI runs (`bash deploy/tests/cabinet-deploy-e2e-test.sh`, `bash build/tests/package-release-e2e-test.sh`) |

### Phase Requirements to Test Map

| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| OWN-01 | every endpoint whose route starts with `/owner` carries the owner-only marker (enumerate `EndpointDataSource`) | integration | `dotnet test --project Cabinet.IntegrationTests` | Wave 0 |
| OWN-01 | owner routes answer 404 on the public port, including path tricks (`/Owner/`, `//owner/pictures`, `/owner/../owner/pictures`, `/%6Fwner/pictures`, `/owner%2Fpictures`) and a spoofed `Host` header | integration (real sockets) | same | Wave 0 |
| OWN-01 | owner port: 200 inside a range via forwarded header from the trusted loopback peer; 403 outside; 403 for a missing remote address | integration | same | Wave 0 |
| OWN-01 | forged `X-Forwarded-For` from an untrusted peer is ignored (remote address stays the peer) | integration on a small test host with `RemoteIpAddress` and `LocalPort` set per request | same | Wave 0 |
| OWN-01 | startup refuses: owner endpoint without ranges, without a known proxy, a `/0` range, a malformed range, the owner port equal to the public or ops port; accepts a bare address and trimmed entries | unit on `OwnerAccess.FromConfiguration` | `dotnet test --project Cabinet.UnitTests` | Wave 0 |
| OWN-01 | public pages work on both ports; the public HTML, shared scripts, layout and card data contain no `/owner`; the owner stylesheet is linked only at the owner address | integration | `dotnet test --project Cabinet.IntegrationTests` | Wave 0 |
| OWN-01 | deploy: rendered nftables has three `tcp dport` rules and still none for 5081; Traefik example has the second router; env example has placeholders only; selfcheck stubs cover the owner port | bash tests | `bash deploy/tests/render-templates-test.sh`, `bash deploy/tests/sandboxing-test.sh` | exist, need edits |
| OWN-01 | scripted route check script passes and fails correctly against stubbed `curl` | bash test | `bash build/tests/check-owner-routes-test.sh` | Wave 0 |
| OWN-02 | store: round trip, atomic write, newer schema refused without renaming, damaged file reported `Unreadable` with the file untouched, earlier versions rotate (time based, forced before restore), count capped | unit | `dotnet test --project Cabinet.UnitTests` | Wave 0 |
| OWN-02 | owner data survives a sync round and a restart (two hosts on one storage directory), and a release (file untouched) | integration | `dotnet test --project Cabinet.IntegrationTests` | Wave 0 |
| OWN-02 | 30-day expiry on `FakeTimeProvider`: clock starts at the first accepted snapshot without the game, resets when the game returns, ignores held-back results and the unreadable state | unit | same | Wave 0 |
| OWN-02 | export then restore round trip; restore matrix: oversized, deep, wrong `kind`, newer version, unknown location reference, duplicate ids, name rules, bad link host, over limits; nothing changes on any failure; current data kept as an earlier version | unit | same | Wave 0 |
| OWN-02 | concurrency: owner save interleaved with a sync commit keeps both; stale revision gets 409; undo valid only for the latest change; undo removes the log entry and lowers the banner count | unit and integration | same | Wave 0 |
| OWN-02 | coalescing: a burst of saves produces one notification after the quiet period on `FakeTimeProvider` | unit | same | Wave 0 |
| LOC-03 | add, rename (id stable), reorder, delete with games becomes unplaced, limits (50, 60, duplicate, trimmed, control and bidi characters), delete-undo | unit | same | Wave 0 |
| LOC-04 | assign, bulk move, effective location for expansions (follows base; own; orphan; several bases), card record and collection version carry the location, `unplacedCount` | unit and integration | same | Wave 0 |
| LOC-03, LOC-04 | page flows: closed disclosure with a location and open without; two-line item layout; delete dialog opens on the safe button; reorder keeps focus; dock appears, last row clears it at 390 px, move dialog opens on the first option, Escape returns focus; row picker makes one save and one log entry when arrowed | browser | `dotnet test --project Cabinet.BrowserTests` | Wave 0 |
| IMG-02 | link policy matrix (blank, not a URL, BGG page link, other host, http, port, user info, trailing-dot host) with the right message each; scripted fetch outcomes (404, timeout, too large, wrong type, undecodable, too many pixels, extreme shape); no Authorization header on the fetch | unit and integration (`ScriptedImageHandler`) | same | Wave 0 |
| IMG-02 | preview staged in memory, served by token only, expires on `FakeTimeProvider`, cap enforced, unknown token 404, owner port only | integration | same | Wave 0 |
| IMG-02 | apply: card cover URL and spine colour change; box shape follows BGG size, then a flat cover, then the estimate; a 3D shot never shapes a box; version and ETag change; "Remove link" and undo (refetch) | unit and integration | same | Wave 0 |
| IMG-02 | files of a link are never pruned (older than the grace, and while owner data is unreadable); dead link keeps the copy and marks the row; analysis-version bump reprocesses link records; "changed on BGG" and "reviewed" rules incl. the worklist rule | unit | same | Wave 0 |
| IMG-02 | page flows: radio tiles save at once with Undo, Looks right counts down, preview shows box and spine, every refusal message, Your link tile stays selectable, single-game view shows the location picker | browser | same | Wave 0 |
| Cross | no CSP violation on any owner page; no request to another host; every control at least 44 px; reduced motion removes the dock slide; owner pages are English with no toggle; the plate on every owner-address page; Edit link and corner tag at the owner address only and tags never overlap a label at 390 and 1440 px | browser | same | Wave 0 |
| Cross | `owner-format.js` pure logic; the two new bilingual strings in both tables and the parity test | node | `node --test build/tests/*.test.mjs` | Wave 0 |
| Cross | contrast checks listed in the UI contract (including the danger colour and the corner tag tones) | unit (like `CardContrastTests`) | `dotnet test --project Cabinet.UnitTests` | Wave 0 |
| Folded | details-attempt backoff; `FromCover` plausibility; `Explain` order independence (shuffled input gives the same result); review sheet counts add up, no clipping, read-only load leaves the file untouched, `--bold-font` alone, path errors, surrogate pairs | unit | same | Wave 0 (failing first) |
| Folded | finish slot: `finish-classic` on both ports, unknown stored value falls back | integration | same | Wave 0 |

### Sampling Rate

- **Per task commit:** the touched test class through the built test executable (`-class` filter) and `node --test build/tests/*.test.mjs`.
- **Per wave merge:** both .NET test projects in full (about 75 seconds for the unit project today), the node tests, and `build/lint.sh`.
- **Phase gate:** unit, integration, browser, node, lint and script tests green before `/gsd-verify-work`; then the deployed-release check through both routers after the release.

### Wave 0 Gaps

- [ ] `CabinetWebApplicationFactory`: third port, owner endpoint and ranges in the in-memory configuration, `CreateOwnerClient()`; update `TestHostPortTests` for the new tuple.
- [ ] `CabinetPageTest`: `OwnerPort` and an owner page base class (CSP console check, host recorder).
- [ ] A small untrusted-peer test host helper for the forged-header test.
- [ ] Owner-data test kit: in-memory store, fake clock service factory, synthetic collections with invented locations (no real names).
- [ ] Fake BGG and sample cases listed in the UI contract's review matrix (owned and main pictures that differ, a 3D verdict, a game with no picture, a dead link, a changed-on-BGG game, expansions with one, several and no owned base), all synthetic.
- [ ] Failing tests for the folded fixes written before their changes.

## Security Domain

`security_enforcement` is enabled in `.planning/config.json` (ASVS level 2, block on high). [VERIFIED: config.json read this session]

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture, trust boundaries | yes | Two independent walls on the owner port (listener plus client range) plus Traefik allow-list and nftables; owner data never reaches a public endpoint |
| V2 Authentication | no (owner decision: network location is the only lock; guests on the same subnet can edit) | Recorded as an accepted risk; every change is logged and exportable |
| V3 Session management | no sessions; the only cookies are the antiforgery cookie on owner pages and the existing language cookie | Cookie flags: HttpOnly, SameSite Strict, Secure in Production |
| V4 Access control | yes | `LocalPort` plus `OwnerOnly` metadata, client-range middleware, fail-closed startup validation, an endpoint-coverage test |
| V5 Validation and encoding | yes | Strict stored-JSON parsing, explicit limits, link policy, `textContent` and Razor encoding for BGG titles and location names, redirect targets fixed |
| V6 Cryptography | yes (tokens only) | `RandomNumberGenerator.GetHexString`; Data Protection for antiforgery; nothing hand-rolled |
| V7 Error handling and logging | yes | Categories and counts only, no client addresses, no names, no links; bodyless 400/403/404 |
| V8 Data protection | yes | Location names are public by design (stated on the page); owner file is in the state directory with the service's existing permissions; export is a plain file the owner holds |
| V10 Malicious code | n/a | no new dependencies |
| V12 Files and resources | yes | Restore upload: size cap, depth cap, strict shape; previews held in memory; no path built from request values |
| V13 API and web service | yes | Antiforgery on every post, JSON and form; revision check; small body limits |
| V14 Configuration | yes | Startup validation of owner endpoint, ranges, known proxy; CSP unchanged and strict; `AllowedHosts` is `*` today, so no Host filtering applies |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Owner pages reached through the public route | Elevation of privilege | Port-based 404 (not Host-based), metadata guard, prefix guard, path-trick tests, Traefik router rule that does not route owner paths on the public host, scripted route check |
| Forged `X-Forwarded-For` | Spoofing | Believed only from `KnownProxies`; `ForwardLimit = 1` reads only the entry Traefik appended; probe in selfcheck |
| Proxy address mistaken for the client (wrong `KnownProxies`) | Spoofing, elevation | Startup refuses an owner endpoint without a known proxy; probe in selfcheck |
| Cross-site request from a page open in the owner's browser | Tampering | Antiforgery token plus `SameSite=Strict` cookie; optionally reject `Sec-Fetch-Site: cross-site` on owner posts as defence in depth |
| DNS rebinding to the owner hostname | Spoofing | Traefik's Host-based router does not match a foreign hostname; optional owner host check as hardening |
| SSRF through a pasted link | Information disclosure, tampering | `ArtSourcePolicy` (https, default port, no user info, exact host), three redirect hops each re-checked, no cookies, no credentials, byte, pixel and time caps, one check at a time |
| Link used to leak the BGG token | Information disclosure | The image client has no auth handler; test asserts no Authorization header |
| Path traversal through preview tokens, version names or art file names | Tampering | Token regex and dictionary lookup, enumerated version list, `ArtCache` name pattern |
| Oversized or hostile restore file (huge, deeply nested, wrong shape) | Denial of service, tampering | 2 MB cap before parsing, `MaxDepth = 16`, strict types, count limits, name rules, link policy; nothing applied on any failure; current data kept as an earlier version |
| Script injection through location names or titles | Tampering, XSS | `textContent` and Razor encoding only; control and bidi characters rejected; CSP blocks inline script |
| Information disclosure of owner data on the public port | Information disclosure | Change log, versions and addresses only in owner pages; test that public payloads contain none |
| Log leakage | Information disclosure | No client addresses, names or links in logs |
| Link checks used to hammer the image host | Denial of service | One check at a time, shared 1-second pacer, per-game staged entries capped |
| Damaged owner file overwritten silently | Tampering, data loss | Never renamed or overwritten until a restore; earlier versions kept; prune skipped while unreadable |

**Accepted by the owner (record in the threat model):** no password; anyone on the allowed subnets can edit; mitigated by the change log, export and earlier versions.

## Sources

### Primary (HIGH confidence)
- Repository files read this session: `.claude/CLAUDE.md`, `Cabinet.Service/Program.cs`, `Hosting/OpsEndpoint.cs`, `Hosting/ContentSecurityPolicy.cs`, `Collection/CollectionStore.cs`, `Collection/StorageLocation.cs`, `Sync/SyncRunner.cs`, `Sync/SyncStartup.cs`, `Sync/ArtSync.cs`, `Sync/SyncEndpoints.cs`, `Sync/CabinetStatus.cs`, `Live/*`, `Pages/*`, `Cabinet.Domain/Collection/*` and `Cards/CardRecords.cs`, `Cabinet.Repository/Images/*` and `Storage/*`, `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs`, `TestHostPortTests.cs`, `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs`, `deploy/*`, `build/lint/checks/10-repo-rules.sh`, `docs/lxc-setup.md`, the phase context, UI contract, front-end review and probe-gap files, `03-SPIKE-OUTCOME.md`.
- Scratch builds and runs on SDK 10.0.112 and Chromium 153 (this session): `System.Net.IPNetwork` behaviour, `ForwardedHeadersOptions` defaults, two-port routing with endpoint metadata, antiforgery behaviour and defaults, `RandomNumberGenerator.GetHexString`, `JsonDocumentOptions.MaxDepth`, `TimeProvider.CreateTimer`, select arrow-key events.
- https://learn.microsoft.com/en-us/aspnet/core/fundamentals/routing?view=aspnetcore-10.0 (host matching and the spoofing warning).
- https://raw.githubusercontent.com/dotnet/aspnetcore/main/src/Middleware/HealthChecks/src/Builder/HealthCheckApplicationBuilderExtensions.cs (port predicate).
- https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0 (Razor Pages validation, header name, minimal API behaviour, Data Protection).

### Secondary (MEDIUM confidence)
- https://learn.microsoft.com/dotnet/api/microsoft.aspnetcore.builder.forwardedheadersoptions and the .NET 10 compatibility list (obsolete `KnownNetworks`), via web search.
- https://github.com/MatthewThompson/arnak (`test_data/game/game_expansion.xml`, picture address shape).

### Tertiary (LOW confidence, marked for validation)
- WordPress Trac ticket 40925 and jQuery ticket 14186 (select change behaviour across browsers, 2011 to 2017); the Chromium result above is first-hand, the Firefox and macOS statements are not.
- The gallery "Copy image address" variant (assumption A8).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, no new packages; every API used was exercised on the pinned SDK or read in the repository.
- Architecture: HIGH for the gate, store layering, publisher and prune interplay (read from the code that must change); MEDIUM for the link worker's cadence and the in-memory staging, which are design choices flagged above.
- Pitfalls: HIGH for those verified by scratch runs or code reading; MEDIUM for browser focus behaviour that needs the planned browser tests.

**Research date:** 2026-10-12
**Valid until:** 2026-11-11 for the framework facts (stable); the repository facts hold until the files named above change.

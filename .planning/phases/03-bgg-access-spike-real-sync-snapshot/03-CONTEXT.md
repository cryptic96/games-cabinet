# Phase 3: BGG Access Spike, Real Sync & Snapshot - Context

**Gathered:** 2026-10-06
**Status:** Ready for planning

<domain>
## Phase Boundary

This phase replaces the invented sample collections on the deployed site with the owner's real BGG owned collection, kept fresh by an hourly background sync and a cooldown-guarded "sync now", and keeps the cabinet working when BGG does not. It covers:

- the location spike with the real token from the container, with its outcome recorded and signed off by the owner
- the BGG client, the single-flight sync job, the snapshot and sync state on disk, and the guard against bad results
- the "sync now" control, the "last synced" line, the stale note and live updates of open pages
- the intentional "cabinet is being filled" state before the first sync
- the linked "Powered by BGG" credit on every public page
- the token and username kept in server-side configuration only

Requirements: SYNC-01, SYNC-02, SYNC-03, SYNC-04, SYNC-05, SYNC-08, LOC-02, SEC-05.

Not in this phase:

- box art, art-coloured spines, real box proportions beyond what the collection call already returns, and the `thing` enrichment (Phase 4)
- pairing expansions with their base games (Phase 4); until then expansions show on their own (D-15)
- showing storage locations, the detail card, language labels (Phases 5 and 7)
- owner tools, owner-data storage and backups (Phase 6)
- public exposure, rate limits and resource caps (Phase 8)

</domain>

<decisions>
## Implementation Decisions

### Storage
- **D-01:** **Local files now; the database question is reopened in Phase 6.** The synced collection (snapshot) and the sync bookkeeping (last attempt, last success, last error category, cooldown end, any held-back result) live as JSON files under the systemd state directory, written atomically, behind a small storage interface. The owner raised using their existing shared database server for all data. It was not chosen for this phase because: the snapshot is a rebuildable copy of BGG; the site must keep serving the last good collection when anything else is down, so a local copy is needed even with a database; and the shared database server would add a SQL login on the container that will face the internet, a firewall path from it to the shared database host, migrations in every release with a migration-aware rollback (Phase 1 deliberately removed that branch, so rollback is always allowed), and a database in CI. The owner remains open to the shared database server (or SQLite, or a backed-up file) for the owner data in Phase 6, where backups are the real need. — **Reversibility:** reversible — the snapshot is rebuildable from BGG and sits behind the storage interface, so moving it into a database later touches one implementation and no stored layout state.

### Location spike
- **D-02:** **Owner prerequisite: set an inventory location on two or three games on BGG before the spike runs.** The owner has no locations filled in yet; without some, "no location came back" proves nothing.
- **D-03:** **The spike is a script that prints only the response's shape.** It runs on the container as the app's user and reads the token from the server env file itself. It prints only status codes, response headers of interest (never request or auth headers), which elements and attributes are present, and counts. It never prints the token, the username, game titles, location names or raw XML. Claude runs it over SSH after the owner approves that specific run; the owner reads and signs off the summary. The recorded outcome under `.planning/` contains shape only, because the repository is public. The spike is the first work of the phase and gates the sync plans. It also answers the roadmap's research focus: `own=1` and subtype behaviour, duplicate collection entries, 202/429/403 behaviour, honest User-Agent requirements, and whether version dimensions come back in the collection response (D-14).
- **D-04:** **Outcome handling.** If the private inventory location is readable with the token, every sync asks for it and the snapshot carries each game's location (roadmap success criterion 5); nothing shows it yet (Phases 5 and 7). If it is not readable, the outcome is recorded and Phase 6 builds the location tools (LOC-03, LOC-04). The owner's password or session cookie is never stored (locked at project level).

### Sync now and freshness
- **D-05:** **Cooldown: 10 minutes**, one global window shared by all visitors, persisted in the sync state so restarts and releases do not reset it. A failed manual sync still uses up the window, so a struggling BGG is not retried on demand.
- **D-06:** **The controls sit on one compact status line under the title:** "Synced 12 minutes ago · Sync now", scrolling away with the header. Not the footer: the owner noted that on a long cabinet (65 games on a phone, or 400) the footer is a long scroll away. This is a temporary home; a separate settings page is captured for Phase 6 (see Deferred Ideas).
- **D-07:** **After pressing:** the button shows "Syncing…", the page redraws the cabinet by itself when the sync finishes, then the button shows the cooldown countdown. A short note says BGG can take a few minutes to show recent edits. On failure the line says BGG didn't respond, the last collection stays, and the countdown still runs (D-05). While a sync is already running, pressing reports that instead of queuing another one.
- **D-08:** **"Last synced" is relative** ("Synced 12 minutes ago") with the exact date and time on hover or tap, in the visitor's local time (rendered client-side from a UTC timestamp). Labels are English only until Phase 5.
- **D-09:** **Open pages are live-updated through SignalR push** (owner's choice over the recommended light status check; Server-Sent Events and no live update were also offered). Constraints for research and planning:
  - The hub only broadcasts from server to clients: sync started, finished or failed, collection changed (with the snapshot version), cooldown state. Clients cannot invoke any hub method that triggers work. "Sync now" stays a POST endpoint behind the global cooldown, so SEC-05 holds: no visitor input reaches BGG beyond the guarded sync.
  - On connect and on every reconnect, the page fetches the current status once, so missed events (sleeping phone, dropped connection) are caught up.
  - No Node toolchain: the SignalR JavaScript client is added to `wwwroot` as a pinned, checksummed copy of Microsoft's official package. Record its version and checksum, document how to update it (Dependabot cannot track a copied file), and keep the JS comment lint passing (exclude the copied file explicitly or otherwise handle it). Fetching the file is a download that needs the owner's approval at execution time.
  - The strict Content-Security-Policy (`default-src 'self'`) must allow the same-origin WebSocket in every target browser; verify, and add an explicit `connect-src` if needed. The cabinet must still work with no CSP violations.
  - The host is low-power and will face the internet: bound the number of concurrent connections and choose transports deliberately. Phase 8 sizes the final limits from real load.
  — **Reversibility:** reversible — a hub, a copied client script and a page module; falling back to polling the same status endpoint touches only the page module and one service registration.
- **D-10:** **An open page that learns the collection changed redraws quietly in place** and updates "Synced … ago". No box can be open yet in this phase, so nothing is interrupted (revisit when the detail card arrives in Phase 5).

### Bad-result guard
- **D-11:** **An empty result when the cabinet had games is always held back.** It is never accepted automatically, so the cabinet is never wiped (SYNC-04).
- **D-12:** **A result that shrinks by more than half is held back, then accepted when the next sync returns the same smaller collection** (the hourly run, or a manual one after the cooldown). Smaller removals, such as selling a few games, go through immediately. No owner switch is needed.
- **D-13:** **The "showing last sync from …" note appears once the last good sync is about 3 hours old, or while a suspicious result is held back.** A single failed hourly run stays quiet; the always-visible "Synced … ago" line already tells the time.

### Real games before box data arrives
- **D-14:** **Interim box sizes:** use the owned version's width, length and depth when the collection call already returns them (the call asks for the owner's selected version anyway, so this costs no extra BGG request) and they are plausible; otherwise one realistic default size. The spike checks how often they are present and in what unit. Phase 4 completes the chain (flat-cover aspect ratio, verified units, realistic defaults). The cabinet rearranges when real sizes land; accepted, because the layout is a pure function of the collection.
- **D-15:** **Expansions show as flat expansion boxes labelled "Expansion"** (the existing orphan-expansion look, without a base-game title), placed in the first cubby with room. Phase 4 pairs them with their owned base games, and they move beside them then. The engine and renderer must handle an expansion with no known base game (sub-line "Expansion" instead of "Expansion for {base title}").
- **D-16:** **Before the first successful sync, visitors see the approved empty minimum cabinet (bare planked wood) with a short message above it**, along the lines of "The cabinet is being filled from BoardGameGeek — check back in a few minutes." This is a normal 200 page, never an error.
- **D-17:** **The deployed site shows only the real collection.** The sample collections and their switcher stay available in local development only (and the fake BGG from D-20 can serve sample-sized collections for future layout reviews). The switcher never appears on the deployed site. This largely closes the prototype item of the go-public todo; adding noindex stays in Phase 8.

### BGG credit and duplicates
- **D-18:** **The BGG credit sits in the footer only:** the "Powered by BGG" logo, linked to BoardGameGeek, at a legible size on every public page, including the empty and error states and phone widths. The header status line does not name BGG. The logo comes from BGG's official usage page, not a third-party copy. The owner downloads it, or approves Claude downloading it at execution time.
- **D-19:** **Owning two copies or versions of the same game shows one box per copy**, like a real shelf. Each owned collection entry is its own item; expansions sit beside the first copy (lowest collection id), which the engine already does. Anything keyed per box on the page (DOM data, future detail and accessibility hooks) uses the collection entry, not only the BGG id. The shrink guard (D-12) counts collection entries.

### Carried forward (locked earlier; not re-discussed)
- Own small typed BGG client with LINQ to XML and hardened XML reading. Base URI `https://boardgamegeek.com/xmlapi2/` without `www`; redirects carrying the token are never followed. At least 5 seconds between BGG calls, enforced in one place. An explicit, bounded loop for 202 "queued" answers. 401 is a configuration error: stop, record it, no hot retries. 403 and 5xx mean BGG is unavailable. A non-200 is never treated as "no games".
- Two collection calls per sync: base games with `excludesubtype=boardgameexpansion`, and expansions with `subtype=boardgameexpansion`. Owned items only (also check each item's own status). A full collection fetch every run; `modifiedsince` is not used.
- `BackgroundService` with `PeriodicTimer` about hourly plus start-up jitter. One bounded channel with a single consumer makes the sync single-flight. The timer, the start-up run and "sync now" all enqueue the same job.
- The token and username live only in the server env file (`dotnet user-secrets` locally), never in the repo, logs, fixtures or chat. The env example carries placeholders only. The token is sent only to the BGG API host. Visitors cannot supply a username.
- `/health` stays loopback-only and never depends on BGG: an empty or missing snapshot is healthy. Sync status may be reported there as information only.
- Fixtures and test data are synthetic: hand-written XML in BGG's response shape only, never captured real responses. The owner's BGG username equals the public GitHub handle, so the personal-data denylist cannot catch it; keep it out of code, fixtures and examples.
- No ads, donations or affiliate links. The site's JSON stays same-origin, undocumented, with no CORS headers and no bulk raw dump.
- BGG text is untrusted: render with `textContent`, never as markup.

### Claude's Discretion
- Sync timing details: jitter size, whether to sync on start-up (only when the snapshot is older than the interval), 202 backoff and total cap, per-sync request budget, an interval floor in code.
- Shape and file names of the snapshot and sync state, the schema version and what an older release does with a newer file (treat it as no snapshot and resync, never crash).
- What "the same smaller collection" compares (for example the set of collection entry ids).
- Plausibility bounds for version dimensions and the interim default size; unit conversion once the spike shows the unit.
- Where the spike script lives (committed tool or throwaway), as long as it holds no secrets and prints shape only.
- The User-Agent text (honest: project name and public repo URL, never a browser string).
- SignalR transports, connection caps (initial values), and how the status line and the redraw use the broadcasts.
- How the prototype switch becomes off on the deployed site and on in local development.
- Exact copy for the status line, the stale note, the empty state and the failure message.

### Folded Todos
- **Add dev-only fake BGG host for the sync phase** (`.planning/todos/pending/2026-10-05-add-dev-only-fake-bgg-host-for-the-sync-phase.md`). Problem: the sync loop could only be exercised end to end against the real API. Folded in full (D-20):
  - **D-20:** A scripted `HttpMessageHandler` stub for automated tests (202 then 200, 429 without `Retry-After`, 5xx, slow responses, an HTML/Cloudflare-style page with status 200, malformed XML, empty and shrunken collections), plus a **local-only fake BGG** serving synthetic collection XML (base games, expansions via subtype) with switches for queued, throttled, slow, broken, empty and shrunken answers. It is used to run sync, cooldown, live updates and every failure state locally and to screenshot them without touching real BGG. Constraints from the todo apply: built from public docs with invented data only; never shipped in the release artifact; production configuration cannot point at it (the base-URI override is honoured in Development only); it never receives a real token. It cannot answer what needs the real token (private info, real limits, auth behaviour); the spike (D-03) covers those.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` §"Phase 3: BGG Access Spike, Real Sync & Snapshot": goal, success criteria, owner prerequisites, research focus for the live spike
- `.planning/REQUIREMENTS.md`: SYNC-01..05, SYNC-08, LOC-02, SEC-05 in scope; SYNC-06/07, IMG-01/03, CAB-03 (Phase 4) and LOC-03/04, OWN-01/02 (Phase 6) shape what this phase must leave room for; Out of Scope table (no BGG username input, no hotlinking, no stored BGG password or cookie)
- `.planning/PROJECT.md` §Context and §Key Decisions: BGG data facts (about 65 owned items, owned versions selected, Dutch 3D version images), plan B outcome for locations, no-database decision

### BGG sync design (research)
- `.planning/research/ARCHITECTURE.md` §1 "BGG Sync Pipeline" (access rules, two collection calls, 202 handling, failure behaviour, triggering and single-flight), §2 "Storage Location Source" (spike recommendation; the public-comment convention there is rejected by the owner), §3 "Data Model and Snapshot" (on-disk layout, snapshot rules), Pattern 1 "Immutable snapshot swap", Pattern 4 "Pacing and classification in one place"
- `.planning/research/PITFALLS.md` Pitfall 1 (token, Cloudflare, User-Agent, host), Pitfall 2 (private location), Pitfall 3 (hammering BGG), Pitfall 4 (bad snapshot replacing a good one), Pitfall 5 (collection data model traps, duplicates), Pitfall 6 (terms, attribution), Pitfall 12 (BGG text as markup)
- `.planning/research/STACK.md` §1 "BGG integration", §6 "Background sync", §7 "Testing"; and `.claude/CLAUDE.md` §"BGG API Access Rules" (official rules summary)

### Prior decisions and folded work
- `.planning/phases/02-layout-engine-cabinet-prototype/02-CONTEXT.md`: layout as a pure function of the collection (D-01, D-11), first cubby with room (D-10), orphan expansions (D-17), generated covers as permanent fallback (D-08), empty cubbies as bare planked wood (D-04, D-20)
- `.planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-CONTEXT.md`: naming and paths (`/var/lib/cabinet` state directory, `/etc/cabinet` env), loopback `/health` that never depends on BGG, rollback always allowed, SkiaSharp (D-17)
- `.planning/todos/pending/2026-10-05-add-dev-only-fake-bgg-host-for-the-sync-phase.md`: folded in full (D-20)
- `.planning/todos/pending/2026-10-06-turn-off-prototype-mode-and-add-noindex-before-go-public.md`: prototype item largely closed by D-17; noindex stays in Phase 8

### Conventions
- `.claude/CLAUDE.md`: hard rules (no planning references outside `.planning/`, `///`-only comments, public repo with no personal data, synthetic fixtures only, branching, BGG API etiquette)
- `build/lint/checks/10-repo-rules.sh`: C# `//` ban and the JS comment rule (relevant to the copied SignalR client, D-09)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- `Cabinet.Domain/Layout/CabinetItem.cs`: the engine input (`BggId`, `CollectionId`, `Title`, `Kind`, `Box`, `ExpansionOf`) is already independent of where games come from; the sync maps the snapshot into it.
- `Cabinet.Domain/Layout/CabinetLayoutEngine.cs`: orders items by collection id then BGG id, rejects an exact duplicate entry, and attaches expansions to the first copy of a base game (`firstBaseById`), which already fits D-19. An expansion with an empty `ExpansionOf` needs the "Expansion" label path (D-15).
- `Cabinet.Service/Layout/LayoutCache.cs` and `LayoutEndpoint.cs`: cached, ETag-ed layout per sample and profile with allowlists. For real data the key becomes the snapshot version plus profile; the `sample` parameter only exists in local development (D-17).
- `Cabinet.Service/Prototype/SampleCatalog.cs`: the prototype switch (`Prototype:Enabled`, committed as true) and the sample allowlist; this phase turns it off on the deployed site.
- `Cabinet.Service/Pages/Index.cshtml`: header with title and sample switcher, the existing "The cabinet is being built" message, the footer with the version; the status line (D-06), the empty state (D-16) and the BGG credit (D-18) go here.
- `Cabinet.Service/wwwroot/js/cabinet.js`, `render.js`, `copy.js`: load and redraw cycle with superseded-load protection and the phone profile query; the redraw on sync (D-07, D-10) reuses `load()`. `render.js` sets `data-game-id` from the BGG id; per-copy keys follow D-19.
- `Cabinet.Repository/`: currently only the image smoke test; the BGG client, snapshot store and sync state belong here (outside-world code).
- `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs`: in-process host where the stub handler (D-20) replaces the BGG client's transport.

### Established Patterns
- Layered solution: Domain (pure) / Repository (outside world) / Service (host, endpoints, hosted services, `wwwroot`).
- Settings validated at startup and pinned by `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs` (must parse, no secret-shaped values). New sync settings (interval, cooldown) go into committed appsettings; the token and username go only into the env file.
- `Cabinet.Service/Hosting/ContentSecurityPolicy.cs`: the app's own strict policy (`default-src 'self'`, no inline script or style); live updates and the status line must work under it (D-09).
- Warnings-as-errors and lock files: a new package (SignalR server parts ship with ASP.NET Core; resilience package if used) means updating `packages.lock.json`.
- `///` XML docs in C#, `/** */` doc blocks only in JS.

### Integration Points
- `Cabinet.Service/Program.cs`: register the BGG client (typed `HttpClient`, separate from any image client), the sync hosted service, the storage, the sync-now endpoint, the SignalR hub and the forwarded-headers setup already present.
- `deploy/systemd/cabinet.service`: `StateDirectory=cabinet` gives the writable `/var/lib/cabinet`; `ProtectSystem=strict` stays. `deploy/cabinet.env.example`: add placeholders for the BGG token and username.
- Outbound network from the container to `boardgamegeek.com` over HTTPS must be allowed (check the provisioned firewall rules).
- `docs/`: development notes for user-secrets, the fake BGG and the spike script, written without planning references.

</code_context>

<specifics>
## Specific Ideas

- The owner found the footer the wrong place for anything interactive: on a long cabinet it takes a while to scroll there. Controls belong near the top for now.
- Long term, the owner wants the main page kept clean, with update controls on a separate page (see Deferred Ideas).
- The owner wants an open page to update by itself when the collection changes, not only for the person who pressed "sync now".
- Two copies of a game are two boxes, like a real shelf.

</specifics>

<deferred>
## Deferred Ideas

- **Public settings page (for the Phase 6 discussion).** "Sync now" and the sync status move off the main page to a separate page that everyone can open. Each control on it is either public or owner-only; owner-only controls appear and work only from the home network or VPN, enforced on the server (OWN-01 still holds: owner endpoints are unreachable from the public route). It is a natural home for the language switch (Phase 5) and the selectable-finishes todo. Raised when the owner asked whether "sync now" should move to owner tools; making it owner-only would change SYNC-02, so that stays a Phase 6 decision.
- **Owner-data store (Phase 6).** The owner is open to their existing shared database server, SQLite or a backed-up file for image overrides and locations; decide with the backup need in view (D-01).

### Reviewed Todos (not folded)
- `2026-10-06-box-look-polish-for-the-box-images-phase.md`: belongs to Phase 4 (box images).
- `2026-10-06-cabinet-accessibility-notes-for-the-detail-phase.md`: belongs to Phase 5 (detail and accessibility).
- `2026-10-06-phone-cabinet-density-for-the-filters-phase.md`: belongs to Phase 7 (filters and locations).
- `2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`: unscheduled; possibly hosted by the settings page idea above.
- `2026-10-06-turn-off-prototype-mode-and-add-noindex-before-go-public.md`: Phase 8; its prototype item is largely done by D-17, noindex remains.

</deferred>

---

*Phase: 03-bgg-access-spike-real-sync-snapshot*
*Context gathered: 2026-10-06*

# Phase 3: BGG Access Spike, Real Sync & Snapshot - Research

**Researched:** 2026-10-06
**Domain:** Server-side BGG XML API2 sync (typed HttpClient + LINQ to XML), single-flight background job, atomic JSON snapshot on disk, ASP.NET Core SignalR push to a vanilla-JS page, shared Razor layout with the BGG credit
**Confidence:** MEDIUM-HIGH. Framework, SignalR, file and test mechanics are HIGH (several verified by running code in this session). BGG response details that need the real token (private location, units, 202/429 behaviour, User-Agent tolerance) are MEDIUM/LOW by nature and are exactly what the spike measures.

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

**Storage**
- **D-01:** **Local files now; the database question is reopened in Phase 6.** The synced collection (snapshot) and the sync bookkeeping (last attempt, last success, last error category, cooldown end, any held-back result) live as JSON files under the systemd state directory, written atomically, behind a small storage interface. The owner raised using their existing shared database server for all data. It was not chosen for this phase because: the snapshot is a rebuildable copy of BGG; the site must keep serving the last good collection when anything else is down, so a local copy is needed even with a database; and the shared database server would add a SQL login on the container that will face the internet, a firewall path from it to the shared database host, migrations in every release with a migration-aware rollback (Phase 1 deliberately removed that branch, so rollback is always allowed), and a database in CI. The owner remains open to the shared database server (or SQLite, or a backed-up file) for the owner data in Phase 6, where backups are the real need. — **Reversibility:** reversible — the snapshot is rebuildable from BGG and sits behind the storage interface, so moving it into a database later touches one implementation and no stored layout state.

**Location spike**
- **D-02:** **Owner prerequisite: set an inventory location on two or three games on BGG before the spike runs.** The owner has no locations filled in yet; without some, "no location came back" proves nothing.
- **D-03:** **The spike is a script that prints only the response's shape.** It runs on the container as the app's user and reads the token from the server env file itself. It prints only status codes, response headers of interest (never request or auth headers), which elements and attributes are present, and counts. It never prints the token, the username, game titles, location names or raw XML. Claude runs it over SSH after the owner approves that specific run; the owner reads and signs off the summary. The recorded outcome under `.planning/` contains shape only, because the repository is public. The spike is the first work of the phase and gates the sync plans. It also answers the roadmap's research focus: `own=1` and subtype behaviour, duplicate collection entries, 202/429/403 behaviour, honest User-Agent requirements, and whether version dimensions come back in the collection response (D-14).
- **D-04:** **Outcome handling.** If the private inventory location is readable with the token, every sync asks for it and the snapshot carries each game's location (roadmap success criterion 5); nothing shows it yet (Phases 5 and 7). If it is not readable, the outcome is recorded and Phase 6 builds the location tools (LOC-03, LOC-04). The owner's password or session cookie is never stored (locked at project level).

**Sync now and freshness**
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

**Bad-result guard**
- **D-11:** **An empty result when the cabinet had games is always held back.** It is never accepted automatically, so the cabinet is never wiped (SYNC-04).
- **D-12:** **A result that shrinks by more than half is held back, then accepted when the next sync returns the same smaller collection** (the hourly run, or a manual one after the cooldown). Smaller removals, such as selling a few games, go through immediately. No owner switch is needed.
- **D-13:** **The "showing last sync from …" note appears once the last good sync is about 3 hours old, or while a suspicious result is held back.** A single failed hourly run stays quiet; the always-visible "Synced … ago" line already tells the time.

**Real games before box data arrives**
- **D-14:** **Interim box sizes:** use the owned version's width, length and depth when the collection call already returns them (the call asks for the owner's selected version anyway, so this costs no extra BGG request) and they are plausible; otherwise one realistic default size. The spike checks how often they are present and in what unit. Phase 4 completes the chain (flat-cover aspect ratio, verified units, realistic defaults). The cabinet rearranges when real sizes land; accepted, because the layout is a pure function of the collection.
- **D-15:** **Expansions show as flat expansion boxes labelled "Expansion"** (the existing orphan-expansion look, without a base-game title), placed in the first cubby with room. Phase 4 pairs them with their owned base games, and they move beside them then. The engine and renderer must handle an expansion with no known base game (sub-line "Expansion" instead of "Expansion for {base title}").
- **D-16:** **Before the first successful sync, visitors see the approved empty minimum cabinet (bare planked wood) with a short message above it**, along the lines of "The cabinet is being filled from BoardGameGeek — check back in a few minutes." This is a normal 200 page, never an error.
- **D-17:** **The deployed site shows only the real collection.** The sample collections and their switcher stay available in local development only (and the fake BGG from D-20 can serve sample-sized collections for future layout reviews). The switcher never appears on the deployed site. This largely closes the prototype item of the go-public todo; adding noindex stays in Phase 8.

**BGG credit and duplicates**
- **D-18:** **The BGG credit sits in the footer only:** the "Powered by BGG" logo, linked to BoardGameGeek, at a legible size on every public page, including the empty and error states and phone widths. The header status line does not name BGG. The logo comes from BGG's official usage page, not a third-party copy. The owner downloads it, or approves Claude downloading it at execution time.
- **D-19:** **Owning two copies or versions of the same game shows one box per copy**, like a real shelf. Each owned collection entry is its own item; expansions sit beside the first copy (lowest collection id), which the engine already does. Anything keyed per box on the page (DOM data, future detail and accessibility hooks) uses the collection entry, not only the BGG id. The shrink guard (D-12) counts collection entries.

**Carried forward (locked earlier; not re-discussed)**
- Own small typed BGG client with LINQ to XML and hardened XML reading. Base URI `https://boardgamegeek.com/xmlapi2/` without `www`; redirects carrying the token are never followed. At least 5 seconds between BGG calls, enforced in one place. An explicit, bounded loop for 202 "queued" answers. 401 is a configuration error: stop, record it, no hot retries. 403 and 5xx mean BGG is unavailable. A non-200 is never treated as "no games".
- Two collection calls per sync: base games with `excludesubtype=boardgameexpansion`, and expansions with `subtype=boardgameexpansion`. Owned items only (also check each item's own status). A full collection fetch every run; `modifiedsince` is not used.
- `BackgroundService` with `PeriodicTimer` about hourly plus start-up jitter. One bounded channel with a single consumer makes the sync single-flight. The timer, the start-up run and "sync now" all enqueue the same job.
- The token and username live only in the server env file (`dotnet user-secrets` locally), never in the repo, logs, fixtures or chat. The env example carries placeholders only. The token is sent only to the BGG API host. Visitors cannot supply a username.
- `/health` stays loopback-only and never depends on BGG: an empty or missing snapshot is healthy. Sync status may be reported there as information only.
- Fixtures and test data are synthetic: hand-written XML in BGG's response shape only, never captured real responses. The owner's BGG username equals the public GitHub handle, so the personal-data denylist cannot catch it; keep it out of code, fixtures and examples.
- No ads, donations or affiliate links. The site's JSON stays same-origin, undocumented, with no CORS headers and no bulk raw dump.
- BGG text is untrusted: render with `textContent`, never as markup.

**D-20 (folded todo): fake BGG.** A scripted `HttpMessageHandler` stub for automated tests (202 then 200, 429 without `Retry-After`, 5xx, slow responses, an HTML/Cloudflare-style page with status 200, malformed XML, empty and shrunken collections), plus a **local-only fake BGG** serving synthetic collection XML (base games, expansions via subtype) with switches for queued, throttled, slow, broken, empty and shrunken answers. It is used to run sync, cooldown, live updates and every failure state locally and to screenshot them without touching real BGG. Constraints from the todo apply: built from public docs with invented data only; never shipped in the release artifact; production configuration cannot point at it (the base-URI override is honoured in Development only); it never receives a real token. It cannot answer what needs the real token (private info, real limits, auth behaviour); the spike (D-03) covers those.

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

### Deferred Ideas (OUT OF SCOPE)
- **Public settings page (for the Phase 6 discussion).** "Sync now" and the sync status move off the main page to a separate page that everyone can open. Each control on it is either public or owner-only; owner-only controls appear and work only from the home network or VPN, enforced on the server (OWN-01 still holds). It is a natural home for the language switch (Phase 5) and the selectable-finishes todo. Making "sync now" owner-only would change SYNC-02, so that stays a Phase 6 decision.
- **Owner-data store (Phase 6).** The owner is open to their existing shared database server, SQLite or a backed-up file for image overrides and locations; decide with the backup need in view (D-01).
- Reviewed todos not folded: box look polish (Phase 4), cabinet accessibility notes (Phase 5), phone cabinet density (Phase 7), selectable cabinet finishes (unscheduled), turn off prototype mode and add noindex (Phase 8; its prototype item is largely done by D-17, noindex remains).
- Not in this phase: box art and `thing` enrichment (Phase 4), pairing expansions with base games (Phase 4), showing locations / detail card / language labels (Phases 5 and 7), owner tools and owner-data storage (Phase 6), public exposure, rate limits and resource caps (Phase 8).
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SYNC-01 | Owned collection (base games and expansions) syncs automatically about hourly | BGG client + two-call fetch, `BackgroundService` + `PeriodicTimer(TimeProvider)` + start-up jitter, FakeTimeProvider-driven tests (sections "BGG client", "Sync orchestration") |
| SYNC-02 | "Sync now" with one global persisted cooldown, remaining time shown | `SyncCoordinator` (cooldown persisted before enqueue), `POST /cabinet/sync`, status payload with `cooldownEndsUtc` + server clock offset |
| SYNC-03 | Visitors see when the collection was last synced | `lastSyncedUtc` in status payload and server-rendered `<time>` on first paint |
| SYNC-04 | Down / throttled / erroring / empty / shrunk BGG keeps last good collection with a note | `BggFailure` classification, `totalitems` check, pure `ShrinkGuard`, held-back state, stale rule (3 h or held back) |
| SYNC-05 | Intentional "cabinet is being filled" state before first successful sync | Empty-collection layout (existing engine), `hasEverSynced` flag, server-rendered message block |
| SYNC-08 | Every public page carries the linked "Powered by BGG" credit | Shared `_Layout` + `_ViewStart`, logo served from own origin, per-page test |
| LOC-02 | Spike with real token determines whether private inventory location is readable; outcome recorded | Spike design ("Live spike design"), outcome template, conditional `showprivate=1` + location in snapshot |
| SEC-05 | Token and username server-side only; visitors cannot supply a username or cause BGG calls beyond the guarded sync | Config via env/user-secrets only, token handler pinned to the BGG host, no redirect following, log filter for HttpClient URLs, sentinel-secret leak tests |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Treated as locked, same authority as the decisions above.

- **No planning references outside `.planning/`**: no requirement keys, decision IDs, phase/plan numbers or planning document names anywhere in code, strings, tests, docs, scripts, config, workflow files, the vendored-asset notes or the logo's commit message body beyond git history. Enforced by `build/lint/checks/10-repo-rules.sh` over every tracked file (patterns verified by reading the script this session). Every code example below is written to comply.
- **C#: `///` XML doc summaries only. No `//` comments.** JS: `/** ... */` doc blocks only (lint rejects any `//` not after a colon and any `/*` not opening `/**`). Shell `#` comments are fine.
- **Public repo, no personal data**: no BGG username, token, real domain, homelab IP, real location names, recorded BGG responses. The owner's BGG username equals the GitHub handle: never write it, and keep the User-Agent contact URL in the env file, not in code. Fixtures are hand-written synthetic XML.
- **Never commit to `main`**; work on `milestone/v1-games-cabinet` / feature branches.
- **BGG etiquette**: only the background sync talks to BGG (plus the cooldown-guarded "sync now"); "Powered by BGG" linked credit on every public page; no ads/donations/affiliate links; token only to the BGG API host.
- Warnings-as-errors and lock files (`RestorePackagesWithLockFile`); release script restores with `--locked-mode`, so every new package needs updated `packages.lock.json` files committed.

## Summary

The phase is mostly assembly of well-understood ASP.NET Core parts around three BGG unknowns that only a live run from the container can answer. The design is: a small typed `BggClient` (Repository) that makes at most one request per 5 seconds, never follows redirects, attaches the token only for host `boardgamegeek.com`, classifies every non-200 into a failure category and parses collection XML with a hardened `XmlReader`; a pure Domain layer (snapshot model, mapper to `CabinetItem`, `ShrinkGuard`, cooldown policy); JSON-file stores written with temp file + fsync + `File.Move(overwrite: true)` (verified to be `rename(2)` on Linux, so atomic when the temp file sits in the same directory); and a Service layer with a single-flight coordinator, a hosted service, status/sync endpoints and a server-to-client-only SignalR hub. Everything is driven by `TimeProvider`, so cooldown, jitter, spacing and the hourly tick are tested with `FakeTimeProvider` (verified working with `PeriodicTimer` and `Task.Delay` in this session).

Findings that change the plan relative to the project-level research: (1) do **not** use `Channel` `DropWrite` for coalescing: `TryWrite` returns `true` even when the item was dropped (verified on .NET 10.0.112); use capacity 1 with `Wait` and read the `false` from `TryWrite`. (2) The existing integration factory builds **two** hosts (a TestServer host and a real Kestrel host); any hosted service will run twice unless it is switched off for one of them, so sync tests need a flag and per-test opt-in. (3) The default `HttpClient` logging writes the request URL, which contains `username=`; it must be filtered. (4) `deploy/provision.d/20-accounts.sh` writes `/etc/cabinet/cabinet.env` once and never again, so the owner adds the BGG keys by hand and the app must start healthy, serve "being filled", and record a configuration failure when they are missing. (5) The vendored SignalR client (`@microsoft/signalr` 10.0.11, `dist/browser/signalr.min.js`, 47,668 bytes) passes every repo lint pattern except its final line `//# sourceMappingURL=...`, so the JS-comment lint needs an explicit vendored-path exclusion; it also contains a `new Function("return this")` fallback that is never reached because `globalThis` exists, and CSP `'self'` already covers same-origin `ws:`/`wss:` in all three engines (Safari since the April 2022 WebKit fix), so **no CSP change is required**; verify in real browsers rather than loosening the policy.

**Primary recommendation:** Build in three parallel tracks that meet at the spike outcome: (A) spike script + owner prerequisites (gates only location, units and User-Agent decisions); (B) pure Domain + storage + BGG client/parser + sync runner against the scripted stub and the fake BGG; (C) Service/page work (shared layout and credit, status/sync endpoints, SignalR hub, vendored client, layout-endpoint switch to real data, layout JSON additions). Keep every BGG-dependent knob (unit factor, `showprivate`, User-Agent) a single constant or setting that the spike outcome flips.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Talk to BGG (spacing, 202 loop, classification, token) | API / Backend (Repository) | — | Only the server may call BGG; token never leaves the server |
| Parse BGG XML, map to snapshot | API / Backend (Repository) | Domain (pure mapping) | Hardened XML reading is outside-world code; mapping is pure |
| Shrink/empty guard, cooldown policy, stale rule inputs, box-size mapping | Domain (pure) | — | Pure functions, unit-tested without I/O |
| Snapshot and sync-state persistence | Database / Storage (files under `StateDirectory`) | API / Backend | Atomic JSON files behind an interface |
| Single-flight scheduling, hourly timer, manual trigger | API / Backend (Service hosted service) | — | One consumer loop, state in memory + persisted cooldown |
| Layout computation for the real collection | API / Backend (Domain engine, Service cache) | — | Engine is a pure function; cache keyed by snapshot version + profile |
| Status line text on first paint, stale note, "being filled" block | Frontend Server (Razor Pages SSR) | Browser | Server renders UTC-based no-flash text; script rewrites dates to local time |
| Live updates, countdown, relative time, quiet redraw, button states | Browser / Client (vanilla ES modules + vendored SignalR client) | API (hub broadcast + status endpoint) | Needs visitor clock/timezone; server only pushes state |
| Credit footer and logo | Frontend Server (shared `_Layout`) + CDN/Static (`wwwroot`) | — | Every HTML page inherits it; logo served from own origin |
| CSP and WebSocket allowance | API / Backend (existing middleware) | Browser enforcement | App owns its policy; verify per engine |
| Fake BGG for dev | Separate dev-only project | — | Never in the release artifact |

## Standard Stack

### Core
| Library | Version | Purpose | Why Standard |
|---------|---------|---------|--------------|
| ASP.NET Core (shared framework, SignalR server included) | 10.0.x; SDK pinned 10.0.112 | Host, `HttpClient`, `BackgroundService`, `PeriodicTimer`, `Channel<T>`, SignalR hub | No new server package: SignalR server ships in `Microsoft.AspNetCore.App` [VERIFIED: ran a hub on the 10.0.112 SDK in a scratch project this session] |
| `System.Xml.Linq` + `XmlReader` | BCL | BGG response parsing | Tolerates optional nodes; hardened via `XmlReaderSettings` |
| `System.Text.Json` | BCL | Snapshot, state, status payloads | Already used by `LayoutJson` |
| `@microsoft/signalr` (vendored file only) | 10.0.11 (published 2026-08-04, MIT, `dist/browser/signalr.min.js`) | Browser SignalR client | Official Microsoft package; single UMD file, no Node toolchain [VERIFIED: npm registry; tarball inspected this session] |

### Supporting
| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 (MIT, Microsoft) | `FakeTimeProvider` | UnitTests and IntegrationTests for timer, cooldown, spacing, jitter [VERIFIED: nuget.org nuspec; STACK.md already pins it as the reference-project choice] |
| `Microsoft.AspNetCore.SignalR.Client` | 10.0.12 (MIT, Microsoft) | .NET hub client in integration tests | IntegrationTests only: prove broadcasts arrive and no method is invokable [VERIFIED: nuget.org nuspec] |
| xunit.v3 4.0.1, FluentAssertions 8.11.0, Mvc.Testing 10.0.12 | existing | Test stack | Already referenced; no change |

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Hand-written pacing + one request per call (no retries) | `Microsoft.Extensions.Http.Resilience` 10.10.0 (project-level STACK choice) | **Recommend not adding it for this phase.** Its default pipeline retries 429/5xx up to 3 times, multiplying calls exactly when BGG is struggling, and conflicts with D-05 (a failed manual sync must not be retried on demand). The hourly cadence is the retry. The 202 loop is domain logic and has to be explicit anyway. Saves a package and lock-file churn. Adopt later only if real traffic shows transport blips worth retrying. |
| Vendored UMD `signalr.min.js` as a classic script | Vendor `dist/esm/*` (about 30 files) for `import` | UMD is one file with one checksum; the module graph is not worth it. A classic `<script src>` before the module script sets `window.signalR`. |
| `NSubstitute` / `RichardSzalay.MockHttp` | Hand-written `ScriptedHandler : HttpMessageHandler` and small fakes | No new packages; D-20 asks for a scripted handler anyway |
| Python3 stdlib spike script piped over SSH | A `bgg-spike` subcommand in `Cabinet.Service` (like the existing `image-smoke`) | The subcommand reuses the real client but needs a tagged, approved, deployed release before the spike can run (days), and the spike gates the plans. The LXC has `dotnet` runtime only, no SDK. Python3 is very likely present (`unattended-upgrades` is in the provisioned package list and depends on it) [ASSUMED: confirm `python3 --version` on the container]. Fallback if absent: bash + `curl` + `jq` cannot read XML, so use the subcommand route. |

**Installation (executor, after owner approval for any download):**
```bash
dotnet add Cabinet.UnitTests/Cabinet.UnitTests.csproj package Microsoft.Extensions.TimeProvider.Testing --version 10.10.0
dotnet add Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj package Microsoft.Extensions.TimeProvider.Testing --version 10.10.0
dotnet add Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj package Microsoft.AspNetCore.SignalR.Client --version 10.0.12
dotnet restore Cabinet.slnx --force-evaluate
npm pack @microsoft/signalr@10.0.11
```
Commit the regenerated `packages.lock.json` files (the release script restores with `--locked-mode`).

**Version verification (this session):** `npm view @microsoft/signalr` returned version `10.0.11`, dist-tag `latest`, modified `2026-08-04T05:00:19.932Z`, license MIT, `dist.integrity` `sha512-FulOJ2EEtKvLQcswe/U7v8pzyXyk7Jua5xgWJPPwyQU/2Z9ORvKcVjyO75VvLsem+CJ1ORRexJ+7Bz1FCei2aw==`, `dist.shasum` `2b96b016ed38f9754178d389ca1d7fd89666f471`, no `postinstall`. NuGet flat-container index shows `Microsoft.Extensions.TimeProvider.Testing` up to 10.10.0 and `Microsoft.AspNetCore.SignalR.Client` 10.0.12 available. The `8.0.x` / `9.0.x` dist-tags also exist; use the 10.0 line to match the server.

**Vendored file facts (from the 10.0.11 tarball, inspected this session):**

| Item | Value |
|------|-------|
| Path in tarball | `package/dist/browser/signalr.min.js` |
| Size | 47,668 bytes |
| SHA-256 of the file | `97e9b97e642a72e5a470917147a2bf79f86cad829a5c6786adb10614d248bb95` |
| Companion map | `signalr.min.js.map` (4a28abdf...; **do not vendor**; the file's last line `//# sourceMappingURL=signalr.min.js.map` would 404 only when devtools are open) |
| Global it defines | `signalR` (UMD: `t.signalR=e()`), no ES export |
| License notice in file | none; MIT is in `package.json`; add a short notice file next to it |
| Repo lint patterns it trips | exactly one: JS line-comment rule on the last line (`//# sourceMappingURL`). Zero hits for plain `/*` blocks, requirement keys, decision IDs, phase words, planning file names, e-mail shapes |
| CSP-relevant code | one `new Function("return this")()` inside a `try`, reached only if `globalThis` is not an object; every supported browser has `globalThis`, so no violation fires. The executor must still confirm "no CSP violation in the console" in each engine |

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| `@microsoft/signalr` 10.0.11 | npm | years (10.x line) | about 2.06M/week | github.com/dotnet/aspnetcore | OK (`gsd-tools query package-legitimacy check` returned `OK`, no postinstall, not deprecated) | Approved; fetched as a tarball for one file, never installed into the repo |
| `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 | NuGet | Microsoft first-party | n/a | dotnet/extensions | seam does not cover NuGet; authors/licence confirmed from nuspec | Approved (already named in the project STACK as the reference choice) |
| `Microsoft.AspNetCore.SignalR.Client` 10.0.12 | NuGet | Microsoft first-party | n/a | dotnet/aspnetcore | seam does not cover NuGet; authors Microsoft, MIT | Approved; test project only |

**Packages removed due to SLOP:** none. **Packages flagged SUS:** none. No package here was discovered from a non-authoritative source.

## Architecture Patterns

### System Architecture Diagram

```
                        BGG XML API2 (https://boardgamegeek.com/xmlapi2/, Bearer token)
                                      ^   (>= 5 s apart, no redirects, token only on this host)
                                      |
 timer tick -----+                    |
 start-up run ---+--> SyncCoordinator --(single-flight)--> SyncRunner
 POST /cabinet/sync (cooldown gate) -+       |                |
                                             |    1 fetch base  2 fetch expansions   (202 loop, classify, totalitems check)
                                             |                |
                                             |        merge by collection id, own=1 only
                                             |                |
                                             |        ShrinkGuard (pure) vs current snapshot + held-back state
                                             |          Accept            HeldBack(kind, fingerprint)
                                             |            |                       |
                                             |   write snapshot.json         write sync-state.json
                                             |   (tmp + fsync + rename)      (held-back record)
                                             |            |
                                             v            v
                                  SyncStatusStore   CollectionStore (immutable CollectionState: snapshot,
                                  (state in memory)  version hash, layouts per profile; swapped atomically)
                                             |            |
                          +------------------+            +---------------------+
                          v                                                     v
              CabinetHub broadcast (server -> client only)        GET /cabinet/layout?profile=   (ETag = layout version
              statusChanged(status payload)                       + fingerprint + snapshot version + profile)
                          |                                                     ^
   Browser: status.js / live.js (vendored signalR) --- GET /cabinet/status ----+ (on connect, reconnect, visible)
            POST /cabinet/sync (visitor press)   quiet redraw via cabinet.js load()
            Razor Pages shell (_Layout: head, footer, BGG credit) renders first-paint status + stale note + "being filled"
```

### Recommended Project Structure
```
Cabinet.Domain/Collection/        # Snapshot records, SnapshotMapper, BoxFromVersion, ShrinkGuard, SyncStatus, CooldownPolicy, interfaces (IBggClient, ISnapshotStore, ISyncStateStore)
Cabinet.Repository/Bgg/           # BggClient, BggXmlParser, BggAuthHandler, RequestPacer, BggFailure, BggOptions
Cabinet.Repository/Storage/       # AtomicJsonFile, SnapshotStore, SyncStateStore
Cabinet.Service/Sync/             # SyncSettings, SyncCoordinator, SyncRunner, SyncBackgroundService, SyncEndpoints (status + sync)
Cabinet.Service/Live/             # CabinetHub, ICabinetClient, LiveNotifier, LiveConnectionLimiter
Cabinet.Service/Layout/           # LayoutCache re-keyed by snapshot version (existing files change)
Cabinet.Service/Pages/            # _ViewStart.cshtml, Shared/_Layout.cshtml, Index.cshtml(.cs)
Cabinet.Service/wwwroot/js/       # cabinet.js (entry), status.js, live.js, copy.js, render.js
Cabinet.Service/wwwroot/lib/signalr/   # signalr.min.js (byte-identical to upstream) + NOTICE
Cabinet.Service/wwwroot/img/      # powered-by-bgg logo (official asset)
Cabinet.FakeBgg/                  # dev-only Web SDK project (not referenced by Service, not published)
docs/                             # sync and operations guide, vendored-asset notes, dev guide additions (no planning references)
```

### Pattern 1: Immutable collection state swapped atomically
**What:** One `CollectionStore` singleton holds an immutable `CollectionState(Snapshot? snapshot, string? version, lazily built layouts per profile)`. A successful sync builds a new state and replaces the reference (`Volatile.Write`). Layout and page reads never lock.
**When to use:** every read path (layout endpoint, page, status). The layout cache dies with the state, which bounds it to two entries (plus dev samples) and removes the need to evict by key.
**Example:**
```csharp
public sealed class CollectionStore
{
    private CollectionState _current = CollectionState.Empty;

    public CollectionState Current => Volatile.Read(ref _current);

    public void Replace(CollectionState next) => Volatile.Write(ref _current, next);
}
```

### Pattern 2: Single-flight with an explicit coordinator, `Wait` channel, and persisted cooldown
**What:** `SyncCoordinator.TryRequest(trigger)` is the only entry. It decides under a lock: already running or queued -> `Running`; manual and `now < cooldownEndsUtc` -> `Cooldown`; else persist the new cooldown/started time, mark running, `TryWrite` to a capacity-1 `Wait` channel. One consumer loop awaits the channel and runs `SyncRunner`. Persist **before** enqueue so a crash after acceptance cannot lose the cooldown.
**Example:**
```csharp
public SyncRequestResult TryRequest(SyncTrigger trigger)
{
    lock (_gate)
    {
        var now = _time.GetUtcNow();

        if (_running)
        {
            return SyncRequestResult.AlreadyRunning;
        }

        if (trigger == SyncTrigger.Manual && now < _state.CooldownEndsUtc)
        {
            return SyncRequestResult.CoolingDown(_state.CooldownEndsUtc);
        }

        _state = _state with { LastStartedUtc = now, CooldownEndsUtc = now + _settings.ManualCooldown };
        _stateStore.Save(_state);
        _running = true;

        return _queue.Writer.TryWrite(trigger) ? SyncRequestResult.Started : SyncRequestResult.AlreadyRunning;
    }
}
```
Verified channel semantics (scratch run, .NET 10.0.112): `BoundedChannelFullMode.DropWrite` -> `TryWrite` returned `True` twice (the second item is silently dropped); `Wait` -> `True` then `False`. Use `Wait`.

### Pattern 3: Pure guard returning a decision
**What:** `ShrinkGuard.Evaluate(int previousCount, IReadOnlyCollection<long> candidateEntryIds, HeldBackRecord? heldBack)` returns `Accept` or `HeldBack(kind, fingerprint, count)`. No I/O, no clock.
**Semantics (recommended, all counted in collection entries):**
- `previousCount == 0` -> `Accept` (first sync, including an owner with genuinely zero games).
- `candidate.Count == 0 && previousCount > 0` -> `HeldBack(Empty)`; **never accepted automatically**, even on repeat (D-11).
- `candidate.Count * 2 < previousCount` (removes strictly more than half) -> `HeldBack(Shrunk)` unless `heldBack?.Fingerprint == Fingerprint(candidate)` and `heldBack.Kind == Shrunk`, in which case `Accept` (D-12). Fingerprint = SHA-256 of the sorted entry ids; compare the set, not the count.
- anything else -> `Accept`, and any stored held-back record is cleared.
- A new suspicious result with a different fingerprint replaces the held-back record (the "second agreement" restarts).
**Known limitation to document:** an owner who really deletes the whole collection can never reach an empty cabinet automatically; the escape hatch is deleting `snapshot.json` from the state directory. Mention in the operations notes.

### Pattern 4: Atomic JSON file write
```csharp
public static void WriteAtomically(string path, ReadOnlySpan<byte> content)
{
    var directory = Path.GetDirectoryName(path)!;
    var temporary = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");

    using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
    {
        stream.Write(content);
        stream.Flush(flushToDisk: true);
    }

    File.Move(temporary, path, overwrite: true);
}
```
Why this is sound: [VERIFIED: dotnet/runtime `FileSystem.Unix.cs` `MoveFile(source, dest, overwrite: true)` calls `rename(2)` directly; only on `EXDEV` (different devices) does it fall back to copy + delete]. So the temp file must be in the **same directory** as the target (never `/tmp`; the unit has `PrivateTmp=yes` and `ProtectSystem=strict` anyway). Skip fsync of the directory: the data is rebuildable, and a crash window at worst leaves the previous file. Delete stray `.*.tmp` files at start-up.

### Pattern 5: Server-to-client-only hub with an injected notifier
```csharp
public interface ICabinetClient
{
    Task StatusChanged(CabinetStatus status);
}

public sealed class CabinetHub(LiveConnectionLimiter limiter) : Hub<ICabinetClient>
{
    public override Task OnConnectedAsync()
    {
        if (!limiter.TryAdmit(Context.ConnectionId))
        {
            Context.Abort();
            return Task.CompletedTask;
        }

        return base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        limiter.Release(Context.ConnectionId);
        return base.OnDisconnectedAsync(exception);
    }
}
```
The sync code never sees the hub: it calls an `ILiveNotifier` whose production implementation wraps `IHubContext<CabinetHub, ICabinetClient>`; tests inject a recorder. A broadcast failure is caught and logged, never fails a sync.

**Verified (scratch project, .NET 10.0.112 + SignalR.Client 10.0.0):** a hub with only `OnConnectedAsync` overridden accepted a WebSockets-only client with **no** `app.UseWebSockets()` call; `IHubContext.Clients.All.SendAsync` reached the client; `InvokeAsync("OnConnectedAsync")`, `Dispose`, `GetType`, `Equals` and an unknown name all failed with `HubException: Method does not exist`. So a hub with no declared public methods exposes nothing invokable, which still deserves a permanent integration test.

### Pattern 6: BGG token handler pinned to the BGG host
```csharp
public sealed class BggAuthHandler(BggOptions options) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Authorization = null;

        if (request.RequestUri is { Scheme: "https" } uri
            && string.Equals(uri.IdnHost, BggOptions.ApiHost, StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrEmpty(options.Token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.Token);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
```
`BggOptions.ApiHost` is the constant `boardgamegeek.com` (no `www`). With the Development-only base-URI override pointing at the fake (`http://127.0.0.1:...`), no token is attached, so the fake never receives a real token, with no extra code path. The primary handler is `SocketsHttpHandler { AllowAutoRedirect = false, AutomaticDecompression = DecompressionMethods.All }`; a 3xx is classified as `Unavailable` and logged by status only.

### Anti-Patterns to Avoid
- **`DropWrite` for coalescing**: hides the "already queued" fact (verified above).
- **Hosted sync in every test host**: the existing factory builds two hosts (see Pitfall 2).
- **Letting an inline `<script type="application/json">` carry page state**: the existing test `InlineScriptBody` regex fails any `<script>` without `src` that has a body. Use `data-*` attributes on the mount/header.
- **Reading the username from any request**: the endpoints take no parameters that reach BGG.
- **Logging `ex.ToString()` of `HttpRequestException`** or any request URI: the URI contains `username=`. Log the failure category only.
- **Per-IP or other state on the hub**: Phase 8 owns limits; keep the cap simple and global.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Real-time push and fallback transports | Custom WebSocket/SSE protocol | SignalR hub + official JS client (automatic WebSockets -> Server-Sent Events fallback) | Heartbeats, reconnect, framing already solved; fallback transports use `fetch`/`EventSource`, which `'self'` covers |
| Fake time | Custom clock abstraction | `TimeProvider` (BCL) + `FakeTimeProvider` | `PeriodicTimer(period, timeProvider)` and `Task.Delay(delay, timeProvider, ct)` are first-party and tested [VERIFIED scratch run: `Advance(1h)` then `Advance(3h)` produced 4 ticks; a 5 s `Task.Delay` completed only after `Advance(5s)`] |
| XML parsing safety | Regex/string scanning of BGG bodies | `XmlReader` with `DtdProcessing.Prohibit`, `XmlResolver = null`, bounded size, then `XDocument` | DTD/entity attacks and malformed input are handled by the reader |
| Relative-time and date formatting | Hand-rolled English plural/locale code | `Intl.RelativeTimeFormat` and `Intl.DateTimeFormat` in the browser (as the UI contract says); a C# function only for the no-flash first paint, locked to the same case table | Two implementations must agree: keep one shared fixture of (elapsed seconds -> text) cases |
| Atomic persistence | Custom journal/lock files | temp + `Flush(true)` + `File.Move(overwrite)` | `rename(2)` atomicity, see Pattern 4 |
| Retry/backoff framework | Polly pipeline for this phase | Explicit bounded 202 loop; hourly cadence as the retry | See Alternatives; avoids call multiplication |
| Scheduling | Hangfire/Quartz | `BackgroundService` + `PeriodicTimer` | Locked at project level |
| Images/colours | n/a | Phase 4 | Out of scope |

**Key insight:** every piece that looks like infrastructure here is already in the shared framework; the real work is the policy (what counts as success, what may replace the snapshot, who may trigger what) and keeping that policy in pure, tested functions.

## Detailed Findings

### 1. Live spike design (what the executor must run, and what it must measure)

**Owner prerequisites (before the run):** BGG application approved and a token created (STATE says done); `Bgg__Token` and `Bgg__Username` added to `/etc/cabinet/cabinet.env` by hand (provisioning never rewrites that file: `deploy/provision.d/20-accounts.sh` writes it only when absent [VERIFIED: 20-accounts.sh lines 27-60, `if [[ ! -f "$ENV_PATH" ]]`]); an inventory location set on two or three games on BGG (D-02).

**Where and how it runs:** committed as a single-file Python 3 stdlib script (suggested path `deploy/tools/bgg-spike.py`; it is a one-off operations tool, contains no secrets, uses `#`-free docstring style to avoid any ambiguity with the comment rules). Run as the app user so file permissions match the service: `ssh <lxc-alias> 'sudo -u cabinet python3 - ' < deploy/tools/bgg-spike.py`. The script itself parses `/etc/cabinet/cabinet.env` (a `KEY=VALUE` file; strip matching quotes; ignore comments) for `Bgg__Token`, `Bgg__Username`, optional `Bgg__ContactUrl`; the file is `640 root:cabinet` [VERIFIED: `deploy/bin/cabinet-selfcheck` line 86 `check_path /etc/cabinet/cabinet.env 640 root:cabinet`], readable by the `cabinet` user.

**Transport rules it must follow (mirrors the production client):** `http.client.HTTPSConnection("boardgamegeek.com")` (no redirect following by construction); header `Authorization: Bearer <token>` only on that host; `Accept: text/xml, application/xml`; honest `User-Agent: GamesCabinet-spike/1 (+<ContactUrl if configured>)`; at least 6 s between any two requests; hard cap of about 14 requests in total; 202 polling with waits 5, 10, 20, 30 s for at most 6 polls per call.

**Call plan (about 10 requests, each labelled):**

| Label | Request | Question it answers |
|-------|---------|---------------------|
| A | `collection?username=U&own=1&excludesubtype=boardgameexpansion&stats=1&version=1` | the production base-games call: 202 count and timing, `totalitems` vs item count, element/attribute shape, own flags, duplicates, version dimension presence and numeric ranges, `stats` presence |
| B | same as A plus `showprivate=1` | does `<privateinfo>` appear with only the Bearer token; which attributes; how many items carry a non-empty `inventorylocation` (LOC-02) |
| C | `collection?username=U&own=1&subtype=boardgameexpansion&stats=1&version=1` | the expansion split: item count, `subtype` values returned, overlap with A (must be 0) |
| D | `collection?username=U&own=1&stats=1` (no subtype filters) | does the default call return expansions mislabelled as `boardgame`; does |D| = |A| + |C| |
| E | `collection?username=U&own=1&showprivate=1&subtype=boardgameexpansion` | whether private info also comes back on the expansion call |
| F | `thing?id=13` **with** an honest User-Agent | baseline for a cheap authenticated call (public game id, no personal data) |
| G | `thing?id=13` with **no** User-Agent header | is a User-Agent required or does Cloudflare react? |
| H | `collection?username=U&own=1` **without** the Authorization header | exact 401 shape (status, content type, body class) |
| I | `collection?username=U&own=1` with a deliberately wrong token value | 401 vs 403 for a bad token |

Do **not** try to provoke 429; record it only if it happens. Do not call `www.boardgamegeek.com` with a token (the token must not even be attempted there). If any call returns 401, 403 or 429, stop the run after recording it (the production rule: no hot retries).

**Output contract (printed shape only):** per call: label, HTTP status, wall time in whole seconds, selected response headers (only: `content-type`, `content-length`, `cache-control`, `retry-after`, `server`, `cf-mitigated`, `x-ratelimit-*` names with values, and the **names** of any `set-cookie`), body byte length, body class (`xml:items`, `xml:errors`, `xml:other`, `html`, `empty`, `other`), and for `html` bodies only booleans (`mentions_cloudflare`, `has_form`). For `items` bodies:
- root attributes: `totalitems` value, names of other root attributes; parsed item count; `totalitems == item count`
- distinct item attribute names with counts; distinct child element names (and their attribute names) with counts, **names only**
- `subtype` value distribution; `status@own` distribution (`own!="1"` count); objectids repeated (count of duplicated ids); collids repeated (count)
- `version` child: items with a `version` element; items whose version carries `width`, `length`, `depth` as non-zero numbers; for each of the three: minimum, median, maximum of the raw numbers and the count of zeros (these are measurements, not personal data); whether the number lives in the element's `value` attribute or its text; count where `length >= width`; the `thumbnail`/`image` child presence counts
- name element form: text node vs `value` attribute; count of names whose decoded text still contains a literal `&amp;` or `&#`
- `yearpublished`, `numplays`, `comment`: whether text or attribute, presence counts
- `privateinfo`: items having it, attribute names found on it, count with non-empty `inventorylocation`, count of **distinct** non-empty values (a number only), the maximum value length (a number only)
- `stats`: items having it; attribute names

The script must never print: the token, the username, any title, any location value, any URL, any raw XML. Add a self-check that scans its own output buffer for the token and username strings before printing and aborts if found.

**Outcome file:** `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SPIKE-OUTCOME.md` with these headings: Run (date, release version, duration, request count), Decision table (below), Per-call shape summary, Open items. The owner reads the printed summary and signs off before it is committed; the commit goes through the pre-commit denylist hook as usual.

**Decision table the outcome must fill, and what each answer flips:**

| Question | If yes / value | Consequence in the build |
|----------|----------------|--------------------------|
| `privateinfo` with non-empty `inventorylocation` returned for B (token only) | yes | Production calls add `showprivate=1` on both collection calls; snapshot items carry `location`; Phase 6 location tools are not needed (LOC-03/04 stay unbuilt) |
| same | no (absent, or present without `inventorylocation`, or 401/403 on B) | Production calls omit `showprivate`; snapshot has no location; Phase 6 builds the owner location tools; do not request private data again |
| `version` dimensions present for what fraction of items, units (median magnitudes) | inches expected (median front side about 10-16, depth about 1-4) | `MillimetresPerInch = 25.4` constant in the box mapper; if medians are about 250-400 treat as millimetres, about 25-40 as centimetres; set the constant accordingly |
| `length >= width` for nearly all | yes | map `length` -> standing height, `width` -> front width (matches the approved sample convention: height is the longer side) |
| User-Agent: G (none) differs from F (honest) | blocked or challenged | honest UA stays mandatory and is set in the client; if not blocked, still send it (politeness) |
| 202 observed on A/C | typical | record count and seconds; set the 202 wait schedule and cap from the observation |
| `totalitems` equals parsed item count | yes | keep it as a hard integrity check; if not, downgrade to a warning and rely on the guard |
| Default call D returns expansions as `boardgame` | yes | confirms the two-call split is required |
| Name form / entity artefacts | text nodes, no `&amp;` artefacts | parse names as element text, no decoding step |

### 2. BGG XML API2 collection: what is known vs to be measured

| Fact | Detail | Source / confidence |
|------|--------|---------------------|
| Auth | `Authorization: Bearer <token>`; host `boardgamegeek.com` without `www` ("may interfere with request authorization") | `.claude/CLAUDE.md` "BGG API Access Rules" table (project-verified summary of the official page) [CITED] |
| 202 | "if it's 202 (vs. 200) ... BGG has queued your request and you need to keep retrying (hopefully w/some delay between tries) until the status is not 202" | official wiki as quoted in project docs [CITED]; third-party lib `boardgamegeek2` retries with `retry_delay` and 1.5x growth, treats 503 as throttle [VERIFIED: its `utils.py` read this session] |
| Collection item shape | `<items totalitems=..>` -> `<item objecttype objectid subtype collid>` with `<name>` (element text), `<yearpublished>`, `<image>`, `<thumbnail>`, `<status own prevowned fortrade want wanttobuy wishlist preordered lastmodified>`, `<numplays>`, `<comment>`, optional `<stats minplayers maxplayers minplaytime maxplaytime playingtime ...>`, optional `<version><item type="boardgameversion" id=..>` | read from a maintained parser's source (`boardgamegeek2` 1.0.1 `loaders/collection.py`: names via element text `xml_subelement_text(item, "name")`, status attributes listed, `stats` attributes, `version` -> `item[@type='boardgameversion']`) [CITED, third-party; MEDIUM]. `collid` is documented in the project research; spike confirms |
| Version item | children `thumbnail`, `image`, `link type=language/boardgamepublisher/boardgameartist`, `name value=`, `yearpublished value=`, `productcode value=`, `width value=`, `length value=`, `depth value=`, `weight value=` (floats, 0 when unknown) | `boardgamegeek2` `utils.py get_board_game_version_from_element` [CITED, third-party; MEDIUM] |
| Units | inches (BGG shows inches) and 0 means "never entered" | third-party project notes (0.5" tolerances, 13.2" Kallax opening; "BGG's 0 parses as never entered") [CITED, LOW-MEDIUM]; **spike measures** |
| `version=1` | returns the owner's selected version only; items without one simply omit it | project STACK/PITFALLS + a third-party skill note [CITED, MEDIUM] |
| `showprivate=1` | "Only works when viewing your own collection and you are logged in (include cookies...)"; an application token is not a website login | official wiki per project docs [CITED]; whether the token alone suffices is **unknown until the spike**. Shape on success expected as `<privateinfo ... inventorylocation=".."/>` [ASSUMED attribute names: `pricepaid`, `pp_currency`, `currvalue`, `cv_currency`, `quantity`, `acquisitiondate`, `acquiredfrom`, `inventorylocation`; the spike prints the real names] |
| 429 | reported without `Retry-After`; unpublished limits; 5 s between requests "seems to suffice" | project docs [CITED]; spike must not provoke |
| Error body | HTTP 200 with `<errors><error><message>..` seen for bad usernames; Cloudflare/HTML pages possible | `boardgamegeek2` checks `.//error`; project PITFALLS [CITED] |
| Official pages unreachable by automation | BGG pages returned 403 to every automated fetch this session (Cloudflare), as the project docs predicted | [VERIFIED this session] |

### 3. BGG client behaviour (prescriptive)

- **Calls per sync:** base (`own=1&excludesubtype=boardgameexpansion&version=1[&showprivate=1]`) then expansions (`own=1&subtype=boardgameexpansion&version=1[&showprivate=1]`). Do not send `comment=1` (the public-comment location convention was rejected) and, for this phase, do not send `stats=1` (nothing uses it; Phase 4 adds it if it saves `thing` calls). `stats=1` is in the spike calls so Phase 4 learns whether it is worth it.
- **Pacing:** `IRequestPacer.WaitTurnAsync(ct)` guarded by a `SemaphoreSlim(1,1)`: before each request, `await Task.Delay(Max(0, lastRequestEnd + gap - now), timeProvider, ct)`; `gap = Max(configured, 5 s)` (floor in code). One pacer instance shared by the client; every request including each 202 poll passes through it. Tests substitute a no-op pacer or drive a real one with `FakeTimeProvider`.
- **202 loop (per call):** waits 5, 10, 20, 30, 30, 30 s (about 2 minutes) then give up with failure `Queued`; total request budget per sync 16; the whole sync has a 10 minute `CancellationTokenSource`. Final numbers come from the spike.
- **Classification (`BggFailure`):** `NotConfigured` (token or username empty, before any request), `Unauthorized` (401: stop the run, no retry in this run; hourly cadence continues), `Unavailable` (403, 3xx, 5xx, connection errors; includes Cloudflare), `Throttled` (429, 503 with throttle semantics; abort the run immediately; honour a `Retry-After` only by recording it), `Timeout`, `BadAnswer` (200 but wrong content type, HTML, `<errors>`, unexpected root, malformed XML, `totalitems` mismatch), `Queued` (202 cap reached). **No failure is ever mapped to "empty collection".** A failure in the expansion call fails the whole sync (never commit base games alone).
- **Hardened reading:** `XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 20_000_000, MaxCharactersFromEntities = 0, IgnoreComments = true, IgnoreProcessingInstructions = true }` over the response stream; load with `XDocument.Load(reader)`; also cap the response body (`HttpClient.MaxResponseContentBufferSize = 20 MB`). Check content type is `text/xml` or `application/xml` before parsing. Consider `CheckCharacters = false` plus a string sanitiser (drop C0 control characters, trim, cap title length) so one bad character in one title cannot stall every sync [ASSUMED: BGG occasionally emits such characters; if it does not, the default `true` is stricter and fine]. A single malformed item degrades that item (skip and count), but a malformed document fails the sync.
- **Mapping:** keep only items with `status@own == "1"`; key = `collid`; if the same `collid` appears in both calls, the expansion call wins as `kind = expansion`; the engine rejects exact `(collid, objectid)` duplicates, so dedupe by `collid` before mapping. Blank or whitespace title stays blank (renderer shows "Untitled game").
- **User-Agent:** `GamesCabinet/{assemblyVersion}` plus ` (+{Bgg:ContactUrl})` when the optional setting is present. The repository URL is the owner's GitHub handle, which equals the BGG username, so it lives in the env file, never in code (hard rule). Never a browser string.
- **HttpClient logging:** the standard `System.Net.Http.HttpClient.*` logs include the request URL (with `username=`). Set `"System.Net.Http.HttpClient": "Warning"` in the committed `appsettings.json` (not only Production) and add a test that boots with a sentinel username and asserts no captured log line contains it.
- **Base URI:** constant `https://boardgamegeek.com/xmlapi2/`. `Bgg:BaseUri` is read only when `IHostEnvironment.IsDevelopment()`; in any other environment the setting is ignored and a warning is logged if it is present. Tests (environment `Testing`) override with `services.PostConfigure<BggOptions>(...)`.

### 4. Sync orchestration, timing and tests

- **Settings (committed `appsettings.json`, validated at start-up like `LayoutSettings`, error message names the key):** `Sync:BackgroundEnabled` (true), `Sync:IntervalMinutes` (60, floor 15 in code), `Sync:ManualCooldownMinutes` (10, floor 1), `Sync:StartupJitterMaxSeconds` (120), `Sync:StaleAfterHours` (3), `Bgg:MinRequestGapSeconds` (5, floor 5), `Live:MaxConnections` (100). `Bgg:Token`, `Bgg:Username`, `Bgg:ContactUrl`, `Storage:Directory` are **not** committed (the configuration test flags any non-empty value under a key containing `token`, `secret`, `password`, `apikey`).
- **Loop:** on start, optionally one run after `jitter` (uniform 10 s to `StartupJitterMaxSeconds`) when there is no snapshot, or `now - lastSuccess >= interval`, and `now >= lastStarted + floor`; then `PeriodicTimer(interval, timeProvider)` ticks enqueue a `Scheduled` request, skipped when running, or when `now < lastStarted + MinimumScheduledSpacing` (the floor). Never run on `Restart=always` crash loops: the persisted `lastStartedUtc` bounds start-up runs.
- **Cooldown anchor (planner to confirm, see Open Questions):** recommended = every sync start, whatever the trigger, sets `cooldownEndsUtc = start + 10 min`; only **manual** requests are refused during it. This gives one honest invariant ("at most one sync per window"), costs nothing, and shows the countdown after an hourly run too.
- **State machine for status (`SyncStatus` payload):** `running` = accepted-or-running; `lastSyncedUtc` = last success only; a held-back run leaves `lastSyncedUtc` untouched and sets `heldBack = true`; a changed/unchanged success clears `heldBack`.
- **Tests with `FakeTimeProvider`:** construct coordinator/runner/loop with the fake; `Advance` in **period-sized steps** (a 3 h jump produced three ticks in the scratch run, so one jump is not "one tick"); await outcomes with a small polling helper (`WaitUntil(() => ..., real 5 s timeout)`) because continuations run after `Advance` returns. Keep most logic in `SyncRunner.RunAsync` (no timers) so only two or three tests need the hosted loop.
- **Do not let the scheduled loop run in tests by default** (Pitfall 2).

### 5. Snapshot and sync-state on disk

Location: `Storage:Directory`, else `$STATE_DIRECTORY` (set by systemd for `StateDirectory=cabinet`, so `/var/lib/cabinet` in production [ASSUMED from systemd.exec behaviour; unit file confirms `StateDirectory=cabinet` at line 10 of `deploy/systemd/cabinet.service`]), else in Development `<content root>/.cabinet-state` (add to `.gitignore`), else fail at start-up with a message naming the key (tests always pass a unique temp directory). Files:

```
snapshot.json     {"schemaVersion":1,"capturedAtUtc":"2026-01-01T12:00:00Z","items":[{"collectionId":5000001,"gameId":100001,"title":"Example Game","kind":"base","year":2020,"box":{"width":11.5,"length":15.4,"depth":2.7},"location":"Shelf A"}]}
sync-state.json   {"schemaVersion":1,"lastStartedUtc":"...","lastFinishedUtc":"...","lastSuccessUtc":"...","lastResult":"changed","lastFailure":"none","consecutiveFailures":0,"cooldownEndsUtc":"...","heldBack":{"kind":"shrunk","fingerprint":"...","count":12,"detectedUtc":"..."}}
```
All values synthetic. `box` holds the raw BGG numbers (conversion lives in the pure mapper, so Phase 4 can change it without a resync); `location` only when the spike allows it; `lastResult` in `changed|unchanged|failed|heldBack`; `lastFailure` category from `BggFailure` plus `none`. The visitor payloads never expose the failure category (UI contract: one generic sentence), so nothing about 401/Cloudflare leaks.

- **Read rules:** missing file -> empty; `schemaVersion` greater than supported, malformed JSON, or I/O error -> treat as "no snapshot", log once (category only), rename the bad file to `<name>.bad` (overwrite) so it is inspectable, serve "being filled" and let the next sync rebuild. Evolve additively (unknown fields ignored, missing fields default) and reserve a version bump for breaking changes, so a newer release's file does not blank an older rolled-back release unnecessarily; an older release seeing a newer `schemaVersion` falls back to "no snapshot".
- **Write order on success:** snapshot first, then sync-state (a crash between leaves a new snapshot with an old state: harmless). Never write the snapshot on `Unchanged`.
- **Version hash:** `version = hex(SHA256(canonical text of the mapped CabinetItem list))[0..16]` where the canonical text is `collectionId|gameId|kind|title|widthMm|heightMm|depthMm` per line in collection-id order. Computed from the **mapped items** so a change of mapping rules also changes the ETag. `Unchanged` = same version.
- **Deploy changes:** `deploy/systemd/cabinet.service` already has `StateDirectory=cabinet`, `ProtectSystem=strict`, `PrivateTmp=yes`; no change is required. Optional hardening `StateDirectoryMode=0750` needs a provisioning re-run (units are installed by provisioning; see `docs/lxc-setup.md`), skip unless wanted. `deploy/cabinet.env.example`: add placeholder lines `Bgg__Username=`, `Bgg__Token=`, `Bgg__ContactUrl=` (empty or obvious placeholders like `replace-with-...`; keep gitleaks quiet). Output firewall policy is `accept` [VERIFIED: `chain output ... policy accept` in `deploy/nftables/cabinet.nft.in`], so outbound HTTPS to BGG needs no rule. After adding keys: `sudo systemctl restart cabinet`. Document in a new operations doc and `docs/lxc-setup.md`.

### 6. Status, sync and layout endpoints

| Route | Method | Behaviour |
|-------|--------|-----------|
| `/cabinet/status` | GET | JSON, `Cache-Control: no-store`, no CORS: `{serverTimeUtc, snapshotVersion|null, lastSyncedUtc|null, running, cooldownEndsUtc|null, heldBack, staleAfterSeconds, lastResult|null, lastResultAtUtc|null}`. Same shape is the hub payload. No failure category, no counts, no usernames |
| `/cabinet/sync` | POST | No body, no parameters read. `202 {outcome:"started", ...status}`; `409 {outcome:"running", ...}`; `429 {outcome:"cooldown", ...}` with `Retry-After` seconds. Clients adopt the returned status; refusals are not errors in the UI |
| `/cabinet/layout` | GET | `profile` allowlist as today. `sample` honoured only when the prototype switch is on (non-Production); absent -> real collection. In production `sample` is ignored (never echoed). ETag `"{LayoutVersion}-{fingerprint}-{snapshotVersion|empty}-{profile}"`; `no-cache` + 304 handling stays. No snapshot -> 200 with the empty-collection layout (one section of bare cubbies; exists today as sample `0`) |
| `/cabinet/live` | hub | `HttpTransportType.WebSockets | ServerSentEvents` only (no long polling, which costs a request per client per 90 s); `ApplicationMaxBufferSize = 4096`, `TransportMaxBufferSize = 8192`, hub `MaximumReceiveMessageSize = 1024`, `KeepAliveInterval = 15 s`, `ClientTimeoutInterval = 30 s`, `HandshakeTimeout = 10 s`, `EnableDetailedErrors = false` (defaults and meaning [CITED: learn.microsoft.com/aspnet/core/signalr/configuration]); admission cap `Live:MaxConnections = 100` in the hub plus `Limits.MaxConcurrentUpgradedConnections = 100` on Kestrel (the hub counter also covers SSE connections, which are not upgraded). Phase 8 re-sizes |

- `Program.cs`: `AddSignalR(...)`, `MapHub<CabinetHub>("/cabinet/live", ...)`; no `UseWebSockets()` needed (verified). CSP middleware already wraps every response, including negotiate.
- **Origin check:** browsers send `Origin` on WebSocket upgrades and CORS does not apply to WebSockets [CITED: learn.microsoft.com SignalR security]. The hub is read-only public data, so cross-site hijacking has no impact; defer an `AllowedOrigins` check to the public-exposure phase (needs the real host name), and note it there.
- **Layout JSON additions (additive):** `Placement` gains `long EntryId` (the collection entry id of the drawn item; for `MoreMarker` the base game's entry) and `bool? IsExpansion` (true when the drawn item is an expansion, omitted otherwise; set for `OrphanExpansion`, `ExpansionLayer`, `ExpansionSpine` and for a `Cover` of an expansion). `LayoutJson` already omits nulls. Bump `CabinetLayoutEngine.LayoutVersion` 8 -> 9 and re-record goldens (`CABINET_UPDATE_GOLDENS=1 dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=Layout"`); update `layout-version.txt`, the 400-item hash pins, and the ETag regex in `LayoutEndpointTests`. Renderer: `data-entry-id` from `entryId`; a placement with `isExpansion` and no `baseTitle` gets the sub-line `Expansion` and the name `{title}, expansion`; with `baseTitle` the text is unchanged. Update `namesUnownedBase`/`hasBaseLine`/`accessibleName` in `render.js` and add the two strings to `copy.js`. Verified current renderer: `namesUnownedBase` requires `placement.baseTitle !== undefined`, which is why the flag is needed.
- **Interim box mapping (pure, `BoxFromVersion`):** accept only a fully non-zero triple with front sides within 50-700 mm and depth within 5-300 mm after conversion; `height = max(width, length)`, `width = min(width, length)` converted at the spike-confirmed factor; else the kind's default (base 225 x 300 x 60 mm; expansion 200 x 260 x 40 mm, chosen inside the ranges the existing sample generator uses: height 120-390, depth 20-110, width 70-100 percent of height). The engine already clamps to the design limits and depth to 150 mm. Longer side = height matches the approved sample convention [VERIFIED: `SyntheticCollections.CreateBox` width = height x 70-100 percent]; whether real BGG `length` is the longer side is measured by the spike. Phase 4 replaces this with cover aspect ratios.
- **Orphan expansions this phase:** every expansion has `ExpansionOf = []`, which the engine already treats as an orphan with no base title [VERIFIED: `FamilyIndex.Create`: `named?.Title` is null], so D-15 needs only the flag and renderer text.

### 7. Prototype switch, page and shared layout

- **Prototype switch (D-17):** set `"Prototype": {"Enabled": false}` in the committed `appsettings.json` and create `appsettings.Development.json` with `true` (the configuration test globs `appsettings*.json`, so the new file is covered). Honour the switch only when the environment is not Production (`SampleCatalog.FromConfiguration(configuration, environment)`), so a stray `Prototype__Enabled=true` in the env file cannot reveal the switcher. Existing sample tests must opt in explicitly through the factory settings.
- **Dev default:** unknown/missing `sample` -> synced collection; add `Synced` as the first switcher link; nav `aria-label` becomes `Collection to show` (UI contract). `SampleCatalog.DefaultName` stops being `65`.
- **Shared layout:** add `Pages/_ViewStart.cshtml` (`Layout = "_Layout";`) and `Pages/Shared/_Layout.cshtml` holding `<!DOCTYPE html>`, `<head>` (fingerprinted `site.css`, `cabinet.css`), `@RenderBody()`, the footer (`Version ...` then the credit), and a `Scripts` section. Test pattern change: existing assertion `<footer>\s*<p class="version">...</p>\s*</footer>` must be rewritten for the new footer contents.
- **Credit:** `<a class="bgg-credit" href="https://boardgamegeek.com" rel="noopener"><img src="~/img/<logo-file>" alt="Powered by BGG" width="W" height="H" asp-append-version="true"></a>`. The logo asset is **obtained at execution time from BGG's official "Powered by BGG Logos" page, linked from the XML API Terms of Use page** [CITED via search result titles; page itself unreachable to automation]; owner downloads or approves the fetch; record the source page in the commit. Format is unknown [ASSUMED PNG or SVG]; both are served from `'self'` under the current CSP. A test must assert the asset returns 200 with an image content type and that **every** Razor page route renders the credit (enumerate page endpoints from `EndpointDataSource` so a future page cannot skip the layout).
- **First-paint state without inline script:** `data-*` attributes on the header/mount (`data-snapshot-version`, `data-last-synced`, `data-cooldown-ends`, `data-server-time`, `data-running`, `data-held-back`, `data-stale-after`), plus server-rendered text. The vendored client is a classic `<script src="~/lib/signalr/signalr.min.js">` placed before the module script so `window.signalR` exists when `cabinet.js` runs; `live.js` reads `globalThis.signalR` and, if it is missing or the connection cannot start, silently falls back to status polling.
- **JS modules:** `cabinet.js` (entry, existing load/redraw; the existing `load()` keeps `showLoading()` for first load and profile change, a new `redraw()` skips it and swaps with one `replaceChildren`, restoring focus by `data-entry-id`), `status.js` (pure functions: relative time, countdown, stale decision, state machine), `live.js` (connection and fallback), `copy.js` (strings). Page state moves only through `textContent`, `hidden`, `aria-*`, `data-*`, classes (strict CSP).
- **Connection code shape (doc-comment style only):**
```javascript
/**
 * Starts the live connection and reports every pushed status to the handler.
 * @param {(status: object) => void} onStatus Called with each status payload.
 * @param {() => void} onReconnected Called after a reconnect so the page fetches status once.
 * @returns {Promise<void>}
 */
export async function connect(onStatus, onReconnected) {
  const signalR = globalThis.signalR;
  const delays = [0, 2000, 10000, 30000];
  const connection = new signalR.HubConnectionBuilder()
    .withUrl('/cabinet/live')
    .withAutomaticReconnect({ nextRetryDelayInMilliseconds: (context) => delays[context.previousRetryCount] ?? 60000 })
    .configureLogging(signalR.LogLevel.None)
    .build();

  connection.on('statusChanged', onStatus);
  connection.onreconnected(onReconnected);

  for (let attempt = 0; ; attempt += 1) {
    try {
      await connection.start();
      return;
    } catch {
      await new Promise((resolve) => setTimeout(resolve, delays[attempt] ?? 60000));
    }
  }
}
```
`withAutomaticReconnect` only covers connections that were established; a failed first `start()` is retried by the loop (Pitfall 9). The 5-second own-press status poll while disconnected and the visibility handling follow the UI contract.

### 8. CSP and WebSockets

| Engine | `connect-src`/`default-src 'self'` and same-origin `ws:`/`wss:` | Evidence |
|--------|----------------------------------------------------------------|----------|
| Chrome / Chromium | matches (fixed around 2018) | WebKit bug 201591 discussion records Chromium fixed it earlier and Firefox always worked [CITED: bugs.webkit.org/show_bug.cgi?id=201591] |
| Firefox | matches | same |
| Safari / WebKit | failed through at least Safari 15.3; fixed by WebKit bug 235873 ("CSP: Improve compatibility of source matching"), committed 2 April 2022 [CITED: bugs.webkit.org/show_bug.cgi?id=235873]; first shipping Safari/iOS version not stated in the bug [ASSUMED: iOS/Safari 16 or later; 15.5 possible] |
| Spec | CSP Level 3 "Changes from Level 2": `'self'` now matches `https:` and `wss:` variants of the page's origin, even on pages whose scheme is `http` | [CITED: w3.org/TR/CSP3 section 1.3] |

**Recommendation: keep the policy string exactly as it is** (`default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'`). Explicit `connect-src 'self'` would be behaviour-neutral (it is the fallback anyway) and would force a pinned-policy test change for nothing; `wss:` or a host-specific origin would loosen the policy (and a host needs the real domain, which must not be in the repo). The safety net for an old WebKit is built into the client: if the WebSocket is blocked, SignalR negotiates down to Server-Sent Events (`EventSource`, same-origin, covered by `'self'`), and if everything fails the page polls status. The executor verifies with real browsers using a scratch Playwright install outside the repo (as earlier phases did) in Chromium, Firefox and WebKit, asserting no `securitypolicyviolation` events and no console CSP errors on the page with live updates active; plus one look from the owner's phone browser. Only if a supported engine blocks both the WebSocket and SSE would an explicit same-origin rule be added, and then through configuration, never a wildcard.

**Interaction with existing tests:** `ContentSecurityPolicyTests` pins the exact string, so no change; its request list should gain `/cabinet/status`, `/cabinet/live/negotiate` (POST), `/lib/signalr/signalr.min.js` and the logo asset. `CabinetPageTests` regexes: `InlineScriptBody` tolerates the classic `<script src>`; `ModuleScript` still finds the module; `ScriptWithoutSource` still passes. Add a test that the vendored file served equals the pinned SHA-256 and that exactly one third-party script is referenced.

### 9. Vendoring and the JS lint

Procedure (document in `docs/` without planning references): `npm pack @microsoft/signalr@<version>` in a scratch directory outside the repo; verify the tarball against `npm view @microsoft/signalr@<version> dist.integrity`; extract `package/dist/browser/signalr.min.js` unmodified to `Cabinet.Service/wwwroot/lib/signalr/signalr.min.js`; record version, source, the tarball integrity string and the file SHA-256 in a notice file next to it together with the MIT licence text (the file carries no banner). Add `.gitattributes` with `Cabinet.Service/wwwroot/lib/** -text` so git never rewrites bytes. Add a unit test pinning the file SHA-256 (so an accidental edit or a half-done upgrade fails CI, since Dependabot cannot see a copied file). Update steps: bump the version in the notice, repeat the procedure, update the pinned hash.

Lint: `build/lint/checks/10-repo-rules.sh` collects `js_files` with `git ls-files '*.js' '*.mjs'`; the vendored file fails the `//` rule only because of its last line. Minimal change: exclude the vendored directory from the **JS comment check only** (keep it in the planning-reference check, which it passes), add a self-test case proving a `//` comment under a non-vendored path is still flagged and one under `wwwroot/lib/` is not. Do not edit the vendored bytes to satisfy the lint.

### 10. Fake BGG and test doubles

- **Scripted stub (unit + integration):** `ScriptedHandler : HttpMessageHandler` taking a queue/list of responses (status, content type, body, optional delay on a `TimeProvider`) and recording every request (URI, headers present/absent). Scenarios: 202 then 200; 429 without `Retry-After`; 500/503; 401; 403 with an HTML body; 200 with HTML; 200 with `<errors>`; malformed XML; wrong `totalitems`; empty; shrunken; 301 to `www`. Request recording is what proves SEC-05 (token header only on the BGG host, none after a redirect, one username only).
- **Synthetic XML builder:** `BggXml.Collection(items, totalitems?)` with invented ids/titles in BGG's response shape (element-text names, `status own="1"`, optional `version` with dimensions, optional `privateinfo`). One class, used by tests and by the fake host.
- **Local fake host:** new project `Cabinet.FakeBgg` (`Microsoft.NET.Sdk.Web`), in `Cabinet.slnx` under a `/Tools/` folder, referenced by the test projects (for the XML builder) but **not** by `Cabinet.Service`; `build/package-release.sh` publishes only `Cabinet.Service.csproj` [VERIFIED: publish line 57], so it cannot ship. Routes `/xmlapi2/collection` (honours `own`, `subtype`, `excludesubtype`, `showprivate`, `version`) and `/xmlapi2/thing`; scenario switches by query parameter or startup argument: `queued=N`, `throttle`, `slow=ms`, `broken` (HTML 200), `malformed`, `empty`, `shrunk`, `unauthorized`, plus collection sizes (about 65, 400). It ignores the Authorization header entirely and never logs it. Dev usage documented in `docs/development.md`: run the fake, run the service in Development with `Bgg__BaseUri=http://127.0.0.1:<port>/xmlapi2/` and any dummy `Bgg__Token`/`Bgg__Username` via user-secrets (add `<UserSecretsId>` to `Cabinet.Service.csproj`; user secrets load only in Development).
- **End-to-end test over real HTTP:** one integration test starts the fake on a loopback port, `PostConfigure<BggOptions>` points the real `BggClient` at it, and runs a real sync, which exercises the real handler chain (decompression, no redirects, UA, pacing no-op) that a stubbed handler bypasses.

### 11. Integration-test factory changes (read from `CabinetWebApplicationFactory.cs`)

- The factory's `CreateHost` builds the host **twice** (`testHost` for TestServer, `realHost` on Kestrel) from the same builder and starts both. A `BackgroundService` therefore starts twice, with two loops sharing one `FakeTimeProvider` and one state directory. Fix: default `Sync:BackgroundEnabled=false` for both builds in `ConfigureWebHost`, and let a test opt in for the **real** host only (apply the opt-in setting inside the `builder.ConfigureWebHost(... UseKestrel())` block in `CreateHost`). `SyncCoordinator`/`SyncRunner` stay registered either way so `POST /cabinet/sync` works in every test; the consumer loop is what is conditional.
- Add constructor parameters: extra settings (existing), `Action<IServiceCollection>` for `ConfigureTestServices` (inject `FakeTimeProvider`, stub handler, notifier recorder), and always a unique `Storage:Directory` under the temp path, deleted on dispose.
- Existing tests that depend on sample data (`sample=65`, "Invented collection of 65 items.", switcher) must pass `Prototype:Enabled=true` explicitly; the "prototype off" tests become the default behaviour tests (credit present, "being filled" block, no switcher, `?sample=` ignored).

## Common Pitfalls

### Pitfall 1: Coalescing with `DropWrite`
**What goes wrong:** `TryWrite` returns `true` when the item is dropped, so "already queued" is invisible and a second run can be accepted or the cooldown can be spent without a run.
**How to avoid:** capacity 1, `Wait`, honour `false`; keep an explicit `running` flag under the coordinator lock. **Warning sign:** two syncs back to back in tests with a single press.

### Pitfall 2: Two hosts, two hosted services
**What goes wrong:** the factory starts the app twice; both loops react to the same fake clock and write the same state files, producing double BGG calls and flaky tests.
**How to avoid:** the `Sync:BackgroundEnabled` default-off approach above; unique temp state directory per factory. **Warning sign:** stub request counts that are exactly doubled.

### Pitfall 3: The username leaks through logs
**What goes wrong:** `HttpClientFactory` logs "Sending HTTP request GET https://.../collection?username=..." at Information.
**How to avoid:** committed log level `System.Net.Http.HttpClient: Warning`; never log URIs or exception `ToString()` from the client; sentinel-secret test over captured logs. **Warning sign:** journal lines containing the collection URL.

### Pitfall 4: Provisioning never updates the env file
**What goes wrong:** the first release with sync is deployed before `Bgg__Token`/`Bgg__Username` exist; if start-up validation throws, the health check fails and the installer rolls the release back.
**How to avoid:** missing token or username is **not** a start-up error; log one warning, keep serving, record `NotConfigured` on the first sync attempt. Document the manual env edit and restart. `/health` must stay healthy with no snapshot, no state directory contents and no credentials.

### Pitfall 5: Vendored client versus lint and the `//` rule
**What goes wrong:** `git ls-files '*.js'` picks up the vendored file; its last line fails the JS comment lint; "fixing" it by editing the file breaks the checksum story.
**How to avoid:** path exclusion for the comment check only; byte-identical file; pinned hash test; no `.map`; classic script tag. **Also:** the file has no licence banner; ship the notice.

### Pitfall 6: Cross-device `File.Move`
**What goes wrong:** a temp file outside the state directory makes `rename` fail with `EXDEV` and silently degrade to copy + delete (not atomic).
**How to avoid:** temp file in the same directory; never `/tmp`.

### Pitfall 7: Expansions silently becoming base games or doubles
**What goes wrong:** relying on `subtype` attributes (mislabelled in the default call) or merging the two calls without keying on `collid`.
**How to avoid:** two calls, expansion call wins per `collid`, dedupe by `collid`, own-flag check, `totalitems` check per call. The spike's call D confirms the quirk.

### Pitfall 8: HTTP 200 that is not a collection
**What goes wrong:** Cloudflare challenge pages, `<errors>`, or a truncated body parse as "zero items" and wipe the cabinet.
**How to avoid:** check status, content type, root element, `totalitems` equality, and run the guard; an empty result is held back regardless.

### Pitfall 9: SignalR reconnect semantics
**What goes wrong:** `withAutomaticReconnect` does not retry a failed initial `start()`; a phone that opens the page offline never connects. Also, a server `Abort()` in `OnConnectedAsync` (cap reached) makes the client reconnect forever with the schedule.
**How to avoid:** initial-start retry loop (example above), refusals are handled by the same schedule (60 s steady state is gentle), and the page always has the polling fallback and fetches status on connect, reconnect, visibility and online events.

### Pitfall 10: Clock skew in the countdown
**What goes wrong:** a visitor clock minutes off shows a wrong cooldown.
**How to avoid:** the status payload carries `serverTimeUtc`; the page stores `offset = serverTime - Date.now()` at each response and computes everything through it (as the UI contract specifies).

### Pitfall 11: Layout additions without a version bump
**What goes wrong:** adding `entryId`/`isExpansion` changes every golden and every ETag; forgetting the bump leaves stale caches/ETags that omit the fields.
**How to avoid:** bump `LayoutVersion`, re-record goldens, update `layout-version.txt`, the 400-item pins and the ETag regex in the same change.

### Pitfall 12: Timer tests that jump
**What goes wrong:** `fake.Advance(3h)` delivers several ticks (observed), or none are observed because assertions run before continuations.
**How to avoid:** advance by one period per step and await observable effects with a polling helper.

### Pitfall 13: Unreachable copy
**What goes wrong:** the UI contract lists "Synced yesterday" but its own rule ("under 48 h whole hours rounded down") can never produce it; server and client implementations could diverge on this edge.
**How to avoid:** put the (elapsed seconds -> text) table in one shared fixture used by the C# first-paint formatter test and the JS formatter check; raise the inconsistency with the planner (Open Question 2).

### Pitfall 14: Held-back state keyed by count
**What goes wrong:** comparing counts accepts a different small collection of equal size.
**How to avoid:** fingerprint of sorted entry ids (set equality), as in Pattern 3.

## Code Examples

### Collection XML reader settings and entry points
```csharp
public static class BggXmlReader
{
    private const int MaxCharacters = 20_000_000;

    public static XDocument Load(Stream body)
    {
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = MaxCharacters,
            MaxCharactersFromEntities = 0,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        using var reader = XmlReader.Create(body, settings);

        return XDocument.Load(reader, LoadOptions.None);
    }
}
```
Source: Microsoft XmlReaderSettings guidance (standard hardening); parsing of items is LINQ to XML over `Root.Elements("item")`.

### Pure guard decision type
```csharp
public abstract record GuardDecision
{
    public sealed record Accept : GuardDecision;

    public sealed record HeldBack(HeldBackKind Kind, string Fingerprint, int Count) : GuardDecision;
}

public static class ShrinkGuard
{
    public static GuardDecision Evaluate(int previousCount, IReadOnlyCollection<long> candidateEntryIds, HeldBackRecord? heldBack)
    {
        if (previousCount == 0)
        {
            return new GuardDecision.Accept();
        }

        var fingerprint = Fingerprint(candidateEntryIds);

        if (candidateEntryIds.Count == 0)
        {
            return new GuardDecision.HeldBack(HeldBackKind.Empty, fingerprint, 0);
        }

        if (candidateEntryIds.Count * 2 < previousCount)
        {
            return heldBack is { Kind: HeldBackKind.Shrunk } known && known.Fingerprint == fingerprint
                ? new GuardDecision.Accept()
                : new GuardDecision.HeldBack(HeldBackKind.Shrunk, fingerprint, candidateEntryIds.Count);
        }

        return new GuardDecision.Accept();
    }
}
```

### FakeTimeProvider-driven test shape
```csharp
[Fact]
[Trait("Category", "Sync")]
public async Task The_scheduled_tick_starts_one_run_and_manual_requests_wait_for_the_cooldown()
{
    var time = new FakeTimeProvider(new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero));
    var harness = SyncHarness.Create(time, ScriptedHandler.Collection(Items(3)));

    harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().Be(SyncRequestResult.Started);
    await harness.WaitUntilIdleAsync();
    harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().BeOfType<SyncRequestResult.CoolingDown>();

    time.Advance(TimeSpan.FromMinutes(10));

    harness.Coordinator.TryRequest(SyncTrigger.Manual).Should().Be(SyncRequestResult.Started);
}
```

### Box mapping (pure)
```csharp
public static BoxDimensions FromVersion(double widthIn, double lengthIn, double depthIn, ItemKind kind, double millimetresPerUnit)
{
    var front = new[] { widthIn, lengthIn }.Select(value => value * millimetresPerUnit).Order().ToArray();
    var depth = depthIn * millimetresPerUnit;
    var plausible = front[0] >= 50 && front[1] <= 700 && depth >= 5 && depth <= 300;

    return plausible
        ? new BoxDimensions((int)Math.Round(front[0]), (int)Math.Round(front[1]), (int)Math.Round(depth))
        : Default(kind);
}
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Unauthenticated BGG XML API | Registered application, Bearer token, Cloudflare in front | 2025-07-02 (usage page version date) | Token on every call; 403 can be an edge challenge, not a token error |
| `Task.Delay` + `DateTime.UtcNow` | `TimeProvider` overloads for `PeriodicTimer`, `Task.Delay`, timers | .NET 8 | Deterministic tests with `FakeTimeProvider` |
| Safari ignoring `'self'` for WebSocket | Fixed in WebKit (commit 2 April 2022) | 2022 | A plain `'self'` policy is enough in current engines |
| SignalR needing `UseWebSockets()` | Hub endpoints handle WebSockets themselves (verified on 10.0.112) | long-standing | No extra middleware |
| Hand-written SSE/polling for live status | SignalR with automatic transport fallback | n/a | Owner's explicit choice |

**Deprecated/outdated:** `app.UseSignalR`, `Microsoft.AspNetCore.SignalR` (old) client package names; the 202-every-500-ms default retry of `BoardGamer.BoardGameGeek` (not used).

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | `python3` exists on the Ubuntu 24.04 LXC (inferred from `unattended-upgrades` in the provisioned list) | Spike design, Alternatives | Spike cannot run as written; fall back to a `bgg-spike` subcommand in a release |
| A2 | BGG version dimensions are inches; zero means missing | Findings 2, 6 | Wrong box sizes (engine clamps, so cosmetic); spike prints magnitudes to settle it |
| A3 | BGG `length` is the longer front side so `height = max(width, length)` fits real boxes | Box mapping | Boxes drawn rotated; cosmetic and interim (Phase 4 replaces with cover aspect) |
| A4 | `<privateinfo>` attribute names (`inventorylocation` etc.) | Findings 2 | Parser looks for the wrong name; spike prints the real names before any parser is written |
| A5 | First Safari/iOS version that carries the April 2022 CSP fix is 15.5 or 16 | CSP section | An old iPhone could block WebSockets; SSE/polling fallbacks cover it |
| A6 | BGG does not reject requests without a User-Agent (or does) | Client behaviour | Spike calls F and G measure it; we send an honest UA either way |
| A7 | The official logo is PNG or SVG, usable on a light plate | Credit | Plate rule in the UI contract decides; asset-dependent |
| A8 | `$STATE_DIRECTORY` is set by systemd for `StateDirectory=cabinet` | Storage | Fallback to `Storage:Directory` set in the env file; start-up message names the key |
| A9 | Traefik passes WebSocket upgrades without extra config (the example config has no buffering middleware) | Live updates | Hub falls back to SSE then polling; verify once through the real route |
| A10 | BGG occasionally emits characters that fail strict XML character checks | XML hardening | `CheckCharacters = false` plus sanitising is harmless if untrue |
| A11 | Cooldown anchored on every sync start (not only manual) is acceptable to the owner | Orchestration | Alternative is a manual-only anchor; either is a small change in `TryRequest` |
| A12 | The first deployed release will precede the owner adding the BGG keys | Pitfall 4 | None if start-up tolerates it; a throw would trigger rollback |

## Open Questions

1. **Cooldown anchor.** Should an hourly (scheduled) sync also open the 10-minute window?
   - Known: D-05 speaks of manual syncs; the success criterion says "one sync per cooldown window globally".
   - Unclear: whether the owner wants the countdown visible after an hourly run.
   - Recommendation: anchor on every sync start; refuse only manual requests inside the window (A11). One-line change if the owner prefers manual-only.
2. **"Synced yesterday" copy.** The relative-time rule (hours up to 47, then days) cannot produce it, but the copy table lists it. Ask the planner/UI owner to either change the rule (day granularity from 24 h with `numeric: 'auto'`) or drop the copy; the shared fixture must encode the decision.
3. **Automated tests for the JS logic.** The repo has no JS runner. Option: a dependency-free `node --test` file for the pure `status.js` functions (relative time, countdown, stale decision) run in CI (GitHub runners ship Node), or keep to server-side contract tests plus scratch-Playwright screenshot rounds as in earlier phases. Recommendation: commit the small `node --test` file only if the owner accepts a Node binary (not a toolchain) in CI; otherwise keep the shared (seconds -> text) fixture and verify JS in the screenshot rounds.
4. **Optional `StateDirectoryMode=0750`.** Harmless hardening for private location values; requires re-running provisioning on the server. Defer unless the owner wants it.
5. **Spike failure modes.** If call B returns 401/403 only because `showprivate` demands a website login, the outcome is "not readable" (plan B per D-04). If the token itself is rejected (A also 401), the whole phase waits on the owner; the spike script must report this distinctly from "private info absent".
6. **Empty collection escape hatch.** D-11 means a genuinely emptied BGG collection never reaches the page automatically. Confirm the documented manual step (delete `snapshot.json`, restart) is acceptable.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests | yes (dev machine) | 10.0.112 | none needed |
| ASP.NET Core runtime on the LXC | run | assumed yes (provisioning installs `aspnetcore-runtime-10.0`, per `deploy/versions.env`) | 10.0.x | none |
| Node + npm | one-off vendoring download, scratch Playwright | yes on dev machine (node v24.19.0) | 24.19.0 | `curl` the tarball from the registry URL |
| python3 on dev machine | local spike-script self-test with the fake | yes | 3.14.4 | none needed |
| python3 on the LXC | spike | unverified (not probed; inferred) | — | `bgg-spike` subcommand in a release |
| curl, jq on the LXC | ops | yes (provisioned list) | — | — |
| Docker | `build/lint.sh` (shellcheck, gitleaks etc. run in containers) | yes | — | — |
| Outbound HTTPS from the LXC to `boardgamegeek.com` | sync, spike | firewall output policy is accept; DNS/egress unverified | — | none; Cloudflare behaviour on the home egress address is one of the spike's findings |
| BGG token + username in `/etc/cabinet/cabinet.env` | sync, spike | owner action | — | app runs without them ("being filled" plus `NotConfigured`) |
| Official "Powered by BGG" logo file | credit | owner download or approved fetch | — | none; do not use a third-party copy |
| Real browsers (Chromium, Firefox, WebKit) | CSP/WebSocket verification | scratch Playwright (earlier phases used 1.63.0 outside the repo); downloads need owner approval | — | owner checks Safari on a phone |

**Missing with no fallback:** BGG token in the env file, the official logo asset (both owner actions, both gated by approval at execution time).
**Missing with fallback:** python3 on the LXC (probe first).

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.1 on Microsoft.Testing.Platform, FluentAssertions 8.11.0, Mvc.Testing 10.0.12; add `FakeTimeProvider` (10.10.0) and, for integration, `Microsoft.AspNetCore.SignalR.Client` 10.0.12 |
| Config file | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`), `Directory.Build.props` |
| Quick run command | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Sync"` (also `Bgg`, `Snapshot`, `Layout`, `Configuration`) |
| Full suite command | `dotnet test --solution Cabinet.slnx --no-restore` then `build/lint.sh` |
| Estimated runtime | about 30-60 s unit, about 1-2 min integration (real Kestrel hosts per test) |

Notes: trait filters make `dotnet test --solution` exit 8 when a project has no match; filter per project with `--project`. Re-recording goldens: `CABINET_UPDATE_GOLDENS=1 ...` (local only).

### Phase Requirements -> Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SYNC-01 | Start-up run after jitter only when snapshot missing/old; hourly tick enqueues one run; interval floor; two collection calls with the expected query strings; at least 5 s between every request (fake-clock timestamps) | unit (`FakeTimeProvider`) + stub handler | quick command, `Category=Sync` | ❌ Wave 0 |
| SYNC-01 | End-to-end sync of a synthetic collection (base + expansion, duplicate collid, non-owned item filtered) through the real handler chain against the fake host | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj --no-restore` | ❌ Wave 0 |
| SYNC-02 | One manual sync per window, persisted across a restart (new coordinator over the same state files), accepted press while running -> `running`, cooldown refusal -> 429 with `Retry-After`, failed manual sync still consumes the window; status shows `cooldownEndsUtc` | unit + integration | quick + integration commands | ❌ Wave 0 |
| SYNC-03 | `lastSyncedUtc` only moves on success (not on failure/held-back); first-paint `<time datetime>` and relative text match the shared case table | unit + integration | `Category=Sync` | ❌ Wave 0 |
| SYNC-04 | Each failure (401, 403 HTML, 429, 5xx, timeout, HTML-200, `<errors>`, malformed, wrong `totalitems`, 202 forever) leaves snapshot, in-memory state and layout ETag unchanged and records a category; empty is held back (even twice); halved is held back then accepted on the same set; small removal accepted; stale rule at 3 h and while held back; single failure under 3 h shows no note | unit (pure guard + runner) + integration (served layout unchanged) | `Category=Sync`, `Category=Snapshot` | ❌ Wave 0 |
| SYNC-05 | Fresh host (no snapshot): `/` is 200 with the "being filled" block, `/cabinet/layout` is 200 with one bare section, `/health` healthy; after the first successful sync the block is gone from the server-rendered page; zero-game first sync shows no block | integration | integration command | ❌ Wave 0 |
| SYNC-08 | Every Razor page route renders `a.bgg-credit` linking `https://boardgamegeek.com` with the logo `img`; logo asset served with an image content type from the own origin; footer still shows the version; no third-party origin in the page | integration | integration command | ❌ Wave 0 |
| LOC-02 | Parser reads `privateinfo`/`inventorylocation` from synthetic XML when present, yields `null` when absent; `showprivate=1` is on both calls only when the setting says so; outcome document exists with required headings and passes the repo lint (shape only, no forbidden content) | unit + lint | `Category=Bgg`; `build/lint.sh repo-rules` | ❌ Wave 0 (outcome doc: manual sign-off) |
| SEC-05 | Sentinel token and username never appear in any public response body or header, in captured log output, or in the status/hub payloads; token header only on host `boardgamegeek.com`, absent after a 301 and never followed; request username equals the configured one regardless of query/body sent to `/cabinet/sync`; visitor GETs cause zero BGG calls; repeated POSTs inside the window cause one run; configuration test still passes; `Bgg:BaseUri` ignored outside Development | unit + integration | `Category=Bgg`, integration command, `Category=Configuration` | ❌ Wave 0 (config test exists) |
| D-09 | Hub accepts a client, delivers `statusChanged` on sync start/finish, rejects every invocation (`Method does not exist`), enforces the connection cap, allows only WebSockets/SSE transports; vendored client file equals pinned SHA-256 and is served as JavaScript; CSP header unchanged | integration (SignalR.Client) + unit | integration command | ❌ Wave 0 |
| D-15/D-19 | Layout JSON carries `entryId` on every placement and `isExpansion` for expansion placements; two copies of one game yield two placements with distinct `entryId`; engine + goldens at the new layout version | unit (goldens) + integration | `Category=Layout` | partial (goldens exist; re-record) |
| D-17 | Production-environment catalog ignores `Prototype:Enabled=true`; default config has the switcher off; Development file turns it on; `?sample=` never echoed | unit + integration | `Category=Configuration`, integration | partial |
| cross-cutting | No `style=` attributes or inline script bodies on any page; JS comment lint passes with the vendored exclusion; planning-reference lint passes | integration + lint | integration command, `build/lint.sh` | exists, extend |
| Browser (manual/scratch) | Live redraw, countdown, button states, no CSP violation in Chromium/Firefox/WebKit, credit legible at 320/390/1440 px, "being filled" state against the fake | scratch Playwright rounds + owner review | not in CI (outside repo, as in earlier phases) | ❌ manual |

### Sampling Rate
- **Per task commit:** the matching quick command (`--filter-trait` category) plus `build/lint.sh repo-rules` when strings, comments or JS changed.
- **Per wave merge:** `dotnet test --solution Cabinet.slnx --no-restore` and `build/lint.sh`.
- **Phase gate:** full suite and lint green, CI green on the pull request, spike outcome signed off by the owner, browser rounds reviewed, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] `Cabinet.UnitTests`: add `Microsoft.Extensions.TimeProvider.Testing`; new test files under `Bgg/`, `Sync/`, `Snapshot/`, `Collection/` (guard, cooldown, mapper, box mapping, relative-time cases fixture)
- [ ] `Cabinet.IntegrationTests`: add `TimeProvider.Testing` and `SignalR.Client`; extend the factory (Pitfall 2 changes, service-injection overload, unique state directory)
- [ ] Shared test support: `ScriptedHandler`, `BggXml` synthetic builder, `SyncHarness`, `WaitUntil`
- [ ] `Cabinet.FakeBgg` project + `Cabinet.slnx` entry + committed `packages.lock.json`
- [ ] Update `packages.lock.json` for every project whose references change (locked-mode restore in the release script)
- [ ] Re-record layout goldens and bump the layout version
- [ ] Spike script and (manually) the signed-off outcome file

## Security Domain

`security_enforcement` is enabled (absent = enabled), ASVS level 2 per `.planning/config.json`.

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V2 Authentication | no (public read-only site; no accounts) | n/a; the BGG token is an outbound credential, handled under V6/V14 |
| V3 Session Management | no | no cookies, no sessions |
| V4 Access Control | yes | Hub exposes no invokable methods (test); sync trigger is cooldown-guarded; `/health` loopback-only listener; owner-only features are Phase 6 |
| V5 Input Validation | yes | BGG XML is untrusted: hardened `XmlReader`, size caps, content-type/root checks, `totalitems` check, sanitise strings, `textContent` only on the page; `sample` and `profile` allowlists; `/cabinet/sync` and `/cabinet/status` accept no parameters that reach BGG |
| V6 Cryptography | yes | TLS to BGG via the platform; no custom crypto; SHA-256 only for version and fingerprint hashing; token compared nowhere |
| V7 Error handling and logging | yes | log failure **categories** only; HttpClient URL logging set to Warning; no exception text with URIs; generic visitor failure sentence |
| V8 Data protection | yes | token only in env file (640 root:cabinet) / user-secrets; snapshot may hold private location names under `/var/lib/cabinet` (not exposed by any endpoint in this phase); layout JSON carries no location |
| V9 Communications | yes | token only over HTTPS to `boardgamegeek.com`; redirects not followed; handler strips the header for any other host |
| V12 Files | yes | storage path from configuration only, never from requests; temp file in the target directory; stray temp cleanup |
| V13 API | yes | same-origin JSON, no CORS headers, no bulk dump; undocumented routes |
| V14 Configuration | yes | CSP unchanged and tested; no secrets in committed appsettings (existing test); fake BGG not shippable; base-URI override Development-only |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Token disclosure via redirect, log, error page, response or UA | Information disclosure | Host-pinned auth handler, `AllowAutoRedirect=false`, log filters, sentinel-secret tests, no secret in any payload |
| Username supplied by a visitor reaching BGG (SSRF-like parameter injection) | Tampering | No endpoint reads a username or URL; BGG base URI constant outside Development |
| Sync-now spam amplifying traffic to BGG | Denial of service | Global persisted cooldown, single-flight, interval floor, pacer; per-IP limits are Phase 8 |
| Hub connection flood on a low-power host | Denial of service | Connection cap, WebSockets/SSE only, small buffers and message sizes, short handshake timeout; Phase 8 re-sizes |
| Cross-site WebSocket to the hub | Spoofing/Info disclosure | Hub is read-only public data; origin allow-list deferred to public exposure (needs the real host) |
| Poisoned or truncated BGG answer replacing the snapshot | Tampering | Content checks, `totalitems`, pure guard, held-back state, atomic write |
| XML entity/DTD attacks, billion laughs | Tampering/DoS | DTD prohibited, no resolver, entity expansion 0, character cap |
| Stored text rendered as markup (XSS) | Tampering | `textContent` only; CSP forbids inline script/style |
| Supply chain via the copied client script | Tampering | Official package only, byte-identical copy, pinned SHA-256 test, recorded integrity string |
| Corrupt or hostile state files | Tampering | Parse failure -> "no snapshot", file renamed, never executed or trusted for paths |
| Rollback to an older release reading a newer file | Availability | Additive schema, version check, "no snapshot" fallback instead of a crash |

## Sources

### Primary (HIGH confidence)
- Repository files read this session: `Cabinet.Service/Program.cs`, `Hosting/ContentSecurityPolicy.cs`, `Layout/*.cs`, `Prototype/SampleCatalog.cs`, `Pages/Index.cshtml(.cs)`, `wwwroot/js/*.js`, `appsettings*.json`, `Cabinet.Domain/Layout/*.cs`, `Cabinet.IntegrationTests/**`, `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs`, `build/lint/checks/10-repo-rules.sh`, `deploy/systemd/cabinet.service`, `deploy/provision.d/20-accounts.sh`, `deploy/nftables/cabinet.nft.in`, `deploy/cabinet.env.example`, `build/package-release.sh`, `.github/workflows/ci.yml`, `.github/dependabot.yml`
- Scratch runs on .NET SDK 10.0.112 (this session): channel `DropWrite` vs `Wait`; `PeriodicTimer`/`Task.Delay` with `FakeTimeProvider` 10.10.0; SignalR hub without `UseWebSockets`, WebSockets-only client, no invokable methods (SignalR.Client 10.0.0)
- dotnet/runtime `FileSystem.Unix.cs` `MoveFile(..., overwrite)` -> `rename(2)`, `EXDEV` fallback (raw GitHub, read this session)
- npm registry: `@microsoft/signalr` 10.0.11 metadata and tarball (inspected, hashed); `gsd-tools query package-legitimacy check` -> OK
- NuGet flat container: `Microsoft.Extensions.TimeProvider.Testing` 10.10.0, `Microsoft.AspNetCore.SignalR.Client` 10.0.12 nuspecs
- Microsoft Learn, SignalR configuration and security (aspnetcore-10.0 monikers): option defaults, buffer limits, WebSocket origin restriction, URL logging note

### Secondary (MEDIUM confidence)
- WebKit bugs 201591 and 235873 (CSP `'self'` vs WebSockets; fix committed 2 April 2022); W3C CSP3 section 1.3 summary
- `boardgamegeek2` 1.0.1 (PyPI sdist): `loaders/collection.py`, `utils.py` (collection and version element shapes, 202/503 handling)
- Project-level research (`STACK.md`, `ARCHITECTURE.md`, `PITFALLS.md`) and `.claude/CLAUDE.md` BGG rules summary (themselves derived from archive captures of official pages)

### Tertiary (LOW confidence)
- Third-party notes on inches and zero-as-missing dimensions (kestrelsnest bggpipe repository notes); search-result snippets for the 202 message and the "Powered by BGG Logos" page; official BGG pages were unreachable (403) so nothing here was read from them directly

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH. Everything is shared-framework or Microsoft-published; versions confirmed against registries.
- Architecture: HIGH for sync/storage/hub mechanics (two key behaviours run, not recalled); MEDIUM for the cooldown anchor choice (owner preference).
- BGG specifics: MEDIUM for shapes (third-party parser source), LOW until the spike for private info, units, User-Agent and 202/429 timings.
- Pitfalls: HIGH where tied to existing code or verified runs; MEDIUM for browser-engine behaviour (verify in real engines).

**Research date:** 2026-10-06
**Valid until:** about 2026-11-05 for framework facts; BGG behaviour is re-measured by the spike and should be re-checked if more than a month passes before it runs.

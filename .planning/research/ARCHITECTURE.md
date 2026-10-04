# Architecture Research

**Domain:** Public, read-only .NET 10 web app that mirrors a BoardGameGeek "owned" collection as a dynamically drawn visual cabinet, self-hosted in a Proxmox LXC behind Traefik
**Researched:** 2026-10-03
**Confidence:** MEDIUM-HIGH overall. Deployment and layering are HIGH (read directly from the reference project). BGG access rules are MEDIUM: BGG's own pages return 403 to automated fetchers (Cloudflare), so findings come from search-result excerpts of the official pages plus third-party projects quoting them. Layout engine design is MEDIUM (own reasoning, to be validated by prototype).

## Headline Decisions

| Question | Decision |
|----------|----------|
| Database? | **No database.** One atomically written JSON snapshot plus an on-disk image cache, both derived data that can be rebuilt from BGG. No EF Core, no migrations, no backups. |
| Storage location source | **Plan B (a): convention in the public collection comment** (`Location: <name>`), parsed server-side. Run a 10-minute spike with the application token to see whether `showprivate=1` works with a token; design assumes it does not. Do **not** store the owner's BGG password or session cookie. |
| Layout engine | **Server-side C# in the Domain project**, pure and deterministic, emitting JSON in abstract units for 3 width profiles x 2 grouping modes. Client only draws and filters. |
| Determinism | Online, prefix-stable packing ordered by BGG collection-item id, stable hash (never `string.GetHashCode`), integer units. Adding a game appends; existing placements do not move. |
| Frontend | Static HTML/CSS/ES modules served by the same Kestrel host. No SPA framework, no Node toolchain in CI. (Final frontend choice belongs to STACK.md; the architecture only requires "server emits layout JSON, client draws".) |
| Images | Download at sync time, resize to WebP, serve from own origin with immutable cache headers. No hotlinking. Enables a strict CSP with no third-party origins. |
| Deployment | Copy the reference model, delete everything database/Grafana/Prometheus/backup. Public Traefik route with rate limits and a CSP; health/ops stays on a loopback-only listener. |
| Blocking lead time | **BGG application registration is manually approved and can take a week or more.** Start it on day one; build the layout engine and release pipeline against synthetic fixtures while waiting. |

## Standard Architecture

### System Overview

```
                      Internet
                         |
                 [ Traefik LXC (existing) ]   TLS, rate limit, security headers, CSP
                         |  http, only this source is allowed by the LXC firewall
┌────────────────────────┼─────────────────────────────────────────────────┐
│  cabinet LXC (new, unprivileged Ubuntu)                                    │
│                        v                                                   │
│   public listener :5080                      ops listener 127.0.0.1:5081   │
│  ┌───────────────────────────────────────┐   ┌─────────────────────────┐  │
│  │ Cabinet.Service (ASP.NET Core, Kestrel)│   │ /health (version, sync  │  │
│  │                                       │   │ age) used by installer  │  │
│  │  Static shell + JS renderer           │   └─────────────────────────┘  │
│  │  GET /api/catalogue   (game metadata) │                                 │
│  │  GET /api/cabinet?profile=&mode=      │                                 │
│  │  GET /images/{hash}-{w}.webp          │                                 │
│  │  POST /api/sync       (global cooldown)                                 │
│  │                                       │                                 │
│  │  SyncHostedService (hourly + manual)  │                                 │
│  └───────┬───────────────────┬───────────┘                                 │
│          │ reads (in-memory) │ writes (atomic)                             │
│   ┌──────v───────────────────v──────┐      ┌─────────────────────────┐    │
│   │ ISnapshotProvider (current ref) │      │ /var/lib/cabinet/       │    │
│   │ + layout cache (hash-keyed)     │<─────│  snapshot.json (+prev)  │    │
│   └─────────────────────────────────┘      │  sync-state.json        │    │
│                                            │  images/                │    │
│                                            └─────────────────────────┘    │
│   systemd: cabinet.service, cabinet-deploy-poll.timer (root installer)    │
└────────────────────────────────────────────┬──────────────────────────────┘
                                             │ outbound HTTPS, >= 5 s apart, Bearer token
                                             v
                                   BGG XML API2 (collection, thing)
                                   + BGG image CDN (image download only)

Build side (no code from here ever runs on the server except the verified archive):
  GitHub Actions on tag -> build, test, publish, attest -> draft release -> owner approves
  -> published -> timer on LXC polls -> installer verifies attestation offline -> swaps symlink -> health check
```

### Component Responsibilities

| Component | Project | Responsibility | Talks to |
|-----------|---------|----------------|----------|
| Domain model | Cabinet.Domain | `Game`, `ExpansionLink`, `Snapshot`, `SyncState`, interfaces (`IBggClient`, `ISnapshotStore`, `IImageStore`) | nothing (leaf) |
| Layout engine | Cabinet.Domain | Pure function: games + options -> `CabinetLayout`. No I/O, no clock, no randomness except seeded PRNG | Domain model only |
| Location parser | Cabinet.Domain | Comment text -> location name (tolerant, case-insensitive grouping) | Domain model only |
| Sync planning | Cabinet.Domain | Diff old snapshot vs fresh collection (added, removed, changed), decide which games need `thing` refresh, "suspect shrink" guard | Domain model only |
| BGG client | Cabinet.Repository (`Bgg/`) | HTTP, bearer token, pacing, 202 retry, 429/5xx/403 classification, XML parsing into Domain types | BGG (outbound) |
| Snapshot store | Cabinet.Repository (`Snapshots/`) | Atomic JSON write (temp + rename), keep previous, schema version check, load on boot | filesystem |
| Image store | Cabinet.Repository (`Images/`) | Download, decode, resize to WebP, aspect ratio, dominant colour, content-hash filenames | BGG image CDN, filesystem |
| Sync orchestrator | Cabinet.Service (`Sync/`) | Single-flight sync run: collection -> diff -> thing batches -> images -> assemble -> commit snapshot; cooldown; state file | BGG client, stores, Domain planning |
| Snapshot provider + layout cache | Cabinet.Service | Holds the current immutable snapshot; memoises layouts by (snapshot hash, layout version, profile, mode) | Domain engine |
| HTTP surface | Cabinet.Service (`Endpoints/`) | Minimal API: catalogue, cabinet layout, sync trigger, status; ETag handling | provider, orchestrator |
| Frontend | Cabinet.Service (`wwwroot/`) | Fetch layout + catalogue, draw DOM, pull-out animation, detail card, filters (dimming), toggle, resize profile switch | HTTP surface |
| Ops listener | Cabinet.Service (loopback port) | `/health` for the installer; never routed by Traefik | installer |
| Installer + provisioning | `deploy/` | Poll, verify, install, activate, health-check, rollback; one-time LXC setup | GitHub (read-only, anonymous) |

## 1. BGG Sync Pipeline

### Verified access rules (BGG XML API2, current as of this research)

| Fact | Detail | Confidence |
|------|--------|------------|
| Registration and token required | Since 2025-07-02 the XML APIs require a registered application and `Authorization: Bearer <token>`. Unauthenticated calls get 401. Register at `https://boardgamegeek.com/applications`, then create a token under that application. | HIGH (multiple independent implementers quote the official announcement) |
| Approval is manual | Applications are approved by hand; reports say a week or more. Non-commercial licences are free. | MEDIUM |
| Non-commercial licence scope | "Strictly non-commercial": no ads, no charging, no donations if you want to stay non-commercial. This site qualifies. | MEDIUM |
| Mandatory attribution | Credit BGG by name and show the "Powered by BGG" logo, linked to BGG, on public-facing uses. | MEDIUM-HIGH |
| Caching and server-side only | Requests must come from servers with results cached; direct-from-client calls risk licence suspension. | MEDIUM-HIGH |
| Prohibited | AI/LLM training, modifying the data, relaying BGG data to other applications, using private site APIs. | MEDIUM |
| Rate guidance | No published numeric limit. The API2 wiki suggests about one request every 5 seconds. Heavy rate limiting for unregistered collection reads. 429 and 500/503 mean back off. | MEDIUM |
| Hostname | Use `https://boardgamegeek.com/xmlapi2` (BGG asks that `www.` not be used). | MEDIUM |
| Cloudflare | Non-browser clients sometimes get 403 challenge pages; treat 403 as "BGG not usable right now", not as a token error. Send a descriptive `User-Agent`. | MEDIUM |

Architectural consequence: the application token is a secret (env file only, never in repo or logs), the footer needs the logo and credit from day one, and the sync must be the only thing that talks to BGG (visitors never trigger a BGG call; "sync now" only enqueues the same single-flight job).

### Collection endpoint

Use two calls per sync, both with the owner's username from configuration:

1. Games: `GET /xmlapi2/collection?username={u}&own=1&excludesubtype=boardgameexpansion&comment=1&version=1`
2. Expansions: `GET /xmlapi2/collection?username={u}&own=1&subtype=boardgameexpansion&comment=1`

Why two calls: the default subtype (`boardgame`) returns expansions too, and mislabels them `subtype="boardgame"` (known long-standing BGG quirk, with `excludesubtype=boardgameexpansion` as the standard workaround). Do not trust the collection's subtype attribute at all for classification; the `thing` response's `type` attribute (`boardgame` vs `boardgameexpansion`) is authoritative, and the second call is used to make sure owned expansions are not missed.

Notes:
- `own=1` is the owned filter. Never include wishlist/prevowned/preordered flags. Also check the per-item `<status own="1">` when parsing, since the API has returned stray items before.
- Do not use `modifiedsince`. The collection list is cheap (1-2 calls) and a full list is the only way to detect removals. Incremental refresh applies to the expensive `thing` enrichment, not to the collection.
- `comment=1` returns the collection comment, which is public. This is the location convention carrier (section 2).
- `version=1` asks BGG to include the version the owner selected for each item; where present this carries width/length/depth (see below). Behaviour is reportedly inconsistent (some items return no version), so treat it as best-effort and confirm against the real collection in a spike.
- Capture `collid` per item. It increases with time added and is the stable ordering key for the layout engine.

### The 202 retry dance

BGG answers 202 ("request accepted, queued") with an empty body while it prepares a collection. Handling:

```
call -> 200: parse
     -> 202: wait, retry. Backoff 5 s, 10 s, 20 s, 30 s, 30 s ... cap total about 3 minutes, then fail this run
     -> 429: honour Retry-After if present else backoff 60 s; counts against the same cap
     -> 401: configuration error (token missing/rejected). Stop, log once, surface in /health, do NOT retry hot
     -> 403: treat as BGG unavailable (Cloudflare), transient
     -> 5xx: transient, same backoff
```

Rules: never treat a non-200 as "empty collection" (a real-world bug in other clients); a global minimum spacing of 5 seconds between any two BGG requests, enforced in one place (the BGG client, via a single `SemaphoreSlim` plus `Task.Delay`), not by callers. Use `Microsoft.Extensions.Http.Resilience` only for transport errors; the 202 loop is domain logic and belongs in the client explicitly so it is testable with a fake `HttpMessageHandler`.

### Enrichment with `thing`

- `GET /xmlapi2/thing?id={id1,id2,...}&stats=1` in batches of **at most 20 ids** (BGG enforces a hard 20-item cap since mid-2024; larger requests fail with "Too many items"). A few hundred games means roughly 15 sequential requests, about 75-100 seconds at 5 s spacing. That is fine for an hourly job.
- Per item you get: `name` (primary), `yearpublished`, `image`, `thumbnail`, `minplayers`, `maxplayers`, `playingtime`/`minplaytime`/`maxplaytime`, the `suggested_numplayers` poll (Best / Recommended / Not Recommended votes per player count, including "N+" buckets), `statistics/ratings/averageweight` (weight), and `link` elements.
- **Expansion mapping.** On an expansion item, `link type="boardgameexpansion"` entries with `inbound="true"` point at the base game(s) it expands; on a base game, the same link type without `inbound` lists its expansions. For each owned expansion, take inbound links, intersect with owned base game ids, pick deterministically (lowest id) if several. No owned base: mark "orphan expansion" and let the layout engine place it as a standalone sideways spine in the same location group.
- **Best player count.** Compute from the poll in the Domain layer: `best` = counts whose Best votes are the maximum; `recommended` = counts where (Best + Recommended) exceeds Not Recommended. Store both arrays; the filter UI uses `recommended` for "plays well with N" and shows `best` on the card.
- **Box dimensions.** Versions carry `width`, `length`, `depth` (BGG UI uses inches; weight in pounds; zero means unknown; data is user-contributed and sparse). Fallback chain, evaluated in the Domain assembler:
  1. Dimensions from the owner's selected version in the collection response (`version=1`), if non-zero.
  2. Otherwise a second pass `thing?id=...&versions=1` for only the games still lacking dimensions, one id per request at most a few at a time (the versions payload can be very large for popular games). Pick the version matching year/name of the primary edition; accept only fully non-zero triples. Cache the result per game indefinitely; never refetch unless the game is re-added.
  3. Otherwise estimate from the cached image aspect ratio (width : height from the front image), assume a standard longest edge of about 30 cm, and pick depth from a size-class default modulated by weight (heavier games tend to have deeper boxes). Record `dimensionsSource = version | estimated` in the snapshot so the UI and tests can tell.
  Treat all of this as MEDIUM confidence until verified against real responses; the unit conversion and zero-handling must be covered by fixtures built from real (then anonymised/synthetic) XML.
- Weight (`averageweight`) and polls drift slowly: refresh a game's `thing` data at most every 14 days, or immediately when the game is new. Removing a game deletes its entry and (eventually) its images.

### Incremental vs full refresh

| Step | Frequency | Cost |
|------|-----------|------|
| Collection (2 calls) | every run (hourly + manual) | ~2 requests plus 202 waits |
| `thing` for added games | every run, only new ids | 1 request per 20 new games |
| `thing` refresh of existing games | when older than 14 days, spread across runs (at most N batches per run) | bounded |
| Versions pass | only for games without usable dimensions | rare |
| Image download | only when image URL is new or changed | N images once |

Added/removed detection is a set diff on BGG ids between the stored snapshot and the fresh collection. "Changed" means the collection item's comment, `collid` or status changed (location edits show up as comment changes). A run with no differences does not rewrite the snapshot or bump its ETag.

### Last-good snapshot and failure behaviour

- The site always serves the current in-memory snapshot, loaded from `snapshot.json` at boot. BGG being down, rate limiting, or the token being rejected never affects page loads.
- **Never wipe on a suspicious result.** A transient BGG glitch can return an empty or truncated collection with HTTP 200. Guard: if the fresh collection is empty or shrinks by more than 50 percent versus the snapshot (and the snapshot had more than a handful of games), do not commit; record "suspect shrink", and commit only if the next run agrees. Small real removals pass immediately.
- **Partial enrichment is committed progressively.** A game with failed `thing` or image still appears (as a spine from the collection data) with `detailsPending = true`; the next run retries. Image failure never fails a sync.
- Sync state (`sync-state.json`: last attempt, last success, last error category without secrets, consecutive failures, last manual trigger timestamp) is written separately from the snapshot so a failing run never corrupts the last good data.
- Cold start with no snapshot and BGG unreachable: serve 200 with a deliberate "the cabinet is being stocked" empty state, not a 5xx.
- The UI footer shows "synced N hours ago" and, when stale beyond a threshold, "BGG currently unreachable, showing the last known collection".

### Triggering, single-flight and the manual button

- `BackgroundService` with `PeriodicTimer` (hourly, plus jitter of a few minutes; run once at startup if the snapshot is older than the interval).
- One `Channel<SyncRequest>` with capacity 1 and a single consumer = single-flight. A manual request while a run is in progress returns 202 "already running".
- **Global cooldown** (for example 10 minutes) for manual triggers, persisted in `sync-state.json` so a service restart cannot be used to bypass it. Over-cooldown responses are 429 with `Retry-After`. Because the cooldown is global (not per client), a botnet cannot amplify traffic to BGG beyond one run per cooldown window; Traefik and the in-app rate limiter protect the endpoint itself (section 6).

## 2. Storage Location Source (private info question)

### What the evidence says

| Question | Finding | Confidence |
|----------|---------|------------|
| Does `showprivate=1` expose `invlocation`? | Yes, in a `<privateinfo>` element on each item (purchase price, inventory location, private comment and similar). | MEDIUM-HIGH |
| For whom? | BGG's wiki states `showprivate` works **only on your own collection when you are logged in** (default 1 for logged-in users, 0 otherwise). Community threads confirm: "be authenticated as the same account you're requesting the collection of". | MEDIUM-HIGH |
| Does an application token grant it? | **No documentation says so, and no source found demonstrates it.** The 2025 registration rules explicitly separate "an application token" from "downloading your own collection while logged in" (the latter needs no registration). Tokens exist to identify and rate-regulate applications, not to impersonate a user. | MEDIUM (absence of evidence) |
| How do tools get private info today? | Reverse-engineered: POST username and password to `https://boardgamegeek.com/login/api/v1`, then replay the resulting session cookies against the XML API with `showprivate=1`. Undocumented, brittle (the `bgg_username`/`bgg_password` cookies are set several times, one as `deleted`). | MEDIUM |
| Is the cookie route allowed? | The terms list "using private site APIs" as unlicensed and the licence is for the registered application. A server holding the owner's password or session cookie is outside the documented access model; it also fails on any future 2FA, CAPTCHA or Cloudflare challenge. | MEDIUM |

### Recommendation

1. **Spike once the token exists (about 10 minutes):** call the collection with only the bearer token and `showprivate=1` for the owner's own username, and check whether `<privateinfo>` appears. If it does, add a `PrivateInfoLocationSource` behind the same interface and prefer it. Do not plan the architecture around this outcome.
2. **Build plan B (a) as the default: location convention in the public collection comment.** The comment is returned publicly by `comment=1`, needs no extra credential, keeps the app database-free and read-only, and the owner keeps editing in BGG, which matches the core value. Convention: a line starting with `Location:` (case-insensitive, also accept `@`), for example `Location: Shelf A cabinet`. Parser rules (Domain, unit-tested): first match wins; trim and collapse whitespace; group case-insensitively with the most common casing as display name; no match = "Unsorted" group; expansions inherit the base game's location unless they carry their own.
3. **Do not store the owner's BGG password or session cookie anywhere on the host.** A public-facing box holding a credential that unlocks the owner's account is a worse trade than a comment convention, and it is unlicensed-API territory.
4. Consequence for the owner (flag in the roadmap): room names written in comments are public on BGG, and the site publishes them to the world. If a location name is sensitive, use a neutral label.

### Plan B (b) in case the comment convention proves unworkable

| Aspect | Impact |
|--------|--------|
| Storage | Still no real database needed: a small `locations.json` overlay (BGG id -> location) written atomically next to the snapshot is enough for a few hundred rows. SQLite is only warranted if the editor grows beyond that. |
| Architecture | A second Kestrel listener (admin port) serving the editor and write endpoints, reachable only from the home network/VPN; Traefik routes it with an `ipAllowList` middleware and the LXC firewall allows the admin port only from the Traefik source. Do not distinguish admin vs public by `Host` header alone. |
| Cost | The overlay becomes a source of truth that needs backup, which breaks "everything is derived from BGG". Adds auth-adjacent surface (CSRF, input validation) to an otherwise read-only app. |
| Switching cost | Low. Location is one field applied during snapshot assembly behind `ILocationSource`; swapping or layering sources does not touch layout, rendering or deployment. |
| Existing shared database LXC | Reject: cross-container dependency and shared failure domain, a connection secret, EF migrations (which the reference's installer machinery would have to be extended for), all for about 1 MB of data. |

## 3. Data Model and Snapshot

### Do we need a database? No.

Reasons: the whole dataset is a few hundred rows (well under 1 MB); there is exactly one writer (the sync job) and it replaces the whole dataset; all data is derived from BGG, so loss is recoverable by re-syncing (the only unrecoverable data would be plan B (b)'s overlay); a database drags in migrations, a migrator role, pre-migration backups, "no automatic rollback after a migration" logic and encrypted backups, which is most of the reference deployment's complexity. Without one, rollback is always a safe symlink flip.

Rejected alternatives: SQLite (fine technically, but its only benefit here is partial updates and queries we do not need; keep as the upgrade path if plan B (b) outgrows a JSON overlay); the shared database LXC (see above); PostgreSQL in the LXC (reference pattern, unjustified at this size).

### On-disk layout

Under `StateDirectory=cabinet` (outside the release directory so it survives release swaps):

```
/var/lib/cabinet/
  snapshot.json        current snapshot (atomic write: temp file, fsync, rename)
  snapshot.prev.json   previous good snapshot (for suspect-shrink comparison and manual recovery)
  sync-state.json      last attempt/success/error category, consecutive failures, last manual trigger
  images/              {contentHash}-{width}.webp, immutable filenames
```

### Snapshot shape (illustrative, all values synthetic)

```json
{
  "schemaVersion": 1,
  "contentHash": "sha256:...",
  "syncedAtUtc": "2026-01-01T12:00:00Z",
  "games": [
    {
      "bggId": 100001,
      "collId": 5000001,
      "name": "Example Game",
      "kind": "base",
      "year": 2020,
      "players": { "min": 2, "max": 4, "best": [3], "recommended": [2,3,4] },
      "playTimeMinutes": { "min": 45, "max": 60 },
      "weight": 2.7,
      "location": "Shelf A cabinet",
      "image": { "hash": "abc123", "aspect": 1.0, "dominantColor": "#a3412c", "widths": [240, 480] },
      "dimensionsMm": { "w": 295, "h": 295, "d": 70, "source": "version" },
      "expansionOf": null,
      "detailsPending": false,
      "detailsRefreshedUtc": "2026-01-01T12:00:00Z"
    },
    {
      "bggId": 100002,
      "kind": "expansion",
      "expansionOf": 100001
    }
  ]
}
```

Rules:
- Strings from BGG (names, comments, location) are untrusted text. Store as-is, render with `textContent` only, and enforce a CSP (section 5).
- `schemaVersion` mismatch on boot (after a rollback to an older release, say) must never crash: treat as "no snapshot", serve the empty state, and let the sync rebuild. Because nothing irreplaceable lives in the snapshot, this is safe.
- Include a layout-inputs-only content hash (exclude `syncedAtUtc`) so an unchanged collection keeps the same ETag and layout cache.
- Dominant colour is computed once per image at download time (average of saturated, non-extreme pixels after downscale, or quantisation), not per request.
- Image cache is pruned after games are removed (grace period of a few sync cycles).

## 4. Layout Engine

### Where: server C#, in `Cabinet.Domain`

| Option | Verdict |
|--------|---------|
| Server C# (pure function in Domain) | **Chosen.** Testable with xUnit including golden-file determinism tests, one language, no layout code duplicated in JS, memoised per snapshot so cost is paid once per sync, trivially deterministic (no browser float/layout differences). |
| Client JS | Rejected: untested in the owner's stack, re-runs on every visit and resize, risks different results across browsers, harder to guarantee "same collection -> same layout". |
| Hybrid (server layout per profile, client scales) | **This is the chosen shape:** the server owns *structure* for a small set of width profiles; the client owns *scale* (CSS) and *filtering* (dimming) only. |

Trade-off accepted: reflow is discrete (profiles) rather than continuous. Three profiles (for example narrow phone, tablet, desktop) cover it; the client picks one with `matchMedia` and refetches only when the profile changes (rotation, window resize across a breakpoint). Within a profile, everything scales fluidly via one CSS custom property.

### Inputs and outputs

Input: ordered games with physical size in millimetres (version dimensions or the aspect-ratio estimate from section 1), kind (base / expansion with `expansionOf`), `collId`, location; plus `LayoutOptions { Profile (bay width in cells), Mode (All | ByLocation) }`.

Output (all coordinates integer millimetres in an abstract cabinet space, never pixels):

```
CabinetLayout
  mode, profile, layoutVersion
  cabinets[]                    1 in All mode, one per location in ByLocation mode (stable order, "Unsorted" last)
    label, widthMm, heightMm
    bays[]                      grows with the collection; a new bay is opened when rows are full
      rows[]                    shelf levels with a height class (tall / medium / low) and a decorative drawer row at the bottom
        cubbies[]               irregular vertical dividers, deterministic positions
          placements[]          { gameId, kind: face | spine | lyingStack | expansionSpine,
                                  x, y, w, h, z, rotation, stackIndex }
```

Expansion handling: a base game and its owned expansions form an atomic *family unit*; the expansion spines are emitted as thin sideways placements adjacent to the base placement (stacked horizontally beside it, or on top of a lying box). Orphan expansions are stand-alone expansion spines.

### Packing and style choices

- Display mode per game is a pure function of (size class, stable hash of `bggId`): large boxes are biased to face-out, a quota (about a quarter to a third) overall for a lived-in mix, smaller or thicker boxes as spines, flat small boxes as lying stacks. No dependence on other games, so adding a game never flips an existing game's style.
- Shelf row height classes come from a fixed set; a row takes the next unit that fits width and height using best-fit among the currently open rows of the current bay.
- Empty and tiny collections: always render a minimum cabinet (for example one bay, three rows, a few empty cubbies, a drawer row) so zero or five games looks intentional. A min-fill rule can decorate empty cubbies with props (bookend, plant) drawn purely in CSS.

### Determinism (same collection, same layout, no reshuffle on revisit)

1. **Order by `collId` ascending** (BGG's collection item id increases as games are added; fall back to `bggId`). Packing is *online* and *prefix-stable*: each unit's position depends only on units before it. Adding a game appends at the end; no existing placement moves. Removing a game shifts only later items. This mirrors how a real shelf grows and removes the "everything reshuffled after a sync" failure.
2. Per-cabinet independence: in ByLocation mode each location's sequence is laid out alone, so a new game in one location never disturbs another.
3. **No process-randomised hashing.** `string.GetHashCode()` is randomised per process in .NET; use a fixed hash (FNV-1a or xxHash over the id) feeding a small hand-written PRNG (splitmix64) seeded with (cabinet label hash, bay index, row index).
4. Integer units only (millimetres), no floating-point accumulation; no `Dictionary`/`HashSet` iteration order dependence (sort explicitly).
5. A `LayoutVersion` constant is part of the cache key and the ETag. Changing the algorithm intentionally reshuffles once; everything else must not.
6. Golden-file tests: a synthetic 120-game fixture must produce byte-identical layout JSON across runs and across appends of N more games (assert the prefix is unchanged).

### Reflow and the "one cabinet per location" toggle

- Profiles differ only in bay width (cells per row) and breakpoints; the engine is the same. Phone profile = narrow bays stacked into a tall single column.
- ByLocation is just a different partitioning of the input before running the same engine per partition. The toggle fetches `?mode=ByLocation`; both modes can be precomputed for all profiles after each sync (about 6 layouts, small), so toggling is instant and cached.
- Filters never trigger relayout. They only toggle a "dimmed" state on existing elements, which keeps the cabinet stable while filtering and makes dimming work identically in both modes.

## 5. Rendering

### Page composition

- Static shell (`index.html`, one CSS file, one or two ES modules). The module fetches `/api/catalogue` (metadata for filters and cards) and `/api/cabinet?profile=&mode=` (layout), then builds the DOM.
- Boxes are absolutely positioned elements inside a scale wrapper: `left/top/width/height` computed as `calc(var(--u) * value)`, where `--u` is set once from the container width and bay width. Face-out boxes are `<img>` (lazy loading, `decoding=async`); spines are `<div>`s with the title (`writing-mode: vertical-rl` for standing spines) on a background from the stored dominant colour, with contrast-checked text colour; expansion spines are the same, smaller and sideways. Wood, shelves and cubby frames are CSS gradients plus at most one small texture asset.
- A few hundred DOM nodes with lazy images is comfortable on phones. If counts ever pass roughly 1000, virtualise by bay (render only bays near the viewport); do not build that up front.

### Detail card and pull-out animation

- Click/tap on a placement looks up the game by id in the already-loaded catalogue (no network round trip), runs a FLIP-style animation: the box's element translates forward and scales (`transform` only, GPU-friendly, `prefers-reduced-motion` respected) while a native `<dialog>` or panel opens with player count, play time, weight, best count, location, expansions list, and a link to the game on BGG.
- Expansion spines open the card of their base game (with the expansion highlighted) or their own card if orphaned.

### Filters and dimming against the layout

- Filter state (player count, play time range, location, text search) lives in the client. A predicate over catalogue entries produces a set of matching game ids; the renderer keeps a `Map<gameId, Element[]>` built once at draw time and toggles `data-dim` on elements. Expansion spines inherit their base game's match; a text search that matches the expansion name un-dims it too.
- Dimming is a CSS filter/opacity transition on elements; it never changes geometry, so it composes with both layout modes and any profile.

### Image delivery: download, resize, serve ourselves

| Option | Verdict |
|--------|---------|
| Hotlink BGG CDN | Rejected: leaks every visitor's IP and referrer to a third party, full-size originals are heavy on phones, URLs and hotlink behaviour can change, and it forces third-party origins into the CSP. |
| Proxy per request | Rejected: puts BGG/CDN latency on the visitor path and needs on-demand work on a low-power host. |
| **Download at sync, resize once, serve static** | **Chosen.** Server-side caching is what the terms ask for. Produce two widths (for example 240 and 480 px, WebP) per game, filenames contain a content hash so they are immutable. Keeps `img-src 'self'`. The image-processing library choice is a STACK.md item (check licence terms of candidates for a public open-source repo). |

One caution: BGG image copyright belongs to publishers/artists; the BGG terms permit server-side caching and public display as part of the licensed use, but confirm the image wording in the actual terms text once the application is approved (could not be read directly for this research). Process images sequentially at low priority; a few hundred images is a one-off CPU cost.

### Caching headers

| Resource | Header |
|----------|--------|
| `/images/{hash}-{w}.webp` | `Cache-Control: public, max-age=31536000, immutable` |
| Fingerprinted JS/CSS (`MapStaticAssets`, precompressed gzip/brotli at build time) | `public, max-age=31536000, immutable` |
| `index.html` | `no-cache` (revalidate with ETag) |
| `/api/catalogue`, `/api/cabinet` | `ETag` = snapshot content hash + layout version, `Cache-Control: public, max-age=60, stale-while-revalidate=600`; compressed bytes memoised per snapshot |
| `/api/sync`, status | `no-store` |

## 6. Deployment Architecture

### What the reference does (read from its docs and `deploy/`)

- New unprivileged Ubuntu 24.04 LXC (`nesting=1`, so systemd sandboxing works), created with `pct create`.
- `deploy/provision.sh` runs idempotent modules in `provision.d/` (`10-packages`, `20-accounts`, `30-postgresql`, `40-services`, `50-firewall`, `60-grafana-accounts`). First run creates `/etc/<app>/provision.conf` from an example and stops; second run applies. Config templates are rendered from it (nftables, env file).
- A root-owned installer `*-deploy` with `poll`, `install vX.Y.Z`, `rollback X.Y.Z`, `verify`. A systemd timer (`OnBootSec=2min`, `OnUnitActiveSec=5min`) runs `poll`: anonymous GitHub `releases/latest` read, semver compare, download zip + `.sigstore.json`, `gh attestation verify` with all GitHub tokens unset (signer workflow, source ref `refs/tags/vX.Y.Z`, `--deny-self-hosted-runners`), confirm the attested commit is on `main` via the unauthenticated compare API, refuse downgrades, unpack to `releases/{version}`, atomically repoint `current` symlink (`mv -T`), restart, health-check, auto-rollback if no migration ran.
- Release workflow: strict-semver tag -> build job (package, test, attest, draft release) -> publish job gated by the `deploy` environment (owner approval), which re-verifies checksum and attestation before `gh release edit --draft=false`. All third-party actions pinned by SHA, `permissions: {}` at top level.
- Network: nftables default-drop; SSH only from admin subnets; app ports only from the Traefik container; the app's ops endpoint on a loopback-only second Kestrel listener (the reference uses 5080 public-ish and 5081 loopback); Traefik file-provider dynamic config with an `ipAllowList` and security headers; app trusts `X-Forwarded-For` only from `ReverseProxy__KnownProxies__0`.

### This app's equivalent

```
Host
  Traefik LXC (existing)  --public router--> cabinet LXC :5080
cabinet LXC (new, unprivileged Ubuntu 24.04, nesting=1)
  /opt/cabinet/releases/{version}/      unpacked verified releases (app/, deploy/, release-manifest.json)
  /opt/cabinet/current -> releases/{version}   atomic symlink
  /etc/cabinet/cabinet.env              BGG token, owner username, KnownProxies, AllowedHosts   (640 root:cabinet)
  /etc/cabinet/deploy.conf              repo slug, signer workflow, keep-releases, ops URL
  /var/lib/cabinet/                     snapshot + images (StateDirectory, survives releases)
  systemd: cabinet.service, cabinet-deploy-poll.service/.timer, nftables.service
```

Sizing: 1 core, 512 MB to 1 GB RAM (framework-dependent Kestrel plus bursts of image processing), 4-8 GB root disk, versus the reference's 2 cores / 2 GB / 16 GB. Set `MemoryMax` on the unit so image processing can never starve other guests on the shared 16 GB host.

### What to delete from the reference

| Reference piece | Here |
|-----------------|------|
| PostgreSQL module, roles, peer auth, `ledger_migrator`, EF migration bundle, pre-migration backup, "migration ran, cannot roll back" branch | **Remove entirely.** Release manifest lists no migrations; rollback is always allowed. |
| Backup service/timer, `age` recipients, restore drill | **Remove.** Nothing irreplaceable on disk; keep the env file's secrets in the password manager. |
| Grafana, Prometheus, node-exporter, textfile metrics | **Remove** unless the owner already runs monitoring that wants to scrape it. `journalctl` plus the `/health` JSON (snapshot age, last sync result, version) is enough. Deploy outcome email via the existing relay is an optional later addition. |
| Data Protection certificate | **Remove** (no encrypted cookies or stored secrets). |
| `ledger-bank-key`, API keys | Not applicable. |
| `60-grafana-accounts` | Remove. |

Keep, nearly verbatim: `provision.sh` framework, `20-accounts` (service user, directories, env file, never overwritten), `40-services` (units), `50-firewall`, `deploy/lib/*`, the installer with `poll/install/rollback/verify`, the pinned-SHA release and CI workflows, `build/package-release.sh` (minus efbundle), `validate-release-tag.sh`, the repository-settings guide (tag ruleset, `deploy` environment with required reviewer and tag-only policy, protected `main`), and the `build/lint` checks that enforce no personal data and no planning references (valuable for a public repo).

The installer's health check changes from "metrics contain `build_info{version=...}`" to "`GET http://127.0.0.1:5081/health` returns healthy and reports the expected version".

### What changes for public exposure

| Concern | Reference (LAN/VPN only) | This app (public) |
|---------|--------------------------|-------------------|
| Traefik access control | `ipAllowList` on every router | **No allow-list** on the site router. Add `rateLimit` (per client IP, tolerant of an image burst: for example average 30/s, burst 100 for the site; a separate stricter router for `PathPrefix(/api/sync)` such as average 1 per minute, burst 3). Traefik's `rateLimit` supports `sourceCriterion.ipStrategy`; since public traffic arrives directly at Traefik, the source IP is the real client. |
| Security headers | HSTS, nosniff, frame deny, referrer policy | Same plus `contentSecurityPolicy` (`default-src 'self'; img-src 'self' data:; style-src 'self'; script-src 'self'; base-uri 'none'; frame-ancestors 'none'; form-action 'none'`) and `permissionsPolicy` (disable unused features). Only possible because images, fonts and the BGG logo are self-hosted. |
| Health/ops | Loopback ops listener, no Traefik route | **Keep exactly that.** Health, status details and any diagnostics exist only on `127.0.0.1:5081`. The public listener exposes no `/health`, `/metrics`, Swagger or status internals. |
| Firewall | App ports from Traefik only | Same: public port allowed only from the Traefik source; ops port not allowed at all (loopback). |
| Forwarded headers | `KnownProxies` | Same, and required for the in-app rate limiter and logs to see real client IPs. Also set `AllowedHosts` to the public hostname. |
| Write surface | REST API with keys | One public write-ish endpoint, `POST /api/sync`: global persisted cooldown, in-app `RateLimiter` per IP, tiny request size, no body. Worst case it triggers one extra sync per cooldown window. |
| Kestrel limits | defaults | Tighten: small `MaxRequestBodySize`, header and request timeouts, `MaxConcurrentConnections` sized for the host. |
| Bot load on a low-power host | not relevant | Everything the public hits is static files or memoised, pre-compressed JSON; no per-request BGG call, no per-request image work. |
| Privacy of content | household-only | Location names, game list and the owner's collection are public by design; add the BGG credit and "Powered by BGG" logo in the footer. No analytics or third-party scripts. |
| Public DNS and TLS | internal hostnames | Public DNS record plus the existing certificate resolver and NAT path already used for another public service. |

### Release pipeline (simplified)

Tag `vX.Y.Z` on `main` -> `release.yml`: validate tag, `dotnet restore --locked-mode`, publish framework-dependent `linux-x64` into `app/`, bundle `deploy/` (minus tests) and `release-manifest.json` (version, commit; no migrations), run the full test suite (no database service container any more, so faster), attest the zip, upload to a **draft** release -> `publish` job in the `deploy` environment (owner approves) re-checks checksum and attestation and publishes. If the frontend later needs a build step, run it in CI and ship only its output; the server never has Node.

## Recommended Project Structure

Following the reference layering (Domain / Repository / Service, `.slnx`, `Directory.Build.props` with nullable, warnings-as-errors and lock files, `global.json` pinning the SDK):

```
Cabinet.slnx
Directory.Build.props
global.json
Cabinet.Domain/
  Games/            Game, ExpansionLink, DimensionSource, Players, PlayTime
  Snapshots/        Snapshot, SyncState, schema version, content hash
  Locations/        LocationConvention parser
  Sync/             SyncPlanner (diff, refresh policy, suspect-shrink guard), PollInterpreter (best/recommended)
  Layout/           CabinetLayoutEngine, LayoutOptions, CabinetLayout, StableHash, SplitMix prng
  Abstractions/     IBggClient, ISnapshotStore, IImageStore, IClock
Cabinet.Repository/
  Bgg/              BggClient (pacing, 202 loop, status classification), XML parsing, version/dimension parsing
  Snapshots/        FileSnapshotStore (atomic writes, schema guard)
  Images/           FileImageStore (download, resize, aspect, dominant colour)
Cabinet.Service/
  Program.cs
  Hosting/          dual Kestrel listeners, forwarded headers, rate limiter, security header defaults
  Sync/             SyncHostedService, SyncOrchestrator, cooldown
  Cabinet/          SnapshotProvider, LayoutCache
  Endpoints/        catalogue, cabinet, sync, status (public); health (ops listener only)
  wwwroot/          index.html, cabinet.css, cabinet.js (ES modules; JSDoc-style docs, no loose line comments, matching the repo convention)
Cabinet.UnitTests/       layout golden/determinism, parser, planner, BGG XML fixtures, poll interpretation
Cabinet.IntegrationTests/ WebApplicationFactory + fake BGG HttpMessageHandler: 202 dance, BGG down serves last good snapshot, shrink guard, cooldown, headers
deploy/                 provision.sh, provision.d/, systemd/, nftables/, traefik/cabinet.yml.example, bin/cabinet-deploy, lib/, *.example, tests/
build/                  package-release.sh, validate-release-tag.sh, lint/
docs/                   deploy.md, lxc-setup.md, releasing.md, github-repository-settings.md
.github/workflows/      ci.yml, release.yml
```

Structure rationale:
- Domain has no I/O and no framework dependencies, so the two most valuable and most intricate pieces (layout engine, sync planner) are pure and fast to test.
- Repository owns everything that touches the outside (BGG, filesystem, image decoding), mirroring the reference where EF/data access lives only in Repository. If the BGG client later grows, it can be split into its own `Cabinet.Bgg` project without touching Domain.
- Service wires everything and is the only project the host references; it should be thin.
- Test fixtures are synthetic: hand-written BGG XML with invented titles and ids, a synthetic username, example.com hostnames. No real collection data ever enters the repo.

## Architectural Patterns

### Pattern 1: Immutable snapshot swap
**What:** The sync builds a complete new `Snapshot`, persists it atomically, then swaps a single reference. Readers hold whatever instance they started with.
**When to use:** Always here; one writer, many readers, tiny data.
**Trade-offs:** Zero locking in request paths; costs a full rewrite of about 0.5 MB per changed sync, which is irrelevant.

```csharp
public sealed class SnapshotProvider
{
    private Snapshot current = Snapshot.Empty;

    /// <summary>Gets the snapshot every request should read from.</summary>
    public Snapshot Current => Volatile.Read(ref current);

    /// <summary>Publishes a fully built snapshot to all readers.</summary>
    public void Publish(Snapshot next) => Volatile.Write(ref current, next);
}
```

### Pattern 2: Pure, seeded layout function
**What:** `Layout(IReadOnlyList<LayoutGame> games, LayoutOptions options) -> CabinetLayout`, no clock, no `Random`, no I/O.
**When to use:** Anything that must be reproducible and golden-testable.
**Trade-offs:** Needs discipline (stable hash, integer math, explicit sorting) but pays back with trivial testing and caching.

### Pattern 3: Source-agnostic location and dimension providers
**What:** `ILocationSource` (comment convention now, private-info or overlay later) and an ordered dimension fallback chain, both resolved during snapshot assembly in Domain.
**When to use:** Where research left an open question (private info access, sparse dimensions).
**Trade-offs:** One small interface now avoids touching layout and rendering later.

### Pattern 4: Pacing and classification in one place
**What:** A single BGG gateway that owns the token, the 5-second spacing, and the mapping of HTTP outcomes to `Success | Queued | Throttled | Unavailable | Unauthorized`.
**When to use:** Always; callers never see raw status codes.
**Trade-offs:** Slightly more code, but a missed 202 or a misread 403 can never silently blank the cabinet.

## Data Flow

### Sync flow

```
timer / manual POST -> cooldown check -> single-flight channel
   -> BggClient.GetCollection (games, expansions; 202 loop, 5 s spacing)
   -> SyncPlanner.Diff(previous snapshot, fresh collection)   [suspect-shrink guard]
   -> BggClient.GetThings(batches of <= 20 for added/stale ids; optional versions pass)
   -> ImageStore.Ensure(new/changed images)                    [aspect, dominant colour, webp widths]
   -> SnapshotAssembler (location parse, expansion mapping, dimension fallback chain)
   -> SnapshotStore.WriteAtomic -> SnapshotProvider.Publish -> LayoutCache invalidated by hash
   -> SyncState updated (success or categorised failure)
```

### Request flow

```
Browser -> Traefik (TLS, rate limit, headers) -> Kestrel public listener
   GET / (static shell, no-cache)           -> static files
   GET /api/catalogue, /api/cabinet?...     -> SnapshotProvider.Current -> LayoutCache (compute once, memoise)
   GET /images/...                          -> static files (immutable)
Browser: renders DOM from layout, filters/dims in memory, opens card from catalogue, no BGG contact
```

### State management (client)

```
catalogue (immutable) + layout (per profile/mode) + filter state
filter state -> matching id set -> data-dim toggles (no relayout)
viewport profile change or mode toggle -> fetch layout (HTTP-cached) -> redraw
```

## Scaling Considerations

| Scale | Architecture adjustments |
|-------|--------------------------|
| Tens of games, a handful of visitors | Everything above, as is. |
| Hundreds of games (the target) | Layout computed once per snapshot per profile/mode; about 6 small layouts cached; images pre-resized; sync takes 1-3 minutes, dominated by BGG pacing. |
| Thousands of games | Virtualise by bay in the client, paginate `thing` refresh across runs (already bounded), consider splitting the catalogue by location. Still no database needed. |
| Many visitors (public link shared widely) | Static files and cached JSON; add Traefik-side caching only if measured to matter. |

Scaling priorities: (1) first real bottleneck is BGG pacing for large first syncs, solved by progressive commit and a bounded per-run enrichment budget; (2) second is image processing memory/CPU on the shared host, solved by sequential low-priority processing and `MemoryMax`.

## Anti-Patterns

### Anti-Pattern 1: Storing the owner's BGG credentials or cookie to reach private info
**What people do:** Log in with username/password server-side and replay cookies for `showprivate=1`.
**Why it's wrong:** Undocumented API, outside the licensed model, breaks on 2FA/CAPTCHA/Cloudflare, and a public-facing host then holds a credential that unlocks the owner's account.
**Do this instead:** Comment convention (plan B (a)); spike whether the application token alone works.

### Anti-Pattern 2: Treating any non-200 as "no games"
**What people do:** Map 202/429/403/5xx to an empty collection and overwrite the cache.
**Why it's wrong:** A BGG hiccup wipes the cabinet.
**Do this instead:** Typed outcomes, retry on 202, suspect-shrink guard, never commit on error.

### Anti-Pattern 3: Randomised or order-dependent layout
**What people do:** `new Random()`, `GetHashCode()`, alphabetical re-sorting, unordered collections.
**Why it's wrong:** The cabinet reshuffles on every restart or sync, which feels broken.
**Do this instead:** Stable hash, seeded PRNG, `collId` order, online packing, golden tests.

### Anti-Pattern 4: Hotlinking BGG images or calling BGG from the browser
**Why it's wrong:** Violates the server-side-with-caching term, leaks visitor data, bloats phones, weakens CSP.
**Do this instead:** Download, resize, serve own origin.

### Anti-Pattern 5: Rendering BGG text as HTML
**Why it's wrong:** Names and comments are user-authored on a site the app does not control; on a public origin that is stored XSS.
**Do this instead:** `textContent` only, strict CSP, validate/normalise on ingest.

### Anti-Pattern 6: Adding a database "because the reference has one"
**Why it's wrong:** Brings migrations, roles, backups and rollback restrictions for 0.5 MB of derived data.
**Do this instead:** JSON snapshot; revisit only if an owner-edited overlay (plan B (b)) must be durable.

### Anti-Pattern 7: Exposing health or diagnostics on the public listener
**Do this instead:** Loopback ops listener, no Traefik route, no firewall hole.

## Integration Points

### External Services

| Service | Integration Pattern | Notes |
|---------|---------------------|-------|
| BGG XML API2 | Server-side only, bearer token from env file, single gateway with 5 s spacing | Registration is manual; start immediately. 20-id cap on `thing`. 202 on collection. 403 can be Cloudflare. Credit + logo required. |
| BGG image CDN | Server-side download at sync time only | Not hit by visitors. |
| GitHub (release assets, `releases/latest`, compare API, Sigstore trust root) | Anonymous, from the installer only | No credential on the host; unreachable Sigstore means verification fails, never skipped. |
| Traefik LXC | File-provider dynamic config copied from `deploy/traefik/*.example` | Rate limits, CSP, no allow-list. Port allowed only from this source. |

### Internal Boundaries

| Boundary | Communication | Notes |
|----------|---------------|-------|
| Service -> Domain | direct calls to pure functions | Layout and planning are I/O-free. |
| Service -> Repository | via Domain interfaces | Keeps BGG/file/image details replaceable and testable. |
| Sync orchestrator -> providers | publish immutable snapshot | One writer. |
| Frontend -> backend | read-only JSON over HTTP with ETag | No client-side BGG access. |
| Installer -> app | loopback `/health` | Only place health is exposed. |

## Suggested Build Order

```
[0 Lead-time, start now]  register BGG application (manual approval)  |  create public repo, protect main
        |
[1 Foundations + release path]  solution scaffold, CI, release workflow, LXC provisioning, installer,
        |                        Traefik public route, hello page + ops /health, deployed and auto-updating
        |                        (independent of BGG token; proves the whole delivery chain early)
        |
        +--> [4 Layout engine + renderer prototype]  built against synthetic fixtures; needs only the Domain model.
        |        Can run in parallel with 2 and 3 and is the main use of the wait for BGG approval.
        v
[2 BGG sync + snapshot]  token, collection (2 calls, 202 dance), diff, shrink guard, atomic snapshot, image-less
        |                 minimal page rendering real game names as a basic cabinet (FIRST VERTICAL SLICE)
        v
[3 Enrichment]  thing batches, polls, expansion mapping, location convention, dimensions fallback chain,
        |        image pipeline (webp, aspect, dominant colour), details refresh policy
        v
[4 -> integrate]  engine fed with real data; spines, face-out mix, expansions, mobile profiles, per-location mode
        v
[5 Interaction]  pull-out animation, detail card, filters + dimming, view toggle, empty/near-empty states
        v
[6 Hardening]  public-exposure tuning (rate limits, CSP review), stale/BGG-down UX, selfcheck script, docs, soak
```

### Dependencies that drive the order

- Deployment path (1) has no dependency on BGG and is the highest "yak-shaving" risk; doing it first means every later slice ships to the real LXC.
- BGG access (token) gates 2 and 3; layout engine (4) does not, so it proceeds on fixtures.
- Snapshot schema (Domain model) is the contract between sync, layout and frontend: freeze a first version at the start of 2 and bump `schemaVersion` deliberately.
- Image pipeline must exist before the layout can use aspect-ratio dimensions and dominant colours (3 before final 4 integration), but the first slice can use plain coloured spines without images.
- Interaction (5) depends on the catalogue and layout contracts, not on sync internals.

### First vertical slice (end of step 2)

Real BGG collection, fetched by the deployed service on the LXC through the public Traefik route, rendered as a basic cabinet: a simple row/shelf grid of spines labelled with game names (no images, no expansions, no packing cleverness), last-good-snapshot behaviour proven by pulling the network, BGG credit and logo in the footer, deployed by tag via the pull model. It validates the token, the 202 handling, the snapshot, the release path and public exposure in one thin pass.

### Research flags for later phases

| Phase | Flag |
|-------|------|
| 2 BGG sync | Needs a hands-on spike with a real token: confirm `own=1` + `excludesubtype` behaviour on the owner's collection, 202 timings, `showprivate` with token-only (private info), and `collid` ordering. |
| 3 Enrichment | Needs a spike on real version XML: units, zero handling, how often dimensions exist, and whether `version=1` on the collection returns them. Decide the image-processing library and check its licence terms. |
| 4 Layout engine | Needs a prototype loop: packing quality vs prefix-stability trade-off, size-class defaults, cubby irregularity. Highest creative risk. |
| 1 Foundations, 5 Interaction, 6 Hardening | Standard patterns (copy the reference, CSS/JS animation, header tuning); little further research. |

## Open Questions

- Does a BGG application token alone expose `<privateinfo>` for the owner's own collection? (Spike; design assumes no.)
- Exact wording of the BGG image terms for self-hosted resized copies (could not be read directly; confirm once the application is approved and the terms page is accessible in a browser).
- Whether the owner is comfortable with location names being public (they are written in public BGG comments and shown on the site).
- Whether `version=1` on the collection returns box dimensions for owned versions (affects whether the heavy `versions=1` pass is ever needed).

## Sources

- Reference project (read directly): `docs/deploy.md`, `docs/lxc-setup.md`, `docs/releasing.md`, `docs/github-repository-settings.md`, `deploy/` (provision.sh, provision.d, systemd units, nftables template, traefik example, installer and libs), `.github/workflows/ci.yml` and `release.yml`, `build/package-release.sh`, `Directory.Build.props`, `global.json`, solution layout. HIGH.
- BGG: [Using the XML API](https://boardgamegeek.com/using_the_xml_api), [XML API Terms of Use](https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use), [BGG XML API2 wiki](https://boardgamegeek.com/wiki/page/BGG_XML_API2), [XML API registration required](https://boardgamegeek.com/thread/3540336/xml-api-registration-required), [Read this for uninterrupted access](https://boardgamegeek.com/thread/3539581/xml-api-read-this-for-uninterrupted-access). The pages themselves were blocked (HTTP 403 Cloudflare) for automated fetch; content taken from search excerpts and implementers quoting them. MEDIUM.
- Implementer reports of the licensing terms and behaviour: [keeplore issue 88](https://github.com/JacobStephens2/keeplore/issues/88) (terms summary: non-commercial licence, credit and "Powered by BGG" logo, server-side caching, no AI training, private site APIs unlicensed, 5 s guidance), [nalanda PR 39](https://github.com/isstiaung/nalanda/pull/39) (bearer token, 401 vs 403, hostname), [nalanda PR 58](https://github.com/isstiaung/nalanda/pull/58) (202/429/5xx handling, one request per 5 s), [gamecache issue 95](https://github.com/EmilStenstrom/gamecache/issues/95) (own-collection-while-logged-in exemption, heavy limits for unregistered collection reads), [BoardGamer.BoardGameGeek](https://github.com/Cobster/BoardGamer.BoardGameGeek) (202 retry defaults), [gobgg](https://github.com/fzerorubigd/gobgg) (token, retry with backoff). MEDIUM.
- Private info: BGG forum threads [How to get private info using XML API2?](https://boardgamegeek.com/thread/3336167/how-to-get-private-info-using-xml-api2) and [3335595](https://boardgamegeek.com/thread/3335595/how-to-get-private-info-using-xml-api2) (showprivate needs the logged-in owner; cookie replay via the login API), [Board Game Geek API (blog)](https://blog.arranfrance.com/post/board-game-geek-api/) (login API plus cookies approach), [tnaskali/bgg-api](https://github.com/tnaskali/bgg-api) (token vs cookie session for private endpoints). MEDIUM.
- Collection quirks and limits: [XML API2 returns expansions even when specifying boardgame subtype](https://boardgamegeek.com/thread/1583046/xml-api2-returns-expansions-even-when-specifying-b), [BGG Collections API: Correct flagging of expansions](https://boardgamegeek.com/thread/1945660/bgg-collections-api-correct-flagging-of-expansions), [Max 20 items from XML API and XML API 2](https://boardgamegeek.com/thread/3336313/max-20-items-from-xml-api-and-xml-api-2), [Version info in collection](https://boardgamegeek.com/thread/1463828/version-info-in-collection-using-xmlapi2collection), [Can board game versions and their dimensions be gotten?](https://boardgamegeek.com/thread/1924162/can-board-game-versions-and-their-dimensions-be-go). MEDIUM.
- Traefik: [RateLimit middleware](https://doc.traefik.io/traefik/reference/routing-configuration/http/middlewares/ratelimit/), [Headers middleware](https://doc.traefik.io/traefik/reference/routing-configuration/http/middlewares/headers/) (official docs, option names confirmed). HIGH.
- Layout determinism, packing approach, rendering approach, and the no-database conclusion are this research's own reasoning applied to the constraints, not external sources. MEDIUM.

---
*Architecture research for: public read-only BGG-backed visual cabinet, .NET 10, self-hosted LXC behind Traefik*
*Researched: 2026-10-03*

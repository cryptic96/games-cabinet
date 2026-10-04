# Technology Stack

**Project:** Games Cabinet (public, read-only web app that renders a BoardGameGeek collection as a dynamically drawn wooden cabinet)
**Researched:** 2026-10-03
**Overall confidence:** MEDIUM-HIGH (package versions and BGG rules verified against primary sources; BGG XML payload shapes and the location-field question need one live spike, see "Open verification items")

## Summary of Decisions

| Question | Decision | Confidence |
|----------|----------|------------|
| BGG client | Thin hand-written typed `HttpClient` + LINQ to XML (`XDocument`). No BGG client library. | HIGH |
| BGG resilience | `Microsoft.Extensions.Http.Resilience` 10.10.0 for 429/5xx/timeouts, plus an explicit bounded poll loop for 202 and a request-spacing handler (at least 5 s between BGG calls) | HIGH |
| BGG access | Registered non-commercial application + Bearer token, server-side only, token in the server env file, "Powered by BGG" logo and credit on the public site | HIGH (official source) |
| Persistence | **No database.** Atomic JSON snapshot file + image directory under the systemd `StateDirectory`. | HIGH |
| Location editor (if ever needed) | Second small JSON overrides file first; `Microsoft.Data.Sqlite` (not EF) only if it outgrows that. Not the shared database LXC. | MEDIUM |
| Frontend | ASP.NET Core Razor Pages shell + vanilla ES modules + modern CSS (container query units, `<dialog>`, View Transitions with fallback). DOM/CSS rendering, no SPA framework, no Blazor, no Canvas. No Node toolchain. | MEDIUM-HIGH |
| Layout/packing | Hand-written shelf packer in C# (pure, seeded, unit-tested). No packing library. | HIGH |
| Images | Download once server-side, resize to WebP with `SixLabors.ImageSharp` 4.1.2, serve from own origin; dominant colour hand-written (about 40 lines) | MEDIUM-HIGH |
| Background sync | `BackgroundService` + `PeriodicTimer(TimeProvider)` + single-flight `Channel`. No Hangfire/Quartz. | HIGH |
| Tests | Match reference: xunit.v3 4.0.1, FluentAssertions 8.11.0, NSubstitute 6.2.0, TimeProvider.Testing 10.10.0, Mvc.Testing 10.0.12, MTP runner. Add `Microsoft.Playwright.Xunit.v3` 1.63.0 in a separate UI test project. | HIGH (versions) / MEDIUM (UI test shape) |

## BGG API Access Rules (verified from official BGG pages)

BGG's own pages are behind a Cloudflare challenge for automated fetching, so the text below was read from Internet Archive captures of the official pages (captured 2026-09-24 to 2026-10-02) and cross-checked against community reports. The live URLs are the authoritative ones; re-read them before go-live because BGG states the policies can change at any time.

| Rule | What the official page says | Impact on this project |
|------|-----------------------------|------------------------|
| Registration | "Registration and authorization is required for use of the XML API." Register at `https://boardgamegeek.com/applications`. "Please be patient regarding a response; it may be a week or more before we get back to you." | **Start registration before any code is written.** Approval latency is the longest lead-time item in the project. Develop against synthetic fixtures meanwhile. |
| Token | Create tokens under your approved application ("Tokens" link). Send `Authorization: Bearer <token>`. Tokens are not currently refreshed ("may change"). | One token in the server-side env file. Never in the repo, logs or client. |
| Host | Use `https://boardgamegeek.com/xmlapi2/` **without** `www`: "Their usage may interfere with request authorization." | Hard-code the base URI; do not follow redirects to `www` while carrying the token. |
| Licence tier | Non-commercial licence is "generally provided at no cost (this may change in the future), but may have different usage limits". Commercial = for-profit, ads, payments, or money raised "in any way". Donation-only apps still need a commercial licence ("most likely free"). | Register as **Non-commercial**. No ads, no donate button, no payments on the site. |
| Rate limits | "We are still determining exact usage limits." API2 wiki: "BGG throttles the requests ... 500 or 503 ... Currently, a 5-second wait between requests seems to suffice." Community reports 429 responses without a `Retry-After` header. Current usage is visible at `/applications` > "Usage". | Treat 5 s between requests as the contract; serialise all BGG calls; expect 429/5xx anyway. Numeric limits are **not** published (LOW confidence on any specific number). |
| Caching / client-side | "All requests should be made by your servers, with the results cached." Requests from browsers "may result in too much traffic, which could be grounds for having your license suspended." | Server-side only, snapshot cache. The browser never talks to the BGG API. |
| Attribution | "credit BoardGameGeek by name as the source of the data" and "include the 'Powered by BGG' logo (linked back to BoardGameGeek) in public-facing uses", at a legible size. Logo files are linked from the official usage page. | Footer logo + link on every page. Obtain the logo from BGG's official link, not from a third-party repo. |
| Data modification | "You may not modify the data, including User Submissions, retrieved through the BGG XML API in any way." | Display faithfully. Derived presentation (generated spines, filters, layout) is rendering, not data modification, but never rewrite names, ratings or descriptions. LOW-MEDIUM: legal interpretation, flag for the owner. |
| Relaying | "Third party services that allow other applications (not end users) to access our data are strictly prohibited." | The site's own JSON endpoints should expose only the fields the UI needs, undocumented, no CORS, no bulk raw dump. |
| AI | "Use of the XML API ... to train an AI ... or Large Language Model ... system is strictly prohibited." | Not applicable; also matches the owner's rule of no LLM calls from the app. |
| Exceptions | Downloading your **own** collection while logged in needs no registration. Other users' collections without a registered app are "heavily rate limited". | The server is not "logged in", so it needs the token even for the owner's own collection. |
| Private data | `showprivate=1`: "Only works when viewing your own collection and you are logged in (include cookies from logging into the website in request)". "Private APIs used by our website" are **not licensed**. | See "Storage location" below: the inventory-location field is not reachable with an application token alone as documented. |

Official sources:
- Using the XML API (version date 2025-07-02): https://boardgamegeek.com/using_the_xml_api (archive: https://web.archive.org/web/20260924202844/https://boardgamegeek.com/using_the_xml_api)
- XML API Terms of Use: https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use
- BGG XML API2 reference: https://boardgamegeek.com/wiki/page/BGG_XML_API2

### Endpoints and parameters this app needs (from the API2 wiki)

| Need | Call | Notes |
|------|------|-------|
| Owned base games | `/xmlapi2/collection?username=<name>&own=1&excludesubtype=boardgameexpansion&stats=1&version=1` | `stats=1` returns `minplayers`/`maxplayers`/`playingtime` on each item (so most filter fields need no thing call). `version=1` returns version info for the owner's selected version, which may carry box width/length/depth. |
| Owned expansions | Same call with `subtype=boardgameexpansion` | Wiki bug note: the default call returns expansions mislabelled as `boardgame`. The documented workaround is exactly this two-call split. |
| Weight, expansion to base link, descriptions | `/xmlapi2/thing?id=1,2,...,20&stats=1` | **Maximum 20 ids per request.** `averageweight` is only in thing stats. Expansion `<link type="boardgameexpansion" ... inbound="true">` points at the base game. Do **not** pass `versions=1` on thing calls (returns every printing; very large for popular games). |
| Queued collection | HTTP 202 on collection | "if it's 202 (vs. 200) ... BGG has queued your request and you need to keep retrying (hopefully w/some delay between tries) until the status is not 202." |
| Change detection | `modifiedsince=` | Does not report deletions, so use a full owned-collection fetch each hour (one or two calls). Use `modifiedsince` for nothing. |

Sync cost at 300 owned games: 2 collection calls plus 15 thing calls on a cold cache, about 85 s at one request per 5 s. Warm cache: only new ids hit `thing`. Comfortably within an hourly budget.

### Storage location (the open data question, stack-level conclusion)

- The documented way to read the private "inventory location" is `showprivate=1` with a logged-in website session cookie. An application Bearer token is authorization for the API, not a website login. The only automated alternative is to store the owner's BGG password and replay the website login, which uses a private, unlicensed endpoint and puts a credential for a personal account on the server. **Do not do this.**
- **Do a 5-minute experiment first** (see Open verification items): request the owner's own collection with `showprivate=1` and only the Bearer token. If `<privateinfo>` (with `inventorylocation`) comes back, the field is readable with no extra secret. Result is unknown; treat as LOW confidence until tested.
- If it does not work: use a convention in a **public** per-item field the collection already returns (the collection `<comment>` text, e.g. a `Location: Shelf A` line parsed server-side). That keeps the app database-free. This is the lowest-effort path consistent with the owner's "BGG is the single source of truth" decision.

## Recommended Stack

### Core Framework (fixed by the owner, mirrored from the reference project)

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| .NET SDK | 10.0.112 (`rollForward: latestFeature`) | Build | Copy the reference `global.json` verbatim, including `"test": { "runner": "Microsoft.Testing.Platform" }` so `dotnet test --solution` works. |
| ASP.NET Core | 10.0.x (framework-dependent publish, `linux-x64`) | Host: Razor Pages shell, JSON endpoints, hosted sync service | Same runtime package the LXC provisioning already installs from the distro archive. |
| Solution layout | `.slnx`, projects Domain / Repository / Service + test projects | Match the reference | Domain = models + packing algorithm (pure, no I/O). Repository = snapshot store, image cache, BGG client (everything that touches the outside). Service = ASP.NET host, endpoints, hosted services, `wwwroot`. |
| `Directory.Build.props` | Copy reference: `net10.0`, `Nullable`, `ImplicitUsings`, `TreatWarningsAsErrors`, `Deterministic`, `RestorePackagesWithLockFile` | Conventions | Warnings-as-errors with lock files is the owner's supply-chain stance. Release script keeps `dotnet restore --locked-mode`. |

### Data / Persistence

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| JSON snapshot file via `System.Text.Json` | built in | Last good BGG snapshot: games, expansions, locations, dominant colours, `lastSyncedAt`, schema version | See "Persistence" below. |
| Image directory on disk | n/a | Resized WebP files served as static content | Survives restarts; rebuildable from the snapshot if lost. |
| systemd `StateDirectory=` | n/a | Writable location (`/var/lib/<app>`) under `ProtectSystem=strict` | Exactly the reference `ledger.service` pattern; no other write paths needed. |
| ~~Database~~ | none | | No EF Core, no migrator, no efbundle step in the release script, one fewer deploy phase. |

### Infrastructure

| Technology | Version | Purpose | Why |
|------------|---------|---------|-----|
| `Microsoft.Extensions.Http.Resilience` | 10.10.0 | 429/5xx/timeout handling for BGG and CDN `HttpClient`s | Built on Polly 8.8.0, first-party, `IHttpClientFactory` integrated. |
| `Microsoft.Extensions.Http` | 10.0.12 | Typed clients | Pulled by the above. |
| ASP.NET Core `MapStaticAssets` | built in (since .NET 9) | Fingerprinted, build-time brotli/gzip static assets with ETags | Right fit for a low-power host: zero runtime compression cost for CSS/JS. |
| ASP.NET Core `AddOutputCache` | built in | Cache layout JSON per snapshot version; evict by tag after each sync | Serving 100 visitors costs one computation per sync, not per request. |
| ASP.NET Core `AddRateLimiter` | built in | Cooldown/abuse guard on the manual sync endpoint and general fixed-window limit | No extra package. Requires the reference's `ForwardedHeaders` + known-proxy setup so the client IP is real behind Traefik. |
| Health checks | built in | `/healthz` reporting snapshot age and last-sync result | The deploy timer's health-check step needs a cheap endpoint; mirror the reference. |
| Logging | `AddSystemdConsole` in Production | | Same as reference. |

### Supporting Libraries

| Library | Version | Purpose | When to Use |
|---------|---------|---------|-------------|
| `SixLabors.ImageSharp` | 4.1.2 (Sep 2026) | Decode box art, resize to two widths, encode WebP, sample pixels for dominant colour | Always (image pipeline). Pure managed, no native assets. Licence condition below. |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 | Fake time for cooldown/PeriodicTimer tests | Test projects (same as reference). |
| (none) | | Packing, XML parsing, colour extraction, cooldown | Intentionally hand-written; see rationale. |

**ImageSharp licence (Six Labors Split License v1.0, verified from the repo LICENSE):** Apache-2.0 applies when "consuming the Work in for use in software licensed under an Open Source or Source Available license", or as a for-profit/individual under 1M USD annual gross revenue, or non-profit. Because this repository will be public, the repo **must carry an OSI-approved licence file** (MIT or Apache-2.0 recommended); with that in place ImageSharp is free for this use. If the owner ever wants the repo unlicensed/closed, swap to SkiaSharp (below). Keep ImageSharp current: 4.1.1 and 4.1.2 (Aug/Sep 2026) are both input-validation hardening releases, and a service that decodes images from a third-party CDN should take such fixes promptly (Dependabot).

### Development / Test Tools

| Tool | Version | Purpose | Notes |
|------|---------|---------|-------|
| `xunit.v3` | 4.0.1 | Test framework | Matches reference; test projects are `OutputType=Exe` with `IsTestProject=true`. |
| `FluentAssertions` | 8.11.0 | Assertions | Matches reference. v8 is under the Xceed community licence (free for non-commercial and open-source use); fine for this project, noted so nobody is surprised later. |
| `NSubstitute` | 6.2.0 | Mocks | Matches reference. |
| `Microsoft.Extensions.TimeProvider.Testing` | 10.10.0 | `FakeTimeProvider` | Matches reference. |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | In-process integration tests | Matches reference. |
| `RichardSzalay.MockHttp` | 7.1.0 | Scripted BGG responses (202 then 200, 429, malformed XML) | Optional; a 30-line hand-written `HttpMessageHandler` stub is equally fine. |
| `Microsoft.Playwright.Xunit.v3` | 1.63.0 | Browser tests of the cabinet | Separate project, separate CI job (browser download). |
| Dependabot (NuGet + GitHub Actions) | n/a | Updates | Reference already has `.github/dependabot.yml` and `zizmor.yml`; copy. |

## Detailed Rationale

### 1. BGG integration

**Recommendation: own thin client, LINQ to XML.**

- **No maintained library is worth the dependency.** The only credible .NET option is `BoardGamer.BoardGameGeek` (MIT; NuGet 0.10.0; repository last pushed 2026-04-18; Bearer token supported via `BoardGameGeekXmlApiClientOptions.AuthorizationToken` since 0.9.0). It is alive, but pre-1.0, single maintainer, about 21 GitHub stars, and its 202 handling retries every 500 ms up to 20 times by default, which is far more aggressive than BGG's 5 s guidance and would need overriding. Other NuGet packages found (`Bgg.Sdk` 1.0.0, `BoardGameGeekClientAPI`, `MyTurnYet.BggSharp`) have low adoption and no evidence of tracking the 2025 auth change. This app uses exactly three calls (owned collection, expansion collection, thing). A typed client of about 150 lines plus records is less code than configuring and constraining a general library, and keeps full control over spacing, 202 policy and logging. If the owner prefers a library anyway, `BoardGamer.BoardGameGeek` 0.10.0 is the only acceptable choice; override `MaxRetries`/`Delay` and wrap its `HttpClient` with the spacing handler.
- **XML parsing:** `XDocument.Load(XmlReader)` with `XmlReaderSettings { DtdProcessing = Prohibit, XmlResolver = null, MaxCharactersInDocument = ~20 MB }`, projected into immutable records. BGG's schema is attribute-heavy (`<name type="primary" value="..."/>`, `<minplayers value="2"/>`), which makes `XmlSerializer` attribute classes verbose and brittle against optional elements; LINQ to XML tolerates missing nodes. Decode HTML entities only in free-text fields such as descriptions (BGG double-encodes them). Validate the root element and content type: a Cloudflare/HTML error page with a 200 must not poison the snapshot.
- **Resilience pipeline (HIGH):** the standard handler defaults (verified in Microsoft Learn, updated 2026) are total timeout 30 s, retry 3 with exponential backoff and jitter at 2 s base, circuit breaker, attempt timeout 10 s, and it retries HTTP 500+, 408 and **429**. It does **not** treat 202 as retryable. So:
  1. Register the BGG client with `AddStandardResilienceHandler`, raised to the BGG reality: base delay 5 s, 4 retries, attempt timeout 30 s, total timeout 3 min, circuit-breaker sampling duration at least twice the attempt timeout, and `ShouldRetryAfterHeader = true` in case BGG starts sending `Retry-After` (today it does not; MEDIUM).
  2. Handle 202 **outside** the HTTP pipeline in the client method: bounded loop (for example 6 polls: 5, 10, 20, 30, 45, 60 s), then give up and keep the previous snapshot. Each poll goes through the pipeline and the spacing handler.
  3. Add a `DelegatingHandler` that serialises BGG requests and enforces at least 5 s between request starts (uses `TimeProvider` so tests do not sleep). Because the whole app is single-instance, an in-process `SemaphoreSlim` is sufficient.
  4. The Bearer header is added by a `DelegatingHandler` only for the BGG host. **Use a separate `HttpClient` without the token for the image CDN**, otherwise the token is sent to a third-party host.
  5. Set a descriptive `User-Agent` (project name plus a contact URL placeholder from configuration).
- **Failure policy:** any failed or partial sync leaves `snapshot.json` untouched and records the failure in memory and in `/healthz`. Never write a snapshot that has fewer items because a fetch was incomplete; require `collection.totalitems` to match parsed items before swapping.

### 2. Persistence: no database; JSON snapshot on disk

| Option | Verdict | Reasoning |
|--------|---------|-----------|
| **Atomic JSON snapshot + image dir** | **Choose** | Data is read-mostly, single writer (the sync service), a few hundred records (<1 MB), no queries beyond in-memory filtering, restart-safe, trivially backed up (copy a directory), trivially inspected, zero schema/migration machinery. Load into an immutable in-memory object at start and swap atomically after each sync; write `snapshot.json.tmp` then `File.Move(..., overwrite: true)` (rename is atomic on the same filesystem). Include `schemaVersion` and fall back to "empty, syncing" if the file is unreadable. |
| SQLite (EF Core 10.0.12 or `Microsoft.Data.Sqlite` 10.0.12) | Reject for the snapshot | Adds a schema, migrations, a connection model and a native `e_sqlite3` binary to answer queries that are a `.Where()` on 300 objects. Better only if a writable, multi-row, user-edited dataset appears. |
| Existing shared database LXC | **Reject** | Makes a public hobby site's availability depend on another LXC and a shared SQL Server, adds a network credential and a larger blast radius on an internet-facing service, needs EF migrations in the deploy flow, and contradicts the owner's own newer direction (the reference project moved to a database inside the app LXC with no network listener). Also the opposite of "keep serving the last good snapshot when things are down". |

**Location editor fallback (only if the comment convention and private-info route both fail):** keep a second file, `location-overrides.json` (`{ "<bggId>": "Shelf A" }`), merged over the synced data when building the snapshot view. One writer, tiny, human-editable, no auth surface on the public site (edit by SSH or by a home-network-only endpoint that rewrites the file atomically). If it ever needs history, concurrency or many editors, move **just that table** to SQLite via `Microsoft.Data.Sqlite` (no EF, one table, `StateDirectory` file, `.backup`-able). Do not move it to the shared database LXC.

### 3. Frontend: server-rendered shell + vanilla JS + modern CSS, DOM rendering

| Option | Verdict | Reasoning |
|--------|---------|-----------|
| **Razor Pages shell + vanilla ES modules + CSS** | **Choose** | One page, one data payload, a few interactions (filter dim, mode toggle, pull-out, dialog). Zero build step, zero npm supply chain (important next to a hardened release pipeline), nothing to run in CI besides `dotnet`. Owner stays in C#/HTML/CSS. Smallest payload on mobile. |
| Blazor Server | Reject | Holds a SignalR circuit and server memory per anonymous visitor on a public site on a low-power host; latency on every filter click; poor fit for animation. |
| Blazor WebAssembly | Reject | Multi-MB runtime download on mobile for a page that needs about 20 KB of JS. |
| Blazor static SSR | Pointless | Same as Razor Pages but the interactivity still needs hand-written JS interop. |
| Svelte/SvelteKit static, React, Vue SPA | Reject for v1 | Reactivity helps with many interdependent widgets; here filters toggle a data attribute on at most a few hundred nodes. Adds Node, a lock file ecosystem, a CI build step and a second language to maintain. Revisit only if an editor UI appears. |
| Canvas | Reject | No native text/a11y, manual hit-testing, blurry text on DPR changes, re-draw logic for every dim/pull-out. |
| SVG | Use only for tiny decorative assets | Hundreds of `<image>` plus text in SVG is workable but text layout (wrapped titles on spines) and focus/keyboard handling are worse than HTML. |

**Rendering approach (DOM + CSS):**
- Each game is a `<button>` (focusable, keyboard-operable, `aria-label` = title) absolutely positioned inside a cabinet container. At 50 to ~600 elements DOM is well within budget on a phone; Canvas buys nothing here.
- **Scale with container query units instead of re-laying out on resize.** Mark the cabinet `container-type: inline-size`; define `--u: calc(100cqw / var(--cabinet-units))` on a **child** of the container (a `cq*` unit resolves against an ancestor container, not the element itself) and position with `left: calc(var(--x) * var(--u))`. One layout scales to any width; no JS resize handler. Container query units are long-baseline (all evergreen browsers since early 2023).
- **Mobile reflow = a different layout, not different CSS.** The server emits layouts for two profiles (wide, for example 48 units, and narrow, for example 18 units) per mode (combined, per-location); the client picks one with `matchMedia`. This is how it stays "a narrow tall cabinet" rather than a squashed wide one.
- **Spines:** DOM text with `writing-mode: vertical-rl`, coloured from the pre-computed dominant colour, with text colour chosen by contrast ratio on the server. Face-out boxes: `<img width height loading="lazy" decoding="async">` with `aspect-ratio` set to avoid layout shift. Expansions: the same spine element rotated into a sideways thin strip (`writing-mode: horizontal-tb`).
- **Pull-out animation:** CSS `transform` (translate/scale, small `rotateY`/`translateZ` for the 3D feel; `perspective` on the cabinet) on the one active element, transition only transform/opacity (compositor-only). Use the same-document **View Transitions API** to morph the box into the detail card where available (Chrome/Edge 111+, Safari 18+, Firefox 144+, Baseline Newly available since Oct 2025) with a plain class-toggle fallback and `prefers-reduced-motion` honoured. Apply 3D transforms only to the active box, never to every spine (mobile GPU memory).
- **Filter dimming:** toggle `data-dim` on elements; style with `opacity` and `transition: opacity`. Avoid `filter: blur()` and `box-shadow` animation on hundreds of nodes. Leave dimmed items tappable.
- **Cost control:** `content-visibility: auto` on each per-location cabinet section; generate the wood texture once as a small tiled WebP (or CSS gradients) rather than SVG `feTurbulence` at runtime.
- **Detail card:** native `<dialog>` (focus trap, Escape, `::backdrop`) instead of a hand-built modal.
- **Delivery:** `MapStaticAssets` for fingerprinted CSS/JS; layout and game JSON endpoints behind output cache with `ETag`; strict CSP (`default-src 'self'`) is achievable **because** images are served from the app origin (see Images).
- **JS conventions:** the owner's "no `//` comments" rule is C#-specific in wording; for JS adopt the equivalent (`/** ... */` doc blocks only) and say so in the repo's contributing notes. Keep JS to a handful of ES modules (`cabinet.js`, `filters.js`, `detail.js`), no bundler.

### 4. Layout / packing: hand-written, in C#

- Candidate libraries do not match the problem. `RectpackSharp` 1.2.0 (C#, MIT, last release Jan 2024) is a port of `stb_rect_pack` and `potpack` 2.1.0 (JS, ISC, last release Jul 2025) / `maxrects-packer` 2.7.3 are texture-atlas packers: they minimise the area of one bounding rectangle with free rotation and arbitrary placement. A cabinet needs *rows* (shelf rows with fixed ceilings), cubby dividers, a mix of upright spines and face-out boxes, stacked horizontal piles, and expansions glued beside their base game. That is a constrained **shelf (level) packing** problem, which is an afternoon of code and is the norm for this kind of UI.
- **Approach:** (1) assign each item a size class from box dimensions when BGG has them (width/length/depth; unit as returned by BGG, convert once) or from a fallback heuristic (weight, player count, category), (2) decide presentation (face-out vs spine) by a seeded RNG keyed on the BGG id so the layout is stable between visits and syncs, (3) first-fit-decreasing-height style fill into shelf rows of the cabinet width, with cubby dividers inserted at deterministic intervals, (4) emit geometry in abstract "cabinet units". All of it is a pure function `(items, profile, seed) -> layout`.
- **Why C# not JS:** the owner is a .NET developer, xunit property-style tests (no overlaps, nothing out of bounds, deterministic for the same input, empty collection yields a valid empty cabinet, 0/1/50/600 items) run in milliseconds in the existing test runner, and the result is computed once per sync then cached, not per visitor.

### 5. Images

| Decision | Detail |
|----------|--------|
| Proxy/cache locally, do not hotlink | (a) Sync must keep working when BGG or its CDN is down, which hotlinks cannot do; (b) a strict CSP and no third-party requests means no visitor IPs leak to the CDN; (c) the dominant-colour step needs the pixels server-side anyway (cross-origin canvas reads are blocked without CORS headers); (d) BGG asks that traffic come from your server, not from clients, and hundreds of hotlinked images per page view multiplies CDN load; (e) own-origin images can be sized for the cabinet (two widths) instead of full-resolution originals. |
| Download rules | Only from an allowlisted host (`*.geekdo-images.com`), HTTPS only, byte cap (for example 10 MB), pixel-count cap before decode, no token on this client, same global spacing politeness (a 1 s gap is enough for the CDN, not the API). Download once per content change; discard originals after resizing. |
| Library | **SixLabors.ImageSharp 4.1.2**: managed, no native libs, so `dotnet publish -r linux-x64 --self-contained false` stays small and the lock files stay simple; WebP encoder built in. Licence condition above. |
| Output | `/img/{bggId}-{hash8}-{240|480}.webp`, quality about 80, served by static files with `Cache-Control: public, max-age=31536000, immutable` (the hash in the name busts the cache when art changes). 300 games at roughly 15 KB + 40 KB is under 20 MB of disk. |
| Alternatives | `SkiaSharp` 4.153.1 + `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.153.1 (MIT, native, fast): the right swap if the repo cannot carry an OSI licence. `NetVips` 3.2.0 + `NetVips.Native.linux-x64` 8.18.7 (libvips, lowest memory, LGPL native): overkill for a few hundred one-time images, and adds native assets to the release zip. Both are valid, neither is needed. |
| Dominant colour | Hand-written, about 40 lines: downscale to about 48 px, bucket pixels in a 4-bit-per-channel histogram, score buckets by population weighted by saturation (so a white border does not win), take the winning bucket's mean, then derive a spine background (clamped lightness) and pick black/white text by WCAG contrast. Stored in the snapshot. Not worth a package. |

Images are BGG user-submitted/publisher content; resizing for display is the standard practice for BGG-backed apps but is a judgement call against "do not modify". Keep the BGG credit and logo prominent and link each card to its BGG page (already a requirement).

### 6. Background sync: `BackgroundService` + `PeriodicTimer`

- **Choose** a single `BackgroundService` running `PeriodicTimer(TimeSpan.FromHours(1), timeProvider)` with a random start jitter (up to a few minutes, so restarts and releases never hit BGG on the hour boundary), and a bounded `Channel<SyncRequest>` (capacity 1, `DropWrite`) that both the timer and the manual endpoint write to. One consumer loop means **single-flight by construction**: no overlapping syncs, no locks.
- **Cooldown:** the manual endpoint checks `lastSyncStartedAt + cooldown` (global, not per client; for example 5 to 10 minutes) against `TimeProvider`, returns `429` with `Retry-After` when cooling down or when a sync is already running, otherwise enqueues. Persist `lastSyncStartedAt` in the snapshot so a restart does not reset the cooldown. Put the built-in fixed-window rate limiter in front of it as well.
- **Why not Hangfire (1.8.25) or Quartz (4.3.0):** both want a persistent job store (SQL/Redis/in-memory with no persistence), a dashboard to secure on a public host, and extra tables or schemas, to run one timer. The hosted-service pattern is already used in the reference project and needs nothing extra. Time-based tests use `FakeTimeProvider`, which the reference already references.
- Do the work in the background, never inside a request: the visitor-facing endpoints only read the in-memory snapshot, so BGG slowness is invisible to visitors.

### 7. Testing

| Layer | Tool | What it covers |
|-------|------|----------------|
| Unit | xunit.v3 4.0.1, FluentAssertions 8.11.0, NSubstitute 6.2.0, `FakeTimeProvider` | Packing (property-style invariants, determinism, 0/1/50/600 items), BGG XML parsing against **synthetic** fixtures (no real usernames or collections), 202/429/malformed-body handling with scripted handlers, cooldown logic, dominant-colour on generated bitmaps, snapshot atomic-write and corrupt-file recovery. |
| Integration | `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 | Endpoints, health, headers/CSP, rate-limit 429, "stale snapshot is still served when BGG fails" using a stub handler for the BGG client. No database means none of the reference's Postgres service container or test-database plumbing is needed in CI. |
| Browser | `Microsoft.Playwright.Xunit.v3` 1.63.0 | Real browser against a real Kestrel port. .NET 10 `WebApplicationFactory` supports `UseKestrel()` for this (the default in-memory `TestServer` has no port a browser can reach). Assert geometry from the DOM (no overlapping bounding boxes, everything inside the cabinet, dim class toggles, dialog opens on tap, narrow viewport uses the narrow layout, empty-collection state renders). Emulate a phone viewport and run Chromium; add WebKit later for Safari quirks. |
| Visual regression | Screenshots as **CI artifacts**, not pixel-diff gates | Playwright's `ToHaveScreenshot` baseline comparison is part of the Node test runner; the .NET page assertions only offer title, URL and aria snapshot (`ToMatchAriaSnapshotAsync`), verified in the .NET API docs. Cross-OS font rendering makes homemade pixel diffs flaky. Take screenshots at three viewports and upload them from CI for human review; gate on DOM invariants instead. |

CI shape (copy the reference): `ubuntu-24.04`, pinned action SHAs, `permissions: {}` at top level, `dotnet test --solution ... --no-restore` after a locked-mode restore inside the package script. Put the Playwright project in a **separate job** that installs Chromium (`pwsh bin/Release/net10.0/playwright.ps1 install --with-deps chromium`, `pwsh` is preinstalled on GitHub's Ubuntu runners) so ordinary unit/integration runs stay fast and offline-capable. Browser binaries and the Playwright driver are CI-only and never enter the release zip, which preserves "no CI code runs on the server".

## Release and Deploy Fit (from the reference project)

- Keep the reference flow: tag `vX.Y.Z` triggers `release.yml` (build, test, `actions/attest-build-provenance`, save the sigstore bundle, draft release with zip + sha256 + bundle), then a `deploy`-environment approval publishes. The server timer pulls, verifies offline, installs, health-checks, rolls back.
- **Simplifications vs the reference:** drop the PostgreSQL service container, `dotnet ef migrations bundle` and the migration manifest; the `release-manifest.json` can shrink to version + commit. The systemd unit keeps the same hardening (`NoNewPrivileges`, `ProtectSystem=strict`, `StateDirectory=`), with `EnvironmentFile=` holding only the BGG token, BGG username, contact URL and proxy settings.
- Outbound network needed from the LXC: `boardgamegeek.com` (API) and the image CDN host over HTTPS. Everything else can stay blocked.
- Public exposure: Traefik router with the reference's security-headers middleware (HSTS, nosniff, frame deny, referrer policy) but **without** the reference's LAN/VPN allowlist (this site is public); add a Traefik rate-limit middleware in front of `/api/sync` as defence in depth.

## Alternatives Considered

| Category | Recommended | Alternative | Why Not |
|----------|-------------|-------------|---------|
| BGG client | Own typed client + `XDocument` | `BoardGamer.BoardGameGeek` 0.10.0 | Pre-1.0, one maintainer, 500 ms/20-retry 202 default conflicts with BGG's 5 s guidance; usable if the owner insists. |
| BGG client | Own typed client | Scraping website JSON or logging in with credentials | Private, unlicensed endpoints; would store a personal credential on a public-facing host. |
| Persistence | JSON snapshot | SQLite / EF Core | Schema and native binary for a 300-object read-only dataset. |
| Persistence | JSON snapshot | shared database LXC | Cross-LXC availability and credential coupling; wrong direction for a public site. |
| Frontend | Razor Pages + vanilla JS | Blazor Server/WASM | Per-visitor circuit cost / multi-MB download. |
| Frontend | Razor Pages + vanilla JS | SvelteKit static SPA | Node toolchain and supply chain for one screen. Revisit if an editor UI is added. |
| Rendering | DOM + CSS | Canvas / SVG | A11y, text, hit-testing, DPR handling all worse for no performance win at this scale. |
| Packing | Hand-written shelf packer | RectpackSharp 1.2.0, potpack 2.1.0, maxrects-packer 2.7.3 | Atlas packers; wrong constraint model for shelves with cubbies and face-out/spine mixes. |
| Images | ImageSharp 4.1.2 | SkiaSharp 4.153.1 / NetVips 3.2.0 | Native assets in the release; use Skia only if the repo cannot be OSI-licensed. |
| Scheduling | `BackgroundService` + `PeriodicTimer` | Hangfire 1.8.25 / Quartz 4.3.0 | Persistent store and dashboard for a single hourly job. |
| Visual tests | DOM invariants + CI screenshot artifacts | Pixel-diff baselines | `ToHaveScreenshot` is not available in Playwright .NET; custom diffs are flaky across OSes. |

## What NOT to Use

| Avoid | Why | Use Instead |
|-------|-----|-------------|
| Calling BGG from the browser | Violates BGG's server-side/caching rule and would expose the token | Server-side sync, snapshot |
| Sending the BGG token to any non-BGG host (including the image CDN) | Token leak | Separate token-less `HttpClient` for images |
| `www.boardgamegeek.com` as the API host | BGG: "may interfere with request authorization" | `https://boardgamegeek.com/xmlapi2/` |
| Replaying a BGG website login (password + session cookies) for private fields | Private, unlicensed endpoints; credential for a personal account on the server | Comment convention, or test whether the token alone returns `privateinfo` |
| Stock `AddStandardResilienceHandler()` with defaults | 2 s base delay, 10 s attempt timeout and 3 retries are too tight for queued BGG calls, and 202 is not retried | Tuned options plus an explicit 202 poll loop and a 5 s spacing handler |
| `versions=1` on `thing` for 20 ids at once | Returns every printing; huge payloads for popular games | `version=1` on the collection call (owner's chosen version) plus heuristic fallbacks |
| Hotlinked box art in the page | CDN outage = broken cabinet, third-party requests, CSP holes, no colour extraction | Local resized WebP cache |
| Blazor Server for the public page | Per-visitor SignalR circuit on a low-power host | Razor Pages + vanilla JS |
| EF Core / migrations / efbundle | There is nothing relational to migrate | JSON snapshot |
| Hangfire / Quartz | Job store + dashboard for one timer | `BackgroundService` + `PeriodicTimer` |
| CSS/SVG `feTurbulence` wood grain computed live, `filter: blur` on all items, 3D transforms on every spine | Mobile GPU/memory cost | Pre-rendered tiled texture; `opacity` dimming; 3D only on the active box |
| A GitLab runner or any self-hosted runner | Out of scope per the owner and the security model | GitHub-hosted runners, attested releases, pull-based deploy |
| Pixel-diff visual regression gating in CI | Not supported by Playwright .NET, flaky across fonts/OS | DOM/geometry assertions + screenshot artifacts |
| A public documented JSON API of raw BGG data | BGG forbids third-party relaying services | Minimal, undocumented UI-only endpoints |

## Installation

```bash
# Service project
dotnet add GamesCabinet.Service package Microsoft.Extensions.Http.Resilience --version 10.10.0

# Repository project (image pipeline)
dotnet add GamesCabinet.Repository package SixLabors.ImageSharp --version 4.1.2

# Unit tests
dotnet add GamesCabinet.UnitTests package xunit.v3 --version 4.0.1
dotnet add GamesCabinet.UnitTests package FluentAssertions --version 8.11.0
dotnet add GamesCabinet.UnitTests package NSubstitute --version 6.2.0
dotnet add GamesCabinet.UnitTests package Microsoft.Extensions.TimeProvider.Testing --version 10.10.0
dotnet add GamesCabinet.UnitTests package RichardSzalay.MockHttp --version 7.1.0   # optional

# Integration tests
dotnet add GamesCabinet.IntegrationTests package xunit.v3 --version 4.0.1
dotnet add GamesCabinet.IntegrationTests package FluentAssertions --version 8.11.0
dotnet add GamesCabinet.IntegrationTests package Microsoft.AspNetCore.Mvc.Testing --version 10.0.12
dotnet add GamesCabinet.IntegrationTests package Microsoft.Extensions.TimeProvider.Testing --version 10.10.0

# Browser tests (separate project and CI job)
dotnet add GamesCabinet.UiTests package Microsoft.Playwright.Xunit.v3 --version 1.63.0
dotnet add GamesCabinet.UiTests package xunit.v3 --version 4.0.1
dotnet add GamesCabinet.UiTests package Microsoft.AspNetCore.Mvc.Testing --version 10.0.12
pwsh GamesCabinet.UiTests/bin/Release/net10.0/playwright.ps1 install --with-deps chromium
```

Regenerate every `packages.lock.json` after adding packages (reference convention: `RestorePackagesWithLockFile`, `dotnet restore --locked-mode` in the release script). `Microsoft.EntityFrameworkCore.*`, `Npgsql.*` and the `dotnet-ef` local tool from the reference are **not** carried over.

## Version Compatibility

| Package | Compatible With | Notes |
|---------|-----------------|-------|
| `Microsoft.Extensions.Http.Resilience` 10.10.0 | .NET 10 / `Microsoft.Extensions.Http` 10.0.x | Polly 8.8.0 under the hood; use the 10.x line to match the runtime. |
| `SixLabors.ImageSharp` 4.1.2 | .NET 8+ (4.0 moved to net8 baseline) | 4.x is a major version over the 3.1.x most blog posts cover; re-check API names (WebP casing changed in 4.0) against 4.x docs rather than older snippets. |
| `Microsoft.AspNetCore.Mvc.Testing` 10.0.12 | `WebApplicationFactory.UseKestrel()` (new in .NET 10) | Calling `WithWebHostBuilder` on a Kestrel-configured factory yields a TestServer-backed factory (known issue); configure everything before `UseKestrel`. |
| `Microsoft.Playwright.Xunit.v3` 1.63.0 | `xunit.v3` 4.0.1 | Verify the exact xunit.v3 range at install time; both packages ship frequently. |
| `xunit.v3` 4.0.1 | `Microsoft.Testing.Platform` runner via `global.json` | Same as the reference. |

## Open Verification Items (resolve in the first data-facing phase, with a live token)

1. **Token alone vs `showprivate=1`:** does the owner's own collection returned with only the Bearer token include `<privateinfo>`/`inventorylocation`? (Decides plan B.) LOW until tested.
2. **Collection `<comment>` is returned** for items with a public comment, and its exact element shape.
3. **Box dimensions:** with `version=1` on the collection call, how often are width/length/depth present, and in what unit? Decides how much heuristic sizing the packer needs.
4. **Real 202/429 behaviour** from the home connection: poll counts, whether `Retry-After` ever appears, whether the API tolerates 5 s spacing in practice.
5. **Image CDN behaviour:** hotlink/CORS rules, available size variants (BGG exposes both `image` and `thumbnail` URLs), and the real byte sizes.
6. Re-read the official usage page and terms immediately before launch; BGG states both can change at any time.

## Sources

- BGG, Using the XML API (version date 2025-07-02), via Internet Archive capture 2026-09-24: https://web.archive.org/web/20260924202844/https://boardgamegeek.com/using_the_xml_api (official, HIGH; live page is Cloudflare-gated for automated fetch)
- BGG, XML API Terms of Use (capture 2026-09-24): https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use (official, HIGH)
- BGG XML API2 reference (capture 2026-09-30): https://boardgamegeek.com/wiki/page/BGG_XML_API2 (official, HIGH for parameters; wiki is partly community-maintained)
- Community confirmation of 429 without `Retry-After`, 20-id limit and non-commercial terms summary: https://boardgamegeek.com/thread/2065587/wbggs-move-to-cloud-xmlapi-may-now-return-status-4 and https://github.com/JacobStephens2/keeplore/issues/88 (MEDIUM/LOW, secondary)
- `BoardGamer.BoardGameGeek` repository and README (MIT, bearer token support from 0.9.0, retry defaults): https://github.com/Cobster/BoardGamer.BoardGameGeek ; NuGet registration for 0.10.0 (MEDIUM)
- Microsoft Learn, Build resilient HTTP apps (standard resilience handler defaults, retried status codes; updated 2026-03): https://learn.microsoft.com/en-us/dotnet/core/resilience/http-resilience (HIGH)
- Six Labors Split License v1.0 (verified text): https://github.com/SixLabors/ImageSharp/blob/main/LICENSE ; ImageSharp 4.0.0 to 4.1.2 release notes: https://github.com/SixLabors/ImageSharp/releases (HIGH)
- NuGet / npm registries queried 2026-10-03 for all package versions listed above (HIGH)
- Playwright .NET PageAssertions (no screenshot assertion): https://playwright.dev/dotnet/docs/api/class-pageassertions (HIGH)
- `WebApplicationFactory.UseKestrel()` in .NET 10: https://blog.safia.rocks/2025/11/10/aspnetcore-ten/ and https://github.com/dotnet/aspnetcore/issues/69655 (MEDIUM)
- View Transitions Baseline status: https://web.dev/blog/same-document-view-transitions-are-now-baseline-newly-available (HIGH)
- MDN container query length units (`cqw`, `cqi`, ...): https://developer.mozilla.org/en-US/docs/Web/CSS/CSS_containment/Container_queries (HIGH for units; baseline date from general knowledge, MEDIUM)
- Reference project (read directly): `Directory.Build.props`, `global.json`, `*.csproj`, `.github/workflows/ci.yml` and `release.yml`, `build/package-release.sh`, `deploy/systemd/ledger.service`, `.claude/CLAUDE.md` (HIGH)

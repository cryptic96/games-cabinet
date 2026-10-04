# Pitfalls Research

**Domain:** Public, read-only .NET 10 web app that syncs a BoardGameGeek (BGG) collection and renders it as a dynamically drawn virtual cabinet, self-hosted on a low-power Proxmox LXC behind an existing Traefik, deployed by a pull-based attested-release pipeline copied from ing-dashboard.
**Researched:** 2026-10-03
**Confidence:** MEDIUM-HIGH. BGG access rules, terms and API quirks are HIGH (official BGG wiki pages, read via Internet Archive captures because BGG's Cloudflare returns 403 to non-browser clients; I did not try to bypass it). BGG behaviour changes of September 2026 (Cloudflare challenge, inventory-location unavailability) are MEDIUM/LOW (dated third-party reports, a few forum snippets I could not open). Rendering/accessibility/hardening guidance is HIGH where tied to a spec or vendor doc and otherwise reasoned (flagged).

## How to read the phase labels

The roadmapper owns the final phase list. These labels are used for mapping and assume this natural order:

| Label | Meaning |
|-------|---------|
| **P-Repo** | Repo creation, privacy guardrails, CI, release pipeline, walking-skeleton deploy (hello-world through Traefik, LAN-only) |
| **P-Sync** | BGG access, sync worker, snapshot, image pipeline, location parsing |
| **P-Layout** | Deterministic packing engine as a pure function with golden tests (no pixels yet) |
| **P-Render** | Cabinet rendering, pull-out animation, detail card, accessibility, mobile performance |
| **P-Views** | Filters, per-location cabinets, mobile reflow, empty/sparse states, polish |
| **P-Harden** | Public exposure: Traefik router, headers, rate limits, firewall, go-public switch |
| **All** | Cross-cutting, enforce in every phase |

---

## Critical Pitfalls

### Pitfall 1: BGG is token-gated, approval is a calendar dependency, and Cloudflare may challenge your server

**What goes wrong:** Since 2025 every XML API request needs an `Authorization: Bearer <token>` from a registered application. Unauthenticated calls get HTTP 401. The official guide (version date 2025-07-02) says approval "may be a week or more." The plan assumes sync works, so the whole core value is blocked until a human at BGG approves. Separately, BGG sits behind Cloudflare: clients that fake a browser User-Agent get HTTP 403 with `cf-mitigated: challenge` before the token is even read, and one report says a hosted server address was still challenged (a different problem from the User-Agent one). One project observed its token's usage drop to zero from 2026-09-09 with no deploy on its side, which suggests BGG tightened something around then (single source, treat as LOW-MEDIUM).

**Why it happens:** Developers register late, test from a laptop on a residential connection and from a browser, then discover that the server's egress address, User-Agent or `www.` host behaves differently. Docs say to use `boardgamegeek.com` WITHOUT `www` because it "may interfere with request authorization."

**How to avoid:**
- Register the application on day one (non-commercial, public-facing hobby site, describe the read-only snapshot design and caching). Do this before writing any sync code; it is the longest-lead item in the project.
- Early spike, from the actual LXC (not a laptop): one `collection` call and one `thing` call with the real token, an honest descriptive User-Agent (project name plus public repo URL placeholder, never a browser string), host `boardgamegeek.com`, no redirects followed silently. Record: status codes, headers, whether Cloudflare challenges the home egress address.
- Treat 401/403 as "token or edge problem, alert the owner and stop," never as "retry soon." A retry loop on a revoked token or a challenge page is how you get banned.
- Do not hard-code the token expectation forever: the page states "this may change (as may anything else)." Keep the auth header injection in one place.
- Document a contingency (see Recovery Strategies): serve last snapshot indefinitely; manual import of an owner-downloaded export as degraded mode.

**Warning signs:** 401 from curl while the token "looks active"; 403 with `cf-mitigated: challenge`; sync works on the dev machine and fails on the LXC; the BGG application "Usage" page shows no calls.

**Phase to address:** **P-Sync** (registration action is a P-Repo/day-one task; the access spike is the first task of P-Sync and gates its plan).

---

### Pitfall 2: Storage location ("inventory location") is not available through the token API; the cookie workaround is unlicensed and a credential liability

**What goes wrong:** The plan reads a private BGG field. The official API2 wiki documents `showprivate=1` as "Only works when viewing your own collection and you are logged in (include cookies from logging into the website in request)." The usual workaround is POSTing username and password to BGG's private login endpoint and replaying the session cookies. Problems stack up:
1. BGG's guide states that its private site APIs are not licensed: "Unless otherwise noted or authorized, we are granting no license for use of those endpoints." Automated login is a private endpoint. The application token does not unlock private info.
2. It means storing the owner's BGG password (or a long-lived cookie; reports say the password cookie lasts about a month) on an internet-facing server. A leak is an account takeover, not an API-key rotation.
3. Forum reports (MEDIUM/LOW, threads I could not open) indicate inventory location is not exposed by the XML API or CSV export even with `showprivate=1`; only fields such as price paid, acquisition date and private comment appear in `<privateinfo>`. Confirm with the owner's real data in the spike before building anything on it.

**Why it happens:** The field exists in the BGG UI, so it is assumed to exist in the API.

**How to avoid:**
- Decide Plan B in the P-Sync spike, with evidence. Recommended default: **public comment convention** (e.g. a line `Location: <name>` in the per-item public comment, which the collection endpoint returns as `<comment>` without login). It keeps the app database-free and credential-free.
- Never store a BGG password or session cookie on the server. Reject the cookie approach in the project's key decisions.
- Parse defensively: take only the matched segment after the `Location:` marker; never render the whole comment (it is public user text and may contain anything). Normalize case/whitespace, map aliases through server-side config (`shelf a` vs `ShelfA`), and put unknown or missing locations in an explicit "Unsorted" bucket instead of dropping games.
- Do NOT request `brief=1` if you read comments: brief returns abbreviated results (verify in the spike whether `<comment>` survives).
- If a database-backed in-app editor (Plan B (b)) is chosen instead, it brings auth, CSRF, backups and a migration pipeline; treat that as a scope increase (see Pitfall 15).

**Warning signs:** `<privateinfo>` absent or lacking a location attribute in a real response; a design doc that says "log in to BGG from the server"; a password field in the env example.

**Phase to address:** **P-Sync** (decision recorded before layout work, because the per-location cabinet view depends on it).

---

### Pitfall 3: Hammering BGG (restart loops, manual sync amplification, retry storms)

**What goes wrong:** BGG says it throttles "if you send requests too frequently" with 500/503 ("a 5-second wait between requests seems to suffice," official wiki, no numeric limit published), forum reports describe 429, and the terms warn that traffic can be "grounds for having your license suspended." Ways this project can generate excess traffic:
- **Sync on start + `Restart=always`:** the ing-dashboard unit file restarts the service every 10 s; a crash loop with sync-on-start is a request every 10 s. Every deploy restart also triggers an immediate sync.
- **Manual "sync now" per visitor:** an unauthenticated POST that synchronously calls BGG is an amplification vector, and bots/prefetchers hit GET endpoints.
- **Retries multiplying:** resilience handlers (retry x3, per-batch) multiply calls exactly when BGG is struggling.
- **Polling 202 too tightly:** the collection endpoint answers 202 while it queues the request; the official wiki says to keep retrying "hopefully w/some delay."
- **Batching mistakes:** `thing` accepts a maximum of 20 ids per request (official); more is rejected or errors.

**How to avoid:**
- One `BggSyncCoordinator` (single-flight) is the only code that talks to BGG. All triggers (timer, manual, startup) enqueue into it; concurrent triggers coalesce into one run.
- Persist `lastSyncAt` and `nextAllowedAt` in the state directory, so restarts and deploys do not reset the cooldown. Sync on start only when the snapshot is older than the interval; add start-up jitter.
- Manual button: `POST` only, returns immediately (202 plus status JSON), enforces a **global** cooldown server-side (the PROJECT already says global); UI disabling is cosmetic. The same limiter guards the hourly timer. Add a small per-IP limit on the endpoint itself so spamming the cheap "cooling down" response is also bounded.
- Respect a 5 s minimum gap between BGG calls (`SemaphoreSlim` plus delay), honour `Retry-After` when present, and on any 429/500/503 extend `nextAllowedAt` with exponential backoff and a hard per-sync request budget. No unbounded retry loops. 202 polling: start about 3-5 s, back off, give up after about 1-2 minutes and keep the last snapshot.
- Steady-state budget is tiny: 2 collection calls (games excluding expansions, expansions only) plus `thing` calls only for ids new to the cache or past a long TTL (weekly, spread out). A 300-game first sync is about 2 + 15 `thing` calls, roughly two minutes at 5 s spacing. Refreshing all `thing` data every hour would be a self-inflicted 15x multiplier.
- Cap the sync interval floor in code (for example never below 15 minutes) even if config says otherwise.

**Warning signs:** BGG "Usage" page shows hundreds of calls per day; journal shows sync starting after every deploy or crash; 202 loops in logs; manual-sync endpoint reachable by GET.

**Phase to address:** **P-Sync** (coordinator, persistence, backoff); **P-Harden** (verify the endpoint is abuse-resistant from outside).

---

### Pitfall 4: Replacing a good snapshot with a bad one (empty, partial, error XML)

**What goes wrong:** BGG can answer 200 with an `<errors>` body or an empty `<items totalitems="0">`, return 202 mid-flow, time out half-way through `thing` batches, or return malformed/oversized XML. If the sync writes whatever it parsed, the public cabinet suddenly shows zero games, or half. The owner's collection is also legitimately small and growing, so "few games" is not itself an error.

**How to avoid:**
- Build the new snapshot completely in memory/temp file, validate, then atomically swap (write temp, `File.Move(..., overwrite: true)`). Keep the previous snapshot and serve it until the swap.
- Sanity guard: reject (keep last good, raise an alert, expose "sync failing" on the ops endpoint) when the new collection has zero items while the old one had some, or drops by more than a configured fraction in one sync. Allow an explicit owner override (config flag) for legitimate big removals.
- Parse XML with a hardened reader (`DtdProcessing.Prohibit`, bounded size, tolerate illegal characters by not hard-failing on a single bad item). A single bad item must degrade that item (fallback name/art), not abort the sync.
- Show "last synced X ago" in the UI; if older than a threshold (for example 24 h) show a quiet stale note, never an error page.
- Snapshot file carries a schema version. Because releases can roll back automatically (the ing-dashboard installer reactivates the previous release), an older release must not crash on a newer snapshot: treat the snapshot as disposable derived data, version the filename (`snapshot.v<N>.json`), and on mismatch ignore it and resync.

**Warning signs:** public page ever renders an empty cabinet after having had games; snapshot file modified time changes while sync log says failure; rollback leaves service unhealthy because of unreadable snapshot.

**Phase to address:** **P-Sync**.

---

### Pitfall 5: BGG collection data model traps

**What goes wrong:** Several documented and reported quirks silently corrupt the cabinet:
- **Expansions mislabeled as games.** Official wiki: "the default (or using subtype=boardgame) returns both boardgame and boardgameexpansion's in your collection... but incorrectly gives subtype=boardgame for the expansions. Workaround is to use excludesubtype=boardgameexpansion and make a 2nd call asking for subtype=boardgameexpansion." Using only `subtype=boardgame` puts every expansion on a face-out shelf.
- **Duplicates.** Each collection entry has its own `collid`; owning two copies or two versions yields repeated `objectid` entries. Dedupe by `objectid` (keep count and version info), or the same box appears twice.
- **`own=1` is a filter, not a guarantee.** Also check `<status own="1">` on each item. Wishlist, preordered, for-trade, previously-owned must never render (PROJECT: owned only).
- **`modifiedsince` does not report deletions** (official). Do not do incremental collection sync; the collection fetch is cheap, always pull it in full and diff locally.
- **Expansion linkage is lists of ALL expansions, not owned ones.** A popular base game links to dozens or hundreds of expansions on BGG; the cabinet must intersect with the owned expansion set. Also handle orphans (owner has the expansion but not the base game), expansions that attach to several base games, and non-standard item types (integrations, compilations, accessories). Decide a rule for each: orphan expansions become standalone thin spines in an "unattached" cluster.
- **`versions=1` on `thing` is a payload bomb.** It returns every version of each item (popular games have very many) for up to 20 items per request. Use the collection's `version=1` instead, which returns the owner's chosen version only.
- **Weight needs `stats=1`** on `thing`. Player count and play time are in the base response.
- **Dimensions are sparse and may be wrong.** Version records carry width/length/depth but many are zero, missing, swapped, or in a unit you did not expect (confirm units and presence in the spike against a real response; this research could not fetch one). The collection only has version data when the owner picked a specific version for the item. Pre-clamp to a sane range and fall back to category defaults, flagging "estimated."
- **HTML entities.** Official enhancement wiki documents double-escaping in descriptions (e.g. `&amp;amp;`, mojibake like `Ã©`). Names are mostly fine after XML decoding but descriptions are not. Easiest prevention: the detail card does not show descriptions at all. If any BGG text is decoded, `HtmlDecode` exactly once and render as text, never as HTML.
- **Primary vs alternate names.** `thing` returns several names; use the primary one, and keep BGG's sort index handling for leading articles if you sort.
- **Missing images.** Some items have no image or thumbnail; the pipeline needs a generated fallback (spine-only).
- **Collection freshness.** After editing on BGG, the change can lag; the manual sync may show nothing new. Say so in the UI copy ("BGG may take a few minutes"); do not retry-spam.

**Warning signs:** expansions on face-out shelves; same game twice; 20+ expansions listed in a detail card; first sync takes minutes and megabytes per request; names showing `&amp;`.

**Phase to address:** **P-Sync** (fixtures for every quirk, synthetic); **P-Layout** (orphans, dimension fallbacks).

---

### Pitfall 6: Breaking BGG's terms (and getting the application revoked)

**What goes wrong:** The license is narrow and revocable ("BGG shall have the right to terminate this license for any or all users at any time and for any reason"). Rules that matter for this site, all from the official Terms of Use, Commercial Use and "Using the XML API" pages:
- **Strictly non-commercial.** Commercial includes "Taking payments," "Showing ads," "Selling merchandise, directly or indirectly." A voluntary donation button also needs a commercial license (likely free, but still a license). So: no ads, no tip jar, no affiliate "buy this game" links, no sponsor banners.
- **Credit BGG by name and show the "Powered by BGG" logo, linked back to BoardGameGeek, on public-facing uses, sized so text is easily legible.** Logo assets are linked from the official pages. Pitfall: logo hidden on mobile, covered by cabinet art, tiny, or unlinked. Put it in a persistent footer.
- **"You may not modify the data... in any way."** Display-only transformations (decode, format, lay out) are normal; do not rewrite names, translate, or "fix" values on screen. Keep the BGG link on each card.
- **No relaying.** "Third party services that allow other applications (not end users) to access our data are strictly prohibited." Keep the site's JSON same-origin, no open CORS, no documented public API, no bulk download endpoint.
- **No AI/LLM training with the data**, no use of private site APIs (Pitfall 2), no scraping of anything the XML API does not expose.
- **Server-side requests with caching**; client-side requests "may result in too much traffic, which could be grounds for having your license suspended." The word "resources" in that sentence arguably covers images (Pitfall 8).
- **Images are a gray area.** The official pages say nothing explicit about third-party display of API-provided image URLs. Treat image display as covered by "reproduce and display the data available through the BGG XML API" but keep it minimal: downscale only, no cropping or overlays that obscure the art, keep attribution. If unsure, ask BGG through the application's contact path. (MEDIUM: interpretation, not an official statement.)
- The page also says the API and policies can change at any time and that business use is "at your own risk." Fine for a hobby, but plan for graceful degradation.

**Warning signs:** a "support the site" link; any `Access-Control-Allow-Origin: *` on data endpoints; a README that documents the site's JSON as an API; logo missing from the rendered page at 360 px width.

**Phase to address:** **P-Render** (footer, attribution); **P-Views** (check on mobile); **P-Harden** (CORS audit); **All** (no monetization features).

---

### Pitfall 7: Non-deterministic or churny layout

**What goes wrong:** The cabinet "reshuffles" on every load or restart, or adding one game reorders everything. Friends who remember "the red spines are on the top shelf" lose trust; screenshots and bug reports cannot be reproduced; tests are flaky.

**Why it happens in .NET specifically (verified against Microsoft docs):**
- `string.GetHashCode()` is randomized per process; Microsoft says hash codes "should never be persisted" and two runs can differ. Seeding from it reshuffles on every restart.
- `new Random(seed)` is repeatable on one runtime, but the Random class notes say the algorithm "isn't guaranteed to remain the same across major versions of .NET." A .NET upgrade silently reshuffles every layout.
- `List<T>.Sort` / `Array.Sort` are unstable: equal keys may swap between runs or versions. `OrderBy` is stable, but only if you also add a total tiebreaker.
- Dictionary/HashSet enumeration order, parallel loops and floating-point accumulation order can all change results.
- Layout computed client-side from the same data on different devices produces different results per viewport unless the algorithm is explicitly a pure function of (data, viewport class).

**How to avoid:**
- Layout is a **pure function** `Layout(games, config) -> placements`, implemented in plain C# with its own tiny PRNG (e.g. SplitMix64/xorshift written in the repo) seeded from a stable hash of the BGG object id (FNV-1a or XxHash on the id's bytes), never from `GetHashCode` or `Random`.
- Total ordering everywhere: sort by (stable key, BGG id). No unstable sort; no reliance on dictionary order.
- Aim for **stability under insertion**: adding or removing one game should move as few boxes as possible (e.g. placement driven per shelf with deterministic hashes, sticky assignment of "face-out vs spine" per id, first-fit in a stable order), not reflow the whole cabinet. Define this as an explicit property and test it ("add one game, at most N boxes change shelf").
- Compute layout once per sync into the snapshot (versioned), do not recompute per request. The client renders, it does not decide.
- Golden-file tests: fixed synthetic collections (0, 1, 3, 50, 400 games, one giant box, all-identical sizes) produce byte-identical layout JSON; any change is a visible diff in review.
- Separate layouts per viewport class (desktop/mobile) are separate pure inputs, not client-side randomness.

**Warning signs:** two page loads differ; layout changes after service restart or after a .NET patch update; golden tests that "sometimes fail."

**Phase to address:** **P-Layout** (design property up front); **P-Sync** (snapshot carries layout version).

---

### Pitfall 8: Image handling: hotlinking, oversized originals, layout shift, arbitrary-resize DoS

**What goes wrong:**
- **Hotlinking** BGG's image CDN from the visitor's browser multiplies BGG traffic by your visitor count (the terms warn about client-side traffic), exposes visitors' IPs to a third party, and has a history of Referer-based 403s (an old forum thread reports API image links returning 403 for third-party apps; BGG also says elsewhere "please don't hotlink the images"; both older, treat as MEDIUM).
- **Original images are huge** (the API's `image` is the full-size upload, often multi-megabyte; the `thumbnail` is tiny). Phones decode `width x height x 4` bytes per image: a 2000x2000 original is about 16 MB decoded. A cabinet with 100 face-out boxes at that size will crash iOS Safari.
- **Layout shift:** the API gives no image dimensions, so the browser cannot reserve space and shelves jump while loading.
- **Resize-on-demand endpoint** (`/img?url=...&w=...`) is an open proxy (SSRF) and an unbounded CPU sink on a low-power host.
- **Native image libraries in a minimal LXC** can fail at runtime (missing system libs) even though they pass on a dev machine; some imaging libraries also have licensing terms to check (verify the license of whichever is chosen against a public hobby repo).
- **Cache busting:** if the image URL does not change when content changes, stale art persists; if it changes on every sync, caches never hit.

**How to avoid:**
- Server fetches each image once (when a game is new or its BGG image URL changed), with the honest User-Agent, only from URLs found in BGG XML responses and only for an allowlisted BGG image host (never from request input). Serialize downloads and decoding (one at a time, bounded memory) with a pause, same politeness as API calls.
- Pre-generate fixed variants at sync time (for example 2 widths around 160 and 320 px, modern format with a fallback), store them in the state directory, and serve them as static files with content-hashed names and `Cache-Control: public, max-age=31536000, immutable`. No dynamic `w=` parameter. Under CSP this also lets `img-src 'self'`.
- Store each variant's pixel dimensions in the snapshot; render with explicit `width`/`height` or `aspect-ratio` so there is zero shift. Reserve every shelf's height from the layout engine, not from loaded content.
- Missing/failed image: deterministic placeholder (generated spine/face with title), never a broken-image icon.
- Evict images no longer referenced by the snapshot; cap total disk.
- Test the imaging stack inside the same LXC image the release runs on, in the first walking-skeleton release, not at the end.

**Warning signs:** DevTools shows requests to the BGG image host from the browser; a single image over 300 KB on mobile; CLS above about 0.1 in Lighthouse; the app container memory spikes during sync.

**Phase to address:** **P-Sync** (pipeline, variants, dimensions); **P-Render** (srcset, aspect-ratio); **P-Harden** (confirm no open proxy).

---

### Pitfall 9: Mobile performance with many images and CSS 3D transforms

**What goes wrong:** The desktop demo is smooth; a mid-range phone with 150-400 games stutters, runs hot, or reloads the tab. Typical causes: hundreds of GPU-promoted layers (`will-change: transform` everywhere), a drop shadow or blur filter on each box, a large wood-texture bitmap repeated, `perspective` on a huge container, animating layout properties instead of `transform`/`opacity`, and decoded image memory growing with every face-out box (see Pitfall 8; reports say iOS Safari's per-tab memory ceiling is strict, MEDIUM).

**3D gotcha:** `transform-style: preserve-3d` is flattened by ancestors with `overflow` other than visible, `opacity` below 1 or a `filter`. So "dim non-matching games" implemented as `opacity` on a shelf wrapper, or a scroll container with `overflow: hidden`, silently kills the 3D pull-out.

**How to avoid:**
- Keep the resting cabinet **flat** (plain absolutely/grid-positioned elements or one SVG/canvas layer). Apply 3D only to the single box being pulled out, on a transient overlay element.
- Promote layers on demand (`will-change` set just before animation, removed after). Box shadows from a few shared gradients or a pre-rendered shelf shadow, not per-box filters.
- Virtualize: `content-visibility: auto` with `contain-intrinsic-size` on each shelf, `loading="lazy"` and `decoding="async"` on images, `srcset` so phones get the smaller variant.
- Wood texture as CSS gradients or a small tiled image (a few KB), not a full-viewport JPEG.
- Use `dvh`/`svh`, not `100vh`, for the narrow tall mobile cabinet.
- Define a budget and test it: for example 400-game synthetic collection, mid-range Android and an older iPhone, scrolling stays smooth, page weight on first view under a few MB, no tab reload. Add a synthetic data generator early so this is testable before the owner's real collection exists.
- Prefer server-rendered markup of the layout (HTML from the snapshot) over client frameworks that must hydrate hundreds of components. Avoid stateful circuit-per-visitor server UI models for a public low-power host (every visitor holds server memory and a persistent connection through Traefik; this is reasoning from how such frameworks work, flag for the stack decision).

**Warning signs:** frame drops while scrolling on a phone; DevTools Layers shows hundreds of composited layers; the phone heats up; the pull-out animation has no depth.

**Phase to address:** **P-Render**; **P-Views** (mobile reflow budget re-check).

---

### Pitfall 10: Accessibility of a visual-only metaphor

**What goes wrong:** A cabinet drawn as decorative shapes is invisible or unusable to keyboard and screen reader users, and parts are hard even for sighted users.
- **Spine text unreadable:** generated colours with arbitrary text colours fail contrast (WCAG 1.4.3: 4.5:1 for normal text). Mid-tone backgrounds are the trap because neither black nor white reaches 4.5:1. Long titles overflow; non-Latin names fall back to missing glyphs; web-font swaps change text width after load.
- **Thin expansion spines** are tiny touch targets. WCAG 2.2 Target Size (Minimum, AA) requires at least 24 by 24 CSS px, or the spacing exception where a 24 px circle centered on each undersized target does not intersect another target or circle. Packed-together spines never satisfy the spacing exception.
- **Keyboard and screen reader:** hundreds of tab stops, no semantic structure, a custom pull-out card that traps focus or loses it.
- **Motion:** the pull-out animation ignores `prefers-reduced-motion`. MDN's guidance is to replace scaling/panning/transforms with gentler alternatives such as opacity fades.
- **Dimming as the only filter signal:** non-matching games just fade; assistive tech and low-vision users cannot tell what matches.

**How to avoid:**
- Pick spine colours from a **curated palette** whose entries each have a precomputed text colour with at least 4.5:1, enforced by a unit test over the whole palette. Do not derive spine colours from box-art averages without the same check.
- Spine text: deterministic fit rules (shorten at the first `:` or dash subtitle, clamp lines, minimum font size around 11-12 px), full title always in `aria-label` and a tooltip; self-host a font (no third-party font requests) or use a system stack, with synthetic fixtures covering very long, CJK, emoji and RTL names.
- Expansion spines: minimum 24 px hit area on touch; if layout cannot allow it, make the expansion cluster one target that opens the base game's card with the expansions listed. Do not rely on 1.4.11/2.5.8 pass by "it looks fine."
- Semantic backbone: render each shelf as an ordered list of real `<button>`s in visual order, with accessible names like "Title, 2 to 4 players, 60 minutes." Provide a skip link past the cabinet. Open the detail card as a native `<dialog>` opened with `showModal()` (focus trap, Esc, focus restoration are built in). Visible focus ring that meets 3:1 on the wood background.
- Filtering: announce "N of M games match" in an `aria-live="polite"` region; keep non-matching games out of the tab order (or mark them) rather than only lowering opacity; keep dimmed contrast acceptable.
- Reduced motion: honour the CSS media query AND check `matchMedia('(prefers-reduced-motion: reduce)')` before starting any JavaScript or Web Animations API motion (CSS alone does not stop script-driven animation); listen for changes mid-session.
- Run axe-core in automated tests on the filled, sparse and empty states.

**Warning signs:** Lighthouse/axe contrast failures; Tab key needs 200 presses to reach the footer; pull-out card does not return focus; users on iOS cannot hit an expansion spine.

**Phase to address:** **P-Render** (semantics, spines, dialog, motion); **P-Views** (filter announcements, mobile touch targets).

---

### Pitfall 11: Treating a public homelab exposure like the LAN-only ing-dashboard

**What goes wrong:** ing-dashboard is LAN/VPN-only except one OAuth endpoint, so its Traefik example ships an IP allowlist and security headers but little public-internet hardening. This site is fully public. What typically goes wrong when one more public router is added to an existing Traefik:
- **App reachable while bypassing Traefik.** Without a host firewall, anything on the LAN/VPN/IoT segment can hit the app port directly and skip headers and rate limits. ing-dashboard solves this with a default-drop nftables firewall that only lets the Traefik address reach the app port. Copy that.
- **Forwarded headers misconfigured.** Since .NET 8 the middleware ignores `X-Forwarded-*` from unknown proxies (official breaking change), so if you do not configure the Traefik address as a known proxy every client appears to be Traefik: per-IP limits and logs are meaningless. If you "fix" it by trusting everything, clients can spoof their IP. In .NET 10 `KnownNetworks` is obsolete in favour of `KnownIPNetworks` (there is a reported gotcha around `Clear()`), so copied .NET 8/9 snippets may warn or misbehave. Verify with a test through the real router.
- **Rate limiting keyed on the wrong address.** Traefik's `rateLimit` groups by source; behind another proxy or CDN all clients share one key unless `ipStrategy.depth` is set (Traefik docs). With UniFi port forwarding the client address is normally preserved, but LAN clients that reach the public name via hairpin NAT will not be.
- **Ops endpoints exposed.** Routing a path prefix or the whole app exposes `/health` and `/metrics`. ing-dashboard keeps health/metrics on a separate loopback-only port, with no router for it. Do the same; health output must not carry versions, paths or exception text.
- **Security headers copied blindly.** The ing example uses `stsPreload: true` and `stsIncludeSubdomains: true`; preload is effectively permanent and should not be set casually on a shared domain. Add a Content-Security-Policy suited to this app (self-hosted images and fonts make `default-src 'self'` achievable; avoid `unsafe-inline`), `X-Content-Type-Options`, `Referrer-Policy`, `frame-ancestors`/frame deny, a minimal `Permissions-Policy`. Turn off the Kestrel `Server` header.
- **Router/TLS slips.** Router attached to `web` and `websecure` without a redirect, missing `certResolver` (falls back to Traefik's default self-signed cert), a typo in the dynamic file that makes Traefik drop the whole file, a broad `Host` or path rule that captures other sites. Remember Certificate Transparency makes the new hostname public the moment a certificate is issued; "secret subdomain" is not a control.
- **Host header trust.** Set `AllowedHosts` to the real hostname (from env), so the app does not answer arbitrary Host headers.
- **Error and dev surfaces.** Production environment only, generic exception page, no Swagger/dev pages, no stack traces.
- **Low-power host DoS.** Everything public should be served from the in-memory snapshot and static files, with `ETag`/`304`, precompressed assets, no per-request BGG, image processing or disk scanning. Cap request body size (a read-only site needs about none), header/body timeouts and concurrent connections in Kestrel; in Traefik add `rateLimit` and `inFlightReq`; at the OS level cap the LXC (CPU/memory limits, systemd `MemoryMax`/`CPUQuota`) so a flood cannot starve the Traefik and other guests that share the NUC.
- **Compression done twice.** Pick Traefik `compress` or Kestrel response compression, not both.

**How to avoid:** adopt ing-dashboard's firewall and loopback ops pattern; write the Traefik file with documentation-range placeholders only; keep the router LAN-only (ipAllowList) until hardening is done, then remove the allowlist as the explicit "go public" step with a checklist (headers verified with an external scanner, rate limit verified with a burst test, TLS grade, `/metrics` returns 404 from outside, direct app port refused from a second LAN host).

**Warning signs:** the app port answers from a laptop on the LAN; the app logs one client IP for everyone; `/metrics` or `/health` reachable on the public hostname; browser shows a self-signed cert warning; Traefik logs a config parse error.

**Phase to address:** **P-Repo** (firewall and ops-port pattern in the walking skeleton); **P-Harden** (headers, rate limits, go-public checklist).

---

### Pitfall 12: Rendering untrusted BGG text as markup

**What goes wrong:** Game names, version names and public comments (location line) are user-submitted on BGG. A client-side renderer that builds the cabinet with `innerHTML`, `Html.Raw`, or SVG `<text>` from strings, or inserts names into inline `style`/attributes, turns a BGG page edit into XSS on the owner's public site. Also: double-escaped entities (Pitfall 5) tempt people to "fix" text by rendering it raw.

**How to avoid:** treat every BGG string as untrusted data. Use framework auto-encoding or `textContent`; never `Html.Raw`; no BGG text in `style`, `href` (except URLs the server builds from numeric ids, for example `https://boardgamegeek.com/boardgame/<id>`), or JS string contexts without JSON encoding. The CSP in Pitfall 11 is the backstop. Add a test fixture with a game named like markup and quotes, and with an overlong name.

**Warning signs:** `Html.Raw`, `innerHTML =`, `dangerouslySetInnerHTML` anywhere near collection data; a CSP that needs `unsafe-inline`.

**Phase to address:** **P-Render**; fixture from **P-Sync**.

---

### Pitfall 13: Copying the ing-dashboard release pipeline without its assumptions

**What goes wrong:** The pattern is solid, but it depends on repository-side settings and a few facts that are easy to miss on a fresh repo. Verified against the reference files and GitHub docs:

1. **Unprotected `deploy` environment auto-created.** GitHub: "Running a workflow that references an environment that does not exist will create an environment with the referenced name... the newly created environment will not have any protection rules." If the first tag is pushed before the environment, reviewer and tag policy exist, the `publish` job runs without approval and the draft release is published automatically. Create and configure the environment (and run `build/check-github-settings.sh` adapted) BEFORE the first tag.
2. **Self-review.** With one owner, `prevent_self_review` must stay `false` (ing-dashboard documents exactly this) or releases wait forever. Environments with required reviewers on the Free plan only work for public repos (GitHub docs), another reason the repo must be public from the start.
3. **Attestations need a public repo (or Enterprise Cloud).** GitHub docs: on Free/Pro/Team, artifact attestations are only for public repositories; public ones use the public Sigstore instance. Flipping the repo to private later breaks the pipeline; the server's offline verification also pins to the public flow.
4. **The "offline" verification is not entirely offline.** The reference installer's own comment says `gh` "does fetch Sigstore's public trust root, so an unreachable Sigstore instance fails verification." That is fail-closed and fine, but expect failed installs when egress or Sigstore is down, and do not describe it to yourself as air-gapped. Also needs `gh` 2.49 or newer (ing pins a floor in `versions.env`); Ubuntu's own `gh` package may be older, so install from GitHub's apt repository and assert the version in provisioning.
5. **GitHub API polling limits.** The poller calls unauthenticated `releases/latest` (and a `compare` call on install). GitHub: 60 requests per hour for unauthenticated requests, tied to the originating IP; and 304 responses are free only when correctly authorized, so with no token every poll counts. Two projects polling every 5 minutes from one NAT address use 24/hour before any other tool. Poll every 10-15 minutes with jitter, stay unauthenticated (the security model forbids server credentials), log `x-ratelimit-remaining`, and make a 403/429 a quiet retry rather than an alert storm.
6. **Bootstrap chicken-and-egg.** The server's installer lives in the repo's `deploy/` directory and `releases/latest` returns 404 until a first published release exists. ing-dashboard's guide clones the repo at the latest release tag for provisioning. For this repo plan the very first release as a minimal walking-skeleton, and make a 404 from `releases/latest` a quiet "nothing to install yet," not a failure email every poll.
7. **Branch protection on a fresh repo.** A repository ruleset that requires pull requests blocks the initial push, and required status checks cannot be selected until the check has run once. Order: create the repo EMPTY (no GitHub-generated README or license, or the histories become unrelated and PRs show "no common history"), push `main` first, then the working branch, open a PR so CI runs once, then add the ruleset with the now-known check names. With a solo owner set required approvals to 0 (an author cannot approve their own PR) but keep PR-required, required checks, no force-push and no deletion, and tag ruleset restricting `v*` tags to the admin role (as in the reference doc).
8. **Health check must not depend on BGG.** The installer waits for `/health`, and rolls back (when no migration ran) if it is unhealthy. If health includes "BGG reachable" or "snapshot fresh," a BGG outage during a deploy rolls back a good release, and the very first deploy with no snapshot would fail. Health = process up and able to serve (an empty snapshot is healthy). Report BGG sync status as a separate metric.
9. **No database here.** The reference release job runs a PostgreSQL service container, builds an EF migration bundle, and its installer has migration-aware rollback. For a DB-less app delete all of that rather than leaving dead branches in the installer; keep the simple rollback path, and remember the snapshot-schema rule from Pitfall 4.
10. **Immutable releases and draft flow.** GitHub immutable releases (GA 2025-10-28) lock assets and tags once published; the draft-then-publish flow in `release.yml` is the recommended one. Enable the setting before the first release; do not plan to re-upload assets to a published release.
11. **Residue and naming.** Systemd units, env var prefixes, mail templates and docs carry the reference project's names and ledger/banking text; rename and grep before the first public commit. Keep action SHA pins, Dependabot for `github-actions` and `nuget`, locked-mode restore, and "require actions pinned to full SHA."
12. **Runtime and trimming.** The reference ships a framework-dependent `linux-x64` app with the ASP.NET Core runtime from the distribution archive. Do not enable trimming for a Razor/ASP.NET app; keep the runtime package pinned in provisioning; ensure any native imaging assets match `linux-x64` and the LXC's libc.

**How to avoid:** make the first release a deliberate, documented rehearsal in P-Repo: settings applied and read back, tag, approve, observe the poller install a hello-world and a deliberate rollback test. Do this before any feature code depends on the pipeline.

**Warning signs:** the first release published without an approval prompt; poller emails about HTTP 404; `gh attestation verify` "unknown flag" on the server; GitHub 403 rate-limit messages in the poll journal; rollback after a deploy during a BGG outage.

**Phase to address:** **P-Repo** (all of it); **P-Sync** (health vs sync status separation when the worker is added).

---

### Pitfall 14: Personal data leaking into a public repository

**What goes wrong:** The repo is public from its first push, and git history is effectively permanent (deleted commits stay reachable by SHA on GitHub, in forks and caches). Leak paths specific to this project:
- **Recorded BGG responses as test fixtures.** A real `collection` response contains the owner's username in context, personal ratings, comments, play counts, per-item `lastmodified` timestamps and possibly `<privateinfo>`; `thing` responses are BGG data that the terms only license for display, so committing them is redistribution. Use hand-written synthetic XML (invented ids, invented game names) that mimic each quirk in Pitfall 5.
- **Screenshots in the README or PRs** of the real collection show owned games, real storage names and BGG art (copyright). Ship a demo data provider with a fully synthetic collection and use it for screenshots, tests and the performance budget.
- **Git commit metadata.** The three existing local commits carry a personal email address and a real name. Before the first push (history is still local and safe to rewrite) decide what identity the public history should show; GitHub's `noreply` address is the reference project's documented recommendation.
- **`.planning/` is committed too.** Docs there already name hardware and neighbouring services in the homelab; keep to placeholders, no hostnames, addresses, usernames, and no pointers to private paths that reveal more than needed.
- **Config examples.** `appsettings*.json` and `.env.example` hold placeholders only; the BGG username and token live only in the server env file. Unit tests must not read real config.
- **Server-visible leaks.** The username can leak through logs (default HTTP client logging prints request URLs including `username=`), exception messages shown to users, or health output. Real storage names appear on the public page by design; make that an explicit owner decision, and display only the parsed location segment.
- **The user-agent string** is sent to BGG, not the public; keep a repo URL, not an email, in it.
- **Hostnames in Certificate Transparency** and DNS are public regardless of repo hygiene; keep them out of docs anyway.
- **A denylist scanner leaks what it protects.** Putting the real username/domain into a repo-level secret-scan config publishes it. Keep the denylist in a local pre-commit/pre-push hook outside the repo (a file in the user's home), plus repo-level generic rules (private IPv4 ranges, `.local`/`.lan`, token-shaped UUIDs, email addresses) and GitHub secret scanning with push protection (free on public repos).

**How to avoid:** guardrails first (P-Repo): `.gitignore` (env files, state dirs, local snapshots, caches, `*.local`), local denylist hook, generic scanner rules in CI, PR template checklist, synthetic-only fixtures rule written down, a demo provider. Review the first push's full history, not just the tip.

**Warning signs:** any real-looking BGG id list in tests; images in `docs/`; a fixtures folder named after a person; a scanner config containing a name.

**Phase to address:** **P-Repo** (before the first push); **All**.

---

### Pitfall 15: Scope creep for a hobby showcase

**What goes wrong:** The cabinet metaphor invites endless polish, and the "just one more feature" list is long: realistic wood-grain generator, physics, a 3D engine (three.js/WebGL) instead of CSS, drag-and-drop rearranging (contradicts read-only), an in-app location editor with auth and a database, accounts and per-visitor "my picks," plays and ratings from BGG, wishlist or trade shelves (explicitly out of scope), theming, multi-language UI, PWA/offline mode, share cards, analytics, a "recommend a game" engine, multi-owner support, an admin dashboard, image recognition for real spines. Each adds a security surface or a BGG-terms risk (relaying, commercial indicators) on a public site.

**How to avoid:**
- Keep the PROJECT out-of-scope list as a hard gate; any new idea must name the requirement it replaces or the phase it extends.
- Define MVP as: sync, deterministic layout, face-out plus generated spines, detail card, empty state, mobile reflow, hardened public deploy. Filters and per-location cabinet are second wave; expansion sideways spines third (they drive the hardest layout and a11y cases).
- Spend polish budget only after the performance and a11y budgets in Pitfalls 9-10 are green.
- Convention conflict to resolve once: the carried-over rule "`///` XML docs only, no `//` comments" applies to C#; decide the equivalent for frontend code (for example JSDoc blocks only) in P-Repo so it is not relitigated.

**Warning signs:** a phase adds a dependency with a large runtime (3D/physics/animation library); a feature needs a login; a plan mentions "while we're here."

**Phase to address:** **All** (roadmap scope control); **P-Views** is where cuts happen first.

---

## Technical Debt Patterns

| Shortcut | Immediate Benefit | Long-term Cost | When Acceptable |
|----------|-------------------|----------------|-----------------|
| Hotlink BGG images in the browser | No image pipeline | Terms/traffic risk, CLS, privacy, mobile memory | Only in a throwaway spike, never in a release |
| Layout computed in the browser with `Math.random` | Fast to prototype | Non-deterministic, untestable, differs per device | Never for the shipped layout; fine for a visual sketch |
| Seed from `string.GetHashCode()` or `new Random(id)` | One line | Reshuffles on restart or runtime upgrade | Never; own PRNG plus stable hash |
| Store the BGG password/cookie on the server | Gets private location | Account takeover risk, unlicensed endpoint | Never |
| Sync on every app start | Simple startup | Hammers BGG on crash loops and every deploy | Never; sync only if snapshot is older than interval |
| Real BGG XML as test fixtures | Realistic tests | Privacy and licensing leak into public history | Never; synthetic fixtures only |
| `versions=1` on `thing` for dimensions | One request | Huge payloads, timeouts | Never; use collection `version=1` |
| Health check includes BGG reachability | "More honest" status | Rolls back good releases during BGG outages | Never as the deploy health gate |
| Skip golden layout tests | Faster iteration | Silent visual regressions and reshuffles | Never once layout exists |
| In-memory only snapshot, no disk | No state dir to manage | Empty site after every restart until BGG answers | Only if first-sync latency is acceptable and BGG is reachable; prefer persisted snapshot |
| Skip CSP | Fewer header fights | XSS impact if any BGG text slips through | Never on the public router |
| Leave Traefik LAN allowlist off during dev | Easier testing | Accidental early public exposure | Never; flip to public as an explicit step |

## Integration Gotchas

| Integration | Common Mistake | Correct Approach |
|-------------|----------------|------------------|
| BGG `collection` | Treat 202 as an error or retry instantly | 202 means queued: wait with backoff until 200, bounded retries, then fall back to last snapshot |
| BGG `collection` | `subtype=boardgame` only | Two calls: `excludesubtype=boardgameexpansion` for games, `subtype=boardgameexpansion` for expansions, both `own=1`, verify `<status own>` |
| BGG `collection` | Incremental sync with `modifiedsince` | Full pull each time; it does not report deletions |
| BGG `thing` | More than 20 ids, `versions=1`, forgetting `stats=1` | Batches of at most 20; `stats=1` for weight; versions via collection `version=1` |
| BGG host | `www.boardgamegeek.com`, browser-like UA | `boardgamegeek.com`, honest UA, `Authorization: Bearer` |
| BGG errors | Retry 401/403 | Stop and alert; retry only 202/429/500/503 with backoff and a budget |
| BGG images | Browser hotlinks, original size | Server-side fetch once, pre-generate variants, serve static |
| Traefik | Router on all entrypoints, no cert resolver, no redirect | `websecure` only plus global http redirect, explicit `certResolver`, exact `Host` rule |
| Traefik | Rate limit behind NAT/CDN keyed on proxy address | Configure `ipStrategy.depth` where applicable; test from outside |
| ASP.NET Core forwarded headers | Trust none (all clients = proxy) or trust all (spoofable) | Trust only the Traefik address via `KnownProxies`/`KnownIPNetworks` (.NET 10 names), test end to end |
| GitHub API | Polling every 5 minutes unauthenticated from a shared NAT address | 10-15 minute interval with jitter, treat 403/429 as quiet retry |
| GitHub environments | Referencing an environment before configuring it | Create environment with reviewer and tag policy first; read back settings |
| GitHub rulesets | Requiring checks/PRs before the first push and first CI run | Push `main`, run CI once, then enable rulesets |
| `gh attestation verify` | Assuming it needs no network, assuming old distro `gh` works | Needs Sigstore trust root reachable; require `gh` >= 2.49 from GitHub's apt repo |

## Performance Traps

| Trap | Symptoms | Prevention | When It Breaks |
|------|----------|------------|----------------|
| Decoded image memory (`w x h x 4` per image) | Tab reloads on iPhone, janky scroll | 160/320 px variants, lazy loading, `content-visibility: auto` | Roughly 50-100 face-out boxes with originals; fine with variants at 400 |
| Per-box shadows/filters and `will-change` everywhere | Hundreds of layers, GPU memory pressure, heat | Shared gradient shadows, on-demand promotion | 100+ boxes on a mid-range phone |
| Flattened `preserve-3d` (overflow/opacity/filter on ancestors) | Pull-out looks 2D | 3D only on a dedicated overlay element | Any size, as soon as dimming ships |
| Layout recomputed per request | CPU spikes on the host CPU, jitter | Compute once per sync into snapshot | Any public traffic burst |
| Image processing on request path or concurrent during sync | Memory spikes, OOM in a small LXC | Single worker, bounded memory, sequential | First sync of 300 games |
| First sync of a large collection | Minutes-long sync, 202 loops | Batches of 20, 5 s spacing, progress state, show partial cabinet only after validated swap | 300+ games |
| Unbounded font/asset downloads | Slow first paint, layout shift | Self-hosted subset font or system stack, `font-display` and fallback metrics | Any |
| Serving everything from Kestrel without compression/ETag | Bandwidth and CPU on a low-power host | Precompress static assets, ETag/304, one compression layer | A few dozen concurrent visitors |

## Security Mistakes

| Mistake | Risk | Prevention |
|---------|------|------------|
| BGG token in repo, logs, or client-side code | Revocation, abuse of the owner's quota | Server env file only (root-owned, mode 600), never logged, never sent to browsers |
| BGG password or session cookie on server | Account takeover | Do not use; location via public comment convention |
| Open image proxy or `?w=` resize endpoint | SSRF, CPU DoS | No request-driven fetches or resizes; fixed variants only |
| BGG text rendered as HTML | XSS on a public site | Auto-encoding, no raw HTML, CSP |
| Manual sync as GET or unlimited POST | BGG ban, DoS amplification | POST, global persisted cooldown, per-IP limit, coalescing |
| Health/metrics on the public router | Information disclosure | Loopback-only ops port, no router, 404 from outside |
| App port reachable from LAN directly | Bypass of headers/limits | nftables allow only the Traefik address to the app port |
| Trusting `X-Forwarded-For` from anyone | Spoofed IPs defeat rate limits | Known proxy only |
| Wide-open CORS | Third parties relay BGG data (terms violation) | Same-origin only |
| Dev environment or Swagger on in production | Information disclosure | Production environment in env file, no Swagger package |
| Release published without approval | Unreviewed code reaches the server | Environment with reviewer configured before first tag; read-back check |
| Cooldown and snapshot state in a world-writable path | Tampering | Systemd `StateDirectory`, hardened unit like the reference |

## UX Pitfalls

| Pitfall | User Impact | Better Approach |
|---------|-------------|-----------------|
| Cabinet reshuffles between visits | Cannot find "the shelf where it was" | Deterministic, insertion-stable layout |
| Search only dims games | Hard to see matches on a 400-game phone cabinet | Dim plus count, optional scroll-to-first-match |
| 3D pull-out ignores reduced motion | Discomfort for vestibular users | Fade/instant alternative |
| Tiny expansion spines on touch | Mis-taps, frustration | Clustered target opening base game card |
| Empty or tiny collection looks broken | First impression of "bug" | Intentional empty/sparse designs: minimum cabinet size, friendly message, demo-free |
| One huge box sets the shelf height | Wasted space, ugly gaps | Cap outlier dimensions; allow variable cubby heights; test with an extreme box |
| Generated spine titles cut off | Games unrecognisable | Subtitle shortening, line clamping, tooltip and accessible label |
| No "last synced" indicator | Owner and friends cannot tell whether a game is missing or just not synced | Quiet freshness note; expectation text on manual sync |
| Non-matching games stay focusable | Keyboard users tab through 300 dimmed games | Roving or filtered tab order, skip link |
| Locations as free text with typos | Several cabinets for one room | Alias map and an "Unsorted" bucket |

## "Looks Done But Isn't" Checklist

- [ ] **BGG sync:** works with the real token from the LXC (not just a laptop), with honest User-Agent, and a revoked/invalid token produces an alert, not a retry loop.
- [ ] **Sync-now:** a burst of 50 POSTs causes at most one BGG sync; restarting the service does not reset the cooldown; GET is rejected.
- [ ] **Snapshot:** a simulated empty/error BGG response leaves the previous snapshot untouched; rollback to the previous release still starts.
- [ ] **Expansions:** none appear as games; none unowned appear on cards; orphans handled.
- [ ] **Duplicates:** two copies of one game render once.
- [ ] **Layout determinism:** two process restarts and a .NET patch bump produce identical layout JSON; adding one game changes a bounded number of placements.
- [ ] **Edge cases:** 0, 1, 3, 50, 400 games, one giant box, missing dimensions, missing image, overlong/CJK/emoji names all render sanely.
- [ ] **Images:** no browser requests to BGG's image host; every `<img>` has dimensions; CLS under about 0.1; no variant over a few tens of KB on mobile.
- [ ] **Mobile:** 400-game synthetic cabinet is smooth on a real mid-range phone; no tab reload on an older iPhone.
- [ ] **Accessibility:** axe clean on filled/sparse/empty; spine palette contrast test passes; keyboard-only run through filter, open card, close card; reduced-motion verified.
- [ ] **Attribution:** BGG credit text plus linked "Powered by BGG" logo visible at 360 px width on every page showing BGG data.
- [ ] **No monetization:** no ads, donation, affiliate or sponsor elements.
- [ ] **Public hardening:** app port refused from another LAN host; `/health` and `/metrics` 404 on the public name; CSP present and enforced; headers verified externally; rate-limit burst test; TLS served with the right certificate; one client IP per visitor in logs.
- [ ] **Release pipeline:** first release required a human approval; poller handles "no release yet" quietly; server `gh` version asserted; rollback rehearsed.
- [ ] **Privacy:** full history scanned (not only tip) for username, domain, IPs, personal email; fixtures are synthetic; screenshots use the demo collection.

## Recovery Strategies

| Pitfall | Recovery Cost | Recovery Steps |
|---------|---------------|----------------|
| BGG revokes or never approves the token | MEDIUM | Keep serving last snapshot; contact BGG via the application; degraded mode: owner-run manual export imported to the state directory (permitted for downloading one's own collection while logged in, per official guide) |
| Cloudflare challenges the server's address | MEDIUM-HIGH | Confirm honest User-Agent and host; lower request rate; ask BGG; consider egress via a different network path; otherwise degraded manual import |
| Banned for excess traffic | MEDIUM | Raise cooldown floors, audit restart/retry paths, ask BGG to reinstate |
| Bad snapshot published | LOW | Restore previous snapshot file (kept), fix guard, force resync |
| Layout non-determinism discovered after launch | MEDIUM | Replace PRNG/hash, regenerate layouts; one-time visible reshuffle announced |
| Personal data pushed to the public repo | HIGH | Treat as burned: rotate token/credentials, rewrite history AND assume forks/caches retain it, contact GitHub to purge cached views if sensitive; far cheaper to prevent with pre-push scanning |
| Release published without approval | MEDIUM | Delete/supersede the release (immutable once published), fix environment, cut a new tag; audit what was installed |
| Rollback fails due to snapshot schema | LOW | Delete snapshot file, restart, resync; make this the documented step |
| Terms notice from BGG | LOW-MEDIUM | Remove the offending feature (ads, relay, logo placement), reply, keep logo/credit visible |

## Pitfall-to-Phase Mapping

| Pitfall | Prevention Phase | Verification |
|---------|------------------|--------------|
| 1. Token approval, Cloudflare, UA | P-Sync (registration on day one in P-Repo) | Real call from the LXC returns 200 with token; 401 path alerts |
| 2. Inventory location unavailable / cookie risk | P-Sync | Decision record with evidence from real response; no credential in env example |
| 3. Hammering BGG | P-Sync, P-Harden | Burst test; restart/deploy does not trigger sync when snapshot fresh |
| 4. Bad snapshot replacement | P-Sync | Injected empty/error responses leave snapshot intact |
| 5. Collection data model traps | P-Sync, P-Layout | Synthetic fixtures per quirk pass |
| 6. BGG terms | P-Render, P-Views, P-Harden, All | Footer logo at 360 px; no monetization; same-origin only |
| 7. Non-deterministic layout | P-Layout | Golden tests; insertion-stability test; restart diff is empty |
| 8. Images | P-Sync, P-Render, P-Harden | No browser calls to BGG image host; CLS budget; no open proxy |
| 9. Mobile performance / 3D | P-Render, P-Views | 400-game budget on real phones; layer count check |
| 10. Accessibility | P-Render, P-Views | axe + keyboard + reduced-motion checks; palette contrast unit test |
| 11. Public exposure on homelab | P-Repo (firewall, ops port), P-Harden | Go-public checklist, external header scan, burst test |
| 12. Untrusted text as markup | P-Render | Markup-named game fixture renders inert |
| 13. Copying release pipeline | P-Repo | Rehearsed first release; settings read-back script passes |
| 14. Personal data in public repo | P-Repo, All | History scan before first push and in CI; local denylist hook installed |
| 15. Scope creep | All | Roadmap gate against Out of Scope list |

## Gaps That Need a Spike, Not More Desk Research

- Real `collection` and `thing` responses were not fetched (Cloudflare blocks non-browser tooling and no token exists yet): confirm presence and units of version dimensions, whether collection `version=1` includes them, whether `<comment>` survives, and what the private-info block really contains.
- Whether BGG's Cloudflare treats the owner's home egress address differently from cloud hosts (single dated report, LOW).
- Exact current BGG throttle behaviour (5 s guidance is official but "currently"; 429 reports are older).
- Image licensing posture for resized box art: unresolved interpretation, consider asking BGG.
- Which imaging library and licence fit the public repo and the LXC's libc (decide in the stack step).

## Sources

**Official BGG (HIGH; read through Internet Archive captures because BGG returns 403 to non-browser clients):**
- Using the XML API, version date 2025-07-02: https://boardgamegeek.com/using_the_xml_api (registration, tokens, exceptions, licenses, usage limits, client-side warning, public-facing logo, private APIs unlicensed, third-party services prohibited)
- XML API Terms of Use: https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use (non-commercial license, credit and logo, no modification, AI/LLM training prohibited, revocable)
- BGG XML API Commercial Use: https://boardgamegeek.com/wiki/page/BGG_XML_API_Commercial_Use (ads, payments, merchandise, donations)
- BGG XML API2 wiki: https://boardgamegeek.com/wiki/page/BGG_XML_API2 (host without `www`, 5 s wait and 500/503 throttling, 20 ids max, 202 queued, subtype bug and workaround, `showprivate`, `modifiedsince`)
- XML API Enhancements wiki: https://boardgamegeek.com/wiki/page/XML_API_Enhancements (double-escaped entities)
- BGG Image Policy wiki (about uploads, context only): https://boardgamegeek.com/wiki/page/BGG_Image_Policy

**BGG forum and third-party reports (MEDIUM/LOW):**
- Announcement threads quoted via a 2025-07-20 GitHub issue (phases, "All users of the XML API, commercial and non-commercial, must register"): https://github.com/EmilStenstrom/gamecache/issues/95
- Unauthenticated calls return 401, token and no-`www` requirement, dated 2026-09-28: https://github.com/isstiaung/nalanda/pull/39
- Cloudflare challenge for browser-faking User-Agent, honest UA reaches BGG's own 401, token usage stopped 2026-09-09, dated 2026-10-01: https://github.com/chrismcclarin/nextgamenight-backend/pull/57
- Collected terms and live checks (401 unauthenticated, 202 polling, 20 ids), dated 2026-10-01: https://github.com/JacobStephens2/keeplore/issues/88
- Private info via login cookies: https://blog.arranfrance.com/post/board-game-geek-api/ and https://github.com/tnaskali/bgg-api
- Inventory location not exposed (forum reports, snippets only): https://boardgamegeek.com/thread/1894272/download-collection-with-inventory-location and https://boardgamegeek.com/thread/3695783/is-there-a-way-to-download-inventory-location-priv
- Image hotlink 403 report (old): https://boardgamegeek.com/thread/1771107/xml-api-image-and-thumbnail-links-broken
- 429 reports after cloud move (old): https://boardgamegeek.com/thread/2065587/wbggs-move-to-cloud-xmlapi-may-now-return-status-4

**GitHub (HIGH):**
- Environments auto-creation, Free plan limits, prevent self-review: https://docs.github.com/en/actions/how-tos/deploy/configure-and-manage-deployments/manage-environments
- Unauthenticated rate limit 60/hour per IP: https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api
- Conditional requests and 304 (free only when authorized): https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api
- Artifact attestations (public Sigstore for public repos; private repos need Enterprise Cloud): https://docs.github.com/en/actions/concepts/security/artifact-attestations
- Immutable releases GA 2025-10-28: https://github.blog/changelog/2025-10-28-immutable-releases-are-now-generally-available/

**Microsoft / web standards / proxy docs (HIGH):**
- Random class notes (algorithm not guaranteed across major versions): https://learn.microsoft.com/en-us/dotnet/api/system.random?view=net-10.0
- `string.GetHashCode` (differs between runs, never persist): https://learn.microsoft.com/en-us/dotnet/api/system.string.gethashcode?view=net-10.0
- .NET 8 forwarded headers ignore unknown proxies: https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/8/forwarded-headers-unknown-proxies?view=aspnetcore-10.0
- .NET 10 `KnownNetworks` obsolete: https://learn.microsoft.com/en-us/aspnet/core/breaking-changes/10/ipnetwork-knownnetworks-obsolete?view=aspnetcore-10.0
- WCAG 2.2 Target Size (Minimum): https://www.w3.org/WAI/WCAG22/Understanding/target-size-minimum.html
- prefers-reduced-motion: https://developer.mozilla.org/en-US/docs/Web/CSS/@media/prefers-reduced-motion
- Traefik rateLimit and `ipStrategy.depth`: https://doc.traefik.io/traefik/v3.4/middlewares/http/ratelimit/

**Reference project (read directly, HIGH for what it does, project-specific):**
- ing-dashboard `docs/deploy.md`, `docs/releasing.md`, `docs/github-repository-settings.md`, `docs/lxc-setup.md`, `.github/workflows/release.yml`, `build/validate-release-tag.sh`, `build/check-github-settings.sh`, `deploy/lib/deploy.sh`, `deploy/nftables/ledger.nft.in`, `deploy/traefik/ledger.yml.example`, `deploy/systemd/*`, `deploy/versions.env`

**Reasoned, not externally verified (flagged inline):** mobile memory budgets, `preserve-3d` flattening rules (standard CSS transforms behaviour), circuit-per-visitor UI cost, image-licensing interpretation, unstable-sort behaviour of `List<T>.Sort`.

---
*Pitfalls research for: public BGG-backed virtual game cabinet on a self-hosted homelab*
*Researched: 2026-10-03*

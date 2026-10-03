# Project Research Summary

**Project:** Games Cabinet
**Domain:** Public, read-only showcase web app — a personal board game collection, synced from BoardGameGeek, rendered as a dynamically drawn wooden cabinet; self-hosted in a Proxmox LXC behind Traefik
**Researched:** 2026-10-03
**Confidence:** MEDIUM-HIGH

## Executive Summary

This is a small, read-mostly .NET 10 app. It has one external dependency, the BGG XML API2, and one hard creative problem: a deterministic, natural-looking cabinet that works from 0 to several hundred games, including on a phone.

The researchers converge on one design. A single-flight background sync (hourly, plus a cooldown-guarded "sync now") writes an atomic JSON snapshot and a local WebP image cache under the systemd `StateDirectory`. Visitors only ever read an immutable in-memory snapshot and memoised layouts. There is **no database**: the shared database LXC, SQLite and PostgreSQL were all considered and rejected. The frontend is a Razor Pages shell with vanilla ES modules and modern CSS (DOM rendering, native `<dialog>`, container query units, View Transitions), with no SPA, Blazor or Node toolchain. The cabinet layout is a pure, seeded C# function in the Domain project, golden-tested and prefix-stable, so a new game appends without reshuffling the shelves. Delivery reuses the ing-dashboard pull-based attested-release model, simplified: Postgres, EF, Grafana, Prometheus and backups are dropped.

The biggest risks are external, not technical:
- **BGG token approval.** BGG application registration with a Bearer token has been mandatory since 2025-07-02, and approval "may be a week or more". It is the longest lead-time item and must start on day one.
- **Storage location.** The location is probably *not* readable via the app token: `showprivate=1` needs the owner's logged-in website session, and forum reports say inventory location isn't exposed even then. The default design is therefore a `Location: …` convention in the public collection comment, which keeps the app database-free; a spike with a real token confirms it.
- **Remaining risks:**
  - hammering BGG and getting throttled or revoked;
  - replacing a good snapshot with a bad or empty one;
  - layout churn between syncs;
  - personal data leaking into the public repo, including git history;
  - an auto-created, unprotected `deploy` environment publishing the first release without approval;
  - exposing a low-power homelab host to the public internet without rate limits or a CSP.

## Key Findings

### Recommended Stack

The backend is .NET 10 / ASP.NET Core with the reference project's solution layout (Domain / Repository / Service, `.slnx`, `Directory.Build.props` with nullable, warnings-as-errors and lock files; SDK 10.0.112 via `global.json`, MTP test runner). BGG is reached through a thin typed `HttpClient` plus LINQ to XML. No BGG client library is used: `BoardGamer.BoardGameGeek` 0.10.0 is the only acceptable fallback, but it is pre-1.0 and its 202-retry defaults are too aggressive.

**Core technologies:**
- **ASP.NET Core 10 Razor Pages + vanilla ES modules + modern CSS**: page shell and cabinet rendering. DOM/CSS beats Canvas/SVG for accessibility and tap targets. `cqw` units scale one layout to any width; mobile gets its own server-computed narrow profile.
- **Microsoft.Extensions.Http.Resilience 10.10.0 (tuned)**: retries and timeouts for BGG. Defaults are too tight. Add an explicit bounded 202 poll loop outside the pipeline and a spacing handler that keeps BGG calls at least 5 s apart.
- **SixLabors.ImageSharp 4.1.2**: downscale box art to WebP and extract a dominant colour (hand-written, ~40 lines). It is free under its split licence **only if the public repo carries an OSI licence** (MIT/Apache-2.0); otherwise use SkiaSharp 4.153.1.
- **BackgroundService + PeriodicTimer(TimeProvider) + capacity-1 Channel**: hourly sync and single-flight manual sync, with a persisted global cooldown. No Hangfire or Quartz.
- **Atomic JSON snapshot files**: `snapshot.json` plus the previous snapshot, plus `sync-state.json`, plus a content-hashed image cache. Everything can be rebuilt from BGG, so there are no migrations and no backups, and rollback is a safe symlink flip.
- **Testing**: xunit.v3 4.0.1, FluentAssertions 8.11.0, NSubstitute 6.2.0, Microsoft.Extensions.TimeProvider.Testing 10.10.0, Microsoft.AspNetCore.Mvc.Testing 10.0.12. Microsoft.Playwright.Xunit.v3 1.63.0 lives in a separate project and CI job, using `WebApplicationFactory.UseKestrel()`. Playwright .NET has no screenshot assertion, so it asserts DOM and geometry invariants and keeps screenshots only as CI artifacts.

**Hard BGG constraints:**
- API host is `boardgamegeek.com` (no `www`).
- The token is sent only to the BGG API host; a separate token-less client fetches the image CDN.
- `thing` calls take at most 20 ids and never use `versions=1`.
- Register under the non-commercial licence: no ads, donations or affiliate links.
- A linked "Powered by BGG" logo and credit appear on every public page.
- Calls are server-side only and cached.
- No relaying of BGG data: no public JSON API and no CORS.
- No AI training on the data.

### Expected Features

The closest prior art is a static Kallax-cubby viewer. It shows every game face-out, tints cubbies by cover colour, and offers filters and a detail panel. Nothing found does mixed face-out plus spine packing with expansion spines, so the cabinet is a genuine but unproven differentiator.

**Must have (table stakes):**
- Automatic BGG sync (owned only), last-good snapshot, "last synced" indicator, and a cooldown-guarded sync-now.
- A mixed face-out/spine cabinet with stable, natural packing.
- Filters that dim non-matching games: name search, player count, play time, storage location. Filters never relayout.
- A detail card with the pull-out animation: players, play time, weight, location, owned expansions, BGG link.
- Thing-endpoint enrichment (needed for expansion links, weight and the best-player-count poll).
- Expansion spines beside their base game, with documented edge cases (expansion owned without its base, standalone expansions, big-box editions, promos).
- Location display plus a toggle between one cabinet and one cabinet per location (with an "Unsorted" cabinet).
- Mobile reflow into a narrow, tall cabinet.
- Empty and sparse collections look intentional.
- An accessible hidden list of games for screen readers and keyboard users.
- BGG attribution.

**Should have (v1.x differentiators):**
- A "best at N" filter based on the BGG poll, and weight labels.
- A random picker that respects active filters and pulls the chosen box out.
- Filter and selected-game state in the URL, so a filtered view is a shareable link.
- Per-game Open Graph tags from server-rendered meta.
- Designer and mechanic search, and a small stats plaque.

**Defer (v2+):** a rendered Open Graph image of the whole cabinet (needs a headless browser, too heavy for the host), a layout driven by real box dimensions, and themes.

**Anti-features:**
- Accepting a BGG username from the client, which would turn the site into a proxy that hammers BGG.
- Hotlinking BGG images, or live BGG calls per visitor.
- Accounts, editing, wishlist and other non-owned statuses, play logging, voting, recommendations.
- Sort controls on the packed shelf (they conflict with stable packing; use filter-and-dim plus the random picker instead).
- WebGL.

### Architecture Approach

The app is a single process with two Kestrel listeners. The public listener serves pages, the read-only catalogue JSON (same-origin), images and the POST-only sync endpoint. A loopback-only ops listener serves `/health`, is never routed through Traefik, and does not depend on BGG reachability.

The sync pipeline runs in this order:
1. Two collection calls: `own=1&excludesubtype=boardgameexpansion`, then `own=1&subtype=boardgameexpansion`. The default subtype returns mislabelled expansions. Pull the full collection every time, because `modifiedsince` doesn't report deletions. Dedupe by `objectid`.
2. A diff with a suspect-shrink guard: never treat a non-200 or an empty 200 as "no games".
3. `thing` batches of 20 with `stats=1`.
4. The image pipeline.
5. Assembly: location parse, expansion-to-base mapping via inbound links, a dimension fallback chain (the owner's selected version in the collection, then an estimate from the image aspect ratio).
6. Atomic snapshot write, publish, and invalidation of the layout cache.

The layout engine emits integer-millimetre JSON for three width profiles × two modes (All / ByLocation). The client only draws, scales via a CSS variable, and dims.

**Major components:**
1. **Domain**: model, layout engine (stable hash rather than `string.GetHashCode`, its own PRNG rather than `System.Random`, a total ordering, prefix-stable packing by `collId`), location parser, sync planner.
2. **Repository**: BGG gateway (spacing, 202 loop, 401/403 treated as stop-and-alert), file snapshot store, image store.
3. **Service**: host, sync coordinator, endpoints, `wwwroot` (cabinet renderer, dialog, filters).
4. **Tests**: UnitTests (golden layouts, no-overlap, insertion stability, parser) and IntegrationTests (fake BGG handler, synthetic XML fixtures).
5. **Deploy**:
   - Unprivileged Ubuntu 24.04 LXC (1 core, 512 MB–1 GB, `MemoryMax`).
   - Provisioning modules and a root installer.
   - A poll timer every 10–15 minutes with jitter, not 5, because unauthenticated GitHub polling is limited to 60 requests per hour.
   - Offline attestation verification, symlink activation and automatic rollback.
   - Public Traefik route with rate limits (tolerant site-wide, strict on `/api/sync`), CSP, Permissions-Policy, explicit forwarded headers (`KnownIPNetworks` in .NET 10), a default-drop nftables firewall, and no `ipAllowList`.

### Critical Pitfalls

1. **BGG access is a calendar dependency.** Register on day one and build against synthetic fixtures meanwhile. Use an honest User-Agent, or Cloudflare returns 403. Treat 401/403 as stop-and-alert and never retry them. Run the access spike from the actual LXC.
2. **Sync loops and abuse amplification.** `Restart=always` plus sync-on-start can call BGG every 10 s in a crash loop. Use a single-flight coordinator with a persisted global cooldown, sync on start only when the snapshot is stale, make "sync now" a POST that only enqueues, and rate-limit it in Traefik.
3. **Layout churn and non-determinism.** `string.GetHashCode` is randomised per process, `Random(seed)` isn't stable across .NET versions, and `List<T>.Sort` is unstable. Use a pure function with its own PRNG and stable hash, compute the layout once per sync, and add golden plus insertion-stability tests.
4. **Cloning the ing-dashboard pipeline:**
   - Referencing a `deploy` environment that doesn't exist auto-creates it unprotected. Configure it with a reviewer before the first tag.
   - Attestations need a public repo.
   - `releases/latest` returns 404 until the first release exists.
   - Deploy health must not depend on BGG, or a BGG outage during a deploy rolls back a good release.
   - Version the snapshot schema, because automatic rollback may start an older release against a newer snapshot.
5. **Privacy in a public repo:**
   - Fix the git identity before the first push.
   - Use synthetic XML fixtures only, never recorded BGG responses.
   - Use a demo data provider for screenshots.
   - Keep the personal-value denylist in a local hook outside the repo.
   - Remember that `.planning/` is committed too.

## Implications for Roadmap

Suggested phase structure. The architecture order is preferred over the features-first order: the BGG token cannot arrive for about a week, so deploy plumbing and the layout engine fill that wait productively, and the first release is rehearsed before any feature depends on it.

### Phase 1: Repo, guardrails & walking-skeleton deploy
**Rationale:** This is the least reversible work. Public git history and the pipeline settings must be correct before the first push and the first tag. The BGG registration is submitted the same day.
**Delivers:**
- Empty public GitHub repo with protected `main`, a tag ruleset, a `deploy` environment with a reviewer, immutable releases and push protection.
- OSI licence, privacy hooks, rewritten git identity.
- CI and release workflows, solution scaffold.
- LXC provisioning, installer and deploy-poll timer.
- A hello page released and rolled back as a rehearsal; the ops `/health` endpoint.
- The imaging stack smoke-tested inside the real LXC. The router stays LAN-only.
**Avoids:** privacy leaks, unprotected first release, attestation and private-repo trap.

### Phase 2: Layout engine & renderer prototype (parallel with the BGG approval wait)
**Rationale:** The highest creative risk, and buildable on synthetic data. It needs a visual prototype and review loop.
**Delivers:**
- Pure C# shelf packer with face-out vs spine decisions, stacking and expansion slots.
- Golden, no-overlap and insertion-stability tests.
- A synthetic collection generator covering 0, 1, 5, 50 and 400 games plus edge cases.
- A static renderer prototype with intentional sparse and empty states.
**Uses:** Domain project, DOM/CSS rendering, `cqw` scaling.

### Phase 3: BGG access spike, sync & snapshot (first real vertical slice)
**Rationale:** Needs the token. It turns the prototype into the real collection on the deployed site.
**Delivers:**
- Live spike from the LXC: token-only `showprivate`, `<comment>` shape, `own=1` and subtype behaviour, `collid` duplicates, 202/429/403 behaviour.
- BGG gateway with spacing and a bounded 202 loop.
- Sync coordinator with persisted cooldown and shrink guard.
- Atomic snapshot, attribution footer, BGG-down states.

### Phase 4: Enrichment, images & storage location
**Delivers:**
- `thing` batches with poll interpretation (best/recommended player counts) and weight.
- Expansion-to-base mapping and its edge cases.
- Location parser (`Location: …` convention, or the private field if the spike finds it readable).
- Image pipeline: allowlisted download, size caps, WebP downscale, dominant colour with WCAG-contrast spine text.
- Dimension fallback chain.

### Phase 5: Cabinet UI & interaction
**Delivers:**
- Production renderer, pull-out (transform-only FLIP or View Transition, with a `prefers-reduced-motion` fallback) and the `<dialog>` detail card.
- Filters that dim, and the per-location toggle.
- Mobile profile and accessibility: keyboard navigation, screen-reader list, 24 px tap targets on thin spines.
- A 400-game mobile performance budget.
- If the schedule slips, filters and then expansions are the first cuts.

### Phase 6: Public hardening & go-public
**Delivers:**
- Traefik rate limits and headers, a strict CSP, forwarded headers and Kestrel limits.
- LXC CPU and memory caps, the nftables firewall.
- A re-read of the BGG terms.
- A go-public checklist, followed by the router opening the public route.

### Phase 7 (post-launch, v1.x): Differentiators
Best-at-N, weight labels, random picker, URL state, Open Graph tags, designer and mechanic search, stats plaque.

### Phase Ordering Rationale

- Repo and pipeline settings come first because they are the hardest to fix later (public history, first-tag protection).
- The layout engine runs during the BGG approval wait because it needs no external data and carries the most creative risk.
- Sync comes before enrichment, and enrichment before UI, because expansion spines, weight, best-at-N and location filters all depend on enriched snapshot data.
- Hardening is a distinct gate before opening the public route; until then the router stays LAN-only.

### Research Flags

Phases likely needing deeper research during planning:
- **Phase 3:** live token spike from the LXC. Real BGG behaviour in 2026 is MEDIUM-LOW confidence.
- **Phase 4:** box-dimension coverage and units, expansion shapes (standalone, contains, multi-parent), image CDN behaviour, imaging licence.
- **Phase 2:** packing prototype loop; the visual quality is subjective and needs owner review.

Phases with standard patterns (skip research-phase):
- **Phase 1:** mirrors the reference project closely.
- **Phase 5:** standard web UI and accessibility patterns.
- **Phase 6:** documented Traefik and ASP.NET hardening.

## Confidence Assessment

| Area | Confidence | Notes |
|------|------------|-------|
| Stack | MEDIUM-HIGH | Package versions verified against registries on 2026-10-03. BGG payload details need a live spike |
| Features | MEDIUM | Several comparable tools read directly; shelf realism has little prior art |
| Architecture | MEDIUM-HIGH | Reference deployment read directly; layout and no-DB conclusions are reasoned, to be validated by the prototype |
| Pitfalls | MEDIUM-HIGH | Official BGG wiki read via archive captures (live pages are Cloudflare-gated for automated fetch); GitHub, Microsoft and Traefik docs verified |

**Overall confidence:** MEDIUM-HIGH

### Gaps to Address

- **Storage location source:** does a token alone expose `<privateinfo>`? The design assumes no and uses the public-comment `Location: …` convention. **The owner must accept that location names written there are public**, on BGG and on the site. Plan B (b), a loopback- or VPN-restricted `locations.json` overlay, stays possible behind an `ILocationSource` abstraction.
- **Box dimensions:** coverage and units in BGG version data are unconfirmed and sparse. v1 relies on aspect ratio plus a heuristic depth, and real dimensions are an optional upgrade. The researchers disagree on a rare single-id `versions=1` pass; the recommendation is to skip it in v1.
- **Image library:** ImageSharp (requires an OSI licence on the repo) or SkiaSharp (check libc compatibility in the LXC). The recommendation is ImageSharp plus MIT/Apache-2.0, smoke-tested in Phase 1.
- **Spine colour:** derive it from the cover, clamp lightness, pick text colour by WCAG contrast, and unit-test it. Pitfalls research preferred a curated palette as the safer option.
- **Image resizing under BGG terms:** the non-modification clause is ambiguous for downscaled copies. Downscale only, never crop, keep the credit and link, and consider asking BGG.
- **Exact BGG throttle numbers:** none are published. Use 5 s spacing and treat every figure as LOW confidence.
- **Frontend comment convention:** the carried-over "`///` only" rule is written for C#. Decide JSDoc-only for JS early.

### Spike list (consolidated)

1. Token-only `showprivate=1` on the owner's collection
2. `<comment>` shape and presence (with and without `brief=1`)
3. Dimensions via collection `version=1`
4. Live 202 / 429 / 403 behaviour from the LXC egress
5. `own=1`, `excludesubtype`, duplicates per `collid`, `totalitems`
6. Expansion shapes: standalone, big-box "contains", multi-parent
7. Image CDN behaviour (hotlink, referrer, sizes)
8. Imaging stack inside the target LXC
9. Release and rollback rehearsal
10. Public-exposure checks (headers, rate limits, health not routed)
11. BGG terms re-read before go-live
12. 400-game mobile performance budget

### Owner actions (consolidated)

1. **Register the BGG application now** (non-commercial). Approval may take a week or more.
2. Create the public GitHub repo, empty, then apply the planned settings: protected `main`, tag ruleset, `deploy` environment with a reviewer, immutable releases, push protection.
3. Confirm the public git identity (GitHub noreply) before the first push; rewrite the local history if needed.
4. Choose an OSI licence (MIT or Apache-2.0).
5. Accept that location names written in BGG comments are public, and fill in BGG using the agreed `Location: …` convention.
6. Keep the BGG username, token and any personal denylist server-side or outside the repo.
7. Provide DNS, TLS and port-forward details, create the LXC, and decide when to go public.
8. Approve draft releases.
9. Keep the site non-commercial: no ads, donations or affiliate links.
10. Optionally ask BGG about displaying resized box art.

## Sources

### Primary (HIGH confidence)
- BGG, *Using the XML API* (version date 2025-07-02), via Internet Archive capture — registration, tokens, licences, usage limits, logo requirement, private APIs unlicensed: https://boardgamegeek.com/using_the_xml_api
- BGG, *XML API Terms of Use* — non-commercial licence, credit and logo, no modification, no AI training: https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use
- BGG, *XML API Commercial Use* — what counts as commercial: https://boardgamegeek.com/wiki/page/BGG_XML_API_Commercial_Use
- BGG, *XML API2* wiki — host, 5 s spacing, 20-id cap, 202 queueing, subtype bug, `showprivate`, `modifiedsince`: https://boardgamegeek.com/wiki/page/BGG_XML_API2
- Microsoft Learn — HTTP resilience defaults; `System.Random` and `string.GetHashCode` stability notes; forwarded-headers breaking changes in .NET 8 and 10
- GitHub Docs — environments auto-creation, REST rate limits and conditional requests, artifact attestations (public repos), immutable releases (GA 2025-10-28)
- Traefik docs — RateLimit (`ipStrategy.depth`) and Headers middlewares
- Six Labors Split License v1.0 — ImageSharp licensing conditions
- WCAG 2.2 Target Size (Minimum); MDN `prefers-reduced-motion`, container query units; web.dev View Transitions Baseline
- Reference project, read directly — `docs/deploy.md`, `docs/lxc-setup.md`, `docs/releasing.md`, `docs/github-repository-settings.md`, `deploy/`, `.github/workflows/`, `build/`, `Directory.Build.props`, `global.json`
- NuGet registry queried 2026-10-03 for all package versions

### Secondary (MEDIUM confidence)
- Implementer reports of 2025–2026 BGG behaviour and terms: keeplore issue 88, nalanda PRs 39 and 58, gamecache issue 95, nextgamenight-backend PR 57 (Cloudflare and User-Agent, dated 2026-10-01), `BoardGamer.BoardGameGeek` and `gobgg` repositories
- BGG forum threads on private info (`showprivate` needs the logged-in owner), the subtype bug, the 20-item cap, version info in collections
- Comparable products: static Kallax collection viewer (`sirmmo/games`), The Shelf, TableTop Pick, BG Stats filters and expansion logging, BGG Collection Wall, LayerUp Games
- Box size and storage article (brainbaking.com, 2025)

### Tertiary (LOW confidence)
- Forum snippets claiming inventory location is not exposed even with `showprivate=1` — must be verified with a real token
- Conflicting forum threads on box-dimension availability via the API
- Old reports of image hotlink 403s and 429s after BGG's cloud move
- GameShelf.io and other listings only partially read

---
*Research completed: 2026-10-03*
*Ready for roadmap: yes*

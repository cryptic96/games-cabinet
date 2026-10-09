# Roadmap: Games Cabinet

## Overview

Games Cabinet goes from an empty public repository to a public, mobile-friendly wooden cabinet that mirrors the owner's BoardGameGeek "owned" collection with no manual entry. The journey front-loads the least reversible work (public repo guardrails and a rehearsed release pipeline), then builds the highest creative risk (the deterministic cabinet layout) on synthetic data while the BGG API token approval is pending. After that, the real collection arrives through a polite, guarded sync; box art and true box shapes enrich it; visitors get the pull-out detail card, accessibility and Dutch/English labels; the owner gets home/VPN-only tools to correct images and locations; and game-night filters and per-location cabinets complete the experience. The public route opens only after a hardening gate and go-public checklist pass.

Every phase is a vertical slice: it ends with something the owner can see or do on the deployed site.

## Phases

**Phase Numbering:**

- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [x] **Phase 1: Repo, Guardrails & Walking-Skeleton Deploy** - Public repo with enforced guardrails and a hello page proven through release, deploy and rollback (completed 2026-10-04)
- [x] **Phase 2: Layout Engine & Cabinet Prototype** - Deterministic, natural-looking cabinet on synthetic data, approved visually by the owner (completed 2026-10-06)
- [x] **Phase 3: BGG Access Spike, Real Sync & Snapshot** - The owner's real collection appears automatically in the deployed cabinet, resilient to BGG outages (completed 2026-10-07)
- [x] **Phase 4: Enrichment, Box Images & Shape** - Real box art, art-coloured spines, true box proportions and full game details, all served from the site (completed 2026-10-09)
- [ ] **Phase 5: Game Detail, Accessibility & Language** - Pull-out animation, detail card, keyboard and screen-reader access, English and Dutch labels
- [ ] **Phase 6: Owner Tools & Persistence** - Home/VPN-only image overrides (and location editing if needed), persisted and backed up
- [ ] **Phase 7: Game-Night Filters & Location Cabinets** - Dim-to-filter search and a per-location cabinet toggle
- [ ] **Phase 8: Public Hardening & Go-Public** - HTTPS exposure through Traefik with rate limits, headers, resource caps and a go-public checklist

## Phase Details

### Phase 1: Repo, Guardrails & Walking-Skeleton Deploy

**Goal:** A public repository with enforced guardrails and a proven release pipeline, so a hello page travels from semver tag to approved release to running LXC (and back via rollback) before any feature depends on it.
**Mode:** mvp
**Depends on:** Nothing (first phase)
**Requirements:** OPS-01, OPS-02, OPS-03, OPS-04, OPS-05
**Owner prerequisites**: Register the BGG application (non-commercial tier) on day one, because approval can take a week or more and gates Phase 3. Create the empty public GitHub repository, choose an OSI licence (MIT or Apache-2.0), confirm the public git identity before the first push, and apply the repository settings (protected `main`, tag ruleset, `deploy` environment with a required reviewer, immutable releases, push protection). Create the LXC on the Proxmox host. The router stays LAN-only until Phase 8.
**Success Criteria** (what must be TRUE):

  1. The public GitHub repository exists with a protected `main`, an OSI licence, and a clean scan: no personal data in code, docs, fixtures or git history.
  2. Opening a pull request runs build, tests and lint on GitHub-hosted runners, and `main` can change only through a pull request.
  3. Pushing a semver tag produces an attested draft release, and nothing is published until the owner approves it in the protected deploy environment.
  4. On the LXC, the pull timer finds the published release, verifies its attestation offline, installs it and health-checks it, and the hello page loads from the home network. A deliberately broken release rolls back automatically, health does not depend on BGG, and no self-hosted runner exists (GitHub never executes anything on the server).
  5. A fresh LXC can be created and provisioned from documented, repeatable scripts alone, and an image-processing smoke test passes inside it.

**Plans:** 15/15 plans complete

Plans:
**Wave 1**

- [x] 01-01-PLAN.md — Privacy gate: full-history scan script, scrub and in-place history rewrite (owner-approved)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 01-02-PLAN.md — Personal-data guard hooks (pre-commit, commit-msg, pre-push) and developer guide
- [x] 01-03-PLAN.md — App foundation: hello page with its version and loopback-only ops health
- [x] 01-04-PLAN.md — Lint framework: runner, repository and licence rules, shellcheck, gitleaks over all refs, script tests, MIT licence, README
- [x] 01-05-PLAN.md — Provisioning framework: packages, accounts, firewall, Traefik template, pinned versions

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 01-06-PLAN.md — Release packaging run from a symlinked release directory, SkiaSharp image-smoke subcommand
- [x] 01-07-PLAN.md — Workflow lint (actionlint, zizmor, pin rules) and Dependabot with grouped SkiaSharp updates

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 01-08-PLAN.md — Unit and integration tests pinning the health, page and configuration contract
- [x] 01-10-PLAN.md — Installer with rejected-version memory and quiet poll statuses, end-to-end and tamper tests

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 01-09-PLAN.md — CI and release workflows, tag validation, settings read-back script, settings and release docs
- [x] 01-11-PLAN.md — Systemd units, services module, on-host selfcheck with sandboxed image smoke, LXC setup guide

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 01-12-PLAN.md — Go-live: public repository, CI on the PR, every control applied and read back (owner-approved)

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 01-13-PLAN.md — First release v0.1.0 approved, pulled, verified, installed and health-checked on the LXC

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 01-14-PLAN.md — Rollback rehearsal: deliberately broken v0.1.1 rolls back and is skipped

**Wave 9** *(blocked on Wave 8 completion)*

- [x] 01-15-PLAN.md — Fixed v0.1.2 installs past the rejected version; final phase gate

### Phase 2: Layout Engine & Cabinet Prototype

**Goal:** A deterministic, natural-looking cabinet layout, built on synthetic data while the BGG approval is pending, that the owner has reviewed and approved visually from an empty cabinet up to several hundred games, on desktop and phone widths.
**Mode:** mvp
**Depends on:** Phase 1
**Requirements:** CAB-01, CAB-02, CAB-04, CAB-05, CAB-06, CAB-07, EXP-01, EXP-02, EXP-03
**Owner prerequisites**: Visual review of the deployed prototype on a desktop browser and a phone. This is the acceptance gate for the phase, and several review rounds are expected.
**Research focus**: Packing algorithm prototype loop (face-out versus spine decisions, irregular cubbies, expansion slots, narrow phone profile). Visual quality is subjective.
**Success Criteria** (what must be TRUE):

  1. The owner opens the deployed prototype with synthetic collections and approves that it reads as a real wooden cubby cabinet: face-out covers mixed with spines, packed full in irregular cubbies rather than a uniform grid.
  2. The cabinet grows with the collection: synthetic collections of 0, 1, 5, about 65 and 400 games all render without overlap or overflow, and empty or near-empty collections look intentional (a minimum cabinet, boxes facing out when there are few).
  3. The same collection always renders the same cabinet, and adding a plain game, or an expansion to an existing stack, changes at most one cubby (verified by automated tests); the documented exceptions (switching out of the few-games look, a base game's first expansion, an expansion that widens its family) are tested as their own cases (D-09, D-19, D-23).
  4. Each expansion appears with its name beside its base game: thick (big-box) expansions stand upright next to it and thin ones lie as thin sideways spines in a stack, thickest at the bottom (D-23, D-24); an expansion whose base game is absent stands alone, labelled with the game it expands; a base game with many expansions collapses the extras into a "+N more" stack that never overflows its shelf.
  5. On a phone-width screen the cabinet reflows into a narrower, taller cabinet that still looks like a cabinet, with spines readable and large enough to tap.

**Plans:** 9/9 plans complete

Plans:
**Wave 1**

- [x] 02-01-PLAN.md — Tracer: invented 65-item collection drawn as spines in wooden cubbies end to end; determinism and append-stability harness

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 02-02-PLAN.md — Shelf mix: per-game covers by strategy, flat stacks, few-games switch, layout settings validated at startup

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 02-03-PLAN.md — Box look: contrast-checked palette, generated covers, readable shortened labels, and the owner's chosen classic furniture finish (grain, moulded top, planked backs, plinth) with a screenshot check
- [x] 02-04-PLAN.md — Page and samples: sample switcher, load states, viewport profile, cached endpoint, prototype switch, layout guide

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 02-05-PLAN.md — Expansion families: reserved stacks beside base games, "+N more", orphans, family stability and the first-expansion exception

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 02-09-PLAN.md — Box poses: piles largest at the bottom, big boxes lie flat before a new section (server setting), thick expansions upright beside their base, stacks thickest at the bottom; before and after measurements and screenshots

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 02-06-PLAN.md — Phone cabinet with derived tap floors, design validation and box limits, recorded golden layouts

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 02-07-PLAN.md — Owner screenshot review rounds with geometry and strict-CSP checks, tuning until approved (owner-gated)

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 02-08-PLAN.md — Release through the existing pipeline and the owner's deployed check on desktop and phone (owner-gated)

**UI hint**: yes

### Phase 3: BGG Access Spike, Real Sync & Snapshot

**Goal:** The owner's real BGG owned collection appears automatically in the deployed cabinet, kept fresh by a polite hourly sync and a guarded "sync now", and the site keeps working when BGG does not.
**Mode:** mvp
**Depends on:** Phase 2
**Requirements:** SYNC-01, SYNC-02, SYNC-03, SYNC-04, SYNC-05, SYNC-08, LOC-02, SEC-05
**Owner prerequisites**: The BGG application is approved and the token issued (registered in Phase 1). The token and BGG username go only into the server-side environment file. The owner reads and signs off the recorded outcome of the location spike.
**Research focus**: Live token spike from the LXC: private inventory-location visibility, collection response shape, `own=1` and subtype behaviour, duplicate collection entries, 202/429/403 behaviour, honest User-Agent requirements.
**Success Criteria** (what must be TRUE):

  1. The owner marks a game as owned on BGG and, within about an hour or after a manual sync, it appears in the deployed cabinet with nothing entered in the app. The real collection of about 65 items, expansions included, shows by title (box art arrives in the next phase).
  2. Any visitor can press "sync now", but only one sync per cooldown window is allowed across all visitors, even across restarts; the button shows the time remaining, visitors can see when the collection was last synced, and no visitor input can supply a username or trigger any other BGG call. The token and username live only in server-side configuration.
  3. When BGG is down, throttling, returns an error, or returns a suspiciously empty or shrunken result, visitors keep seeing the last good collection with a "showing last sync from ..." note, and the cabinet is never wiped.
  4. Before the first successful sync, visitors see an intentional "cabinet is being filled" state instead of an error, and every public page carries the linked "Powered by BGG" credit.
  5. The location spike has been run with the real token from the LXC and its outcome recorded: either BGG exposes the private inventory location and the synced data carries each game's location, or it does not and Phase 6 builds the owner location tools.

**Plans:** 15/15 plans complete

Plans:
**Wave 1**

- [x] 03-01-PLAN.md — Shape-only BGG access check script and the owner's go-ahead (locations set, keys in the env file, run approved) (owner-gated)
- [x] 03-03-PLAN.md — Layout data for real games: entry id per copy, "Expansion" label without a known base, layout version 9
- [x] 03-04-PLAN.md — Shared page layout with the linked official "Powered by BGG" credit on every page (owner supplies the logo)

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 03-02-PLAN.md — Access check run from the container, shape-only outcome signed off by the owner and committed (owner-gated)
- [x] 03-05-PLAN.md — Page reads the synced collection store; "being filled" state; invented samples only in local development

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 03-06-PLAN.md — Local fake BGG, synthetic BGG-shaped XML and a scripted transport for tests

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 03-07-PLAN.md — Tracer: sync now fetches the owned collection from BGG, stores it atomically and shows it in the cabinet

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 03-08-PLAN.md — Shared persisted cooldown, hourly and start-up runs, status endpoint
- [x] 03-09-PLAN.md — Faithful copies, expansions, box sizes and locations; token and username provably server-side

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 03-10-PLAN.md — Failure classification, polite 202 polling, empty and shrunken results held back
- [x] 03-11-PLAN.md — "Synced ... ago" status line, stale note, node --test checks in CI

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 03-12-PLAN.md — Sync now button, countdown, own-press notes and quiet in-place redraw
- [x] 03-13-PLAN.md — Broadcast-only SignalR hub with transport and connection limits

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 03-14-PLAN.md — Live page updates through the vendored official SignalR client, three-engine CSP checks and screenshots (owner approves downloads)

**Wave 9** *(blocked on Wave 8 completion)*

- [x] 03-15-PLAN.md — Release and the owner's check of the real collection in the deployed cabinet (owner-gated)

**UI hint**: yes

### Phase 4: Enrichment, Box Images & Shape

**Goal:** Every game in the real cabinet shows its real box art (or a spine coloured from that art) at true proportions, with complete BGG details behind it, all served from the site itself.
**Mode:** mvp
**Depends on:** Phase 3
**Requirements:** SYNC-06, SYNC-07, IMG-01, IMG-03, CAB-03
**Research focus**: Coverage and units of version dimensions in BGG data, expansion shapes (standalone, big-box "contains", multi-parent), image CDN behaviour, detecting 3D or perspective product shots, spine colour extraction with readable text contrast.
**Success Criteria** (what must be TRUE):

  1. After a sync, every game carries player count, play time, weight, designers, mechanics, minimum age and BGG rating, and every owned expansion knows which base game(s) it expands, so real expansions sit beside their owned base games in the deployed cabinet.
  2. Box art is downloaded during sync, downscaled and served from the site itself; a visitor's browser never requests an image from BGG.
  3. Each box uses the owned version's image when it is a flat front cover, and falls back to the base game's main image when it looks like a 3D or perspective shot (a slanted box on a plain background), checked against the owner's real Dutch editions.
  4. Box proportions come from the owned version's real BGG dimensions when available, else from a flat cover's aspect ratio, else from a realistic default, and a 3D shot's outline is never used, so real boxes in the cabinet visibly differ in size and shape.
  5. Each spine takes its colour from its box art (ignoring plain backgrounds around product shots) and shows the title legibly with readable text contrast.

**Plans:** 23/23 plans complete

Plans:
**Wave 1**

- [x] 04-01-PLAN.md — Shape-only art check for version images, details and the image host, and the owner's go-ahead (owner-gated)
- [x] 04-02-PLAN.md — Tracer: real box art from the collection answer to face-out covers served from the site
- [x] 04-03-PLAN.md — Spine colour pair and image choice as tested pure functions

**Wave 2** *(blocked on Wave 1 completion)*

- [x] 04-04-PLAN.md — Synthetic box art fixtures and the picture analysis (detector features, art and edge colours)
- [x] 04-05-PLAN.md — Art check run from the container, shape-only outcome signed off by the owner (owner-gated)
- [x] 04-06-PLAN.md — Game details from BGG, weekly refresh, and expansions beside their owned base games
- [x] 04-07-PLAN.md — Thin boxes at true thickness with one-line titles, cover line steps, plinth apron, "+N more" name; layout version 10

**Wave 3** *(blocked on Wave 2 completion)*

- [x] 04-08-PLAN.md — Both candidate pictures, read-time choice and art-coloured spines with legible titles
- [x] 04-09-PLAN.md — Phone cabinet density measured and closed on realistic sizes

**Wave 4** *(blocked on Wave 3 completion)*

- [x] 04-10-PLAN.md — True box proportions: real sizes, flat cover shape, stable estimates, verdict-free poses
- [x] 04-11-PLAN.md — Local fake image host with synthetic art and a Development-only origin

**Wave 5** *(blocked on Wave 4 completion)*

- [x] 04-12-PLAN.md — Server-run review sheet and its font
- [x] 04-13-PLAN.md — Local screenshot review rounds with geometry, CSP and request checks (owner-gated)

**Wave 6** *(blocked on Wave 5 completion)*

- [x] 04-14-PLAN.md — Release for the server round; the real collection enriched and its art stored (owner-gated)

**Wave 7** *(blocked on Wave 6 completion)*

- [x] 04-15-PLAN.md — The owner's review of the real collection: sheet, cover shares, tuning (owner-gated; round 2 ran on v0.4.1, round 3 runs after 04-23 on v0.5.0)
- [x] 04-17-PLAN.md — Round-1 fixes: landscape flat covers turn real sizes, unsure landscape pictures turn boxes (new setting), detector root-caused for white, transparent and dark-bordered pictures, analysis version 2 (gap closure)

**Wave 8** *(blocked on Wave 7 completion)*

- [x] 04-18-PLAN.md — Release v0.4.1, deploy and bounded re-measure syncs on the container, games still waiting counted (owner-gated, gap closure)

**Wave 9** *(blocked on Wave 8 completion)*

- [x] 04-19-PLAN.md — Detector fix from the real round-2 failures: local measurement harness at several sizes, synthetic stand-ins for cut-outs, tight white crops and covers with coloured or dark fields (row 8's main picture included), analysis version 3, wrapped rules line on the review sheet, override-candidate rows recorded (gap closure)

**Wave 10** *(blocked on Wave 9 completion)*

- [x] 04-20-PLAN.md — Series stand together: BGG family links stored with a one-off details refresh, `Game:`/`Series:` families and title links, series placed as one block, `Layout:GroupSeries` switch, layout version 13 (gap closure)

**Wave 11** *(blocked on Wave 10 completion)*

- [x] 04-21-PLAN.md — Families with two or more expansions face out (`Layout:CoverFromExpansions`), family columns continue into the next cubby on the same shelf, EXP-03 invariant (gap closure)

**Wave 12** *(blocked on Wave 11 completion)*

- [x] 04-22-PLAN.md — Desktop density retune on a realistic size mix, phone no worse, local review round of the fake collection (owner-gated, gap closure)

**Wave 13** *(blocked on Wave 12 completion)*

- [x] 04-23-PLAN.md — Release v0.5.0, deploy and bounded syncs for the picture re-measure and the details refresh, counts only (owner-gated, gap closure)

**Wave 14** *(blocked on Wave 13 completion)*

- [x] 04-16-PLAN.md — Tuned defaults committed, final release and the owner's deployed check (owner-gated)

### Phase 5: Game Detail, Accessibility & Language

**Goal:** A visitor can tap any box, watch it slide out of the shelf, and read everything needed to pick it for game night in English or Dutch, by touch, mouse, keyboard or screen reader.
**Mode:** mvp
**Depends on:** Phase 4
**Requirements:** DET-01, DET-02, DET-03, A11Y-01, A11Y-02, I18N-01
**Success Criteria** (what must be TRUE):

  1. Tapping or clicking a box, on desktop or phone, pulls it out of the shelf with an animation and then opens its detail card.
  2. The detail card shows the cover, title and year; player count, play time and weight; storage location and owned expansions; BGG rating, designers, minimum age and mechanics; and a link to the game on BGG.
  3. A visitor who prefers reduced motion gets the detail card without the pull-out animation.
  4. A keyboard-only user can move through the games in the cabinet and open any detail card, and a screen-reader user gets an accessible list of all games behind the visual cabinet.
  5. The site's own labels appear in English or Dutch: the default follows the browser language, a visitor can switch, the choice is remembered, and game titles stay exactly as BGG provides them.

**Plans**: TBD
**UI hint**: yes

### Phase 6: Owner Tools & Persistence

**Goal:** From the home network or VPN only, the owner can correct box images (and, if BGG cannot supply storage locations, define and assign them), with that data surviving syncs, restarts and releases and being backed up.
**Mode:** mvp
**Depends on:** Phase 5
**Requirements:** IMG-02, LOC-03, LOC-04, OWN-01, OWN-02
**Owner prerequisites**: The home-network and VPN address ranges allowed to reach owner tools, kept in server-side configuration and never in the repository.
**Success Criteria** (what must be TRUE):

  1. From the home network or VPN, the owner can override any game's image with the owned version's image, the base game's image, or a specific image from the game's BGG gallery given by link, and the cabinet and detail card show the chosen image afterwards. Links that are not BGG image links are rejected.
  2. Owner screens and endpoints answer only to home-network or VPN clients; a request arriving through the public route is refused.
  3. Overrides (and locations, if edited in the app) survive syncs, restarts and new releases, and a backup exists from which a fresh container can be restored without losing them.
  4. Conditional on the Phase 3 spike outcome: if BGG cannot supply locations, the owner can define their own list of storage locations (rooms, cabinets, "Lent out") and assign one to each game; if BGG does supply them, this criterion is closed by recording that no location editor is needed.

**Plans**: TBD
**UI hint**: yes

### Phase 7: Game-Night Filters & Location Cabinets

**Goal:** A visitor can narrow the cabinet to tonight's game by dimming everything else, and can switch between one big cabinet and one cabinet per storage location.
**Mode:** mvp
**Depends on:** Phase 6
**Requirements:** FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05
**Success Criteria** (what must be TRUE):

  1. A visitor can search by name, filter by player count (games whose range includes the chosen number), filter by play time (games taking at most the chosen minutes) and filter by storage location, and non-matching games dim in place instead of disappearing or moving.
  2. Filters combine, a match count is shown, and one tap clears all filters.
  3. An expansion dims or lights up together with its base game when filters are applied.
  4. Every game's storage location is visible to every visitor, whether it was read from BGG or entered by the owner.
  5. A visitor can toggle between one big cabinet and one cabinet per storage location, with games that have no location placed in an "Unknown location" cabinet.

**Plans**: TBD
**UI hint**: yes

### Phase 8: Public Hardening & Go-Public

**Goal:** The site is reachable from the internet over HTTPS through the existing reverse proxy, resilient to traffic floods and unable to starve the other guests on the host, and the owner opens it only after a go-public checklist passes.
**Mode:** mvp
**Depends on:** Phase 7
**Requirements:** SEC-01, SEC-02, SEC-03, SEC-04, SEC-06
**Owner prerequisites**: DNS name, TLS certificate and router port-forward for the public route (details kept private, outside the repository), and the owner's decision on when to go public. Re-read the BGG terms of use before go-live.
**Success Criteria** (what must be TRUE):

  1. From the public internet the site loads over HTTPS through Traefik, while health and ops endpoints and every owner tool are unreachable (confirmed by an external probe).
  2. Rapid repeated requests from one client are rate-limited, with a much stricter limit on "sync now".
  3. Every response carries security headers including a strict Content-Security-Policy, and the cabinet works fully under that policy with no violations.
  4. The container has CPU and memory caps sized from real image-processing and sync load, and a flood of requests slows the app without starving the other guests on the host.
  5. A go-public checklist (headers, rate limits, health not routed, BGG credit present, BGG terms re-read, no personal data in the repository) is completed, and only then does the router open the public route.

**Plans**: TBD

## Progress

**Execution Order:**
Phases execute in numeric order: 1 → 2 → 3 → 4 → 5 → 6 → 7 → 8

| Phase | Plans Complete | Status | Completed |
|-------|----------------|--------|-----------|
| 1. Repo, Guardrails & Walking-Skeleton Deploy | 15/15 | Complete    | 2026-10-04 |
| 2. Layout Engine & Cabinet Prototype | 9/9 | Complete    | 2026-10-06 |
| 3. BGG Access Spike, Real Sync & Snapshot | 15/15 | Complete    | 2026-10-07 |
| 4. Enrichment, Box Images & Shape | 23/23 | Complete    | 2026-10-09 |
| 5. Game Detail, Accessibility & Language | 0/0 | Not started | - |
| 6. Owner Tools & Persistence | 0/0 | Not started | - |
| 7. Game-Night Filters & Location Cabinets | 0/0 | Not started | - |
| 8. Public Hardening & Go-Public | 0/0 | Not started | - |

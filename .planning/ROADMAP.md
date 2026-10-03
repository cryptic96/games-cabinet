# Roadmap: Games Cabinet

## Overview

Games Cabinet goes from an empty public repository to a public, mobile-friendly wooden cabinet that mirrors the owner's BoardGameGeek "owned" collection with no manual entry. The journey front-loads the least reversible work (public repo guardrails and a rehearsed release pipeline), then builds the highest creative risk (the deterministic cabinet layout) on synthetic data while the BGG API token approval is pending. After that, the real collection arrives through a polite, guarded sync; box art and true box shapes enrich it; visitors get the pull-out detail card, accessibility and Dutch/English labels; the owner gets home/VPN-only tools to correct images and locations; and game-night filters and per-location cabinets complete the experience. The public route opens only after a hardening gate and go-public checklist pass.

Every phase is a vertical slice: it ends with something the owner can see or do on the deployed site.

## Phases

**Phase Numbering:**
- Integer phases (1, 2, 3): Planned milestone work
- Decimal phases (2.1, 2.2): Urgent insertions (marked with INSERTED)

Decimal phases appear between their surrounding integers in numeric order.

- [ ] **Phase 1: Repo, Guardrails & Walking-Skeleton Deploy** - Public repo with enforced guardrails and a hello page proven through release, deploy and rollback
- [ ] **Phase 2: Layout Engine & Cabinet Prototype** - Deterministic, natural-looking cabinet on synthetic data, approved visually by the owner
- [ ] **Phase 3: BGG Access Spike, Real Sync & Snapshot** - The owner's real collection appears automatically in the deployed cabinet, resilient to BGG outages
- [ ] **Phase 4: Enrichment, Box Images & Shape** - Real box art, art-coloured spines, true box proportions and full game details, all served from the site
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
**Plans**: TBD

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
  3. The same collection always renders the same cabinet, and adding a game does not move any existing box (verified by automated tests).
  4. Each expansion appears as a thin sideways spine with its name beside its base game; an expansion whose base game is absent stands alone, labelled with the game it expands; a base game with many expansions collapses the extras into a "+N more" stack that never overflows its shelf.
  5. On a phone-width screen the cabinet reflows into a narrower, taller cabinet that still looks like a cabinet, with spines readable and large enough to tap.
**Plans**: TBD
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
**Plans**: TBD
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
**Plans**: TBD

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
| 1. Repo, Guardrails & Walking-Skeleton Deploy | 0/0 | Not started | - |
| 2. Layout Engine & Cabinet Prototype | 0/0 | Not started | - |
| 3. BGG Access Spike, Real Sync & Snapshot | 0/0 | Not started | - |
| 4. Enrichment, Box Images & Shape | 0/0 | Not started | - |
| 5. Game Detail, Accessibility & Language | 0/0 | Not started | - |
| 6. Owner Tools & Persistence | 0/0 | Not started | - |
| 7. Game-Night Filters & Location Cabinets | 0/0 | Not started | - |
| 8. Public Hardening & Go-Public | 0/0 | Not started | - |

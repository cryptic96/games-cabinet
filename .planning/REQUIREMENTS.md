# Requirements: Games Cabinet

**Defined:** 2026-10-03
**Core Value:** Anyone with the link sees an up-to-date, good-looking cabinet of exactly the games the owner owns on BGG, with no manual data entry in the app.

## v1 Requirements

Requirements for the initial release. Each maps to a roadmap phase.

### BGG Sync

- [ ] **SYNC-01**: The owner's BGG "owned" collection (base games and expansions) syncs automatically about every hour. Nothing has to be entered in the app.
- [ ] **SYNC-02**: A visitor can press "sync now". One global cooldown, shared by all visitors and persisted across restarts, allows at most one sync per cooldown window. While it is active the button shows the time remaining.
- [ ] **SYNC-03**: Visitors can see when the collection was last synced.
- [ ] **SYNC-04**: When BGG is down, throttling, or returns an error or a suspiciously empty or shrunken result, visitors keep seeing the last good collection with a "showing last sync from …" note. The cabinet is never wiped.
- [ ] **SYNC-05**: Before the first successful sync, visitors see an intentional "cabinet is being filled" state instead of an error.
- [ ] **SYNC-06**: Each game is enriched from BGG with:
  - player count and play time
  - weight, designers, mechanics and minimum age
  - BGG rating
  - for expansions, which base game(s) it expands
- [ ] **SYNC-07**: Box art is downloaded during sync, downscaled and served from the site itself. Visitors' browsers never load images from BGG.
- [ ] **SYNC-08**: Every public page credits BGG with the linked "Powered by BGG" logo, as BGG's API licence requires.

### Cabinet

- [x] **CAB-01**: Visitors see every owned game in a cabinet drawn to fit the collection. Shelves and cubbies grow with it, from an empty collection to several hundred games. The design target is the current collection: about 65 owned items, expansions included.
- [x] **CAB-02**: Boxes appear as a mix of face-out covers and spines, like a real game shelf.
- [ ] **CAB-03**: Each spine shows the game's title legibly, in a colour taken from its box art, with readable text contrast. Plain backgrounds around product shots are ignored when picking the colour.
- [x] **CAB-04**: Boxes are packed to look natural and full, sized by box shape (see IMG-03), in irregular cubbies like the inspiration photo rather than a uniform grid.
- [x] **CAB-05**: The layout is stable: the same collection always renders the same cabinet, and adding a game does not reshuffle existing boxes.
- [x] **CAB-06**: Small or empty collections look intentional. There is always a minimum cabinet, and boxes face out when there are only a few.
- [x] **CAB-07**: On phones, the cabinet reflows into a narrower, taller cabinet that still looks like a cabinet, with readable and tappable spines.

### Expansions

- [x] **EXP-01**: Each owned expansion appears with its name right beside its owned base game: thick (big-box) expansions stand upright as spines next to the base, thin ones lie as thin sideways spines in a stack beside it.
- [x] **EXP-02**: An owned expansion whose base game is not owned still appears, as its own spine, labelled with the game it expands.
- [x] **EXP-03**: A base game with many expansions collapses the extras into a "+N more" stack, so a family never overflows its shelf.
- [ ] **EXP-04**: An expansion dims or lights up together with its base game when filters are applied.

### Game Detail

- [ ] **DET-01**: Tapping or clicking a game pulls its box out of the shelf with an animation, then opens a detail card.
- [ ] **DET-02**: The detail card shows:
  - cover, title and year
  - player count, play time and weight
  - storage location and owned expansions
  - BGG rating, designers, minimum age and mechanics
  - a link to the game on BGG
- [ ] **DET-03**: Visitors who prefer reduced motion get the detail card without the pull-out animation.

### Filters

- [ ] **FILT-01**: A visitor can search games by name.
- [ ] **FILT-02**: A visitor can filter by player count; this shows games whose player range includes the chosen number.
- [ ] **FILT-03**: A visitor can filter by play time; this shows games that take at most the chosen number of minutes.
- [ ] **FILT-04**: A visitor can filter by storage location.
- [ ] **FILT-05**: Non-matching games dim instead of disappearing. Filters combine, a match count is shown, and one tap clears all filters.

### Storage Location

- [ ] **LOC-01**: Each game's storage location is visible to every visitor.
- [ ] **LOC-02**: An early spike with the real BGG token determines whether BGG exposes the private "inventory location" field to the app. The outcome is recorded.
  - **If it does:** the owner sets locations on BGG and the app reads them.
  - **If it does not:** LOC-03 and LOC-04 are built as owner tools.
- [ ] **LOC-03**: The owner can define their own list of storage locations, for example rooms, cabinets, or "Lent out".
- [ ] **LOC-04**: The owner can assign one of those locations to each game.
- [ ] **LOC-05**: Visitors can toggle between one big cabinet and one cabinet per storage location. Games without a location go in an "Unknown location" cabinet.

### Box Images & Shape

- [ ] **IMG-01**: Each game's box image defaults to the owned version's image when that image is a flat front cover. When it looks like a 3D or perspective product shot (a slanted box on a plain background), the base game's main image is used instead.
- [ ] **IMG-02**: The owner can override any game's image with one of:
  - the owned version's image
  - the base game's image
  - a specific image from the game's BGG gallery, given by link (only BGG image links are accepted)
- [ ] **IMG-03**: Box proportions come from the first of these that is available:
  1. the owned version's real dimensions in BGG
  2. a flat cover's aspect ratio
  3. a realistic default

  A 3D shot's outline is never used.

### Owner Tools

- [ ] **OWN-01**: Owner tools (image overrides, and location editing if LOC-02 requires it) work only from the home network or VPN. The public internet can never reach owner screens or endpoints.
- [ ] **OWN-02**: Owner-entered data (image overrides, and locations if edited in the app) survives syncs, restarts and new releases. It is backed up, so losing the container does not lose it.

### Language

- [ ] **I18N-01**: The site's own labels are available in English and Dutch. The default follows the browser language, a visitor can switch, and the choice is remembered. Game titles stay as BGG provides them.

### Accessibility

- [ ] **A11Y-01**: Keyboard users can move through the games in the cabinet and open any game's detail card.
- [ ] **A11Y-02**: Screen-reader users get an accessible list of all games behind the visual cabinet.

### Repository & Release

- [x] **OPS-01**: A public GitHub repository exists with:
  - a protected `main`
  - an OSI licence
  - no personal data in code, docs, test fixtures or git history
- [x] **OPS-02**: Every pull request runs build, tests and lint on GitHub-hosted runners.
- [x] **OPS-03**: Pushing a semver tag builds an attested release as a draft. It is published only after the owner approves it through a protected deploy environment.
- [x] **OPS-04**: The server pulls the newest published release on a timer, verifies its attestation, installs it, health-checks it, and rolls back automatically on failure. There is no self-hosted runner, and no CI-executed code runs on the server.
- [x] **OPS-05**: The app's LXC on Proxmox can be created and provisioned from documented, repeatable scripts.

### Public Exposure & Security

- [ ] **SEC-01**: The site is reachable from the public internet over HTTPS through the existing Traefik reverse proxy.
- [ ] **SEC-02**: Public traffic is rate-limited, with a much stricter limit on "sync now".
- [ ] **SEC-03**: Every response carries security headers, including a strict Content-Security-Policy.
- [ ] **SEC-04**: Health and ops endpoints are not reachable from the internet, and neither are any owner tools (OWN-01).
- [ ] **SEC-05**: The BGG username and token exist only in server-side configuration. Visitors can never supply a username or cause BGG calls beyond the cooldown-guarded sync.
- [ ] **SEC-06**: The app's container has CPU and memory caps, so a traffic flood cannot starve the other guests on the host.

## v2 Requirements

Deferred to a future release. Tracked but not in the current roadmap.

### Game Night

- **NIGHT-01**: Filter by "best at N players", using BGG's community poll
- **NIGHT-02**: Filter by weight, with Light / Medium / Heavy labels
- **NIGHT-03**: A "What should we play?" random picker that respects active filters and pulls the chosen box out
- **NIGHT-04**: Search by co-op, designer or mechanic

### Sharing

- **SHARE-01**: Filters and the selected game live in the URL, so a filtered view can be shared as a link
- **SHARE-02**: Sharing a game link shows its cover and title as a link preview in chat apps
- **SHARE-03**: A "brass plaque" with collection stats: games, expansions, locations, last synced
- **SHARE-04**: A "new arrival" marker on recently added games

### Cabinet Extras

- **CABX-01**: Decorative wooden drawers along the bottom of the cabinet
- **CABX-02**: A cabinet image used as the site's own link preview
- **CABX-03**: A wood finish or theme the owner can choose
- **CABX-04**: Experiment: automatically straighten 3D box shots into flat covers. Check BGG's no-modification rule first.

### Operations

- **OPSX-01**: The owner gets an email when a deploy or a sync fails

## Out of Scope

Explicitly excluded. Documented to prevent scope creep.

| Feature | Reason |
|---------|--------|
| User accounts or login | Anyone with the link views the same cabinet; nothing public to personalise or protect |
| Adding or editing games in the app | BGG is the single source of truth for what is owned |
| Wishlist, preordered, for-trade or previously-owned games | Owner wants owned games only |
| Location as a `Location: …` line in the public BGG comment | Owner declined: the location text would be public on BGG itself |
| Storing the owner's BGG password or session cookie on the server | Security risk on a public server, and BGG's private site APIs are not licensed |
| Entering any BGG username in the site | Would turn the site into a proxy that hammers BGG |
| Hotlinking BGG images or calling BGG per visitor | Rate limits, fragility and BGG terms; everything is served from the synced snapshot |
| Sort controls on the cabinet | Conflict with the stable, packed layout; filter-and-dim replaces sorting |
| Play logging, play stats, voting, recommendations | A different product; needs write access or accounts |
| Drag-and-drop or user-arranged layouts | The layout is generated and deterministic |
| WebGL / 3D engine | Heavy on phones and on the low-power host; CSS rendering covers the look |
| Ads, donations, affiliate links, merchandise | BGG's non-commercial API licence forbids them |
| GitLab runner or self-hosted CI runner | The ing-dashboard pull model is the target |
| Migrating another existing project to the new deploy model | A separate project |

## Traceability

Which phases cover which requirements. Updated during roadmap creation.

| Requirement | Phase | Status |
|-------------|-------|--------|
| SYNC-01 | Phase 3 | Pending |
| SYNC-02 | Phase 3 | Pending |
| SYNC-03 | Phase 3 | Pending |
| SYNC-04 | Phase 3 | Pending |
| SYNC-05 | Phase 3 | Pending |
| SYNC-06 | Phase 4 | Pending |
| SYNC-07 | Phase 4 | Pending |
| SYNC-08 | Phase 3 | Pending |
| CAB-01 | Phase 2 | Complete |
| CAB-02 | Phase 2 | Complete |
| CAB-03 | Phase 4 | Pending |
| CAB-04 | Phase 2 | Complete |
| CAB-05 | Phase 2 | Complete |
| CAB-06 | Phase 2 | Complete |
| CAB-07 | Phase 2 | Complete |
| EXP-01 | Phase 2 | Complete |
| EXP-02 | Phase 2 | Complete |
| EXP-03 | Phase 2 | Complete |
| EXP-04 | Phase 7 | Pending |
| DET-01 | Phase 5 | Pending |
| DET-02 | Phase 5 | Pending |
| DET-03 | Phase 5 | Pending |
| FILT-01 | Phase 7 | Pending |
| FILT-02 | Phase 7 | Pending |
| FILT-03 | Phase 7 | Pending |
| FILT-04 | Phase 7 | Pending |
| FILT-05 | Phase 7 | Pending |
| LOC-01 | Phase 7 | Pending |
| LOC-02 | Phase 3 | Pending |
| LOC-03 | Phase 6 | Pending (conditional on LOC-02 outcome) |
| LOC-04 | Phase 6 | Pending (conditional on LOC-02 outcome) |
| LOC-05 | Phase 7 | Pending |
| IMG-01 | Phase 4 | Pending |
| IMG-02 | Phase 6 | Pending |
| IMG-03 | Phase 4 | Pending |
| OWN-01 | Phase 6 | Pending |
| OWN-02 | Phase 6 | Pending |
| I18N-01 | Phase 5 | Pending |
| A11Y-01 | Phase 5 | Pending |
| A11Y-02 | Phase 5 | Pending |
| OPS-01 | Phase 1 | Complete |
| OPS-02 | Phase 1 | Complete |
| OPS-03 | Phase 1 | Complete |
| OPS-04 | Phase 1 | Complete |
| OPS-05 | Phase 1 | Complete |
| SEC-01 | Phase 8 | Pending |
| SEC-02 | Phase 8 | Pending |
| SEC-03 | Phase 8 | Pending |
| SEC-04 | Phase 8 | Pending |
| SEC-05 | Phase 3 | Pending |
| SEC-06 | Phase 8 | Pending |

**Coverage:**

- v1 requirements: 51 total
- Mapped to phases: 51
- Unmapped: 0 ✓

---
*Requirements defined: 2026-10-03*
*Last updated: 2026-10-03 after roadmap creation (traceability filled)*

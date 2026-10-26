# Games Cabinet

## What This Is

A public, mobile-friendly web app that shows the owner's board game collection as a virtual wooden cabinet: box art facing out and generated spines packed onto shelves, like a real game shelf. The collection comes straight from the owner's BoardGameGeek (BGG) "owned" collection, so adding a game on BGG makes it appear on the site automatically. It is for friends picking a game for game night, for the owner to see what they have and where it is stored, and for showing the collection off.

## Core Value

Anyone with the link sees an up-to-date, good-looking cabinet of exactly the games the owner owns on BGG, with no manual data entry in the app.

## Requirements

### Validated

- ✓ Runs in its own new LXC on the Proxmox host, created and provisioned from documented scripts — Phase 1
- ✓ Releases follow the ing-dashboard model: semver tag → attested draft → owner approval in the `deploy` environment → server timer pulls, verifies offline, installs, health-checks and rolls back automatically. No self-hosted runner — Phase 1 (rollback rehearsed for real)
- ✓ Public GitHub repository with `main` protected, required PR checks and personal-data guardrails (hooks, full-history scan, secrets lint) — Phase 1
- ✓ Cabinet is drawn dynamically from a deterministic layout engine: sections of irregular cubbies grow with the collection from an empty cabinet to several hundred games, and the same collection always gives the same cabinet — Phase 2 (v0.2.0, owner-approved on desktop and phone)
- ✓ Boxes shown as a natural mix like a real shelf: face-out covers, upright spines and flat piles (widest at the bottom, big boxes lying flat before a new section opens), packed full in irregular cubbies — Phase 2 (generated covers until real box art arrives)
- ✓ Expansions sit beside their base game: thick big-box expansions stand upright, thin ones lie in a stack thickest at the bottom, overflow collapses into "+N more", and expansions without an owned base are labelled with the game they expand — Phase 2
- ✓ On phones the cabinet reflows into its own narrower, taller cabinet with readable, tappable spines — Phase 2
- ✓ Empty and near-empty collections look intentional: a trimmed minimum cabinet of bare planked wood, and boxes face out when there are few — Phase 2
- ✓ Owned collection syncs from BGG automatically (hourly, plus a guarded start-up run), with no manual game entry in the app; the last good collection stays on screen when BGG fails, and empty or more-than-halved results are held back until confirmed — Phase 3 (v0.3.0, owner-approved on the deployed site)
- ✓ Manual "sync now" button for every visitor, guarded by one shared, persisted 10-minute window; open pages update live when any sync changes the collection — Phase 3
- ✓ Storage-location question answered by a one-time, shape-only access check: BGG's private location is not readable with the application token, so locations come from home/VPN-only owner tools — Phase 3
- ✓ BGG token and username stay on the server; the "Powered by BGG" credit links back on every page — Phase 3
- ✓ Face-out boxes show the real BGG box art, downloaded once by the server and served from the site itself, and spines take their colour from the art with readable contrast — Phase 4 (v0.5.x, owner-approved on the deployed site)
- ✓ Box images: the owned version's picture when it is a flat cover, otherwise the game's main picture ("flat covers first", confirmed by the owner on the real collection) — Phase 4; per-game owner overrides follow in the owner-tools phase
- ✓ Box proportions come from the owned version's real BGG dimensions when available, else from a flat cover's shape, else from an estimate; landscape covers turn their boxes wide — Phase 4
- ✓ Every owned game carries its BGG details (players, play time, weight, rating, designers, mechanics, minimum age, expansion and family links), refreshed politely within the hourly sync — Phase 4
- ✓ Series stand together and a base game with two or more owned expansions faces out, its expansions continuing into the next cubby when needed — Phase 4 (owner request during the server review)
- ✓ Tapping a game pulls the box out of the shelf (view-transition animation, plain fade where unsupported) and opens a detail card: cover, title, year, players, play time, weight, minimum age, owned expansions (tap to swap), BGG rating, designers, mechanics and a link to BGG; the storage-location row is built and shows as soon as locations exist (owner tools) — Phase 5 (v0.6.0, owner-checked on iPhone Safari, desktop and Firefox)
- ✓ Reduced motion gets the card with a fade and the box outlined in place, no pull-out — Phase 5
- ✓ Keyboard: the cabinet is one tab stop with arrow, Home and End navigation and Enter to open; screen readers get a skip link and an A to Z list of every game whose entries open the card — Phase 5 (Android Back checked on a real phone in Phase 7; the real screen-reader listen is deferred by the owner and kept as a todo)
- ✓ Site labels in English and Dutch: the page follows the browser language from the first byte, an EN · NL toggle switches in place and is remembered in one cookie, and game titles stay exactly as BGG provides them — Phase 5
- ✓ Owner tools on their own port and hostname, reachable only from the home network or VPN (proxy allow-list, container firewall, a listener wall and a client-range wall that fails closed unless the request came through the configured proxy); the public site answers 404 for every owner path and asset — Phase 6 (v0.7.0, owner-verified from home and from outside)
- ✓ The owner chooses each game's picture (owned edition, main picture or a pasted BGG gallery link, previewed on a mini shelf) from a "Worth a look" worklist, and every visitor's cabinet and card follow — Phase 6
- ✓ The owner keeps an ordered list of storage locations and places games (row picker, bulk move, expansions follow their base); every visitor's card shows the location — Phase 6
- ✓ Owner data lives in its own file beside the synced collection and survives syncs, restarts and releases (ten earlier versions kept, change log with Undo); backed up by the owner's own download and restorable after a strict check and a side-by-side preview — Phase 6 (live backup and restore round trip done by the owner)
- ✓ Game-night filters: search by name (also across a game's other BGG titles), player count, play time and storage location combine; non-matching games dim in place, an expansion follows its base game, a match count shows and one tap clears everything — Phase 7 (v0.10.0, owner-checked on the deployed site and a real Android phone)
- ✓ Visitors toggle between one big cabinet and one cabinet per storage location, in the owner's order, with unplaced games in an "Unknown location" cabinet — Phase 7
- ✓ Filters stay out of sight until asked for: a folded rail of symbols at the left on desktop opens a filter panel (beside the cabinet on wide screens, over it below that), and phones keep a slim bar with a Filters sheet; the choices last for the tab and the view for the device — Phase 7 (owner-requested rework after v0.9.0)
- ✓ Storage locations are kept per copy, so two copies of one game can stand in different places; the owner tools place each copy, and the location list's game counts and delete warnings count copies — Phase 7
- ✓ Site is public and read-only with no accounts, reachable over HTTPS through the existing Traefik reverse proxy; the LAN/VPN allow-list was dropped from the public route only after a go-public checklist passed, and the owner tools, health and ops endpoints stay unreachable from the internet (phone probe from mobile data; SSL Labs, securityheaders.com and the Mozilla Observatory all A+) — Phase 8 (opened 2026-10-26, v1.0.0, patched as v1.0.1)
- ✓ Floods are limited per visitor address at the proxy (rate and in-flight) and in the app (sync presses, live connections, a site-wide cap on new live connections); every answer carries a strict CSP and the full security-header set — Phase 8
- ✓ The service runs under measured memory and CPU caps with a .NET heap limit, cannot reach the home network, and a flood slows it without starving other guests; the one neighbour slowdown seen (the shared proxy's single core refusing a single-address flood) was accepted by the owner under an amended definition — Phase 8
- ✓ The two maintenance accesses (cabinet and reverse proxy) are kept by the owner's decision, pinned to the workstation and reachable only from the admin sources; review point 2027-01-24 — Phase 8

### Active

- [ ] Follow-ups after going public (todos): a not-found page instead of an empty 404, Chromium page stalls on CI outside the containment test, rotating the reverse proxy's access log (owner maintenance), the intermittent test-host crash; optional: the sync button only for the home network and VPN

### Out of Scope

- User accounts or login — anyone with the link can view, and there is nothing to edit in the app
- Adding or editing games in the app — BGG is the single source of truth, and the owner confirmed BGG covers all their games
- Non-owned BGG statuses (wishlist, preordered, for trade, previously owned) — the owner wants owned games only
- Location as a `Location: …` line in the public BGG comment — owner declined: the text would be public on BGG itself
- Storing the owner's BGG password or session cookie on the server — security risk; BGG's private site APIs are unlicensed
- Game-night extras (best-at-N, weight filter, random picker, co-op/designer search) and sharing extras (URL state, link previews, stats plaque, new-arrival marker) — deferred to v2 by the owner
- A self-hosted CI runner or push-style deployment — the ing-dashboard pull model is the target
- Migrating another existing project to the ing-dashboard deploy model — a separate project

## Context

**Homelab.** Proxmox VE on a low-power mini PC shared with other guests, so the app must stay lightweight. An existing Traefik reverse-proxy LXC publishes other services on public subdomains, there is no global IPv6, and public traffic arrives via IPv4 NAT to Traefik. Environment details are kept outside this repo; never put addresses or hostnames in it.

**Reference project for CI/CD and deployment:** the public `ing-dashboard` repository. It is a .NET 10 layered solution (Domain / Repository / Service projects, `Directory.Build.props` with nullable + warnings-as-errors + lock files, `global.json`). On a semver tag GitHub Actions builds the release, attests its provenance and attaches it to a draft release. The owner approves publishing. A systemd timer on the server polls for the newest published release, verifies the attestation offline, installs it, health-checks it and rolls back automatically if needed. One-time provisioning scripts live under `deploy/`, with a Traefik dynamic-config example. See `docs/lxc-setup.md` and `docs/deploy.md` there. No code that ran in CI ever runs on the server.

**Not the model to follow:** an older project that deploys through a GitLab runner and uses EF Core with SQL Server.

**Visual inspiration:** a photo of a real wooden, cubby-style board game cabinet. It has irregular cubbies; big boxes face out (Scythe, War of the Ring, Wingspan, Star Wars: Rebellion), while smaller ones stand as vertical spines in rows or lie stacked. There are wooden drawers along the bottom. The app should evoke that look, rendered dynamically.

**BGG data:**
- The owner has now recorded (nearly) all games: **about 65 owned items, expansions included**. This is the design target; growth to several hundred must still work.
- The owner has selected the **owned version** of each game on BGG. Dutch editions' version images are often **3D perspective product shots**, not flat covers, and the base game's default image doesn't always match the owned box either. That is why box images use a smart default plus owner override, and box proportions come from version dimensions rather than image aspect ratio.
- BGG changed XML API access in 2025 (application registration / token requirement). Research must confirm current access rules, rate limits and terms of use.
- BGG only provides front box images. Spines have to be generated. Box dimensions may be available via BGG version data, which would drive face-out vs spine and realistic packing. Research needed.
- **Storage location in BGG:** collection items carry private info, including an "inventory location" field. Reading it likely requires the owner's authenticated BGG session rather than an application token. Research needed. **Plan B is deliberately undecided** until research reports. Candidates: (a) a convention in the game's public BGG comment (e.g. "Location: …"), keeping the app database-free; (b) an in-app location editor reachable only from the home network/VPN, backed by a small database.

**Location access check (Phase 3, measured):** the shape-only check run once from the container found no private info on any of the 65 items with the application token alone, so plan B (b) stands: home/VPN-only owner tools for locations. BGG version dimensions are in inches, and the collection must be fetched as two calls (base games, expansions) because the default call labels expansions as base games.

**Plan B outcome (after research):** research found the private field is most likely not readable with an app token; it needs the owner's logged-in session, and possibly not even then. The owner rejected the public-comment convention. Decision: spike first; if the token can't read the field, build plan B (b), home/VPN-only owner tools for locations.

**Database (after research):** no database. The app keeps an atomic JSON snapshot of BGG data, a local image cache, and a small owner-data file (image overrides, plus locations if they are edited in the app) under the systemd state directory. The owner-data file is the only part that can't be rebuilt from BGG, so it is backed up. The shared database LXC is not used.

**BGG API access:** since 2025-07-02 BGG requires a registered application and a Bearer token. Approval can take a week or more, so the owner registers (non-commercial tier) right away. The licence requires a linked "Powered by BGG" credit and forbids ads and donations.

## Constraints

- **Tech stack**: .NET 10 / ASP.NET Core, structured like ing-dashboard — this is the owner's stack across projects. Frontend: Razor Pages, vanilla ES modules and modern CSS, with no SPA framework and no Node toolchain (see research/STACK.md).
- **Hosting**: new unprivileged Ubuntu LXC on the Proxmox host, behind the existing Traefik — low-power shared host, keep the footprint small.
- **Deployment**: no self-hosted CI runner; the server pulls approved, attested releases, and no GitHub-executed code runs on the server — the ing-dashboard security model.
- **Repository**: public GitHub repository (not yet created) — free artifact attestations and credential-less release downloads depend on it being public.
- **Privacy**: no personal data anywhere in the repo, including code, docs, fixtures, test data and commit messages. That covers the BGG username, real domain/hostnames, homelab IPs and real house location names. Personal configuration lives only in the server-side env file; examples use `example.com`-style placeholders; all test data is synthetic — the repo is public.
- **External dependency**: the BGG API — respect its rate limits and terms of use, cache aggressively, and keep serving the last good snapshot when BGG is down or slow.
- **Data source**: BGG is the single source of truth for which games are owned. Storage locations come from BGG only if the token spike shows the private field is readable; otherwise they come from the owner tools.
- **Conventions** (carried over from ing-dashboard):
  - No planning references (requirement IDs, phase/plan numbers, planning document names) outside `.planning/`; git commit messages are the only exception.
  - Comments are `///` XML doc summaries only, no `//` comments. If a line needs explaining, rename or extract it.
  - Never commit to `main`. All work, including planning docs, goes on branches (`milestone/v<N>-<name>`, `feature/<short-description>`) and lands via pull request.

## Key Decisions

| Decision | Rationale | Outcome |
|----------|-----------|---------|
| BGG "owned" collection is the single source of truth | Adding a game on BGG makes it appear on the site, so there is no data entry in the app | — Pending |
| Storage location: BGG private field if a token spike proves it readable, otherwise home/VPN-only owner tools; visible to all visitors | Owner rejected putting locations in public BGG comments; the private field is likely unreadable via token | — Pending (spike) |
| Owner tools (image overrides, maybe locations) reachable only from home network/VPN | No accounts needed; nothing writable on the public internet | — Pending |
| Box image: owned version's image if flat, else base image; owner override per game | Dutch version images are often 3D product shots | ✓ Good — "flat covers first" confirmed by the owner after three server review rounds (Phase 4); overrides pending (owner tools) |
| No database: JSON snapshot, image cache and a backed-up owner-data file | Data is small and mostly rebuildable from BGG; lighter than a shared SQL Server | — Pending |
| English and Dutch site labels | Friends are local; BGG titles stay as-is | — Pending |
| Game-night and sharing extras deferred to v2 | Keep v1 focused on the cabinet itself | — Pending |
| Public, read-only site with no accounts | Anyone with the link can view; nothing editable to protect | — Pending |
| Sync about hourly, plus a manual sync button with a global cooldown | Fresh enough after adding a game; can't be abused to hit BGG rate limits | — Pending |
| Mix of face-out boxes and generated spines, packed to look good | Mirrors the inspiration photo; looks like a real cabinet | ✓ Good — approved after two screenshot review rounds and on the deployed v0.2.0 (Phase 2); flat piles and big boxes lying flat added at the owner's request |
| Expansions as sideways spines beside their base game | Owner's idea; keeps families together visually | ✓ Revised — thick big-box expansions stand upright beside the base, thin ones stack thickest at the bottom (Phase 2, owner request after seeing the first families) |
| Toggle between one cabinet and one cabinet per location | Supports both browsing and "where is it?" | ✓ Good — one cabinet per location in the owner's order, Unknown location last (Phase 7, v0.10.0) |
| On mobile, reflow to a narrow, tall cabinet | Keeps the cabinet look on phones instead of falling back to a list | ✓ Good — separate phone section design with derived readability minimums; owner walked it on a real phone (Phase 2) |
| Tap shows a pull-out animation and a detail card | Delight plus the info friends need to pick a game | — Pending |
| Owned games only | Wishlist, preordered and for-trade statuses are not wanted | — Pending |
| Public internet exposure via existing Traefik | "Anyone with the link" must work from anywhere | — Pending |
| .NET 10 with the ing-dashboard release/deploy pattern | Owner's stack; proven secure pull-based deploy without a runner | ✓ Good — four releases shipped, automatic rollback proven on the real server (Phase 1) |
| Public GitHub repo | Required for free attestations and credential-less release pulls | ✓ Good — public with all controls read back; history scrubbed before go-live (Phase 1) |
| Planning and build work on `milestone/v1-games-cabinet`; `main` protected | Same branching rules as ing-dashboard | ✓ Good — main ruleset enforced; merge commits for the milestone branch, squash for small PRs (Phase 1) |
| Release workflow split into test, package and attest jobs; only attest can sign | Test code and test-only packages must not run inside the provenance boundary | ✓ Good (Phase 1 code review) |
| Owner's handle and first-name commit author display name are public | The handle is the repository owner, licence holder and release signer; emails stay noreply | ✓ Accepted (Phase 1 UAT) |
| Layout is a pure, versioned function of the collection, guarded by recorded golden layouts | Same collection, same cabinet; any engine change is a deliberate version bump, and nothing stored depends on the layout | ✓ Good — eight layout versions during Phase 2 with no stored state to migrate |
| Cabinet furniture uses the "classic" wooden finish, built as CSS on finish-neutral hooks | Owner picked it from mocked-up directions; other finishes and a lit-cubbies toggle stay possible as CSS swaps | ✓ Good (Phase 2); selectable finishes captured as a todo |
| The app sends its own strict Content-Security-Policy | The proxy sets none, so the policy travels with the app and is tested | ✓ Good (Phase 2 code review) |
| Box art is downloaded by the server only, token-less, from an allowlisted host, and served as own-origin WebP | Visitors never contact BGG or its image host; the CSP stays `default-src 'self'` | ✓ Good — browser audits found no foreign request (Phase 4) |
| Real-collection review artefacts (sheets, screenshots, measurements) never enter the repository | The repository is public; only synthetic screenshots are committed | ✓ Good — denylist and history scans clean after three server rounds (Phase 4) |
| Default cover share 33 percent, series grouped, families with two or more expansions face out | Picked by the owner on the real collection; about a third of games face out | ✓ Good — committed defaults in v0.5.1 (Phase 4) |
| Phase 4 code-review findings logged as todos rather than fixed, except the picture-pipeline robustness and oversized-picture fixes | Owner's choice; the security audit raised the oversized-picture risk to high | ✓ Fixed in v0.5.2; remaining items tracked as todos |
| Card data is its own undocumented endpoint with a fixed field set, versioned with the collection | Keeps the layout payload small and BGG relaying minimal; a details-only sync still refreshes every card | ✓ Good (Phase 5 security audit: 56 of 56 threats closed) |
| Dutch site name "Spellenkast"; the punched hole on the card dropped | Owner's review-round answers | ✓ Approved (Phase 5 review round) |
| A phone rotating across the phone breakpoint closes an open card | The cabinet is redrawn with a different arrangement, so the card would sit over moved boxes | ✓ Accepted by the owner (Phase 5 device check); revisit if it annoys in daily use |
| Browser tests run in their own CI job and test hosts dispose their file watchers | A full browser run leaked one inotify instance per host and hit the per-user limit | ✓ Good — 133 browser tests stable locally and in CI (Phase 5) |
| Owner data in its own file, never in the synced snapshot; one writer with revisions, Undo and ten earlier versions | Owner choices must survive syncs, restarts and releases, and a damaged or newer file must never be overwritten silently | ✓ Good — proven across sync, restart, release and a live restore round trip (Phase 6) |
| Backup by the owner's own download (reminder banner), not a scheduled job | Smallest moving part on a low-power host; the owner keeps the file off the server | ✓ Good — owner made a real backup and restored it in UAT (Phase 6) |
| Owner tools on a separate port and hostname behind the proxy allow-list, not a login | No accounts on a public site; nothing writable reachable from the internet | ✓ Good — walls proven by self-check, route check and an outside-network test (Phase 6) |
| Owner picture links only from BGG's own picture host | One fixed host keeps the server-side fetch safe and stays within BGG's gallery and terms | ✓ Confirmed by the owner (Phase 6): no other sources |
| Filters dim in place instead of hiding or moving games | The cabinet stays the same cabinet while narrowing; friends still see where everything stands | ✓ Good — owner-approved over two build review rounds (Phase 7) |
| Filter options folded away by default: a left rail and panel on desktop, a slim bar and sheet on phones | Owner disliked the filter bar above the cabinet after v0.9.0; the cabinet comes first | ✓ Good — reworked within Phase 7 and released as v0.10.0 |
| Storage locations per copy, not per game | Owners can keep two copies of one game in different places | ✓ Good — owner's choice during Phase 7 (copy key shown beside the game) |
| Real screen-reader listen deferred | The owner cannot test with a screen reader at the moment; automated tests cover the structure, roles, live regions and focus | ⚠ Deferred by the owner (Phase 7 device round); kept as a todo |

## Evolution

This document evolves at phase transitions and milestone boundaries.

**After each phase transition** (via `/gsd-transition`):
1. Requirements invalidated? → Move to Out of Scope with reason
2. Requirements validated? → Move to Validated with phase reference
3. New requirements emerged? → Add to Active
4. Decisions to log? → Add to Key Decisions
5. "What This Is" still accurate? → Update if drifted

**After each milestone** (via `/gsd-complete-milestone`):
1. Full review of all sections
2. Core Value check — still the right priority?
3. Audit Out of Scope — reasons still valid?
4. Update Context with current state

---
*Last updated: 2026-10-26 after Phase 8 (public hardening and go-public; the site opened on 2026-10-26 as v1.0.0, patched as v1.0.1)*

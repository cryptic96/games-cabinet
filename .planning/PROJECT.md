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

### Active

- [ ] Face-out boxes show the real BGG box art and spines take their colour from it (the mix itself shipped in Phase 2 with generated covers)
- [ ] Tapping a game pulls the box out of the shelf (animation) and opens a detail card: player count, play time, weight, storage location, expansions, BGG rating, designers, minimum age, mechanics, link to BGG
- [ ] Each game shows its storage location to every visitor. The access check proved BGG's private field is not readable with the token, so the owner manages locations in home/VPN-only owner tools
- [ ] Visitors can toggle between one big cabinet and one cabinet per storage location
- [ ] Box images: the owned version's image if it is a flat cover, otherwise the base game's image (Dutch editions are often 3D perspective shots). The owner can override per game from home/VPN
- [ ] Box proportions come from the owned version's real BGG dimensions when available
- [ ] Site labels in English and Dutch (browser default, switchable)
- [ ] Game-night filters: player count, play time, storage location, search by name (non-matching games dim on the shelf)
- [ ] Before the site goes public: revoke or downgrade the development admin account (passwordless sudo) on the server, and drop the LAN/VPN allow-list from the route
- [ ] Site is public and read-only with no accounts, reachable from the internet through the existing Traefik reverse proxy. Owner tools are reachable only from the home network or VPN

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
| Box image: owned version's image if flat, else base image; owner override per game | Dutch version images are often 3D product shots | — Pending |
| No database: JSON snapshot, image cache and a backed-up owner-data file | Data is small and mostly rebuildable from BGG; lighter than a shared SQL Server | — Pending |
| English and Dutch site labels | Friends are local; BGG titles stay as-is | — Pending |
| Game-night and sharing extras deferred to v2 | Keep v1 focused on the cabinet itself | — Pending |
| Public, read-only site with no accounts | Anyone with the link can view; nothing editable to protect | — Pending |
| Sync about hourly, plus a manual sync button with a global cooldown | Fresh enough after adding a game; can't be abused to hit BGG rate limits | — Pending |
| Mix of face-out boxes and generated spines, packed to look good | Mirrors the inspiration photo; looks like a real cabinet | ✓ Good — approved after two screenshot review rounds and on the deployed v0.2.0 (Phase 2); flat piles and big boxes lying flat added at the owner's request |
| Expansions as sideways spines beside their base game | Owner's idea; keeps families together visually | ✓ Revised — thick big-box expansions stand upright beside the base, thin ones stack thickest at the bottom (Phase 2, owner request after seeing the first families) |
| Toggle between one cabinet and one cabinet per location | Supports both browsing and "where is it?" | — Pending |
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
*Last updated: 2026-10-07 after Phase 3 (BGG access check, real sync and snapshot; released as v0.3.0)*

# Phase 8: Public Hardening & Go-Public - Discussion Log

> **Audit trail only.** Do not use as input to planning, research, or execution agents.
> Decisions are captured in CONTEXT.md — this log preserves the alternatives considered.

**Date:** 2026-10-19 to 2026-10-20
**Phase:** 08-public-hardening-go-public
**Areas discussed:** Limits for the open web, Resource caps & load test, Go-public day, Access cleanup & egress

---

## Todo cross-reference

| Option | Description | Selected |
|--------|-------------|----------|
| Live and sync limits | Per-client limits on sync now and live connect, origin allow-list, caps from real load (parked for this phase by the sync phase's audit) | ✓ |
| Flaky browser tests | Harden the browser tests that flake on the CI runner | ✓ |
| Screen-reader check | Deferred by the owner; no screen reader available | |

**User's choice:** Live and sync limits, Flaky browser tests.
**Notes:** The other seven matches were keyword-only and not offered.

---

## Limits for the open web

### Where should the rate limits live?

| Option | Description | Selected |
|--------|-------------|----------|
| Both layers | Traefik per-address limit and in-flight cap; app per-client limits on sync now and live connect, tested in CI | ✓ |
| Traefik only | One place, no app code, not testable in CI | |
| App only | Everything tested, but a flood still reaches Kestrel | |

### How generous should the per-address limit be?

| Option | Description | Selected |
|--------|-------------|----------|
| Room for a group | Measure one first visit, allow about 5 to 10 at once from one address | ✓ |
| One visitor's worth | Just above one first visit | |
| Very loose | Only catches extreme floods | |

**Notes:** On the first ask the owner said they were confused about where the limit is active and what it is for. Claude explained the two layers, why a limit is needed on the open internet, and why shared addresses (one Wi-Fi, carrier NAT) make generosity matter. The question was then re-asked and answered.

### When a limit refuses someone, what should they see?

| Option | Description | Selected |
|--------|-------------|----------|
| Quiet handling | Traefik's plain 429; sync press shows the existing window message; refused live connection just means no auto-refresh | ✓ |
| Quiet, plus a live note | Status line notes that live updates are paused | |
| Styled refusal page | Traefik errors middleware serves a site-styled page | |

### Per-address cap on live connections?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, game-night sized | About 20 per address, plus the origin allow-list | ✓ |
| Yes, tight | About 5 per address | |
| Site-wide cap only | Total cap and origin allow-list only | |

### Traefik rate limit on the owner router?

| Option | Description | Selected |
|--------|-------------|----------|
| No limit there | Home and VPN only; app limits still apply on both hostnames | ✓ |
| Same limit as public | One rule everywhere | |

### Where should the security headers live?

| Option | Description | Selected |
|--------|-------------|----------|
| App sends the full set | nosniff, referrer policy, Permissions-Policy, cross-origin headers in the app and tested in CI; Traefik keeps only HSTS | ✓ |
| Keep the split, extend Traefik | New headers in Traefik's middleware; only the probe checks them | |

---

## Resource caps & load test

### Where should the CPU and memory caps be set?

| Option | Description | Selected |
|--------|-------------|----------|
| Both | Proxmox container limits set by the owner; systemd unit limits shipped in the repo | ✓ |
| Proxmox only | One place; the app can still use the whole container | |
| systemd unit only | Container stays uncapped; requirement not met | |

**Notes:** The first ask was interrupted when the session ended; it was asked again on the owner's request and answered.

### How should real load be measured?

| Option | Description | Selected |
|--------|-------------|----------|
| On the container | Picture rebuild and sync peaks; flood from the Traefik container through the router and straight at the app port, never through the home gateway's IPS | ✓ |
| Flood from my desktop | Over the LAN; the IPS has blocked desktop bursts before | |
| No flood, measure sync only | Quicker, but criterion 4 is not shown | |

### Is one forced full picture rebuild fine?

| Option | Description | Selected |
|--------|-------------|----------|
| Yes, once | About 65 image downloads at the normal polite pace | ✓ |
| No, use synthetic pictures | No downloads; peak is an estimate | |

### Pass bar for "slows without starving"

| Option | Description | Selected |
|--------|-------------|----------|
| Concrete checks | Neighbour site keeps response time, SSH responsive, host CPU not saturated, recovery without restart or OOM kill, numbers recorded | ✓ |
| Cabinet survives | Only that the cabinet stays up and recovers | |

---

## Go-public day

### External probe

| Option | Description | Selected |
|--------|-------------|----------|
| Phone + scanners | Owner on mobile data checks site, health, ops and owner host; Claude runs SSL Labs, securityheaders.com, Mozilla Observatory after asking | ✓ |
| Phone only | No third-party scanners | |
| Scanners only | No real outside-network check of closed paths | |

### Order

| Option | Description | Selected |
|--------|-------------|----------|
| v1.0.0 first, then open | Hardening ships as v1.0.0, checked on the LAN-only route, then the allow-list comes off | ✓ |
| Open on 0.x, v1.0.0 later | Open on the last 0.x, cut v1.0.0 after a few days | |

### Search engines

| Option | Description | Selected |
|--------|-------------|----------|
| Keep noindex | Link-only site; easy to lift later | ✓ |
| Allow indexing | Drop noindex at go-public | |
| noindex + robots.txt | Also ask crawlers to stay away | |

### Checklist location and who flips the switch

| Option | Description | Selected |
|--------|-------------|----------|
| Repo doc + Claude flips | Generic checklist in docs/, results in the phase record; Claude re-reads the BGG terms, owner signs off, Claude removes the allow-list over SSH and runs the probe with the owner | ✓ |
| Repo doc + owner flips | Owner removes the allow-list following the doc | |
| Planning record only | Checklist only under .planning/ | |

---

## Access cleanup & egress

### Claude's sudo SSH keys at go-public

| Option | Description | Selected |
|--------|-------------|----------|
| Revoke both, re-grant per job | Remove keys and accounts; documented temporary re-grant | |
| Keep, add a passphrase | Owner unlocks the keys into the SSH agent only when Claude should work on the servers | ✓ |
| Downgrade cabinet, revoke Traefik | Read-only journal account on the cabinet; proxy key removed | |
| Keep both as they are | Extend the accepted risks unchanged | |

**Notes:** The owner first asked to confirm that the keys only work from their desktop. Claude checked both containers without printing addresses. Every authorized key is pinned with `from=` to the desktop's address, and both accounts have passwordless sudo. The cabinet container's firewall admits SSH only from the admin ranges; the proxy container has no SSH firewall rule, so its SSH port answers on the whole LAN. The remaining risk Claude named was software running on the desktop using the passphrase-less keys. The question was then re-asked with the passphrase option added.

### Outbound firewall on the cabinet container

| Option | Description | Selected |
|--------|-------------|----------|
| Block the home network | Internet on needed ports stays open; no new connections into LAN or VPN | ✓ |
| Full hostname allow-list | Only GitHub, Sigstore, BGG, image host, mirrors; brittle with CDN addresses | |
| Later | Keep inbound-only filtering | |

### Proxy container SSH open to the LAN

| Option | Description | Selected |
|--------|-------------|----------|
| Add as your step | Owner restricts SSH on the proxy container to admin addresses; Claude supplies the rule | ✓ |
| Leave it | Out of scope; key pinning stays the only guard | |

---

## Claude's Discretion

- Exact limit numbers: Traefik average, burst, period and in-flight cap; app limiter windows; live caps within the agreed sizes; cap values plus headroom.
- Kestrel request limits and host filtering.
- Exact header values, as long as nothing the cabinet uses breaks and every value is tested.
- How to force the picture rebuild, the flood tool, and how to measure live memory.
- Checklist doc name and layout; whether parts of the probe get a script in `build/`.

## Deferred Ideas

- Full hostname-based egress allow-list.
- Styled refusal page.
- Search indexing (owner decision for later).
- Screen-reader check stays deferred (owner has no screen reader).

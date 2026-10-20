# Phase 8: Public Hardening & Go-Public - Research

**Researched:** 2026-10-20
**Domain:** Public-internet hardening of an ASP.NET Core 10 site behind Traefik v3 on a shared, low-power Proxmox host (rate limiting in two layers, security headers, Kestrel/host limits, cgroup caps, outbound firewall, go-public checklist and probe)
**Confidence:** HIGH for the app-side mechanisms, Traefik semantics and the container facts (all probed or read from source this session); MEDIUM for every concrete number (they are starting hypotheses that the phase's own measurements must replace)

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

#### Rate limits and the live channel
- **D-01: Two layers of rate limiting.**
  - **Traefik** (outer wall): a per-client-address `rateLimit` plus an `inFlightReq` cap on the public router, so a flood is refused before it reaches the container. Lives in `deploy/traefik/cabinet.yml.example` with placeholders; the real file is on the Traefik container.
  - **The app** (inner wall): ASP.NET Core rate limiting partitioned by the client address from the existing forwarded-headers setup (trusted from the known proxy only), on `POST /cabinet/sync` and on the live channel's negotiate and connect (`/cabinet/live`). Covered by integration tests.
  - Same idea as the owner tools' walls: one misconfigured layer is still covered by the other.
  - **Reversibility:** reversible.
- **D-02: Traefik's per-address limit leaves room for a group.**
  - Measure one real first visit (requests and burst shape: page, scripts, styles, box pictures, layout and card data, live negotiate). Use the largest realistic collection; locally the 400-game sample shows how it scales.
  - Set the average and burst so about 5 to 10 simultaneous first visits from one address pass. Only a sustained flood is refused.
  - **Why:** friends on one Wi-Fi share their house's public address, and phones on mobile data often share one through carrier NAT. Visitors on the owner's own LAN reach Traefik through local DNS with their own addresses, so they are not affected.
- **D-03: Refusals are quiet.**
  - Traefik answers a flood with its plain 429. No errors middleware and no styled page.
  - A sync press over the per-client limit gets the same response shape as the shared 10-minute window (429 with `Retry-After`), so the page shows the existing status-line message and countdown. No new copy.
  - A refused live connection means that page doesn't update by itself. It still shows the cabinet, and a reload catches up (the page already fetches status on connect).
- **D-04: Live-channel caps.**
  - About 20 concurrent live connections per client address: enough for a game night's phones and a few spare tabs on one Wi-Fi.
  - The site-wide cap (`Live:MaxConnections`, 100 today) is re-sized from measured memory per connection.
  - An origin allow-list on `/cabinet/live` admits only pages from the site's own hostnames. The public and owner hostnames come from the server env file, never from the repo.
  - Both transports are counted: Server-Sent Events as well as WebSockets. The folded todo notes that Kestrel's `MaxConcurrentUpgradedConnections` covers WebSockets only, and SSE is counted only at the hub.
  - Every limit gets a test. Accepted risks AR-03-02 and AR-03-03 are then revisited and closed.
- **D-05: No Traefik rate limit on the owner router.** Only home and VPN ranges reach it, and the pictures page loads many images at once. The app's sync and live limits still apply on both ports and hostnames.
- **D-06: The app sends the full security-header set; Traefik keeps only HSTS.**
  - The app adds `X-Content-Type-Options: nosniff`, a `Referrer-Policy`, a `Permissions-Policy` switching off unused browser features, and cross-origin isolation headers (`Cross-Origin-Opener-Policy`, `Cross-Origin-Resource-Policy`). These sit beside the existing CSP and are tested on every response type on both ports: pages, static assets, box art, JSON, live negotiate, 404s.
  - Traefik keeps HSTS because it terminates TLS. The external probe checks it.
  - The headers Traefik sets today (frame denial, nosniff, referrer) move into the app, so nothing is sent twice. The CSP's `frame-ancestors 'none'` already covers framing.
  - Same reasoning as the CSP decision: headers that live with the app are tested with the page they protect.
  - The cabinet must keep working under the policy with no violations. The existing browser tests that listen for CSP violations stay green.

#### Resource caps and load measurement
- **D-07: Caps at two levels.**
  - **Proxmox container limits:** cores, memory, swap, and a CPU weight lower than the reverse proxy's. The owner sets them with a documented command (placeholders in `docs/lxc-setup.md`), because Claude has no Proxmox host access. Claude then checks the effective limits from inside the container (cgroup files) and reports them.
  - **systemd limits on `cabinet.service`**, shipped in the repo (memory and CPU limits, e.g. `MemoryHigh`/`MemoryMax`/`CPUQuota`). Sized below the container caps, so SSH, nftables and the deploy poller keep headroom, and a rollback still works while the app is flooded.
  - **Reversibility:** reversible.
- **D-08: Real load is measured on the real container.**
  - Record CPU and memory peaks during one forced full picture rebuild and one normal sync.
    - The rebuild downloads every box picture again from the image host once, at the normal polite pace (about 65 pictures). The owner approved this once.
    - It must not touch the owner-data file or the last good snapshot.
  - Then run a controlled flood from the reverse-proxy container, twice:
    - through the public router, TLS included;
    - straight at the app port, bypassing Traefik's limits to test the app's own caps.
  - The flood traffic stays on the host bridge and never passes the home gateway's IPS, which has blocked bursts from the desktop before.
  - Caps = measured peak plus headroom.
  - At execution time, each of these needs the owner's go: the rebuild, the flood, and any tool placed on the shared proxy container (remove it afterwards).
- **D-09: Pass bar for "slows without starving".**
  - While the flood runs:
    - another site behind the same Traefik keeps its normal response time (measured before and during);
    - SSH into the cabinet container stays responsive;
    - the host CPU does not saturate. The owner reads the Proxmox host graphs, which the container cannot see.
  - Afterwards the cabinet recovers by itself: no restart and no out-of-memory kill (check the journal and the unit's restart count).
  - The before-and-after numbers go into the phase record under `.planning/`, without hostnames or addresses.

#### Go-public day
- **D-10: Order: v1.0.0 first, then open.**
  1. All hardening ships as `v1.0.0`, as planned in the repo-setup phase (0.x until go-public).
  2. Everything is checked on the still LAN-only route: headers, limits with a burst test, caps, the flood test, and the full browser suite under the CSP.
  3. After the owner's checklist sign-off, the allow-list comes off and the external probe runs.

  The version that goes public is the version that was checked.
  - **Reversibility:** one-way. Releases are immutable, so `v1.0.0` cannot be re-cut. Once the route is open, the site can be fetched and cached by anyone. Re-adding the allow-list closes it again but does not undo what was fetched.
- **D-11: The external probe is the owner's phone plus online scanners.**
  - On mobile data (Wi-Fi off), the owner opens the site and tries `/health` on the public hostname, the ops port and the owner hostname. All must fail: 404, refused or blocked.
  - Claude runs SSL Labs, securityheaders.com and Mozilla Observatory against the public hostname. It asks the owner before sending the hostname to them; the hostname is already public through certificate-transparency logs.
  - Scanner screenshots and the hostname never go into the repository. The phase record holds the grades and findings only.
- **D-12: Keep `noindex` at go-public.**
  - The site stays "for anyone with the link". No `robots.txt` disallow, because crawlers must be able to read the tag.
  - The completed noindex todo is reopened if the owner later decides to allow indexing.
- **D-13: A generic go-public checklist lives in `docs/`, the filled-in results in the phase record, and Claude flips the switch on the owner's go.**
  - **The checklist** uses placeholders only, carries no planning references, and stays reusable for later re-checks. It covers:
    - security headers;
    - rate limits (burst test);
    - health, ops and owner tools not routed publicly;
    - the BGG credit on every page, including empty, error and phone views;
    - BGG terms re-read;
    - no personal data in the repository (full-history scan and lint);
    - resource caps applied and verified;
    - the outbound firewall active;
    - `noindex` present;
    - maintenance keys passphrase-protected (D-14);
    - the owner step on the proxy's SSH (D-16).
  - **BGG terms:** Claude re-reads them (the official page, or an archive capture if it is Cloudflare-gated) and writes the owner a short summary of anything that changed since the version recorded in the stack research. The owner confirms.
  - **The switch:** the owner signs off and says "go". Then, over SSH on the Traefik container, Claude:
    1. backs up the dynamic file outside the watched folder;
    2. removes `cabinet-lan-only` from the public router only (the owner router keeps it permanently);
    3. runs the external probe with the owner.
  - Re-adding the middleware is the documented way to close the site again.

#### Access and egress
- **D-14: Claude's maintenance keys stay, with a passphrase.**
  - **Context:** the development admin account on the cabinet container (AR-08) and the maintenance key on the reverse-proxy container (AR-06-05) were due to be revoked or downgraded at go-public. The owner chose to keep both and add a mitigation instead.
  - **The mitigation:** the owner sets a passphrase on both private keys and loads them into the SSH agent only when Claude should work on the servers. Claude never sees or types the passphrase.
  - The `from=` pinning to the desktop's address stays. Verified on 2026-10-20: every authorized key on both containers is pinned.
  - **Consequences:**
    - This replaces "Claude revokes its own access as the very last step" in D-13. The last step becomes the owner setting the passphrases and locking the agent.
    - The accepted-risk register entries AR-08 and AR-06-05 are re-accepted with the new mitigation and a new expiry or review point, chosen at planning time with the owner.
    - The PROJECT.md active items that say "revoke or downgrade" are updated at the phase transition.
  - **Residual risk, accepted:** while the agent is unlocked, anything running on the desktop can use the keys.
- **D-15: Outbound firewall: block the home network.**
  - The cabinet container's nftables output chain refuses new connections to private, LAN and VPN ranges, except what the host needs: its DNS resolver, local time servers if any, and the loopback.
  - Replies to Traefik are established traffic and keep working.
  - The internet stays open on the ports the host needs (HTTPS, HTTP for package mirrors, DNS, NTP).
  - The ranges and resolver come from the server-side provision config. The template carries placeholders, and the provision logic tests cover the new rules.
  - **Verify on the container:** no connection to a LAN host succeeds, while sync, picture downloads, the deploy poll, Sigstore verification and package updates still work.
  - A full hostname allow-list was considered and rejected for now: CDN addresses change, and a missed one silently breaks sync or deploys.
  - **Reversibility:** reversible.
- **D-16: An owner step on the checklist: restrict SSH on the reverse-proxy container.**
  - It currently answers SSH from the whole LAN, guarded only by key pinning.
  - The step restricts it to admin addresses, like the cabinet container already does.
  - Claude supplies the rule at execution time. The repository carries only a generic checklist line, nothing about that container.

### Claude's Discretion
- Exact numbers, sized from the measurements:
  - Traefik `average`/`burst`/`period` and the `inFlightReq` amount;
  - the app limiter windows for sync press and live negotiate;
  - per-address and site-wide live caps within D-04;
  - container and unit cap values plus headroom.
- Kestrel request limits for a public host: `MaxConcurrentConnections`, a small `MaxRequestBodySize`, header and keep-alive timeouts, minimum data rates.
- Host filtering (`AllowedHosts` set to the public and owner hostnames from the env file; `*` today).
- Exact header values (Permissions-Policy feature list, COOP/CORP values, whether to also send `X-Frame-Options`), as long as nothing the cabinet uses breaks and every value is tested.
- How to force the one-time picture rebuild, which flood tool to use, and how to measure per-connection live memory.
- The checklist doc's name and layout, and whether the probe gets a scripted helper in `build/` (like `check-owner-routes.sh`) for the parts that can be scripted.
- Plan split for the folded flaky-browser-tests todo.

### Deferred Ideas (OUT OF SCOPE)
- **Full hostname-based egress allow-list** (GitHub, Sigstore, BGG API, image host, package mirrors only). Rejected for now in favour of blocking the home network (D-15). Revisit if a resolver-fed set or an egress proxy becomes worth its upkeep.
- **Styled "slow down" page for refused visitors.** Rejected for quiet handling (D-03).
- **Search indexing.** Kept off at go-public (D-12); an owner decision for later.
- Folded todos (in scope, see CONTEXT.md): the live/sync limits todo and the flaky browser tests todo. Not folded: the screen-reader check of the skip link and games list stays deferred.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| SEC-01 | The site is reachable from the public internet over HTTPS through the existing Traefik reverse proxy. | Traefik 3.5.4 on the proxy; entry point already redirects 80 to 443 and a resolver issues the certificate; the real client address is preserved through the port forward (855 distinct public sources in the proxy's own access log); go-public is removing the allow-list middleware from the public router only; probe script and scanners in "Go-public checklist and probe". |
| SEC-02 | Public traffic is rate-limited, with a much stricter limit on "sync now". | Traefik `rateLimit` + `inFlightReq` semantics and the three traps (inFlightReq default criterion is the host, in-flight counts SSE/WebSocket for their lifetime, no `depth` strategy on an edge Traefik); ASP.NET Core partitioned limiters on `/cabinet/sync` and the hub (conventions reach negotiate and connect); per-address live cap via a concurrency limiter plus the existing site-wide limiter; origin guard; response shape that the page already understands. |
| SEC-03 | Every response carries security headers, including a strict Content-Security-Policy. | One middleware that sets the whole set at the start of the pipeline (exact values recommended below), test matrix over response types and both ports, console-clean browser test, Traefik keeps HSTS only. |
| SEC-04 | Health and ops endpoints are not reachable from the internet, and neither are any owner tools (OWN-01). | Loopback ops port and owner router allow-list unchanged; `AllowedHosts` must list loopback or the health check, selfcheck and deploy poller break; external probe helper and phone probe; render tests that pin the owner router. |
| SEC-06 | The app's container has CPU and memory caps, so a traffic flood cannot starve the other guests on the host. | Verified on the real container: every systemd resource property works inside the unprivileged LXC; container limits are partly invisible from inside; .NET GC reads only `memory.max` and walks to the namespace root; measurement method, rebuild mechanism, flood tooling, pass bar instrumentation; outbound firewall rules verified in a throwaway namespace. |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Treated with the authority of locked decisions. Source: `.claude/CLAUDE.md`.

- **No planning references outside `.planning/`.** No requirement keys, decision IDs, phase/plan/wave/milestone numbers, planning document names in README, `docs/`, code, comments, string literals, log or exception messages, test names, scripts, config or workflow files. `build/lint/checks/10-repo-rules.sh` enforces this with these patterns (read this session): `\b(A11Y|CABX?|DET|EXP|FILT|I18N|IMG|LOC|NIGHT|OPSX?|OWN|SEC|SHARE|SYNC)-[0-9]{2}\b`, `\bD-[0-9]{2}\b`, `\bphase[-_ ]?([0-9]+|one|...)\b`, `\b(plan|wave|milestone)[-_ ]?[0-9]+\b`, a bare `NN-NN` plan-number pattern, and the planning file names. The new go-public checklist and every test name must pass these. Git commit messages are the only exception.
- **Comments: `///` only in C#; `/** */` only in JavaScript.** No `//`. Lint enforces both. Shell `#` comments are fine.
- **Public repository, no personal data.** No names, e-mail, BGG username, real domains or hostnames, homelab addresses or ranges, real location names. RFC 5737 addresses and `example.com`/`example.org` only. `.gitleaks.toml` (read this session) flags RFC 1918 host addresses and RFC 1918 networks in CIDR form, with an allow-list for exactly the three whole blocks `10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`, the whole `100.64.0.0/10`, and the bare IPv6 block starts `fc00::`, `fd00::`, `fe80::`. Writing those blocks out in the nftables template and the docs is therefore allowed; any narrower real range is not.
- **Branching:** never commit to `main`; work on the milestone branch and land by pull request.
- **BGG API etiquette:** only the server's background sync talks to BGG; the picture rebuild must keep the polite pace; the token goes only to the BGG host.
- **Settings pattern (CONTEXT.md, code context):** every new limit is read and validated at startup with a message naming the key, so a typo stops the app instead of silently weakening a limit.
- **Test pattern:** integration tests boot the real pipeline in-process; browser tests run in their own CI jobs; CI draws `system-ui` wider than a desktop (DejaVu) so run UI geometry checks with a DejaVu fontconfig before pushing.

## Summary

The app already has the right skeleton: a first-in-pipeline header middleware, a trusted-proxy forwarded-headers setup, a loopback-only ops port, a three-wall owner design, a site-wide live cap and a shared persisted sync window. The phase adds (1) per-address limits in Traefik and in the app, (2) the rest of the header set, (3) host filtering and Kestrel limits, (4) unit caps and an egress block shipped as repo templates plus owner-set container caps, and (5) the checklist, probe and release choreography. No new package is needed: rate limiting, host filtering and Kestrel limits are in the ASP.NET Core 10 shared framework (server runtime 10.0.13). The only tool installed anywhere is a temporary flood generator on the proxy container.

Eight findings change the plan from what the context assumed and the planner must carry them into tasks. (a) Traefik's `inFlightReq` defaults its criterion to the request **host**, so unless `sourceCriterion.ipStrategy` is set explicitly all clients share one bucket; and it counts a request until it finishes, so every open SSE or WebSocket connection counts for its lifetime, which couples the per-address `amount` to the live cap. (b) On an edge Traefik the right strategy is the default remote address; `ipStrategy.depth` would read an empty `X-Forwarded-For` at rate-limit time and put everyone in one bucket. (c) `AllowedHosts` host filtering also guards the loopback ops port and the selfcheck's `127.0.0.1:5080` probe, so loopback hosts must be listed or the deploy health check fails and the release rolls back. (d) Kestrel `KeepAliveTimeout` must stay above Traefik's 90 s backend idle timeout. (e) From inside the unprivileged container the Proxmox limits for memory, swap and cores are readable only through lxcfs views (`/proc/meminfo`, `/proc/swaps`, `nproc`); the cgroup files at the namespace root show `max`, and `cpuunits`/`cpulimit` are not readable at all, so "Claude verifies from the cgroup files" holds only for the unit's own limits and for what lxcfs shows. (f) A full picture rebuild is about 99 downloads, above the per-run cap of 80, so it needs two sync runs; the mechanism is moving the `art` directory aside, never touching the snapshot or owner data. (g) Releases are immutable and the unit limits ship in the repo, so sizing measurements must run on a `0.x` pre-release plus runtime overrides, and `v1.0.0` is the verification build, not the measurement build. (h) The AllowedHosts/env and provisioning changes must be applied before or with the release so the new version's health check does not fail.

**Primary recommendation:** Build a tracer slice first (the per-address sync-press limiter through settings, policy, response shape, integration test, the Traefik example middlewares and their render test, in one pass), then widen sideways in parallel tracks (headers + host filtering + Kestrel limits; live caps + origin guard; deploy templates for caps and egress; docs, probe and checklist; flaky-test hardening), then run the owner-approved measurements on a `0.x` pre-release with runtime overrides, put the final numbers into the repo, and cut `v1.0.0` as the version that is verified and then opened.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| TLS termination, HSTS, HTTP to HTTPS redirect | Reverse proxy (Traefik) | — | Only Traefik terminates TLS (D-06); the app speaks plain HTTP from the known proxy only. |
| Coarse per-address request flood limit and in-flight cap | Reverse proxy (Traefik public router) | App (inner wall on sync and live only) | Cheapest place to refuse; the proxy sees the real client address because there is no CDN. |
| Strict limit on "sync now", live connect rate and per-address live cap | App (ASP.NET Core rate limiter) | Reverse proxy (general limit) | Needs endpoint knowledge and the forwarded client address; tested in-process. |
| Site-wide live cap, hub abort without reconnect permission | App (`LiveConnectionLimiter`, Kestrel upgraded cap) | — | Already exists; remains the backstop. |
| Origin check for the live channel | App (middleware before the limiter) | — | Needs the Host header and the endpoint; browsers send `Origin` on WebSocket and cross-origin requests. |
| Security headers other than HSTS | App (first middleware) | — | Tested with the page they protect; covers every response including static, 404, 429 and negotiate. |
| Host header validation | App (HostFiltering via `AllowedHosts`) | Reverse proxy (router `Host` rules) | Defence in depth; the proxy only routes configured hosts. |
| Request size, timeouts, connection ceiling | App host (Kestrel limits) | Reverse proxy | Kestrel is the process that holds the memory. |
| CPU, memory, task caps | Container (Proxmox) + service unit (systemd) | — | Two levels: container protects neighbours, unit protects SSH/deploy inside the container. |
| Outbound LAN block | Container firewall (nftables output chain) | — | Kernel-level, independent of the app, also closes SSRF to the LAN. |
| Health and ops reachability | App host (loopback listener) | Firewall (no rule) | Unchanged; probed from outside. |
| Owner tools unreachable from the internet | Reverse proxy (owner router allow-list) | Firewall, app wall | Three walls; the owner router keeps its allow-list permanently. |
| Go-public switch | Reverse proxy dynamic file (outside the repo) | Owner sign-off | A one-line router edit, with backup and probe. |

## Standard Stack

### Core

No new NuGet or npm package. Everything is in the shared framework or already in the repo.

| Library / component | Version | Purpose | Why Standard |
|---------------------|---------|---------|--------------|
| `Microsoft.AspNetCore.RateLimiting` and `System.Threading.RateLimiting` | shared framework; server has Microsoft.AspNetCore.App 10.0.13 | Partitioned fixed-window and concurrency limiters, named policies, `OnRejected` | First-party, documented for .NET 10. `[VERIFIED: ssh on the cabinet host, dotnet --list-runtimes → "Microsoft.AspNetCore.App 10.0.13"]` |
| HostFiltering middleware | shared framework | `AllowedHosts` Host-header allow-list | `CreateDefaultBuilder` already registers it; disabled while `AllowedHosts` is `*`. `[CITED: learn.microsoft.com/aspnet/core/fundamentals/servers/kestrel/host-filtering]` |
| Kestrel limits | shared framework | `MaxRequestBodySize`, `RequestHeadersTimeout`, `KeepAliveTimeout`, `MaxConcurrentConnections` | Existing code already configures `KestrelServerOptions` through DI. |
| Traefik | 3.5.4 on the proxy `[VERIFIED: ssh traefik version → "Version: 3.5.4", Go 1.24.9]` | `rateLimit`, `inFlightReq`, `headers` middlewares, file provider | The existing proxy; example file says "Traefik v3". |
| systemd | 255.4-1ubuntu8.18 on the cabinet host `[VERIFIED: systemctl --version]` | `MemoryHigh`, `MemoryMax`, `MemorySwapMax`, `CPUQuota`, `CPUWeight`, `TasksMax` | Already the unit manager. |
| nftables | 1.0.9-1ubuntu0.1 `[VERIFIED: dpkg-query on the host]` | Output-chain egress rules | Already renders the input chain; throwaway test used nft 1.0.9, the same version. |
| xunit.v3 4.0.2, FluentAssertions 8.11.0, Playwright .NET 1.63.0 | per csproj `[VERIFIED: Cabinet.IntegrationTests.csproj:14, Cabinet.BrowserTests.csproj:10-14]` | Tests | Existing. |

### Supporting

| Tool | Version | Purpose | When to use |
|------|---------|---------|-------------|
| `hey` (Ubuntu archive package) | 0.1.4-2ubuntu0.24.04.3, 6.9 MB installed, depends only on libc6 `[VERIFIED: apt-cache show hey on the cabinet host]` | Flood generator on the proxy container | Only at execution time, with the owner's go; remove with `apt-get remove --purge hey` afterwards. The proxy container has no `curl`, `wget`, `hey`, `wrk`, `oha` or `ab`; it has `python3`, `ss`, `awk` and `systemd-cgtop` `[VERIFIED: ssh command -v probe]`. |
| `wrk` | 4.1.0-4build2, 123 KB plus libluajit/libssl | Alternative generator | If `hey` cannot hold the needed rate. |
| `curl` | 8.5.0 on the cabinet host, 8.18.0 on the desktop | Probe script, neighbour-site timing, `--parallel` flooding from the desktop-side scripts | Already present where needed. |
| `systemd-cgtop`, `systemctl show`, `memory.events` | built in | Peak and throttle measurement | Measurement phase. |

### Alternatives Considered

| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| Concurrency limiter for the per-address live cap | Extend `LiveConnectionLimiter` with a per-address map, counted in `OnConnectedAsync` | The limiter approach refuses with a 429 before any upgrade or handshake and counts SSE and WebSocket by request lifetime with no extra code; the hub approach reuses the "abort without reconnect permission" behaviour and the existing tests but only counts after the handshake. Recommended: limiter for the per-address cap, keep the hub for the site-wide cap. |
| Separate `Live:AllowedOrigins` list | Reuse `AllowedHosts` as the origin allow-list | One env value serves both, so the two cannot drift; the guard also compares the origin host with the request `Host`, so tests and development work without any list. Recommended (see Open Question 1). |
| `hey` on the proxy | `wrk`, `oha` single static binary in `/tmp` | `hey` is archive-signed with no extra dependencies; `oha` has `--connect-to` but its release assets and checksums were not confirmed this session. |
| `ServerGarbageCollector` as is | Workstation GC, non-concurrent | See "GC configuration". |

**Installation (execution time, proxy container, owner's go required):**
```bash
apt-get update && apt-get install -y hey      # ~6.9 MB, libc6 only
hey -n 1 -c 1 https://"$CABINET_PUBLIC_HOST"/ # smoke
apt-get remove --purge -y hey && apt-get autoremove -y
```

**Version verification:** versions above were read from the hosts and csproj files this session, not from training data. `[VERIFIED]` entries cite the probe.

## Package Legitimacy Audit

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| (none) | NuGet / npm / PyPI / crates | — | — | — | N/A | The phase adds no language-package dependency. Rate limiting, host filtering and Kestrel are in the ASP.NET Core shared framework. |
| `hey` | Ubuntu 24.04 archive (noble-updates/universe) | package 0.1.4, updated for noble | — | github.com/rakyll/hey (apt Homepage field) | OK (distribution-signed, not run through the npm/pypi/crates seam, which does not cover apt) | Approved for a temporary install on the proxy container with the owner's go; removed afterwards. |

**Packages removed due to [SLOP] verdict:** none
**Packages flagged as suspicious [SUS]:** none

*The `gsd-tools package-legitimacy check` seam supports npm, PyPI and crates only; no package of those ecosystems is installed in this phase. Any new tool the planner adds beyond the above must be tagged `[ASSUMED]` and gated behind a `checkpoint:human-verify`.*

## Architecture Patterns

### System Architecture Diagram

```
 Internet client ──TLS 443──▶ home gateway (port forward, source address preserved)
                               │
                               ▼
              ┌──────────────────────────────────────────────┐
              │ Traefik 3.5.4 (1 core, 512 MB, six sites)    │
              │ entry point websecure: TLS, HTTP/2           │
              │ public router (Host rule):                   │
              │   [cabinet-lan-only  ← removed at go-public] │
              │   rateLimit (per remote address)             │
              │   inFlightReq (per remote address)           │
              │   headers: HSTS only                         │
              │ owner router: allow-list ALWAYS, no rateLimit│
              └───────────────┬──────────────────────────────┘
        append client to X-Forwarded-For; 429 "plain" on flood
                              │ HTTP/1.1, backend idle timeout 90 s
                              ▼
   cabinet LXC  nftables: input drop-by-default (Traefik IP → 5080/5082)
   ┌──────────────────────────────────────────────────────────────┐
   │ cabinet.service  (MemoryHigh/Max, CPUQuota, CPUWeight,       │
   │                   TasksMax, MemorySwapMax)                   │
   │  Kestrel: AllowedHosts filter → limits                       │
   │   :5080 public   :5082 owner   :5081 ops (loopback only)     │
   │  pipeline: SecurityHeaders → ConnectionPeer → ForwardedHdrs  │
   │   → OwnerClientRange → health(ops port) → OwnerPathGuard     │
   │   → CabinetArt → UseRouting → OwnerOnlyRoutes                │
   │   → [OriginGuard] → [RateLimiter] → endpoints                │
   │        POST /cabinet/sync      (policy: per-address window)  │
   │        /cabinet/live (+/negotiate) (policy: concurrency 20   │
   │            + connect rate; hub: site-wide cap, abort)        │
   │  outbound: nftables output chain                             │
   │   established ✔  DNS resolver ✔  loopback ✔                 │
   │   private / LAN / VPN ranges ✘ (reject, instant failure)     │
   │   internet ✔ (BGG, image host, GitHub, Sigstore, apt)        │
   └──────────────────────────────────────────────────────────────┘
 Outside the repository: Traefik dynamic file, Proxmox container limits,
 /etc/cabinet/provision.conf, /etc/cabinet/cabinet.env, SSH keys.
```

### Recommended Project Structure (additions)

```
Cabinet.Service/
├── Hosting/
│   ├── SecurityHeaders.cs        # replaces ContentSecurityPolicy.cs middleware; keeps the Policy constant
│   ├── ClientAddress.cs          # partition key (IPv4-mapped normalised, IPv6 to /64)
│   ├── HostLimitSettings.cs      # Kestrel limits read and validated at startup (pattern: LiveSettings)
│   └── OriginGuard.cs            # same-origin / allowed-host check for the live route
├── Limits/
│   ├── RateLimitSettings.cs      # sync press and live connect numbers, validated at startup
│   ├── SyncPressPolicy.cs        # IRateLimiterPolicy<string>, OnRejected = existing 429 shape
│   ├── LiveConnectPolicy.cs      # chained: concurrency then fixed window
│   └── RateLimitSetup.cs         # AddCabinetRateLimits / UseCabinetRateLimits, shared with the untrusted-peer test host
deploy/
├── nftables/cabinet.nft.in       # gains the output rules (verified snippet below)
├── systemd/cabinet.service       # gains resource directives
├── provision.conf.example        # gains egress keys
├── provision.d/50-firewall.sh    # renders the new tokens
├── bin/cabinet-selfcheck         # gains check_limits, check_egress
└── tests/                        # render-templates-test.sh, provision-logic-test.sh, sandboxing-test.sh extended
build/
├── check-public-routes.sh        # external probe (modelled on check-owner-routes.sh)
└── tests/check-public-routes-test.sh   # stubbed-curl test, picked up by build/lint.sh script-tests
docs/
└── go-public.md                  # generic checklist (name is the planner's choice)
```

### Pattern 1: One security-header middleware at the start of the pipeline

**What:** Replace `UseContentSecurityPolicy` with a `UseSecurityHeaders` that sets every header before `next`, so static assets, 404s, 304s, 429s and live negotiate carry them. `[VERIFIED: Cabinet.Service/Hosting/ContentSecurityPolicy.cs:22 "context.Response.Headers.ContentSecurityPolicy = Policy;" set before "await next(context);", and Program.cs:67 "app.UseContentSecurityPolicy();" as the first middleware]`. It does not use `OnStarting`.

**Known gaps (accept and test knowingly):** responses Kestrel or the host produce before the pipeline runs carry none of these headers: HostFiltering's 400 (it is a startup filter, `[CITED: HostFilteringMiddleware.cs, HostValidationFailed sets 400]`), 431/413 produced by Kestrel itself, and an unhandled-exception 500 where Kestrel resets headers `[ASSUMED]`. They are empty-bodied; the planner should list them as exclusions in the header test rather than pretend coverage.

**Recommended values** (`[ASSUMED]` choices, syntax per MDN; each needs its test):

| Header | Value | Note |
|--------|-------|------|
| `Content-Security-Policy` | unchanged: `default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'` | `[VERIFIED: ContentSecurityPolicy.cs:12-13]`. Do not add `upgrade-insecure-requests`: browser tests use `http://127.0.0.1` and would be upgraded. |
| `X-Content-Type-Options` | `nosniff` | moves from Traefik. |
| `Referrer-Policy` | `strict-origin-when-cross-origin` | the value Traefik sends today `[VERIFIED: cabinet.yml.example:referrerPolicy]`; the BGG links get the origin only. |
| `X-Frame-Options` | `DENY` | belt and braces beside `frame-ancestors 'none'`; both say the same, so no conflict. Recommended yes. |
| `Permissions-Policy` | `accelerometer=(), camera=(), display-capture=(), geolocation=(), gyroscope=(), hid=(), magnetometer=(), microphone=(), midi=(), payment=(), serial=(), usb=(), xr-spatial-tracking=()` | `()` disables a feature `[CITED: MDN Permissions-Policy]`. Grep of `wwwroot/js` and `Pages` found no use of clipboard, geolocation, sensors, fullscreen, vibration, share or media; the only gated-API-like use is `document.startViewTransition`, which is not a permission feature. |
| `Cross-Origin-Opener-Policy` | `same-origin` | links to BGG already use `rel="noopener"` `[VERIFIED: CardOpenTests.cs:157-158 asserts target "_blank" and rel "noopener"]`. |
| `Cross-Origin-Resource-Policy` | `same-origin` | all art, scripts and styles are served from the page's own origin; `same-site` would also allow the owner hostname to embed public art, which nothing does. |
| COEP | do not send | would only matter for cross-origin isolation, which nothing needs; `require-corp` risks blocking nothing today and gains nothing. |
| `Strict-Transport-Security` | Traefik only: `stsSeconds: 31536000`; drop `stsPreload` | Preload needs the apex domain with `includeSubDomains` `[CITED: hstspreload.org]`, so on a sub-domain host the flag is meaningless and removal after submission is slow. The example and the real file currently set `stsPreload: true` and `stsIncludeSubdomains: true` `[VERIFIED: cabinet.yml.example]`. |
| Do not send | `X-XSS-Protection`, `Expect-CT`, `Feature-Policy`, `Public-Key-Pins` | obsolete. |

**Console hazard:** a browser may log a console message for a Permissions-Policy directive it does not recognise. The owner-test base asserts a clean console (`AssertCleanConsole` in `OwnerPickerTests.cs:498-503`, `ConsoleErrors.Should().BeEmpty()`), so add a Chromium and a WebKit browser test that loads the public page and an owner page under the new headers and asserts no console message at all (not just errors), and trim the feature list to whatever that test reports.

### Pattern 2: Per-address rate limiting behind the forwarded-headers setup

**What:** Named policies as `IRateLimiterPolicy<string>`, partitioned by the client address that `UseForwardedHeaders` already resolved, attached with `RequireRateLimiting`. `[CITED: learn.microsoft.com/aspnet/core/performance/rate-limit]`: "UseRateLimiter must be called after UseRouting when rate limiting endpoint specific APIs are used."

**Pipeline placement** (current order `[VERIFIED: Program.cs:67-102]`): `UseContentSecurityPolicy` (67), `UseConnectionPeer` (68), `UseForwardedHeaders` (69), `UseOwnerClientRange` (70), health checks (72), `UseOwnerPathGuard` (90), `UseCabinetArt` (92), `UseRouting` (94), `UseOwnerOnlyRoutes` (95), then endpoint mapping. Insert after line 95: the origin guard first, then `UseRateLimiter()`. The guard goes before the limiter so a cross-site page cannot burn a visitor's own budget.

**Facts verified in source** (`dotnet/aspnetcore`, `RateLimitingMiddleware.cs` read this session):
- The middleware does nothing for an endpoint with no policy and no global limiter, so unlimited endpoints stay unlimited.
- Lease lifetime: `using var leaseContext = await TryAcquireAsync(...); ... await _next(context);`, so the lease is held for the entire downstream pipeline. A concurrency limiter therefore counts a WebSocket or SSE request for the whole connection.
- Global and endpoint limiters are chained: both must grant.
- Rejection: sets `_rejectionStatusCode` (the options default is 503; set 429 explicitly) and calls the policy's `OnRejected` when the endpoint limiter rejected.
- `MapHub` returns the builder of `MapConnections`, which wraps the main endpoint and `/negotiate` in one composite convention builder, so `RequireRateLimiting` on `MapCabinetLive()` covers both the negotiate POST and the connect request. `[CITED: ConnectionEndpointRouteBuilderExtensions.cs: "CompositeEndpointConventionBuilder"]`. Prove it with a test that negotiates repeatedly and sees the 429.

**Chained policy order:** put the concurrency limiter first, then the fixed window. `[CITED: same docs page]`: "time-based limiters ... don't return a permit that was already acquired when a later limiter in the chain rejects", while disposing a lease returns a concurrency permit. So concurrency first means a request refused by the window gives its concurrency permit back, and a request refused by concurrency costs no window permit.

**Partition key:** normalise IPv4-mapped IPv6, and truncate IPv6 to the /64 so one client cannot dodge by cycling addresses (the same idea as Traefik's `ipv6Subnet`). Partitions are evicted when idle by the partitioned limiter; the docs warn that partitions keyed on spoofable input are a DoS risk, which is why the key must be the post-forwarded-headers address only.

**Response shape:** the page treats any 429 on the sync route like the shared-window refusal. `[VERIFIED: sync.js:457-465 — on 429, if the body has no status object it calls adoptRetryAfter(response), which reads the Retry-After header as whole seconds, and then says youCanSyncAgain(...)]`. The existing handler answers 429 with a `Retry-After` header and JSON `{ "outcome": "cooldown", "status": ... }` `[VERIFIED: SyncEndpoints.cs Handle: "statusCode: StatusCodes.Status429TooManyRequests"]`. So the policy's `OnRejected` only needs: status 429, `Retry-After` = whole seconds of the policy window, `Cache-Control: no-store`, JSON `{ "outcome": "cooldown" }`. No new copy, no JS change.

**Timing in tests:** rate limiter windows run on real timers, not `TimeProvider` (the repo's fake clock does not drive them). Make the windows settings (validated like `LiveSettings`) and let tests use one- or two-second windows, or assert only the refusal and the partitioning, not the recovery.

**Testing partitions without a second host:** the integration factory configures `127.0.0.1` as a known proxy `[VERIFIED: CabinetWebApplicationFactory.cs:171 builder.UseSetting("ReverseProxy:KnownProxies:0", "127.0.0.1")]`, so a test client can send `X-Forwarded-For: 203.0.113.7` and `203.0.113.8` and prove two clients have separate budgets. A forged header from an untrusted peer needs the `UntrustedPeerHost` pattern; move the limiter registration into one extension (`AddCabinetRateLimits`/`UseCabinetRateLimits`) used by both `Program.cs` and `UntrustedPeerHost`, exactly as `ForwardedHeadersSetup` is shared today.

### Pattern 3: Live channel counting and the origin guard

**Today** `[VERIFIED]`: `LiveConnectionLimiter.TryAdmit` counts by connection id against `options.MaxConnections` in `CabinetHub.OnConnectedAsync`, and aborts a refused connection with `Context.Abort()` (no reconnect permission). The transports are `connection.Transports = HttpTransportType.WebSockets | HttpTransportType.ServerSentEvents;` (`LiveEndpoints.cs:99`), so **long polling is not offered**. Kestrel's cap is `kestrel.Limits.MaxConcurrentUpgradedConnections = options.MaxConnections` (`LiveEndpoints.cs:87`) and covers upgraded (WebSocket) connections only; the docs say "After a connection is upgraded, it isn't counted against the MaxConcurrentConnections limit." SSE connections are ordinary HTTP requests and count toward `MaxConcurrentConnections`, not the upgraded cap.

**Per-address cap (recommended):** a named policy on the hub endpoints with a per-address `ConcurrencyLimiter` (`PermitLimit` about 20, `QueueLimit` 0) chained with a fixed-window connect-rate limiter. Both transports are counted by request lifetime. A negotiate holds a permit only for its own duration. Keep `LiveConnectionLimiter` as the site-wide cap.
- Hub-side alternative: add `Context.GetHttpContext()!.Connection.RemoteIpAddress` to `TryAdmit` and count per address; the HttpContext of the connect request has already passed `UseForwardedHeaders`. It counts only after the handshake, so a flood of un-handshaken sockets (held up to `HandshakeTimeout` of 10 s, `LiveEndpoints.cs:85`) is not counted. That is the reason to prefer the limiter.
- What a refused page experiences: a 429 on negotiate or on the connect; `live.js` catches the failed `start()` and retries with `reconnectDelayMs` (about 2 s, 10 s, 30 s, then about 60 s with jitter), and the page still shows the cabinet. `[VERIFIED: live.js keepConnected loop, status.js reconnectDelayMs]`. Add one browser test that forces a 429 on the live route and asserts the page stays usable and quiet.
- **Wait for release in tests:** the existing infrastructure shows the hub frees its place a moment before Kestrel frees the upgrade (`KestrelUpgradeWatch` doc comment). Tests that reconnect right after a close must wait for the count to settle with `SyncHarness.WaitUntil`.

**Origin guard (recommended design):** a small middleware, placed before the limiter, for requests whose path is the live route (use `StartsWithSegments(LiveEndpoints.Route)`; the public `HubMetadata` check is an alternative):
- No `Origin` header: allow. Browsers send `Origin` on WebSocket handshakes and on cross-origin requests, and not on same-origin GET/HEAD `[CITED: MDN Origin header]`; a same-origin SSE GET therefore has none, and a non-browser client can forge anything anyway.
- `Origin` present: require its host and effective port to equal the request `Host` (behind Traefik the original Host is passed through; the forwarded-headers setup only touches `X-Forwarded-For` and `X-Forwarded-Proto`). Because host filtering has already restricted `Host` to the env-file list, "origin equals Host" is the same as "origin is one of the site's own hostnames", with no second list to drift, and it works unchanged on `127.0.0.1:<port>` in tests and development.
- Mismatch: 403 with no body.
- Not covered by `WebSocketOptions.AllowedOrigins`: that option does not cover SSE, which is why a custom guard is recommended `[ASSUMED]`; verify there is no SignalR-side origin option before dropping this reasoning.
- Test with raw sockets (`RawHttp` already exists): no Origin allowed, same-origin allowed, foreign Origin 403, on both public and owner ports, for negotiate and for the WebSocket upgrade request.

**Measuring memory per live connection:** hold N connections (for example 50, 200, 500) against a locally run published app with `Live__MaxConnections` raised, read the server's `VmRSS` from `/proc/<pid>/status` after each step, and take the slope in bytes per connection, for each transport. `.NET 10` file-based apps (`dotnet run file.cs` with `#:package Microsoft.AspNetCore.SignalR.Client`) keep the tool out of the repo; or hold the connections from a throwaway test with a trait excluded from the normal run. Kestrel exposes `kestrel.upgraded_connections` and `kestrel.active_connections` meters if a counter is wanted `[VERIFIED: KestrelUpgradeWatch.cs MeterName/InstrumentName]`. Expect tens of kilobytes per connection (buffers `ApplicationMaxBufferSize` 4096 and `TransportMaxBufferSize` 8192 `[VERIFIED: LiveEndpoints.cs]` plus framework state) `[ASSUMED]`, so the current 100 is probably far below what the host can hold and the real constraint is abuse resistance, not memory.

### Pattern 4: Kestrel limits and host filtering

- `MaxRequestBodySize` default is 30,000,000 bytes `[CITED: Kestrel options docs, older version]`. Nothing public takes a body: the sync press, the language switch and negotiate are bodyless, and SSE client messages are tiny (hub `MaximumReceiveMessageSize = MaxMessageBytes` where `MaxMessageBytes = 1024`, `LiveEndpoints.cs:82`). The owner pages already declare their own limits: `RequestSizeLimitAttribute(DefaultBodyLimitBytes)` with `DefaultBodyLimitBytes = 64 * 1024` (`OwnerEndpoints.cs:34,84`) and `UploadLimitBytes = 3 * 1024 * 1024` on Settings (`Settings.cshtml.cs:33,37`); a per-endpoint attribute overrides the server limit, but `UseOwnerOnlyRoutes` also compares `ContentLength` with the endpoint's metadata `[VERIFIED: OwnerAccess.cs:223-225]`. Recommended global limit about 16 KB `[ASSUMED]`; the existing owner restore tests (`OwnerRestoreTests`) prove a 3 MB upload still works.
- `RequestHeadersTimeout` default 30 s: recommend about 10 s. `MinRequestBodyDataRate` and `MinResponseDataRate` default to 240 bytes/s with a 5 s grace; keep the defaults.
- **`KeepAliveTimeout` must stay above Traefik's backend idle timeout.** `[CITED: Traefik serversTransport docs: forwardingTimeouts.idleConnTimeout default 90s; maxIdleConnsPerHost default resolves to 2]`. If Kestrel closes an idle connection first, Traefik can reuse a connection that just closed and answer 502. Keep Kestrel's default (about 2 minutes, `[CITED: Kestrel options docs]`) and do not lower it.
- `MaxConcurrentConnections` is **server-wide, across all listeners**, including the loopback ops port and the owner port; SSE connections count; upgraded WebSockets do not. Setting it too tight lets a flood starve the deploy health check and roll back a good release. Size it from the live cap plus generous page concurrency (for example live cap + 400, about 512) `[ASSUMED]`, and prove in the flood test that `/health` on the ops port still answers while the public port is saturated.
- Because Traefik opens a new backend connection per concurrent request beyond two idle ones, a flood shows up as connection churn on the app port, not as a few long connections.
- **Host filtering:** `AllowedHosts` is a semicolon-delimited list without ports; entries may be wildcards `*.example.com`; `*` disables filtering `[CITED: host-filtering docs]`; a rejection is a 400 `[CITED: HostFilteringMiddleware.cs]`. `[VERIFIED: appsettings.json:2 "AllowedHosts": "*"]`. The filter is a startup filter, so it also guards the ops port and the owner port. Required entries: the public hostname, the owner hostname, **and `127.0.0.1`** (and `localhost`/`[::1]` if used) because the deploy poller health check calls `http://127.0.0.1:5081/health` (`DEFAULT_OPS_URL`, `deploy/bin/cabinet-selfcheck`) and the selfcheck probes `http://127.0.0.1:5080/owner/pictures` and `http://127.0.0.1:5082/`. An IP literal must be listed explicitly. Quote the value in `EnvironmentFile` (`AllowedHosts="host-a;host-b;127.0.0.1"`). Tests: a request with a foreign Host gets 400, the listed host gets 200, and health on the ops port still answers with the loopback entry present; the default `*` in `appsettings.json` keeps every existing test working.

### Pattern 5: Resource caps

**Verified on the real container (read-only probes plus one transient test unit):**
- Kernel 7.0.12-pve, cgroup v2 mounted `nsdelegate`; `cgroup.subtree_control` at the container root lists `cpu memory pids`; the container sees `cgroup.controllers: cpuset cpu io memory hugetlb pids rdma misc dmem`.
- A transient unit created with `systemd-run -p CPUQuota=50% -p CPUWeight=50 -p MemoryHigh=48M -p MemoryMax=64M -p TasksMax=64` showed `cpu.max=50000 100000`, `cpu.weight=50`, `memory.max=67108864`, `memory.high=50331648`, `pids.max=64` in its own cgroup. **So every property works inside the unprivileged LXC.** `[VERIFIED: ssh test 2026-10-20]` The `cpu` controller is not enabled on `system.slice` until a unit asks for it, which is why `cabinet.service` has no `cpu.max` today.
- The unit today: `MemoryMax=infinity`, `CPUQuotaPerSecUSec=infinity`, `TasksMax=16489`, `NRestarts=0`; its `memory.stat`: anon 57 MB, file 36 MB; the whole container: anon 99 MB, file cache 663 MB (page cache counts toward the container limit but is reclaimable), `oom 0`, `oom_kill 0`. `memory.peak` of the unit after restart: 95.9 MB. 18 threads. `[VERIFIED]`
- Inside the container the Proxmox limits are **not** readable from the cgroup files: `memory.max`, `memory.high`, `cpu.max` at the namespace root read `max`, because Proxmox applies them on the parent `lxc/<id>` cgroup (the container's root is an inner `ns` cgroup). What is visible: `/proc/meminfo` shows `MemTotal: 1048576 kB`, `SwapTotal: 524288 kB`, `nproc` is 1 and `cpuset.cpus.effective` lists a single CPU, all through lxcfs. `cpuunits` (weight) and `cpulimit` are not visible from inside at all. So D-07's "Claude checks the effective limits from inside (cgroup files)" can confirm memory, swap and cores through lxcfs and the unit's own limits through its cgroup; the owner must read the CPU weight from `pct config` or the Proxmox UI. `[VERIFIED: probes]`
- Proxmox `pct set <ctid> --cores N --memory MB --swap MB --cpuunits U` (cpuunits default 100 on cgroup v2, range clamp 1-10000; `cpulimit` default 0 = unlimited) `[CITED: pve.proxmox.com pct.1]`. Whether a running container applies them without a restart is not stated in the excerpt read; tell the owner to check `pct pending` `[ASSUMED]`.
- The Traefik container is **1 core and 512 MB** and its `memory.peak` already reads about 510 MiB `[VERIFIED: ssh]`; it also carries five other sites. A flood costs it TLS handshakes before any rate limit applies, which is why the pass bar watches a neighbour site.

**Starting hypotheses for the unit limits** (all `[ASSUMED]` until measured; the planner should write them as `Environment`/directives with the numbers filled from the measurement task):

```ini
MemoryHigh=450M
MemoryMax=600M
MemorySwapMax=0
CPUQuota=80%
CPUWeight=50
TasksMax=512
LimitNOFILE=4096
```
`MemorySwapMax=0` keeps the app from swapping on a shared host; `memory.high` throttles allocation before the kill line; `CPUWeight=50` only matters against siblings inside the container (SSH, poller, journald, which stay at 100); `CPUQuota=80%` leaves a fifth of the single core for everything else. The container caps (cores 1, memory 1024, swap 512 today in `docs/lxc-setup.md`) stay the outer wall; the unit stays well under them so rollback still works during a flood.

**GC configuration:** `Cabinet.Service.runtimeconfig.json` on the host has `"System.GC.Server": true` `[VERIFIED: cat on the host]` (the Web SDK default). `[CITED: Microsoft Learn]`: "Workstation garbage collection is always used on a computer that has only one logical CPU, regardless of the configuration setting", and the container has one CPU, so Server GC is moot today but would silently apply if the owner raises cores to 2. Recommend explicit workstation, non-concurrent GC (`<ServerGarbageCollector>false</ServerGarbageCollector>` and `<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>` in `Cabinet.Service.csproj`, or `Environment=DOTNET_gcServer=0 DOTNET_gcConcurrent=0` in the unit) and let the measurement decide. Memory limits: `[VERIFIED: dotnet/runtime cgroup.cpp]` the runtime reads only `memory.max` (never `memory.high`), walks from the process's cgroup up to the mount root taking the minimum, and treats "no limit" as unrestricted. In this container the namespace root reads `max`, so **without `MemoryMax` on the unit the runtime sees no limit at all**; with `MemoryMax=600M` it sees 600 MB and, per `[CITED: Microsoft Learn]` ("the default value is either 20 MB or the heap hard limit percent of the container limit... default 75%"), sets a heap hard limit of 75% of it. That is another reason `MemoryMax` is the right directive, not only `MemoryHigh`. ImageSharp pixel buffers are partly unmanaged, so they count toward the cgroup but not toward the GC heap limit; the existing guard is `Images:MaxMegapixels` 36 (`appsettings.json:45`), about 144 MB for one worst-case RGBA decode, so leave headroom above the measured rebuild peak for one worst-case image. Use `DOTNET_GCHeapHardLimit` (hex bytes) only if the measurement shows the 75% default is wrong.

### Pattern 6: Outbound firewall (verified)

The current output chain is `type filter hook output priority 0; policy accept;` with no rules `[VERIFIED: deploy/nftables/cabinet.nft.in:33-35]`. This candidate loads and behaves correctly in a throwaway network namespace (Alpine nft 1.0.9, the same as the host's 1.0.9): blocked ranges fail instantly ("Host is unreachable", 0 ms), a public address connects, counters increment.

```nft
  chain output {
    type filter hook output priority 0; policy accept;

    oif lo accept
    ct state established,related accept
    meta l4proto ipv6-icmp accept

    ip daddr { @CABINET_EGRESS_RESOLVERS@ } udp dport 53 accept
    ip daddr { @CABINET_EGRESS_RESOLVERS@ } tcp dport 53 accept

    ip daddr { @CABINET_EGRESS_BLOCKED_RANGES@ } counter reject with icmpx type admin-prohibited
    ip6 daddr { @CABINET_EGRESS_BLOCKED_RANGES6@ } counter reject with icmpx type admin-prohibited
  }
```
- Order matters: loopback, established and related (replies to Traefik and to SSH stay up), ICMPv6 (neighbour discovery needs link-local, so the IPv6 block must not catch it), the resolver exceptions, then the blocks.
- `reject` instead of `drop` so a mistakenly blocked service fails at once rather than hanging for the connect timeout.
- Generic defaults for the example config (allowed by the gitleaks allow-list): IPv4 `10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,100.64.0.0/10,169.254.0.0/16`; IPv6 `fc00::/7,fe80::/10`. The cabinet container has an IPv6 global /64 on its LAN `[VERIFIED: ip -6 addr]`, which is also "the home network", so the owner must be able to add that prefix to the IPv6 list; the 100.64.0.0/10 default covers the common VPN overlay range; narrower real ranges belong only in the server-side config.
- DNS: `/etc/resolv.conf` on the host names a single LAN resolver directly (not the `127.0.0.53` stub), DNSSEC off, DoT off `[VERIFIED: resolvectl status]`, so the resolver must be an allowed UDP/TCP 53 destination. There is no NTP daemon in the container (`systemd-timesyncd`, `chrony`, `ntp` all inactive `[VERIFIED]`), so no NTP exception is needed; keep the key optional or omit it.
- Provisioning plumbing: `provision_render_template` replaces every `@NAME@` token, checks values with `provision_validate_safe_value` (no `;`, braces or newlines) and a stricter shape for two known tokens, and dies on any unreplaced token `[VERIFIED: deploy/provision.sh render function]`. New tokens need new validators (an IPv4 address list for the resolvers, an IPv6 CIDR list), entries in `PROVISION_CONF_ALLOWED_KEYS`, lines in `provision.conf.example`, the call in `50-firewall.sh`, and tests. `provision_validate_conf` dies before touching the host when a required key is missing, so a host with an old `provision.conf` must get the new keys before provisioning is re-run; make that the documented order.
- Existing test assertions that will break and must be updated: `deploy/tests/render-templates-test.sh:154-155` asserts `'hook output priority 0; policy accept;'` (keep the policy, so it still passes), and `:164` asserts `"exactly three tcp dport rules exist"` (the DNS rule adds `tcp dport` lines: scope the count to the input chain). Add assertions for the new rules and "no unreplaced token".
- Selfcheck additions: a limits check (`systemctl show cabinet -p MemoryMax -p CPUQuotaPerSecUSec -p TasksMax` against the shipped directives) and an egress check (the output chain contains the reject rule; one instant-fail probe to an address at the top of a blocked block, which must fail fast, not time out). `cabinet-selfcheck` already captures command output into variables before matching.
- Side effects to state in the docs: the cabinet host can no longer reach LAN services at all (a local apt cache, a LAN time server, LAN monitoring); the owner link-check feature fetches owner-chosen URLs server-side, and this rule also stops it reaching LAN addresses; after enabling, neighbour-site timing measurements must be taken from the proxy container or the desktop, not from the cabinet host.

### Pattern 7: Traefik public router (file-provider shape)

```yaml
http:
  middlewares:
    cabinet-rate-limit:
      rateLimit:
        average: 50          # placeholder: sized from one measured first visit
        period: 1s
        burst: 1000          # placeholder: about 8 simultaneous first visits
        sourceCriterion:
          ipStrategy:
            ipv6Subnet: 64
    cabinet-in-flight:
      inFlightReq:
        amount: 150          # must exceed the live cap plus concurrent page loads
        sourceCriterion:
          ipStrategy:
            ipv6Subnet: 64   # explicit: the default criterion is the request host
    cabinet-security-headers:
      headers:
        stsSeconds: 31536000
  routers:
    cabinet:
      rule: "Host(`cabinet.example.com`)"
      entryPoints: [websecure]
      middlewares:
        - cabinet-lan-only             # removed at go-public, on this router only
        - cabinet-rate-limit
        - cabinet-in-flight
        - cabinet-security-headers
      service: cabinet
      tls: { certResolver: letsencrypt }
    cabinet-owner:                     # unchanged: allow-list, no rate limit, headers
      middlewares: [cabinet-lan-only, cabinet-security-headers]
```
Verified semantics (Traefik docs and source at v3.5.4, read this session):
- `rateLimit` is a token bucket: `average`/`period` is the refill rate, `burst` the bucket size ("Maximum number of requests allowed to go through at the very same moment"), `average: 0` disables it. If no `sourceCriterion` is set the default is the request's remote address. Only one strategy may be set at a time.
- **It delays before it refuses.** A request whose required wait is within `maxDelay` (500 ms below 1 req/s, otherwise `1s / (2 × rate)`) is held and then forwarded; beyond that the answer is `429` with `Retry-After` (whole seconds, rounded up) and `X-Retry-In`; a request above the burst gets `429` with the body "No bursty traffic allowed". 429 decisions are not logged.
- Buckets are in memory, per middleware instance, keyed `<middleware name>:<source>`; the page notes the limiter runs before proxying, so the previous hop is not yet in `X-Forwarded-For`.
- **`ipStrategy.depth` is wrong for an edge Traefik**: it indexes the incoming `X-Forwarded-For` from the right. At rate-limit time that header holds nothing from the client (Traefik strips `X-Forwarded-*` from untrusted peers: `DeleteXForwardedHeaders` when no trusted IPs are configured, `[VERIFIED: forwardedheaders source]`), so the key would be empty for everyone and all clients would share one bucket. Use the remote address (the default).
- `ipv6Subnet` maps an IPv6 address to the first address of its subnet, applies to the depth and remote-address strategies, and is present in the v3.2 to v3.4 docs and later `[CITED: Traefik docs]`; 3.5.4 is later. Confirm Traefik accepts the file on save by watching its log.
- `inFlightReq`: `amount` is the simultaneous in-flight limit, answer `429`; **the default source criterion is the request host**, not the address (`[VERIFIED: inflight_req.go]`: if no criterion is set, `RequestHost: true`). Set `ipStrategy` explicitly. Whether an empty `ipStrategy: {}` decodes to a non-nil value is unverified `[ASSUMED]`; `ipv6Subnet: 64` is non-empty and desirable anyway.
- **In-flight counts an SSE stream or WebSocket until it closes** `[ASSUMED: from how the in-flight counter decrements when the handler returns; a reverse-proxied streaming request holds the handler]`. So the per-address `amount` has to exceed the live cap (20) plus parallel loads of a group (a first visit opens many parallel HTTP/2 streams); 10 phones on one address can mean 10 live connections plus 100 parallel page requests. Verify with the flood tool: hold 20 SSE connections from one address and confirm normal page loads still succeed.
- Traefik hand-edit hazards: a typo drops the whole file, and a `.bak` file inside the watched directory would be parsed as a second configuration. The existing backup is kept outside the watched directory (`/etc/traefik/` beside `dynamic/`) `[VERIFIED: ls]`. Edit by writing a copy outside the directory, checking it, then an atomic `mv` into place.
- Name drift: the real file names its allow-list middleware differently from the example's `cabinet-lan-only` and uses `stsPreload`/`stsIncludeSubdomains` `[VERIFIED: read of the real file with hostnames masked]`. The go-public step must read the real file first; the repo documentation stays generic.

### Anti-Patterns to Avoid
- **Rate limiter before `UseForwardedHeaders` or before `UseRouting`:** every client looks like the proxy, or the endpoint policies are never seen.
- **A global limiter as the main defence:** the first-visit burst (many parallel asset requests) would be refused; keep the Traefik limit sized from a measured visit and the app limits on the two endpoints that cost something.
- **Hand-parsing `X-Forwarded-For` in the limiter:** use `HttpContext.Connection.RemoteIpAddress` after the existing middleware.
- **`drop` rules for blocked egress:** hanging connects look like hangs in sync and deploy; `reject` fails visibly.
- **Lowering Kestrel `KeepAliveTimeout`** or setting a tight `MaxConcurrentConnections` without proving the ops-port health check survives.
- **Putting the real middleware name, hostnames or addresses in docs or tests.**

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Per-address counters, windows, concurrency | A dictionary of timestamps | `System.Threading.RateLimiting` limiters in named policies | Idle eviction, atomic leases, queueing options, documented chaining. |
| Per-address live connection count by transport | Separate WebSocket and SSE counters | A concurrency limiter on the hub endpoints | The lease lasts as long as the request, whatever the transport. |
| Host header validation | A custom Host middleware | `AllowedHosts` + built-in HostFiltering | Handles wildcards, empty hosts, ports. |
| Origin comparison for the hub | A cross-origin policy framework | One small middleware comparing `Origin` with `Host` | WebSocket origin options do not cover SSE. |
| Token bucket, in-flight cap at the edge | App-side counting of proxy traffic | Traefik `rateLimit` and `inFlightReq` | Refuses before the container pays for the request. |
| Resource caps | A watchdog script | systemd cgroup properties and Proxmox limits | Kernel enforced; verified to work in the unprivileged container. |
| LAN egress blocking | An app-level URL deny-list for the whole host | nftables output rules | Covers every process, including the deploy tools and the owner link fetch. |
| Load generation | A bespoke script | `hey` (or `wrk`) | Latency histograms, fixed concurrency, TLS. |
| External scanner grades | A re-implementation | The scanners, with the owner's go | Plus the scripted probe for the parts that are deterministic. |

**Key insight:** every item above already exists in the framework, the proxy or the kernel; the work is wiring, sizing, ordering and proving each one with a test, not building mechanisms.

## Common Pitfalls

### Pitfall 1: `inFlightReq` shared by every client
**What goes wrong:** with no `sourceCriterion` the bucket is the request host, so the whole internet shares one cap and a single visitor's streams can lock out everyone.
**Why it happens:** `rateLimit` defaults to the remote address, `inFlightReq` does not.
**How to avoid:** set `sourceCriterion.ipStrategy` on both; render test asserts both middlewares carry it.
**Warning signs:** 429s for visitors from different addresses at the same time.

### Pitfall 2: Live connections eat the in-flight budget
**What goes wrong:** 10 phones on one Wi-Fi hold 10 live streams and load pages in parallel; a tight `amount` refuses the page loads.
**How to avoid:** size `amount` above live cap + parallel loads; test with 20 held streams from one address.

### Pitfall 3: Host filtering breaks the deploy health check
**What goes wrong:** `AllowedHosts` without `127.0.0.1` makes `GET http://127.0.0.1:5081/health` a 400, the installer concludes the release is unhealthy and rolls back, and `cabinet-selfcheck` fails its public-port and owner-port probes.
**How to avoid:** list loopback; test health on the ops port with the filter on; set the env value before the release installs (the key is understood by the old version too).

### Pitfall 4: Ordering across the release
**What goes wrong:** the new version needs env and provision values that are not there yet, fails its health check and rolls back; or the new unit and firewall are shipped but never applied.
**How to avoid:** env file keys first (inert for the old version), `provision.conf` keys next, then the release, then re-run `provision.sh` from the active release (units and the firewall template are installed only by provisioning, and `cabinet-selfcheck` fails until that is done) `[VERIFIED: docs/deploy.md "When to re-run provisioning"]`. New app settings must default to safe, working values so an old env still boots.

### Pitfall 5: Measuring on the final tag
**What goes wrong:** unit limits ship in the repo and releases are immutable, so sizing on `v1.0.0` forces another release.
**How to avoid:** measure on a `0.x` pre-release carrying the code, with runtime overrides (`systemctl set-property --runtime cabinet.service MemoryMax=... CPUQuota=...`, a drop-in, env overrides, a hand-edited Traefik file); put the final numbers in the repo; cut `v1.0.0` and re-run the flood as verification.

### Pitfall 6: A flood is a TLS flood first
**What goes wrong:** the rate limit runs after the handshake; the proxy has 1 core and 512 MB and serves five other sites. `hey` runs on the same single core, so the generator and Traefik compete.
**How to avoid:** watch a neighbour site and the proxy's own memory; stop on neighbour slowdown; keep the first run short and small and increase stepwise; remember that the proxy's access log has no rotation (104 MB today, 6 GB free) `[VERIFIED: ssh]`, so note the size before and after.

### Pitfall 7: Picture rebuild is two runs, not one
**What goes wrong:** the snapshot has 99 picture records and 192 stored files `[VERIFIED: snapshot.json and art/ listing on the host]`, while `Images:MaxDownloadsPerRun` is 80 (`appsettings.json:48`), so one run leaves about 19 waiting, and the shared manual window is 10 minutes (`ManualCooldownMinutes: 10`, `appsettings.json:28`).
**How to avoid:** plan two runs (second after the window or the hourly sync) or raise `Images__MaxDownloadsPerRun` for the measurement. "About 65 pictures" in the decision is the game count (66 items), not the picture count.

### Pitfall 8: Header set vs console warnings and early rejections
**What goes wrong:** an unknown Permissions-Policy directive logs in some browsers and fails clean-console tests; host-filter 400s and Kestrel rejections carry no headers.
**How to avoid:** a console-clean browser test on Chromium and WebKit; list the header-less rejections as explicit exclusions.

### Pitfall 9: Egress block breaks something quietly
**What goes wrong:** a wrong resolver address stops DNS, so sync, deploy polling and package updates all fail without an obvious error.
**How to avoid:** `nft -c` dry-run is already in `50-firewall.sh`; add the selfcheck DNS-resolution check; keep `pct enter` as the documented escape hatch; verify sync, picture download, deploy poll, Sigstore verification and an apt update after applying.

### Pitfall 10: Real names leaking into the repository
**What goes wrong:** copying real hostnames, addresses, the proxy's real middleware name or scanner screenshots into docs, tests or the phase record.
**How to avoid:** placeholders; the phase record holds grades and numbers only; run `build/lint.sh` and `build/scan-history.sh`; the gitleaks rules block RFC 1918 host addresses and narrower ranges.

## Code Examples

Patterns, not final code. Every number in these examples is an `[ASSUMED]` starting hypothesis; comments follow the repo rule (`///` only).

### Policy with the existing 429 shape
```csharp
/// <summary>Limits how often one client address may press "sync now"; a refusal looks like the shared-window one.</summary>
public sealed class SyncPressPolicy(RateLimitOptions options) : IRateLimiterPolicy<string>
{
    /// <inheritdoc />
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected => async (context, cancellation) =>
    {
        var response = context.HttpContext.Response;
        response.StatusCode = StatusCodes.Status429TooManyRequests;
        response.Headers.CacheControl = "no-store";
        response.Headers.RetryAfter = ((int)options.SyncWindow.TotalSeconds).ToString(CultureInfo.InvariantCulture);
        await response.WriteAsJsonAsync(new { outcome = "cooldown" }, cancellation);
    };

    /// <inheritdoc />
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            ClientAddress.PartitionKey(httpContext),
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.SyncPressesPerWindow,
                Window = options.SyncWindow,
                QueueLimit = 0,
            });
}
```
Registration: `services.AddRateLimiter(o => { o.RejectionStatusCode = StatusCodes.Status429TooManyRequests; o.AddPolicy<string, SyncPressPolicy>("sync-press"); o.AddPolicy<string, LiveConnectPolicy>("live-connect"); })`, then `endpoints.MapPost(SyncRoute, Handle).RequireRateLimiting("sync-press")` and `MapHub<CabinetHub>(...).RequireRateLimiting("live-connect")`. Source pattern: `[CITED: learn.microsoft.com/aspnet/core/performance/rate-limit, "Chain limiters in a named policy"]`.

### Live policy: concurrency first, then window
```csharp
public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
    RateLimitPartition.Get(
        ClientAddress.PartitionKey(httpContext),
        _ => RateLimiter.CreateChained(
            new ConcurrencyLimiter(new ConcurrencyLimiterOptions { PermitLimit = options.LivePerAddress, QueueLimit = 0 }),
            new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions { PermitLimit = options.LiveConnectsPerWindow, Window = options.LiveWindow, QueueLimit = 0 })));
```

### Client address key
```csharp
/// <summary>The key a client is counted under: the address after forwarded headers, IPv6 reduced to its /64.</summary>
public static string PartitionKey(HttpContext context)
{
    var address = context.Connection.RemoteIpAddress;
    if (address is null) { return "unknown"; }
    if (address.IsIPv4MappedToIPv6) { address = address.MapToIPv4(); }
    if (address.AddressFamily != AddressFamily.InterNetworkV6) { return address.ToString(); }
    Span<byte> bytes = stackalloc byte[16];
    address.TryWriteBytes(bytes, out _);
    bytes[8..].Clear();
    return new IPAddress(bytes) + "/64";
}
```

### Unit directives (verified to work through a transient unit)
```ini
[Service]
MemoryHigh=450M
MemoryMax=600M
MemorySwapMax=0
CPUQuota=80%
CPUWeight=50
TasksMax=512
```
Runtime experiment without editing files: `systemctl set-property --runtime cabinet.service MemoryMax=600M CPUQuota=80%`; revert with `systemctl revert cabinet.service`.

### Measuring on the host
```bash
systemctl show cabinet -p MemoryCurrent -p MemoryPeak -p CPUUsageNSec -p NRestarts
cat /sys/fs/cgroup/system.slice/cabinet.service/memory.events   # high, max, oom, oom_kill counters
cat /sys/fs/cgroup/system.slice/cabinet.service/memory.peak
cat /sys/fs/cgroup/cpu.stat /sys/fs/cgroup/cpu.pressure /sys/fs/cgroup/memory.pressure
systemd-cgtop -b -n 2 -d 5
```
`systemctl show -p MemoryPeak` is available on systemd 255 `[VERIFIED: returned a value on the host]`.

### Forcing the one-time picture rebuild without touching the snapshot or owner data
`[VERIFIED: ArtSync.cs IsDue returns true when "files.Any(file => !cache.Has(file.Name))"; ArtCache constructor creates the directory and removes stray temporaries]`. A picture record is due again when any of its files is missing from the `art` directory, so:
```bash
sudo systemctl stop cabinet
sudo mv /var/lib/cabinet/art /var/lib/cabinet/art.rebuild-backup
sudo systemctl start cabinet         # ArtCache recreates art/ as the service user
# press "sync now" (or wait for the hourly run); a second run is needed for the remaining ~19
# afterwards, once the pictures are back and measured:
sudo rm -rf /var/lib/cabinet/art.rebuild-backup
```
Never delete `snapshot.json`, `owner-data.json`, `sync-state.json` or `owner-data-versions/`. If the rebuild goes wrong, stop, remove the new `art`, move the backup back. Pictures are missing on the site until they are refetched (the site is still LAN-only at that point). `Prune` only removes unreferenced files older than the 7-day grace (`PruneGraceDays`), so it does not interfere. The download gap is 1000 ms (`DownloadGapMilliseconds`), so about 100 pictures take roughly two minutes plus processing.

### Flood commands (execution time, owner's go required; hostname from the environment, never written down)
```bash
# through the public router, TLS included (from the proxy container)
hey -z 20s -c 20  "https://${CABINET_PUBLIC_HOST}/cabinet/status"
hey -z 20s -c 100 -h2 "https://${CABINET_PUBLIC_HOST}/"
# straight at the app port, bypassing Traefik's limits (source is the known proxy)
hey -z 20s -c 100 -H "X-Forwarded-For: 203.0.113.7" "http://${CABINET_APP_ADDRESS}:5080/cabinet/status"
```
`hey` flags: `-z` duration, `-c` workers, `-q` per-worker rate, `-h2`, `-H`, `-disable-keepalive` `[ASSUMED: from memory of the tool; run hey -h first]`. Sending `X-Forwarded-For` straight at the app port is believed because the proxy address is the known proxy, so it can simulate many clients for the partition tests. Neighbour-site timing: a short `python3` urllib loop on the proxy container (no `curl` there) or a few requests a second from the desktop; do not use the cabinet host once the egress block is on.

### Probe helper shape
Model on `build/check-owner-routes.sh` (environment-driven hostnames, `PASS`/`FAIL` per check with status codes only, exit 1 on any failure, 2 when a hostname is missing, stubbed-curl test in `build/tests/`). Checks: public home 200 over HTTPS; HTTP redirects to HTTPS; `/health` 404 on the public hostname; `/owner/pictures` 404 on the public hostname; the ops port and the app and owner ports refuse or time out from outside; owner hostname answers 403 from outside (Traefik's allow-list default `rejectStatusCode` is 403 `[CITED: Traefik ipAllowList docs]`); the header set is present (HSTS, nosniff, CSP, Referrer-Policy, Permissions-Policy, COOP, CORP, X-Frame-Options) and no `Server` header; a short burst yields a 429 with `Retry-After`. Give it an explicit mode (from outside or from the LAN), because the owner-host expectation differs by vantage point.

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| Per-endpoint custom throttling attributes | Built-in rate limiting middleware with named, partitioned policies and chained limiters | ASP.NET Core 7+, chaining documented for 10 | No package; limiters are shared-framework. |
| Traefik `ipWhiteList` | `ipAllowList` | v3 | The example already uses `ipAllowList`. |
| Forwarded headers with `KnownNetworks` | `KnownIPNetworks` | .NET 10 | `ForwardedHeadersSetup` already clears both known lists. |
| Feature-Policy header | Permissions-Policy | browsers 2020+ | Send only Permissions-Policy. |
| X-XSS-Protection, Expect-CT, HPKP | CSP and nothing | removed from browsers | Do not send. |
| Server GC default for web apps | Workstation GC for small single-CPU containers; DATAS adaptive sizing under Server GC since .NET 9 | .NET 9 | One-core container already falls back to workstation. |

**Deprecated/outdated:** `stsPreload` on a sub-domain host; Traefik `ipStrategy.depth` as a client-address source on an edge proxy.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The numbers in this document (Traefik `average 50`, `burst 1000`, `inFlightReq 150`, app sync 10 per window, live 20 per address, unit `450M`/`600M`/`80%`, Kestrel `16 KB`/`10 s`/`512`) are starting hypotheses only | Patterns 2, 4, 5, 7 | Over-tight numbers refuse a group; over-loose ones fail the pass bar. Mitigated by the measurement tasks. |
| A2 | Traefik's `inFlightReq` counts an SSE or WebSocket request until it ends | Pattern 7, Pitfall 2 | If it does not, the `amount` coupling disappears (benign); if it does and is ignored, page loads are refused. Verify with 20 held streams. |
| A3 | `ipStrategy: {}` (empty) may not decode to a non-nil value for `inFlightReq`; `ipv6Subnet: 64` is used instead | Pattern 7 | If neither sets a non-nil strategy the criterion silently falls back to the host. Verify with two source addresses. |
| A4 | `WebSocketOptions.AllowedOrigins` does not cover SSE, so a custom origin guard is needed | Pattern 3 | If SignalR offers its own origin option, the guard is redundant (harmless). |
| A5 | An unhandled-exception 500 and Kestrel's own early rejections carry no app-set headers | Pattern 1 | Test expectations too strict or too loose. |
| A6 | `pct set` applies cores, memory, swap and cpuunits to a running container; cpuunits and cpulimit are unreadable from inside | Pattern 5 | If a restart is needed the owner must schedule it; if readable, more can be self-verified. |
| A7 | Per-connection live memory is tens of kilobytes | Pattern 3 | The site-wide cap would be sized wrongly; measurement decides. |
| A8 | `hey` flag names and behaviour as listed | Code Examples | A flood run fails to start; run `hey -h`. |
| A9 | SSL Labs and securityheaders.com cannot be driven by Claude without the owner (SSL Labs API v4 needs a registered organisation e-mail; its page is script-driven) | Go-public checklist | If wrong, more can be automated. |
| A10 | Packet loss from `reject` towards a LAN resolver is not an issue because DNS is allowed first | Pattern 6 | DNS outage after applying; mitigated by the selfcheck and `pct enter`. |

## Open Questions

1. **Origin allow-list source (D-04 says "from the env file").**
   - What we know: `AllowedHosts` is already needed from the env file; "origin host equals Host" plus host filtering is equivalent to an allow-list of the site's own hostnames and needs no second value.
   - What is unclear: whether the owner wants a separate explicit key.
   - Recommendation: reuse `AllowedHosts`; if the owner wants a separate key, add it with the same startup validation.
2. **SSL Labs and securityheaders.com automation (D-11).**
   - What we know: the SSL Labs page is script-driven and its API v4 asks for registration with a name and organisation e-mail `[CITED: ssllabs-scan docs]`; the MDN Observatory v2 scan is a POST with a `host` query parameter `[CITED: MDN FAQ]`; securityheaders.com has a server-rendered result page.
   - Recommendation: Claude runs Observatory and securityheaders.com; the owner opens SSL Labs in a browser with the results hidden from the public board and reads the grade back. Ask before sending the hostname.
3. **How the .NET runtime sees memory inside the container.** Without `MemoryMax` it reads no limit. Confirm after applying `MemoryMax` by logging `GC.GetGCMemoryInfo().TotalAvailableMemoryBytes` once in a throwaway run, or with `dotnet-counters`.
4. **Access log rotation on the proxy.** The access log is 104 MB with no `logrotate` entry; a flood grows it. Out of the repository; record it as an owner/maintenance item.
5. **Re-acceptance dates for the two maintenance accesses (D-14).** Chosen with the owner at planning time; suggest the next release after go-public or a fixed 90-day review.
6. **Whether a generous per-address global limiter in the app is wanted.** Recommendation: no. Traefik covers general floods, the measured first visit is the sizing input, and the app's own caps are the two endpoints plus Kestrel and the unit limits; a direct flood at the app port then slows the app under the unit's CPU cap rather than being refused.
7. **Real source address after go-public.** Verified indirectly (see Environment Availability), but confirm with the owner's phone on mobile data by reading the proxy access log's first field for that request before trusting per-address limits.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK (desktop) | build and tests | yes | 10.0.113 (pin 10.0.112, `latestFeature`) | — |
| ASP.NET Core runtime (cabinet host) | the app | yes | 10.0.13 | — |
| Docker (desktop) | `build/lint.sh`, nft dry-run experiments | yes | 29.9.1 | — |
| Node (desktop) | page script tests | yes | v24.19.0 | — |
| shellcheck (desktop) | shell lint | no | — | `build/lint.sh` runs it in a container |
| nft (desktop) | rule syntax | yes (needs privileges for `-c`) | 1.1.6 | check inside a container with `NET_ADMIN`: done, OK |
| SSH to the cabinet and proxy containers | measurements, probes | yes (agent keys loaded) | — | owner runs commands |
| systemd cgroup properties inside the unprivileged LXC | unit caps | yes, verified | systemd 255 | — |
| Traefik | the proxy | yes | 3.5.4 | — |
| `curl` on the proxy container | probes, flood | **no** | — | `python3` is there; `hey` via apt (owner's go) |
| Flood tool | D-08 | **no** (none installed) | `hey` 0.1.4 in the archive | `wrk`; `curl --parallel` from the desktop (not for the TLS flood) |
| Real client address through the port forward | per-address limits | yes (indirect) | 855 distinct public source addresses among 18,498 public-source requests in the last 20,000 lines of the proxy's access log, for an already public site | confirm with the phone probe |
| Proxmox host access | container caps | **no** (by design) | — | owner runs `pct set` |
| BGG official pages | terms re-read | live pages return 403 (Cloudflare); archive captures available | newest captures: `using_the_xml_api` 2026-09-24, `XML_API_Terms_of_Use` 2026-10-02 | owner reads the live pages in a browser |

**Missing dependencies with no fallback:** none.
**Missing dependencies with fallback:** flood tool and `curl` on the proxy (install `hey` with the owner's go, remove afterwards); live BGG pages (archive captures plus the owner's browser).

**BGG terms re-read (D-13).** The archived "Using the XML API" capture of 2026-09-24 still reads "Version date 2025-07-02", the same version recorded in `.claude/CLAUDE.md`; the "XML API Terms of Use" capture of 2026-10-02 carries the same terms the stack research summarised: licence "solely for strictly non-commercial purposes", no AI training, "You may not modify the data", credit BoardGameGeek by name and show the "Powered by BGG" logo linked back "at a size such that the text is easily legible", requests from servers with results cached, tokens sent to `boardgamegeek.com` without `www`. No change found. Caveats: the newest captures are 18 and 26 days old and the live pages could not be fetched, so the owner should skim the live pages in a browser before saying "go". `[VERIFIED: archive.org captures read via curl this session]`

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.2 on Microsoft.Testing.Platform (`global.json` `"test": { "runner": "Microsoft.Testing.Platform" }`), FluentAssertions 8.11.0; Playwright .NET 1.63.0 for browser tests; bash tests for `deploy/` and `build/`; `node --test` for page scripts |
| Config file | `global.json`, `Directory.Build.props`; no per-project runner config |
| Quick run command | `dotnet test --project Cabinet.IntegrationTests --filter-class "Cabinet.IntegrationTests.<Class>"` ; `bash deploy/tests/render-templates-test.sh` |
| Full suite command | `dotnet test --project Cabinet.UnitTests && dotnet test --project Cabinet.IntegrationTests && node --test build/tests/*.test.mjs && build/lint.sh` ; browser: `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.<prefix>*"` (as CI does) |

### Phase Requirements → Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| SEC-03 | Every header present exactly once on page, static, art, JSON, negotiate, 404, 429, 304, on public, owner and ops ports | integration | `dotnet test --project Cabinet.IntegrationTests --filter-class "Cabinet.IntegrationTests.SecurityHeadersTests"` | ❌ Wave 0 (extends `ContentSecurityPolicyTests`, which exists) |
| SEC-03 | Page and an owner page log nothing to the console under the new headers (Chromium, WebKit); CSP-violation tests stay green | browser | `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.Lights*"` plus a new class | ❌ Wave 0 (`LightsTests`, `FilterAccessTests`, `FilterDockMemoryTests` exist) |
| SEC-02 | Sync press over the per-address limit gets 429 + `Retry-After` in the shared shape; two forwarded addresses have separate budgets; a forged header from an untrusted peer does not create a budget | integration | `... --filter-class "Cabinet.IntegrationTests.SyncRateLimitTests"` | ❌ Wave 0 |
| SEC-02 | Negotiate and connect limited; 21st concurrent live connection from one address refused for WebSocket and SSE; release frees the place; site-wide cap and `MaxConcurrentUpgradedConnections` applied | integration (`Trait("Category","Live")`) | `... --filter-class "Cabinet.IntegrationTests.LiveAddressCapTests"` | ❌ Wave 0 (`LiveCapRefusalTests`, `KestrelUpgradeWatch` exist) |
| SEC-02 | Foreign `Origin` refused on negotiate and WebSocket upgrade; no Origin and same-origin allowed | integration (raw socket) | `... --filter-class "Cabinet.IntegrationTests.LiveOriginTests"` | ❌ Wave 0 |
| SEC-02 | Refused live page stays usable and quiet | browser | `... --filter-class "Cabinet.BrowserTests.LiveLook*"` plus new test | ❌ Wave 0 |
| SEC-04 | Foreign Host gets 400; listed host 200; loopback health still answers with the filter on; ops port unchanged | integration | `... --filter-class "Cabinet.IntegrationTests.HostFilteringTests"` | ❌ Wave 0 (`HealthEndpointTests`, `OwnerWallTests` exist) |
| SEC-04 | Owner router keeps allow-list, carries no rate limit; public router carries rate limit and in-flight with explicit source; no `PathPrefix`; only documentation addresses and `example.com` names | script | `bash deploy/tests/render-templates-test.sh` | ✅ exists, needs new assertions |
| SEC-04 | External probe script passes and fails per check, never prints a hostname | script | `bash build/tests/check-public-routes-test.sh` | ❌ Wave 0 |
| SEC-06 | Unit carries the limit directives; rendered ruleset has the output rules and no unreplaced token; validators refuse malformed ranges; selfcheck limit and egress checks | script | `bash deploy/tests/sandboxing-test.sh && bash deploy/tests/render-templates-test.sh && bash deploy/tests/provision-logic-test.sh` | ✅ exist, need new assertions |
| SEC-06 | Kestrel limits applied and body limit does not break owner uploads | integration | `... --filter-class "Cabinet.IntegrationTests.KestrelLimitsTests"` and `OwnerRestoreTests` | ❌ Wave 0 / ✅ |
| SEC-01, SEC-04, SEC-06 | Phone on mobile data, scanners, flood pass bar, caps applied on the host, rebuild and sync peaks | manual | checklist in the go-public document | manual-only: needs the real network, the owner's phone and the Proxmox host graphs |
| (folded todo) | Browser tests no longer flake on CI | browser | failing classes run repeatedly, before and after | ✅ existing classes; helper changes Wave 0 |

### Sampling Rate
- **Per task commit:** the single new test class plus the template test it touches (each well under 30 s except the live and browser classes, which take longer; run those by class).
- **Per wave merge:** `dotnet test --project Cabinet.UnitTests`, `dotnet test --project Cabinet.IntegrationTests`, `build/lint.sh script-tests`, `node --test build/tests/*.test.mjs`.
- **Phase gate:** full integration and unit suites, all browser slices (run solo, not in parallel: concurrent browser suites exhaust the inotify limit `[CITED: project memory]`), `build/lint.sh` (all checks), `build/scan-history.sh`, then the manual checklist.

### Wave 0 Gaps
- [ ] `Cabinet.IntegrationTests/SecurityHeadersTests.cs` — SEC-03 matrix (reuse `CabinetWebApplicationFactory`, `CreateOwnerClient`, `CreateOpsClient`)
- [ ] `Cabinet.IntegrationTests/SyncRateLimitTests.cs`, `LiveAddressCapTests.cs`, `LiveOriginTests.cs`, `HostFilteringTests.cs`, `KestrelLimitsTests.cs`
- [ ] Limiter registration shared with `UntrustedPeerHost` for the forged-header test
- [ ] `Cabinet.BrowserTests` console-clean test (Chromium and WebKit) and live-refusal test
- [ ] `build/tests/check-public-routes-test.sh` and `build/check-public-routes.sh`
- [ ] Extend `deploy/tests/render-templates-test.sh` (scoped chain counts, egress, Traefik middlewares), `provision-logic-test.sh` (new validators), `sandboxing-test.sh` (unit directives, selfcheck checks)
- [ ] Shared browser helpers in `CabinetPageTest` for the flaky-test hardening (live-connected wait, URL retry, settle) — see "Flaky browser tests"
- [ ] Framework install: none; all frameworks exist

### Flaky browser tests (folded todo): concrete patterns already in this repo
- **Retrying assertions on the end state** are the norm: `await Expect(Page.Locator(OpenCard)).ToHaveCountAsync(0)` (`BackButtonTests.cs`), `Expect(...).ToHaveAttributeAsync(..., new() { Timeout = FollowTimeoutMs })` (`LiveLookTests.cs`). Gaps found: `Page.Url.Should().Be(...)` is a one-shot read right after `GoBackAsync()` in `BackButtonTests.cs` (lines 27, 31, 43, 47, 58, 62, 73, 77, 109, 114, 132) and `SheetDragTests.cs` (67, 71, 209, 213); use `await Expect(Page).ToHaveURLAsync(...)`. No default expect timeout is set anywhere (`SetDefaultExpectTimeout` has no hits), so every retrying assertion has Playwright's 5 s default; set a larger default once in `CabinetPageTest` (retrying assertions cost nothing on pass).
- **Settled markers instead of fixed holds:** `Expect(Page.Locator("html[data-pull-kind]")).ToHaveCountAsync(0)` waits until the open animation is over (used in `BackButtonTests.OpenCardAfterBlankPageAsync` and `CardSyncTests`); `WaitUntilTheCardStepIsGoneAsync()` waits until the history step is removed. Apply the same marker before any close in `SheetDragTests`/`CardOpenTests`. Fixed `Task.Delay` holds remain in `CardSyncTests` (`HoldPeriod` of 3 s, negative assertions) and `SheetDragTests` (250/300 ms); replace "nothing changes during X seconds" with waiting for a positive event (for example the page hearing the push) and then asserting no second effect.
- **Wait for the live connection before publishing:** `LiveLookWebKitTests` installs a spy (`window.__live.heard`) and `WaitUntilThePageHearsAPushAsync` republishes the current status until the page heard one, bounded; promote it into `CabinetPageTest` and use it in `LiveLookTests` and `CardSyncTests`.
- **Settle layout before measuring:** the two-frame stability loop already added for `FilterShowOnShelfTests`; the `DefaultLookIdentityTests` sample-400 first draw has `FirstDrawTimeoutMs = 30000` (`LiveLookTests.cs:32`), so a failure there is more likely the slow first layout; reuse the same constant and a retrying wait.
- **Reproduce first:** run the failing classes repeatedly under throttling (Chromium DevTools protocol `Emulation.setCPUThrottlingRate` behind an environment variable in the test base, or `systemd-run --user --scope -p CPUQuota=100%` on the desktop), record failure counts before and after, and keep the DejaVu fontconfig for geometry checks. Run browser slices solo.

## Security Domain

Security enforcement is enabled; ASVS level 2; block on high (`.planning/config.json`).

### Applicable ASVS Categories

| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture, design and threat modelling | yes | Two-layer limits, three-wall owner design, loopback ops, default-drop firewall; threat table below |
| V2 Authentication | no | The site has no user authentication; the owner tools are guarded by network location, unchanged |
| V3 Session management | no | No sessions; the only cookie is the owner-port antiforgery cookie (`OwnerWallTests` asserts no other cookie) |
| V4 Access control | yes | Owner router allow-list kept permanently, owner port firewall rule, in-app owner wall, ops on loopback; external probe |
| V5 Input validation | yes | Host header (`AllowedHosts`), `Origin` check on the live route, body size limits, existing bodyless public endpoints |
| V7 Error handling and logging | yes | Quiet 429s, no per-request logging of refusals, `Server` header off, no stack traces (Production) |
| V9 Communications | yes | TLS at Traefik, HSTS, HTTP to HTTPS redirect, scanner grades |
| V12 Files and resources | yes | Owner upload limits retained under a smaller global body limit; picture downloads allow-listed host only |
| V13 API and web service | yes | Per-client rate limits, single-flight sync with shared window, no CORS |
| V14 Configuration | yes | Security headers, host filtering, resource limits, egress block, unnecessary features off, startup validation of limit settings |

### Known Threat Patterns for this stack

| Pattern | STRIDE | Severity | Standard Mitigation |
|---------|--------|----------|---------------------|
| Request flood exhausting the single-core proxy or the cabinet container and starving neighbours | DoS | high | Traefik `rateLimit` + `inFlightReq`, Kestrel limits, unit and container caps, measured pass bar (D-09) |
| Owner tools exposed when the allow-list is removed (wrong router edited, middleware removed from both) | Elevation / Information disclosure | high | Edit the public router only with a backup, keep owner router allow-list, render test pins it, external probe from mobile data, selfcheck owner-wall checks |
| Client address spoofing through `X-Forwarded-For`, defeating per-address limits | Spoofing | high if misconfigured | Known-proxy-only forwarded headers with `ForwardLimit = 1` (exists), no `notAppendXForwardedFor`, no client networks in `trustedIPs`, forged-header test on the untrusted-peer host |
| All internet clients sharing one bucket (wrong `ipStrategy`, in-flight default criterion) | DoS (self-inflicted) | high | Remote-address strategy, explicit criterion on both middlewares, test with two source addresses |
| Cross-site WebSocket hijacking and cap exhaustion by a third-party page | Tampering / DoS | low to medium | Origin guard, per-address concurrency cap, site-wide cap; accepted risk AR-03-03 closed |
| Cross-site POST to the sync endpoint | Tampering | low | Shared persisted window, single flight, per-address limiter, no body read; AR-03-02 closed |
| Host header injection and DNS rebinding | Spoofing | low | `AllowedHosts` from the env file, router `Host` rules |
| SSRF to the home network through the owner link check or any outbound fetch | Information disclosure | medium | nftables output block with resolver exception, image host allow-list in settings |
| Clickjacking, MIME sniffing, referrer leakage, device-API abuse | Tampering / Information disclosure | low | `frame-ancestors 'none'` + `X-Frame-Options`, nosniff, Referrer-Policy, Permissions-Policy, COOP/CORP |
| Slow-request attacks (slowloris) | DoS | medium | `RequestHeadersTimeout`, default minimum data rates, Traefik read timeout default 60 s `[CITED: Traefik entry points docs]` |
| Long-lived maintenance access to both containers | Elevation | high (accepted) | AR-08 and AR-06-05 re-accepted with passphrase-protected keys loaded only when needed, `from=` pinning verified, new review point; checklist lines |
| Supply chain of the temporary flood tool on a shared host | Tampering | low | Archive-signed package, installed with the owner's go, removed afterwards |
| Irreversible HSTS preload on a sub-domain host | Denial (self) | low to medium | Drop `stsPreload`, keep `stsSeconds` |
| Personal data in the public repository or phase record | Information disclosure | high | Placeholders, `build/lint.sh`, `build/scan-history.sh`, record grades and numbers only |
| One-way go-public (cached by anyone) | Information disclosure | medium | Checklist sign-off, owner's explicit "go", documented re-close |

## Release flow for v1.0.0

`[VERIFIED: docs/releasing.md, build/package-release.sh, build/validate-release-tag.sh, git tag]` The version comes from the tag: `package-release.sh --version X.Y.Z` passes `-p:Version="$VERSION"`; there is no version number to edit in a project file. The latest tag is `v0.9.0`. Steps and facts the plan must cover:
- A tag must be strict `vMAJOR.MINOR.PATCH`, its commit must be on `main`, and it must equal the triggering commit; the draft needs the owner's `deploy` environment approval; the release runs unit and integration tests only (browser tests gate the pull request).
- `docs/releasing.md` already states the 0.x rule ("Versions stay in the `0.x` range until the site is opened to the public, and the first public release is `1.0.0`") and its examples use `v0.2.0`; no doc rewrite is required for the bump, but the go-public document should link to it.
- Releases are immutable: a defect found after `v1.0.0` is fixed by `v1.0.1`.
- The server pulls it within about ten minutes (`cabinet-deploy poll`), then provisioning must be re-run from the active release for the unit and firewall changes; env keys and `provision.conf` keys go in first (Pitfall 4).
- Tell the owner explicitly when the draft has been checked (`build/verify-published-release.sh vX.Y.Z`) before they approve, per the owner's standing request.
- Sequence: milestone-branch PRs (code, templates, docs) → a `0.x` pre-release for measurement with runtime overrides → final numbers into the repo → `v1.0.0` → verification on the LAN-only route (headers, burst, caps, flood re-run, browser suite) → checklist and BGG summary → owner "go" → router edit and external probe.

## Sources

### Primary (HIGH confidence)
- Repository files read this session: `08-CONTEXT.md`, `Cabinet.Service/Program.cs`, `Hosting/ContentSecurityPolicy.cs`, `Hosting/ForwardedHeadersSetup.cs`, `Hosting/OpsEndpoint.cs`, `Live/*.cs`, `Sync/SyncEndpoints.cs`, `Sync/SyncCoordinator.cs`, `Sync/ArtSync.cs`, `Owner/OwnerAccess.cs`, `Owner/OwnerEndpoints.cs`, `appsettings*.json`, `wwwroot/js/live.js` and `sync.js`, `Cabinet.Repository/Images/ArtCache.cs`, `deploy/**`, `build/check-owner-routes.sh`, `.gitleaks.toml`, `build/lint/checks/10-repo-rules.sh`, the integration and browser test infrastructure, `docs/lxc-setup.md`, `docs/deploy.md`, `docs/releasing.md`, both folded todos, the research documents' relevant sections.
- Live probes this session (read-only unless stated): cabinet host cgroup v2 layout, lxcfs views, runtime and package versions, `memory.stat`/`memory.events`, nftables ruleset, resolver, state directory; a transient `systemd-run` unit that showed the CPU, memory, swap and task limits work; proxy host version, entry-point config, dynamic file (hostnames masked), access-log source-address classification (counts only).
- Traefik docs and source: rateLimit, inFlightReq, ipAllowList, headers, entry points, serversTransport pages on doc.traefik.io; `rate_limiter.go` at tag v3.5.4; `inflight_req.go` and `forwarded_header.go` on master.
- Microsoft Learn: rate limiting middleware (updated 2026-10-09), host filtering, Kestrel options, GC configuration, workstation vs server GC. dotnet/aspnetcore source: `RateLimitingMiddleware.cs`, `ConnectionEndpointRouteBuilderExtensions.cs`, `HostFilteringMiddleware.cs`. dotnet/runtime `src/coreclr/gc/unix/cgroup.cpp`.
- Throwaway-namespace test of the egress ruleset (Docker, Alpine nftables 1.0.9).
- BGG archive captures: `using_the_xml_api` 2026-09-24, `XML_API_Terms_of_Use` 2026-10-02.
- hstspreload.org submission requirements; Proxmox `pct(1)`.

### Secondary (MEDIUM confidence)
- MDN: Permissions-Policy, Cross-Origin-Resource-Policy, Origin (WebSocket detail from general knowledge of RFC 6455).
- SSL Labs API v4 and MDN HTTP Observatory v2 via search excerpts.
- oha README (flags), hey package metadata.

### Tertiary (LOW confidence)
- `hey` flag names from memory; per-connection memory expectation; behaviour of Traefik `inFlightReq` with streaming requests; whether `pct set` applies live.

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, nothing new is installed and every version was read from the hosts or csproj files.
- Architecture: HIGH for the pipeline, limiter semantics and Traefik behaviour (source read); MEDIUM for the origin guard design and the in-flight/SSE coupling.
- Pitfalls: HIGH for items verified in code or on the hosts; MEDIUM for flood-time behaviour, which only the measurements can settle.
- Numbers: MEDIUM-LOW by design; every figure is a placeholder for a measurement.

**Research date:** 2026-10-20
**Valid until:** 2026-11-19 for the code and tooling facts; re-read the BGG pages on go-live day regardless.

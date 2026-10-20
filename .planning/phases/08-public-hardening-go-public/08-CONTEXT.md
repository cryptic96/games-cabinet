# Phase 8: Public Hardening & Go-Public - Context

**Gathered:** 2026-10-20
**Status:** Ready for planning

<domain>
## Phase Boundary

The cabinet becomes reachable from the public internet over HTTPS through the existing Traefik, and only after it is hardened:

- per-client rate limits in Traefik and in the app, with a much stricter limit on "sync now" and on the live channel;
- a full set of security headers on every response, with the cabinet working under the strict CSP with no violations;
- CPU and memory caps on the container and on the app's unit, sized from measured picture-rebuild, sync and flood load;
- an outbound firewall that keeps the public container off the home network;
- a go-public checklist, signed off by the owner, after which the LAN/VPN allow-list comes off the public router and an external probe confirms the result.

Health, ops and every owner tool stay unreachable from the internet (the owner router keeps its allow-list permanently).

Most owner prerequisites are already in place: the public hostname already has a certificate through the existing resolver, and HTTPS already reaches Traefik from the internet. Opening the site is therefore removing the `cabinet-lan-only` middleware from the public router on the Traefik container, nothing else.

Not in this phase: new visitor features, search indexing, a styled refusal page, a full hostname-based egress allow-list, the real screen-reader check.

</domain>

<decisions>
## Implementation Decisions

### Rate limits and the live channel
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

### Resource caps and load measurement
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

### Go-public day
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

### Access and egress
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

### Folded Todos
- **Size the live-connection and sync-press limits for go-public** (`.planning/todos/pending/2026-10-07-size-live-and-sync-limits-for-go-public.md`).
  - Its asks are now D-01, D-04 and D-08: caps sized from measured load, the live origin allow-list, per-client limiters on sync and negotiate behind the forwarded-headers setup, a Traefik limit in front of both, tests for each limit, and revisiting AR-03-02 and AR-03-03.
  - Per D-05, the Traefik middleware applies to the public router only.
- **Harden the browser tests that flake on the CI runner** (`.planning/todos/pending/2026-10-17-harden-browser-tests-that-flake-on-ci.md`).
  - Goal: a clean, trustworthy CI before `v1.0.0`.
  - Still open: card dialogs that did not close in time, `BackButtonTests`, `SheetDragTests`, `CardSyncTests`, the sample-400 first draw in `DefaultLookIdentityTests`, and `LiveLookTests`.
  - Approach from the todo: retrying assertions on the end state, waiting for the live connection to be up before publishing, settling layout before measuring, and recording failure rates before and after.
  - The two tests already hardened during the filters phase stay as they are.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Phase scope and requirements
- `.planning/ROADMAP.md` § "Phase 8: Public Hardening & Go-Public": goal, owner prerequisites (re-read BGG terms), five success criteria.
- `.planning/REQUIREMENTS.md`: SEC-01, SEC-02, SEC-03, SEC-04, SEC-06 (SEC-05 already complete).
- `.planning/PROJECT.md`: Active items (allow-list removal, maintenance access), Constraints, Key Decisions (app-owned CSP, owner tools on their own port and hostname, own-origin art).
- `.claude/CLAUDE.md` § Hard rules: no planning references outside `.planning/`, `///`-only comments, no personal data anywhere (no hostnames, addresses or ranges in the repo), branching, BGG API etiquette.

### Research
- `.planning/research/ARCHITECTURE.md` §6 "Deployment Architecture", especially "What changes for public exposure": Traefik `rateLimit` sizing ideas, headers, Kestrel limits, `AllowedHosts`.
- `.planning/research/PITFALLS.md` Pitfall 11 "Treating a public homelab exposure like the LAN-only ing-dashboard": rate limits keyed on the right address, router/TLS slips, low-power host DoS, the go-public checklist items.

### Earlier decisions this phase builds on
- `.planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-CONTEXT.md`: D-08 (starting size, caps tuned here), D-09 (LAN-only route; go-public = remove the allow-list), D-10 (firewall, loopback ops), D-13 (`v1.0.0` at go-public), deferred egress allow-list.
- `.planning/phases/01-repo-guardrails-walking-skeleton-deploy/01-SECURITY.md`: AR-08 (development admin account).
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-CONTEXT.md`: D-05/D-21 (shared, persisted 10-minute window), D-09 (SignalR live channel constraints and CSP).
- `.planning/phases/03-bgg-access-spike-real-sync-snapshot/03-SECURITY.md`: AR-03-02 (no per-client sync limiter), AR-03-03 (no hub origin allow-list).
- `.planning/phases/06-owner-tools-persistence/06-CONTEXT.md`: D-01 to D-04 (owner port and hostname, the three walls, the owner router keeps the allow-list permanently).
- `.planning/phases/06-owner-tools-persistence/06-SECURITY.md`: AR-06-05 (proxy maintenance key), T-06-02-02 (owner tools at go-public).
- `.planning/todos/completed/2026-10-06-turn-off-prototype-mode-and-add-noindex-before-go-public.md`: noindex shipped; reopen only if indexing is allowed.

### Folded todos
- `.planning/todos/pending/2026-10-07-size-live-and-sync-limits-for-go-public.md`
- `.planning/todos/pending/2026-10-17-harden-browser-tests-that-flake-on-ci.md`

### Deploy and docs
- `deploy/traefik/cabinet.yml.example`: public and owner routers, `cabinet-lan-only`, `cabinet-security-headers`.
- `deploy/nftables/cabinet.nft.in` and `deploy/provision.d/50-firewall.sh`: default-drop input; the output chain is `accept` today (D-15 changes it).
- `deploy/provision.conf.example`: `CABINET_TRAEFIK_IP`, `CABINET_ADMIN_SSH_SOURCES`; new egress ranges go here as placeholders.
- `deploy/systemd/cabinet.service`: unit hardening; gains the D-07 limits.
- `deploy/cabinet.env.example`: placeholders for the public and owner hostnames (origin allow-list, host filtering).
- `deploy/bin/cabinet-selfcheck`: natural home for checking unit limits and the egress rule on the container.
- `deploy/tests/`: render-template, provision-logic and sandboxing tests that must follow template changes.
- `docs/lxc-setup.md` §6 "Install the Traefik route" and §7a "The owner host": where the cap command and the go-public step are documented.
- `docs/deploy.md`: release and owner-route check flow.
- `build/check-owner-routes.sh`: pattern for a scripted route check.

### BGG terms (re-read before go-live)
- https://boardgamegeek.com/using_the_xml_api (archive: https://web.archive.org/web/20260924202844/https://boardgamegeek.com/using_the_xml_api)
- https://boardgamegeek.com/wiki/page/XML_API_Terms_of_Use

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- **`Cabinet.Service/Hosting/ContentSecurityPolicy.cs`** and **`Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs`**: the one-middleware, one-test pattern to grow into the full security-header set (D-06). It runs first in the pipeline, so every response carries it.
- **`Cabinet.Service/Hosting/ForwardedHeadersSetup.cs`**: the trusted client address (known proxy only, `ForwardLimit = 1`), which partitions the app's limiters (D-01, D-04).
- **`Cabinet.Service/Live/LiveEndpoints.cs`, `LiveConnectionLimiter.cs`, `CabinetHub.cs`**:
  - the site-wide cap with settings validation at startup;
  - `MaxConcurrentUpgradedConnections` and small message and buffer sizes;
  - the place for the per-address cap and origin check.
- **`Cabinet.Service/Sync/SyncEndpoints.cs`**: `POST /cabinet/sync` already answers 429 with `Retry-After` inside the shared window. The per-client limiter reuses that response so the page shows the existing message (D-03).
- **`Cabinet.Service/Hosting/OpsEndpoint.cs`**: health bound to the loopback ops port, validated at startup; unchanged and probed from outside.
- **Browser tests that listen for CSP violations** (`LightsTests`, `FilterAccessTests`, `FilterDockMemoryTests`): evidence for "works fully under the policy".
- **`build/check-owner-routes.sh`**: a route check driven by host environment variables; a model for scripting parts of the probe.

### Established Patterns
- Settings are read and validated at startup with messages naming the key (`LiveSettings`, `ImageSettings`). New limit settings follow suit, so a typo in the env file stops the app instead of silently weakening a limit.
- Personal values (addresses, ranges, hostnames) live only in the server env file or `provision.conf`. Templates and examples use RFC 5737 addresses and `example.com`.
- Templates rendered by provisioning have logic tests under `deploy/tests/`.
- No planning references, `///`-only C# comments and `/** */`-only JS comments in anything outside `.planning/`, including the new `docs/` checklist.
- Integration tests boot the real pipeline in-process (`CabinetWebApplicationFactory`), with an untrusted-peer host for forwarded-header tests.
- Browser tests run in their own CI job, and their hosts dispose file watchers (inotify limit). The CI font differs from local (run geometry checks with the DejaVu fontconfig).

### Integration Points
- **Pipeline order in `Cabinet.Service/Program.cs`:**
  - security headers stay first, then the connection peer and forwarded headers;
  - the rate limiter must sit after `UseForwardedHeaders` (it needs the real client address) and after `UseRouting`, if endpoint-specific policies are used.
- **`appsettings.json`:** `AllowedHosts` is `*` and `Live:MaxConnections` is 100. Production values come from the env file.
- **Outside the repository:** the Traefik dynamic file on the proxy container (Claude has maintenance SSH; back up before editing; the file provider applies edits on save), Proxmox container settings (owner only), and the provision config on the cabinet container.

</code_context>

<specifics>
## Specific Ideas

- The owner was unsure where the rate limit sits and what it is for. Docs and the checklist should explain it plainly: Traefik counts requests per internet address and refuses a flood cheaply before it reaches the container; the app adds stricter limits on just "sync now" and the live channel.
- "Room for a group" is the guiding image for every per-address number: several friends opening the link at once on one Wi-Fi must never see missing pictures.
- A flood also costs the shared Traefik container TLS handshakes before any rate limit applies. That is why the pass bar watches a neighbour site behind the same Traefik, not just the cabinet.
- On go-public day the owner wants to be told plainly when each gate has passed, in the same spirit as the explicit "draft check passed, you can approve deploy" message for releases.

</specifics>

<deferred>
## Deferred Ideas

- **Full hostname-based egress allow-list** (GitHub, Sigstore, BGG API, image host, package mirrors only). Rejected for now in favour of blocking the home network (D-15). Revisit if a resolver-fed set or an egress proxy becomes worth its upkeep.
- **Styled "slow down" page for refused visitors.** Rejected for quiet handling (D-03).
- **Search indexing.** Kept off at go-public (D-12); an owner decision for later.

### Reviewed Todos (not folded)
- **Screen-reader check of the skip link and games list** (`.planning/todos/pending/2026-10-12-screen-reader-check-of-games-list.md`): the owner still has no screen reader to test with; it stays deferred.
- The other keyword matches (selectable finishes, actionlint label entry, phase 4 test sharpening, uneven desktop sections, check link while the worker is busy, no-script Undo copy id, side-by-side location cabinets) matched only on generic words and are not public-hardening work.

</deferred>

---

*Phase: 08-public-hardening-go-public*
*Context gathered: 2026-10-20*

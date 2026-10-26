---
phase: 08
slug: public-hardening-go-public
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 2
created: 2026-10-26
---

# Phase 08 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Internet → reverse proxy (public router) | Anonymous visitors reach the public cabinet route; the owner router keeps its LAN/VPN allow-list | public page requests, live-channel connections, "sync now" presses |
| Reverse proxy → app | Only the known proxy's forwarded headers are trusted; the client address keys every per-visitor limit | client address, scheme, host |
| App container → home network | Outbound block refuses new connections to private, shared, link-local and IPv6 local ranges, except DNS to the resolver | none permitted (SSRF boundary) |
| App container → internet | BGG API (token only to the API host), the image CDN, release downloads and package mirrors | collection data, pictures, signed releases |
| Workstation → containers (maintenance SSH) | Key-only accounts pinned with `from=` to the workstation; port 22 reachable only from the admin sources on both containers | administrative commands (sudo) |
| Release pipeline → container | Attested, checksummed releases verified on the workstation and again by the server before install | release archive |
| Repository (public) | No personal data: hostnames, addresses, ranges and names stay on the servers and in chat | documentation, tests, records |

---

## Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-08-01-01 | Spoofing | Partition key from a forged `X-Forwarded-For` | high | mitigate | Key is `Connection.RemoteIpAddress` after the existing known-proxy-only forwarded-headers setup (`ForwardLimit = 1`); the forged-header case on the untrusted-peer host proves a non-proxy peer cannot move presses into another budget (Task 2) | closed |
| T-08-01-02 | Denial of service | Hammering `POST /cabinet/sync` | medium | mitigate | Per-client fixed window refuses before the handler runs; the shared single-flight window stays behind it; tests prove the boundary (Tasks 1 and 2) | closed |
| T-08-01-03 | Denial of service | Limiter registered before routing or forwarded headers, putting every client in one bucket | high | mitigate | `UseCabinetClientLimits` placed after `UseForwardedHeaders`, `UseRouting` and `UseOwnerOnlyRoutes`; acceptance checks the order; the separate-budget test proves two forwarded clients are counted apart | closed |
| T-08-01-04 | Denial of service | Many partitions from cycling IPv6 addresses | low | mitigate | IPv6 counted per /64 (unit and integration tests); the framework's partitioned limiter evicts idle partitions | closed |
| T-08-01-05 | Repudiation / log flooding | Per-refusal logging | low | mitigate | `OnRejected` logs nothing | closed |
| T-08-01-06 | Tampering (cross-site POST) | Cross-site form posts to the sync route | low | accept | The press reads no body and returns nothing secret; the per-client limit now bounds it; accepted risk AR-03-02 is closed in plan 08-05 together with the live-channel risk | closed (accepted, R-08-01/02) |
| T-08-02-01 | Information disclosure / Elevation | SSRF from the public container into the LAN | medium | mitigate | Output chain rejects new connections to the configured private, shared, link-local and LAN IPv6 ranges (Task 2); selfcheck proves a blocked network refuses at once (Task 3); applied and verified on the container in plan 08-12 | closed |
| T-08-02-02 | Denial of service | App exhausting container memory or CPU during a flood | high | mitigate | `MemoryHigh`/`MemoryMax`/`MemorySwapMax=0`/`CPUQuota`/`CPUWeight`/`TasksMax`/`LimitNOFILE` on the unit (Task 1); selfcheck compares effective values and checks the unit sits below the container (Task 1); sized and proven by floods in plans 08-15, 08-17 and 08-20 | closed |
| T-08-02-03 | Tampering | A malformed or injected provision.conf value producing a broken or open ruleset | medium | mitigate | Typed validators per token, the existing safe-value check, unreplaced-token refusal and `nft -c -f` before loading (Task 2 tests) | closed |
| T-08-02-04 | Denial of service (self) | A wrong resolver breaking DNS and with it sync, deploys and updates | medium | mitigate | `reject` instead of `drop` (visible failure), DNS exception first, `check_dns` in the selfcheck (Task 3), `pct enter` escape hatch documented; verification of sync, picture downloads, deploy poll, Sigstore and apt in plan 08-12 | closed |
| T-08-02-05 | Information disclosure | Real ranges or addresses committed in templates or tests | high | mitigate | Only whole private blocks and documentation prefixes; derived probe address at run time; `40-secrets.sh` gitleaks check in Task 2 acceptance | closed |
| T-08-03-01 | Information disclosure | Hostnames in the report or committed files | high | mitigate | Hostnames only from the environment; labels in every line; the test asserts neither hostname appears (Tasks 1 and 2) | closed |
| T-08-03-02 | Elevation of privilege | Owner tools or health reachable publicly going unnoticed | high | mitigate | Explicit 404 checks for `/health` and an owner path, `000` for ports 5080-5082, owner host 403 from outside (Task 2); used at go-public in plans 08-22 and 08-23 | closed |
| T-08-03-03 | Denial of service (self) | The burst tripping the home gateway's intrusion prevention | low | mitigate | Opt-in; 8 requests by default, counts above 20 refused, one connection for all requests; the default is pinned by a test (Task 2) | closed |
| T-08-04-01 | Repudiation (process) | Flaky checks teaching people to ignore red | medium | mitigate | Waits on end-to-end conditions, measured 10 solo runs under a one-core quota before and after, counts recorded (Tasks 1 and 2) | closed |
| T-08-04-02 | Tampering (test integrity) | A test made green by weakening its assertions | medium | mitigate | Only waits change; acceptance checks the assertions remain; the prohibition is reviewed at code review | closed |
| T-08-05-01 | Tampering / Denial of service | Cross-site WebSocket or SSE hijacking and cap exhaustion | medium | mitigate | Origin guard before the limiter, foreign and `null` origins 403 on negotiate, WebSocket and SSE, both ports (Task 2 tests) | closed |
| T-08-05-02 | Denial of service | One address holding many live connections | high | mitigate | Per-address concurrency on connect requests for both transports, refused before upgrade; site-wide cap behind it; `LiveAddressCapTests` on both transports (Task 1) | closed |
| T-08-05-03 | Denial of service | Rapid reconnect or negotiate churn | medium | mitigate | Per-address negotiate window with `Retry-After`; page backs off on its own schedule (Task 1 tests; `live.js` unchanged) | closed |
| T-08-05-04 | Denial of service (self) | Counting SSE pings as connects and refusing healthy fallback pages | medium | mitigate | Message posts mapped to no limiter; an SSE page outlives two pings under a window of one (Task 1 test) | closed |
| T-08-05-05 | Spoofing | Origin check comparing against a forged `Host` | low | mitigate | Host filtering from the env file (plan 08-07) restricts `Host`; behind Traefik only routed hostnames arrive | closed |
| T-08-06-01 | Denial of service | Request flood through the public router | high | mitigate | Per-address `rateLimit` and `inFlightReq` on the public router (Task 1); sized from measurements and proven by floods in plans 08-15 and 08-20 | closed (residual accepted, R-08-03) |
| T-08-06-02 | Denial of service (self-inflicted) | All internet clients sharing one bucket | high | mitigate | Explicit `sourceCriterion.ipStrategy` with `ipv6Subnet: 64` on both middlewares, no `depth`; asserted by the render test (Task 1) | closed |
| T-08-06-03 | Elevation / Information disclosure | Owner router weakened when the public route is opened | high | mitigate | Owner router keeps the allow-list and gets no new middleware; render test pins it (Task 1); the go switch edits the public router only (plan 08-22) | closed |
| T-08-06-04 | Denial of service (self) | HSTS preload locking the sub-domain into HTTPS-only lists | low | mitigate | Preload and sub-domain flags removed; asserted absent (Task 2) | closed |
| T-08-06-05 | Denial of service (self) | Host filtering without loopback rolling back every release | high | mitigate | Loopback entries in the env example with the reason; asserted by the render test (Task 2); host-filtering tests in plan 08-07; set before the release in plan 08-12 | closed |
| T-08-07-01 | Spoofing | Host header injection, DNS rebinding | low | mitigate | `AllowedHosts` from the env file; foreign host 400 on every listener (Task 1) | closed |
| T-08-07-02 | Denial of service (self) | Host filtering without loopback rolling back releases | high | mitigate | Test proves loopback health needs the loopback entry (Task 1); env example carries it (plan 08-06); set before the release in plan 08-12 | closed |
| T-08-07-03 | Denial of service | Slow-request attacks holding connections | medium | mitigate | `RequestHeadersTimeout` 10 s, default minimum data rates kept; raw-socket test (Task 2) | closed |
| T-08-07-04 | Denial of service | Connection exhaustion starving the ops health check | medium | mitigate | The ceiling applies per listener (one limit middleware per endpoint); a test holds the public listener at its ceiling and proves loopback health still answers (Task 2); health under flood checked in plans 08-15 and 08-20 | closed |
| T-08-07-05 | Denial of service (self) | Lowering keep-alive below the proxy's idle timeout causing 502s | medium | mitigate | Keep-alive left at its default and asserted 130 s (Task 2) | closed |
| T-08-07-06 | Information disclosure | Header-less early rejections (400 from host filtering, Kestrel's own 408/413/431) | low | accept | Empty-bodied framework responses before the pipeline; listed as exclusions in the header tests (plan 08-08) | closed (accepted, R-08-01/02) |
| T-08-08-01 | Tampering | Clickjacking | low | mitigate | `frame-ancestors 'none'` and `X-Frame-Options: DENY` on every response (Tasks 1 and 2) | closed |
| T-08-08-02 | Tampering | MIME sniffing of art or JSON | low | mitigate | `nosniff` exactly once on every response, including art (Task 2) | closed |
| T-08-08-03 | Information disclosure | Referrer leakage to BGG links | low | mitigate | `strict-origin-when-cross-origin` (Task 1) | closed |
| T-08-08-04 | Elevation of privilege | Device APIs or cross-origin windows abused | low | mitigate | Permissions-Policy off-list, COOP and CORP `same-origin` (Task 1); console-clean in two engines (Task 3) | closed |
| T-08-08-05 | Elevation of privilege | CSP loosened to make a change work | high | mitigate | CSP compared byte for byte with a literal; inline/eval/other-origin assertions kept; violation listeners in browser tests (Tasks 1 and 3) | closed |
| T-08-08-06 | Information disclosure | Early framework rejections without headers | low | accept | Empty-bodied, listed and tested as exclusions (Task 2) | closed (accepted, R-08-01/02) |
| T-08-09-01 | Repudiation (process) | Red checks ignored because they flake | medium | mitigate | Condition-based waits, measured 10 solo runs under a one-core quota before and after, slice-by-slice suite run (Tasks 1-3) | closed |
| T-08-09-02 | Tampering (test integrity) | Tests made green by weakening assertions | medium | mitigate | Only waits change; acceptance greps for the removed one-shot reads only; prohibition reviewed at code review | closed |
| T-08-10-01 | Information disclosure | Real hostnames, ranges or the proxy's real middleware name in docs | high | mitigate | Placeholders only; repo-rules and secrets checks in both tasks | closed |
| T-08-10-02 | Elevation of privilege (procedural) | A re-check or re-open that edits the owner router or forgets a backup | medium | mitigate | The checklist's opening steps name the public router only, the backup outside the watched folder, and the close-again step (Task 1) | closed |
| T-08-10-03 | Denial of service (self) | A release installed before its host settings exist, failing its health check | medium | mitigate | The order written down in `docs/deploy.md` and the checklist (Tasks 1 and 2) | closed |
| T-08-11-01 | Elevation of privilege | Merge into `main` and tag push | high | mitigate | Blocking owner gate after green checks; a check report alone never triggers Task 3 | closed |
| T-08-11-02 | Tampering | Release artifact | high | mitigate | Attested build, checksum and Sigstore bundle verified on the workstation before the owner is told the draft is verified (Task 3) | closed |
| T-08-11-03 | Information disclosure | Pull request text and SUMMARY | medium | mitigate | Plain user terms only; no planning references or personal data; identities reported only as "accepted"; `build/scan-history.sh` in the local gates | closed |
| T-08-11-04 | Denial of service (self) | Release installed before its host settings, failing its health check and rolling back | medium | mitigate | The owner hears the two server-side steps at the gate; plan 08-12 orders them before and after the deploy | closed |
| T-08-12-01 | Denial of service (self) | Host list without loopback rolling back the release | high | mitigate | Step A done and checked on the running version before the deploy approval (Task 2), foreign host 400 and loopback health 200 confirmed (Task 3) | closed |
| T-08-12-02 | Denial of service (self) | Outbound block breaking DNS, sync, deploys or updates | medium | mitigate | Selfcheck DNS check; apt update, poll, Sigstore verify, start-up sync and pictures proven after the reload (Task 3); `pct enter` escape hatch in the steps | closed |
| T-08-12-03 | Information disclosure | Homelab values copied into the repository | high | mitigate | Placeholders in the steps document; secrets check; SUMMARY holds PASS lines and counts only | closed |
| T-08-12-04 | Tampering | Release artifact | high | mitigate | Draft re-checked against recorded hashes; `verify-published-release.sh`; the server verifies before installing | closed |
| T-08-12-05 | Elevation of privilege | Claude changing the server beyond its mandate | medium | mitigate | Every state-changing command listed and individually approved at the checkpoint | closed |
| T-08-13-01 | Denial of service (self) | A typo dropping the whole cabinet dynamic file on save | medium | mitigate | Edit a copy outside the watched folder, parse-check and diff against the approved diff, atomic move, watch the log, restore from the backup on any failed check (Task 3) | closed |
| T-08-13-02 | Elevation / Information disclosure | Owner router or another site's middleware changed by mistake | high | mitigate | Only cabinet-only middlewares change; the owner approves the exact diff first (Task 2); the applied diff must equal it; owner router untouched; route check after (Task 3) | closed |
| T-08-13-03 | Information disclosure | Hostnames or file content in the record | medium | mitigate | Numbers only; the diff is shown to the owner in chat and never written to the repository; changed-line counts in the SUMMARY; secrets check (Tasks 1-3) | closed |
| T-08-13-04 | Denial of service (self) | Burst from the workstation tripping the gateway's protection | low | mitigate | Only the script's small single-connection burst; floods only from the proxy container (plan 08-15) | closed |
| T-08-14-01 | Tampering / data loss | Snapshot or owner data changed or deleted during the rebuild | high | mitigate | Only `art/` is moved aside; the four-line fingerprint (owner data and its versions byte-compared, snapshot and sync-state projections) is taken before and compared after, mechanically in the verify; any mismatch stops the plan and is reported to the orchestrator and owner with no automatic restore; `art/` is restored only when pictures stay missing; the backup is deleted only after verification (Task 3) | closed |
| T-08-14-02 | Denial of service (third party) | Hammering the image host | low | mitigate | The app's own one-second pace and per-run cap unchanged; one rebuild only, with the owner's go (Task 2) | closed |
| T-08-14-03 | Information disclosure | Collection details in the record | low | mitigate | Counts and numbers only; secrets check (Task 3) | closed |
| T-08-15-01 | Denial of service | Neighbour sites on the shared proxy during the flood | high | mitigate | Stepped load with the stop rule (abort the running step at once when the neighbour's median exceeds 2× baseline, record, step down); neighbour sampled throughout; verdict table per flood gates the decision in plan 08-16 (Task 3) | closed (residual accepted, R-08-03) |
| T-08-15-02 | Tampering (supply chain) | Flood tool on the shared proxy | low | mitigate | Archive-signed package installed only after the owner's go, removed and purged afterwards; verify checks it is gone (Task 3) | closed |
| T-08-15-03 | Denial of service (self) | Flood starving the deploy and SSH on the cabinet | high | mitigate | Unit limits below the container caps; SSH latency and loopback health sampled during the flood; recovery without restart checked (Task 3) | closed |
| T-08-15-04 | Denial of service (self) | Proxy counting all clients in one bucket | high | mitigate | Two-address test with the in-flight cap saturated (Task 3, step 3) | closed |
| T-08-15-05 | Information disclosure | Neighbour URL, addresses or hostnames in the record | medium | mitigate | Values only in shell variables; numbers only in the record; secrets check | closed |
| T-08-16-01 | Denial of service | Caps too loose, starving neighbours on the shared host | high | mitigate | Verdict gate: "approve as proposed" only when every row of every flood's verdict table is PASS, including the owner's host-CPU reading; otherwise reflood or approve with changes (Task 2); every changed value is re-flooded in plan 08-20 and must pass every row there | closed (residual accepted, R-08-03) |
| T-08-16-02 | Denial of service (self) | Per-address limits too tight, refusing a group on one Wi-Fi | medium | mitigate | D-02 arithmetic shown per value (Task 1) | closed |
| T-08-17-01 | Denial of service | A limit transcribed looser than decided | medium | mitigate | One grep per decided value recorded in the SUMMARY; settings tests bind the committed values (Tasks 1 and 2) | closed |
| T-08-17-02 | Tampering | A validator range widened to fit a value | low | mitigate | Ranges unchanged; out-of-range values stop the plan (Task 1) | closed |
| T-08-18-01 | Elevation of privilege | Merge and the one-way tag | high | mitigate | One-way decision checkpoint directly before the tag task; green checks alone never trigger it (Task 2) | closed |
| T-08-18-02 | Tampering | Release artifact | high | mitigate | Checksum, attestation and manifest verified before the owner is told (Task 3) | closed |
| T-08-18-03 | Information disclosure | Pull request text, SUMMARY, repository history | high | mitigate | Plain text only; `build/scan-history.sh` and `build/lint.sh` in the local gates (Task 1) | closed |
| T-08-19-01 | Tampering | Release artifact | high | mitigate | Draft re-checked against recorded hashes; `verify-published-release.sh`; the server verifies before installing (Tasks 1 and 3) | closed |
| T-08-19-02 | Denial of service (self) | A proxy file typo taking the cabinet routes down | medium | mitigate | Backup outside the watched folder, edited copy checked, atomic move, log watched, restore on failure (Task 3) | closed |
| T-08-19-03 | Elevation of privilege | Owner router changed during the proxy edit | high | mitigate | Only the cabinet's middlewares change; owner router compared before and after; route check (Task 3) | closed |
| T-08-19-04 | Denial of service | Decided limits not in force on the public version | high | mitigate | Provisioning re-run and effective values compared with the decided ones (Task 3) | closed |
| T-08-20-01 | Denial of service | Neighbour sites during the flood | high | mitigate | Stepped load up to the agreed largest step with the stop rule (abort at once above 2× the neighbour's baseline, record, step down), neighbour sampled throughout, verdict table gating go-public (Task 3) | closed (residual accepted, R-08-03) |
| T-08-20-02 | Denial of service (self) | Flood starving SSH and the deploy timer on the cabinet | high | mitigate | Shipped unit limits under container caps; SSH and loopback health sampled; recovery checked (Task 3) | closed |
| T-08-20-03 | Tampering (supply chain) | Temporary tool on the proxy | low | mitigate | Archive package with the owner's go, removed and verified gone (Task 3) | closed |
| T-08-21-01 | Elevation of privilege | Proxy SSH open to the whole LAN | medium | mitigate | Owner applies the supplied admin-address rule (Task 2); presence checked (Task 2 verification) | closed |
| T-08-21-02 | Elevation of privilege | Long-lived maintenance access (AR-08, AR-06-05) | high | mitigate → accept (owner) | Passphrase-protected keys loaded only when needed, `from=` pinning and a 90-day review point, recorded here (Task 3); verified by the owner's passphrase step and Claude's checks that need no passphrase (`ssh-keygen -y` refuses an empty passphrase, `ssh-add -l` lists neither key, batch-mode SSH fails, `from=` still present) in plan 08-24. The residual risk (anything on the workstation can use the keys while the agent is unlocked) is the accepted part, per D-14 | closed (owner-accepted risk, R-08-04) |
| T-08-21-03 | Repudiation (compliance) | Going public under changed BGG terms | medium | mitigate | Archive re-read, summary, owner confirmation from the live pages (Tasks 1 and 2) | closed |
| T-08-21-04 | Information disclosure | Real SSH rule, addresses or scanner output in the repository | high | mitigate | Placeholders in the review; real rule in chat only; secrets check (Tasks 1 and 3) | closed |
| T-08-22-01 | Elevation / Information disclosure | Owner tools exposed by editing the wrong router or both | high | mitigate | Diff shows exactly one removed line inside the public router; owner router compared before and after (Task 3); phone check from outside in plan 08-23 | closed |
| T-08-22-02 | Information disclosure | Going public without the owner's explicit decision | high | mitigate | One-way decision checkpoint directly before the switch (Task 2) | closed |
| T-08-22-03 | Denial of service (self) | A half-written watched file dropping the routes | medium | mitigate | Copy outside the watched folder, atomic move, log watched, restore path (Task 3) | closed |
| T-08-22-04 | Information disclosure | Hostname or scanner output in the repository | medium | mitigate | Scanners only with the owner's yes; SSL Labs results kept off its public boards; grades and test names only; no screenshots; secrets check (Task 3) | closed |
| T-08-22-05 | Denial of service | Flood once public | high | mitigate | Outer wall, app limits, Kestrel and unit limits, container caps all verified on `v1.0.0` (plans 08-19, 08-20) | closed (residual accepted, R-08-03) |
| T-08-23-01 | Elevation / Information disclosure | Health, ops or owner tools reachable from outside | high | mitigate | Phone probe from mobile data with expected failures (Task 2); on any unexpected answer the site is closed again at once from the backup, then reported (Task 3) | closed |
| T-08-23-02 | Spoofing (degraded limits) | Port forward hiding the real client address, putting all visitors in one bucket | high | mitigate | Checked before the switch on the proxy's other public sites (plan 08-22); confirmed by the phone's request here; a non-public address closes the site again at once (Task 3) | closed |
| T-08-23-03 | Elevation of privilege | Maintenance keys still unlocked after go-public | high | mitigate → accept (owner) | Locked in plan 08-24 by the owner's passphrase step and verified there by Claude's checks that need no passphrase (`ssh-keygen -y` refuses an empty passphrase, `ssh-add -l` lists neither key, batch-mode SSH to both containers fails, `from=` pinning present); the residual risk while the agent is unlocked is the accepted part, per D-14 | closed (owner-accepted risk, R-08-04) |
| T-08-23-04 | Information disclosure | Phone address or hostnames in the record | medium | mitigate | Class counts and outcomes only; secrets check (Task 3) | closed |
| T-08-24-01 | Elevation of privilege | Maintenance keys usable by anything on the workstation | high | mitigate → accept (owner) | Passphrases set by the owner and the agent locked (Task 2); verified without a passphrase by three checks (Task 3); `from=` pinning confirmed (Task 1). The residual risk while the owner unlocks the agent for maintenance is accepted, per D-14 | closed (owner-accepted risk, R-08-04) |
| T-08-24-02 | Information disclosure | Key paths, fingerprints or authorized-key lines in the record | medium | mitigate | Yes or no per check only; secrets check (Task 1) | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

Evidence per threat (file and line, or record section) is in the audit of 2026-10-26 summarised below; the code mitigations sit at the right boundaries (ASVS L2): host filtering, then the security headers first in the app pipeline, the forwarded-headers setup before the per-visitor limiter, the origin guard before the limiter, and the owner router keeping its allow-list.

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| R-08-01 | T-08-01-06 | Cross-site form posts to the "sync now" route: the press reads no body and returns nothing secret; it is bounded by the per-visitor press limit (5 per 60 s), the shared cooldown and the proxy's rate limit. Same residual as AR-03-02 (`03-SECURITY.md`) and review note IN-04. | owner (accepted at planning) | 2026-10-24 |
| R-08-02 | T-08-07-06, T-08-08-06 | Early framework rejections (host-filtering 400, Kestrel's own 408/413/431) carry no security headers: they are empty-bodied, happen before the app pipeline, and are listed and tested as exclusions. | owner (accepted at planning) | 2026-10-24 |
| R-08-03 | T-08-06-01, T-08-15-01, T-08-16-01, T-08-20-01, T-08-22-05 | A single-address flood through the public router roughly doubles a neighbour site's response time while it lasts, because the shared reverse proxy's single core spends its CPU refusing requests (about 5,400 req/s refused); the neighbour keeps answering and recovers by itself, the cabinet's caps held, the host CPU never reached 100 % (owner's reading). Accepted under the amended SEC-06 definition (`08-SIZING-REVIEW.md`, "SEC-06 amendment after the verification flood"); a second core for the proxy container stays an owner option. Owner: "Accept with reason". | owner | 2026-10-26 |
| R-08-04 | T-08-21-02, T-08-23-03, T-08-24-01 | The two maintenance keys stay without a passphrase: `from=` pinning to the workstation (re-verified 2026-10-26 on both containers) and SSH reachable only from the admin sources on both containers remain; residual risk: anything running on the workstation can use the keys, with admin rights on the cabinet and on the reverse proxy. Recorded in AR-08 (`01-SECURITY.md`) and AR-06-05 (`06-SECURITY.md`); review point 2027-01-24. Owner: "I think, since you can only access it from within my local network, i'll accept these access tokens." | owner | 2026-10-26 |

*Accepted risks do not resurface in future audit runs.*

---

## Observations (non-blocking)

- An empty `ReverseProxy:KnownProxies` in Production is accepted silently; every visitor would then share one limiter partition and the origin guard would refuse live connections. The live host is configured correctly. A startup warning or refusal would harden T-08-01-01 and T-08-01-03 against a misconfigured env file (review note IN-03; follow-up todo).
- The selfcheck compares six unit limits; the file-descriptor limit is read from `systemctl show` only (records corrected).
- The reverse proxy's SSH still accepts password logins; it is reachable only from the admin sources (known item).

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-26 | 93 | 93 | 0 | gsd-security-auditor (ASVS L2, block on high): 84 mitigated and verified, 3 accepted at planning, 3 changed to accepted by the owner, 3 with an owner-accepted residual (plus 2 counted as mitigated carrying the same residual) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-26

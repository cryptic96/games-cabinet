---
phase: 08-public-hardening-go-public
plan: 19
subsystem: deploy
tags: [release, v1.0.0, provisioning, resource-limits, websockets, route-check]
requires:
  - phase: 08-public-hardening-go-public
    provides: "verified v1.0.0 draft (plan 08-18), decided values (08-SIZING-REVIEW.md)"
provides:
  - "v1.0.0 published by the owner and running healthy on the container"
  - "decided unit limits and the .NET heap limit in force; the runtime now sees 384 MB; selfcheck 39 of 39"
  - "proxy file already equal to the decided values; owner router unchanged"
  - "LAN-only route check with burst passes every line; live updates connect over WebSockets through the proxy with no 403 and no CSP violation"
affects: [08-20]
key-files:
  created: []
  modified: []
key-decisions:
  - "No proxy edit and no fresh backup: the live file already carried the decided numbers, the newest backup (taken before the flood) is byte-identical to it, so the prepared diff was empty"
status: complete
completed: 2026-10-26
actuals:
  tokens: 9000
  tasks: 3
  commits: 1
---

# Phase 8 Plan 19: v1.0.0 deployed, provisioned and proven on the LAN-only route Summary

v1.0.0 is published, installed on the container and healthy; the decided unit limits and the 384 MiB heap limit are in force (the runtime now reports 384 MB, fixing the earlier finding), the selfcheck passes 39 of 39, the proxy already carries the decided numbers, and the route check with its burst plus a real-browser check pass, including live updates over WebSockets through the proxy.

## Task results

| Task | Result | Commit |
|------|--------|--------|
| 1 Tracer: draft unchanged, owner told | Draft had the three recorded assets and the recorded checksum (`332a97b3...535a`); the proxy diff was empty, owner told | none |
| 2 Owner decision | Owner approved the deploy environment; `v1.0.0` published 2026-10-26T12:10:00Z. Answer, verbatim: "Provisioning + burst + poll start (Recommended)". Allowed: the poll start if not installed after 15 minutes, the provisioning re-run, the burst route check. The proxy edit was not needed | none |
| 3 Runs with the decided limits, route proven | Done; no deviation from the allowed commands | this summary |

## Evidence (sanitised)

Published release and install:
- `gh release view v1.0.0`: `isDraft` false, published 2026-10-26T12:10:00Z.
- The container installed it on its own at 12:17:51 UTC (the poll timer, within eight minutes of publication); the poll start (command 1) was not needed.
- `build/verify-published-release.sh v1.0.0` ends `all checks passed for v1.0.0`: three assets, checksum matches, attestation verified for source digest `909ef7304f87134fe4ca7d1aed2b2e2b08815f69`, that commit is on `main`, the manifest names version 1.0.0 and the commit, a one-byte-modified copy is refused.
- Health on loopback: `Healthy`, version `1.0.0`, commit `909ef7304f87134fe4ca7d1aed2b2e2b08815f69`, `missingArt` 0.

Provisioning (command 2, allowed):
- `sudo /opt/cabinet/current/deploy/provision.sh` ran to "Provisioning complete" (exit 0) and restarted the app once (12:18:26 UTC, `NRestarts` 0).
- `systemctl show cabinet`: `MemoryHigh` 503316480 (480 MiB), `MemoryMax` 629145600 (600 MiB), `MemorySwapMax` 0, `CPUQuotaPerSecUSec` 800ms, `CPUWeight` 50, `TasksMax` 512, `LimitNOFILE` 4096; environment carries `DOTNET_GCHeapHardLimit=0x18000000` (384 MiB).
- The limit directives of the installed unit equal, line for line, those in `deploy/systemd/cabinet.service` at tag `v1.0.0` (`MemoryHigh=480M`, `MemoryMax=600M`, `MemorySwapMax=0`, `CPUQuota=80%`, `CPUWeight=50`, `TasksMax=512`, `LimitNOFILE=4096`), and these are the decided values.
- Runtime line in the journal. v0.11.0 start: `The runtime sees 15594 MB of memory and 1 processors; server GC: False`. v1.0.0 start before provisioning (same line): 15594 MB. After provisioning: **`The runtime sees 384 MB of memory and 1 processors; server GC: False`**. This resolves the finding recorded in plan 08-12: the explicit heap hard limit is what the runtime now sees (it equals the heap limit rather than 75 % of `MemoryMax`).
- `sudo -n cabinet-selfcheck`: **39 passed, 0 failed**. It includes the review-fix checks, all PASS: the app refuses a request for a host that is not listed; the firewall output chain rejects new connections to the blocked IPv4 and IPv6 ranges; a connection to the first blocked network was counted by the reject rule; "this host has no global IPv6 address, so there is no IPv6 prefix to cover"; the six limit checks in force as the unit file sets them; the container shows 1024 MiB memory, 512 MiB swap and 1 core; MemoryMax leaves at least 128 MiB to the host; the CPU quota stays below the container's core; health reports 1.0.0; installed scripts, libraries and units match the active release; image smoke in the production sandbox `PASS image-smoke 480x360 webp 45030 bytes`.

Proxy (read-only; command 3 was not needed):
- The real file's hash (sha256 `10cb3100bb2b...`) is unchanged; the newest backup (`cabinet.yml.bak-<UTC stamp>-flood`, outside the watched folder) is byte-identical to the live file, so the diff holds no line at all, and so no fresh backup was taken (that would have been a state change on the proxy host the owner did not allow).
- Live values equal the `proxy-*` lines of "Decided values", compared with the verify's own extraction: `cabinet-rate-limit` average 60 / 60, period `1s` / `1s`, burst 800 / 800; `cabinet-in-flight` amount 200 / 200; `ipv6Subnet: 64` on both middlewares.
- The owner router block hashes identically in the backup and the live file (`cb55dc3cb9bb`): owner router unchanged: yes. The public router carries `cabinet-security-headers`, the allow-list middleware (the one the example calls `cabinet-lan-only`), `cabinet-rate-limit`, `cabinet-in-flight`; the owner router carries only the LAN/VPN allow-list and the headers middleware.

Route check, `build/check-public-routes.sh --from-lan --burst` (exit 0, 31 PASS lines, no FAIL):
- Home page 200; Strict-Transport-Security, Content-Security-Policy, X-Content-Type-Options, Referrer-Policy, X-Frame-Options, Permissions-Policy, Cross-Origin-Opener-Policy and Cross-Origin-Resource-Policy each exactly once with the expected value, no Server header, on both the home page and the static file (200); plain HTTP redirects to HTTPS (301); public health page 404; public owner page 404; ports 5080, 5081 and 5082 answer nothing on the public hostname; noindex robots meta tag present; robots.txt absent; linked BoardGameGeek credit present; no `/owner` reference; the owner hostname answers 200 from the home network.
- Burst of 8 presses through one connection: **202 x1, 409 x4, 429 x3**, refused by the app's own limit with a `Retry-After` header and a body without a status object. The one accepted press started a real sync (a cooldown-guarded one, as allowed): it finished at 12:19:13 UTC with game details 0 calls, 0 games described, box pictures 0 stored and 0 failed; health stayed `Healthy`, `missingArt` 0, `NRestarts` 0, resident memory about 83 MiB afterwards.

Real-route browser check (throwaway .NET file-based Playwright tool, Chromium, in the session scratchpad, nothing added to the repository), opening the public route on the LAN at both sizes with listeners on console messages, page errors, `securitypolicyviolation`, WebSockets and responses:

| Size | CSP violations | Console messages | Cabinet placements drawn | Credit visible | WebSockets | Live route responses | 403 responses |
|------|----------------|------------------|--------------------------|----------------|------------|----------------------|---------------|
| 1440x900 | 0 | 0 | 72 | yes | 1 (`wss://<host>/cabinet/live?id=...`) | `POST 200 /cabinet/live/negotiate` | 0 |
| 390x800 | 0 | 0 | 67 | yes | 1 (`wss://<host>/cabinet/live?id=...`) | `POST 200 /cabinet/live/negotiate` | 0 |

The live connection's transport through the proxy is WebSockets over `wss`: the forwarded-scheme origin-guard fix shipped in v1.0.0 works on the real route, with no 403.

## Deviations from Plan

### Auto-fixed Issues

None.

### Notes

**1. [Note] No proxy backup and no edit taken in this task.** The plan asked for a fresh backup "even if no value changes" so the verify can diff the newest backup against the live file. The owner's allowed command list did not include a proxy change, and the newest existing backup is byte-identical to the live file, so the verify's diff check (only `average`, `burst`, `period`, `amount` value lines) holds trivially and the value comparison was run directly on the live file.

**2. [Note] The runtime figure is the heap limit, not 75 % of `MemoryMax`.** The plan expected "about 75 % of the decided `MemoryMax`" (450 MB); with the explicit hard heap limit in the unit the runtime reports 384 MB, which is the decided heap limit and below `MemoryHigh`.

**3. [Note] The plan's one-shot verify command was run as its separate parts** (the shell on the workstation does not word-split like the plan's snippet assumes), with the same checks and the same outcomes: published release, health, selfcheck, unit directives equal to the tag's, proxy values equal to the decided lines, the route check with its burst.

**4. [Note] Test-only changes merged before publishing.** Before publishing, two CI test failures on `main` were fixed and proven green in PR #36 (test-only, no app change); a Playwright dependency update (test-only) is merged separately. Neither changes the v1.0.0 artifact.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Release `v1.0.0` is public and not a draft; the three assets exist; `verify-published-release.sh v1.0.0` passed.
- Health reports 1.0.0 with the attested commit; selfcheck 39 passed, 0 failed.
- Route check with burst: exit 0. Real-route browser check: zero violations at both sizes.

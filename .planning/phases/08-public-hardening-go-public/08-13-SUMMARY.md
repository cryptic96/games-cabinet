---
phase: 08-public-hardening-go-public
plan: 13
subsystem: deploy
tags: [reverse-proxy, rate-limit, in-flight-limit, hsts, measurement]
requires:
  - phase: 08-public-hardening-go-public
    provides: "measurement release v0.11.0 running healthy behind its host list (plan 08-12)"
provides:
  - "first-visit shape and live-connection cost measured (08-MEASUREMENTS.md)"
  - "proxy values derived from the counted first visit: average 60, period 1s, burst 800, in-flight amount 200"
  - "the proxy's real cabinet file carries the per-address rate limit and in-flight cap on the public route and HSTS-only headers, applied after the owner's go"
affects: [08-14, 08-15, 08-17]
key-files:
  created:
    - .planning/phases/08-public-hardening-go-public/08-MEASUREMENTS.md
  modified: []
key-decisions:
  - "Both proxy middlewares set the source criterion explicitly: remote address, ipv6Subnet 64, no depth"
  - "The headers middleware is reduced to HSTS only and placed first on the public route, so the app's own headers arrive exactly once"
  - "The owner route's middleware list was left untouched"
status: complete
completed: 2026-10-25
actuals:
  tokens: 14000
  tasks: 3
  commits: 2
---

# Phase 8 Plan 13: The proxy's outer wall is live on the LAN route with numbers from a counted first visit Summary

The shared proxy's cabinet file now refuses a flood per address (60 requests per second refill, burst 800, 200 in flight, each counted per remote address with IPv6 grouped by /64) and sends only HSTS, exactly as the owner approved; the route check passes every line, with and without the burst, and the first visit and the cost of a live connection are measured.

## Task results

| Task | Result | Commit |
|------|--------|--------|
| 1 Tracer: first visit and live connection measured, proxy numbers derived | `08-MEASUREMENTS.md` written with the first-visit, proxy-values and live-connection sections; the proxy's real file was read read-only | f9568d0 |
| 2 Owner decision | Shown the numbers with reasoning, the backup path and the diff. Owner's answer, verbatim: "Go (Recommended)" | none |
| 3 Apply the approved change and prove it | Applied; structure check and both route-check runs pass | this summary |

## Evidence (sanitised)

Owner's go:
- Recorded verbatim above, given on the exact diff that was applied.

Safety steps on the proxy:
- The live file's checksum was re-read before touching it and equalled the one recorded at the checkpoint (fd96647...), so no new diff was needed.
- Backup taken, outside the watched folder, with the stamp `202610252024` (file `cabinet.yml.bak-202610252024` next to the dynamic folder); its checksum equals the pre-change file's.
- The approved copy was written outside the watched folder first; its checksum equals the approved copy (10cb310...), it parsed as YAML, and `diff` against the backup showed **27 changed lines**, the same count as the approved diff.
- Moved into place with one `sudo mv`; the applied file's checksum equals the approved copy's.
- Traefik accepted the file: for 30 seconds after the move and again afterwards, neither the journal (no entries) nor Traefik's own log held any error naming the cabinet file, a middleware or a parse problem. See the note on certificate errors below.

Structure check (the plan's verify command, run against a copy of the live file): passed.
- The rate-limit and in-flight middleware blocks each hold `sourceCriterion:`, `ipStrategy:` and `ipv6Subnet: 64`.
- The file holds no `depth:` key.
- The first middleware of the public route is the headers middleware, and its block holds only `headers:` and `stsSeconds:` lines.

Route check from the home network, `check-public-routes.sh --from-lan`: exit 0, 31 of 31 lines PASS.
- Home page: answers 200; Strict-Transport-Security, Content-Security-Policy, X-Content-Type-Options, Referrer-Policy, X-Frame-Options, Permissions-Policy, Cross-Origin-Opener-Policy and Cross-Origin-Resource-Policy each sent exactly once with the expected value; no Server header.
- Static file: answers 200; the same eight headers each exactly once; no Server header.
- Plain HTTP redirects to HTTPS (301).
- Public health page answers 404; public owner page answers 404; ports 5080, 5081 and 5082 on the public hostname answer nothing.
- Home page carries the noindex meta tag, the linked BoardGameGeek credit and no owner reference; robots.txt is absent.
- Owner hostname seen from the home network answers 200.

Route check with the burst, `check-public-routes.sh --from-lan --burst`: exit 0, 32 of 32 lines PASS (the 31 above plus the burst line).
- Burst of 8 presses answered: **202 x1, 409 x4, 429 x3**. The one accepted press started a real sync and opened the shared window (the 409 answers); the 429 answers came from the app's own per-client limit with a `Retry-After` header and a body without a status object, so no proxy refusal was involved.

Sync finished:
- `/cabinet/status` showed `running: true` at the first poll and `running: false` at the next (15 s later), with `lastResult` `unchanged` and a newer `lastSyncedUtc`. No sync is running, so plan 08-14's idle baseline is undisturbed. The shared cooldown window ends about ten minutes after the press.

## Deviations from Plan

None - plan executed exactly as written.

Notes, not deviations:
- Right after the move, Traefik's log shows repeated failures to obtain certificates for three other sites' routers on the same proxy (their public DNS names do not exist, then Let's Encrypt's failed-authorization limit answers 429). The log already held the same limit message with a retry time from an earlier hour, so this is the proxy's existing behaviour on every dynamic reload, not an effect of this change, and none of the routers named is the cabinet's. Reported for the owner to know; nothing was changed.
- The known browser WebSocket refusal through the real route (the proxy forwards `wss` as the scheme) is fixed on the milestone branch but not in v0.11.0; the route check does not test WebSockets, so it does not appear here.

## Known Stubs

None.

## Threat Flags

None. The only new surface is the proxy's per-address wall this plan was meant to add; no hostname, address or file content is recorded here.

## Self-Check: PASSED

- `08-MEASUREMENTS.md` exists and commit f9568d0 exists.
- Applied file checksum equals the approved copy's; backup exists outside the watched folder.

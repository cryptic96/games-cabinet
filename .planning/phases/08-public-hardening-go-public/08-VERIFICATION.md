---
phase: 08-public-hardening-go-public
verified: 2026-10-26T20:00:00Z
status: passed
score: 5/5 must-haves verified (1 via accepted override)
behavior_unverified: 0
overrides_applied: 1
overrides:
  - must_have: "The container has CPU and memory caps sized from real load, and a flood of requests slows the app without starving the other guests on the host (SEC-06 / criterion 4, router-flood neighbour row)"
    reason: "Judged against the amended SEC-06 definition in 08-SIZING-REVIEW.md ('SEC-06 amendment after the verification flood'): the single failing row (verify-router | neighbour) is caused only by the shared reverse proxy's own CPU spent refusing a single-address flood of about 5,400 req/s; the neighbour kept answering (no errors, no timeouts) and recovered by itself, and rate-capped load of about 1,800 req/s left it unchanged. Neighbour median roughly doubled (about 24 ms to 48-67 ms), which the owner judged not to be starvation."
    accepted_by: "owner (verbatim: 'Accept with reason')"
    accepted_at: "2026-10-26"
re_verification: false
gaps: []
deferred: []
human_verification: []
---

# Phase 8: Public hardening and go-public Verification Report

**Phase Goal:** The site is reachable from the internet over HTTPS through the existing reverse proxy, resilient to traffic floods and unable to starve the other guests on the host, and the owner opens it only after a go-public checklist passes.
**Verified:** 2026-10-26
**Status:** passed (criterion 4 passes under the owner-accepted amended definition, see below)
**Re-verification:** No, initial verification

Method: the SUMMARY claims were not taken as evidence. Code, deploy files, docs and tests were read in the repository, the lint suite and the relevant integration tests were run, and the on-host and external evidence was read from the owner-signed records (`08-GO-PUBLIC-RESULTS.md`, `08-MEASUREMENTS.md`, `08-SIZING-REVIEW.md`, `08-PATCH-v1.0.1.md`). Facts that live only on the servers (live proxy file, container caps, scanner grades, phone probe) cannot be re-observed from the repository; they are accepted from those records, which carry the owner's verbatim answers, timestamps and checksums.

## Goal Achievement

### Observable Truths (roadmap success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | From the public internet the site loads over HTTPS through Traefik; health/ops endpoints and every owner tool are unreachable (confirmed by an external probe) | VERIFIED | Public route opened 2026-10-26T15:04:40Z via a guarded one-line removal of the allow-list (guard also proven to fail on two wrong edits); the owner router keeps its allow-list permanently (`deploy/traefik/cabinet.yml.example`). Owner phone on mobile data (08-23): site loads, public `/health` 404 (empty body), ops port 5081 not accessible, owner host not accessible; the proxy log shows a public client address, so per-address limits count real visitors. `build/check-public-routes.sh --from-lan` after the v1.0.1 install: 31 PASS, 0 FAIL (health and owner page 404, ports 5080/5081/5082 answer nothing, no `/owner` reference, owner host 200 from LAN only). |
| 2 | Rapid repeated requests from one client are rate-limited, with a much stricter limit on "sync now" | VERIFIED | Two layers in code: proxy per-address rate limit (60/s, burst 800) plus in-flight cap (200) on the public router; app-side `ClientLimits` (sync press 5 per 60 s, live connections 20 per address, 60 connects per window per address, 600 site-wide) wired with `RequireRateLimiting` on the sync endpoint and live route. Review-2 trailing-slash bypass fixed and proven live on v1.0.1 (61 sequential negotiates: 200 x 60, 429 x 1 with `Retry-After`). Sync burst on v1.0.0: 202 x1, 409 x4, 429 x3. Floods measured in 08-20. I ran `SecurityHeadersTests`, `SyncRateLimitTests`, `LiveOriginTests`, `LiveAddressCapTests`: 91 passed, 0 failed. |
| 3 | Every response carries security headers including a strict CSP, and the cabinet works fully under that policy with no violations | VERIFIED | `Cabinet.Service/Hosting/SecurityHeaders.cs` assigns CSP (`default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'`), nosniff, referrer policy, frame options, permissions policy, COOP and CORP on every response; HSTS added by the proxy (no duplicates, checked by the route script: each header exactly once, no Server header). Browser tests assert empty console errors (including CSP messages) across the smoke, filter and polish suites. Three outside scanners all A+ (securityheaders, Mozilla Observatory 135/100, SSL Labs). |
| 4 | Container has CPU and memory caps sized from real load, and a flood slows the app without starving other guests | VERIFIED (override: amended SEC-06) | Caps in `deploy/systemd/cabinet.service`: MemoryHigh 480M, MemoryMax 600M, swap 0, CPUQuota 80%, CPUWeight 50, TasksMax 512, .NET heap limit 384 MiB; Proxmox container caps set by the owner; `cabinet-selfcheck` 39 passed / 0 failed on v1.0.1 confirms the limits in force. Sizing came from measured floods and a picture rebuild (`08-MEASUREMENTS.md`, `08-SIZING-REVIEW.md`). Verification flood on v1.0.0: direct flood passes every row to 200 workers (neighbour, ssh, ops-health, host-cpu, recovery); router flood passes every row except `neighbour`. **That one row FAILED as measured and was accepted by the owner ("Accept with reason") under the amended SEC-06 definition**, so this criterion is judged against the amended definition, not the unqualified roadmap wording. Host CPU never reached 100% is the owner's reading of the Proxmox graph, not a machine-captured number. |
| 5 | A go-public checklist (headers, rate limits, health not routed, BGG credit, BGG terms re-read, no personal data) is completed, and only then does the router open the public route | VERIFIED | `docs/go-public.md` exists with the checklist; `08-GO-PUBLIC-RESULTS.md` has every row evidenced before the switch, the owner's sign-off ("signed off, go, scanners yes") precedes the switch time. Independent checks: `build/lint.sh` passes (repo-rules, workflows, shell, secrets, script-tests); a grep outside `.planning` finds no planning IDs, phase numbers or planning document names; no unreferenced TBD/FIXME/XXX in code, deploy, build or docs. BGG terms re-read against the newest archive captures plus the owner's live skim ("Yes, unchanged"). |

**Score:** 5/5 truths verified (criterion 4 via the accepted override); 0 present-but-behavior-unverified.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Cabinet.Service/Hosting/SecurityHeaders.cs` | CSP and headers on every response | VERIFIED | Substantive, registered in the pipeline, covered by `SecurityHeadersTests` |
| `Cabinet.Service/Limits/ClientLimits.cs`, `LiveConnectWindow.cs`, `LiveConnectWindowLimiter.cs` | Per-address and site-wide limits | VERIFIED | Wired through `AddRateLimiter`, `UseCabinetClientLimits`, `RequireRateLimiting`; tests pass |
| `Cabinet.Service/Sync/SyncEndpoints.cs` | Strict limit on sync now | VERIFIED | `RequireRateLimiting(SyncPressPolicyName)` on the sync route |
| `deploy/traefik/cabinet.yml.example` | Public router with rate limit and in-flight cap; owner router permanently allow-listed | VERIFIED | Matches the live file's recorded middleware order |
| `deploy/systemd/cabinet.service` | Memory/CPU/task caps | VERIFIED | Values match the live limits recorded in `08-PATCH-v1.0.1.md` |
| `deploy/bin/cabinet-selfcheck`, `build/check-public-routes.sh`, `docs/go-public.md` | Checks and checklist | VERIFIED | Present; selfcheck 39/0 and route script 31 PASS recorded on v1.0.1 |

### Requirements Coverage

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| SEC-01 | 08-03, 08-10, 08-18, 08-21, 08-22, 08-23, 08-24 | Reachable over HTTPS through Traefik | SATISFIED | Criterion 1 |
| SEC-02 | 08-01, 08-05, 08-06, 08-10, 08-11, 08-13, 08-15 to 08-21 | Rate limiting, stricter on sync now | SATISFIED | Criterion 2 |
| SEC-03 | 08-03, 08-06, 08-08, 08-09, 08-11, 08-13, 08-18, 08-19, 08-21, 08-22 | Security headers incl. strict CSP | SATISFIED | Criterion 3 |
| SEC-04 | 08-03, 08-06, 08-07, 08-10 to 08-12, 08-15, 08-18 to 08-23 | Health/ops and owner tools not internet-reachable | SATISFIED | Criterion 1 |
| SEC-06 | 08-02, 08-07, 08-10 to 08-21 | Container CPU/memory caps | SATISFIED under amended definition | Criterion 4; owner-accepted router `neighbour` row |

No orphaned requirements: REQUIREMENTS.md maps exactly SEC-01, 02, 03, 04, 06 to Phase 8 and all five appear in plan frontmatter (SEC-05 belongs to Phase 3, already complete).

### Anti-Patterns Found

None blocking. Both code reviews are closed: `08-REVIEW.md` (8 findings fixed, shipped v1.0.0) and `08-REVIEW-2.md` (1 critical trailing-slash bypass and 4 warnings plus 2 info, all fixed, shipped v1.0.1 and proven on the container with the live 60/1 negotiate probe).

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Headers, sync limit, live origin guard and per-address cap | `dotnet test --solution Cabinet.slnx` filtered to the four classes | 91 passed, 0 failed (the "Failed" banner is only the two other projects running zero matching tests, exit code 8) | PASS |
| Repository rules, secrets, workflows, shell, script tests | `bash build/lint.sh` | all five groups PASS | PASS |
| No planning references outside `.planning` | grep for requirement IDs, phase numbers, planning doc names | none in code, docs, scripts or config | PASS |

### Human Verification Required

None outstanding. The items that needed a human (phone probe, host CPU graph, scanner runs, BGG terms skim, maintenance keys) were all performed by the owner and recorded with verbatim answers.

### Owner decisions and notes to carry forward (not gaps)

1. **SEC-06 amendment (criterion 4).** The roadmap wording is met only under the amended, owner-accepted definition. The router-flood `neighbour` row FAILED as measured; it is recorded as accepted, not as passed. The proxy container's core count remains an owner maintenance option. The host-CPU "never 100%" result rests on the owner's reading of a graph.
2. **Maintenance SSH keys.** The owner declined the passphrase step (08-24) and accepted the keys as is, relying on `from=` pinning to the workstation and SSH restricted to admin sources on both containers. This is an owner decision recorded in the AR-08 and AR-06-05 rows with a review point of 2027-01-24; the residual risk (anything on the workstation can use admin keys on the cabinet and on the shared proxy) was stated to the owner.
3. **Proxy password logins** on the shared proxy's SSH remain allowed (optional step not taken); outside this phase's goal.
4. **Housekeeping:** `.planning/REQUIREMENTS.md` still shows SEC-01, 02, 03, 04, 06 as unchecked/Pending and should be flipped to Complete when the phase is closed.
5. **Known items outside the goal** (todos): Chromium page stalls on CI beyond the containment test, a friendlier not-found page, the proxy's unrotated access log (about 171 MiB), the intermittent test-host CLR crash, and Dependabot #33 (fixed on the milestone branch, not yet on `main`).
6. **Live version:** the site runs v1.0.1 (installed by the container's poll timer, selfcheck 39/0, route check 31 PASS, trailing-slash bypass refused).

---

_Verified: 2026-10-26_
_Verifier: Claude (gsd-verifier)_

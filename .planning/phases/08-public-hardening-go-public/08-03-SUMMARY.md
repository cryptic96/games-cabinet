---
phase: 08-public-hardening-go-public
plan: 03
subsystem: build-tooling
tags: [bash, curl, route-check, security-headers, rate-limit, go-public]
requires:
  - phase: 08-public-hardening-go-public
    provides: "per-client press limit and header set the check proves (plans 08-01, 08-08, built in parallel)"
provides:
  - "build/check-public-routes.sh: one command proving the deterministic go-public route promises, from the home network or from outside"
  - "build/tests/check-public-routes-test.sh: offline stubbed-curl test (155 checks)"
affects: [08-12, 08-13, 08-19, 08-22, 08-23]
tech-stack:
  added: []
  patterns: ["stubbed curl with a table of answers (URL, status, body, headers), repeated rows answered in order"]
key-files:
  created:
    - build/check-public-routes.sh
    - build/tests/check-public-routes-test.sh
  modified: []
key-decisions:
  - "Burst refusals are classified by body shape: cooldown without a status object is the app, a status object is the shared window, no JSON is the proxy"
  - "Retry-After lines in the shared header dump must be at least the number of app cooldown refusals, so one stray header cannot vouch for every refusal"
  - "CABINET_BURST_COUNT is validated only with --burst; an oversized value is ignored otherwise"
requirements-completed: [SEC-01, SEC-03, SEC-04]
status: complete
actuals:
  tokens: 28000
  tasks: 2
  commits: 1
duration: 25min
completed: 2026-10-25
---

# Phase 8 Plan 03: Scripted public route check Summary

**A single `check-public-routes.sh --from-lan|--from-outside [--burst]` command that prints a PASS or FAIL line per public-route promise (status, redirect, full header set on page and static file, closed routes and ports, noindex, credit, owner host by vantage point, opt-in app-limit burst) without ever printing a hostname.**

## Accomplishments
- Public home page and `/img/powered-by-bgg.svg` each checked for HSTS (max-age, no preload), CSP (`default-src 'self'`, `frame-ancestors 'none'`), nosniff, Referrer-Policy, X-Frame-Options DENY, Permissions-Policy `()`, COOP and CORP `same-origin`, each exactly once, and no `Server` header.
- Plain HTTP must redirect (301/302/307/308) to an `https://` target; public `/health` and `/owner/pictures` must answer 404; ports 5080, 5081, 5082 on the public hostname must answer `000`.
- Home body must carry `content="noindex"`, the linked BGG credit and no `/owner` string; `/robots.txt` must be 404 or have no `Disallow: /` line.
- Owner hostname `/` wants 403 with `--from-outside`, 200 with `--from-lan`.
- `--burst`: `DEFAULT_BURST_COUNT=8`, `MAX_BURST_COUNT=20`, one curl call with one `-o` per press; passes only on a 429 with `{"outcome":"cooldown"}` and no status object plus Retry-After; FAIL text names the shared-window, proxy, missing-header and no-429 cases.
- Exit codes 0 / 1 / 2 (missing vantage, hostname, bad option or bad burst count); `-h` prints usage and exits 0.

## Task Commits
Both tasks live in the same two files and the script was written whole, so they were committed together:
1. Task 1 (tracer) and Task 2: `b6a6119` - feat(08-03): add scripted public route check with offline test

## Verification
- `bash build/tests/check-public-routes-test.sh`: 155 check(s), 0 failures (healthy run = 31 PASS lines, 32 with burst; per-header missing, duplicated, preload, Server, and each closed-route / body / robots / vantage / burst deviation gives exactly one named FAIL; default burst pinned at 8 requests, 20 accepted, 21 / 0 / non-number refused with no request).
- `build/lint/checks/30-shell.sh`, `10-repo-rules.sh`, `40-secrets.sh` pass. `grep -c` acceptance counts: `DEFAULT_BURST_COUNT=8` 1, `MAX_BURST_COUNT=20` 1, `CABINET_PUBLIC_HOST` 3.
- Nothing was run against a real host; `Cabinet.BrowserTests` was not run.

## Deviations from Plan

### Auto-fixed Issues
None of Rules 1-3 applied beyond normal iteration. One process deviation:

**1. [Process] Tracer and expansion task committed together**
- **Issue:** The script was written complete in one pass, so the tracer's header slice and the Task 2 checks share files and could not be split into honest separate commits.
- **Effect:** The tracer feedback gate was satisfied by the offline test (the tracer behaviours pass), but no separate tracer commit or checkpoint exists. No real endpoint exists to verify a tracer against in this plan.
- **Commit:** b6a6119

**2. [Design detail] Retry-After counted per refusal**
- Plan said the header file must hold a Retry-After line; the script requires at least as many Retry-After lines as app cooldown refusals, which is stricter and still passes the planned scenarios.

## Known Stubs
None.

## Threat Flags
None. The script only issues requests a visitor could; hostnames are read from the environment and kept out of output (asserted by the test).

## Self-Check: PASSED
- build/check-public-routes.sh and build/tests/check-public-routes-test.sh exist and are executable; commit b6a6119 exists.

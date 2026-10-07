---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 01
subsystem: bgg-access-check
tags: [bgg, access-check, shape-only, python-stdlib, privacy-guard]
requires: []
provides:
  - "build/bgg-access-check.py: stdlib-only shape-only BGG access check with --plan, --self-test and --run [--env-file PATH]"
  - "build/tests/bgg-access-check-test.sh: script test discovered by the lint job's script-tests check"
  - "docs/bgg-access-check.md: operator guide (what is asked, what is printed, what is never printed, how to run)"
  - "Recorded owner go-ahead for exactly one run against BGG, to be executed in the next plan"
affects: [bgg access run plan, snapshot and sync plans that depend on the measured answers]
tech-stack:
  added: []
  patterns:
    - "report built as one string, scanned for forbidden values, withheld whole if anything matches"
    - "transport injected so the 202 schedule, request cap, spacing and stop rules are proven on a fake clock"
    - "credentials read by the script itself from the server env file, never from the command line"
key-files:
  created:
    - build/bgg-access-check.py
    - build/tests/bgg-access-check-test.sh
    - docs/bgg-access-check.md
  modified: []
decisions:
  - "The access check is shipped to the container by piping the script over SSH to python3 run as the app user; nothing is installed on the container"
  - "The owner approved a single run, exactly as presented, before anything touches BGG"
metrics:
  completed: 2026-10-06
  tasks: 2
  commits: 1
status: complete
actuals:
  tokens: 90000
  tasks: 2
  commits: 1
---

# Phase 3 Plan 01: BGG access check and owner go-ahead Summary

A stdlib-only Python script that asks BGG nine bounded questions with the real token and prints only the shape of the answers, proven on synthetic data to drop titles, locations, credentials, URLs and markup, plus the owner's recorded approval to run it once.

## What was built

### Task 1: shape-only access check (tracer) - commit f3315f9

- `build/bgg-access-check.py` (stdlib only). Modes: `--plan` (prints the call list, reads nothing, no network), `--self-test` (synthetic data, no network), `--run [--env-file PATH]` (default `/etc/cabinet/cabinet.env`). Exit codes 0, 2, 3, 4, 5 as planned.
- Transport: one `HTTPSConnection` to the bare BGG host per request, no redirect handling, token sent only to that host, honest User-Agent (with the contact address when configured), at least 6 seconds between requests, hard cap of 14 requests, 202 poll waits 5, 10, 20, 30, 30, 30 seconds with at most 6 polls.
- Call plan A to I covers the private inventory location with the token alone, `own=1` and subtype behaviour, the default call's expansion mislabelling, duplicate collection entries, 202/401/403/429 behaviour, whether a User-Agent is required, and the presence and magnitude of owned-version dimensions.
- Output guard: the report is built as one string and withheld (exit 4) when it would contain the token, username, contact address, any title, any location value, a URL or markup.
- `build/tests/bgg-access-check-test.sh`: self-test in isolated mode, plan labels A to I with parameter names only, no-argument usage exit 2, not-configured exit 3 (keys missing, file missing, username missing), standard-library-only imports, bare host only.
- `docs/bgg-access-check.md`: plain-language operator guide with a placeholder for the SSH host alias.

### Task 2: owner prerequisites and approval (checkpoint, resolved)

- Inventory locations: the owner confirmed private info is set on about three owned games.
- Env file: the owner edited the server env file by hand with the three BGG keys. Claude did not read the file and no value from it is recorded anywhere.
- Approval: the owner answered "yes approve" for a single run, exactly as presented: first the read-only probe `python3 --version` over SSH to the container, then the access check piped to `python3 - --run` as the app user. The container was not touched in this plan; the run belongs to the next plan.

## Verification (re-run for this summary)

- `bash build/tests/bgg-access-check-test.sh`: 8 passed, 0 failed.
- `python3 -I build/bgg-access-check.py --self-test`: prints `self-test passed`, exit 0.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Report guard skips short, low-signal remembered values**
- **Found during:** Task 1
- **Issue:** Matching every remembered value as a substring made purely numeric tokens under 4 characters and 1-character titles or locations match random digits and letters in a numeric report, withholding every report.
- **Fix:** The guard skips remembered purely-digit values under 4 characters and 1-character titles or locations. Credentials (token, username, contact address, wrong token) are always matched regardless of length.
- **Files modified:** build/bgg-access-check.py
- **Commit:** f3315f9

**2. [Rule 2 - Missing critical functionality] Header values and names are sanitised before printing**
- **Found during:** Task 1
- **Issue:** Whitelisted response header values and element or attribute names come from BGG and could carry arbitrary text into the report.
- **Fix:** Header values, element names and attribute names pass through sanitising helpers before printing; redirects print only a host class, never the target.
- **Files modified:** build/bgg-access-check.py
- **Commit:** f3315f9

**3. [Rule 2 - Missing critical functionality] Extra stop rule for server trouble**
- **Found during:** Task 1
- **Issue:** The planned stop rule covered 401, 403 and 429 only; repeated server errors would keep hammering BGG until the cap.
- **Fix:** Two consecutive 5xx answers or connection errors end the run (exit 5), still printing the shape gathered so far.
- **Files modified:** build/bgg-access-check.py
- **Commit:** f3315f9

**4. [Rule 2 - Missing critical functionality] Self-test also proves the politeness rules**
- **Found during:** Task 1
- **Issue:** The planned self-test covered the summariser and guard only; the 202 schedule, request cap, spacing and stop rules were untested and matter for T-03-03.
- **Fix:** The self-test runs a scripted transport on a fake clock and asserts the 202 poll schedule, the 14-request cap, request spacing and the stop on 401.
- **Files modified:** build/bgg-access-check.py
- **Commit:** f3315f9

## Authentication Gates

Task 2 was a planned human-action checkpoint (BGG private info and the server env file are the owner's own login and secret). It was resolved by the owner as described above; it is normal flow, not a deviation.

## Known Stubs

None.

## Threat Flags

None. The script adds no network endpoint to the application; it is an operator tool whose only outbound conversation is the bounded, token-only call to the BGG API host covered by the plan's threat model.

## Requirements

LOC-02 and SEC-05 are not complete: both depend on the run and its sign-off in the next plan, so they are intentionally not marked here.

## Self-Check: PASSED

- build/bgg-access-check.py, build/tests/bgg-access-check-test.sh and docs/bgg-access-check.md exist.
- Commit f3315f9 exists in history.

---
phase: 04-enrichment-box-images-shape
plan: 01
subsystem: infra
tags: [bgg, shape-only-check, image-host, python-stdlib]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: the shape-only access check, its transport and its output guard
provides:
  - "art suite in build/bgg-access-check.py (calls J, K, L, M1 to M6, N) with shape-only summaries"
  - "token-free, allowlisted, capped image transport"
  - "offline self-test and script tests for both suites; operator docs section"
affects: [04-05 art suite run, image pipeline, candidate rule, image-host allowlist]
actuals:
  tokens: 18000
  tasks: 2
  commits: 2
key-files:
  created: []
  modified:
    - build/bgg-access-check.py
    - build/tests/bgg-access-check-test.sh
    - docs/bgg-access-check.md
key-decisions:
  - "Identifiers of four or more digits are guarded as whole digit runs (not substrings), so a byte count that merely contains an id's digits cannot withhold a costly real run; an exact match still withholds."
  - "Image requests use a separate ImageTransport that refuses any host outside the known list and never builds an Authorization header."
patterns-established:
  - "A suite is a call list plus summarisers plus a self-test block, selected with --suite; the output guard is shared."
requirements-completed: [SYNC-06, SYNC-07, IMG-01]
duration: unrecorded
completed: 2026-10-07
status: complete
---

# Phase 4 Plan 01: Art suite for the shape-only BGG access check Summary

Extended the committed shape-only access check with an "art suite" (`--suite art`) that asks where the owned version's image lives, what the per-game details answer carries and how the image host behaves, printing counts and classes only behind the existing output guard.

## Accomplishments

- **Task 1 (tracer), commit 376cd73:** `--suite {access,art}` on `--plan`, `--run` and `--self-test`; the access suite is unchanged in behaviour.
  - API calls J, K (collections with the selected version) and L (one details call, at most two base and two expansion ids, statistics, no type), at most 10 API requests, 6 s apart, stop on 401/403/429.
  - Image downloads M1 to M6 and the User-Agent-less repeat N: known host only, https or scheme-less addresses only, no token, no redirect followed, at most 12 MB read, at most 7 image requests, 1.5 s apart.
  - Summaries: version and item image counts and equality, address host and path-form classes, details field presence, link types with inbound and outbound expansion links, missing ids, and per download status, type, byte count, format, pixel size and cap flags.
  - Guard: every image address, path, title and id (version, object, collection) is a forbidden value.
  - 120 self-test checks on synthetic data; a mutation check showed the id checks fail when the digit-run match is disabled. The shell test passes 43 cases.
  - Verified: `bash build/tests/bgg-access-check-test.sh`, `python3 -I build/bgg-access-check.py --self-test`, `build/lint.sh` repo-rules, shell, script-tests and secrets all pass.
- **Task 2 (blocking-human decision): owner answer "approved".** One run of the art suite from the container with the exact command `ssh <container> 'sudo -n -u cabinet python3 - --run --suite art' < build/bgg-access-check.py`. Provenance: the answer reached this executor as a message relayed by the orchestrating agent, not from the owner directly, so this executor did not independently verify it. No env-file value is recorded here. This plan made no request to BGG or its image host; the run itself belongs to plan 05, which should confirm the owner's go-ahead directly before running, as the plan's consent prohibition requires.

## Deviations from Plan

None - plan executed as written. One design choice within the plan's intent is listed under key-decisions (whole-digit-run matching for identifiers).

## Known Stubs

None.

## Threat Flags

None. The image transport adds an outbound HTTPS surface to the image host only, already covered by the plan's threat register.

## Issues Encountered

None.

## Self-Check: PASSED

- build/bgg-access-check.py, build/tests/bgg-access-check-test.sh, docs/bgg-access-check.md: present.
- Commit 376cd73: present.

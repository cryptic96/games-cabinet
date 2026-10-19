---
phase: 07-game-night-filters-location-cabinets
plan: 33
subsystem: release
tags: [release, v0.9.0, deploy, container, health, selfcheck]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "Release v0.9.0, tagged and published, verified on the workstation (plan 07-32)"
provides:
  - "Container running v0.9.0 (merge commit 2df832d), Healthy, self-check 25 passed 0 failed"
  - "Filters and location cabinets answering on the container: layout version 17, by-location layout, other-titles names document, filter bar markup"
affects: []

key-files:
  created: []
  modified: []

key-decisions:
  - "Task 1 recorded as a deviation: the owner approved the deploy environment before being told the draft was verified; the published release was verified with the same checks and the owner confirmed the approval was theirs"
  - "Only the read-only commands the owner approved were run on the container; nothing was started, restarted, edited or read beyond status codes, versions and counts"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: n/a
completed: 2026-10-17
actuals:
  tokens: 6000
  tasks: 3
  commits: 1
---

# Phase 7 Plan 33: Release v0.9.0, part two Summary

The container installed v0.9.0 on its own through the pull timer and runs it Healthy. The self-check passes in full, the desktop layout reports version 17, and the new by-location layout, the other-titles names document and the filter bar all answer. The owner's deploy approval came before the "draft verified" message, so the plan's order was not followed (see Deviations).

## Task 1: draft unchanged and owner told (recorded as it happened)

The planned order did not happen. The owner approved the `deploy` environment in the GitHub UI at about 14:33Z on 2026-10-17 while the release run was still in progress, and the release was published at 14:33:26Z, before anyone could tell the owner "the draft is verified". Consequences:

- There was no draft to re-compare. The draft-unchanged check (`isDraft` true, three assets) could not be run; the release was already public.
- The published release was verified instead, twice and independently (by the plan 07-32 executor and by the orchestrator), with `build/verify-published-release.sh v0.9.0`: every line PASS, "all checks passed for v0.9.0". Public, not a draft, exactly three assets (`cabinet-0.9.0.zip` 6297264 bytes, `cabinet-0.9.0.zip.sha256` 84 bytes, `cabinet-0.9.0.zip.sigstore.json` 10929 bytes), checksum OK, attestation verified with the source digest equal to the merge commit (which is on main), manifest naming 0.9.0 and that commit, a one-byte-modified copy refused. SHA-256 of the checksum file: `35a977735afe99c96bd208a3f2d0d2b8853181aa4bfe82afe8ac656f1a9f035c`.
- The orchestrator asked the owner whether the approval was theirs and the owner confirmed it (Task 2 answer below).
- The rollback note (owner-data format: older file copy kept at the first save, rollback lands on that copy through the recovery panel) and the first-sync note (game details re-read once for other titles) were given to the owner before the merge and tag question in plan 07-32. They were not repeated in a separate pre-approval message here.

## Task 2: owner's answer (checkpoint resolved)

Verbatim from the owner in chat: "yes that was me, approved"

- "yes that was me" confirms the owner approved the `deploy` environment themselves.
- "approved" approves the read-only container commands of Task 3.

`gh release view v0.9.0 --json isDraft,isImmutable,publishedAt` now prints `isDraft` false, `isImmutable` true, `publishedAt` 2026-10-17T14:33:26Z.

## Task 3: container evidence

Run through the configured SSH alias, read-only, output limited to status codes, versions and counts. Container clock at the check: 2026-10-17T18:32Z.

| Check | Result |
| ----- | ------ |
| Health (ops listener, loopback) | `Healthy`, version `0.9.0`, commit `2df832d364cd0d70cd99e2224978b0a4879b08be` (the merge commit), `missingArt` 0 |
| Deploy poll timer | active, next run in about 5 minutes, last run 18:25:55Z; the poll service was not started by hand |
| `cabinet-selfcheck` | 25 passed, 0 failed (installed scripts, libraries and units match the active release; no GitHub credential and no Actions runner on the host; SSH password authentication off; image smoke in the production sandbox passes) |
| `/cabinet/layout?profile=desktop` `layoutVersion` | 17 |
| `/cabinet/layout-by-location?profile=desktop` | HTTP 200, `locations` count 2 |
| `/cabinet/search-names` | HTTP 200 |
| Public page filter bar (`class="filter-bar"` count) | 1 |

Workstation: the release reports immutable, published, three assets (above). The release verification script was not re-run here; its two earlier passes are recorded in plan 07-32 and above.

Not collected, and why: the first-sync details re-read was not checked because the plan lists no command for it and no logs may be read; the health document carries no snapshot age field, so none is recorded. No sync was requested, the env file was never opened, no owner data was read beyond the counts above.

## Deviations from Plan

**1. [Process] Owner approved the deploy before the draft message; Task 1 could not run as planned**
- **Found during:** Task 1
- **Issue:** the owner's standing rule is to approve the deploy only after being told the draft is verified. The approval came during the workflow run, and the release was public before the message. The draft-unchanged comparison and the pre-approval message were therefore impossible.
- **Fix:** none outward-facing. Verification was done on the published release with the same checks, twice, by two parties; the owner confirmed the approval was their own.
- **Files modified:** none.
- **Commit:** none.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Health reports 0.9.0 with the merge commit; selfcheck 25 passed, 0 failed; layout version 17; by-location 200; names 200; filter bar count 1.
- Release v0.9.0: published, not a draft, immutable.
- No secrets, addresses, hostnames or personal names in this file.

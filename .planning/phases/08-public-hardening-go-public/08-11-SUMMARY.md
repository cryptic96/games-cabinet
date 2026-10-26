---
phase: 08-public-hardening-go-public
plan: 11
subsystem: release
tags: [release, pull-request, tag, attestation, measurement-build]
requires:
  - phase: 08-public-hardening-go-public
    provides: "all hardening from plans 08-01 to 08-10, merged on the milestone branch"
provides:
  - "pull request merged into main with a merge commit"
  - "annotated tag v0.11.0 on the merge commit"
  - "attested draft release with three assets, verified on the workstation"
affects: [08-12, 08-20]
key-files:
  created:
    - .planning/phases/08-public-hardening-go-public/08-11-OWNER-DECISION.md
  modified:
    - .planning/phases/08-public-hardening-go-public/08-11-RELEASE.txt
    - build/scan-history.sh
    - build/tests/scan-history-test.sh
key-decisions:
  - "The history scan's folder-segment pattern now requires a letter, digit or underscore after /home/, /mnt/, /media/ and /Users/, so elided examples are no longer reported as local paths"
  - "Merged main back into the milestone branch (plain merge commit, no tree change) before pushing, so the branch contains the merge commit and the next pull request does not diverge"
status: complete
completed: 2026-10-25
actuals:
  tokens: 4500
  tasks: 3
  commits: 5
---

# Phase 8 Plan 11: The hardening merged, tagged v0.11.0 and waiting as a verified draft Summary

The hardening reached `main` through pull request 34 (merge commit), carries the annotated tag `v0.11.0` on that merge commit, and its release is an attested draft whose checksum, attestation and manifest were all verified on the workstation; nothing is published.

## Task results

| Task | Result | Commit |
|------|--------|--------|
| 1 Tracer: hardening travels to an open pull request | Pull request 34 opened; `pr=34` recorded | 5819346 |
| 1 (fix) Scan fix, see deviations | History scan narrowed, tests on both sides | 337895f |
| 2 Checks reported, owner decision | CI 7 of 7 green at the opening head and again at the fixed head; owner approved merging and tagging `v0.11.0`; decision page written | 11b2d79 |
| 3 Merge, tag, verified draft | Merged, tagged, built, verified (below) | merge f104bae on main; milestone merge c33d03f |

## Local gates (at the head before the scan fix, all passed)

- Unit tests under a network namespace: 3008 passed
- Integration tests: 603 passed
- Page-script (node) tests: 765 passed
- Browser slices under the CI font (DejaVu): Look 191, Owner 220, Filter 203, rest 183, WebKit 2
- Full `build/lint.sh`: all checks passed
- `build/scan-history.sh`: failed the absolute-paths check at first (see deviations); all six checks pass after the fix

CI on the pull request: 7 of 7 jobs green at the opening head, and 7 of 7 green again at the head the merge was pinned to (`337895f`, `--match-head-commit`). Tests were not re-run after the scan fix, which touches only a build script and its own test.

## Release evidence

- **Merge:** pull request 34 merged with a merge commit; merge commit `f104bae1d62076797c3767dd70bbb4f73ac57499`. Base before merge was `b4be3b384239f9f4261fc712df2998ad33fd1485`.
- **Identities:** accepted.
- **Tag:** `v0.11.0`, annotated (`git cat-file -t` reports `tag`), tagger is the repository's noreply identity, points at the merge commit. `build/validate-release-tag.sh` accepted it (printed `version=0.11.0`). The push succeeded through the documented admin bypass for tag creation.
- **Release build:** the draft appeared on the fourth poll (`true 3`), within the ten allowed; the previous release (`v0.10.0`) was published and untouched.
- **Assets (3):**
  - `cabinet-0.11.0.zip` (SHA-256 `f62bc6e75facbc9564c5c4ef23e8e502765c585806347f330339008e79f3884b`)
  - `cabinet-0.11.0.zip.sha256` (SHA-256 `6e18a6d9c832955b59e06e350651a2d2750e6b355d4f637c2cba70391f9819ed`)
  - `cabinet-0.11.0.zip.sigstore.json` (SHA-256 `b0e1e0a53d6824ae1a1bec15545e6ebaa7470e4dee753590db50c51007a38898`)
- **Checksum file's own SHA-256:** `6e18a6d9c832955b59e06e350651a2d2750e6b355d4f637c2cba70391f9819ed` (the value plan 08-12 compares the draft against).
- **Checks:** `sha256sum -c` reports OK; `gh attestation verify` with the release workflow as signer, `refs/tags/v0.11.0` as source ref and self-hosted runners denied exits 0; `release-manifest.json` inside the archive reads `version` `0.11.0` and `commit` `f104bae1d62076797c3767dd70bbb4f73ac57499`, the merge commit.
- **Plan verify command:** passed (exit 0). The release is still a draft.
- **Milestone branch:** `origin/main` merged into `milestone/v1-games-cabinet` (no tree change) and the branch pushed as a fast-forward; the pull request's head was not touched before the merge.

## Owner-facing message

The v0.11.0 release draft is verified: its checksum, its signed attestation and its manifest (which names v0.11.0 and the merge commit) all check out, and nothing is published yet. The two server-side steps and the deploy approval follow in the next step; please do not approve the deploy environment until then.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] History scan reported 1711 false "absolute local path" findings**
- **Found during:** Task 1 local gates (`build/scan-history.sh`)
- **Issue:** The absolute-paths check failed with 1711 findings. Every commit was inspected: no real local path exists in history. All findings were two placeholder strings repeated across commits: an elided `/home/.../` example in an old research note and a `/home/|/mnt/` regex fragment inside two old plan commands.
- **Fix:** The folder-segment pattern after `/home/`, `/mnt/`, `/media/` and `/Users/` now has to start with a letter, digit or underscore, as a real user or volume name does. New tests cover both sides: placeholders pass, a real-looking mounted-volume path is still reported. The full history scan then passed all six checks. The owner was told, with the offer to revert this one commit and rewrite history instead.
- **Files modified:** `build/scan-history.sh`, `build/tests/scan-history-test.sh`
- **Commit:** 337895f

### Process notes

- The plan allows only one pull request head to be approved; the fix was pushed before the owner's answer, CI re-ran green on it, and the merge was pinned to that exact head.
- The tag push reported a bypassed rule violation ("creations being restricted"); this is the admin-only tag creation described in the releasing guide, not an error.
- A `gh attestation verify` run prints nothing on success when output is piped; the exit status (0) is the evidence.

## Known Stubs

None.

## Threat Flags

None. No new network, auth or file surface; only a build script's pattern was narrowed and tested.

## Self-Check: PASSED

- Merge commit `f104bae` on `origin/main`: found
- Tag `v0.11.0`: annotated, points at `f104bae`
- Commits `5819346`, `337895f`, `11b2d79`: found
- Release `v0.11.0`: draft, three assets, verified

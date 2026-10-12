---
phase: 05-game-detail-accessibility-language
plan: 16
subsystem: release
tags: [release, deploy, device-check, ci]
requires: ["05-15"]
provides:
  - "release v0.6.0 published, attested, immutable and installed on the container"
  - "sanitised deployment evidence (versions, codes, headers, counts)"
  - "the owner's partial device check recorded by item, with open items listed"
affects: [release.yml, test projects, docs]
tech-stack:
  added: []
  patterns: ["shared wait for the card's history step to go before the next navigation"]
key-files:
  created: []
  modified:
    - Cabinet.UnitTests lock file and project file (xunit.v3 4.0.2)
    - Cabinet.IntegrationTests lock file and project file (xunit.v3 4.0.2)
    - Cabinet.BrowserTests project file and shared test base (xunit.v3 4.0.2, shared wait helper)
    - .github/workflows/release.yml (upload-artifact 7.0.2, download-artifact 8.0.2)
decisions:
  - "The three Dependabot updates were folded into the milestone branch before merging; their pull requests were closed"
  - "Rotation with a card open closes the card (designed behaviour); revisit only if it annoys in daily use"
metrics:
  duration: "several sessions with owner approvals between them"
  completed: 2026-10-12
status: complete
actuals:
  tokens: 40000
  tasks: 5
  commits: 3
---

# Phase 5 Plan 16: Release v0.6.0 and the owner's device check Summary

Phase 5 shipped as v0.6.0 through the full path (pull request, green checks, owner-approved merge and tag, attested draft verified on the workstation, owner-approved publish, pull-based deploy) and the deployed cabinet checks out on the container. The owner's check on real devices is only partly done: the card, pull-out, expansion swap, Dutch page, keyboard model, sheet drag and the Safari and Firefox fallbacks are confirmed, while five checks are still open and are listed below as open, not passed.

## What was done

### Task 1: Pull request (tracer)
Pull request #19 (https://github.com/cryptic96/games-cabinet/pull/19) from `milestone/v1-games-cabinet` into `main`, with a plain-language title and body and no planning references or personal data. The local gates (network-isolated non-browser suite, node tests, browser tests, lint) were run at the pushed head before opening it. The executor did not poll the checks.

### Task 2: Checks and the owner's approval
The orchestrator reported "checks passed": `build-test`, `browser-tests` and `lint` all SUCCESS on head `ef6ee7570e345bd62d0d5ec832094b0c83e2e06c` (3 passing, 0 failing, 0 pending, merge state CLEAN). The check history took three rounds, see Deviations. The owner's answer, verbatim: "ci is green, approve merge and tag".

### Task 3: Merge, tag and verified draft
- Merged with a merge commit: base `origin/main` at `5475a5a`, merge commit `7edc720` (full `7edc7208f8fb18eddac75fe159304a7312761fba`). Identities accepted.
- Annotated tag `v0.6.0` on the merge commit, pushed (the only tag pushed). The push noted the owner's admin bypass of the tag-creation rule.
- The draft showed `true 3` on the second of ten polls. `sha256sum -c` OK; `gh attestation verify` (repository, `release.yml` signer, `refs/tags/v0.6.0`, `--deny-self-hosted-runners`) exit 0; the manifest inside the zip names version `0.6.0` and commit `7edc720`.
- The orchestrator independently re-verified tag, assets, checksum, attestation and manifest and told the owner the draft check passed. Release run: https://github.com/cryptic96/games-cabinet/actions/runs/38212954332

### Task 4: Owner publishes
The owner approved the deploy environment themselves. Owner's answer, verbatim: "i just approved".

### Task 5: Container evidence and the owner's device check
Evidence, sanitised (counts, versions, codes and header values only):

| Check | Result |
|-------|--------|
| Published | 07:05:12Z, `isDraft` false, immutable true |
| `build/verify-published-release.sh v0.6.0` | exit 0, including tamper rejection |
| Health before install | Healthy, version 0.5.2 |
| Install | container reported 0.6.0 on the first 3-minute check (07:09:06Z); the deploy poll service was not started |
| Health after | Healthy, version 0.6.0, commit `7edc720`, `missingArt` 0 |
| `cabinet-selfcheck` | 21 passed, 0 failed |
| Desktop `layoutVersion` | 15 |
| Cards route | 200 |
| ETag on the cards route | count 1 on GET (see Deviations for the HEAD form) |
| Dutch request | `Content-Language: nl`; `lang="nl"` count 2 |
| Foreign host (`geekdo`) in layout data | 0 |
| Foreign host (`geekdo`) in card data | 0 |
| Cards | 65 |
| Cards with a location | 0 (private info stays off) |
| The plan's automated verify block | exit 0 |

All container calls went through the workstation alias, one command per call. No logs were read, the env file was never opened and no sync was requested.

## Owner's device check by item

Recorded as yes, no or open. Open is not passed.

| Item | What | Answer |
|------|------|--------|
| 1-4 | Android Chrome Back: button, gesture, after tapping inside the card, second Back leaves | OPEN (owner has no Android device) |
| 5 | iOS Safari edge swipe closes the card | YES |
| 5 | iOS Safari second swipe leaves the site | OPEN (not confirmed) |
| 6 | Desktop browser Back | OPEN (not reported) |
| 7 | Pull-out on a spine opens the card with the real facts | YES |
| 8 | Flat box | YES |
| 9 | Face-out box | YES |
| 10 | No location row | YES |
| 11 | Swap to an owned expansion | YES |
| 12 | Language toggle to Dutch (titles and mechanics stay English) and back | YES |
| 13 | Reduced motion gives a fade with the box outlined | OPEN (owner unsure) |
| 14 | Desktop keyboard: one tab stop, arrows, Enter | YES |
| 15 | Screen reader skip link and five list entries | OPEN (owner will test later; a VoiceOver how-to was given) |
| 16 | Phone sheet drag closes (iPhone Safari) | YES |
| 17 | Safari fallback for the turn animation (iPhone Safari) | YES |
| 17 | Firefox ("firefox works as well as far as i can see") | YES |

Notes from the check:

- Rotation with a card open closes the card. This is the designed behaviour (crossing the phone breakpoint closes the card with a plain fade before the redraw). The owner was informed; it is noted as a possible later revisit if it annoys in daily use.
- The owner briefly reported card pictures missing on desktop. Read-only checks showed all 65 card cover files served 200 as valid WebP; the owner then confirmed it was their own network issue and the pictures show on desktop and iPhone. No change was made.

## Open human checks

These are not passed. The phase verifier should carry them into UAT:

1. Chrome for Android Back (button and gesture, once straight after opening and once after tapping inside the card, then a second Back leaving the site). The owner has no Android device, so this needs another tester or device.
2. iOS Safari: the second edge swipe leaves the site.
3. Desktop browser Back: one Back closes the open card and a second leaves the site.
4. Reduced motion: opening a card gives a fade with the box outlined (the owner was unsure).
5. Screen reader: skip link and five games-list entries read the game-night facts, in both languages.

The plan's acceptance wording asks for the device check on every listed item; it is therefore only partially met, and the phase's real-device Back requirement stays open until items 1 to 3 are confirmed.

## Deviations from Plan

### Auto-fixed Issues

**1. [Owner request] Dependabot updates folded in before merging**
- **Found during:** Task 2 (after the first green check run on `085ee76`)
- **Issue:** The owner asked to take the three pending Dependabot updates with this release.
- **Fix:** `36e30f1` raised xunit.v3 from 4.0.1 to 4.0.2 in all three test projects including the browser tests (unit and integration lock files identical to Dependabot's); `0f02ac7` raised `actions/upload-artifact` to 7.0.2 and `actions/download-artifact` to 8.0.2 in `release.yml`. Dependabot pull requests #16, #17 and #18 were closed.
- **Commits:** `36e30f1`, `0f02ac7`

**2. [Rule 1 - Bug] Race in the browser tests when closing a card**
- **Found during:** Task 2 (second check run)
- **Issue:** `browser-tests` failed once with `net::ERR_ABORTED` in a phone card-look test. Closing a card removes its history step with a traversal, and the next navigation started before that finished.
- **Fix:** A shared `WaitUntilTheCardStepIsGoneAsync` in the browser test base, used by all close helpers and replacing two private copies. Test code only; the re-run was green.
- **Files modified:** `Cabinet.BrowserTests` shared base and the tests that had private copies
- **Commit:** `ef6ee75`

**3. [Plan check correction] The ETag check used HEAD**
- **Found during:** Task 5
- **Issue:** The plan's `curl -sI ... | grep -ci '^etag:'` printed 0. HEAD returns no ETag, because the GET handlers set it. The check was wrong, not the site.
- **Fix:** The same check with GET and header dump printed 1. Recorded here so the plan text is not copied for a later release.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

Commits `36e30f1`, `0f02ac7` and `ef6ee75` exist on the milestone branch, and the merge commit `7edc720` was verified as the tag target during Task 3.

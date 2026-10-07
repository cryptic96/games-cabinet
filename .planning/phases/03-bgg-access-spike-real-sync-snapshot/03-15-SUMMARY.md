---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 15
subsystem: release
tags: [release, attestation, deploy, docs, ci]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: the finished sync, status line, sync now and live updates (03-07 to 03-14)
provides:
  - "README and setup guide describe the real sync and the BoardGameGeek env keys"
  - "Pull request 8 merged into main (merge commit 9c766f9); release v0.3.0 published, attested and installed on the container"
  - "Test factory waits for the in-memory host's own start; end-to-end runs get a state directory like systemd provides"
affects: [end-of-phase verification, go-public checklist]
tech-stack:
  added: []
  patterns:
    - "End-to-end runs outside systemd set STATE_DIRECTORY themselves"
key-files:
  created: []
  modified:
    - README.md
    - docs/lxc-setup.md
    - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
    - build/tests/package-release-e2e-test.sh
    - deploy/tests/cabinet-deploy-e2e-test.sh
    - .planning/todos/pending/2026-10-06-turn-off-prototype-mode-and-add-noindex-before-go-public.md
key-decisions:
  - "Owner approved the release path in chat (\"approved, go\") and kept the note wording that names BGG"
  - "Owner approved the deploy environment on GitHub before the orchestrator's own draft check; the published assets were verified right after publication instead"
requirements-completed: [SYNC-01, SYNC-02, SYNC-03, SYNC-04, SYNC-05, SYNC-08, LOC-02, SEC-05]
duration: about 50 min
completed: 2026-10-07
---

# Phase 3 Plan 15: Release and deployed check

The real sync shipped as v0.3.0: merged through pull request 8, tagged, attested, published by the owner and installed by the container's pull timer, which reported it healthy and completed its first production sync on its own.

## Task commits

- `fcde786` docs(03-15): README current state and guides, setup-guide step for the BoardGameGeek keys
- `6217237` fix(03-15): wait for the in-memory test host's own start before tests read it
- `a49a062` fix(03-15): give the end-to-end runs a state directory like systemd does
- `9c766f9` merge of pull request 8 into main (GitHub)
- Tag `v0.3.0` (annotated, noreply tagger) on `9c766f9`

## Owner decisions

- Task 1 path: approved in chat ("approved, go"), including push, pull request, merge commit and tag.
- Task 2 (publish): the owner approved the `deploy` environment on GitHub ("i approved"). This happened before the orchestrator had verified the draft; the same verification was run on the published assets immediately afterwards (below) and passed.
- Copy: the owner kept the existing note wording that names BGG (own-press notes and held-back notes).

## CI fixes on the pull request

1. **Empty Razor-page route list (test harness race).** `Every_razor_page_renders_the_linked_credit` failed in CI with no page routes. Both test hosts come from one deferred host builder that shares a single "started" signal; the serving host sets it, so starting the in-memory host returned before its program had mapped endpoints. Locally it never reproduced (probe runs under one CPU and under full-suite contention all saw the host started with both page routes). Fix: the factory waits for the in-memory host's own `ApplicationStarted` signal (30-second limit, clear timeout message). CI was green for the test suite after this.
2. **Release-layout end-to-end run aborted at start-up.** The packaged app ran in Production outside systemd, and the sync's storage location requires `Storage:Directory` or the service manager's `STATE_DIRECTORY`. Both end-to-end scripts now set `STATE_DIRECTORY` under their temporary root, as `StateDirectory=cabinet` does on the server. Both scripts pass locally with `CABINET_E2E=1`. Production was never affected (the unit sets the state directory).

## Release verification (sanitised)

- Merged range on `origin/main`: 100 commits, all author and committer emails are the noreply identity; the merge commit carries the profile display name with the GitHub web-flow committer (accepted earlier).
- Release run: test, package, attest and publish all succeeded; v0.3.0 is Latest and not a draft, with exactly three assets.
- `sha256sum -c`: OK. Attestation verified with no GitHub token in the environment (repo, signer workflow, source ref `refs/tags/v0.3.0`, no self-hosted runners); the same command with source ref `v0.2.0` is refused.
- Manifest: version `0.3.0`, commit `9c766f9`.
- `build/verify-published-release.sh v0.3.0`: all checks passed, including refusing a one-byte-modified copy.

## Container evidence (sanitised)

- Health: `Healthy`, version `0.3.0`, commit `9c766f9` (installed by the pull timer, no manual poll needed).
- Status after start-up: `lastResult` `changed`, `lastSyncedUtc` set (07:25:32 UTC on 2026-10-07), `heldBack` false, `running` false, cooldown window open until 10 minutes after the start.
- `cabinet-selfcheck`: 21 passed, 0 failed.
- The "not configured" journal check was not run: the completed sync with result `changed` already proves both keys are set, and it avoids reading production logs.
- Location count: not applicable; the signed-off outcome found locations not readable with the token alone.

## Go-public todo

Updated: the prototype part is done (off by default, development-only, disabled in Production); only the robots noindex remains, to be added to the shared layout.

## Deviations from Plan

- Task 1 steps 5 to 7 were run by the orchestrator instead of an executor, so CI waiting used the app's pull-request status instead of polling.
- The two CI fixes above were made on the branch with normal commits, as the plan allows.
- Publication preceded the draft verification (owner's choice of timing); verification was done on the published assets.

## Open items

- **Owner's final check (human-check of Task 3): APPROVED 2026-10-07.** The owner saw the real collection on desktop and phone; after a sync on the desktop the phone page updated automatically; the countdown shows during the shared 10-minute window. The Safari check is left for after the window. Original check text: on desktop and phone over the home network or VPN, the cabinet shows the real owned collection by title with expansions labelled, the "Synced ... ago" line and the footer credit, no sample links; after marking one more game owned on BGG and pressing Sync now, the new game appears on both devices without a reload, and a second press shows the countdown. Recorded at the end-of-phase review.
- WebKit/Safari strict-policy check with live updates (WebKit could not start in the scratch Playwright); the owner can check in Safari.
- Confirm that the reverse proxy passes WebSocket upgrades on the real route (live updates fall back to status polling if not).

## Self-Check: PASSED

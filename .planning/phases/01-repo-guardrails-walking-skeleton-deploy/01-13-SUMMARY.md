---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 13
subsystem: infra
tags: [release, attestation, lxc, provisioning, traefik, walking-skeleton]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: release workflow and deploy environment (plans 09, 12), installer and poll timer (plans 10, 11), provisioning and setup guide (plans 05, 11)
provides:
  - published immutable release v0.1.0, verified end to end from the public release page
  - provisioned server container running v0.1.0, installed by the poll timer with no GitHub credential on the host
  - LAN/VPN-only route serving the hello page over HTTPS with a valid certificate
affects: [rollback rehearsal]
tech-stack:
  added: []
  patterns:
    - "First provisioning used the latest release tag rather than main, because a release already existed when the container was built"
key-files:
  created: []
  modified: []
key-decisions:
  - "The owner approved the deploy environment before the orchestrator had verified the draft; the same verification ran on the published release instead and passed"
  - "Admin access for the orchestrator is a separate key-only account with passwordless sudo, pinned to the owner's workstation address, created in the container before provisioning; it is to be downgraded or revoked before go-public"
  - "The route file on the reverse-proxy host follows the existing LAN/VPN-only pattern (allow-list plus security headers); the first certificate attempt failed because the public DNS record did not exist yet, and re-adding the route after the record was created issued it"
patterns-established:
  - "Read-only checks against the container go through one shared SSH connection"
requirements-completed: [OPS-03, OPS-04, OPS-05]
duration: ~2h (including owner setup of the container, route and DNS)
completed: 2026-10-04
---

# Plan 01-13: The walking skeleton runs for real

Tag `v0.1.0` went through the release pipeline, was approved by the owner, and the container's poll timer found, verified, installed and health-checked it on its own. The hello page loads over HTTPS through the LAN-only route.

## Task 1: tag to attested release

- Annotated tag `v0.1.0` on the go-live merge commit `8772316` (tagger: the noreply identity), pushed by the owner's admin role.
- Release run: `build` and `publish` jobs both `success`; `publish` waited on the `deploy` environment.
- Release is published and immutable, with exactly `cabinet-0.1.0.zip`, `cabinet-0.1.0.zip.sha256` and `cabinet-0.1.0.zip.sigstore.json`.

## Task 2: owner decision

Approved in GitHub. Because the approval came before the draft check, the verification below ran on the published release.

## Release verification (no GitHub credential configured)

- `sha256sum -c`: OK.
- `gh attestation verify` with token variables unset and a throwaway config dir, `--signer-workflow .../release.yml`, `--source-ref refs/tags/v0.1.0`, `--deny-self-hosted-runners`: exit 0; certificate source ref `refs/tags/v0.1.0`, runner environment `github-hosted`. Negative control with `--source-ref refs/tags/v9.9.9`: exit 1.
- `release-manifest.json`: version `0.1.0`, commit `8772316a212a8aafacdbdfbe3895635e7c5a5641` = `git rev-parse v0.1.0^{commit}`.
- `build/verify-published-release.sh v0.1.0`: all PASS (three assets, checksum, attestation, commit on main, manifest, tampered copy refused).

## Task 3: the container

Created by the owner (unprivileged Ubuntu 24.04, nesting, 1 core, 1 GB, 8 GB) and provisioned by following `docs/lxc-setup.md`:

- Sections 2–4: cloned at `v0.1.0`, first run created `provision.conf`, second run with real values completed (packages, GitHub CLI with verified key, service account, env file, units, poll timer, default-drop firewall with SSH admitted only from the owner's workstation and the app port only from the reverse proxy).
- Section 5 (key-only SSH) was applied before provisioning as part of creating the admin account.
- Sections 6–7: the route file was written on the reverse-proxy host following the existing LAN/VPN-only pattern; the owner added the DNS record.

Poll journal (sanitised):

```
newer release v0.1.0 found, installing
artifact and provenance verified for v0.1.0 (commit 8772316a212a8aafacdbdfbe3895635e7c5a5641)
release 0.1.0 is active and healthy
```

- The first poll ran during provisioning and timed out reaching GitHub (the firewall was being loaded); outbound access was confirmed afterwards and the next scheduled poll installed the release. The "no published release yet" backstop path was not observable because the release was published before provisioning.
- `current` → `/opt/cabinet/releases/0.1.0`; loopback health: `{"status":"Healthy","version":"0.1.0","commit":"8772316a212a8aafacdbdfbe3895635e7c5a5641"}`.
- `cabinet-selfcheck`: 20 passed, 0 failed, including `PASS image-smoke 480x360 webp 45030 bytes` in the production sandbox.

## Human check

From the home network the hello page loads over HTTPS with a valid certificate and shows `Version 0.1.0 (8772316)`. Through the route, `/health` returns 404; HSTS, nosniff, frame deny and referrer policy headers are present.

## Deviations

1. Owner approved before the draft verification; verification ran on the published release (all PASS).
2. Release was published before the container existed, so the "no published release yet" poll path was not exercised.
3. First poll timed out during provisioning; the next timer run succeeded without intervention.
4. First certificate attempt failed (public DNS record created afterwards); re-adding the route triggered a successful issue.

## Self-Check: PASSED

---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 11
subsystem: infra
tags: [systemd, bash, provisioning, selfcheck, sandboxing, lxc, offline-tests]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: provisioning framework and host-guard harness (plan 05), image-smoke subcommand and loopback health JSON (plan 06), root installer and libraries (plan 10)
provides:
  - hardened cabinet.service with no database dependency
  - sandboxed oneshot cabinet-deploy-poll.service and a 3-minute-after-boot, 10-minute, 120-second-jitter timer
  - deploy/provision.d/40-services.sh that installs scripts, libraries and units, renders deploy.conf and enables the timer and app
  - deploy/bin/cabinet-selfcheck, an on-host PASS/FAIL check including a sandboxed image smoke
  - deploy/tests/sandboxing-test.sh (121 checks, offline)
  - docs/lxc-setup.md, the container creation and provisioning guide
affects: [rollback rehearsal, first release, server setup]
tech-stack:
  added: []
  patterns:
    - "Selfcheck is sourceable (main runs only when executed) so tests call individual checks with stand-in ss, nft, sshd, systemd-run, systemctl, pgrep and getent"
    - "Services module installs through one helper that leaves identical content alone and records written/unchanged, so a re-run restarts only what changed"
    - "Every selfcheck command output is captured into a variable before matching; no quiet grep behind a pipe"
key-files:
  created:
    - deploy/systemd/cabinet.service
    - deploy/systemd/cabinet-deploy-poll.service
    - deploy/systemd/cabinet-deploy-poll.timer
    - deploy/provision.d/40-services.sh
    - deploy/bin/cabinet-selfcheck
    - deploy/tests/sandboxing-test.sh
    - docs/lxc-setup.md
  modified: []
key-decisions:
  - "The guide, not provisioning, makes SSH key-only (a sshd drop-in named 00-key-only.conf), because the selfcheck requires it and provisioning does not touch the SSH server"
  - "The image smoke has no precondition on the dll existing: with no release the run fails and its output says why, which keeps the check simple and testable with a stand-in"
  - "The runner-directory check enumerates account home directories from the account database instead of a literal home glob, because the history scan rejects absolute local path shapes"
  - "A changed timer unit restarts the timer and a changed app unit restarts the app (otherwise start); an unchanged unit is never restarted"
patterns-established:
  - "Sandbox properties are asserted twice: as text in the script and as the arguments a stand-in systemd-run actually received"
requirements-completed: [OPS-04, OPS-05]
duration: 45min
completed: 2026-10-04
status: complete
actuals:
  tokens: 10300
  tasks: 3
  commits: 3
---

# Phase 1 Plan 11: Units, services module, selfcheck and container guide Summary

**Hardened app unit, a sandboxed poll service on a 10-minute jittered timer, a services module that installs and enables them, a sub-300-line on-host selfcheck that runs the deployed binary's image smoke inside a transient systemd sandbox, and a guide that takes an owner from `pct create` to a passing selfcheck.**

## Performance

- **Tasks:** 3 of 3 (task 1 was the tracer slice)
- **Commits:** 3 task commits plus the summary commit
- **Files created:** 7, nothing modified

## Accomplishments

- `cabinet.service` runs the deployed dll as the unprivileged `cabinet` user with the full hardening block, `StateDirectory=cabinet`, the env file and no ordering or dependency on anything but the network.
- `cabinet-deploy-poll.service` runs `cabinet-deploy poll` as a oneshot with strict system protection and `ReadWritePaths=/opt/cabinet /var/lib/cabinet-deploy` and nothing else; the timer fires 3 minutes after boot and every 10 minutes with up to 120 seconds of random delay.
- `40-services.sh` installs `deploy/bin/*` (755), `deploy/lib/*.sh` (644) and the units (644) only when content changed, renders `/etc/cabinet/deploy.conf` (600 root) with the repository from the provisioning config, turns on unattended security updates, enables the timer and the app, and starts the app only when `/opt/cabinet/current` exists. All functions load in library mode without calling `systemctl`.
- `cabinet-selfcheck` (285 lines) prints PASS or FAIL for: app and timer active and enabled; modes and owners of six paths; something on 5080 and 5081 on loopback only; default-drop input chain; health `Healthy` at the version the `current` link points at; SSH password and keyboard-interactive off; no GitHub token variable, gh credential file, runner unit, runner process or runner directory; and the image smoke. `--help` works without root; running without root exits 1.
- The image smoke runs `/usr/bin/dotnet Cabinet.Service.dll image-smoke` through `systemd-run --pipe --wait --collect` as `cabinet` from `/opt/cabinet/current/app` with the twelve sandbox properties (no new privileges, strict system, no home, private tmp and devices, kernel and cgroup protections, no namespaces, personality locked, empty capability set, AF_UNIX only) and passes only on a line starting `PASS image-smoke `.
- `docs/lxc-setup.md` covers what is reachable from where, prerequisites and twelve steps from container creation (unprivileged, nesting, 1 core, 1024 MB, 8 GB) through two provisioning runs, key-only SSH, the Traefik route, DNS, repository settings, the first release, the selfcheck, logs and re-provisioning, plus what the guide never does.

## Task Commits

1. **Task 1 (tracer): app unit, sandboxed poll unit and timer, services module** - `2c13bb6`
2. **Task 2: selfcheck with sandboxed image smoke** - `e65bbe4`
3. **Task 3: container creation and provisioning guide** - `0e2e9c2`

## Tracer feedback gate

The tracer's verify (`bash deploy/tests/sandboxing-test.sh && bash deploy/tests/provision-logic-test.sh`) passed before any expansion work and was re-run after each later task. `systemd-analyze verify` on copies of the three units in a temporary directory reported no syntax problems; its only complaint was that the installer binary is not present on the workstation, as expected.

## Verification

- `bash deploy/tests/sandboxing-test.sh`: 121 checks, 0 failures, host-guard call list empty. `provision-logic-test.sh` 47 and `render-templates-test.sh` 27 checks, 0 failures.
- Sensitivity check: temporarily changing `PrivateDevices=yes` to `no` and the firewall pattern from `policy drop` to `policy accept` in the selfcheck made exactly the matching static and behavioural checks fail; the edit was reverted.
- Behavioural checks with stand-ins: image smoke (PASS line, FAIL line, empty output, arguments received), listeners (good, ops beyond loopback, nothing on 5080), firewall (drop, accept), SSH (off, password on), runner (none, directory in an account home, process, unit).
- Acceptance greps: exactly one `ReadWritePaths` line equal to the required value; one `OnUnitActiveSec=10min` and one `RandomizedDelaySec=120`; zero database terms in the app unit; the selfcheck mentions `image-smoke` three times and `readlink -f /opt/cabinet/current` once and is 285 lines.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all PASS. `build/scan-history.sh`: all six checks PASS. The guide contains only the project's public repository URL, `192.0.2.0/24` and `198.51.100.0/24`.
- Not run: the units, the services module and the selfcheck have not executed on a real container (nothing may be installed or started on the workstation). The live selfcheck in the first-release step is where the sandbox properties inside an unprivileged container are confirmed.

## Decisions Made

See `key-decisions` in the frontmatter.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 2 - Missing critical functionality] Guide makes SSH key-only**
- **Issue:** the selfcheck fails unless the SSH server reports password and keyboard-interactive authentication off, but no provisioning module configures the SSH server, so a host built from the guide alone could never pass.
- **Fix:** step 5 of the guide installs a `00-key-only.conf` drop-in (the `00` prefix makes it win over later drop-ins) and reloads the service, and says the selfcheck fails until both settings are off.
- **Files modified:** `docs/lxc-setup.md`
- **Commit:** `0e2e9c2`

**2. [Rule 3 - Blocking] Pre-commit personal-data guard and history scan rejected two constructs**
- **Issue:** the guard blocked a test line that listed database product names (to prove the app unit has none), and the history scan flagged a literal home-directory glob in the runner-directory check as an absolute local path.
- **Fix:** the unit test now asserts the absence of any `Requires`, `Requisite`, `BindsTo`, `PartOf`, `Upholds`, `Wants` line and of any `After=` on a service unit instead of naming products; the runner check enumerates home directories from the account database. The task 2 and 3 commits were recreated with a soft reset on the unpushed agent branch so the flagged text exists in no commit.
- **Files modified:** `deploy/tests/sandboxing-test.sh`, `deploy/bin/cabinet-selfcheck`
- **Commit:** `2c13bb6`, `e65bbe4`

### Additions beyond the plan text

- The selfcheck is sourceable and the test exercises its checks with stand-ins, so the selfcheck's own logic (not only the text of its sandbox arguments) is covered; the plan asked only for the static assertions and `--help`.
- The services module restarts a changed timer or app unit and never restarts an unchanged one; the plan only specified enable and conditional start.
- The guide adds `--swap 512` and `--ssh-public-keys` to the container creation command and an SSH alias example.

## Authentication Gates

None.

## Known Stubs

None. The guide's `{braces}` values and example addresses are intentional placeholders.

## Threat Flags

None. All seven threats in the plan's register have their mitigation implemented: poll unit write allow-list and sandbox (asserted), app unit unprivileged with an empty capability set and `StateDirectory` as the only writable path, image smoke in a transient sandbox as the service user (asserted statically and by the arguments received), SSH, credential and runner failures in the selfcheck (exercised with stand-ins), placeholders-only docs behind the secrets lint, and every check capturing output before matching.

## Issues Encountered

- `shellcheck` is not installed on the host; the lint runner's container was used for every shell check.
- Provisioning leaves the SSH server alone, so the guide carries the key-only step (see deviations); an owner who skips it will see the two SSH selfcheck lines fail.
- The guide links `docs/releasing.md` and `docs/github-repository-settings.md`, which are produced by the release workflow work running in parallel and may not be on this branch until the waves are merged.

## Next Phase Readiness

- The rollback rehearsal can run `cabinet-selfcheck` before and after a rolled-back release; the selfcheck's version comparison uses the same `current` link the installer swaps.
- The first live selfcheck on a real container is the confirmation that the transient sandbox works under nesting; if it does not, only the property list in `check_image_smoke` needs trimming.

## Self-Check: PASSED

- All 7 created files exist on disk.
- Commits `2c13bb6`, `e65bbe4` and `0e2e9c2` exist in the branch history.
- The three offline tests pass; `build/lint.sh` and `build/scan-history.sh` pass.

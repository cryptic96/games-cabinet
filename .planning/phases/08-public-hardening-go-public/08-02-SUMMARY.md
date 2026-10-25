---
phase: 08-public-hardening-go-public
plan: 02
subsystem: infra
tags: [systemd, nftables, cgroups, provisioning, selfcheck, shell]
requires: []
provides:
  - "cabinet.service resource limits (starting values) and workstation non-concurrent GC"
  - "nftables output chain that rejects new connections to configured private ranges, with DNS and replies exempt"
  - "provision.conf keys CABINET_EGRESS_RESOLVERS, CABINET_EGRESS_BLOCKED_RANGES, CABINET_EGRESS_BLOCKED_RANGES6 with validators"
  - "cabinet-selfcheck check_limits, check_egress, check_dns"
affects: [08-10, 08-12, 08-15, 08-16, 08-17, 08-20]
tech-stack:
  added: []
  patterns:
    - "typed render branches per template token, validated by shape before anything is rendered"
    - "selfcheck derives its probe address at run time from the loaded rule, never a literal"
key-files:
  created: []
  modified:
    - deploy/systemd/cabinet.service
    - deploy/bin/cabinet-selfcheck
    - deploy/tests/sandboxing-test.sh
    - deploy/nftables/cabinet.nft.in
    - deploy/provision.sh
    - deploy/provision.d/50-firewall.sh
    - deploy/provision.conf.example
    - deploy/tests/render-templates-test.sh
    - deploy/tests/provision-logic-test.sh
key-decisions:
  - "Unit limits ship as starting values (MemoryHigh 450M, MemoryMax 600M, MemorySwapMax 0, CPUQuota 80%, CPUWeight 50, TasksMax 512, LimitNOFILE 4096); the measurement plans replace them"
  - "Outbound block rejects rather than drops, so a mistaken block fails visibly"
  - "check_limits skips a relation check when the value it depends on already failed, so each defect counts once"
patterns-established:
  - "Selfcheck helpers that read the host (container_memory_kib, container_swap_kib, container_cores, egress_probe, resolve_name) are small functions the offline test overrides"
requirements-completed: []
duration: 1h
completed: 2026-10-25
status: complete
actuals:
  tokens: 13300
  tasks: 3
  commits: 3
---

# Phase 8 Plan 02: The container's own walls in the repository Summary

**Resource caps and GC mode on the app unit, plus a provisioned nftables output chain that refuses new connections to the home network, each proven by a selfcheck check and offline tests.**

## Performance

- **Tasks:** 3 of 3
- **Files modified:** 9
- **Verification:** the three deploy test scripts pass; `build/lint.sh repo-rules shell secrets script-tests` passes.

## Accomplishments

- `cabinet.service` carries `MemoryHigh=450M`, `MemoryMax=600M`, `MemorySwapMax=0`, `CPUQuota=80%`, `CPUWeight=50`, `TasksMax=512`, `LimitNOFILE=4096`, and `DOTNET_gcServer=0` / `DOTNET_gcConcurrent=0`. `MemoryMax` is also what lets the .NET runtime see a memory limit at all inside the container.
- `cabinet-selfcheck check_limits` compares `systemctl show` values with the directives of the loaded unit file (missing directive or `infinity` fails), then reports what lxcfs shows (memory, swap, cores) and fails when `MemoryMax` is above container memory minus the 128 MiB margin (`LIMIT_MEMORY_MARGIN_MIB`) or the CPU quota reaches the container's cores times 100 %.
- `cabinet.nft.in` output chain: `policy accept`, then loopback, established/related, ICMPv6, DNS (UDP and TCP 53) to the configured resolvers, then `reject with icmpx type admin-prohibited` for the configured IPv4 and IPv6 ranges.
- `provision.sh` gains the three keys, `provision_validate_ipv4_list`, `provision_validate_ipv6_cidr`, `provision_validate_ipv6_cidr_list`, extended `provision_validate_conf` and typed render branches; `50-firewall.sh` passes the three values and still dry-runs with `nft -c -f` before loading.
- `provision.conf.example` ships a placeholder resolver (so provisioning refuses it as it stands) and the generic private, shared, link-local and IPv6 local blocks, with a note to append the home network's own global IPv6 prefix.
- `check_egress` confirms both reject rules sit in the output chain and that a probe to the first host of the first blocked IPv4 network is refused within 2 seconds; `check_dns` confirms name resolution and points at `CABINET_EGRESS_RESOLVERS` on failure. Neither prints an address.

## Task Commits

1. **Task 1 (tracer): unit limits and check_limits** - `190dfdc`
2. **Task 2: outbound block rendered from provision.conf** - `d5e9402`
3. **Task 3: check_egress and check_dns** - `c278551`

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Locale-dependent duration parsing in check_limits**
- **Found during:** Task 1 (RED/GREEN run)
- **Issue:** `awk` under a comma-decimal locale read `1.2s` as 1 s, so a 120 % quota looked like exactly one core.
- **Fix:** `LC_ALL=C awk` in `duration_to_ms`.
- **Files modified:** `deploy/bin/cabinet-selfcheck`
- **Commit:** `190dfdc`

**2. [Rule 3 - Blocking] Test fixtures tripped the repository's own secret scan**
- **Found during:** Task 2 (lint run)
- **Issue:** negative test values (a malformed private IPv4 range and a malformed unique-local IPv6 range) matched the private-range gitleaks rules.
- **Fix:** malformed examples use documentation prefixes (`192.0.2.0/33`, `2001:db8::1::2/64`, `2001:db8:::1/64`).
- **Files modified:** `deploy/tests/render-templates-test.sh`, `deploy/tests/provision-logic-test.sh`
- **Commit:** `d5e9402`

**3. [Rule 3 - Blocking] The test's APP_UNIT variable is overwritten by sourcing the selfcheck**
- **Found during:** Task 1
- **Issue:** the selfcheck declares `APP_UNIT=cabinet.service`, so fixtures copied from `$APP_UNIT` after sourcing pointed at nothing.
- **Fix:** fixtures copy `${DEPLOY_DIR}/systemd/cabinet.service` directly.
- **Commit:** `190dfdc`

### Judgment calls

- **Tracer gate:** the tracer's verify (`bash deploy/tests/sandboxing-test.sh`) passed and was re-run before the expansion tasks. Auto mode is off, so the interactive rule would have returned a `checkpoint:human-verify` after the tracer; the project's `human_verify_mode` is `end-of-phase` and the tracer's verification is fully automated, so the plan continued without stopping. Surfacing this so the orchestrator can confirm.
- Test-only extras beyond the plan: the real `egress_probe` and `first_blocked_host` are tested with a stubbed `timeout`, and more IPv6 validator edge cases than listed.

## Issues Encountered

None open. Per instructions, no browser tests were run and no real host or container was touched.

## Known Stubs

None.

## Threat Flags

None. All new surface (the output chain, the three config keys) is covered by the plan's threat register.

## Next Phase Readiness

- Plan 08-12 applies the walls on the container: the three `CABINET_EGRESS_*` keys must be set in the server's `provision.conf` before provisioning is re-run, and `cabinet-selfcheck` then proves limits, egress and DNS.
- Plan 08-10 documents the outbound block's side effects; plan 08-17 replaces the starting unit numbers with measured ones.

## Self-Check: PASSED

- Commits `190dfdc`, `d5e9402`, `c278551` exist on the branch.
- All nine modified files exist; the three deploy test scripts and the pinned lint command pass.

---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 08
subsystem: testing
tags: [xunit, integration-tests, webapplicationfactory, kestrel, health, configuration]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: hello page, loopback ops health, BuildInfo and OpsEndpoint (plan 03); solution file and unit-test project (plan 06)
provides:
  - Cabinet.IntegrationTests project in Cabinet.slnx with a two-host factory serving real loopback sockets
  - Integration tests for ops-only health, health on a fresh host and the version-bearing hello page with its stylesheet
  - Unit tests for BuildInfo parsing, the loopback-only ops listener rule and secret-free committed configuration
affects: [rollback-rehearsal, ci, later-integration-tests]
tech-stack:
  added: [Microsoft.AspNetCore.Mvc.Testing 10.0.12]
  patterns:
    - "Factory builds a throwaway in-memory host plus a real Kestrel host and copies the real addresses onto the test host so clients talk to genuine sockets"
    - "Tests pick their own free loopback ports and never assert the committed ops port"
key-files:
  created:
    - Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj
    - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
    - Cabinet.IntegrationTests/HealthEndpointTests.cs
    - Cabinet.IntegrationTests/HelloPageTests.cs
    - Cabinet.IntegrationTests/packages.lock.json
    - Cabinet.UnitTests/BuildInfoTests.cs
    - Cabinet.UnitTests/Hosting/OpsEndpointTests.cs
    - Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs
  modified:
    - Cabinet.slnx
    - Cabinet.UnitTests/Cabinet.UnitTests.csproj
    - Cabinet.UnitTests/packages.lock.json
key-decisions:
  - "Dropped the reference factory's database, certificate, scheduler and log-capture plumbing; the factory only owns two loopback ports and the real-socket host"
  - "Added a second test to the configuration suite proving the secret detector flags a secret-shaped value, so the scan cannot pass vacuously"
patterns-established:
  - "Contract tests configure Kestrel:Endpoints:Web:Url and Kestrel:Endpoints:Ops:Url in memory with their own ports"
requirements-completed: [OPS-04]
duration: 15min
completed: 2026-10-04
status: complete
actuals:
  tokens: 7000
  tasks: 2
  commits: 3
---

# Phase 1 Plan 08: Contract tests for health, hello page and configuration Summary

**Fast xunit.v3 tests that boot the real host on two free loopback sockets and pin ops-only health JSON with version, a public 404 for `/health`, the version-bearing hello page with its fingerprinted stylesheet, BuildInfo parsing, the loopback-only ops rule and secret-free committed configuration.**

## Performance

- **Tasks:** 2 of 2 complete (tracer task, then the unit-test task)
- **Commits:** 2 task commits plus this summary
- **Tests:** 26 passing across both projects (4 integration, 22 unit including the earlier image-smoke tests)

## Accomplishments

- `CabinetWebApplicationFactory` derives from `WebApplicationFactory<Program>`, picks two free loopback ports in its constructor, sets environment `Testing`, injects the two listener URLs through in-memory configuration and starts a real Kestrel host so both listeners are genuine sockets. It exposes `CreatePublicClient()` and `CreateOpsClient()`.
- `HealthEndpointTests`: health is 200 `application/json` on the ops port and 404 on the public port; on a fresh host with nothing on disk it reports `Healthy` with non-empty `version` and `commit`.
- `HelloPageTests`: `/` returns 200 HTML containing `Version <running version>` (compared with the version health reports), and the stylesheet link is a fingerprinted `/css/site.<hash>.css` that returns 200 `text/css`.
- `BuildInfoTests`, `OpsEndpointTests`, `CommittedConfigurationTests` cover the parsing cases, the loopback-only listener rule (loopback accepted; wildcard, documentation-range and missing values rejected) and the absence of non-empty secret-shaped values in every committed `appsettings*.json`.
- No test source mentions the committed ops port (recursive grep over `*.cs` in both test projects is empty); unit tests use ports 6080 and 6081, integration tests use ports chosen at run time.

## Task Commits

1. **Task 1 (tracer): integration tests on real loopback sockets** - `677ba6b` (test)
2. **Task 2: unit tests for version parsing, ops rule and committed configuration** - `3c27435` (test)

## Tracer feedback gate

After the tracer commit, `dotnet test --solution Cabinet.slnx` passed (10 tests) and `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh` passed, so the locked-mode packaging restore accepts the new integration project's lock file. The e2e test was re-run after the second task and still passes.

## Verification

- `dotnet build Cabinet.slnx -c Release`: 0 warnings, 0 errors.
- `dotnet test --solution Cabinet.slnx`: 26 of 26 pass (integration project 4 tests, 0 failed).
- `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh`: passes after each task.
- `git status` for lock files is clean after the test, build and packaging runs.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all PASS.
- `build/scan-history.sh`: all checks PASS.

## Deviations from Plan

None - plan executed exactly as written. Notes:

- The app contract already existed from earlier plans, so the new tests passed on first run; there was no red phase to commit separately. Each task is a single `test` commit that pins existing behaviour.
- The integration project needed no runtime-identifier declaration: it is not referenced by the published project, and the locked-mode packaging restore accepted its lock file unchanged.

## Issues Encountered

- The sandbox guard rejects git commands combined in compound or computed-variable forms, so commands were issued one at a time. No effect on results.

## Known Stubs

None.

## Threat Flags

None. The tests add no network surface beyond loopback sockets that exist only for the duration of a test run.

## Next Phase Readiness

The rollback rehearsal can change the committed ops port without turning CI red. Later integration tests can reuse `CabinetWebApplicationFactory`; it currently takes no constructor arguments, so a feature that needs extra configuration or service overrides will add optional parameters as the reference did.

## Self-Check: PASSED

- All created files are present and tracked on the worktree branch; commits `677ba6b` and `3c27435` exist.

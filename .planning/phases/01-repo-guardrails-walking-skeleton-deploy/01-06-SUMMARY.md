---
phase: 01-repo-guardrails-walking-skeleton-deploy
plan: 06
subsystem: infra
tags: [dotnet, packaging, release, skiasharp, webp, xunit, e2e]
requires:
  - phase: 01-repo-guardrails-walking-skeleton-deploy
    provides: hello page, loopback ops health and lock-file conventions (plan 03)
provides:
  - Cabinet.slnx solution listing domain, repository, service and unit-test projects
  - build/package-release.sh producing cabinet-X.Y.Z.zip with app/, optional deploy/, release-manifest.json and a sha256
  - image-smoke subcommand proving SkiaSharp decode, resize and WebP encode inside the release binary
  - Cabinet.Repository and Cabinet.UnitTests projects
  - Package-extract-run end-to-end test gated by CABINET_E2E=1
affects: [release-workflow, installer, container-selfcheck, contract-tests, rollback-rehearsal]
tech-stack:
  added: [SkiaSharp 4.153.1, SkiaSharp.NativeAssets.Linux.NoDependencies 4.153.1, xunit.v3 4.0.1, FluentAssertions 8.11.0]
  patterns:
    - "Locked-mode restore of the solution, then publish with the same linux-x64 runtime identifier"
    - "Every project that is published or referenced by a published project declares RuntimeIdentifiers so lock files carry the linux-x64 section for both plain builds and packaging"
    - "Subcommand handled before the web host is built; prints PASS or FAIL lines and returns an exit code"
key-files:
  created:
    - Cabinet.slnx
    - Cabinet.Repository/Cabinet.Repository.csproj
    - Cabinet.Repository/Images/ImageSmoke.cs
    - Cabinet.Repository/packages.lock.json
    - Cabinet.UnitTests/Cabinet.UnitTests.csproj
    - Cabinet.UnitTests/Images/ImageSmokeTests.cs
    - Cabinet.UnitTests/packages.lock.json
    - Cabinet.Service/appsettings.Production.json
    - build/package-release.sh
    - build/tests/package-release-e2e-test.sh
  modified:
    - Cabinet.Domain/Cabinet.Domain.csproj
    - Cabinet.Domain/packages.lock.json
    - Cabinet.Service/Cabinet.Service.csproj
    - Cabinet.Service/Program.cs
    - Cabinet.Service/packages.lock.json
key-decisions:
  - "Declared RuntimeIdentifiers linux-x64 in the domain and repository projects (as the service already did) so the publish restore and a plain build agree on the lock files; without it the domain lock file flipped between runs"
  - "Verify decodes with SKBitmap.Decode and converts its ArgumentNullException on undecodable input into InvalidOperationException, because the Skia binding throws instead of returning null"
  - "ImageSmoke exposes EncodeSample and Verify beside Run so tests can inspect the encoded bytes and the failure paths without a mocked decoder"
patterns-established:
  - "Packaging restores the solution in locked mode rather than a bare directory restore"
  - "End-to-end tests configure their own free loopback ports and never assert the committed ops port"
requirements-completed: [OPS-03, OPS-04, OPS-05]
duration: 25min
completed: 2026-10-04
status: complete
actuals:
  tokens: 14000
  tasks: 2
  commits: 3
---

# Phase 1 Plan 06: Release packaging and image smoke Summary

**A locked-mode packaging script turns the hello-page app into a deterministic zip that runs from a symlinked release directory, and an `image-smoke` subcommand proves SkiaSharp (with its native library) can decode, resize and WebP-encode inside that same artefact.**

## Performance

- **Tasks:** 2 of 2 complete (tracer task, then the test-first image smoke task)
- **Commits:** 3 (tracer, RED tests, GREEN implementation)
- **Files created:** 10, modified: 5

## Accomplishments

- `build/package-release.sh --version X.Y.Z --commit <40-hex> --output DIR` validates strict semver and a 40-hex commit, restores the solution in locked mode, publishes framework-dependent for linux-x64 with the version and source revision stamped in, writes `release-manifest.json` as `{version, commit}`, copies `deploy/` without its `tests` directory, and builds a deterministic `cabinet-X.Y.Z.zip` plus `.sha256`. The reference's tool restore, EF bundle and migrations manifest are gone.
- The end-to-end test packages `0.0.1`, extracts it behind a `current` symlink, runs it on two free loopback ports and asserts: ops health JSON is `Healthy` with the packaged version and full commit, `/` answers 200 and shows the version, the stylesheet link is fingerprinted and loads, and `/health` on the public port is 404. It also asserts the zip carries `app/libSkiaSharp.so` and `app/SkiaSharp.dll` and that `image-smoke` prints `PASS image-smoke 480x360 webp N bytes`.
- `dotnet Cabinet.Service.dll image-smoke` runs two synthetic round trips (a flat 640x480 fill resized to 240x180, and seeded 2400x1800 noise through PNG decode, resized to 480x360) and exits 1 with `FAIL image-smoke <reason>` on any mismatch. It reads no input and writes nothing. Observed output from the packaged release: `PASS image-smoke 480x360 webp 45030 bytes`.
- Six unit tests cover the smoke result, the `RIFF`/`WEBP` markers, decode-back dimensions, and the three failure paths (missing markers, undecodable payload, wrong dimensions). Written and committed failing first.

## Task Commits

1. **Task 1 (tracer): packaged zip runs from a symlinked directory** - `c92a9c7` (feat)
2. **Task 2 RED: failing image smoke tests** - `de60fb7` (test)
3. **Task 2 GREEN: image-smoke subcommand with SkiaSharp** - `3c328ac` (feat)

## Tracer feedback gate

The tracer's verification (build with zero warnings, then the package-extract-run end-to-end test) passed before expansion; the end-to-end test was re-run after the second task and still passes.

## Decisions Made

- **Runtime identifier in every packaged project.** `dotnet publish -r linux-x64` adds a `net10.0/linux-x64` section to the lock file of every referenced project, and a following plain `dotnet build` removes it again, so the domain lock file flipped between runs. Declaring `RuntimeIdentifiers` linux-x64 in the domain and repository projects (the service already did) makes both flows produce the same lock file. Verified: after the end-to-end run and a plain build, a checksum of all four lock files is unchanged and `git status` for lock files is clean.
- **Restore the solution, not the directory.** `dotnet restore Cabinet.slnx --locked-mode` is explicit and also covers the test project.
- **Skia decode failure mode.** `SKBitmap.Decode(byte[])` throws `ArgumentNullException` for undecodable input rather than returning null; the helper normalises both to `InvalidOperationException` with a plain message.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Lock file flip-flop between packaging and plain builds**
- **Found during:** Task 1
- **Issue:** The publish restore rewrote `Cabinet.Domain/packages.lock.json` with a linux-x64 section that a plain build then removed, violating the "lock files unchanged" criterion.
- **Fix:** Declared `RuntimeIdentifiers` in the domain project (and later the repository project), regenerated lock files, committed them. The plan anticipated this ("declare the runtime identifier in the project files").
- **Files modified:** `Cabinet.Domain/Cabinet.Domain.csproj`, `Cabinet.Domain/packages.lock.json`
- **Commit:** `c92a9c7`

**2. [Rule 1 - Bug] Undecodable payload raised ArgumentNullException**
- **Found during:** Task 2 (GREEN)
- **Issue:** The RED-phase test for an undecodable payload exposed that the Skia binding throws instead of returning null, which would have bypassed the `FAIL image-smoke` reporting path.
- **Fix:** Wrapped decoding in a helper converting it to `InvalidOperationException`.
- **Files modified:** `Cabinet.Repository/Images/ImageSmoke.cs`
- **Commit:** `3c328ac`

**Additions beyond the plan text (no behaviour change to the contract):** `ImageSmoke.EncodeSample` and `ImageSmoke.Verify` are public so the tests can read the encoded bytes and exercise failure paths; `Run` still returns `(Width, Height, Bytes)` as specified.

## Issues Encountered

None beyond the deviations above. `dotnet list package --vulnerable --include-transitive` reports no vulnerable packages for any project, and no project references a `SixLabors` package.

## Known Stubs

None.

## Threat Flags

None. No new network surface; `image-smoke` decodes only in-process synthetic data.

## Verification

- `dotnet build Cabinet.slnx -c Release`: zero warnings, zero errors.
- `dotnet test --solution Cabinet.slnx`: 6 of 6 pass.
- `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh`: passes (health JSON `Healthy`, version `0.0.1`, full 40-hex commit).
- `build/lint.sh`: repo-rules, shell, secrets and script-tests all PASS.
- `build/scan-history.sh`: all checks PASS.

## Self-Check: PASSED

- Created files exist: solution, repository project and smoke class, unit-test project and tests, production settings, packaging script, end-to-end test (all present on disk).
- Commits `c92a9c7`, `de60fb7` and `3c328ac` exist in the branch history.

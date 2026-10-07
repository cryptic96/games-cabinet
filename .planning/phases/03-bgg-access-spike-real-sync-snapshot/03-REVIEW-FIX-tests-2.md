---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07T00:00:00Z
review_path: .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
iteration: 2
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 3: Code Review Fix Report (tests, scripts and CI, round 2)

**Fixed at:** 2026-10-07
**Source review:** .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
**Iteration:** 2
**Scope:** the two leftovers from round 1 (WR-11 pacer sleeps, WR-15 sequential disposal) and IN-10 to IN-14.

**Summary:**
- Findings in scope: 7
- Fixed: 7
- Skipped: 0

Verification ran in the isolated worktree for this run. After each C# change: `dotnet test --solution Cabinet.slnx` (814 tests, all passing) and `build/lint.sh` (all five checks passing). Script changes: `python3 -I build/bgg-access-check.py --self-test`, `bash build/tests/bgg-access-check-test.sh` (17 cases), `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh`, `CABINET_E2E=1 bash deploy/tests/cabinet-deploy-e2e-test.sh`, `build/lint.sh workflows`. BGG was never contacted: the new guard cases stub the request loop and block sockets.

## Fixed Issues

### WR-11 (leftover): RequestPacerTests used real-time sleeps

**Files modified:** `Cabinet.UnitTests/Bgg/RequestPacerTests.cs`
**Commit:** 3bbdef0
**Applied fix:** The blind 100 ms `StaysPending` sleep is gone. Every test wraps its fake clock in the existing `TimerCountingClock`, asks for the turn through a helper that waits (bounded) until the pacer has armed its delay timer, and then checks `IsCompleted` directly against the fake clock; once the timer is armed, only advancing the fake clock past its due time can complete the task, so the negative is deterministic. Where the second request is queued behind a held lease (no timer exists) the pending state is certain by construction and is asserted directly. The positive wait is now `Task.WaitAsync` with a 10 s bound instead of a polling loop. The cancellation test also asserts the abandoned wait via `Awaiting(...).ThrowAsync<OperationCanceledException>()`, which removes a race between the semaphore being released and the abandoned task being marked cancelled. The wall-clock-step test keeps its stepping clock, wrapped in the counting clock.

### WR-15 (leftover): header test disposed both hosts in one loop

**Files modified:** `Cabinet.IntegrationTests/SyncStatusLineTests.cs`
**Commit:** 814955d
**Applied fix:** `The_header_never_names_the_data_source` now builds and disposes the two hosts one after the other in separate `await using` blocks, each calling a shared local helper that reads the page and asserts on the header. A failure in the first no longer leaves the second alive, and the second is not built until the first is gone.

### IN-10: hand-rolled `ManualTimeProvider`

**Files modified:** none this round
**Status:** already resolved in round 1 (the class was deleted and `BggXmlTests` uses `FakeTimeProvider`). Confirmed by search: no `ManualTimeProvider` remains in either test project.

### IN-11: duplicated test helpers

**Files modified:** `Cabinet.IntegrationTests/CollectionFidelityTests.cs`, `SyncPipelineTests.cs`, `SecretsStayServerSideTests.cs`, `Infrastructure/SyncHarness.cs`; `Cabinet.UnitTests/Live/LiveSettingsTests.cs`, `Layout/LayoutSettingsTests.cs`, `Sync/SyncSettingsTests.cs`, `Sync/BggSettingsTests.cs`, `Prototype/SampleCatalogTests.cs`, `Configuration/CommittedConfigurationTests.cs`; new `Cabinet.UnitTests/Infrastructure/RepositoryPaths.cs`, `TemporaryDirectory.cs`, `TestEnvironment.cs`
**Commit:** c4f0ad5 (together with IN-10 confirmation and IN-12)
**Applied fix:** Integration project: the private `TemporaryDirectory` copies and the three private `WaitUntil` loops are removed in favour of `Infrastructure/TemporaryDirectory` and `SyncHarness.WaitUntil`; the fidelity test's own change-detection loop now uses the shared wait. The shared deadline is 15 s (the longest of the former 10, 15 and 15 s) so no test got stricter. Unit project: five copies of `FindServiceDirectory` became one `RepositoryPaths.ServiceDirectory()`, and the private `TemporaryDirectory` became a shared one. Only helper definitions and their call sites changed; no test method in the back-end fixer's classes was reordered or rewritten. The two pacer doubles (`ImmediatePacer` in the unit project, `NoWaitPacer` in the integration project) are one per project already; the review's pairing is across two projects, so there is nothing further to merge inside a project and they were left as they are. The store tests (`SnapshotStoreTests`, `SyncStateStoreTests`) still build their own temporary paths in fields; they are in the back-end fixer's area and were not touched.

### IN-12: `HostingEnvironment` from an internal namespace

**Files modified:** `Cabinet.UnitTests/Sync/BggSettingsTests.cs`, `Cabinet.UnitTests/Prototype/SampleCatalogTests.cs`, new `Cabinet.UnitTests/Infrastructure/TestEnvironment.cs`
**Commit:** c4f0ad5
**Applied fix:** A small `TestEnvironment : IHostEnvironment` replaces `Microsoft.Extensions.Hosting.Internal.HostingEnvironment`; the internal `using` is removed from both files.

### IN-13: guard made a short username unusable; `run_check` credential list untested

**Files modified:** `build/bgg-access-check.py`, `build/tests/bgg-access-check-test.sh`
**Commit:** b2b1dec
**Applied fix:** Status: fixed, requires human verification of the minimum length (four characters, the same threshold `contains_value` already uses).
- The username is no longer in the credential list. A new `contains_name` matches it case-insensitively: anywhere in the report when it is four characters or longer, but only as a whole word when shorter, so a short name no longer withholds every report by appearing inside "status" or "items". The token, the contact address and the fixed wrong token stay in a list (`guarded_credentials`) that still matches as plain substrings, so those keep failing closed.
- `run_check` now builds its credential list through `guarded_credentials`, so a test can pin it. The withheld branch prints one extra hint line saying what kinds of value can trigger it; nothing sensitive is in it.
- Self-test: cases for a short name inside words (passes), a short name as a whole word and in another case (fires), a long name inside a longer word (fires), a short token inside a longer word (fires), and the exact credential list.
- Shell test: a "real run" section loads the script as a module, replaces only the request loop with a stub that returns a chosen line, blocks sockets, and calls `main --run`. It asserts exit 0 for a clean report, exit 4 with the hint and no secret in the output for the token, the username, a plain contact address and the fixed wrong token, the short-username cases, and a short token inside a longer word. Dropping any entry from the guarded list, or the username from the names, fails one of these. (The contact case uses a contact value without a URL scheme, because a real contact URL would trip the separate URL-marker rule and not prove the list.)

### IN-14: package end-to-end test has no self-gate; CI runs twice on pull-request branches

**Files modified:** `build/tests/package-release-e2e-test.sh`, `.github/workflows/ci.yml`
**Commit:** 986e2eb
**Applied fix:** The package script now exits cleanly with a message unless `CABINET_E2E=1`, matching the installer end-to-end script (CI already sets the variable; the lint runner already skips it). The workflow's `push` trigger is limited to `main`; `pull_request` against `main` is unchanged, as are the job names and the ubuntu-26.04 labels. `build/lint.sh workflows` passes (no findings); both end-to-end scripts pass with `CABINET_E2E=1`.

## Skipped Issues

None.

---

_Fixed: 2026-10-07_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 2_

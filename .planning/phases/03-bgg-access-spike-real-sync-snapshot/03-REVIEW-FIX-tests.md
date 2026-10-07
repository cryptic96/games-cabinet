---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07T00:00:00Z
review_path: .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
iteration: 1
findings_in_scope: 8
fixed: 8
skipped: 0
status: partial
---

# Phase 3: Code Review Fix Report (tests, scripts and CI)

**Fixed at:** 2026-10-07
**Source review:** .planning/phases/03-bgg-access-spike-real-sync-snapshot/03-REVIEW.md
**Iteration:** 1
**Scope:** WR-09 to WR-16 only. No Info items.

**Summary:**
- Findings in scope: 8
- Fixed: 8 (three of them only in part, because of file ownership or a deliberate omission; see "Left for others" below)
- Skipped: 0

Verification ran in the isolated worktree for this run. Each finding: `dotnet test --solution Cabinet.slnx` (809 tests at the end, all passing) and `build/lint.sh` (all five checks passing). Script changes were also run directly: `CABINET_E2E=1 bash deploy/tests/cabinet-deploy-e2e-test.sh` and `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh`, both passing. The first full test run of the whole session had one integration failure that did not reproduce in any of the later full runs or in repeated isolated runs of the scheduler tests; it was not traced to a specific test.

## Fixed Issues

### WR-09: `?.Should()` on a nullable header turns the assertion into a no-op

**Files modified:** `Cabinet.IntegrationTests/Infrastructure/HttpAssertions.cs` (new), `CabinetPageTests.cs`, `CreditTests.cs`, `HealthEndpointTests.cs`, `HelloPageTests.cs`, `LayoutEndpointTests.cs`, `LivePageTests.cs`, `SyncButtonTests.cs`, `SyncPipelineTests.cs`, `SyncStatusLineTests.cs` (all under `Cabinet.IntegrationTests/`)
**Commit:** c447733
**Applied fix:** One helper class with `ShouldHaveMediaType`, `ShouldHaveMediaTypeStartingWith` and `ShouldBeNoStore`; each asserts the header is present first, then its value. All 13 `?.` sites (the ten the review named plus `HealthEndpointTests` and `HelloPageTests`, which had the same pattern) now call it. In the files owned by the other fixer only the single offending line was replaced.

### WR-10: The "redirect to the www host is not followed" test cannot detect an auto-redirect regression

**Files modified:** `Cabinet.IntegrationTests/SecretsStayServerSideTests.cs`, `Cabinet.IntegrationTests/Infrastructure/LoopbackListener.cs` (new)
**Commit:** 12772e7
**Applied fix:** New test `The_production_transport_does_not_follow_a_redirect_to_another_address`. It boots the host with the production primary handler left in place (only the BGG options and pacer are replaced), points the BGG base address at a loopback listener that answers 301 with a `Location` pointing at a second loopback listener, presses sync, and asserts the first listener got one request, the second got none and the run ended as failed. No real BGG contact. Mutation-checked: swapping the production line for a default `SocketsHttpHandler` made the test fail; production code was restored with `git checkout` and is not part of the commit. The old scripted test is renamed to say what it actually proves (the client treats a 301 answer as a failure and does not chase it).

### WR-11: Negative assertions proven by real-time sleeps

**Files modified:** `Cabinet.UnitTests/TimerCountingClock.cs` (new), `Cabinet.UnitTests/Sync/SyncSchedulerTests.cs`, `Cabinet.UnitTests/FakeBgg/BggXmlTests.cs`, `Cabinet.IntegrationTests/LiveHubTests.cs`
**Commit:** 3645110
**Applied fix:** Status: fixed in part (see below), requires human verification of the scheduler negatives.
- New `TimerCountingClock` wraps a `FakeTimeProvider` and counts timers created on it, with a bounded `WaitForTimersAsync`. The scheduler tests wait for the timers to be armed instead of sleeping 150 ms, assert the start-up delay timer is or is not armed, and use `StopAsync` as a barrier (it awaits the worker, so every already-signalled tick has been handled) before asserting that nothing was queued. The disabled-scheduler test awaits `ExecuteTask` and asserts no timer was ever created.
- One scheduler negative cannot be made fully deterministic without a production hook: "a tick while a run is in progress queues no second request". Cancelling can race with the scheduler re-entering its wait, so that test keeps a short real-time watch (250 ms) that polls the queue and fails the moment a request appears, rather than a blind sleep.
- `BggXmlTests`: the hand-rolled `ManualTimeProvider` (not thread-safe, the subject of a separate info finding) is deleted; the delay test uses `FakeTimeProvider` through the counting clock, waits for the timer to be armed, and watches for early completion with `Task.WhenAny` against a bounded delay.
- `LiveHubTests`: the cap test no longer treats "not closed within 3 real seconds" as admitted. Refusal now waits for the connection to close (bounded at 10 s, a timeout fails the test); admission waits for the limiter's own count to reach the expected value and checks the connection stayed open. The test suite is about 5 s faster for it.
- Not changed: `Cabinet.UnitTests/Bgg/RequestPacerTests.cs`, which the back-end fixer owns. The pacer part of this finding is left to that fixer.

### WR-12: Installer end-to-end test runs the installer with `set -e` suppressed

**Files modified:** `deploy/tests/cabinet-deploy-e2e-test.sh`
**Commit:** bfaedd7
**Applied fix:** Added `run_with_errexit`, which runs the function in a subshell with errexit genuinely on, outside any `||`/`if` list, and stores the exit status. All four installer/poll invocations use it. A small experiment confirmed the old form let a failing step fall through (`continued`, status 0) and the new form stops it (status 1). The end-to-end script passes with the stricter semantics.

### WR-13: Repo-rules lint does not cover several reference kinds the project forbids

**Files modified:** `build/lint/checks/10-repo-rules.sh`
**Commit:** c8bd9cd
**Applied fix:** Status: fixed with one deliberate omission. New checks, each with self-test cases (bad strings built by concatenation so the script does not flag itself):
- review-finding identifiers (CR, WR, IN, BL prefixes, case-sensitive so words like "built-in-10" are not hit);
- plan numbers of the two-digit-dash-two-digit form, written so dates (`2026-10-07`) and time ranges (`10:00-18:00`) are not flagged;
- phase references now case-insensitive and including spelled-out numbers (`PHASE 3`, `phase two`);
- plan, wave and milestone followed by a number;
- `/*` block comments in C# files, matched only after whitespace, a statement or a bracket so `http://*:6080` and `image/*` inside strings are not flagged.
Negative self-tests cover the innocent forms (dates, time ranges, prose with the word plan or wave, a `milestone/v1-example` branch name). Run against the tree: no new findings.
Left out on purpose: the bare word for a milestone. Branch-naming documentation legitimately contains it (`milestone/v<N>-<name>`), so only the numbered form is checked. Also not done: deriving requirement-key prefixes from the planning requirements file at lint time. That file lives in the planning folder the lint is not allowed to reference, so the closed prefix list stays; it needs a manual update when a new prefix appears.

### WR-14: Package end-to-end test aborts silently because of `pipefail` in the stylesheet extraction

**Files modified:** `build/tests/package-release-e2e-test.sh`
**Commit:** bc0d6a9
**Applied fix:** The extraction tolerates "no match" (`grep ... || true` inside a group) and selects with `sed`, so no stage closes a pipe early; the `[ -n "$STYLESHEET" ] || fail` line now reports. Checked with no link, two links, and attributes in the other order; the full end-to-end script passes.

### WR-15: Test host leaks on construction failure and when a loop body fails

**Files modified:** `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs`
**Commit:** 850c73b
**Applied fix:** Status: fixed in part. The constructor now disposes the instance and rethrows when start-up on free ports fails; everything after `_realHost` is assigned in `CreateHost` sits in a `try`, so a failure disposes both hosts and clears `_realHost`.
Not done: the sequential `await using` rewrite of `The_header_never_names_the_data_source` in `SyncStatusLineTests.cs`. The task limited my edits to that file to the header assertion lines, because the front-end fixer may be editing it. Suggested change for whoever owns it: replace the `foreach` over an eagerly built array with two blocks, `await using var first = SyncHarness.CreateFactory(...)` and `await using var second = CreateSeededFactory(storage)`, each calling one shared local function that reads the page and asserts on the header.

### WR-16: Weak assertions that would pass if the behaviour broke

**Files modified:** `Cabinet.IntegrationTests/LiveHubTests.cs`, `Cabinet.IntegrationTests/SecretsStayServerSideTests.cs`
**Commit:** 3ab6925
**Applied fix:** Status: fixed, requires human verification of the chosen server-side signal.
- Long polling: new test that posts to the negotiate route and asserts the offered transports are exactly WebSockets and ServerSentEvents; the existing test now asserts an aggregate failure whose every inner failure is "disabled by the client", instead of any exception.
- Visitor test and hub tests: instead of an immediate empty-request check, they read the status route. An accepted sync request opens the shared window synchronously, so `running` false, no cooldown end and no last result prove no request was accepted; the request-count assertion stays as a second check. (This reads server state rather than waiting on the clock, so it is not subject to the timing blind spot the review described.)

## Left for others

- `Cabinet.UnitTests/Bgg/RequestPacerTests.cs` real-time sleeps (part of WR-11): owned by the back-end fixer.
- `SyncStatusLineTests.cs` sequential disposal (part of WR-15): file ownership, suggestion above.

## Skipped Issues

None.

---

_Fixed: 2026-10-07_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

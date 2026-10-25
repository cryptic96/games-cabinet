---
phase: 08-public-hardening-go-public
plan: 04
subsystem: browser-tests
tags: [browser-tests, flaky-tests, live-channel, signalr, playwright, webkit]
requires:
  - phase: 08-08
    provides: the header-bearing host the browser suite runs against; wave placement so the 10-run gates ran alone
provides:
  - LiveHearing helper: shared live spy, bounded "page heard a push" wait, bounded quiet-period wait
  - live look tests that publish only after the page proved it can hear the channel
  - card redraw tests that prove "nothing happened" by a heard push, not a fixed hold
  - default look first draw as a generous retrying wait
affects: [08-09]
tech-stack:
  added: []
  patterns: [prove the push path end to end before publishing, wait for a positive event then assert the negative]
key-files:
  created:
    - Cabinet.BrowserTests/Infrastructure/LiveHearing.cs
  modified:
    - Cabinet.BrowserTests/LiveLookTests.cs
    - Cabinet.BrowserTests/LiveLookWebKitTests.cs
    - Cabinet.BrowserTests/CardSyncTests.cs
    - Cabinet.BrowserTests/DefaultLookIdentityTests.cs
key-decisions:
  - "The spy and both bounded waits moved verbatim from the WebKit class into one internal helper; the WebKit class keeps only the constants it owns"
  - "The look that must be held back while a box is pulled out waits for one more heard push instead of a two second delay; the blocked-channel test keeps its single quiet-period advance and its one second delay because no push can be heard there"
  - "A run that dies with the runtime's fatal 'Internal CLR error' (zero tests ran) is retried once and reported separately from test failures"
status: complete
actuals:
  tokens: 6500
  tasks: 2
  commits: 2
---

# Phase 8 Plan 04: Browser tests that wait on the live channel stop flaking Summary

Live-channel browser tests now publish a change only after a spy on the page's live client has heard a push, and "nothing happens" is proven by a heard push instead of a sleep. Tracer gate: auto (owner away), verify re-run passed.

## What changed

- `Infrastructure/LiveHearing.cs` (new, internal): `Spy` (verbatim from the WebKit class), `WaitUntilThePageHearsAPushAsync` (re-sends the status the page already shows, up to 8 times, 5 s each, until the heard count grows), `LetTheQuietPeriodPassUntilAsync` (advances the host clock up to 4 times until a condition holds), plus `WaitUntilThePageHasHeardAPushAndActedAsync` (a heard push followed by two animation frames, the moment to assert the page chose to do nothing) and `HeardCountScript`.
- `LiveLookWebKitTests`: private spy, constants and both waits deleted; uses the helper. Behaviour unchanged.
- `LiveLookTests`: the spy is installed before the first navigation; `PublishAsync` first proves the page hears a push, saves on the owner page, then lets the quiet period pass until every published attribute is on the page. The held-back look (pull-out in progress) waits for one more heard push instead of a two second delay. The blocked-channel test publishes without a hearing wait (nothing can be heard) and keeps its single clock advance.
- `CardSyncTests`: `HoldPeriod` and all three `Task.Delay` calls removed; each is replaced by hearing a push (the current status is re-sent until heard) plus two frames, followed by the unchanged "no redraw yet" assertions.
- `DefaultLookIdentityTests`: the first draw waits with a retrying `Expect(...).ToBeVisibleAsync` at `LiveLookTests.FirstDrawTimeoutMs`, and the picture-complete wait uses the same timeout. The pixel comparison and its tolerance are untouched.

No assertion was removed, loosened, skipped or deleted; only how each test waits changed.

## Measurements (solo, one-core CPU quota, DejaVu fontconfig, 10 runs each)

| Class | Before | After |
|-------|--------|-------|
| LiveLookTests | 0 of 10 failed (no method failed) | 0 of 10 failed |
| DefaultLookIdentityTests | 0 of 10 failed | 0 of 10 failed |
| CardSyncTests | 1 of 10 failed (runtime crash, see below) | 0 of 10 failed (clean 10 in a row, 4 crashed attempts retried) |

The suite was already green in these runs on this machine, so the before counts do not show the CI-only flake; the change removes the timing assumptions that cause it (a push assumed to arrive, fixed holds, a one-shot first draw) rather than a failure reproduced here. Two further CardSyncTests rounds of 10 (before the crash-retry rule) failed only by the runtime crash below and never by an assertion. Restart rounds used: 0 (no assertion failure ever occurred). WebKit: `LiveLookWebKitTests` with `CABINET_WEBKIT=1` passed (WebKit is installed locally).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Runtime crash in CardSyncTests, retried once per run**
- **Found during:** Task 2 gates (and in the before baseline: run 7 of 10)
- **Issue:** About one run in eight to six of `CardSyncTests` (whole class or either test alone, through `dotnet test` or the test executable directly) ends with the runtime's fatal `Internal CLR error (0x80131506)` right after "Application is shutting down", exit 134, zero tests reported. It happens with the original test code as well (5 of 40 direct runs, 4 of 33 through `dotnet test`) and with the new code (6 of 70 direct, 13 of 58 through `dotnet test`); the difference is within noise for these sample sizes. It never occurred in `LiveLookTests` or `DefaultLookIdentityTests` (60+ runs). The core dump shows an abort inside the runtime from managed code at host shutdown; the cause is not in a wait, so it was not fixed here.
- **Fix:** The gate treats a run that dies this way as an infrastructure crash, retries it once (the project notes prescribe a retry), and reports crashes beside the failure count. Any assertion failure, or two crashes in a row, would count as a failure.
- **Consequence:** the plan's verify command as written (no retry) would fail roughly one CardSyncTests run in eight on this machine regardless of this plan's changes. This should be looked at on its own (a runtime crash during host shutdown in that class); it is recorded here for the owner and not fixed.

**2. [Rule 2 - extra waits] Held-back look and picture waits**
- The two second delay in the pull-out test and the default timeout of the picture-complete wait were replaced by a heard-push wait and the generous first-draw timeout, in the same spirit as the plan's waits. The blocked-channel test keeps its one second delay and the quiet-period advance once, because with the channel blocked there is no positive event to wait for.

## Known Stubs

None.

## Threat Flags

None. Only test code changed.

## TDD Gate Compliance

The plan marks both tasks as TDD, but they change test waits only, so there is no product behaviour to drive with a failing test first; the measured before and after runs stand in for the red and green steps.

## Self-Check: PASSED

- `Cabinet.BrowserTests/Infrastructure/LiveHearing.cs`, the four edited test files and this summary exist.
- Commits `1b52a5d` and `d4a059e` exist on the branch.
- `Task.Delay` count in `CardSyncTests.cs` is 0; `FirstDrawTimeoutMs` appears in `DefaultLookIdentityTests.cs`; `const string LiveSpy` is gone from the WebKit class; `build/lint.sh` passes.

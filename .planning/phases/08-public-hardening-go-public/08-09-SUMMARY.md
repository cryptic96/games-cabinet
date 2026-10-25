---
phase: 08-public-hardening-go-public
plan: 09
subsystem: browser-tests
tags: [browser-tests, flaky-tests, card-dialog, back-button, playwright]
requires:
  - phase: 08-04
    provides: the live waits and the reworked card sync tests; the first half of the folded flaky-tests todo
  - phase: 08-08
    provides: wave placement so the 10-run gates and slice runs had the machine to themselves
provides:
  - CloseCardAsync and WaitUntilTheCardHasSettledAsync in the browser test base
  - a 15 second default for retrying assertions, set once for the process
  - address reads after going back as retrying assertions
  - the flaky-tests todo closed with before and after counts
affects: []
tech-stack:
  added: []
  patterns: [one shared condition-based close that waits for the settled card, retrying assertion on the end state instead of a one-shot read]
key-files:
  created: []
  modified:
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
    - Cabinet.BrowserTests/CardOpenTests.cs
    - Cabinet.BrowserTests/CardContentTests.cs
    - Cabinet.BrowserTests/CardExpansionTests.cs
    - Cabinet.BrowserTests/CardLookTests.cs
    - Cabinet.BrowserTests/BackButtonTests.cs
    - Cabinet.BrowserTests/SheetDragTests.cs
    - .planning/todos/completed/2026-10-17-harden-browser-tests-that-flake-on-ci.md
key-decisions:
  - "The base keeps its own private card selector constants (named differently from the per-class OpenCard constants) so no derived class hides a base member; a hiding warning is an error here"
  - "The default expect timeout is set in a static constructor of the base: it is process wide state, so setting it once at type initialisation is enough and no test can run before it"
  - "CardOpenTests keeps its Escape presses in the test bodies (Escape is what those tests prove) and only waits for the settled card first"
status: complete
actuals:
  tokens: 4100
  tasks: 3
  commits: 2
---

# Phase 8 Plan 09: Card dialogs and the back button stop flaking, and the flaky-tests todo is closed Summary

Every card test now closes a card through one base helper that waits for the opening pull-out to settle, presses Escape and waits until no card is open and its history step is gone; address reads after going back retry; the browser test base sets a 15 second default for retrying assertions. No assertion was removed, loosened, skipped or deleted; only how the tests wait changed.

Tracer gate: auto (owner away), verify re-run passed.

## What changed

- `CabinetPageTest`: `CloseCardAsync()` (wait for `html[data-pull-kind]` to be gone, Escape, expect no open card, wait for the card step to be gone), a small `WaitUntilTheCardHasSettledAsync()` that it and the tests share, a `DefaultExpectTimeoutMs = 15000` constant with its reason, applied once in a static constructor.
- `CardOpenTests`: the three Escape presses wait for the settled card first; the key press stays in the test body.
- `CardContentTests`, `CardExpansionTests`, `CardLookScenarios` (base of `CardLookTests` and `PhoneCardLookTests`): the private and protected close helpers are gone and the calls reach the base one.
- `BackButtonTests`, `SheetDragTests`: every one-shot `Page.Url` comparison is `await Expect(Page).ToHaveURLAsync(...)` with the same expected address (18 reads in total); the open helpers use the shared settled wait. `SheetDragTests` already waited for the settled marker in its open helper before any drag, flick or resize that closes the card, so no further wait was needed before each close; the drag measurements are untouched.

## Measurements

Method for every count: one class at a time, alone on the machine, `dotnet test --no-build --filter-class`, under `systemd-run --user --scope -p CPUQuota=100%` (one core) and the DejaVu fontconfig (the CI font), 10 runs. A run that dies with the runtime's fatal `Internal CLR error` with zero tests run is the known shutdown crash tracked in the card sync todo; it is retried and counted apart, never as an assertion failure.

| Class | Before (failed of 10) | Runtime crashes before | After (failed of 10) | Runtime crashes after |
|-------|-----------------------|------------------------|----------------------|-----------------------|
| CardOpenTests | 0 | 1 | 0 | 4 |
| CardContentTests | 0 | 2 | 0 (see note) | 4, then 1 in the repeat |
| CardExpansionTests | 0 | 2 | 0 | 0 |
| CardLookTests | 0 | 2 | 0 | 0 |
| PhoneCardLookTests | 0 | 0 | 0 | 1 |
| BackButtonTests | 0 | 2 | 0 | 2 |
| SheetDragTests | 0 | 2 | 0 | 0 |

Note on `CardContentTests`: in the first after round, run 2 of 10 hit the shutdown crash three attempts in a row (my retry budget then was 3), so it counted as a failed run although no assertion failed. I raised the budget to 6 and repeated the whole 10 for this class: 0 of 10 failed (one crashed attempt retried). No restart round for an assertion failure was ever needed (0 of the allowed 3).

The suite was already green in these runs on this machine, so the before counts do not show the CI-only flake; the change removes the timing assumptions that cause it (a close pressed before the card is ready, a one-shot address read right after a history traversal, a five second default) rather than fixing a failure reproduced here.

Whole suite, slice by slice as CI slices it (alone on the machine, `CABINET_WEBKIT=1`, WebKit installed), once normally and once under the DejaVu fontconfig, no runtime crashes in either pass:

| Slice | Tests | Normal | DejaVu |
|-------|-------|--------|--------|
| Look* | 191 | 191 passed | 191 passed |
| Owner* | 220 | 220 passed | 220 passed |
| Filter* | 203 | 203 passed | 203 passed |
| rest (not Look/Owner/Filter, not WebKit) | 183 | 183 passed | 183 passed |
| WebKit | 2 | 2 passed | 2 passed |

No failure occurred outside the hardened classes, so nothing was fixed or recorded as seen once in step 3.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] The private close helpers of two Task 2 classes were removed in the Task 1 commit**
- **Found during:** Task 1
- **Issue:** adding `CloseCardAsync` to the base while `CardExpansionTests` and `CardLookScenarios` still had their own copies is a member-hiding warning, which is an error here, so the Task 1 commit would not have built.
- **Fix:** the deletion of those two helpers (a Task 2 step) went into the Task 1 commit; Task 2 then holds the address-read and settled-wait changes.
- **Files modified:** `Cabinet.BrowserTests/CardExpansionTests.cs`, `Cabinet.BrowserTests/CardLookTests.cs`
- **Commit:** 56208a1

**2. [Method] The 10-run gates also ran under the DejaVu fontconfig**
- The plan's verify loop uses only the one-core quota; the project notes ask for the CI font as well, so every counted run used both. This is stricter than the plan's gate. The plan's automated verify lines were not run verbatim: they stop on the first runtime shutdown crash, which this machine produces in roughly one run in six, so a retrying script stood in with the same quota and the same class filters.

## Known Stubs

None.

## Threat Flags

None.

## Self-Check: PASSED

- Commits 56208a1 and 1ecfc22 exist on the worktree branch.
- The base `CloseCardAsync` appears once; the content, expansion and card look classes have none; no `Page.Url.Should` is left in `BackButtonTests` or `SheetDragTests`.
- The todo is in `.planning/todos/completed/` and no longer in `pending/`.

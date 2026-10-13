---
phase: 05-game-detail-accessibility-language
fixed_at: 2026-10-12T00:00:00Z
review_path: .planning/phases/05-game-detail-accessibility-language/05-REVIEW.md
iteration: 1
findings_in_scope: 3
fixed: 3
skipped: 0
status: all_fixed
---

# Phase 5: Code Review Fix Report

**Fixed at:** 2026-10-12
**Source review:** .planning/phases/05-game-detail-accessibility-language/05-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 3 (no critical findings; info findings out of scope)
- Fixed: 3
- Skipped: 0

Fixes were made in the main checkout on `milestone/v1-games-cabinet`, as the caller instructed (no isolated worktree), so the browser and .NET gates ran against the real tree.

## Fixed Issues

### WR-01: A single failed card-data request leaves every card incomplete for the whole page session, with a misleading message

**Files modified:** `Cabinet.Service/wwwroot/js/cabinet.js`, `Cabinet.Service/wwwroot/js/card-flow.js`, `Cabinet.Service/wwwroot/js/card-view.js`, `Cabinet.Service/wwwroot/js/copy.js`, `build/tests/page-scripts.test.mjs`, `build/tests/card-flow.test.mjs`, `build/tests/copy-parity.test.mjs`, `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs`, `Cabinet.BrowserTests/CardOpenTests.cs`, `Cabinet.BrowserTests/GamesListTests.cs`
**Commit:** 633ee2f
**Status:** fixed: requires human verification (client state and retry logic; covered by node and browser tests)
**Applied fix:**
- `fetchCards` now makes up to three attempts. A network error or non-OK answer is repeated after 1.5 s, then 4 s. A layout-mismatch answer is still repeated at once. Every wait and every answer is guarded by the existing load counter, so a newer load or redraw wins and a stale repeat never fires. The single request was split out into `requestCards`, and the wait uses `window.setTimeout` with a function argument (no string eval, still two script elements).
- When every attempt failed, a flag marks the drawn boxes' fallback record (`detailsFailed`). The flag resets on each new load and draw. `card-view.js` shows the new `detailsFailed` note for such a record and keeps the existing "after the next sync" note for genuinely missing details.
- New copy key `detailsFailed` in both tables: English "Couldn't load the details. Reload the page to try again." and Dutch "De details konden niet worden geladen. Laad de pagina opnieuw om het nog eens te proberen." The copy parity tests pass.
- Tests: node tests for retry after a network error, after a refusal, giving up after three attempts, and a superseded repeat never firing; card-flow tests for the new flag; copy tests for the new strings. Browser tests route the cards request to fail always (English and Dutch note) and fail once (details arrive, no failed note). Two existing browser tests that expected the old note after an always-failing request now expect the new one, and wait for the third attempt through shared helpers in `CabinetPageTest`.
- Known limit: a card opened while the retries are still running is built from the fallback record and shows the pending-sync note, as before; it is not rebuilt when the data arrives.

### WR-02: `--ignore-exit-code 8` applies to the whole solution run, so a project that runs zero tests passes CI and releases

**Files modified:** `.github/workflows/ci.yml`, `.github/workflows/release.yml`, `docs/development.md`
**Commit:** 29c274b
**Applied fix:** Both workflows now run `dotnet test --project Cabinet.UnitTests --no-restore` and `dotnet test --project Cabinet.IntegrationTests --no-restore`, with no exit-code exemption and no trait filter, so a project that discovers no tests fails the run. The browser project keeps its own job. The development guide lists the same commands. Actions stay SHA-pinned, `persist-credentials: false` and the permissions are unchanged. The release-workflow test script asserts no old command, so it needed no change. The review's optional check that `browser-tests` and `lint` are required status checks on `main` is a repository setting and was not verified here.

### WR-03: The fast-flick sheet test depends on real-time gesture speed and can fail on a loaded runner

**Files modified:** `Cabinet.Service/wwwroot/js/card-flow.js`, `Cabinet.Service/wwwroot/js/detail.js`, `build/tests/card-flow.test.mjs`, `Cabinet.BrowserTests/SheetDragTests.cs`
**Commit:** ad6bc72
**Status:** fixed: requires human verification (gesture logic moved; behaviour intended to be identical)
**Applied fix:**
- The release-speed calculation and the 80 ms window moved from `detail.js` into pure exports in `card-flow.js` (`DRAG_SPEED_WINDOW_MS`, `recentSamples`, `releaseSpeed`). `detail.js` calls them with the same samples and the same release point. The 25% distance, 0.6 px/ms threshold and 80 ms window are unchanged.
- Node tests cover the window edge (80 ms counts, 81 ms does not), a finger that rested before lifting, empty samples, a release at or before the oldest sample, an upward movement, a short fast flick against the same distance after a rest, and the 0.6 px/ms threshold.
- The browser flick test now sends its mouse events through a CDP session with chosen event times (10 ms apart, 80 px in total), so a slow runner cannot stretch the gesture past the window. A companion test uses the same distance with a 200 ms rest before the release and proves the sheet springs back, which also shows the page honours the supplied times. The sheet-drag collection doc comment was updated to match.

## Verification gate

All run from the main checkout (not an isolated worktree), so the numbers are reproducible from the tree a reader sees.

| Check | Result |
|-------|--------|
| `dotnet build Cabinet.slnx` | 0 warnings, 0 errors (one transient compiler crash on an earlier build, passed on rerun) |
| `dotnet test --project Cabinet.UnitTests --no-restore` (as CI now runs it, `CI=true`) | 1918 passed, 0 failed |
| `dotnet test --project Cabinet.IntegrationTests --no-restore` (as CI now runs it, `CI=true`) | 275 passed, 0 failed |
| `dotnet test --project Cabinet.BrowserTests --no-build`, run 1 | 133 passed, 0 failed |
| `dotnet test --project Cabinet.BrowserTests --no-build`, run 2 | 133 passed, 0 failed |
| `node --test build/tests/*.test.mjs` | 250 passed, 0 failed |
| `bash build/lint.sh` | repo-rules, workflows, shell, secrets, script-tests all PASS |

Not touched: `.planning/phases/05-game-detail-accessibility-language/05-SECURITY.md` is an untracked file from the parallel security audit and was left alone. REVIEW-FIX.md is not committed, as instructed.

---

_Fixed: 2026-10-12_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

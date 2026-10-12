---
phase: 05-game-detail-accessibility-language
plan: 14
subsystem: cabinet-ui
tags: [css, render, health, polish]
requires: ["05-10", "05-11", "05-12"]
provides:
  - "data-art=failed marker on a cover whose picture did not load"
  - "ArtFileAudit and the missingArt count on the ops health answer"
  - "two-step one-line title sizes, box fallback tokens, plate gap, plinth lip"
affects: [render.js, cabinet.css, site.css, Program.cs]
tech-stack:
  added: []
  patterns: ["size container queries on boxes", "synthetic-art fixture shared by integration and browser tests"]
key-files:
  created:
    - Cabinet.Service/Collection/ArtFileAudit.cs
    - Cabinet.IntegrationTests/ArtFileAuditTests.cs
    - Cabinet.IntegrationTests/Infrastructure/SyntheticArtCollection.cs
    - Cabinet.BrowserTests/CabinetPolishTests.cs
    - build/tests/cabinet-css.test.mjs
  modified:
    - Cabinet.Service/wwwroot/js/render.js
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.Service/Program.cs
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
    - .planning/todos/completed/2026-10-09-cabinet-ui-polish-from-phase-4-ui-review.md
decisions:
  - "ArtFileAudit is registered in Program.cs next to the health writer rather than in the sync registration file, which is outside this plan's files"
  - "missing pictures are counted as distinct stored file names, so a file shared by several games counts once"
status: complete
actuals:
  tokens: 45000
  tasks: 2
  commits: 2
---

# Phase 5 Plan 14: Cabinet polish Summary

A broken box picture now looks exactly like a game with no picture to visitors, is marked `data-art="failed"` in the page, and is counted as `missingArt` on the ops health answer; spine titles use two sizes, the fallback colours are tokens, the plate gap and the plinth lip are fixed.

## What was done

- **Task 1 (tracer):** `render.js` sets `data-art = 'failed'` on image error; every `[data-art]` selector in `cabinet.css` now matches `"true"` only. `ArtFileAudit.CountMissing()` checks every distinct stored picture name the collection names (only plain file names under the art path, existence check only), logs one count-only warning per change, and `missingArt` is added to the health JSON; status unchanged. The tracer was verified end to end (integration and browser test) before the second task.
- **Task 2:** non-cover boxes became size containers; one-line spine and upright titles are 12 px, 16 px from 26 px wide; flat, layer and one-line orphan titles 12 px, 14 px from 28 px tall; two-line upright expansions and two-line orphans keep their earlier rules. `--box-fallback-bg` and `--box-fallback-fg` live in `site.css`; no hex fallback remains in a `var()`. Cover plate gap is 4 px only with more than one child. The plinth's lit lip is an outer edge (`max(1px, 0.8 * u)`), `--arch-lit` 0.22 in narrow sections. The polish todo moved to completed with a closing note.

## Verification

- `dotnet test` for `ArtFileAuditTests`, `HealthEndpointTests`, `FamilyCoverTests` (existing cover test), `CabinetPolishTests` (6 tests): pass.
- `node --test build/tests/cabinet-css.test.mjs`: 6 pass.
- `deploy/tests/cabinet-deploy-logic-test.sh`: passes. `build/lint/checks/10-repo-rules.sh`: passes.
- Screenshots (`CABINET_SCREENSHOT_DIR`): `broken-art-desktop`, `polish-desktop-65`, `polish-phone-*`, `plinth-lip-390`. Phone 65 sample looked even and clean.

## Deviations from Plan

**1. [Rule 3 - Blocking] Browser test base could not take service overrides.** `CabinetPageTest.StartAsync` only accepted settings, so the fake-BGG art fixture could not be used in a browser test. Added an optional `configureServices` parameter (existing callers unchanged) and a shared `SyntheticArtCollection` helper in `Cabinet.IntegrationTests/Infrastructure` (linked into the browser project). Commit 1f97add.

**2. Plinth lip shows only as the computed token in the browser test.** The test asserts `--arch-lit` resolves to 0.22 at 390 px; the lip's visibility itself is the human check below.

## Deferred Issues

- `build/tests/page-scripts.test.mjs` has two tests that assert the old behaviour and now fail: "a picture that fails to load is swapped for the generated cover..." (line 1627, `assert.equal(cover.dataset.art, undefined)`) and "a cover with art takes the art colour and a failed picture keeps it..." (line 1759, same assertion). Both must become `assert.equal(cover.dataset.art, 'failed')`. The file is owned by the parallel plan in this wave, so it was not edited here. The orchestrator should apply these two one-line changes after the merge (`node --test build/tests/*.test.mjs` otherwise reports 2 failures; all 225 others pass).
- Human check pending (end-of-phase mode): evenness of spine titles and the plinth lip on the phone.

## Known Stubs

None.

## Threat Flags

None. The audit is existence-only, restricted to plain file names under the art path, and reports counts only (T-05-14-01, T-05-14-02 mitigated).

## Self-Check: PASSED

Created files exist; commits 1f97add and d5762f2 are in the branch.

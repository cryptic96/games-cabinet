---
phase: 05-game-detail-accessibility-language
plan: 05
subsystem: ui
tags: [i18n, dutch, intl, playwright, vanilla-js]
status: complete
requires: ["05-01", "05-04"]
provides:
  - "SyncStatusText language overloads: Relative, NeverSyncedText, ExactUtc, StaleRecent, StaleHeldBack"
  - "copy.js COPY_BY_LANGUAGE { en, nl }, copyFor(language), pageLanguage(), COPY"
  - "language.js initLanguageToggle: toggle click calls the language route, reloads, falls back to the link"
  - "relative-time-cases.json rows carry a language field (15 en, 15 nl)"
  - "copy-parity.test.mjs and LanguageSwitchTests"
affects: [05-06]
tech-stack:
  added: []
  patterns:
    - "one shared case table read by the C# and the node test, so server and script cannot drift"
    - "two frozen string tables with equal keys, chosen once from html lang"
key-files:
  created:
    - Cabinet.Service/wwwroot/js/language.js
    - build/tests/copy-parity.test.mjs
    - Cabinet.BrowserTests/LanguageSwitchTests.cs
  modified:
    - Cabinet.Service/Pages/SyncStatusText.cs
    - Cabinet.Service/Pages/Index.cshtml.cs
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - build/tests/fixtures/relative-time-cases.json
    - build/tests/page-scripts.test.mjs
    - Cabinet.UnitTests/Sync/SyncStatusTextTests.cs
key-decisions:
  - "Dutch relative time uses Intl numeric 'always' with an explicit yesterday case, because numeric 'auto' gives eergisteren for two days where the contract says 2 dagen geleden"
  - "TIME_LOCALE is no longer exported; nothing imported it and each table now owns its own formatter"
  - "The toggle script requests the language route with redirect manual and the browser still stores the cookie; a failed or refused answer assigns the link address"
metrics:
  tasks: 3
  commits: 3
actuals:
  tokens: 24000
  tasks: 3
  commits: 3
---

# Phase 5 Plan 05: Status line and scripts in both languages Summary

The sync status line, the exact time, the stale notes and every script string now read in the page language, server and script agree through one shared case table, and the toggle switches in place with script (falling back to its link).

## What was built

- Task 1 (tracer): `relative-time-cases.json` gained a `language` field with 15 Dutch rows. `SyncStatusText.Relative(elapsed, language)` and `NeverSyncedText(language)` write the Dutch suffix frame (`12 minuten geleden gesynchroniseerd`, `Gisteren gesynchroniseerd`, `Zojuist gesynchroniseerd`). `copy.js` was restructured into `ENGLISH` and `DUTCH` tables exported as `COPY_BY_LANGUAGE`, with `copyFor`, `pageLanguage` (reads `html lang`, English without a document) and `COPY = copyFor(pageLanguage())` so `sync.js`, `cabinet.js` and the older tests keep working. `IndexModel` uses the language overloads. The tracer verify (node page-scripts test and `SyncStatusTextTests`) passed before the later tasks.
- Task 2: full Dutch table from the copy contract (informal `je`, `nl-NL` exact time with `om`), `ExactUtc`/`StaleRecent`/`StaleHeldBack` language overloads in C#, `copy-parity.test.mjs` (same keys, same kinds and function arity, no ellipsis character, no formal `u`, contract wording, label-in-name for the "+N more" names, BGG titles pass through unchanged) and extra C# tests.
- Task 3: `language.js` (`initLanguageToggle`, imported by `cabinet.js`, does nothing without a `.lang-toggle`), the node harness now copies every `.js` file of the script folder, four node tests for the click handler, and five `LanguageSwitchTests` browser tests with a Dutch browser locale.

## Task commits

| Task | Commit | Description |
| ---- | ------ | ----------- |
| 1 (tracer) | ff98fb9 | Relative sync time follows the page language on the server and in the script |
| 2 | e535b1a | Dutch script strings and status sentences with enforced parity |
| 3 | aafc426 | Toggle switches in place with script; browser proof of the whole switch |

## Verification

- `node --test build/tests/*.test.mjs`: 121 passed.
- `dotnet test --project Cabinet.UnitTests --filter-class "*SyncStatusTextTests"`: 41 passed.
- `dotnet test --project Cabinet.IntegrationTests` with `*SyncStatusLineTests` (10), `*CabinetPageTests` (20) and `*LivePageTests` (2): passed (still exactly two script elements, no inline script or style).
- `dotnet test --project Cabinet.BrowserTests --filter-class "*LanguageSwitchTests"`: 5 passed. The browser test confirms the script itself made the `fetch` to the language route (not the link fallback), exactly one `lang` cookie exists with HttpOnly and SameSite Lax, the address stays `/?sample=65` after switching, labels and bare titles match between the two languages, and at 390 px on the Dutch page nothing scrolls sideways and the sync button is inside the viewport. The phone screenshot was inspected: the longer notes wrap and the button keeps its own row.
- `bash build/lint/checks/10-repo-rules.sh` exits 0.

## Deviations from Plan

**1. [Rule 1 - Bug] Dutch relative time for two days**
- **Found during:** Task 1
- **Issue:** `Intl.RelativeTimeFormat('nl', { numeric: 'auto' })` returns `eergisteren` for -2 days; the contract and shared rows say `2 dagen geleden`.
- **Fix:** Dutch uses `numeric: 'always'` and a single explicit `Gisteren gesynchroniseerd` case for one day.
- **Files modified:** Cabinet.Service/wwwroot/js/copy.js
- **Commit:** ff98fb9

**2. [Plan adjustment] Order of the C# overloads**
- The Dutch `ExactUtc`, `StaleRecent` and `StaleHeldBack` overloads were written in Task 1 together with `Relative`, so the Task 1 commit already carries them; their tests and the `IndexModel.StaleNoteText` wiring are in Task 1 and Task 2 as planned.

**3. [Plan adjustment] Browser assertions use Playwright expectations**
- `Page.WaitForFunctionAsync` evaluates a string as script, which the page's CSP blocks; the language wait uses `Expect(...).ToHaveAttributeAsync` instead.

**4. [Plan adjustment] Overflow test sets the long notes itself**
- The unsynced test host shows no status notes, so the phone test writes the longest Dutch note and stale sentence into the existing note elements before measuring, which is what the layout has to hold.

Out-of-plan files touched: none beyond the plan's list.

## Known Stubs

None.

## Threat Flags

None. `language.js` only calls `href` values of links inside `.lang-toggle`, with same-origin credentials, and sets no markup.

## Self-Check: PASSED

- Files exist: language.js, copy-parity.test.mjs, LanguageSwitchTests.cs.
- Commits exist: ff98fb9, e535b1a, aafc426.

---
phase: 05-game-detail-accessibility-language
plan: 04
subsystem: ui
tags: [i18n, razor-pages, cookie, accept-language, dutch]
status: complete
requires: []
provides:
  - "SiteLanguage (English, Dutch, All, CookieName, Culture, TryGet, Resolve, EnsureAvailable)"
  - "SiteText English and Dutch tables of every server-rendered label"
  - "GET /language/{code} (LanguageEndpoint.Route, MapCabinetLanguage)"
  - "IndexModel.Language, IndexModel.LanguageHref"
  - ".lang-toggle and .sample-switcher CSS classes"
affects: [05-05, 05-06]
tech-stack:
  added: []
  patterns: ["language resolved once per request via HttpContext.Items", "functional cookie plus 303 to the plain page"]
key-files:
  created:
    - Cabinet.Service/Language/SiteLanguage.cs
    - Cabinet.Service/Language/SiteText.cs
    - Cabinet.Service/Language/LanguageEndpoint.cs
    - Cabinet.IntegrationTests/LanguageTests.cs
    - Cabinet.UnitTests/Language/SiteLanguageTests.cs
    - Cabinet.UnitTests/Language/SiteTextTests.cs
  modified:
    - Cabinet.Service/Program.cs
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/Pages/Index.cshtml.cs
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - Cabinet.Service/Pages/_ViewImports.cshtml
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
    - Cabinet.IntegrationTests/CabinetPageTests.cs
    - Cabinet.UnitTests/Prototype/SampleGenerationTests.cs
decisions:
  - "Dutch site name stays the draft Spellenkast until the owner decides"
  - "The toggle separator is CSS generated content with empty alternative text, positioned on the boundary of the second item so both 44 px targets stay intact"
metrics:
  tasks: 3
  commits: 4
actuals:
  tokens: 40000
  tasks: 3
  commits: 4
---

# Phase 5 Plan 04: Page in Dutch from the first byte, and the language toggle Summary

The server now writes the whole page in English or Dutch from the first byte (cookie, then Accept-Language by quality, then English), and an `EN · NL` toggle backed by `GET /language/{code}` switches and remembers the choice in one functional cookie.

## What was built

- `SiteText` / `SiteLanguage`: typed English and Dutch label tables (informal `je`), per-request resolution cached in `HttpContext.Items`, lazy `nl-NL` culture, `EnsureAvailable()` called before `builder.Build()` so a host without ICU stops with a message naming ICU.
- Layout and index page: `<html lang>`, title, `<h1>`, footer version, sync button, exact-time line, being-filled and no-script text follow the language; `Content-Language` and `Vary: Accept-Language, Cookie` are set by the page; BGG logo alt and the developer sample switcher stay English.
- `LanguageEndpoint`: allowlist `en`/`nl` (any casing), cookie `lang` (HttpOnly, SameSite=Lax, Path=/, Max-Age one year, Secure on HTTPS), `Cache-Control: no-store`, 303 to `/` or `/?sample={name}` only for a name `SampleCatalog.TryResolve` returns; unknown code answers 404 with no cookie.
- Toggle markup and CSS: header grid with the title and toggle in one 44 px row, current item weight 600 underlined in `--wall-text`, other item `--chrome-muted`, dot as generated content, no accent colour; the bare `nav` rules are now scoped to `.sample-switcher`.
- Tests: 8 + 12 integration tests in `LanguageTests` (first paint per language, quality order, cookie attributes, allowlist, redirect target incl. hostile sample, cookie precedence, toggle markup, page never sets a cookie, layout ETag unchanged), one CSP test for the 303, and 35 unit tests (resolver rules, table parity, placeholders, no formal `u`, no ellipsis, Dutch month names).

## Task commits

| Task | Commit | Description |
|------|--------|-------------|
| 1 (tracer) | 59af13f | Page rendered in the visitor's language from the first byte |
| 2 | 38a6ac6 | EN/NL toggle, cookie endpoint, CSS, ICU start-up check |
| 3 | 0fc48d3 | Unit tests for resolution and the tables |
| fix | 81ee68e | Request context for the page model in an existing unit test |

Tracer gate: the tracer's verify (`LanguageTests`, `CabinetPageTests`, `HelloPageTests`) passed end to end before the toggle work started.

## Deviations from Plan

**1. [Rule 3 - Blocking] Existing `CabinetPageTests` assertions on the sample switcher**
- **Found during:** Tasks 1 and 2
- **Issue:** The tests pinned the exact `<nav aria-label=...>` markup and `NotContain("<nav")`; the required `sample-switcher` class and the new language `nav` broke them.
- **Fix:** Updated the expected markup to include the class, `NotContain("sample-switcher")` where the switcher must be absent, and anchored the navigation slice on the switcher.
- **Files:** `Cabinet.IntegrationTests/CabinetPageTests.cs` (not in the plan's file list)

**2. [Rule 3 - Blocking] `SampleGenerationTests` builds `IndexModel` without a request**
- **Found during:** full-suite run
- **Issue:** `OnGet` now resolves the language from `HttpContext`, which was null.
- **Fix:** Gave the page model a `PageContext` with a `DefaultHttpContext` in the test.
- **Files:** `Cabinet.UnitTests/Prototype/SampleGenerationTests.cs` (not in the plan's file list)

**3. [Plan adjustment] CSP test for the 303**
- The plan suggested an `InlineData` row; the shared test uses a redirect-following client, so a separate test with a non-following client was added instead.

**4. [TDD note] Task 3 behaviours passed on first run**
- The resolver was written in Task 1 as the plan specified, so the Task 3 unit tests had no RED phase; they are regression guards. `SiteLanguage.Resolve` needed no change.

**5. Task 1 and Task 2 split of the toggle**
- The toggle markup and `LanguageHref` were deferred to Task 2 so each commit builds on its own; no behavioural difference.

## Verification

- Integration: `LanguageTests` (20 cases), `CabinetPageTests` (20), `HelloPageTests` (2), `ContentSecurityPolicyTests` (15), `LiveHubTests` (13) pass.
- Unit: `SiteLanguageTests` (27) and `SiteTextTests` (8) pass; `SampleGenerationTests` (11) pass.
- Full solution run: 2107 tests; one `LiveHubTests` WebSocket case (`A_connection_beyond_the_cap_is_closed...`) failed once under full-suite load and passed alone and on rerun. It does not touch language code; treated as pre-existing flakiness.
- `bash build/lint/checks/10-repo-rules.sh` passes; `grep -cE '^nav \{|^nav a'` on `site.css` prints 0.

## Pending human check

The plan's visual check (toggle placement at 1440, 390 and 320 px, one-row title and toggle at 320 px, switching and persistence after reload) was not performed in this run; it needs a browser against the running site. Layout rules are in `site.css` (`header` grid, `.lang-toggle`).

## Known Stubs

None. The relative-time text and stale notes on first paint stay English by design; the next language plan makes them Dutch.

## Threat Flags

None beyond the plan's threat model; the new route accepts only allowlisted values and the redirect target never echoes input.

## Self-Check: PASSED

- Files exist: SiteLanguage.cs, SiteText.cs, LanguageEndpoint.cs, LanguageTests.cs, SiteLanguageTests.cs, SiteTextTests.cs.
- Commits exist: 59af13f, 38a6ac6, 0fc48d3, 81ee68e.

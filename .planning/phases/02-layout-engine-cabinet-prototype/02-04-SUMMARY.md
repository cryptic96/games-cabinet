---
phase: 02-layout-engine-cabinet-prototype
plan: 04
subsystem: ui
tags: [razor-pages, vanilla-js, etag, prototype-switch, docs]
status: complete
requires:
  - phase: 02-layout-engine-cabinet-prototype
    provides: layout endpoint, section designs, synthetic collections, server settings (plans 01 and 02)
provides:
  - SampleCatalog prototype switch and invented-sample allowlist (one removable folder)
  - Cabinet page chrome (heading, status line, sample switcher, footer) with every load state
  - LayoutCache with ETag and 304 on the layout endpoint
  - Profile selection by one media query, with loading, error and retry states
  - Operator guide docs/cabinet-layout.md
affects: [phone section design, box look review, real collection wiring]
tech-stack:
  added: []
  patterns:
    - "Prototype scaffolding behind one Prototype:Enabled switch in Cabinet.Service/Prototype"
    - "Allowlist-bounded per-process cache of serialised layouts keyed by sample and design"
    - "Test factory applies extra settings through UseSetting because the program reads configuration while building services"
key-files:
  created:
    - Cabinet.Service/Prototype/SampleCatalog.cs
    - Cabinet.Service/Pages/Index.cshtml.cs
    - Cabinet.Service/Layout/LayoutCache.cs
    - docs/cabinet-layout.md
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/Layout/LayoutEndpoint.cs
    - Cabinet.Service/appsettings.json
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
    - Cabinet.IntegrationTests/CabinetPageTests.cs
    - Cabinet.IntegrationTests/LayoutEndpointTests.cs
    - README.md
key-decisions:
  - "Extra test settings go through UseSetting, not AddInMemoryCollection, so values read during service registration take effect"
  - "Tracer check re-run automatically (test suite plus browser) and the human review left to the end-of-phase batch, matching the project's human-verify mode"
requirements-completed: [CAB-01, CAB-06, CAB-07]
metrics:
  tasks: 3
  commits: 4
actuals:
  tokens: 36000
  tasks: 3
  commits: 4
---

# Phase 2 Plan 04: Review surface and layout delivery Summary

**Owner-facing review page: sample switcher over invented collections behind one prototype switch, loading and error states with retry, viewport-driven profile selection, and an ETag-cached layout endpoint, plus a plain-language layout guide.**

## Accomplishments

- `SampleCatalog` holds the `Prototype:Enabled` switch (missing means off, `true`/`false` in any case, anything else stops startup with `Prototype:Enabled must be true or false.`), the allowlist, `Resolve`, `ItemCount` and switcher labels. With it off the page shows only the being-built sentence (no nav, no mount, no module script) and `/cabinet/layout` answers 404.
- Page chrome follows the UI contract: heading, status line (`1 item` / `{n} items`), `nav[aria-label="Sample collection size"]` of plain `/?sample=` links with `aria-current` on the current one, footer holding only the version text, `noscript` message. Unknown sample values resolve silently to 65 and are never echoed (tested with a script-tag value).
- `LayoutCache` builds each sample and profile once per process; the endpoint sends a quoted ETag (`{layoutVersion}-{optionsFingerprint}-{sample}-{profile}`), `Cache-Control: no-cache`, no CORS headers, and answers 304 with an empty body on a matching `If-None-Match` (including `*` and weak tags).
- `cabinet.js` picks `phone` or `desktop` from one `(max-width: 40rem)` media query, re-fetches only when it flips, shows `COPY.loading` with `role="status"`, shows the error heading, body and Try again button on a non-OK status or network failure, and ignores superseded responses.
- `docs/cabinet-layout.md` documents what the layout is, every setting with defaults and ranges, how to override them in the server env file and restart, the stability contract with its accepted exceptions, and the invented collections. README current-state paragraph and guides list updated.

## Task Commits

1. Task 1 (tracer): sample switcher, status line and prototype switch - `d86a1f9`
2. Task 2 RED: failing tests for layout etags and conditional requests - `61cc0a1`
3. Task 2 GREEN: layout cache, etags, loading/error/profile handling - `14b893f`
4. Task 3: layout guide and README - `2eba30e`

## Verification

- `dotnet test --solution Cabinet.slnx`: 190 passed, 0 failed.
- `build/lint.sh repo-rules`: pass. `node --check` on every script: pass. No `innerHTML`, `outerHTML`, `insertAdjacentHTML` or `cssText` in `wwwroot/js`.
- Browser checks (scratch Playwright, Chromium headless shell, app in Development): sample 0 shows one section with no placements and the zero-items status; sample 400 shows 10 sections; no horizontal scroll at 1440 px; no console errors. With the layout request aborted once, the page shows "The cabinet could not be loaded." and Try again, and clicking it renders sections. Resizing 1440 to 390 sent exactly one request with `profile=phone`; back to desktop sent one with `profile=desktop`; resizing within desktop sent none.
- Tracer gate: the tracer's test suite and browser check were re-run and passed before expansion; the interactive human review is deferred to the end-of-phase batch.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Factory settings reach the app through UseSetting**
- **Found during:** Task 1
- **Issue:** The plan joined extra settings to the same in-memory collection as the port settings. The program reads `Prototype:Enabled` while building its services, before that later source is added, so the prototype-off and invalid-value tests saw the committed values.
- **Fix:** The overload applies each extra setting with `UseSetting`, which the program sees at build time; the port settings stay in the in-memory collection.
- **Files modified:** `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs`
- **Commit:** `d86a1f9`

**2. [Rule 3 - Blocking] Error and message styling overrides**
- **Found during:** Task 2
- **Issue:** `cabinet.css` (owned by the box-look slice, not editable here) styles `.cabinet-message` in the accent colour and `.cabinet-retry` with its own padding, so the messages did not use the wall-text colour from the contract.
- **Fix:** `site.css` sets `main .cabinet-message` to `--wall-text` and defines `.cabinet-message-heading`; everything else in `cabinet.css` is left as is.
- **Files modified:** `Cabinet.Service/wwwroot/css/site.css`
- **Commit:** `d86a1f9`

**Total deviations:** 2 auto-fixed (both blocking). No scope changes.

## Issues Encountered

- Until the phone section design exists, a phone-width viewport receives 404 and shows the error state, as the plan states. Nothing is released in between.
- A `pkill -f` used to restart the local dev app matched by command line and may have stopped other local processes whose command line mentioned the service project; nothing in the repository was affected.

## Known Stubs

None.

## Threat Flags

None. The new surface (sample query on the page, conditional headers on the layout endpoint) is covered by the plan's threat model and its tests.

## Self-Check: PASSED

- Created files exist: `Cabinet.Service/Prototype/SampleCatalog.cs`, `Cabinet.Service/Pages/Index.cshtml.cs`, `Cabinet.Service/Layout/LayoutCache.cs`, `docs/cabinet-layout.md`.
- Commits `d86a1f9`, `61cc0a1`, `14b893f`, `2eba30e` exist on the branch.

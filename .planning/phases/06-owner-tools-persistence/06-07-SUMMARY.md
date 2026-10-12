---
phase: 06-owner-tools-persistence
plan: 07
subsystem: owner-shell
tags: [razor-pages, owner-layout, css, playwright, accessibility]
requires:
  - phase: 06-01
    provides: "owner port, first owner layout and Settings page, OwnerStrings"
provides:
  - "OwnerPage base: Content-Language en on every owner answer, page name for title and bar"
  - "Finished _OwnerLayout in the fixed shell order with plate, bar, status region, token meta, Head and Scripts sections"
  - "_Footer partial shared by the public and owner layouts"
  - "owner.css: tokens, plate, bar, panel, five button styles, focus rings, 44px targets"
  - "CabinetPageTest.OwnerPort and GotoOwnerAsync; OwnerShellTests"
affects: [pictures, locations, settings, owner-address-cabinet]
tech-stack:
  added: []
  patterns:
    - "Owner look lives only in owner.css and the owner layout; the public stylesheets are untouched"
    - "Antiforgery token delivered in a meta element, no inline script"
key-files:
  created:
    - Cabinet.Service/Pages/Owner/OwnerPage.cs
    - Cabinet.Service/Pages/Owner/OwnerBarItem.cs
    - Cabinet.Service/Pages/Shared/_Footer.cshtml
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.BrowserTests/OwnerShellTests.cs
  modified:
    - Cabinet.Service/Pages/Shared/_OwnerLayout.cshtml
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - Cabinet.Service/Pages/Owner/Settings.cshtml
    - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
    - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
key-decisions:
  - "The page name flows through ViewData Title, set by OwnerPage before the handler; the layout derives the title and the bar's current item from it"
  - "--tag-size is declared on .placement, not :root, because --u only resolves inside the cabinet; a :root declaration would compute against the registered initial value of 0"
  - ".visually-hidden is redefined in owner.css because the public one lives in card.css, which owner pages do not load"
patterns-established:
  - "Owner bar items are a static list on OwnerPage; the Cabinet link is appended by the layout and pushed to the end"
requirements-completed: [OWN-01]
duration: 35min
completed: 2026-10-12
status: complete
actuals:
  tokens: 16000
  tasks: 2
  commits: 3
---

# Phase 6 Plan 07: The owner shell Summary

**English owner shell in the fixed order (skip link, plate, bar, main with status region, credited shared footer) with a complete owner stylesheet, proven in a real browser at 1440, 390 and 320px.**

## Accomplishments

- Every owner page answers `Content-Language: en` and `<html lang="en">` even with a Dutch language cookie, renders no language toggle, and carries the `Owner tools` plate in the header's second grid cell.
- The bar (`Pictures`, `Locations`, `Settings`, `Cabinet`) marks the current page with `aria-current="page"`; `Cabinet` is pushed to the end; items wrap at 320px and each keeps a 44px box.
- The footer (version and linked BGG credit) moved into `_Footer.cshtml` and both layouts render it; the public page's browser tests (139 in all) and the integration tests still pass.
- `owner.css` holds the new tokens, plate, bar, panel, the five button styles, wood and paper focus rings, hover rules gated by `(hover: hover)`, and dashed `aria-disabled` states. No accent fill, no filter, no animation.
- Owner pages load with no console error and request only the owner host.

## Task Commits

1. **Task 1 (tracer), red:** `304aba7` - failing shell tests, `OwnerPort` and `GotoOwnerAsync` on the test base (5 of 6 failed)
2. **Task 1 (tracer), green:** `5f82104` - OwnerPage, layout, footer partial, first stylesheet slice
3. **Task 2:** `1724459` - full owner stylesheet and the 320px / 44px / plate-not-focusable test

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] owner.css created in the tracer task**
- **Found during:** Task 1
- **Issue:** The layout links `owner.css`; without the file the page logs a failed-resource console error, which the tracer's own test forbids.
- **Fix:** Task 1 created a first slice of `owner.css` (plate placement, hidden text); Task 2 completed it.
- **Commit:** 5f82104

**2. [Rule 1 - Bug] `--tag-size` cannot live in `:root`**
- **Issue:** The plan declares it in `:root`, but it uses `--u`, which is a registered inherited length that only has a meaningful value inside the cabinet sections.
- **Fix:** Declared on `.placement` in `owner.css`, where it resolves against the section's unit.
- **Commit:** 1724459

**3. [Rule 3 - Blocking] `.visually-hidden` is not in `site.css`**
- **Issue:** The plan assumes it is shared; it is defined in `card.css`, which owner pages do not load.
- **Fix:** Defined in `owner.css` (the public stylesheets stay untouched).
- **Commit:** 5f82104

### Tracer gate

The run is not in GSD auto mode (both auto flags are false), which would normally make the tracer end with a human-verify stop. The plan is `autonomous: true` and runs as a parallel wave member that is never resumed, so the tracer's `<verify>` (the `OwnerShellTests` class, plus the whole browser and integration suites) was re-run after the commit instead; it passed before Task 2 started.

## Critic pass

Screenshots at 1440 and 390 were reviewed. Taste call for the owner: the page column is centred at `min(64rem, 100%)` as specified, so on a wide screen the page heading starts further in than the full-width bar and the left-aligned footer. Nothing is wrong, but it may look offset once pages have panels; a left-aligned column would be a small change in `.owner-main`.

## Known Stubs

None. The `Pictures` and `Locations` bar links answer 404 until their own pages exist; the tests check them by `href` only.

## Threat Flags

None. No new endpoints or network paths; the token is in a meta element and the layout has no inline script.

## Self-Check: PASSED

- Files created exist (OwnerPage.cs, OwnerBarItem.cs, _Footer.cshtml, owner.css, OwnerShellTests.cs).
- Commits 304aba7, 5f82104 and 1724459 exist on the branch.
- `bash build/lint/checks/10-repo-rules.sh` exits 0; `dotnet test --project Cabinet.BrowserTests` 139 passed, 0 failed; integration tests 0 failed.

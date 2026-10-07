---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 04
subsystem: page
status: complete
tags: [shared-layout, bgg-credit, licence, static-asset]
requires: []
provides:
  - "Pages/_ViewStart.cshtml and Pages/Shared/_Layout.cshtml: one document shell for every page, with a Scripts section"
  - "Footer with the version paragraph first and the linked Powered by BGG credit second"
  - "Official Powered by BGG logo (SVG) served from the site's own origin"
  - "CreditTests: every Razor page renders the credit; logo served as an image; no foreign origin referenced"
affects: [every future page (inherits the credit through the layout), end-of-phase screenshot review]
tech-stack:
  added: []
  patterns: ["layout applied through _ViewStart so a page cannot skip it", "endpoint-data-source enumeration test guarding the licence requirement"]
key-files:
  created:
    - Cabinet.Service/Pages/_ViewStart.cshtml
    - Cabinet.Service/Pages/Shared/_Layout.cshtml
    - Cabinet.Service/wwwroot/img/powered-by-bgg.svg
    - Cabinet.IntegrationTests/CreditTests.cs
  modified:
    - Cabinet.Service/Pages/Index.cshtml
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.IntegrationTests/CabinetPageTests.cs
    - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
decisions:
  - "Logo variant: Color/SVG reversed RGB artwork, made for dark backgrounds, so no plate (see below)"
  - "The logo is an SVG; every reference uses .svg and no PNG was added"
metrics:
  tasks: 3
  commits: 2
actuals:
  tasks: 3
  commits: 2
---

# Phase 3 Plan 04: Linked BGG credit on every page Summary

One shared layout puts the version text and BGG's linked, official Powered by BGG logo (reversed RGB SVG, served from the site's own origin, with intrinsic dimensions) in the footer of every page, with tests that fail if any page skips it.

## Tasks

| Task | Name | Commit | Notes |
| ---- | ---- | ------ | ----- |
| 1 | Every page carries the linked BGG credit through one shared layout | 42be62b | Tracer; `_ViewStart`, `_Layout`, slimmed `Index.cshtml`, `.bgg-credit` styles, `CreditTests` |
| 2 | The owner supplies the official logo (human-action checkpoint) | none | Resolved: the owner downloaded the official logo pack by hand; the orchestrator picked the variant |
| 3 | The official logo is served from the site without layout shift | 7eba641 | Asset copied unchanged, intrinsic size set, tests added |

## Logo provenance and plate decision

- Source: the official Powered by BGG logo pack (reversed RGB SVG) the owner downloaded from BoardGameGeek's logo page linked from the XML API terms. Copied unchanged. Source page URL to be confirmed by the owner.
- Variant: `powered-by-bgg-reversed-rgb.svg`, 6588 bytes, sha256 `b577fd17bd5f84ea575fb10ff1f7fa3895b54727728bfd4a4ed11300a722bc43`, verified identical before and after the copy. Intrinsic size 342 by 76, viewBox 0 0 342 76. It is stored as `Cabinet.Service/wwwroot/img/powered-by-bgg.svg`.
- Safety inspection (re-run before committing, nothing changed): only `svg`, `title`, `g`, `path` and `polygon` elements; no script, foreignObject, image, href, `url()`, entity/doctype or event attributes. An unused `xmlns:xlink` declaration is present and harmless.
- Plate rule: the artwork is the reversed version made for dark backgrounds (fills only white and the BGG orange), so it sits directly on the dark wood backdrop. No `.bgg-credit-plate` was added.
- Rendering: the `img` carries `width="342" height="76"`; CSS keeps `height: 32px; width: auto`, so it renders at about 144 by 32 and the browser reserves the space before load. The SVG is never recoloured, filtered, stretched or cropped.

## Verification

- `dotnet test --solution Cabinet.slnx`: 449 passed, 0 failed.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all pass.
- The static file is served with an `image/*` content type (asserted), and the `default-src 'self'` policy is unchanged; the logo path is now in the strict-policy path list.
- Not run here: the visual 320px, 390px and 1440px screenshot check (a backstop for the end-of-phase screenshot review, per the instruction not to stop for it).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Format and extension changed from PNG to SVG**
- **Found during:** Task 3 (the plan anticipated this: "if it is SVG, use .svg and update every reference")
- **Fix:** Layout reference, tests and the CSP path list use `.svg`; the plan's `.png` file name was never created. The Task 1 layout (commit 42be62b) referenced `.png` without dimensions; Task 3 changed it to `.svg` with `width="342" height="76"`.
- **Files modified:** `Cabinet.Service/Pages/Shared/_Layout.cshtml`
- **Commit:** 7eba641

**2. [Rule 1 - Bug] A test assertion tripped the repo-rules `//` comment check**
- **Found during:** Task 3 lint run
- **Issue:** the string `"//"` in a protocol-relative URL assertion was read as a comment.
- **Fix:** replaced it with the regular expression `^/(?!/)` (a local path that is not protocol-relative).
- **Files modified:** `Cabinet.IntegrationTests/CreditTests.cs`
- **Commit:** 7eba641

No lint or `.gitattributes` entry was needed for the SVG (no `.gitattributes` exists and the secrets/repo-rules checks accept it).

## Issues Encountered

One run of the integration test project reported a single failing test immediately after a full lint run; the cause was not captured, and 7 further runs of the project passed with 0 failures. Treat as a possible pre-existing flaky test to watch, not caused by this plan's changes as far as can be told.

## Known Stubs

None.

## Threat Flags

None. The only new outbound surface is the fixed `https://boardgamegeek.com` credit link with `rel="noopener"`, already in the plan's threat model (T-03-12 to T-03-15 mitigated: official owner-supplied asset inspected, served from own origin, link asserted by test, every page route checked).

## Self-Check: PASSED

- `Cabinet.Service/wwwroot/img/powered-by-bgg.svg`, `Cabinet.Service/Pages/Shared/_Layout.cshtml`, `Cabinet.Service/Pages/_ViewStart.cshtml`, `Cabinet.IntegrationTests/CreditTests.cs` exist.
- Commits 42be62b and 7eba641 exist on the branch.

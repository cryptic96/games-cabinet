---
phase: 08-public-hardening-go-public
plan: 08
subsystem: hosting
tags: [security-headers, csp, permissions-policy, browser-tests, webkit, bgg-credit]
requires:
  - phase: 08-01
    provides: per-client sync refusal used as a 429 row
  - phase: 08-05
    provides: live negotiate refusal used as an empty-body 429 row
  - phase: 08-07
    provides: host filtering, whose 400 is the tested exclusion
provides:
  - SecurityHeaders constants and UseSecurityHeaders() as the first middleware
  - header matrix over every response type on the public, owner and ops listeners
  - clean-console proof in Chromium and WebKit under the full header set
  - credit visibility proof in every cabinet page state at desktop and phone width
affects: [08-06, 08-12, 08-15]
tech-stack:
  added: []
  patterns: [one first middleware assigning every header, exactly-once helper asserting each header and its value]
key-files:
  created:
    - Cabinet.Service/Hosting/SecurityHeaders.cs
    - Cabinet.IntegrationTests/SecurityHeadersTests.cs
    - Cabinet.BrowserTests/HeaderConsoleTests.cs
    - Cabinet.BrowserTests/HeaderConsoleWebKitTests.cs
    - Cabinet.BrowserTests/CreditVisibilityTests.cs
  modified:
    - Cabinet.Service/Program.cs
key-decisions:
  - "Content-Security-Policy value unchanged; compared with the constant and with a literal copy so a looser policy fails a test"
  - "X-Frame-Options DENY sent beside frame-ancestors 'none'; no COEP; HSTS stays with the proxy"
  - "No Permissions-Policy feature had to be removed: Chromium and WebKit logged nothing for the 13 listed features"
  - "The browser's own favicon request is the single console message ignored, matched by location"
status: complete
actuals:
  tokens: 60000
  tasks: 3
  commits: 3
---

# Phase 8 Plan 08: Full security-header set and a clean cabinet under it Summary

One first-in-pipeline middleware now sends the unchanged Content-Security-Policy plus nosniff, Referrer-Policy, X-Frame-Options DENY, a Permissions-Policy, COOP and CORP same-origin with every response, proven exactly once on every response type and listener, with a clean console in Chromium and WebKit and the BGG credit visible in every page state.

## Tracer gate

Tracer gate: auto (owner away), verify re-run passed. After the tracer commit the header test class passed (19 of 19) before any expansion work.

## Accomplishments

- `SecurityHeaders` replaces `ContentSecurityPolicy` (renamed with `git mv`): seven `public const string` values and `UseSecurityHeaders()`, which assigns (never appends) before `await next`, so the box art and owner preview endpoints that set nosniff themselves replace the value instead of doubling it. `Program.cs` calls it as the first middleware.
- `SecurityHeadersTests` replaces the old policy test class. `AssertFullSet` checks all seven headers exactly once with the expected value, the CSP against both the constant and a literal copy. Every previous row and fact kept, plus: served box picture, missing picture 404, per-client sync 429 (JSON), live negotiate 429 (empty body), owner page, owner settings, owner script, owner JSON, owner 404, owner range refusal 403, owner live negotiation, ops `/health`, and the 304 revalidation from before.
- Known exclusions are written into the class summary (host filtering 400, Kestrel's 408/413/431, an unhandled-exception 500) and one fact proves the host-filtering 400 has no CSP.
- `HeaderConsoleTests` (Chromium) and `HeaderConsoleWebKitTests` (WebKit, `CABINET_WEBKIT=1`): the public cabinet with the prototype sample and an owner pictures page record zero console messages of any type, zero page errors and zero `securitypolicyviolation` events. A shared `ConsoleWatch` helper lives in the Chromium file.
- `CreditVisibilityTests`: the `footer a.bgg-credit` link and its image are visible, point at `https://boardgamegeek.com` and sit inside the page width, on the drawn cabinet, the filling message, an empty collection and a failed layout load (500), at 1440x900 and 390x800.

## Choices recorded

- **Favicon exception:** the site has no icon and no icon link by design. A browser's own `/favicon.ico` request answers 404 and Chromium may log it, so the watch ignores exactly the console message whose location names `/favicon.ico` (a `///` summary says why). In practice headless Chromium and WebKit logged no such message in these runs. Serving an icon would be a visual change and an inline `data:` icon would break the policy.
- **Permissions-Policy:** no feature was removed. A deliberate check confirmed the test bites: adding an invented feature name made Chromium log "Unrecognized feature" and both Chromium tests failed; reverted.
- **Empty collection state:** the empty collection draws neither a placement nor a message, so that test waits for the cabinet room and asserts no placements rather than using the shared ready selector.

## Verification

- Header test class: 32 of 32. Full integration suite: 603 run; the one failure was `TestHostCleanupTests` under the polling variable, which the plan says to run alone without it, and it passed alone.
- Chromium: `HeaderConsoleTests` 2 of 2, `CreditVisibilityTests` 8 of 8 both normally and under a DejaVu fontconfig, `LightsTests` 21, `FilterAccessTests` 26, `FilterDockMemoryTests` 13, all solo.
- WebKit (installed here): `HeaderConsoleWebKitTests` passed with `CABINET_WEBKIT=1`.
- Whole browser project in one run: 799 total, 797 passed, 2 skipped (the WebKit-only classes when the variable is unset), 0 failed. COOP and CORP did not break any existing flow.
- `bash build/lint/checks/10-repo-rules.sh` passes.

## Deviations from Plan

None - plan executed as written. The new browser tests were not split into separate Chromium and shared helper files beyond the three listed; the `ConsoleWatch` helper is a second type inside `HeaderConsoleTests.cs`.

## Known Stubs

None.

## Threat Flags

None. No new network surface; the change only adds response headers.

## Task Commits

1. Task 1 (tracer): full header set first in the pipeline - 84d6167
2. Task 2: matrix over art, refusals, owner and ops answers - 2c9ec59
3. Task 3: clean console in two engines and credit visibility - b317635

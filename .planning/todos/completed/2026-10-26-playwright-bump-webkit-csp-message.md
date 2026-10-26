---
created: 2026-10-26T13:00:00.000Z
title: Let the Playwright bump pass the WebKit console test
area: testing
severity: minor
resolves_phase: 8
files:
  - Cabinet.BrowserTests/HeaderConsoleWebKitTests.cs
  - Cabinet.BrowserTests/HeaderConsoleTests.cs
---

## Problem

Dependabot PR #33 (Playwright group bump; WebKit 27.2, playwright webkit v2370), rebased on main after v1.0.0, fails `browser-webkit`: `HeaderConsoleWebKitTests` records the console error "Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline' appears in neither the style-src directive nor the default-src directive of the Content Security Policy." on the public cabinet page. Every other slice passed. The app ships no inline `<style>`, no constructed or inserted stylesheet and no `data:`/`blob:` CSS, so the likely source is a stylesheet Playwright itself injects (for example around screenshots), which the newer WebKit now refuses under the site's strict CSP. Not yet proven.

## Solution

Confirm the source (run the WebKit test locally with the bumped Playwright; check whether the message appears without any screenshot or locator highlight). If it is Playwright's own style, take console readings before any screenshot or filter that one tool-injected message narrowly, then merge #33. If it is the app, fix the app (and check real Safari). Do not loosen the CSP.

## Closed 2026-10-26

The guess in the problem text was wrong on both counts: the refused stylesheet is neither the app's nor Playwright's. Reproduced locally with Playwright 1.64.1 (WebKit 27.2, webkit-2370; the previous pin ran WebKit 26.6, webkit-2359).

Cause: WebKit 27.2 itself, for every `<select>` element. The engine applies an inline style of its own to the select's internals and holds it against the page policy, so each select logs the console error and fires one `securitypolicyviolation` (`style-src-attr`, blocked URI `inline`), attributed to the select's own source position. Evidence:

- The console message location is the page document (`/:156:0`), not a script or stylesheet; no `<style>` element, adopted sheet or `style` attribute exists in the DOM; no screenshot or `Expect` highlight is involved in the failing test.
- Removing scripts, stylesheets, `<use>` icons, images, dialogs, fieldsets, inputs or `hidden` attributes from the served page did not change it; removing the `<select>` did. A bare document holding only a `<select>`, served under the site policy, reproduces it; the same document without a policy header does not.
- One select gives one message and one violation, three selects three each, also for a select created later by a script. No other form control does it (datalist, date, range, colour, file, number, textarea, meter, progress, details, popover, dialog, checkbox, radio, search, button were all clean). Only the public cabinet page holds a select (the location filter); the owner pictures and settings pages hold none.
- The select renders identically with and without the policy (same box, identical screenshot bytes), so it looks harmless; the opened picker cannot be checked headless.
- Side finding: a Playwright screenshot does inject an inline style that WebKit 27.2 refuses (`style-src-elem`), so a console watch must never be read across a screenshot. The header console tests take none.

Fix: `HeaderConsoleWebKitTests` tolerates exactly the engine's per-select notice and nothing else. The per-select count is measured on a probe page of one select under the real policy (`SecurityHeaders.ContentSecurityPolicy`, answered by a Playwright route, no network), so the allowance is zero on WebKit 26.6 (verified: green on Playwright 1.63.0 too) and never exceeds one message and one violation per select element. A second WebKit test shows an inline style next to a select is still reported (mutation-checked: an unbounded allowance fails it). The policy is unchanged and Chromium's `HeaderConsoleTests` is untouched.

Real Safari: a Safari shipping this WebKit will show the same console error for the location filter select, and fire the violation event (the policy has no report endpoint, so nothing is sent). No visible effect found. If it should be silent, the only app-side options are replacing the `<select>` (a visible change) or allowing inline styles (refused). Worth re-checking in a real Safari 27 once available; the allowance can then be removed if WebKit stops doing it.

Local note: WebKit 27.2 needs `libmanette-0.2-0` on the host (CI installs it with `--with-deps`); locally it was supplied without root by unpacking the package into the browser bundle's `sys/lib`.

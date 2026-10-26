---
created: 2026-10-26T13:00:00.000Z
title: Let the Playwright bump pass the WebKit console test
area: testing
severity: minor
files:
  - Cabinet.BrowserTests/HeaderConsoleWebKitTests.cs
  - Cabinet.BrowserTests/HeaderConsoleTests.cs
---

## Problem

Dependabot PR #33 (Playwright group bump; WebKit 27.2, playwright webkit v2370), rebased on main after v1.0.0, fails `browser-webkit`: `HeaderConsoleWebKitTests` records the console error "Refused to apply a stylesheet because its hash, its nonce, or 'unsafe-inline' appears in neither the style-src directive nor the default-src directive of the Content Security Policy." on the public cabinet page. Every other slice passed. The app ships no inline `<style>`, no constructed or inserted stylesheet and no `data:`/`blob:` CSS, so the likely source is a stylesheet Playwright itself injects (for example around screenshots), which the newer WebKit now refuses under the site's strict CSP. Not yet proven.

## Solution

Confirm the source (run the WebKit test locally with the bumped Playwright; check whether the message appears without any screenshot or locator highlight). If it is Playwright's own style, take console readings before any screenshot or filter that one tool-injected message narrowly, then merge #33. If it is the app, fix the app (and check real Safari). Do not loosen the CSP.

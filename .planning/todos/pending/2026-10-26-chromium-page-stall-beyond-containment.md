---
created: 2026-10-26T17:00:00.000Z
title: Chromium page stalls on CI also hit tests outside the containment test
area: testing
severity: minor
files:
  - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
  - Cabinet.BrowserTests/FilterSlimBarTests.cs
---

## Problem

On a busy GitHub runner, headless Chromium occasionally stops answering for one page (screenshot, script, wait), which was found and handled for `LookContainmentTests` with a proven-stall check and a fresh-page retry. The `v1.0.1` pull request then failed `browser-filters` once: `FilterSlimBarTests.The_dock_never_slides_and_the_listener_exists_only_on_a_phone` timed out after 30 s in `ScrollToAsync` (`Page.WaitForFunctionAsync`), in a test unchanged since phase 7 and unrelated to the change; the failed job was re-run once. The same stall can hit any browser test; only the containment test recovers from it.

## Solution

Decide whether the proven-stall recovery (probe the page with a trivial script; replace it only when the probe also fails) belongs in the shared page base for every test, or only for the few tests that take many screenshots or long waits. Track how often `stalled-pages.txt` appears in CI artifacts first. Never retry an ordinary timeout on a responsive page.

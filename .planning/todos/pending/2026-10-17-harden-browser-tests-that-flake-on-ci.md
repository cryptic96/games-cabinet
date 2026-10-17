---
created: 2026-10-17T15:00:00.000Z
title: Harden the browser tests that flake on the CI runner
area: testing
severity: minor
files:
  - Cabinet.BrowserTests/FilterShowOnShelfTests.cs
  - Cabinet.BrowserTests/LiveLookWebKitTests.cs
  - Cabinet.BrowserTests/BackButtonTests.cs
  - Cabinet.BrowserTests/CardSyncTests.cs
  - Cabinet.BrowserTests/SheetDragTests.cs
  - Cabinet.BrowserTests/DefaultLookIdentityTests.cs
---

## Problem

During the release of the game-night filters, several browser tests failed on the GitHub runner and passed on a re-run, while they always pass locally (also on two CPU cores and under the CI font):

- The first pull request run failed 23 tests in the "rest" job (card dialogs that did not close within the expected time, `BackButtonTests`, `SheetDragTests`, `CardSyncTests`, `CardOpenTests`, card look tests, a sample-400 page that did not draw a section within 30 seconds in `DefaultLookIdentityTests`, `LiveLookTests`) and the single WebKit test.
- The run on `main` after the merge failed `FilterShowOnShelfTests.Show_on_shelf_lands_on_the_box_outlined` (the landing that already failed 2 of 3 local runs by 188 px before a settle-and-measure loop was added) and the WebKit test again (`LiveLookWebKitTests.A_design_change_keeps_the_section_in_the_middle_of_the_screen_where_it_was`: the live design change did not arrive, `data-design` stayed `classic`).

All passed on re-run. A red check that turns green on a re-run teaches people to ignore red checks.

## Solution

Find the waits that assume a fast machine: replace fixed timeouts and one-shot measurements with retrying assertions on the end state (dialog closed, section drawn, design applied), wait for the live-update connection to be up before publishing a look, and settle the Show on shelf landing on a stable layout (no layout shift for two frames) before measuring. Reproduce first by running the failing classes several times in a row with CPU throttling, and record the before and after failure rates.

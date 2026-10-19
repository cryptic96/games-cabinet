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

## Progress

- **2026-10-18 (plan 07-40):** two of the tests on the list are hardened; the rest stays open.
  - `FilterShowOnShelfTests.Show_on_shelf_lands_on_the_box_outlined`: the recorder no longer measures the landing in the same moment the outline is set. It measures once the found box's top has stayed the same for two animation frames, and it reads the veil on every frame while the box is marked until the fade has ended at 0, so a value caught in the 160 ms fade is never taken for the answer. The desktop landing now reads "below the viewport top" (the filters stand in the dock beside the cabinet).
  - `LiveLookWebKitTests.A_design_change_keeps_the_section_in_the_middle_of_the_screen_where_it_was`: before publishing, the host sends the page the status it already shows until a spy on the page's live client has heard it, which proves a push gets through from end to end at that moment (a server that still counts the connection did not prove that: a page drawing 400 games can keep its main thread busy long enough for the connection to drop and come back). After the save the quiet period is let pass on the fake clock again, a bounded number of times, until the design is on the page, and the end state is read with retrying assertions.
  - Repeated local runs (details in `07-40-SUMMARY.md`): the WebKit test passed 20 of 20 before and after the change, also on two cores (20 of 20) and, before, on one core (10 of 10), so the runner's failure did not reproduce here; a timing probe showed the design lands about 0.3 s after the push when the push arrives, so the 5 s wait was never the problem and the push itself was missed. The landing test passed 20 consecutive runs and 20 on two cores after the change.
  - Still open: card dialogs that did not close in time, `BackButtonTests`, `SheetDragTests`, `CardSyncTests`, the sample-400 first draw in `DefaultLookIdentityTests`, and `LiveLookTests`.

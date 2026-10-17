---
phase: 07-game-night-filters-location-cabinets
plan: 31
subsystem: ui
tags: [owner-answer, furniture-ring, view-transition, copy, guides, deploy-guide]
status: complete

requires:
  - phase: 07-game-night-filters-location-cabinets
    provides: "07-REVIEW-ROUND-1.md with the Critic findings and the owner's questions (plan 07-30)"
provides:
  - "The owner's answer recorded verbatim in 07-REVIEW-ROUND-1.md, label by label, with D1, K2 and K4 marked done with their commits"
  - "Found and open box marked with the 3px furniture ring (visible on every wood), pinned by a contrast test on all five woods by day and evening"
  - "View switch fades through the wall instead of cross-fading two layouts"
  - "The 8+ players chip is spoken as 8+ players / 8+ spelers"
  - "Two todos for later changes (side-by-side location cabinets, no-script Undo per copy)"
  - "README, development, looks and deploy guides describing the filters, location cabinets, per-copy choices and the format change on rollback"
affects: [07-32, 07-34, filters, owner-pages, deploy-guide]

key-files:
  created:
    - .planning/todos/pending/2026-10-17-side-by-side-location-cabinets-sized-to-their-games.md
    - .planning/todos/pending/2026-10-17-no-script-undo-should-carry-the-copy-id.md
  modified:
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.Service/wwwroot/css/card.css
    - Cabinet.Service/wwwroot/js/copy.js
    - Cabinet.Service/Language/SiteText.cs
    - build/tests/fixtures/filter-copy-cases.json
    - build/tests/card-css.test.mjs
    - Cabinet.UnitTests/Configuration/LookChromeContrastTests.cs
    - Cabinet.BrowserTests/FilterBarTests.cs
    - Cabinet.BrowserTests/FilterShowOnShelfTests.cs
    - Cabinet.BrowserTests/ReducedMotionTests.cs
    - README.md
    - docs/development.md
    - docs/cabinet-looks.md
    - docs/deploy.md
    - .planning/phases/07-game-night-filters-location-cabinets/07-REVIEW-ROUND-1.md
    - .planning/phases/07-game-night-filters-location-cabinets/ui-refs/build-round/

key-decisions:
  - "Owner's answer was 'all recommended': D1 yes, K2 B and K4 change; every other label kept as built, T1 Dutch approved as drafted"
  - "The owner's yes on D1 overrides the earlier FIXED tag on the found/open outline colour"
  - "K1 B and N17 become todos instead of changes in this release"

patterns-established:
  - "The open/found mark and the keyboard focus ring share one colour source (--wood-light) and differ only in width (3px against 2px)"

requirements-completed: [FILT-01, FILT-02, FILT-03, FILT-04, FILT-05, EXP-04, LOC-01, LOC-05]

duration: about 1 session
completed: 2026-10-17
actuals:
  tokens: 28000
  tasks: 3
  commits: 8
---

# Phase 7 Plan 31: The owner decides, the choices land, and the guides describe the filters Summary

**The owner's "all recommended" is applied: the found and open box wear a 3px furniture ring that shows on Walnut, the view switch fades through the wall, the 8+ chip is spoken "8+ players", two later changes are noted as todos, and the guides describe filters, location cabinets, per-copy choices and the format change on rollback.**

## Accomplishments

- **Task 1 (resolved before this run):** the owner answered the blocking checkpoint in chat with "all recommended". It is recorded verbatim under "Owner's answer" in `07-REVIEW-ROUND-1.md` with a label-by-label table (what each answer meant, what was done, commit).
- **Task 2:**
  - **D1** (`b1b0c29`): `.placement[data-open], .placement[data-found]` now draws `outline: 3px solid var(--wood-light); outline-offset: 2px;` and keeps the `0 0 0 6px var(--ring-halo)` halo. The doc comment says the colour reads against the furniture like the focus ring and is 3px to stay distinct from the 2px ring. The yes overrides the earlier FIXED tag. A new unit test measures the mark on frame/shelf, plain back and painted back of all five woods, by day and evening (10 cases), and checks the shipped rule draws it in 3px with the same colour.
  - **K2** (`3cddf51`): in `card.css` the old view fades out in half the look fade and the new view is delayed by half and fades in during the second half, same total time; the group's fill mode keeps the old gone, and the reduced-motion block still silences every view-transition animation. `card-css.test.mjs` pins the three declarations.
  - **K4** (`1ee9d7f`): the eight chip's spoken name is "8+ players" / "8+ spelers" in `copy.js`, `SiteText.cs` and the shared fixture; other chips unchanged; the browser test reads the new name.
  - **Test updates for D1** (`a8d148b`): the Show on shelf landing checks and the reduced-motion open-box check now expect the 3px furniture ring colour of the default wood.
  - **Todos** (`3c4d577`): side-by-side location cabinets sized to their games (K1 B) and the no-script Undo carrying the copy's collection id (N17).
  - **Frames** (`0f56ad2`): all 14 M7 frames and all 8 M8 frames re-shot with the build-round tooling under the DejaVu fontconfig on loopback with synthetic data, each looked at before the manifest was marked `viewed`. Walnut's found box is now clearly marked; the M8 middle frames show the old view fading into the wall alone (captured about 800 ms of real time into a 12x slowed animation clock). Card frames and settled frames came out identical. No frame shows the 8+ chip's name, so none re-shot for K4.
- **Task 3** (`d6dcd19`): README paragraph on search and filters, the shadow look, the remembered view, Show on shelf and per-copy choices with the one-line pointer to the deploy guide's "Rolling back after a format change"; development guide section on seeing the filters and location cabinets locally and the five CI browser jobs; looks guide section on the shadow, wall chrome, the found/open furniture ring (3px) and fading through the wall; deploy guide paragraph on the per-copy format change with the `"schemaVersion":2,` copy and the matching `grep -l` line beside the existing `"schemaVersion":1,` example.

## Task Commits

1. D1 furniture ring plus contrast test: `b1b0c29`
2. K2 fade through the wall: `3cddf51`
3. K4 8+ players name: `1ee9d7f`
4. Two todos: `3c4d577`
5. Guides (Task 3): `d6dcd19`
6. Browser tests updated for the 3px ring: `a8d148b`
7. Owner's answer recorded and frames re-shot: `0f56ad2`
8. This summary: the docs commit that follows

## Deviations from Plan

### Approved file-list extensions

**1. [Rule 3 - Blocking] Files outside the plan's list**
- **Found during:** Task 2
- **Issue:** the owner's answer needed `card.css` and `card-css.test.mjs` (K2), `LookChromeContrastTests.cs` (D1 contrast test), and the browser tests that read the chip name (`FilterBarTests.cs`).
- **Fix:** all approved by the orchestrator in the task brief.
- **Commits:** `b1b0c29`, `3cddf51`, `1ee9d7f`

**2. [Rule 1 - Bug] Two browser tests asserted the old 2px outline**
- **Found during:** Task 2 browser run
- **Issue:** `FilterShowOnShelfTests` expected a 2px outline and `ReducedMotionTests` expected the wall's text colour on the open box; both are the old mark.
- **Fix:** they now expect 3px and the default wood's furniture ring colour.
- **Files modified:** `Cabinet.BrowserTests/FilterShowOnShelfTests.cs`, `Cabinet.BrowserTests/ReducedMotionTests.cs`
- **Commit:** `a8d148b`

**Total deviations:** 1 approved file-list extension, 1 auto-fixed (test pins of the changed mark). **Impact:** none on scope.

## Findings for the owner and deferred items

- **Plain-font failure that predates this plan:** `FilterSlimBarTests.The_search_hint_is_never_cut_in_the_middle_of_a_word_while_a_filter_is_in_use` fails with the machine's normal fonts at English 390 px (hint 105.27 px, room 106 px, so the test expects the hint drawn, but the container rule at 9.75rem has already swapped it for the magnifier) and passes under the DejaVu fontconfig. It fails the same way on the tree from before this plan (`b5cb0f2`, checked in a throwaway worktree, since removed). The review document's "normal fonts 728 passed" does not match this, so the boundary between the CSS threshold and the test's fit check is font-dependent. Not touched here; it is the D2 fix from plan 07-30 and wants a look in the next plan that touches the slim bar (the test could compare against the container threshold instead of the measured text, or the threshold could follow the text).
- **View fade on the phone with the sheet open:** while the fade runs, the browser paints the fading cabinet picture above the open Filters sheet (a view transition sits above the top layer). It was so before this change (the earlier frame shows the same, only as a double exposure) and lasts a quarter second; noted in the review document.
- `TestHostCleanupTests` fails under the polling file watcher variable, as known; ignored.

## Verification

- `node --test build/tests/*.test.mjs`: 706 passed.
- `Cabinet.UnitTests` in full: 2933 passed (2923 plus the 10 new contrast cases), including `LookChromeContrastTests`, `DimmedBoxContrastTests`, `SiteTextTests`, `FilterCopyTests`.
- `Cabinet.IntegrationTests`: all passed except the known `TestHostCleanupTests`.
- Browser classes with `DOTNET_USE_POLLING_FILE_WATCHER=1`, after the final build: `Filter*` (146), `Look*` (191), `Card*` + `ReducedMotionTests` + `OwnerCabinetTests` (71) with normal fonts, `Filter*` and `Look*` again under the DejaVu fontconfig, then a re-run of `FilterShowOnShelfTests`, `ReducedMotionTests` and `FilterSlimBarTests` and `DefaultLookIdentityTests` (5 passed). Everything passes except the plain-font slim-bar case above.
- Task 3 verify: the five grep checks and `build/lint/checks/10-repo-rules.sh` passed; the full `bash build/lint.sh` passed (repo-rules, workflows, shell, secrets, script-tests).
- Manifest: 164 rows, all `viewed`; the local servers (fake BGG and host) were stopped.

## Known Stubs

None.

## Threat Flags

None. Guides use only the placeholder names Cabinet A and Shelf B and no version numbers or planning references (T-07-31-01); the answer is verbatim and each change links to its commit (T-07-31-02); contrast, copy-parity and CSS-structure tests were rerun after each change (T-07-31-03).

## Self-Check: PASSED

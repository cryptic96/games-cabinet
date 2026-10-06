---
phase: 02-layout-engine-cabinet-prototype
plan: 07
subsystem: ui
tags: [owner-review, screenshot-review, tuning, playwright, layout-version-7, golden-layouts]
status: complete
requires:
  - phase: 02-layout-engine-cabinet-prototype
    provides: phone section, readability floors, recorded layouts, classic finish, box poses, expansion families (plans 01 to 06 and 09)
provides:
  - "Owner approval of the cabinet look after two screenshot review rounds (approved 2026-10-06)"
  - "Round 2 tuning: label shortener without dangling stop words, last section trimmed to its last used row, centred one-to-three column section rows, whole-word cover titles, soft plinth shade"
  - "Layout version 7 with re-recorded golden layouts"
  - "Recorded final tuning values and the open taste calls"
  - "Owner consent to merge into main and push the next 0.x minor tag"
affects: [merge and release plan, deployed check, selectable finishes and lit cubbies todo, later visual polish]
actuals:
  tokens: 40000
  tasks: 3
  commits: 6
tech-stack:
  added: []
  patterns:
    - "Review script lives only in the session scratch directory; checks (a) to (n) run on every sample and width and print one PASS or FAIL line per page"
    - "A tuning change that alters the arrangement raises the layout version and re-records goldens in the same commit"
key-files:
  created:
    - Cabinet.UnitTests/Layout/TrimmedSectionTests.cs
  modified:
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.Service/wwwroot/css/cabinet.css
    - Cabinet.Service/wwwroot/css/site.css
    - Cabinet.UnitTests/Layout/Golden/
    - docs/cabinet-layout.md
key-decisions:
  - "Only the last section is trimmed to its last used row (minimum two rows); trimming earlier sections would need an exception to the stability rules and was left as an open taste call"
  - "Sections wrap into centred rows of one, two or three columns with a maximum width of 123rem instead of stretching, so a lone section stays centred"
  - "Cover titles keep words whole and use an ellipsis with a four-line clamp rather than breaking inside a word"
  - "The plinth opening is a soft half-ellipse shade (alpha 0.6, straight bottom edge) instead of a hard-cut arch"
  - "The owner's approval accepts the look with the listed taste calls still open"
requirements-completed: [CAB-01, CAB-02, CAB-04, CAB-05, CAB-06, CAB-07, EXP-01, EXP-02, EXP-03]
metrics:
  rounds: 2
  tasks: 3
  commits: 6
duration: two review rounds
completed: 2026-10-06
---

# Phase 2 Plan 07: Owner screenshot review loop Summary

**The owner approved the cabinet's look in round 2 of the screenshot review loop, after round-1 feedback produced shorter clean labels, a trimmed last section, centred one-to-three column section rows, whole-word cover titles and a soft plinth shade (layout version 7).**

## Performance

- **Rounds:** 2 (round 1 changed nothing in the repository, round 2 applied the owner's four change requests)
- **Approved:** 2026-10-06
- **Tasks:** 3 (Task 1 ran twice, Task 2 asked twice, Task 3 ran once; it does not run again)
- **Round 2 commits:** 6, merged into the milestone branch as `13d6b24`

## Accomplishments

- Both rounds ran a scratch Playwright review script over 27 pages with strict `default-src 'self'` applied through a route handler, and the owner answered "approve" at the second checkpoint.
- Four owner change requests from round 1 were applied and re-checked: label shortening, last-section trimming, section column layout, cover title wrapping, plus the plinth opening that read as a hard cut.
- The recorded layouts moved from version 6 to 7 and were re-recorded; 393 tests pass (358 unit, 35 integration) and lint passes on the approved commit.

## Round 1

No repository commits. The review script ran over 27 pages: samples 0, 1, 5, 12, 65, 400 and edge at 1440, 390 and 320 px, plus 1280, 1920 and 2560 px for 65 and 400. Checks (a) to (h) and (j) passed on 27 of 27 pages. Check (i), the hard-cut shadow or overhang, is a judgement by eye, and the screenshots showed the plinth arch with a hard cut. The worst label contrast under the shade was 5.28:1.

One defect was in the review tool, not the app: `content-visibility: auto` left off-screen sections blank in Playwright full-page captures. The scratch script now forces sections visible and grows the viewport before capturing.

The owner requested four changes, applied in round 2:

1. Shortened labels must not end on a dangling stop word or separator.
2. The last section should be drawn only down to its last used shelf row.
3. Sections should sit in columns on wide screens instead of one stretched row.
4. Cover titles must not break inside a word.

The owner also flagged the hard-cut plinth arch, fixed in round 2.

## Round 2

| Change | Commit |
| ------ | ------ |
| Label shortener drops dangling stop words and separators; layout version 6 to 7; goldens re-recorded | `a8bc6eb` |
| Last section drawn only to its last used row, at least two rows (`CabinetLayoutEngine.MinTrimmedRows = 2`); `TrimmedSectionTests` with 18 tests; docs | `725f00c` |
| Sections wrap into centred flex rows: `--section-basis` is min(40rem, 100%), halves from 80rem, thirds from 118rem, maximum width 123rem | `c66d1d2` |
| Cover titles keep words whole: `overflow-wrap: normal`, `word-break: normal`, `hyphens: manual`, ellipsis, four-line clamp | `227064a` |
| Plinth opening is a soft half-ellipse shade of `--shade-rgb` at alpha 0.6 (`--arch-shade-alpha`), straight bottom edge, feathered | `870361b` |
| Docs: how sections sit in columns | `8145ba9` |

Section widths after the change: 604 px at 1280, 608 at 1920, 640 at 1440 and 2560, 374 at 390, 304 at 320.

Round 2 checks passed on 27 of 27 pages for (a) to (h) and (j), plus four new checks: (k) the last section ends at its last used row, (l) a lone section is centred within 2 px at 1440 px and wider, (m) no cover word is broken, (n) column count and section width. The negative control for (m) flagged 13 broken words with the old CSS, so the check does bite. Tests: 393 passing (358 unit, 35 integration); lint passes.

## Task Commits

1. **Task 1: Review round of screenshots and geometry checks** - no repository commit in round 1; round 2 re-ran it after the commits above
2. **Task 2: Owner decision** - round 1 answered "tune" with four changes, round 2 answered "approve" on 2026-10-06
3. **Task 3: Apply tuning** - `a8bc6eb`, `725f00c`, `c66d1d2`, `227064a`, `870361b`, `8145ba9` (merged as `13d6b24`)

**Plan metadata:** the commit that adds this summary (docs).

## Final tuning values

| Group | Value |
| ----- | ----- |
| Desktop section | interior 1200 mm, frame 20; rows (cubby height : widths) 360: 380, 220, 560; 300: 260, 340, 200, 340; 400: 460, 300, 400; 260: 300, 220, 300, 320; 330: 420, 340, 400 |
| Phone section | interior 640 mm; rows 360: 300, 320; 300: 200, 200, 200; 400: 420, 200; 260: 150, 230, 220; 340: 310, 310; 300: 200, 420 |
| Spine and stack sizes | maximum spine height 330 desktop, 340 phone; stack column 190; layer height 40 to 70; marker 40 |
| Readability floors | desktop box 34 mm, phone box 59 mm; orphan height 80 desktop, 89 phone; upright expansion width 64 desktop, 71 phone |
| Settings | cover share 25%, strategy SizeWeighted, expansion stack maximum 6, few-games threshold 12, lie flat before new section true |
| Poses | small under 200 mm tall, standard under 320, flat depth limit 40, upright expansion from 50 mm depth, at most 2 per family, flat pile maximum 4, flat chance 50% |
| Palette | 12 tones, 6 cover patterns |
| Fixed rules | shade `#140a04` capped at alpha 0.20, side allowance 32 mm |
| Round 2 additions | arch shade alpha 0.6, trimmed rows minimum 2, breakpoint 40rem, layout version 7 |

## Files Created/Modified

- `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` - label shortening, last-section trimming, `MinTrimmedRows`, layout version 7
- `Cabinet.UnitTests/Layout/TrimmedSectionTests.cs` - trimming behaviour tests
- `Cabinet.UnitTests/Layout/Golden/` - layouts re-recorded at version 7
- `Cabinet.Service/wwwroot/css/cabinet.css` - section column rows, cover title wrapping, plinth shade
- `Cabinet.Service/wwwroot/css/site.css` - section basis and maximum width
- `docs/cabinet-layout.md` - trimmed section, column layout and settings

## Decisions Made

- The look is approved with the open taste calls below carried forward, not blocking.
- Trimming applies to the last section only; trimming earlier sections needs a stability exception and stays a later option.
- Other finishes and the lit-cubbies toggle are not a tuning round; they stay in the separate selectable-finishes todo (`.planning/todos/pending/2026-10-06-selectable-cabinet-finishes-and-lit-cubbies-toggle.md`).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] Review script left off-screen sections blank**
- **Found during:** Task 1 (round 1)
- **Issue:** `content-visibility: auto` meant sections outside the viewport were not painted in full-page captures, which would have hidden defects from review.
- **Fix:** The scratch script forces sections visible and grows the viewport before capture. The app was not changed.
- **Files modified:** none in the repository (scratch script only)

**2. [Rule 1 - Bug] Hard-cut plinth arch**
- **Found during:** Task 1 check (i), judged by eye in round 1
- **Issue:** The plinth arch showed a hard cut.
- **Fix:** Soft shade with a straight bottom edge, commit `870361b`.

**Total deviations:** 2 (both Rule 1; no scope creep). Task 3 ran once, as the owner approved in round 2.

## Issues Encountered

- Known flaky test, not caused by this plan: `CabinetPageTests.Page_links_a_fingerprinted_cabinet_stylesheet_that_is_served` throws `SocketException` occasionally when the unit and integration suites run in parallel. A separate fix task was offered to the owner.

## Open taste calls carried forward

The owner approved with these open; candidates for later polish:

- The plinth arch now reads more as a shadow than an arch (raise `--arch-shade-alpha` or tighten the feather).
- Phone first sections can keep empty rows when a big cover forces a second section; only the last section is trimmed.
- The fourth-line ellipsis is slightly cropped on the smallest phone covers.
- Short upright expansions truncate both lines.
- Expansions 50 to 63 mm deep look thicker than they are.
- Phone spines are wider than real boxes.
- The 400 sample needs many phone sections.
- Cover share is about 18 to 25%.

## Owner consent

"approve" means consent to merge `milestone/v1-games-cabinet` into `main` by pull request once `build-test` and `lint` pass, and to push the next 0.x minor tag, which builds an attested draft release that only the owner publishes. The next plan executes this; this plan did not merge, tag or push.

## User Setup Required

None - no external service configuration required.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register. T-02-SC: Playwright 1.63.0 was installed in the scratch directory only; no scratch path appears in the repository. T-02-23: only the owner's explicit "approve" ended the loop. T-02-24: screenshots show invented data and stay in scratch. T-02-25: every change stayed in tuning-register locations, the layout version was raised with re-recorded goldens, and the suite and lint passed.

## Next Phase Readiness

- The approved commit is the head of the milestone branch (`13d6b24` plus this summary); the merge and release plan can proceed.
- The deployed check on a real desktop browser and phone remains the final word.

---
*Phase: 02-layout-engine-cabinet-prototype*
*Completed: 2026-10-06*

## Self-Check: PASSED

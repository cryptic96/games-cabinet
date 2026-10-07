---
phase: 04-enrichment-box-images-shape
plan: 13
subsystem: layout-engine, local-review
tags: [local-review, screenshots, desktop-density, section-design, fake-bgg]
requires:
  - phase: 04-09
    provides: "Phone design density approach (retuned rows, density helper)"
  - phase: 04-10
    provides: "True box proportions"
  - phase: 04-11
    provides: "Fake image route and Development image origin for local art"
provides:
  - "Two local review rounds of the synthetic-art cabinet with geometry, CSP, request-host and image-density checks"
  - "Desktop section design retuned (layout version 12): no empty row before the last section, none mid-section for about 65 games, no near-empty second-to-last section"
  - "Desktop density tests next to the phone ones, with HollowRows and PlacementsPerSection helpers"
  - "A thick orphan expansion in the fake collection so the two-line expansion box can be seen locally"
  - "The owner's approval of the local look"
affects: [server review round, release]
tech-stack:
  added: []
  patterns:
    - "Section designs are tuned by a seeded search over valid designs scored on the samples and seeded collections, then confirmed on collections the search never saw (as for the phone design)"
key-files:
  created:
    - Cabinet.UnitTests/Layout/DesktopDensityTests.cs
    - .planning/phases/04-enrichment-box-images-shape/ui-refs/round-1/ (6 images)
    - .planning/phases/04-enrichment-box-images-shape/ui-refs/round-2/ (6 images)
  modified:
    - Cabinet.Domain/Layout/SectionDesigns.cs
    - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
    - Cabinet.FakeBgg/SyntheticBggCollection.cs
    - Cabinet.UnitTests/Layout/LayoutDensity.cs
    - Cabinet.UnitTests/Layout/SectionDesignTests.cs
    - Cabinet.UnitTests/Layout/Golden (version file, desktop recordings, both digests, version line of the phone recordings)
    - docs/cabinet-layout.md
key-decisions:
  - "Desktop rows are now 330(250,190,260,440) 360(260,270,430,180) 370(360,320,230,230) 370(260,540,360) 280(180,220,560,180); interior height 1790 mm (was 1730)"
  - "The largest box a desktop section holds is 540 x 370 mm (was 460 x 400 mm); taller boxes are scaled down keeping proportions"
  - "The two-line orphan is made visible by changing one generated fake expansion in place, not by adding an entry, so no collection size or order changes"
  - "Only six key screenshots per round are committed (owner decision); the full sets stay in the session scratchpad"
requirements-completed: [SYNC-07, IMG-01, IMG-03, CAB-03]
duration: 2 rounds
completed: 2026-10-07
status: complete
actuals:
  tokens: 22000
  tasks: 3
  commits: 4
---

# Phase 4 Plan 13: First local review round Summary

Two local review rounds of the synthetic-art cabinet against the fake BGG: round 1 was answered "tune: desktop density", the desktop section design was retuned (layout version 12), and round 2 was answered "approve".

## Owner answers

| Round | What the owner saw | Answer |
|-------|--------------------|--------|
| 1 | 65 and 400 collections at 1440, 390 and 320 px, an all-covers capture of the 65 collection, the picture-case contact sheet, crops | `tune: desktop density` (the 400 collection ended in two nearly empty desktop sections; the 65 collection had a bare row mid-section) |
| 2 | The same pages and crops after the desktop retune | `approve` (the retune and the 370 mm cap are accepted; the owner will judge the cap again on real boxes in the server round) |

The owner's explicit answers, relayed by the coordinator, are the approval. Nothing else was treated as consent.

## Review tooling (session scratchpad only, nothing in the repository)

Playwright 1.63.0 with its Chromium, installed in the scratchpad. Each page, at 1440, 390 and 320 px (900 px tall, device scale factor 2), was loaded with `Content-Security-Policy: default-src 'self'` added to the document and checked for: (a) box overlap, (b) containment in cubby and section, (c) horizontal scroll, (d) 24 px phone boxes, (e) 12 px text, (f) console errors and policy violations, (g) every request going to the page's own origin, (h) cover images at least 1.5 times their CSS width and at most 480 px (390 and 1440), (i) generated cover label inside its plate and plate inside the cover. Fake BGG and cabinet ran on loopback only.

Result: every check passed on every page and width in both rounds, for the 65 collection, the 65 all-covers capture (`Layout__CoverStrategy=Random`, `Layout__CoverSharePercent=100`) and the 400 collection. No request left the page's origin; no console error or policy violation.

Harness notes (not product defects): the live-update WebSocket needs the Chromium flag `--disable-features=LocalNetworkAccessChecks,LocalNetworkAccessChecksWebSockets` because a fulfilled document loses its address space; `fullPage` plus `clip` returned blank crops, so crops scroll the target into view and clip in viewport coordinates; round 2 fixed the thin-upright crop (it had been the same cubby as the not-found crop) and the generated fallback crop (now the whole padded box).

## Desktop retune (round 1 answer)

Cause, inside the tuning register: the engine places each game in the first cubby that can take it and scales every box down to the anchor cubby (tallest, then widest). The old desktop design had one 400 mm row, so the biggest boxes had about three cubbies per section. Smaller games filled the earlier sections, the leftover big covers were pushed to the end and each opened a section holding a handful of them, and the two short rows (260 and 300 mm) stayed bare. No engine change was needed.

Fix: data only in `SectionDesigns.Desktop`, found by a seeded annealing search over valid designs (scored on empty rows, thin sections, tail size and section count over the samples, six seeded 65 collections, three seeded 400 collections and both fake collections) and confirmed on 12 seeded 65 and 6 seeded 400 collections the search never saw. Layout version 12; goldens re-recorded; phone recordings changed only in the version line.

| Collection (desktop) | Before | After |
|----------------------|--------|-------|
| invented 65 sample | 2 sections, 1 empty row mid-section, last two 56 / 8 | 2 sections, none, last two 53 / 11 |
| invented 400 sample | 8 sections, 2 empty rows in a non-last section, last two 22 / 8 | 7 sections, none, last two 54 / 38 |
| seeded 400 (seed 4242) | 9 sections, 1 empty row in the last, last two 38 / 4 | 9 sections, none, last two 37 / 22 |
| seeded 65 | 2 sections, 1 empty row mid-section, last two 45 / 20 | 2 sections, none, last two 51 / 14 |
| fake 65 collection | 2 sections, 1 empty row mid-section, last two 46 / 18 | 2 sections, none, last two 47 / 17 |
| fake 400 collection | 11 sections, 3 empty rows in a non-last section, last two 6 / 4 | 10 sections, none, last two 32 / 10 |

Phone density is unchanged: 65 gives 3 sections, 400 gives 14 (at most 2 empty rows in a non-last section on the fake 400 collection; the earlier phone tests still pass on their samples). Phone page heights at 390 px were identical to round 1 (4148 px and 19,773 px).

Desktop tests (`DesktopDensityTests`): no section but the last keeps an empty row; a 65-sized collection has no empty row mid-section; a 400-sized collection holds at least 30 placements in every section but the last; fewer than 12 games fit one section; the 65 sample takes 2 sections and the 400 sample fewer than 9. They run on the samples and on seeded collections including seeds not used in the search. `docs/cabinet-layout.md` has a new "Keeping the desktop cabinet dense" section.

On unseen seeded 400 collections the last section can still be small (6 to 12 games on 4 of 6) and two of them end with one empty row inside the last section; that is the remainder of a collection of arbitrary size and is not forbidden for the 400 size.

## The 370 mm cap trade-off (for the server round)

The largest box a desktop section holds is now 540 x 370 mm instead of 460 x 400 mm. A box taller than 370 mm is scaled down keeping its proportions (a 432 mm box is drawn at 86 percent of its height). On the invented samples 25 of 400 boxes (was 6) and 5 of 65 (was 2) exceed the limit. This is what spreads the biggest covers over several rows. If real boxes make the cap look too small, the cost of restoring 400 mm is some of the tail pile-up returning. The owner accepted it and will judge it again on real boxes.

## Fake collection change

The fake had no orphan expansion with a thick box (the Edge cases sample has no orphan either), so the two-line orphan could not be seen locally. One generated expansion (position 34, same in the 65 and 400 collections) keeps its place but now has a thick box (about 107 mm) and a base game that is not in the collection. No entry moved, no collection size changed, all tests stay green. To see it in an old local state directory, remove that game's cached details before the next sync.

## Screenshots

Owner's decision: only six key screenshots per round are committed (round 1 was amended to six by the orchestrator). Each of `ui-refs/round-1/` and `ui-refs/round-2/` holds: the 65 full page at 1440 px, the 65 full page at 390 px (round 1 and round 2), the 400 full page at 1440 px, the picture-case contact sheet (all-covers), the 400 crops contact sheet and the 65 plinth crop at 1440 px. Full pages are palette-reduced to keep the public repository small. The full sets (all widths, all crops, reports) stay in the session scratchpad and are not preserved. Invented titles and drawn pictures only.

## Task commits

1. Task 1 (tracer): `ec3228a` docs(04-13): add the first local review round screenshots (amended by the orchestrator to six images)
2. Task 3, round 1 answer: `168a97e` test(04-13): give the fake collection a thick expansion of a game that is not owned
3. Task 3, round 1 answer: `65718d3` feat(04-13): retune the desktop cabinet rows so sections fill evenly
4. Task 3: `88406e5` docs(04-13): add the second local review round key screenshots

Task 2 was the owner decision, answered twice (`tune`, then `approve`). Task 3 ran once, for the first answer, and was skipped after `approve`.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Review harness**
- **Found during:** Task 1
- **Issue:** WebSocket blocked by Chromium's local-network check on a fulfilled document; blank `fullPage` clip crops; fresh worktree needed a locked restore before the first build.
- **Fix:** Chromium flag, viewport-coordinate crops, `dotnet restore --locked-mode`. All in the scratchpad; no repository change.

**2. [Rule 1 - Bug] Tests pinning the old desktop design**
- **Found during:** Task 3
- **Issue:** `SectionDesignTests` pinned the old interior height, the oversize counts and boxes sized against the old anchor cubby.
- **Fix:** Updated expectations (interior height 1790, oversize counts 5 and 25, wide-base tests use boxes wider than the new family limit).
- **Files modified:** Cabinet.UnitTests/Layout/SectionDesignTests.cs
- **Commit:** 65718d3

The review-tool fixes and the fake-collection change were requested by the coordinator after the first answer.

## Known Stubs

None.

## Threat Flags

None. T-04-44 mitigated: Playwright and its Chromium live only in the scratchpad, nothing in the repository or CI. T-04-45 mitigated: only invented titles and drawn pictures are committed, the secrets lint passes. T-04-46 mitigated: check (g) passed on every page and width.

## Open items for the server round

- Real-art bytes: the synthetic pictures are tiny (400 collection: 4 distinct files, 4,538 B at 1440 px; 6 files, 9,530 B at 390 px), so the image-byte totals say nothing about real art. Measure the real collection.
- The 370 mm cap: judge the retuned desktop design and the smaller largest box on real boxes.
- Image ratio: the 400 collection at 1440 px touches the 1.5 limit exactly (minimum natural width over CSS width 1.50, within the 1 px tolerance). Watch it with real art.
- Carried over, not answered locally: the real-collection views of the earlier round questions (art fit, 3D shots, spine colours, 34 mm spines, cover line steps, plinth apron versus ellipse, phone density, landscape covers).

## Verification

- `unshare -rn sh -c 'ip link set lo up; dotnet test --solution Cabinet.slnx --no-restore'`: 1608 passed, 0 failed.
- `node --test build/tests/page-scripts.test.mjs`: 92 passed.
- `build/lint.sh`: repo-rules, workflows, shell, secrets and script-tests all pass.

## Self-Check: PASSED

- Commits exist: ec3228a, 168a97e, 65718d3, 88406e5.
- `DesktopDensityTests.cs` exists; no scratch test file and no review script is in the repository.
- `LayoutVersion = 12` in the engine and `layoutVersion: 12` in the golden version file.
- No server process left running (ports 6180, 6181 and 6190 free).

---
phase: 02-layout-engine-cabinet-prototype
verified: 2026-10-06T12:00:00Z
status: human_needed
score: 4/5 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
human_verification:
  - test: "Open the deployed prototype on a desktop browser, over the home network or VPN route, and step through every sample link: 0, 1, 5, 12, 65, 400 and Edge cases. Check that the footer shows version 0.2.0."
    expected: "Every sample reads as a real wooden cubby cabinet (classic furniture finish): covers mixed with spines and flat stacks in irregular cubbies, packed full, bare shaded wood in unused cubbies. 0 shows an intentional minimum cabinet. 1, 5 and 12 show boxes facing out. The 65 sample has a family with a '+N more' marker, thick expansions upright beside their base and thin ones stacked, and orphan expansions labelled 'Expansion for <base>'. The 400 sample grows into several sections that wrap into centred rows. Nothing overlaps or overflows."
    why_human: "Whether it 'reads as a real cabinet' and whether the empty and near-empty states 'look intentional' is subjective visual quality. It is the phase's acceptance gate (ROADMAP 'Owner prerequisites', criteria 1 and 5), and the owner has not yet looked at the deployed build."
  - test: "Repeat the sample walk on the owner's phone over the same route."
    expected: "The cabinet is narrower and taller, still looks like a cabinet, scrolls only vertically, spine text is readable, and every box can be tapped without hitting its neighbour. The 400 sample needs many sections but stays usable."
    why_human: "Readability and tap comfort on a real device cannot be judged from geometry assertions. Tests prove the derived minimum widths hold, not that they feel right in a hand."
  - test: "Judge the open taste calls recorded at approval and decide whether any needs a gap-closure round before the phase closes."
    expected: "Either accepted as is, or listed as findings for a follow-up patch release. The calls are: plinth arch reads as a shadow; phone first sections can keep empty rows; fourth-line ellipsis slightly cropped on the smallest phone covers; short upright expansions truncate both lines; expansions 50 to 63 mm deep look thicker than they are; phone spines are wider than real boxes; the 400 sample needs many phone sections; cover share is about 18 to 25 percent."
    why_human: "Taste calls the owner explicitly carried forward when approving the screenshots."
---

# Phase 2: Layout Engine and Cabinet Prototype Verification Report

**Phase Goal:** A deterministic, natural-looking cabinet layout, built on synthetic data while the BGG approval is pending, that the owner has reviewed and approved visually from an empty cabinet up to several hundred games, on desktop and phone widths.
**Verified:** 2026-10-06
**Status:** human_needed
**Re-verification:** No, initial verification
**Mode:** mvp (the goal is not in user-story form; the standard goal-backward method was applied against the ROADMAP success criteria)

## Summary

Everything that can be checked from the codebase passes. The engine, the page, the endpoint, the settings, the stylesheet and the recorded layouts all exist, are substantive and are wired end to end. The full suite (414 tests) and the lint run pass on HEAD, and the code outside `.planning/` is byte-identical to the tagged v0.2.0 release that was installed on the container. The one thing not established is the phase's own acceptance gate: the owner's visual check of the deployed cabinet on a desktop browser and a phone. The owner approved the look from screenshots (two rounds, and a re-confirmation of layout version 8), but has not yet seen the deployed build. That is correctly a human item, so the status is `human_needed`, not `passed`.

## Goal Achievement

### Observable Truths (ROADMAP success criteria, judged as amended by the owner decisions)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | The owner opens the deployed prototype with synthetic collections and approves that it reads as a real wooden cubby cabinet (covers mixed with spines, packed full in irregular cubbies, not a uniform grid). | ? UNCERTAIN (human) | Machinery is all present and live: the engine builds irregular desktop and phone section designs, `Prototype:Enabled` is true in committed settings, the page lists seven sample links and the endpoint serves them. The look was approved from screenshots in two review rounds. The deployed-cabinet check by the owner has not happened. See Human Verification items 1 to 3. |
| 2 | The cabinet grows with the collection: samples of 0, 1, 5, about 65 and 400 all render without overlap or overflow, and empty or near-empty collections look intentional (a minimum cabinet, boxes facing out when few). | ✓ VERIFIED (automated part); look is a human item | `SyntheticCollections.SampleNames` is `0, 1, 5, 12, 65, 400, edge`. `Every_sample_builds_a_valid_layout` and the phone equivalent run `LayoutAssertions.AssertValid`, which checks every placement lies inside its cubby, no two placements overlap, cubbies stay inside the section and do not overlap, and every item is placed exactly once. `An_empty_collection_gives_one_section_of_the_minimum_rows_and_no_placements`, `A_one_game_collection_gives_one_section_with_one_placement`, `Section_counts_never_decrease_as_samples_grow...`, `Eleven_games_all_face_out_and_twelve_and_thirteen_follow_the_strategy`, and seeded random-collection tests all pass. Whether the empty and sparse states "look intentional" is visual and routed to the owner. |
| 3 | The same collection always renders the same cabinet, and adding a game does not move any existing box (automated tests), as amended: appending a plain game changes at most one cubby, and the documented exceptions are tested. | ✓ VERIFIED | `Shuffling_the_input_order_gives_byte_identical_json`; golden layouts recorded for 0, 1, 5, 12, 65 and edge on both profiles plus SHA-256 digests for 400, with `The_recorded_layouts_belong_to_the_current_layout_version` and `The_golden_directory_holds_exactly_the_recorded_files`; `Appending_a_game_changes_at_most_one_cubby_across_seeded_collections` (200 seeded collections); the family and phone variants; and the exception tests `First_expansion_for_a_base_game_reserves_its_place_and_keeps_earlier_games_in_place` and `An_expansion_that_widens_its_family_may_move_it_but_keeps_earlier_games_in_place`. Engine has no persisted state (`Build` is a pure function of items, design and options). |
| 4 | Expansions appear beside their base game (thin ones as thin sideways spines in a stack, thick ones upright, as amended); an orphan stands alone labelled with the game it expands; a family with many expansions collapses extras into a "+N more" stack that never overflows its shelf. | ✓ VERIFIED | `FamilyLayoutTests` (stack order, upright rules, orphan boxes, "+N more" count, `The_sample_of_sixty_five_has_a_family_that_needs_a_marker_and_names_how_many_are_hidden`). `AssertFamilyAccountedFor` runs inside `AssertValid` on every layout: every expansion is counted once as upright, layer or marker, all in the base's cubby, uprights at most two and touching the base, layers touch from the floor up and sit inside the cubby (so the marker cannot pass the shelf above), family width stays within the design limit. `copy.js` supplies "Expansion for {base}" and `render.js` draws it for orphans and uprights. |
| 5 | On a phone-width screen the cabinet reflows into a narrower, taller cabinet that still looks like a cabinet, with spines readable and large enough to tap. | ✓ VERIFIED (automated part); feel is a human item | `SectionDesigns` holds a separate phone design (fewer cubbies across, different count); `cabinet.js` picks the profile with one `matchMedia('(max-width: 40rem)')` query. Floors are derived from the pixel target and the rendered width (`ReadabilityFloor`, `PhoneProfileTests.The_phone_design_derives_its_floors_from_the_rendered_width_including_the_furniture_sides`, `Nothing_in_the_samples_is_thinner_on_a_phone_than_the_floor`, `Nothing_in_a_seeded_collection_is_thinner_on_a_phone_than_the_floor`). Round-2 review recorded the geometry and strict-CSP script passing on 27 of 27 pages at 1440, 390 and 320 px and wider. Real-device readability and tap comfort are routed to the owner. |

**Score:** 4/5 truths verified (truth 1 is the owner's deployed visual acceptance and is pending). Behavior-dependent truths with no behavioral test: none. The stability and "never overflows" invariants are exercised by passing tests, not just by symbol presence.

### Where the ROADMAP wording is now out of date

These are recorded owner decisions in `02-CONTEXT.md`. The criteria were judged as amended; the ROADMAP text itself has not been updated and still reads as originally written.

| ROADMAP wording | Amended by | What is true now |
|-----------------|-----------|------------------|
| Criterion 3: "adding a game does not move any existing box" | D-09, D-19, D-23 | Appending a plain game, or an expansion to a family that already has a stack, changes at most one cubby (the one it lands in). Three exceptions are accepted and tested as their own cases: the few-games to normal-mix switch (D-13), the first expansion for a base game (D-19), and an expansion that widens its family, meaning the first one in a stack or one that stands upright (D-23). In those, the family may move and later cubbies may shift, but every game ordered before the base keeps its cubby. |
| Criterion 4: "Each expansion appears as a thin sideways spine" | D-21 to D-24 | Only thin expansions (under 50 mm deep) lie as thin sideways spines in a stack, thickest at the bottom. Thick ones (50 mm and deeper, at most two per family, while the room lasts) stand upright beside the base with a second line naming it. REQUIREMENTS.md EXP-01 was already reworded to match; the ROADMAP criterion was not. |
| Criterion 1: "face-out covers mixed with spines" | D-07, D-22 | The mix also includes flat stacks, and big boxes may lie flat instead of opening a mostly empty section (server setting `Layout:LieFlatBeforeNewSection`, default on). The realised cover share is about 18 to 25 percent. |
| Criterion 1: "wooden cubby cabinet" | D-20 | The furniture ships in the owner-chosen "B classic" finish (grain, moulded top, planked backs, plinth). Other finishes are a pending todo, not part of this phase. |

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Cabinet.Domain/Layout/CabinetLayoutEngine.cs` (422 lines) | Pure deterministic engine, layout version 8 | ✓ VERIFIED | Items sorted by collection id then game id; duplicates rejected; first-cubby-with-room placement, lie-flat fallback, new section last; WR-03 guard throws instead of hiding a lost game. |
| `CubbyArrangement.cs`, `StackLayout.cs`, `Orientation.cs`, `SectionDesign(s).cs`, `LayoutOptions.cs`, `SpineLabel.cs`, `SpinePalette.cs`, `StableHash.cs`, `SplitMix64.cs` | Arrangement, stacks, poses, designs, settings, labels, palette, stable randomness | ✓ VERIFIED | All present, non-trivial, exercised by dedicated test files. |
| `Cabinet.Domain/Samples/SyntheticCollections.cs` | Invented samples 0, 1, 5, 12, 65, 400, edge | ✓ VERIFIED | Names and sizes present; no real titles checked by the sample tests. |
| `Cabinet.Service/Layout/LayoutEndpoint.cs`, `LayoutCache.cs`, `LayoutSettings.cs` | Allowlisted, cached, ETagged endpoint; settings validated at startup | ✓ VERIFIED | WR-01 fix means a sample is generated at most once per process. 404 for any name or profile outside the allowlist. |
| `Cabinet.Service/Pages/Index.cshtml` and `.cs`, `Prototype/SampleCatalog.cs` | Page with sample switcher, version footer, load states | ✓ VERIFIED | Switcher nav, mount element, module script, footer version. |
| `wwwroot/js/cabinet.js`, `render.js`, `copy.js`; `css/cabinet.css`, `site.css` | Fetch, render as buttons with CSSOM geometry, finish | ✓ VERIFIED | No inline script or `style=` attribute in the page; geometry set with `style.setProperty`. |
| `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` | CSP header on every response | ✓ VERIFIED | Added by the review fix; `ContentSecurityPolicyTests` cover it. |
| `Cabinet.UnitTests/Layout/Golden/*` and `layout-version.txt` | Recorded layouts for the current version | ✓ VERIFIED | Pinned to version 8 by test. |
| `docs/cabinet-layout.md` | Plain-language layout guide with no planning references | ✓ VERIFIED | No planning identifiers found by search (see anti-patterns). |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| `Index.cshtml` | `cabinet.js` | `<script type="module">` when prototype enabled | WIRED | Also loads both stylesheets. |
| `cabinet.js` | `/cabinet/layout?sample=&profile=` | `fetch`, with `matchMedia` choosing the profile and a stale-response guard | WIRED | Loading and error-with-retry states present. |
| `LayoutEndpoint` | `LayoutCache` then `CabinetLayoutEngine.Build` | Cache miss only, ETag and 304 handling | WIRED | Allowlist checked before any generation. |
| `LayoutCache` | `SampleCatalog.ItemsOf` | Items generated once per sample per process | WIRED | WR-01. |
| `LayoutSettings` | `appsettings.json` `Layout` section | Startup validation, env override | WIRED | Defaults 25 percent, SizeWeighted, stack max 6, threshold 12, lie-flat on. |
| `render.js` | `copy.js` | Every visitor-facing string | WIRED | Orphan, upright, layer and marker names come from here. |
| `Program.cs` | CSP middleware | Registered first in the pipeline | WIRED | Policy is `default-src 'self'` with no unsafe directives. |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| Rendered cabinet | placements and cubbies | `SyntheticCollections` through `CabinetLayoutEngine.Build` through the cached JSON endpoint | Yes (invented by design; the engine input is independent of the data source) | ✓ FLOWING |

### Behavioral Spot-Checks and Probes

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Full suite | `dotnet test --solution Cabinet.slnx` | 414 succeeded, 0 failed, 0 skipped | ✓ PASS |
| Lint (repo rules, workflows, shell, secrets, script tests) | `bash build/lint.sh` | all five groups PASS | ✓ PASS |
| Deployed code equals HEAD code | `git diff v0.2.0 HEAD -- . ':!.planning'` | empty (only planning docs differ) | ✓ PASS |

Probe execution: no probe scripts are declared by this phase. Step skipped. The app was not started; ports were not touched.

### Requirements Coverage

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| CAB-01 | 02-01, 03, 04, 06, 07, 08, 09 | Cabinet drawn to fit the collection, empty to several hundred | ✓ SATISFIED (visual gate pending) | Validity tests on all samples, growth test, endpoint and page wired. |
| CAB-02 | 02-02, 03, 07, 08, 09 | Mix of face-out covers and spines | ✓ SATISFIED (visual gate pending) | `ShelfMixTests`: share between 15 and 35 percent by default, strategies, covers never revert on a higher share. |
| CAB-04 | 02-01, 02, 03, 06, 07, 08, 09 | Packed natural and full, irregular cubbies | ✓ SATISFIED (visual gate pending) | Irregular section designs, first-cubby-with-room placement, flat piles with no overhang. |
| CAB-05 | 02-01, 05, 06, 07, 08, 09 | Stable layout, appending does not reshuffle | ✓ SATISFIED | Determinism, golden and append-stability tests as amended; exceptions tested. |
| CAB-06 | 02-02, 04, 06, 07, 08 | Small or empty collections look intentional | ✓ SATISFIED (visual gate pending) | Minimum one section, few-games switch, trimmed last section (at least two rows). |
| CAB-07 | 02-04, 06, 07, 08 | Phone reflow, readable and tappable | ✓ SATISFIED (device gate pending) | Phone design, derived floors, single media query. |
| EXP-01 | 02-05, 07, 08, 09 | Expansion beside its base: thick upright, thin as stack | ✓ SATISFIED | Family and upright tests; REQUIREMENTS wording already amended. |
| EXP-02 | 02-05, 07, 08 | Orphan expansion stands alone, labelled with its base | ✓ SATISFIED | Orphan tests; "Expansion for ..." in copy and render. |
| EXP-03 | 02-05, 07, 08, 09 | "+N more" stack, never overflows | ✓ SATISFIED | Marker test; layers and marker asserted inside the cubby on every layout. |

All nine requirement IDs in the phase brief appear in at least one PLAN frontmatter `requirements` field. REQUIREMENTS.md maps no further IDs to Phase 2 (CAB-03 belongs to Phase 4), so there are no orphaned requirements. REQUIREMENTS.md still shows these nine as unchecked and "Pending"; flipping them is bookkeeping to do once the owner accepts the deployed check.

### Anti-Patterns Found

| Check | Result | Severity |
|-------|--------|----------|
| `TBD`, `FIXME`, `XXX`, `TODO`, `HACK` in Domain, Service layout, prototype, wwwroot, pages, docs | none | none |
| `//` comments in C# or JS source | none | none |
| Planning references (requirement keys, decision IDs, phase or plan numbers, planning document names) outside `.planning/` | none in application code, tests, docs, deploy or workflow files; the only hits are the lint rule that bans them | none |
| Stub patterns (empty handlers, static returns) in the render path | none; data flows from the engine | none |

### Open review items (non-blocking)

The four warnings of the code review (WR-01 to WR-04) were fixed and re-verified by tests, and the CSP one was also confirmed in a browser with a negative control. The seven info items (IN-01 to IN-07) are open by the owner's choice and none blocks the goal:

- IN-04: the "+N more" marker's accessible name does not contain its visible text, and every placement is a focusable button that does nothing yet. This belongs to the accessibility phase, where keyboard and screen-reader access are in scope.
- IN-05: the committed settings turn the invented collections on and the pages carry no `noindex`. The router is LAN-only until the public-hardening phase, so exposure is nil today. Add `noindex` or switch the default off before the site is made public.
- IN-01: the layout ETag is derived from version, settings and names, not from the body. This relies on the layout version being bumped on every change, which the golden tests enforce for the small samples. Worth fixing before real data arrives.

### Human Verification Required

#### 1. Deployed cabinet on a desktop browser

**Test:** Over the home network or VPN route, open the deployed prototype and step through samples 0, 1, 5, 12, 65, 400 and Edge cases. Confirm the footer shows version 0.2.0.
**Expected:** Each sample reads as a real wooden cubby cabinet: covers mixed with spines and flat stacks in irregular cubbies, bare wood in unused cubbies, an intentional empty cabinet at 0, covers facing out at 1, 5 and 12, expansion families beside their base games with a "+N more" on the big family, orphans labelled "Expansion for ...", and no overlap or overflow in the 400 sample.
**Why human:** Subjective visual quality; the phase's acceptance gate, not yet performed on the deployed build.

#### 2. Deployed cabinet on a phone

**Test:** Repeat the sample walk on the owner's phone.
**Expected:** A narrower, taller cabinet that still looks like furniture, vertical scrolling only, readable spine text, and every box tappable without hitting its neighbour.
**Why human:** Real-device readability and tap comfort.

#### 3. Carried-forward taste calls

**Test:** Decide whether any of the eight open taste calls recorded at approval needs a follow-up patch release (plinth arch reads as a shadow; empty rows in early phone sections; slightly cropped fourth-line ellipsis on the smallest phone covers; truncated short upright expansions; expansions 50 to 63 mm deep drawn thicker than they are; phone spines wider than real boxes; many phone sections for 400; cover share 18 to 25 percent).
**Expected:** Accepted as is, or recorded as findings for the gap-closure flow.
**Why human:** The owner explicitly deferred these when approving.

### Gaps Summary

No automated gaps. No truth failed, no artifact is missing, stubbed or unwired, and no key link is broken. The phase cannot be marked passed only because its own acceptance gate, the owner's visual check of the deployed cabinet on a desktop browser and a phone, is still open. If the owner rejects the deployed look, the findings go through the gap-closure flow and a later 0.x patch release, and the phase stays open. The ROADMAP wording for criteria 3 and 4 (and, mildly, criterion 1) should be refreshed to the amended decisions so the contract matches what was built.

---

_Verified: 2026-10-06_
_Verifier: Claude (gsd-verifier)_

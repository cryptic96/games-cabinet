---
phase: 05-game-detail-accessibility-language
verified: 2026-10-12T12:00:00Z
status: human_needed
score: 5/5 must-haves verified (automated); 5 owner device checks still open
behavior_unverified: 0
overrides_applied: 0
gaps: []
deferred:
  - truth: "Storage location is visible on the card for every game on the deployed site"
    addressed_in: "Phase 6 (assignment via owner tools) and Phase 7 (LOC-01 shown to every visitor)"
    evidence: "Phase 6 SC4: owner defines and assigns locations; Phase 7 SC4: 'Every game's storage location is visible to every visitor'. LOC-01/03/04 are Pending in REQUIREMENTS.md; the Phase 3 access check recorded that BGG's private field is not readable with the token. Phase 5 context D-12/D-13 deliberately hides the row when no location exists."
human_verification:
  - test: "Chrome for Android Back: open a card, press Back (button and gesture), once straight after opening and once after tapping inside the card; then Back again"
    expected: "First Back closes the card (box returns to its slot); second Back leaves the site"
    why_human: "Real-device history behaviour; owner has no Android device"
  - test: "iOS Safari: close a card with the edge swipe, then swipe again"
    expected: "Second swipe leaves the site (first swipe was confirmed to close the card)"
    why_human: "Real-device gesture; not confirmed by owner"
  - test: "Desktop browser Back with a card open"
    expected: "One Back closes the card, a second leaves the site"
    why_human: "History-step behaviour across real browsers; not reported by owner"
  - test: "Turn on reduced motion in the OS, then open a card on desktop and phone"
    expected: "Card fades in (no pull-out flight, no view transition); the source box stays in place with a clear outline"
    why_human: "Owner was unsure; automated tests emulate the media query but not a real OS setting"
  - test: "Screen reader (VoiceOver/TalkBack/NVDA): press Tab once, activate the skip link, read the games list in English and in Dutch"
    expected: "Skip link lands on the games list; entries read title plus game-night facts; Dutch page reads Dutch labels with titles unchanged"
    why_human: "Real assistive-technology output cannot be asserted from DOM tests"
---

# Phase 5: Game Detail, Accessibility & Language Verification Report

**Phase Goal:** A visitor can tap any box, watch it slide out of the shelf, and read everything needed to pick it for game night in English or Dutch, by touch, mouse, keyboard or screen reader.
**Verified:** 2026-10-12
**Status:** human_needed
**Re-verification:** No, initial verification

## Goal Achievement

All five roadmap success criteria are backed by real, wired code and by passing tests that I re-ran myself. Nothing is FAILED. The status is `human_needed` only because five owner-side real-device checks are explicitly still open and must not be marked passed.

### Observable Truths

| # | Truth (ROADMAP success criterion) | Status | Evidence |
|---|-----------------------------------|--------|----------|
| 1 | Tap/click a box pulls it out with an animation, then opens the card | VERIFIED | `detail.js` `openCard` builds the card then `openWithPull` (View Transition lift/turn into the card's cover, source slot stays empty) when supported, and `openWithFade` otherwise; wired from `cabinet.js` (`initCardDialog`, `openCard` passed to games list and click handlers). Browser tests: `An_upright_spine_lifts_turns_to_its_cover...`, `A_lying_box_tips...`, `Clicking_a_cover/spine/more_marker...`, `Without_the_transition_api_the_card_fades_open...`. Owner confirmed on real devices: spine/flat/face-out pull-out, Safari and Firefox fallback. |
| 2 | Card shows cover, title, year; players, time, weight; location and owned expansions; rating, designers, min age, mechanics; BGG link | VERIFIED | `card-view.js` `buildCard`: cover (`buildCover`), title, year; `factsFor` (players, time, weight, age), `buildWhere` (location), `buildOwnedExpansions`, `buildMeta` (rating, designers, mechanics), `buildLink` (fixed BGG host plus digits-only id, `target=_blank rel=noopener`). Data is real: `CardRecords.ToRecord` maps year, location, min age, weight, rating, designers, mechanics, expansions from the snapshot and `GameDetails`; `/cabinet/cards` serves it (`CardsEndpoint`, ETag/304). Browser test `A_game_with_every_detail_shows_the_strip_then_the_location_then_rating_designers_and_mechanics_then_the_link` passes. Container returned 65 cards; owner confirmed real facts on the card. |
| 3 | Reduced motion gets the card without the pull-out animation | VERIFIED (automated); owner OS-level check OPEN | `card-flow.js` `choosePath` returns `'reduced'` first when `reducedMotion`; `detail.js` never starts a view transition when `prefers-reduced-motion: reduce` (lines 309, 400, 629); `card.css:812` silences all view-transition animations; `cabinet.css:182` removes hover/transition. Browser tests `Opening_and_closing_boxes_and_swapping_never_start_a_view_transition_and_the_box_is_outlined_in_place` and `The_card_says_the_same_with_reduced_motion_as_without` pass. Owner could not confirm on a device. |
| 4 | Keyboard user can move through games and open any card; screen-reader user gets an accessible list of all games | VERIFIED (automated); screen-reader device check OPEN | `keys.js`: whole cabinet is one tab stop, arrows/Home/End move by on-screen position, Enter/Space via native button. `games-list.js` + `Index.cshtml`: skip link to an h2 plus `ul[role=list]` of buttons for every drawn game, including expansions hidden behind "+N more", never removed from the accessibility tree. Browser tests (`KeyboardTests` 16, `GamesListTests` 13) pass, including 400-game and empty cases. Owner confirmed desktop keyboard; screen reader not yet tested. |
| 5 | Labels in English or Dutch; default follows browser; switchable; remembered; titles exactly as BGG | VERIFIED | `SiteLanguage.Resolve`: cookie first, then Accept-Language, then English; `LanguageEndpoint` sets one `lang` cookie (365 days, HttpOnly, Lax) for `en`/`nl` only and 303s to a fixed target (no open redirect); page toggle in `Index.cshtml`; `SiteText` + `copy.js` (parity test `copy-parity.test.mjs`) cover server and client strings; titles are never translated (`Game_titles_are_identical_on_the_English_and_the_Dutch_page` passes). Container served `Content-Language: nl`, `lang="nl"`. Owner confirmed toggle both ways with titles unchanged. |

**Score:** 5/5 truths verified by automated evidence, 0 behavior-unverified; the real-device portions of truths 3 and 4 and the Back-button behaviour remain with the owner.

### Deferred Items

| # | Item | Addressed In | Evidence |
|---|------|-------------|----------|
| 1 | A storage location shown on cards on the deployed site (currently 0 of 65 cards have one) | Phase 6 / Phase 7 | The Phase 3 access check proved BGG's private location is not readable with the token (PROJECT.md), so locations come from owner tools (LOC-03/04, Phase 6) and are shown to all visitors in Phase 7 (LOC-01). REQUIREMENTS.md leaves LOC-01/03/04 Pending. Phase 5 context D-12/D-13: the row renders only when a location exists, designed and tested on invented locations. |

Classification note: DET-02's "storage location" is satisfied as a capability. `buildWhere` renders the row when `record.location` is filled, the pipeline carries `Location` from the snapshot through `CardRecords` to the page, and browser test `A_game_without_a_location_has_no_stored_in_group_and_one_with_a_location_has_it` proves both states on synthetic data. Production shows none by deliberate, documented decision (private info stays off; plan 16 prohibits turning it on), and the data source arrives in later phases. This is not a Phase 5 gap.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Cabinet.Service/wwwroot/js/detail.js` (755 lines) | Pull-out, fade, reduced-motion path, sheet drag, close | VERIFIED | Substantive; imported and initialised by `cabinet.js` |
| `Cabinet.Service/wwwroot/js/card-view.js` (704) | Card DOM for all DET-02 fields | VERIFIED | Text-content only; no `innerHTML` anywhere in `wwwroot/js` (grep) |
| `Cabinet.Service/wwwroot/js/card-flow.js` (340) | Path choice, history step, record fallback | VERIFIED | `choosePath` unit-tested |
| `Cabinet.Service/wwwroot/js/keys.js` (303) | Roving tab stop and arrow navigation | VERIFIED | Wired via `initRoving` in `cabinet.js` |
| `Cabinet.Service/wwwroot/js/games-list.js` (416) | Accessible list and skip-link target | VERIFIED | Wired via `initGamesList`; markup in `Index.cshtml` |
| `Cabinet.Service/wwwroot/js/copy.js`, `format.js` | English/Dutch strings, value formatting | VERIFIED | Parity test passes |
| `Cabinet.Service/Cards/CardsEndpoint.cs`, `CardCache.cs`; `Cabinet.Domain/Cards/CardRecords.cs` | Card data for every game | VERIFIED | Route mapped; allowlisted profile/sample; ETag/304; no CORS |
| `Cabinet.Service/Language/LanguageEndpoint.cs`, `SiteLanguage.cs`, `SiteText.cs` | Language choice, cookie, server text | VERIFIED | Mapped in `Program.cs` (`MapCabinetLanguage`) |
| `Cabinet.Service/wwwroot/css/card.css`, `cabinet.css`, `img/icons.svg` | Card look, reduced-motion rules, icons | VERIFIED | Reduced-motion blocks present |
| `Cabinet.BrowserTests/` (130 tests) | Browser coverage of all criteria | VERIFIED | Ran locally: 130 passed, 0 failed |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| Box tap / games-list entry | `openCard` | `cabinet.js` passes `openCard` to list and click handlers | WIRED |
| `cabinet.js` `fetchCards` | `/cabinet/cards` -> card records -> `buildCard` | ETag-matched fetch after layout draw | WIRED (degraded path, see WR-01) |
| Language toggle | `/language/{code}` -> cookie -> `SiteLanguage.Resolve` -> page and `copy.js` | Index.cshtml links, middleware | WIRED |
| Skip link | games-list heading/first entry | `cabinet.js` lines 354-357 `focusStart` | WIRED |
| `choosePath` reduced | `openWithFade(reduced=true)` | `detail.js` `decidePath` reads media query at every tap | WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| Card facts/meta | `record.*` | `CardRecords.ToRecord` from snapshot item + `GameDetails` (sync) | Yes; container served 65 cards, owner saw real facts | FLOWING |
| Card location row | `record.location` | `stored?.Location` | Null in production by design; non-null on synthetic data | FLOWING (capability), empty by decision |
| Games list | titles then cards | drawn placements, then `showCards` | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Page-logic tests | `node --test` | 238 passed, 0 failed | PASS |
| Browser build | `dotnet build Cabinet.BrowserTests` | 0 warnings, 0 errors | PASS |
| Browser suite (pull-out, card content, reduced motion, keyboard, list, language, Back, sheet drag) | `dotnet test --project Cabinet.BrowserTests --no-build` | 130 passed, 0 failed, 0 skipped | PASS |

I did not re-run the 2193 non-browser .NET tests; I relied on the orchestrator's gate and the green CI on PR #19 for that suite.

### Probe Execution

Step 7c: SKIPPED. No probe scripts are declared by this phase's plans.

### Requirements Coverage

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| DET-01 | 05-01, 05-06, 05-11, 05-12, 05-13, 05-15, 05-16 | Tap pulls the box out, then opens the card | SATISFIED | Truth 1; owner device YES |
| DET-02 | 05-02, 05-03, 05-06, 05-07, 05-09, 05-14, 05-15, 05-16 | Card content list | SATISFIED | Truth 2; location row is capability-complete, empty in production by decision (deferred note) |
| DET-03 | 05-11, 05-15, 05-16 | Reduced motion without pull-out | SATISFIED (automated); device check OPEN | Truth 3 |
| A11Y-01 | 05-01, 05-08, 05-15, 05-16 | Keyboard through games, open any card | SATISFIED | Truth 4; owner desktop keyboard YES |
| A11Y-02 | 05-01, 05-10, 05-15, 05-16 | Accessible list of all games | SATISFIED (automated); screen-reader check OPEN | Truth 4 |
| I18N-01 | 05-01, 05-04, 05-05, 05-07, 05-15, 05-16 | English/Dutch, default from browser, switch, remembered, titles unchanged | SATISFIED | Truth 5 |

No orphaned requirements: REQUIREMENTS.md maps exactly DET-01, DET-02, DET-03, I18N-01, A11Y-01 and A11Y-02 to Phase 5, and every one appears in at least one plan's `requirements` frontmatter. All six are marked Complete in the traceability table.

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `Cabinet.Service/wwwroot/js/cabinet.js` | 93-122 | WR-01: `fetchCards` swallows failures with no retry; cards stay incomplete for the page session and the note says "after the next sync" | Warning | Degraded path only (flaky network). Card still opens with cover, title and BGG link, covered by `A_card_still_opens_with_title_link_and_the_quiet_note_when_the_card_data_never_arrives`. Does not block the goal; recommend a follow-up (retry, or a distinct fetch-failed note). |
| `.github/workflows/ci.yml`, `release.yml` | 46, 45 | WR-02: `--ignore-exit-code 8` on the whole solution run | Warning | A project that discovers zero tests would pass silently. Process hygiene, not goal. |
| `Cabinet.BrowserTests/SheetDragTests.cs` | 96-109 | WR-03: flick test depends on gesture timing | Warning | Possible CI flake. Passed in my run. |
| `Index.cshtml` | 172-193 | English-only strings in the prototype/sample switcher | Info | Development setting only (IN-02). |

Debt-marker scan of the phase's shipped diff (`965df1b..ef6ee75`, excluding tests, lock files, images and `.planning/`): no `TBD`/`FIXME`/`XXX`, no `//` comments added, no planning references (phase, plan, requirement IDs, planning document names) outside `.planning/`. Project hard rules hold on the shipped code.

### Prohibitions

All 26 plan prohibitions are marked resolved. 19 are test-tier and enforced by automated tests that passed. The remaining 12 are soft gates and need human review (non-authoritative; flagged, not silently passed):
- Judgment, synthetic-data-only items (05-01, 05-02, 05-15): my scan of the shipped diff found no real titles, locations, hostnames or personal data; recommend the owner confirms the public CI screenshot artifact contains only synthetic collections.
- Judgment, UI look items (05-06, 05-07, 05-11, 05-14): visual judgements; the owner's device check confirmed pull-out and card on spine, flat and face-out; the spine-evenness and plinth-lip item (05-14) has no recorded owner confirmation.
- Backstop, release conduct (05-16): `unverified-prohibition - human review recommended` for no merge/tag/publish without approval, no personal data in SUMMARY, and private info staying off. The SUMMARY records owner approvals verbatim and production shows 0 cards with a location, consistent with private info off.

### Human Verification Required

1. **Chrome for Android Back.** Open a card, press Back (button and gesture), once straight after opening and once after tapping inside the card. Expected: first Back closes the card, a second leaves the site. Why human: real-device history behaviour; owner has no Android device.
2. **iOS Safari second swipe.** After the edge swipe closes the card, swipe again. Expected: leaves the site. Why human: real-device gesture, unconfirmed.
3. **Desktop browser Back.** With a card open press Back. Expected: closes the card; a second Back leaves the site. Why human: not reported by owner.
4. **Reduced motion on a real OS setting.** Expected: opening a card gives a fade with the box outlined, no pull-out flight. Why human: owner was unsure; tests emulate only the media query.
5. **Screen reader.** Tab once, activate the skip link, read the games list in English and Dutch. Expected: lands on the list, entries read title plus facts, Dutch labels with unchanged titles. Why human: real assistive-technology output.
6. **Spine evenness and plinth lip (judgment prohibition from the polish plan).** Look at a full shelf of thin spines at 1440 px and 390 px. Expected: even title sizes, plinth lip visible on the phone. Why human: visual judgement without a recorded owner answer.

### Gaps Summary

No gaps. The goal is achieved in the codebase for the normal path: pull-out, complete card, reduced-motion path, keyboard model, games list and bilingual labels all exist, are wired, are fed by real synced data, and pass 130 browser tests and 238 node tests that I re-ran. The empty storage-location row on the deployed site is a documented, deliberate outcome of the earlier access check and is deferred to the owner-tools and filters phases. WR-01 is a degraded-path defect worth a follow-up, not a goal blocker. The phase cannot be `passed` until the owner closes the open device checks above.

---

_Verified: 2026-10-12_
_Verifier: Claude (gsd-verifier)_

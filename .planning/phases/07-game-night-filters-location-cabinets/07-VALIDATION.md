---
phase: 07
slug: game-night-filters-location-cabinets
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-15
---

# Phase 07 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.
> Source: `07-RESEARCH.md` § Validation Architecture, plus the verification contract in `07-UI-SPEC.md`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 (Unit, Integration, Browser) on the Microsoft Testing Platform runner; `node --test` for page scripts and CSS text |
| **Config file** | `global.json` (runner), `Cabinet.slnx`, `Cabinet.*/Cabinet.*.csproj`; no Node config |
| **Quick run command** | `node --test build/tests/<touched>.test.mjs`, or `dotnet test --project Cabinet.UnitTests --filter-class "*<Touched>*"` |
| **Full suite command** | `dotnet test --project Cabinet.UnitTests --no-restore && dotnet test --project Cabinet.IntegrationTests --no-restore && node --test build/tests/*.test.mjs`, then `FONTCONFIG_FILE=<dejavu.conf> dotnet test --project Cabinet.BrowserTests` (one browser suite at a time locally) and `build/lint.sh` |
| **Estimated runtime** | ~30 s quick, several minutes full (browser tests dominate) |

---

## Sampling Rate

- **After every task commit:** the quick command for the layer touched (one node file, or one C# filter-class).
- **After every plan wave:** unit + integration + all `node --test`, then the browser classes the wave touched, **one suite at a time** (concurrent browser suites exhaust the inotify limit), under the DejaVu fontconfig before pushing geometry changes.
- **Before `/gsd-verify-work`:** full suite green, `build/lint.sh` clean (including the secrets history scan after merges), CI browser jobs green (including the new `browser-filters` job).
- **Max feedback latency:** 60 seconds for the quick command.

---

## Requirement → Test Map

| Req / Decision | Behaviour | Test Type | Automated Command / Class | File Exists |
|----------------|-----------|-----------|---------------------------|-------------|
| FILT-01, D-13 | Accent and case folding; family-wide match; same-frame title match | node | `node --test build/tests/filter-rules.test.mjs` | ❌ W0 |
| FILT-01, D-14 | Parser reads alternate names (capped, deduped); `SameAs` and the version hash include them; snapshot loads with and without them | unit (C#) | `*BggThingParserTests*`, snapshot compat tests | extend |
| D-14 | Search-names endpoint: owned games only, ETag/304, sample variant, no CORS, no raw fields | integration | `*SearchNames*` | ❌ W0 |
| FILT-02, D-10, D-11, D-15 | Range incl. 8+, widened by owned expansions, 0/missing ignored, unknown dims, tap-again-to-Any | node + browser | `filter-rules.test.mjs`; `FilterBarTests` | ❌ W0 |
| FILT-03, D-12 | Long end counts (45–90 does not light at 60); unknown dims | node | `filter-rules.test.mjs` | ❌ W0 |
| FILT-04, D-16, D-24 | Per-box location; owner order; Unknown last; stale stored id falls back to Any | node + integration + browser | `filter-rules.test.mjs`; `CardsEndpointTests` (locationId, locations) | extend |
| FILT-05, D-06, D-07, D-17 | AND; count equals list entries in both views; zero match; sessionStorage survives reload, EN·NL and redraw; storage throwing | node + browser | `filter-state.test.mjs`; `FilterPersistenceTests` | ❌ W0 |
| EXP-04 | Expansion lit/dimmed with its base; location the only exception | node + browser | `filter-rules.test.mjs`; `FilterSearchTests`, `FilterBarTests`, `FilterLocationTests` | ❌ W0 |
| LOC-01 | Location visible to a public visitor (card row, list phrase, heading) | integration + browser | `CardsEndpointTests`; `FilterListTests` | extend |
| LOC-05, D-23, D-25, D-26, D-27 | Per-location layouts stable, Unknown last, empty left out, few-games rule per group, cached per version and profile, 304; groups on own rows, `h2` headings, no overlap | unit + integration + browser | `*LocationLayouts*`; `LocationLayoutEndpointTests`; `FilterLocationViewTests` | ❌ W0 |
| D-28, D-29, D-30 | Remembered view read before fetch; cross-fade; Show on shelf scrolls the box into view and outlines it; failure copy | browser | `FilterViewSwitchTests`; `FilterShowOnShelfTests` | ❌ W0 |
| D-18, D-19, D-20 | First frame already dimmed (reload, language, redraw, view switch); veil above the lights in every light state and depth; arrows skip dimmed; tab stop moves; "/" and Escape; one announcement | browser | `FilterDimTests`, `FilterKeyboardTests` | ❌ W0 |
| D-19 | Dimmed contrast range documented; peek; rim vs backs | unit (C#) | `*DimmedBoxContrastTests*` | ❌ W0 |
| D-02 | Bar text, lines, focus, quiet-Any bar, muted heading on 5 walls day and evening; muted never on wash | unit (C#) | extend `LookChromeContrastTests` | extend |
| D-02, D-18 | `filters.css` structure (veil in modifiers layer, no unlayered cabinet rule, no colour literal, no filter/blur); stacking map (veil 1, corner tag 2, bar 5) | node | `filters-css.test.mjs`; `cabinet-layers.test.mjs` | ❌ W0 / extend |
| D-01, D-03, D-04, D-05, D-06 | Sheet live with transparent backdrop; tap on cabinet closes without opening a card; slim bar 8px threshold; bar height constant when filtering starts (full bar 1024/1440, slim bar 390, EN and NL); two rows at 1024/1440/1920; Back closes one step each; "Show n games" scrolls to the next lit box | browser | `FilterSheetTests`, `FilterSlimBarTests`, `FilterBarGeometryTests` | ❌ W0 |
| D-31 | v2 owner file loads in place; forced v2 copy; v3 refused by the older reader; restore of v1/v2; per-copy rows; expiry per copy; expansion follows the first copy; game-wide fallback and expand-on-edit | unit + integration + browser | `OwnerDataUpgradeTests` etc.; `OwnerPerCopy*` | extend + ❌ W0 |
| D-32 | Last section drops empty rows (1 and 2 boxes at phone width), minimum two; balanced desktop unchanged; goldens re-recorded | unit (C#) | `--filter-trait "Category=Layout"`; `TrimmedSectionTests` | extend |
| D-33 | Owner address: bar below the owner strip; corner tag above the veil | browser | `OwnerCabinetTests` extension | extend |
| D-34 | Android Back; screen-reader listen | manual | device round on the deployed release | manual |

*Per-task rows are added by the planner and execution (`{N}-{plan}-{task}`), each pointing at one row above. Each task's automated verify runs a fast smoke form (one node test file, one C# test class, or one browser test method through `--filter-method`); the full browser classes run in each plan's verification, one suite at a time.*

### Per-plan sampling (planner)

| Plan | Rows above it samples | Quick command(s) the plan's tasks run |
|------|-----------------------|---------------------------------------|
| 07-01 | FILT-01 D-13; FILT-05; EXP-04; D-18 structure | `node --test build/tests/filter-rules.test.mjs`; `--filter-method "*FilterSearchTests.<method>"` per task; full `FilterSearchTests` at plan end |
| 07-02 | D-32 | `--filter-class "*SparseLastSectionTests"`; `--filter-trait "Category=Layout"` |
| 07-03 | D-31 (format, overlay, per-copy edit, round trip) | `*OwnerOverlayTests`, `*LocationEditsTests`; `*OwnerPerCopyRoundTripTests`, `*OwnerDataUpgradeTests`; `--filter-method "*CardsEndpointTests.Each_copy_of_a_game_shows_its_own_place_on_the_public_card"` |
| 07-04 | FILT-01 D-14 (alternate names kept); D-31 (edition name read) | `*BggThingParserTests`, `*NameSyncTests`; `*BggCollectionParserTests`; `*StoredSnapshotCompatibilityTests` |
| 07-05 | D-14, D-31 real shapes as counts (N1) | `bash build/tests/bgg-access-check-test.sh` |
| 07-06 | Copy contract; CI job | `node --test build/tests/filter-copy.test.mjs build/tests/copy-parity.test.mjs`; `*FilterCopyTests` |
| 07-07 | D-20 keyboard | `keys.test.mjs`; `--filter-method "*FilterKeyboardTests.<method>"` |
| 07-08 | D-31 (Locations rows, pickers, posts) | `owner-shared.test.mjs`, `owner-pickers.test.mjs`; `--filter-method "*OwnerPerCopyLocationTests.<method>"`; `*OwnerLocationTests` |
| 07-09 | D-31 (expiry, hand-over, restore) | `*OwnerExpiryTests`, `*OwnerDataServiceTests`; `--filter-method "*OwnerRestoreTests.<method>"` |
| 07-10 | FILT-04, LOC-01 (data on the card) | `*LocationBookTests`, `*CollectionStateVersionTests`, `*SampleCardDetailsTests`; `--filter-method "*CardsEndpointTests.<method>"` |
| 07-11 | FILT-02, FILT-03, D-10/D-11/D-12/D-15 | `filter-rules.test.mjs`; `--filter-method "*FilterBarTests.<method>"` |
| 07-12 | D-31 (edition labels, bulk, counts) | `*LocationEditsTests`; `*OwnerBulkMovePostTests`; `--filter-method "*OwnerPerCopyLocationTests.<method>"` |
| 07-13 | D-31 (picture choice per copy) | `*OwnerOverlayTests`, `*SnapshotMapperOverlayTests`; `--filter-method "*OwnerPictureChoiceTests.<method>"` |
| 07-14 | LOC-05 D-24/D-25/D-26 (server) | `*LocationLayoutsTests`; `--filter-method "*LocationLayoutEndpointTests.<method>"` |
| 07-15 | D-01/D-02 geometry | `cabinet-layers.test.mjs`; `--filter-method "*FilterBarGeometryTests.<method>"` (DejaVu) |
| 07-16 | D-31 (Pictures rows) | `*PictureRowsTests`; `--filter-method "*OwnerPictureChoiceTests.<method>"`; `--filter-method "*OwnerPerCopyPictureTests.<method>"` |
| 07-17 | FILT-04 D-16/D-24; LOC-01 | `filter-rules.test.mjs`; `--filter-method "*FilterLocationTests.<method>"` |
| 07-18 | D-31 (picture links) | `*LinkPreviewTests`, `*RemoveLinkTests`, `*StagedItemsTests`; `--filter-method "*OwnerLinkTests.<method>"` |
| 07-19 | FILT-01 D-14 (search) | `filter-rules.test.mjs`; `*SearchNamesTests`; `*SearchNamesEndpointTests`; `--filter-method "*FilterAlternateTitleTests.<method>"` |
| 07-20 | D-31 (link checks) | `*OwnerLinkWorkerTests`; `--filter-method "*OwnerLinkFlowTests.<method>"` |
| 07-21 | FILT-05 D-17; D-18 first frame | `filter-state.test.mjs`; `--filter-method "*FilterPersistenceTests.<method>"` |
| 07-22 | LOC-05 D-21/D-23/D-27; D-07 by location | `filter-rules.test.mjs`; `--filter-method "*FilterLocationViewTests.<method>"` |
| 07-23 | D-28/D-29 | `locations-view.test.mjs`, `card-css.test.mjs`; `--filter-method "*FilterViewSwitchTests.<method>"` |
| 07-24 | LOC-01 list; D-22 | `games-list.test.mjs`; `--filter-method "*FilterListTests.<method>"` |
| 07-25 | D-03/D-04; Back | `card-flow.test.mjs`, `filter-sheet.test.mjs`; `--filter-method "*FilterSheetTests.<method>"` |
| 07-26 | D-30 | `card-css.test.mjs`, `cabinet-layers.test.mjs`; `--filter-method "*FilterShowOnShelfTests.<method>"` |
| 07-27 | D-05; U1 | `filter-sheet.test.mjs`; `--filter-method "*FilterSlimBarTests.<method>"` (DejaVu) |
| 07-28 | D-02, D-19 | `*LookChromeContrastTests`, `*DimmedBoxContrastTests` |
| 07-29 | D-18 lights, D-33, stylesheet rules, access | `filters-css.test.mjs`; `--filter-method "*FilterDimTests.<method>"`, `*OwnerCabinetTests.<method>`, `*FilterAccessTests.<method>` |
| 07-30, 07-31 | Review round | full suite, one browser suite at a time, DejaVu, lint |
| 07-32, 07-33 | Release | local gates; CI jobs incl. `browser-filters`; draft verification; container evidence |
| 07-35 | Rail and panel tracer (amendment AM-2 to AM-6) | `--filter-method "*FilterDockGeometryTests.<method>"` (DejaVu); `filters-css.test.mjs` |
| 07-36 | Fly-over modality, close routes, focus, width crossings, containing block | `filter-dock.test.mjs`, `filters-css.test.mjs`, `cabinet-layers.test.mjs`; `--filter-method "*FilterDockFocusTests.<method>"`, `*FilterDockResizeTests.<method>` |
| 07-37 | Rail state, panel count line, phone Filters button, copy | `filter-copy.test.mjs`, `copy-parity.test.mjs`; `--filter-method "*FilterDockRailTests.<method>"`, `*FilterSlimBarTests.<method>` (DejaVu) |
| 07-38 | Column rule, geometry per breakpoint, layout guide | `cabinet-layers.test.mjs`; `--filter-method "*FilterDockGeometryTests.<method>"` (DejaVu); `FilterBarGeometryTests` |
| 07-39 | Remembered panel, head script, default folded, empty pages | `filter-state.test.mjs`, `filter-dock-head.test.mjs`; `--filter-method "*FilterDockMemoryTests.<method>"` |
| 07-40 | Show on shelf, view fade, live redraw beside the dock; two hardened tests | `locations-view.test.mjs`, `card-css.test.mjs`; `--filter-method "*FilterDockFlowTests.<method>"`; `FilterShowOnShelfTests` repeated runs |
| 07-41, 07-42 | Existing desktop-width filter tests moved to the panel | the migrated classes, one suite at a time (DejaVu) |
| 07-43 | Rail and panel contrast, rings, access | `*LookChromeContrastTests`; `filters-css.test.mjs`; `FilterFocusRingTests`, `FilterAccessTests` |
| 07-44, 07-45 | Build round 2 and the owner's answer | full suite, one browser suite at a time, DejaVu, lint; frames manifest check |
| 07-46, 07-47 | Release of the rework | local gates; CI jobs incl. `browser-filters`; draft verification; container evidence |
| 07-34 | D-34 manual | device round on the deployed release (moved last, after 07-47) |

---

## Wave 0 Requirements

- [ ] `build/tests/filter-rules.test.mjs`, `filter-state.test.mjs`, `filters-css.test.mjs`
- [ ] `Cabinet.UnitTests` — `LocationLayoutsTests`, `DimmedBoxContrastTests`, per-copy owner-data tests
- [ ] `Cabinet.IntegrationTests` — `LocationLayoutEndpointTests`, `SearchNamesEndpointTests`
- [ ] Fakes and samples: alternate names in `Cabinet.FakeBgg` (an accented and a Dutch-like invented title), version names, a synthetic owner fixture with "Cabinet A", "Shelf B", "Lent out", unplaced games, an expansion stored apart, a player-count expansion, a game owned in several versions, games with unknown values
- [ ] Promote `OwnerCabinetTests.OwnerHost` to `Cabinet.BrowserTests/Infrastructure`
- [ ] CI: new `browser-filters` job for `Cabinet.BrowserTests.Filter*`, and `--filter-not-class "Cabinet.BrowserTests.Filter*"` on `browser-rest`
- [ ] Re-record golden layouts after the layout version bump

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Android Back closes the card and the Filters sheet, one sensible step each | D-34 | Needs a real Android device and its system Back gesture | On the deployed release: open a card, Back; open the Filters sheet, Back; confirm each Back closes exactly one thing and never leaves the page |
| Screen-reader listen of the skip link, games list, filtered list, By-location grouping and count announcements | D-34, A11Y-01 | Needs a real screen reader | On the deployed release with TalkBack or VoiceOver: skip link → list; filter by players; switch to By location; confirm headings, entries and one count announcement after typing |
| The 360px slim bar stays one row with phone fonts | U1 (UI-SPEC) | CI draws with DejaVu, which needs 390px | Screenshot round on a 360px phone, EN and NL |
| Real BGG shapes for the owned edition's name and alternate names | D-14, D-31 | No real BGG answer may be recorded in the repo | Extended BGG access check reports counts only, on the server |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

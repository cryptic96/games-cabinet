---
phase: 4
slug: enrichment-box-images-shape
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-10-07
validated: 2026-10-09
---

# Phase 4 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.1 on Microsoft Testing Platform, FluentAssertions 8.11.0; Node built-in test runner for page scripts |
| **Config file** | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`), `Cabinet.slnx` |
| **Quick run command** | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --filter-trait "Category=<Enrichment|Images|Layout|Snapshot>"` |
| **Full suite command** | `dotnet test --solution Cabinet.slnx --no-restore && node --test build/tests/page-scripts.test.mjs && build/lint.sh` |
| **Estimated runtime** | ~30 seconds quick, ~120 seconds full |

---

## Sampling Rate

- **After every task commit:** Run the trait-filtered unit command for the area touched; `node --test build/tests/page-scripts.test.mjs` for JS work
- **After every plan wave:** Run the full suite command
- **Before `/gsd-verify-work`:** Full suite must be green
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

Filled in by the planner/executor per task. Requirement-to-test map (from RESEARCH.md § Validation Architecture):

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| SYNC-06 | `thing` parser: designers, mechanics, min age, play time, ratings, inbound expansion links, caps, hostile XML | unit | `--filter-trait "Category=Enrichment"` | ✅ | ✅ green |
| SYNC-06 | Client: 20-id batching, pacing, retry, auth only to API host, no `versions=1` | unit | `--filter-trait "Category=Enrichment"` | ✅ | ✅ green |
| SYNC-06 | Refresh planner: new first, stale oldest first, per-run cap, unchanged run makes zero calls | unit | `--filter-trait "Category=Enrichment"` | ✅ | ✅ green |
| SYNC-06 | Pairing: lowest collection id among owned bases, standalone stays base, orphan, "contains" ignored | unit | `--filter-trait "Category=Snapshot"` | ✅ | ✅ green |
| SYNC-06 | Snapshot v2 round trip, v1 loads, enrichment survives refreshed collection | unit | `--filter-trait "Category=Snapshot"` | ✅ | ✅ green |
| SYNC-06 | Fake BGG → snapshot → layout JSON places expansions beside bases; failed `thing` batch keeps collection | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj` | ✅ | ✅ green |
| SYNC-07 | Downloader: https only, exact host allowlist, redirect re-check, byte cap, token never sent, own pacing | unit | `--filter-trait "Category=Images"` | ✅ | ✅ green |
| SYNC-07 | Analyzer: pixel cap before decode, alpha PNG, variants without upscale, hashed names, atomic write, prune | unit | `--filter-trait "Category=Images"` | ✅ | ✅ green |
| SYNC-07 | No BGG/CDN host in layout JSON or page; `/art/...` served `immutable` | integration | integration project | ✅ | ✅ green |
| IMG-01 | Detector verdicts on synthetic fixtures (flat, framed, 3D on white/grey/black, transparent, undecodable) | unit | `--filter-trait "Category=Images"` | ✅ | ✅ green |
| IMG-01 | Chooser table; verdict flip leaves poses and cubbies unchanged | unit | `--filter-trait "Category=Enrichment"` | ✅ | ✅ green |
| IMG-03 | Shape chain order, disagreement margin, orientation-insensitive ratio, 3D outline never used | unit | `--filter-trait "Category=Snapshot"` | ✅ | ✅ green |
| IMG-03 | Estimate hysteresis | unit | `--filter-trait "Category=Snapshot"` | ✅ | ✅ green |
| IMG-03 | Engine floors, `showBaseLine`, art fit, re-recorded goldens | unit (golden) | `--filter-trait "Category=Layout"` | ✅ | ✅ green |
| CAB-03 | Contrast grid passes after nudge, nudge bounds, invalid stored pair ignored | unit | `--filter-trait "Category=Layout"` | ✅ | ✅ green |
| CAB-03 | Colour extraction ignores plain backdrop, handles transparency and full-bleed gradients | unit | `--filter-trait "Category=Images"` | ✅ | ✅ green |
| CAB-03, IMG-01 | Renderer: art cover markup, error swap, `--bg/--fg`, no `style` attribute | node unit | `node --test build/tests/page-scripts.test.mjs` | ✅ | ✅ green |

| SYNC-07 | Robustness: hairline pictures refused, analysis copy bounded, per-picture failures and per-request time limit | unit + integration | `*ArtWorkingCopyTests`, `*ArtProcessorTests`, `*ArtSyncTests`, `*ImageDownloaderTests`, `*BoxArtTests` | ✅ | ✅ green |
| IMG-01 | Real failure shapes reproduced synthetically (cut-out, tight white crop, coloured or dark edge field); review cases pinned | unit | `*ReviewCaseVerdictTests`, `Category=Images` | ✅ | ✅ green |
| IMG-03 | Landscape flat and unsure pictures turn real-size boxes; pose never comes from a picture | unit + integration | `*BoxShapeTests`, `*PoseStabilityTests`, `*TrueProportionsTests` | ✅ | ✅ green |
| CAB-04, CAB-05, CAB-07 | Series stand together and never skip a cubby; families face out and continue into the next cubby; desktop density on a realistic size mix | unit + integration | `*SeriesLayoutTests`, `*SeriesInvariantTests`, `*FamilyLayoutTests`, `*FamilyStabilityTests`, `*DesktopDensityTests`, `*SeriesTests`, `*FamilyCoverTests` | ✅ | ✅ green |
| EXP-01, EXP-03 | A family never overflows its shelf (invariant over samples, seeds and both designs) | unit | `*FamilyLayoutTests`, `LayoutAssertions` | ✅ | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Planned tasks (filled in by the planner)

| Plan-Task | Requirement | Automated command (from the task's verify) |
|-----------|-------------|---------------------------------------------|
| 04-01-1 | SYNC-06, SYNC-07, IMG-01 | `bash build/tests/bgg-access-check-test.sh && python3 -I build/bgg-access-check.py --self-test` |
| 04-02-1 | SYNC-07 | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj --filter-class "*BoxArtTests"` + page scripts |
| 04-02-2 | SYNC-07 | `--filter-trait "Category=Images"` |
| 04-02-3 | SYNC-07 | `--filter-trait "Category=Images"` + `*BoxArtTests` + full solution |
| 04-03-1..3 | CAB-03, IMG-01 | `*SpineColourTests`, `*ArtChoiceTests`, `Category=Layout`, `Category=Enrichment` |
| 04-04-1..3 | IMG-01, CAB-03 | `*ArtAnalysisTests`, `Category=Images` |
| 04-05-1, 3 | SYNC-06, SYNC-07, IMG-01 | outcome file exists and holds no address or image path; latest commit holds the outcome |
| 04-06-1..3 | SYNC-06 | full integration project, `*EnrichmentTests`, `Category=Enrichment`, `Category=Snapshot` |
| 04-07-1..3 | CAB-03, IMG-03 | `*ThinBoxTests`, `Category=Layout` (goldens), page scripts, `*ContentSecurityPolicyTests` |
| 04-08-1..3 | IMG-01, CAB-03, SYNC-07 | `*ArtChoiceTests`, `Category=Enrichment`, `*ArtSettingsTests`, page scripts |
| 04-09-1..2 | IMG-03 | `Category=Layout` (with `DensityTests`), `build/lint.sh repo-rules` |
| 04-10-1..3 | IMG-03 | `*BoxShapeTests`, `*TrueProportionsTests`, `Category=Snapshot`, `PoseStabilityTests` via full solution |
| 04-11-1..2 | SYNC-07, IMG-01 | `*LocalArtTests`, full solution |
| 04-12-1..3 | IMG-01, IMG-03, CAB-03 | `*ReviewSheetTests`, `*ReviewSheetModelTests`, `*ReviewSheetCommandTests`, `build/lint.sh` |
| 04-13-1, 3 | all | full solution, page scripts, lint (scratch geometry, CSP and request checks reported) |
| 04-14-1, 3 | SYNC-06, SYNC-07 | draft check; published release verification, selfcheck, font present, no foreign host in the layout |
| 04-15-1, 3 | IMG-01, IMG-03, CAB-03 | nothing from the round left in the repository or on the server; service healthy |
| 04-16-1, 3 | all | full solution, page scripts, lint, draft; published release, selfcheck, drop-in removed, todos closed |
| 04-17-1..3 | IMG-01, IMG-03 | `*BoxShapeTests`, `*TrueProportionsTests`, `Category=Images`, full solution offline, page scripts, lint |
| 04-18-1, 3, 5 | SYNC-07, IMG-01 | PR checks reported by the orchestrator; draft and published release verification; counts-only re-measure checks |
| 04-19-1..3 | IMG-01 | `Category=Images`, `*ReviewCaseVerdictTests`, integration `*ArtChoiceTests`, full solution offline, lint |
| 04-20-1..3 | SYNC-06, CAB-04, CAB-05 | `*SeriesTests`, `Category=Layout` (goldens), `Category=Enrichment`, full solution offline |
| 04-21-1..3 | EXP-01, EXP-03, CAB-05 | `*FamilyLayoutTests`, `*FamilyStabilityTests`, `*FamilyCoverTests`, full solution offline |
| 04-22-1..3 | CAB-04, CAB-07 | `*DesktopDensityTests`, `*DensityTests`, goldens, local screenshot checks reported |
| 04-23-1, 3, 5 | SYNC-06, SYNC-07, IMG-01 | PR checks reported by the orchestrator; draft and published release verification; counts-only refresh and re-measure checks |

---

## Wave 0 Requirements

- [x] Server shape and CDN check script (prints counts and classes only) plus its self-test (`build/bgg-access-check.py --suite art`, `build/tests/bgg-access-check-test.sh`)
- [x] Synthetic picture generator (built as `Cabinet.FakeBgg/SyntheticArt.cs`, shared by the unit tests and the fake BGG)
- [x] Enrichment tests (built as `Cabinet.UnitTests/Bgg/BggThingParserTests.cs`, `BggThingClientTests.cs`, `Cabinet.UnitTests/Collection/EnrichmentPlannerTests.cs`, `ExpansionPairingTests.cs`, `ArtChoiceTests.cs`, `BoxShapeTests.cs`, `SizeEstimateTests.cs`)
- [x] `Cabinet.UnitTests/Layout/SpineColourTests.cs`, `ArtFittingTests.cs` and floor cases (`ThinBoxTests.cs`)
- [x] Integration pipeline tests with a scripted image host (built as `Cabinet.IntegrationTests/EnrichmentTests.cs`, `BoxArtTests.cs`, `LocalArtTests.cs`)
- [x] Fake BGG `thing` data, version images, fake image host, Development-only host override
- [x] Page-script cases for art covers and the `+N more` marker

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Flat-vs-3D verdicts on the owner's real Dutch editions | IMG-01 | Real photos; thresholds are tuning values, real data must not enter the repo | Generate the review sheet outside the repository, owner reviews verdicts, tune settings, restart |
| Boxes visibly differ in size and shape; art fit, thin spines, phone density, plinth | IMG-03, CAB-03 | Visual judgement | Fake-BGG screenshot round, then deployed-cabinet review by the owner |
| A browser never requests a foreign host | SYNC-07 | Full network audit of a real page load | Scratch Playwright run against the deployed site, check request hosts (done: browser audit 20 of 20 on v0.5.1, see 04-16-SUMMARY) |
| Server art-check run and the owner's sign-off on its outcome | SYNC-06, SYNC-07, IMG-01 | Needs the real token and a live run against BGG | Approved single run, outcome signed off (04-ART-CHECK-OUTCOME.md) |
| Releases, deploy approval, re-measure and details refresh on the container | SYNC-06, SYNC-07 | Owner-gated operations on the live server | Draft and published verification, counts-only checks (04-14, 04-16, 04-18, 04-23 summaries) |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies (owner checkpoints and release operations are manual by design)
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [ ] Feedback latency < 30s: the targeted category filters run well under 30 s, but the full offline suite now takes about 1.5 minutes (2051 tests)
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-10-09

---

## Validation Audit 2026-10-09

| Metric | Count |
|--------|-------|
| Gaps found | 0 |
| Resolved | 0 |
| Escalated | 0 |

Audit by a report-only gsd-nyquist-auditor run over plans 04-01 to 04-23 and the two post-plan fixes (series continuation, picture pipeline robustness). Suite at audit time: 2051 tests passed offline (1818 unit, 233 integration), 92 page-script tests passed. Test-quality findings from the code review (loosened bounds, real-clock timing, the flaky live-cap refusal test) are reliability items, not coverage gaps; they are tracked in `.planning/todos/pending/2026-10-08-sharpen-tests-from-phase-4-review.md`.

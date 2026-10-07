---
phase: 4
slug: enrichment-box-images-shape
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-07
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
| SYNC-06 | `thing` parser: designers, mechanics, min age, play time, ratings, inbound expansion links, caps, hostile XML | unit | `--filter-trait "Category=Enrichment"` | ❌ W0 | ⬜ pending |
| SYNC-06 | Client: 20-id batching, pacing, retry, auth only to API host, no `versions=1` | unit | `--filter-trait "Category=Enrichment"` | ❌ W0 | ⬜ pending |
| SYNC-06 | Refresh planner: new first, stale oldest first, per-run cap, unchanged run makes zero calls | unit | `--filter-trait "Category=Enrichment"` | ❌ W0 | ⬜ pending |
| SYNC-06 | Pairing: lowest collection id among owned bases, standalone stays base, orphan, "contains" ignored | unit | `--filter-trait "Category=Snapshot"` | ❌ W0 | ⬜ pending |
| SYNC-06 | Snapshot v2 round trip, v1 loads, enrichment survives refreshed collection | unit | `--filter-trait "Category=Snapshot"` | extend | ⬜ pending |
| SYNC-06 | Fake BGG → snapshot → layout JSON places expansions beside bases; failed `thing` batch keeps collection | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj` | ❌ W0 | ⬜ pending |
| SYNC-07 | Downloader: https only, exact host allowlist, redirect re-check, byte cap, token never sent, own pacing | unit | `--filter-trait "Category=Images"` | ❌ W0 | ⬜ pending |
| SYNC-07 | Analyzer: pixel cap before decode, alpha PNG, variants without upscale, hashed names, atomic write, prune | unit | `--filter-trait "Category=Images"` | partial | ⬜ pending |
| SYNC-07 | No BGG/CDN host in layout JSON or page; `/art/...` served `immutable` | integration | integration project | ❌ W0 | ⬜ pending |
| IMG-01 | Detector verdicts on synthetic fixtures (flat, framed, 3D on white/grey/black, transparent, undecodable) | unit | `--filter-trait "Category=Images"` | ❌ W0 | ⬜ pending |
| IMG-01 | Chooser table; verdict flip leaves poses and cubbies unchanged | unit | `--filter-trait "Category=Enrichment"` | ❌ W0 | ⬜ pending |
| IMG-03 | Shape chain order, disagreement margin, orientation-insensitive ratio, 3D outline never used | unit | `--filter-trait "Category=Snapshot"` | extend | ⬜ pending |
| IMG-03 | Estimate hysteresis | unit | `--filter-trait "Category=Snapshot"` | ❌ W0 | ⬜ pending |
| IMG-03 | Engine floors, `showBaseLine`, art fit, re-recorded goldens | unit (golden) | `--filter-trait "Category=Layout"` | extend | ⬜ pending |
| CAB-03 | Contrast grid passes after nudge, nudge bounds, invalid stored pair ignored | unit | `--filter-trait "Category=Layout"` | ❌ W0 | ⬜ pending |
| CAB-03 | Colour extraction ignores plain backdrop, handles transparency and full-bleed gradients | unit | `--filter-trait "Category=Images"` | ❌ W0 | ⬜ pending |
| CAB-03, IMG-01 | Renderer: art cover markup, error swap, `--bg/--fg`, no `style` attribute | node unit | `node --test build/tests/page-scripts.test.mjs` | extend | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] Server shape and CDN check script (prints counts and classes only) plus its self-test
- [ ] `Cabinet.UnitTests/Images/SyntheticArt.cs` generator
- [ ] `Cabinet.UnitTests/Enrichment/` test folder (parser, client, planner, pairing, chooser, shape, estimate)
- [ ] `Cabinet.UnitTests/Layout/SpineColourTests.cs`, art-fit and floor cases
- [ ] `Cabinet.IntegrationTests/EnrichmentPipelineTests.cs` with a fake image host
- [ ] Fake BGG `thing` data, version images, fake image host, Development-only host override
- [ ] Page-script cases for art covers and the `+N more` marker

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Flat-vs-3D verdicts on the owner's real Dutch editions | IMG-01 | Real photos; thresholds are tuning values, real data must not enter the repo | Generate the review sheet outside the repository, owner reviews verdicts, tune settings, restart |
| Boxes visibly differ in size and shape; art fit, thin spines, phone density, plinth | IMG-03, CAB-03 | Visual judgement | Fake-BGG screenshot round, then deployed-cabinet review by the owner |
| A browser never requests a foreign host | SYNC-07 | Full network audit of a real page load | Scratch Playwright run against the deployed site, check request hosts |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

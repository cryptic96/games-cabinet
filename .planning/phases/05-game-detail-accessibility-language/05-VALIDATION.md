---
phase: 5
slug: game-detail-accessibility-language
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-09
---

# Phase 5 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.1 + FluentAssertions 8.11.0 on Microsoft.Testing.Platform; `node:test` for page logic; Playwright .NET 1.63.0 (`Microsoft.Playwright.Xunit.v3`) for browser tests |
| **Config file** | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`); browser project `Cabinet.BrowserTests` — Wave 0 installs |
| **Quick run command** | `dotnet test --project <TestProject> --filter-class "*<Class>"` and/or `node --test build/tests/<file>.test.mjs` |
| **Full suite command** | `dotnet test --solution Cabinet.slnx --no-restore --filter-not-trait "Category=Browser"` then `node --test build/tests/*.test.mjs` then `dotnet test --project Cabinet.BrowserTests` |
| **Estimated runtime** | ~10 s quick; ~90 s full without browser; ~3 min with browser |

---

## Sampling Rate

- **After every task commit:** Run the affected quick command (single class filter or one `node --test` file) plus `build/lint` repo rules
- **After every plan wave:** Run the full non-browser suite and all node tests; browser tests when the wave touches client behaviour
- **Before `/gsd-verify-work`:** Full suite including `Category=Browser` must be green
- **Max feedback latency:** 30 seconds (quick command)

---

## Per-Task Verification Map

Filled in by the planner/executor from PLAN.md tasks. Requirement-to-test seed from RESEARCH.md:

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| 01-T1 | 05-01 | 1 | (infra) | T-05-SC | Locked restore, test-only project | browser | `dotnet test --project Cabinet.BrowserTests --filter-class "*CabinetSmokeTests"` | ❌ W0 | ⬜ pending |
| 01-T2 | 05-01 | 1 | (infra) | T-05-01-02 | SHA-pinned actions, read-only permissions | lint + node | `bash build/lint/checks/20-workflows.sh`; `node --test build/tests/*.test.mjs` | ✅ | ⬜ pending |
| 02-T1..T3 | 05-02 | 1 | DET-02 | T-05-02-01, -03, -04 | Allowlisted query, field allowlist, no CORS | integration + unit | `--project Cabinet.IntegrationTests --filter-class "*CardsEndpointTests"`; `--project Cabinet.UnitTests --filter-class "*CardRecordsTests"`, `"*SampleCardDetailsTests"` | ❌ W0 | ⬜ pending |
| 03-T1..T2 | 05-03 | 2 | DET-02 | T-05-03-02 | Details-only change refreshes cards | unit + integration | `"*SnapshotMapperTests"`; `"*CardsEndpointTests"`; `"*ContentSecurityPolicyTests"` | ✅ partly | ⬜ pending |
| 04-T1..T3 | 05-04 | 1 | I18N-01 | T-05-04-01..06 | Allowlisted `en`/`nl`; fixed 303 target; `Vary`; one HttpOnly cookie; ICU fail-fast | integration + unit | `"*LanguageTests"`; `"*SiteLanguageTests"`; `"*SiteTextTests"` | ❌ W0 | ⬜ pending |
| 05-T1..T3 | 05-05 | 2 | I18N-01 | T-05-05-01..03 | Shared vectors, parity | node + unit + browser | `page-scripts.test.mjs`; `copy-parity.test.mjs`; `"*SyncStatusTextTests"`; `"*LanguageSwitchTests"` | ❌ W0 | ⬜ pending |
| 06-T1..T3 | 05-06 | 3 | DET-01, DET-02 | T-05-06-01, -02 | `textContent` only; digits-only BGG id in `href` | browser + node | `"*CardOpenTests"`; `"*BackButtonTests"`; `card-flow.test.mjs` | ❌ W0 | ⬜ pending |
| 07-T1..T2 | 05-07 | 4 | DET-02 | T-05-07-01..03 | Text only; locations only when sent | node + browser | `format.test.mjs`; `"*CardContentTests"` | ❌ W0 | ⬜ pending |
| 08-T1..T2 | 05-08 | 4 | A11Y-01 | — | N/A | node + browser | `keys.test.mjs`; `"*KeyboardTests"` | ❌ W0 | ⬜ pending |
| 09-T1..T2 | 05-09 | 5 | DET-02 | T-05-09-01, -02 | Chip colour validated; text only | node + unit + browser | `card-css.test.mjs`; `"*CardContrastTests"`; `"*CardExpansionTests"`; `"*CardLookTests"` | ❌ W0 | ⬜ pending |
| 10-T1..T2 | 05-10 | 5 | A11Y-02 | T-05-10-01 | Text only | node + browser | `games-list.test.mjs`; `"*GamesListTests"` | ❌ W0 | ⬜ pending |
| 11-T1..T3 | 05-11 | 6 | DET-01, DET-03 | T-05-11-01..03 | No View Transition under reduced motion | node + browser | `card-flow.test.mjs`; `"*PullOutTests"`; `"*ReducedMotionTests"` | ❌ W0 | ⬜ pending |
| 12-T1..T2 | 05-12 | 6 | (polish) | T-05-12-01 | Full title kept in name and card | unit + golden + node + browser | `--filter-trait "Category=Layout"`; `"*SpineLabelTests"`; `page-scripts.test.mjs`; `"*SpineLabelPageTests"` | ✅ partly | ⬜ pending |
| 13-T1..T3 | 05-13 | 7 | DET-01 | T-05-13-01..03 | No redraw under an open card | node + browser | `card-flow.test.mjs`; `"*CardSyncTests"`; `"*SheetDragTests"` | ❌ W0 | ⬜ pending |
| 14-T1..T2 | 05-14 | 7 | (polish) | T-05-14-01, -02 | Path check on file names; count-only log | integration + node + browser | `"*ArtFileAuditTests"`; `cabinet-css.test.mjs`; `"*CabinetPolishTests"` | ❌ W0 | ⬜ pending |
| 15-T1..T4 | 05-15 | 8 | all | T-05-15-01, -02 | Synthetic review material only | full suite + owner | non-browser suite, `node --test build/tests/*.test.mjs`, `dotnet test --project Cabinet.BrowserTests`, `build/lint.sh` | ✅ | ⬜ pending |
| 16-T1..T5 | 05-16 | 9 | all | T-05-16-01..05 | Attested release, owner publish, counts-only evidence | release + container + owner devices | `gh release view v0.6.0 ...`; container verify command in the plan | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Cabinet.BrowserTests/Cabinet.BrowserTests.csproj` + lock file — new project in `Cabinet.slnx`, `Category=Browser` trait, linked integration infrastructure, `Accept-Encoding: identity` workaround
- [ ] `.github/workflows/ci.yml` — exclude `Category=Browser` from the existing test step; new `browser-tests` job; node step becomes a glob
- [ ] Synthetic fixtures — complete details, invented locations, multi-base and orphan expansions, no-details and no-art games, mixed-script titles
- [ ] Node test files: `card-flow`, `history-step`, `format`, `keys`, `games-list`, `copy-parity`, `card-css`; shared `relative-time-cases.json` gains a `language` field
- [ ] C# test files: `CardRecordsTests`, `LanguageResolverTests`, `SiteTextParityTests`, `CardsEndpointTests`, `LanguageTests`, Version-includes-card-fields test

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Android / iOS Back closes the card and leaves no dead history step | DET-01 | No device automation; close-request behaviour differs per mobile browser | Open the review build on the owner's phone, open a card, press Back once (card closes), press Back again (leaves site) |
| Pull-out animation feel and card visual quality | DET-01, DET-02 | Subjective visual quality | Screenshot round at 390 and 1440 from the CI browser artifacts |
| Dutch copy reads naturally (side-by-side table) | I18N-01 | Language judgement by the owner | Review the generated en/nl side-by-side table |
| Screen reader announces list and card sensibly | A11Y-02 | Real assistive technology | Spot check with a screen reader on desktop or phone |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

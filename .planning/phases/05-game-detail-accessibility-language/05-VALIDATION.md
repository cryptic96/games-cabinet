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
| TBD | TBD | TBD | DET-01 | — | N/A | browser + node | `--filter-class "*PullOutTests"`; `node --test build/tests/card-flow.test.mjs`; `history-step.test.mjs` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | DET-02 | XSS via BGG text / link injection | `textContent` only; digits-only BGG id in `href` | unit + integration + node + browser | `--filter-class "*CardRecordsTests"`; `"*CardsEndpointTests"`; `format.test.mjs`; `"*CardContentTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | DET-03 | — | N/A | browser | `--filter-class "*ReducedMotionTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | A11Y-01 | — | N/A | node + browser | `keys.test.mjs`; `--filter-class "*KeyboardTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | A11Y-02 | — | N/A | node + browser | `games-list.test.mjs`; `--filter-class "*GamesListTests"` | ❌ W0 | ⬜ pending |
| TBD | TBD | TBD | I18N-01 | Open redirect / cookie tampering / shared-cache leak | Allowlisted `en`/`nl`; fixed 303 target; `Vary` header | unit + integration + node + browser | `"*LanguageResolverTests"`; `"*LanguageTests"`; `"*SiteTextParityTests"`; `copy-parity.test.mjs`; `"*LanguageSwitchTests"` | ❌ W0 | ⬜ pending |

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

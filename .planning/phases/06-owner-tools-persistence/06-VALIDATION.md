---
phase: 6
slug: owner-tools-persistence
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-12
---

# Phase 6 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from the Validation Architecture section of `06-RESEARCH.md`, which carries the full requirement-to-test map.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.2 + FluentAssertions 8.11.0 (unit, integration), Microsoft.Playwright.Xunit.v3 (browser), Node built-in test runner (page scripts), bash scripts with stubs (deploy and release scripts) |
| **Config file** | `global.json` (`test.runner: Microsoft.Testing.Platform`), per-project csproj |
| **Quick run command** | `./Cabinet.UnitTests/bin/Debug/net10.0/Cabinet.UnitTests -class "*<TouchedClass>*"` after a build, plus `node --test build/tests/*.test.mjs` |
| **Full suite command** | `dotnet test --project Cabinet.UnitTests && dotnet test --project Cabinet.IntegrationTests && node --test build/tests/*.test.mjs && dotnet test --project Cabinet.BrowserTests && build/lint.sh` plus the deploy and release script tests the CI runs |
| **Estimated runtime** | ~75 s unit, a few minutes integration and browser |

---

## Sampling Rate

- **After every task commit:** the touched test class via the built test executable (`-class` filter) and `node --test build/tests/*.test.mjs`
- **After every plan wave:** both .NET test projects in full, node tests, `build/lint.sh`
- **Before `/gsd-verify-work`:** unit, integration, browser, node, lint and script tests green; after the release, the deployed-release route check through both routers
- **Max feedback latency:** ~90 seconds per task

---

## Per-Task Verification Map

Filled by the planner per task (each task's `<verify>` / `<automated>` block). The requirement-level map is in `06-RESEARCH.md` → "Validation Architecture" → "Phase Requirements to Test Map" (OWN-01, OWN-02, LOC-03, LOC-04, IMG-02, cross-cutting and folded rows).

| Task ID | Plan | Wave | Requirement | Threat Ref | Secure Behavior | Test Type | Automated Command | File Exists | Status |
|---------|------|------|-------------|------------|-----------------|-----------|-------------------|-------------|--------|
| (planner fills) | | | | | | | | | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `CabinetWebApplicationFactory`: third port, owner endpoint and ranges in configuration, `CreateOwnerClient()`; `TestHostPortTests` updated
- [ ] `CabinetPageTest`: `OwnerPort` and an owner page base class (CSP console check, host recorder)
- [ ] Untrusted-peer test host helper for the forged `X-Forwarded-For` test
- [ ] Owner-data test kit: in-memory store, fake-clock service factory, synthetic collections with invented locations
- [ ] Fake BGG and sample cases from the UI contract's review matrix (all synthetic)
- [ ] Failing tests for the folded fixes (WR-02, WR-03, WR-04, review sheet) written before their changes

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Owner host reachable from home/VPN through the second Traefik router; owner paths on the public host answer 404 | OWN-01 | Needs the real Traefik host, DNS and network ranges (owner prerequisites) | Run the scripted route check from the owner's machine against the deployed release |
| Review-round visual approval of owner pages, Edit link, strip and corner tag shape (T1) | IMG-02, LOC-03, LOC-04 | Taste calls on synthetic screenshots | Screenshots at 1440 px and 390 px per the UI contract's review matrix |
| First real override and location session on the deployed site | IMG-02, LOC-04 | Real collection data never enters the repository | Owner works through the override worklist and places games at the shelf |
| Export then restore on the deployed container | OWN-02 | Real owner data, real server | Download owner data, restore it, confirm counts in the preview |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 90s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

---
phase: 2
slug: layout-engine-cabinet-prototype
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-05
---

# Phase 2 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.1 on Microsoft.Testing.Platform, FluentAssertions 8.11.0 |
| **Config file** | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`), `Directory.Build.props` |
| **Quick run command** | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Layout"` |
| **Full suite command** | `dotnet test --solution Cabinet.slnx --no-restore` plus `build/lint.sh` |
| **Estimated runtime** | ~30 seconds |

Note: trait filters make `dotnet test --solution` exit 8 when a project has no matching tests; filter per project with `--project`, or pass `--ignore-exit-code 8`.

---

## Sampling Rate

- **After every task commit:** Run the quick command, plus `build/lint.sh repo-rules` when strings or comments changed
- **After every plan wave:** Run the full suite command and `build/lint.sh`
- **Before `/gsd-verify-work`:** Full suite must be green, CI green on the PR, screenshots reviewed
- **Max feedback latency:** 30 seconds

---

## Per-Task Verification Map

Filled in by the planner once task IDs exist. Requirement-to-test intent from research:

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| CAB-01 | 0, 1, 5, 12, 65, 400 samples produce valid layouts; section count grows; empty = one section of empty cubbies | unit (invariants) | quick command | ❌ W0 | ⬜ pending |
| CAB-02 | Face-out share within tolerance band on 65 and 400 samples; strategies behave as specified | unit | quick command | ❌ W0 | ⬜ pending |
| CAB-04 | Placements inside cubby, no overlap, every item placed exactly once; design validator for both designs | unit (property) | quick command | ❌ W0 | ⬜ pending |
| CAB-05 | Same (and shuffled) input gives byte-identical JSON; appending one item changes at most one cubby (200 seeds); goldens 0/1/5/12/65, hash pin for 400 | unit | quick command | ❌ W0 | ⬜ pending |
| CAB-06 | Below threshold every top-level game is a cover; never fewer than one section; 0 sample renders empty cubbies | unit + integration | quick command + full suite | ❌ W0 | ⬜ pending |
| CAB-07 | Phone design differs from desktop; no phone spine/layer below profile minimum; profile allowlist; endpoint serves both | unit + integration | quick command + full suite | ❌ W0 | ⬜ pending |
| EXP-01 | Every owned expansion with an owned base sits in the base's cubby, in the family column right of the base | unit | quick command | ❌ W0 | ⬜ pending |
| EXP-02 | Orphan expansions placed as their own boxes with a sub-label naming the missing base; multi-parent picks lowest-id owned parent | unit | quick command | ❌ W0 | ⬜ pending |
| EXP-03 | Family above max shows max-or-fewer layers plus a marker whose count equals the hidden total; stack never exceeds cubby height | unit | quick command | ❌ W0 | ⬜ pending |
| cross-cutting | Palette contrast ≥ 4.5:1; spine labels shortened deterministically | unit | quick command | ❌ W0 | ⬜ pending |
| cross-cutting | Page has no `style=` attributes or inline scripts; assets served; sample parameter rejected when prototype setting off or value not allowlisted | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj --no-restore` | ❌ W0 | ⬜ pending |
| cross-cutting | Committed appsettings pass the configuration test with new `Layout` keys | unit | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Configuration"` | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Task-level map (filled by the planner)

| Task | Requirements | Automated verify | Notes |
|------|--------------|------------------|-------|
| 02-01 T1 (tracer) | CAB-01, CAB-04 | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj` + ES-module syntax check | Scratch browser check: 65 placements on `/` |
| 02-01 T2 | CAB-01, CAB-04, CAB-05 | quick command | Invariants, shuffled-input bytes, 200-seed append stability, sample tests |
| 02-02 T1 (tracer) | CAB-02, CAB-06 | quick command | Strategies, hash pins, few-games switch |
| 02-02 T2 | CAB-02 | quick command + `--filter-trait "Category=Configuration"` | Settings binding and startup validation |
| 02-02 T3 | CAB-02, CAB-04, CAB-06 | quick command | Share band, boundaries 11/12/13, flat stacks, independence |
| 02-03 T1 (tracer) | CAB-02, CAB-04 | quick command + syntax check | Palette contrast >= 4.5:1, tone/pattern independence |
| 02-03 T2 | CAB-02 | quick command | Label shortening in text elements |
| 02-03 T3 | CAB-01, CAB-04 | quick command + syntax check | Classic furniture finish (D-20): palette and marker-chip contrast under the shade cap; scratch screenshots at 1440 and 390 px checking reserved height, unclipped top, plinth and floor shadow, per-label contrast with the shade, focus ring above furniture, no console or policy errors, no horizontal scroll |
| 02-04 T1 (tracer) | CAB-01, CAB-06 | integration project | Sample switcher, unknown sample not echoed, prototype off |
| 02-04 T2 | CAB-07 | integration project + syntax check | ETag/304, no style attributes or inline scripts, profile by media query |
| 02-04 T3 | CAB-01 | docs grep + `build/lint.sh repo-rules` | Settings and stability guide |
| 02-05 T1 (tracer) | EXP-01, EXP-03 | quick command + syntax check | Stacks beside bases, "+N more", stack height |
| 02-05 T2 | EXP-02 | quick command | Orphans, multi-parent, few-games count |
| 02-05 T3 | CAB-05, EXP-01, EXP-03 | quick command | Family append stability, first-expansion exception |
| 02-06 T1 (tracer) | CAB-07 | quick command + integration project | Derived phone floors, phone endpoint |
| 02-06 T2 | CAB-01, CAB-04 | quick command | Design validator, box clamp, worst-case family |
| 02-06 T3 | CAB-05 | quick command | Goldens 0/1/5/12/65, 400 digest, version-bump guard |
| 02-07 T1 (tracer) | all phase requirements | `dotnet test --solution Cabinet.slnx` | Scratch geometry and strict-CSP check on every sample and width |
| 02-07 T2 | all | n/a (owner decision) | Blocking owner checkpoint |
| 02-07 T3 | all | `dotnet test --solution Cabinet.slnx` | Tuning round; goldens and version when arrangement changes |
| 02-08 T1 (tracer) | all | `gh release list` draft check | PR, merge, tag, attested draft |
| 02-08 T2 | all | n/a (owner decision) | Owner publishes |
| 02-08 T3 | all | `build/verify-published-release.sh` + `cabinet-selfcheck` | Deployed owner check (human) |

---

## Wave 0 Requirements

- [ ] `Cabinet.UnitTests/Layout/` test classes for invariants, stability, determinism, strategies, stack, palette, designs, synthetic data (all tagged `[Trait("Category", "Layout")]`)
- [ ] `Cabinet.UnitTests/Layout/Golden/` JSON goldens plus an opt-in regeneration path (environment variable read by the test, never set in CI)
- [ ] Integration tests for page, endpoint, asset and CSP-shape checks (extend the existing factory)

No new framework or package needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Cabinet reads as a real wooden cubby cabinet (face-outs mixed with spines, irregular cubbies) | CAB-01, CAB-02, CAB-04 | Visual quality is subjective; owner approval is the phase acceptance gate | Review screenshots at desktop and phone widths for each sample between rounds; final check on the deployed release |
| Phone reflow still looks like a cabinet, spines readable and tappable | CAB-07 | Subjective readability on a real device | Open the deployed prototype on a phone for the 65 and 400 samples |
| Empty and near-empty collections look intentional | CAB-06 | Subjective | Review 0, 1 and 5 samples on desktop and phone |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 30s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

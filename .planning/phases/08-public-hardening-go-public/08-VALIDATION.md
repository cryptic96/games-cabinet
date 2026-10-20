---
phase: 8
slug: public-hardening-go-public
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-20
---

# Phase 8 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.2 on Microsoft.Testing.Platform, FluentAssertions 8.11.0; Playwright .NET 1.63.0 (browser); bash tests under `deploy/tests/` and `build/tests/`; `node --test` for page scripts |
| **Config file** | `global.json`, `Directory.Build.props` (no per-project runner config) |
| **Quick run command** | `dotnet test --project Cabinet.IntegrationTests --filter-class "Cabinet.IntegrationTests.<Class>"` ; `bash deploy/tests/render-templates-test.sh` |
| **Full suite command** | `dotnet test --project Cabinet.UnitTests && dotnet test --project Cabinet.IntegrationTests && node --test build/tests/*.test.mjs && build/lint.sh` ; browser slices: `dotnet test --project Cabinet.BrowserTests --filter-class "Cabinet.BrowserTests.<prefix>*"` (solo, never in parallel) |
| **Estimated runtime** | ~30 s per class; integration suite a few minutes; browser slices several minutes each |

---

## Sampling Rate

- **After every task commit:** Run the single new or touched test class plus the template test it touches
- **After every plan wave:** `dotnet test --project Cabinet.UnitTests`, `dotnet test --project Cabinet.IntegrationTests`, `build/lint.sh script-tests`, `node --test build/tests/*.test.mjs`
- **Before `/gsd-verify-work`:** Full suite green, all browser slices run solo, `build/lint.sh` (all checks), `build/scan-history.sh`, then the manual go-public checklist
- **Max feedback latency:** 60 seconds for non-browser classes

---

## Per-Task Verification Map

Filled in from the plans once they exist. Requirement-level map from research:

| Requirement | Behavior | Test Type | Automated Command | File Exists | Status |
|-------------|----------|-----------|-------------------|-------------|--------|
| SEC-03 | Every security header present exactly once on page, static, art, JSON, negotiate, 404, 429, 304 on public, owner and ops ports | integration | `--filter-class "Cabinet.IntegrationTests.SecurityHeadersTests"` | ❌ W0 | ⬜ pending |
| SEC-03 | Page and an owner page log nothing to the console under the headers; CSP-violation tests stay green | browser | `--filter-class "Cabinet.BrowserTests.Lights*"` plus new class | ❌ W0 | ⬜ pending |
| SEC-02 | Sync press over the per-address limit gets 429 + `Retry-After` in the shared shape; separate budgets per address; forged header from untrusted peer creates no budget | integration | `--filter-class "Cabinet.IntegrationTests.SyncRateLimitTests"` | ❌ W0 | ⬜ pending |
| SEC-02 | Negotiate and connect limited; per-address live cap counts WebSocket and SSE; release frees the place | integration | `--filter-class "Cabinet.IntegrationTests.LiveAddressCapTests"` | ❌ W0 | ⬜ pending |
| SEC-02 | Foreign `Origin` refused on negotiate and upgrade; none or same-origin allowed | integration | `--filter-class "Cabinet.IntegrationTests.LiveOriginTests"` | ❌ W0 | ⬜ pending |
| SEC-02 | Refused live page stays usable and quiet | browser | `--filter-class "Cabinet.BrowserTests.LiveLook*"` plus new test | ❌ W0 | ⬜ pending |
| SEC-04 | Foreign Host gets 400; listed host 200; loopback health still answers | integration | `--filter-class "Cabinet.IntegrationTests.HostFilteringTests"` | ❌ W0 | ⬜ pending |
| SEC-04 | Owner router keeps allow-list without rate limit; public router has rate limit and in-flight with explicit source | script | `bash deploy/tests/render-templates-test.sh` | ✅ (new assertions) | ⬜ pending |
| SEC-04 | External probe script passes and fails per check, never prints a hostname | script | `bash build/tests/check-public-routes-test.sh` | ❌ W0 | ⬜ pending |
| SEC-06 | Unit limit directives; rendered egress rules; validators refuse malformed ranges; selfcheck limit and egress checks | script | `bash deploy/tests/sandboxing-test.sh && bash deploy/tests/render-templates-test.sh && bash deploy/tests/provision-logic-test.sh` | ✅ (new assertions) | ⬜ pending |
| SEC-06 | Kestrel limits applied; body limit does not break owner uploads | integration | `--filter-class "Cabinet.IntegrationTests.KestrelLimitsTests"` and `OwnerRestoreTests` | ❌ W0 / ✅ | ⬜ pending |
| Folded todo | Browser tests no longer flake on CI | browser | failing classes run repeatedly, before and after | ✅ | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Cabinet.IntegrationTests/SecurityHeadersTests.cs` — SEC-03 matrix
- [ ] `Cabinet.IntegrationTests/SyncRateLimitTests.cs`, `LiveAddressCapTests.cs`, `LiveOriginTests.cs`, `HostFilteringTests.cs`, `KestrelLimitsTests.cs`
- [ ] Limiter registration shared with the untrusted-peer host for the forged-header test
- [ ] Browser console-clean test (Chromium and WebKit) and live-refusal test
- [ ] `build/check-public-routes.sh` and `build/tests/check-public-routes-test.sh`
- [ ] New assertions in `deploy/tests/render-templates-test.sh`, `provision-logic-test.sh`, `sandboxing-test.sh`
- [ ] Shared browser helpers in `CabinetPageTest` (live-connected wait, URL retry, settle)
- No framework install needed.

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Site loads publicly over HTTPS; health, ops and owner routes unreachable from the internet | SEC-01, SEC-04 | Needs the real network and the owner's phone on mobile data | Go-public checklist in `docs/`: phone probe plus online scanners |
| Flood slows the app without starving neighbours; recovers without restart or OOM kill | SEC-06 | Needs the real host, the shared proxy and the Proxmox host graphs | Controlled flood from the proxy container, before/during numbers, journal and restart count |
| Container caps applied; rebuild and sync peaks recorded | SEC-06 | Proxmox settings are owner-only; peaks need the real container | Owner runs the documented `pct set`; Claude reads cgroup files and unit peaks |
| BGG terms re-read and confirmed | SEC-01 | Owner decision | Claude summarises changes; owner confirms |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

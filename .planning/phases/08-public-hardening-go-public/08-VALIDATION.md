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

Filled in from the plans (2026-10-20). `IT` = `DOTNET_USE_POLLING_FILE_WATCHER=1 dotnet test --project Cabinet.IntegrationTests`, `UT` = `dotnet test --project Cabinet.UnitTests`, `BT` = `dotnet test --project Cabinet.BrowserTests --no-build` (solo).

| Plan-Task | Req | Behavior | Test Type | Automated Command | Status |
|-----------|-----|----------|-----------|-------------------|--------|
| 08-01-1 | SEC-02 | Per-client sync refusal in the shared shape; boundary N/N+1 | integration | `IT --filter-class "Cabinet.IntegrationTests.SyncRateLimitTests"` | ⬜ pending |
| 08-01-2 | SEC-02 | Separate budgets, forged header, IPv6 /64, window recovery, settings, page sentence | integration + unit + node | `IT --filter-class "...SyncRateLimitTests"`, `UT --filter-class "*ClientLimitSettingsTests"`, `node --test build/tests/page-scripts.test.mjs` | ⬜ pending |
| 08-02-1 | SEC-06 | Unit limits and GC env; selfcheck limits vs container | script | `bash deploy/tests/sandboxing-test.sh` | ⬜ pending |
| 08-02-2 | SEC-06 | Output chain rendered, validated, ordered | script | `bash deploy/tests/render-templates-test.sh && bash deploy/tests/provision-logic-test.sh` | ⬜ pending |
| 08-02-3 | SEC-06 | Selfcheck egress refused at once; DNS | script | `bash deploy/tests/sandboxing-test.sh` | ⬜ pending |
| 08-03-1 | SEC-03 | Header set exactly once, no hostname printed | script | `bash build/tests/check-public-routes-test.sh` | ⬜ pending |
| 08-03-2 | SEC-01, SEC-04 | Routes closed, owner host by vantage, noindex, credit, burst (default 8 presses pinned, cap 20) | script | `bash build/tests/check-public-routes-test.sh` | ⬜ pending |
| 08-04-1 (wave 4, alone with the docs plan) | — (folded todo) | Live look tests wait for a heard push | browser | 10-run loop of `BT --filter-class "Cabinet.BrowserTests.LiveLookTests"` under a one-core quota, 0 of 10 failures | ⬜ pending |
| 08-04-2 | — (folded todo) | Card redraw and sample-400 waits | browser | 10-run loops of `"...CardSyncTests"` and `"...DefaultLookIdentityTests"`, 0 of 10 each | ⬜ pending |
| 08-05-1 | SEC-02 | Per-address live cap both transports (incl. committed default: 20 admitted, 21st refused, other address admitted); negotiate window; SSE pings not counted; upgraded cap | integration | `IT --filter-class "Cabinet.IntegrationTests.LiveAddressCapTests"` | ⬜ pending |
| 08-05-2 | SEC-02 | Origin guard on negotiate, WebSocket, SSE, both ports | integration + unit | `IT --filter-class "Cabinet.IntegrationTests.LiveOriginTests"` | ⬜ pending |
| 08-05-3 | SEC-02 | Refused page stays usable and quiet | browser | `BT --filter-class "Cabinet.BrowserTests.LiveRefusalTests"` | ⬜ pending |
| 08-06-1 | SEC-02, SEC-04 | Outer wall on the public router only, explicit source criteria | script | `bash deploy/tests/render-templates-test.sh` | ⬜ pending |
| 08-06-2 | SEC-03, SEC-04 | HSTS-only headers; `AllowedHosts` with loopback | script | `bash deploy/tests/render-templates-test.sh` | ⬜ pending |
| 08-07-1 | SEC-04 | Foreign host 400; listed host and loopback health 200 | integration | `IT --filter-class "Cabinet.IntegrationTests.HostFilteringTests"` | ⬜ pending |
| 08-07-2 | SEC-06 | Kestrel limits, header timeout, connection ceiling per listener (ops health answers at the public ceiling), upload handlers carry a size limit, runtime report; owner uploads still work | integration + unit | `IT --filter-class "Cabinet.IntegrationTests.KestrelLimitsTests"`, `"...OwnerRestoreTests"` | ⬜ pending |
| 08-08-1 | SEC-03 | Full header set once on public responses | integration | `IT --filter-class "Cabinet.IntegrationTests.SecurityHeadersTests"` | ⬜ pending |
| 08-08-2 | SEC-03 | Art, refusals, owner and ops listeners, exclusions | integration | `IT --filter-class "Cabinet.IntegrationTests.SecurityHeadersTests"` | ⬜ pending |
| 08-08-3 | SEC-03 | Clean console in Chromium and WebKit; credit in every state | browser | `BT --filter-class "...HeaderConsoleTests"`, `"...CreditVisibilityTests"`, WebKit with `CABINET_WEBKIT=1` | ⬜ pending |
| 08-09-1..3 | SEC-03 | Card closes, URL retries, all slices green | browser | 10-run loops per class (0 of 10), then the five CI slices, solo, also under DejaVu | ⬜ pending |
| 08-10-1..2 | SEC-01, SEC-06 | Generic checklist and docs, no planning refs or personal data | lint | `bash build/lint/checks/10-repo-rules.sh && bash build/lint/checks/40-secrets.sh` | ⬜ pending |
| 08-11, 08-18 | all | Release PR checks, verified draft | CI + script | `gh release view <tag> --json assets` (3 assets), checksum, attestation | ⬜ pending |
| 08-12, 08-19 | SEC-04, SEC-06 | Installed, selfcheck all pass, outbound proven, LAN route check | server + script | `ssh cabinet-lxc sudo -n cabinet-selfcheck`, `build/check-public-routes.sh --from-lan --burst` | ⬜ pending |
| 08-13 | SEC-02, SEC-03 | First visit and live cost measured; owner's go on the exact proxy diff; outer wall live, burst 429 | script + manual | `build/check-public-routes.sh --from-lan --burst` | ⬜ pending |
| 08-14 | SEC-06 | Rebuild and sync peaks, owner data untouched | server | health `missingArt` 0, checksums unchanged | ⬜ pending |
| 08-15, 08-20 | SEC-06, SEC-04 | Name resolution local on the proxy, public hostname as Host on the direct flood (zero 400s), status split and generator CPU per step, held-streams A2/A3, verdict table (5 rows per flood, checked mechanically; any FAIL fails 08-20), recovery, tool removed | server + manual | `ssh traefik-lxc "! command -v hey"`, health, route check, the awk verdict check in each plan's verify; owner reads host graph | ⬜ pending |
| 08-16, 08-17 | SEC-02, SEC-06 | Decided values in the repository | unit + script | `UT`, `bash deploy/tests/sandboxing-test.sh` | ⬜ pending |
| 08-21 | all | Checklist complete, BGG terms, proxy SSH, risks re-accepted | manual + lint | `build/scan-history.sh`, `build/lint.sh`, route check | ⬜ pending |
| 08-22, 08-23 | SEC-01, SEC-04 | Switch on the public router only; SSL Labs/Observatory/securityheaders by Claude; phone probe (close again on failure); real client address | manual + script | `build/check-public-routes.sh --from-lan`; owner's phone on mobile data | ⬜ pending |
| 08-24 | SEC-01 | Maintenance keys locked, verified without a passphrase | script + manual | `ssh-keygen -y -P ''` refuses each key; `ssh-add -l` lists neither; batch-mode SSH fails | ⬜ pending |

Requirement-level map from research:

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

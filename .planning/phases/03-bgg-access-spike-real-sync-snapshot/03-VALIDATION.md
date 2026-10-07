---
phase: 3
slug: bgg-access-spike-real-sync-snapshot
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: validated
nyquist_compliant: true
wave_0_complete: true
created: 2026-10-06
validated: 2026-10-07
---

# Phase 3 — Validation Strategy

> Per-phase validation contract for feedback sampling during execution. Derived from the `## Validation Architecture` section of `03-RESEARCH.md`.

---

## Test Infrastructure

| Property | Value |
|----------|-------|
| **Framework** | xunit.v3 4.0.1 on Microsoft.Testing.Platform, FluentAssertions 8.11.0, Mvc.Testing 10.0.12; add `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 (unit + integration) and `Microsoft.AspNetCore.SignalR.Client` 10.0.12 (integration only) |
| **Config file** | `global.json` (`"test": { "runner": "Microsoft.Testing.Platform" }`), `Directory.Build.props` |
| **Quick run command** | `dotnet test --project Cabinet.UnitTests/Cabinet.UnitTests.csproj --no-restore --filter-trait "Category=Sync"` (also `Bgg`, `Snapshot`, `Layout`, `Configuration`) |
| **Full suite command** | `dotnet test --solution Cabinet.slnx --no-restore && build/lint.sh` |
| **Estimated runtime** | ~30–60 s unit, ~1–2 min integration |

Notes: trait filters make `dotnet test --solution` exit 8 when a project has no match, so filter per project with `--project`. Re-record goldens locally only with `CABINET_UPDATE_GOLDENS=1`.

---

## Sampling Rate

- **After every task commit:** the matching quick command (category trait) plus `build/lint.sh repo-rules` when strings, comments or JS changed
- **After every plan wave:** `dotnet test --solution Cabinet.slnx --no-restore` and `build/lint.sh`
- **Before `/gsd-verify-work`:** full suite and lint green, CI green on the pull request, spike outcome signed off by the owner, browser rounds reviewed
- **Max feedback latency:** ~60 seconds (quick command)

---

## Per-Task Verification Map

Task IDs are filled in by the planner and executor; the requirement-level map below is the contract each task row must trace to.

| Req ID | Behavior | Test Type | Automated Command | File Exists | Status |
|--------|----------|-----------|-------------------|-------------|--------|
| SYNC-01 | Start-up run after jitter only when snapshot missing/old; hourly tick enqueues one run; two collection calls with expected query strings; ≥5 s between requests (fake clock) | unit + stub handler | quick, `Category=Sync`, `Category=Bgg` | ✅ `SyncSchedulerTests`, `RequestPacerTests`, `QueuedAnswerTests`, `BggRetryTests` | ✅ green |
| SYNC-01 | End-to-end sync of a synthetic collection (base + expansion, duplicate collid, non-owned filtered) through the real handler chain against the fake host | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj --no-restore` | ✅ `SyncPipelineTests`, `CollectionFidelityTests`, `BackgroundSyncTests`, `FakeBggServerTests` | ✅ green |
| SYNC-01 | Storage directory order (configured, then service manager, then Development only), start-up failure naming the key; snapshot load, corrupt and newer-schema files set aside | unit + integration | `Category=Sync`, `Category=Snapshot` | ✅ `StorageLocationTests` (added by audit), `SnapshotStoreTests`, `SyncPipelineTests` | ✅ green |
| SYNC-02 | One manual sync per window, persisted across restart; press while running → `running`; cooldown refusal → 429 + `Retry-After` in whole seconds rounded up; failed manual sync consumes the window | unit + integration | quick + integration | ✅ `SyncCoordinatorTests`, `SyncNowTests` (rounding cases added by audit), `RejectedTokenBackoffTests` | ✅ green |
| SYNC-02 | Button states, countdown and accessible name, no native disable, no request while not pressable, one sentence per own-press outcome (changed, unchanged, failed, held back, running, cooldown, offline), note cleared at window end, tick only while visible | node | `node --test build/tests/page-scripts.test.mjs` | ✅ `page-scripts.test.mjs` (button and outcome cases added by audit) | ✅ green |
| SYNC-03 | `lastSyncedUtc` moves only on success; first-paint `<time datetime>` and relative text match the shared case table | unit + integration + node | `Category=Sync`, node | ✅ `SyncStatusTextTests`, `SyncStatusLineTests`, `StatusEndpointTests`, `page-scripts.test.mjs` | ✅ green |
| SYNC-04 | Every failure category leaves snapshot, in-memory state and layout ETag unchanged; empty held back (even twice); halved held back then accepted on same set; small removal accepted; stale rule at 3 h and while held back; a run cancelled without a stop is a timeout; an unexpected exception is recorded by type only | unit + integration | `Category=Sync`, `Category=Bgg`, `Category=Snapshot` | ✅ `BggFailureTests`, `BggDeclaredTotalTests`, `ShrinkGuardTests`, `SyncFailureTests`, `HeldBackTests`, `SyncWorkerTests` (cases added by audit) | ✅ green |
| SYNC-05 | Fresh host: `/` 200 with "being filled" block, layout 200 with one bare section, `/health` healthy; block gone after first success (quiet in-place redraw removes it); zero-game first sync shows no block | integration + node | integration, node | ✅ `CabinetPageTests`, `LayoutEndpointTests`, `HealthEndpointTests`, `HeldBackTests`, `page-scripts.test.mjs` (redraw cases added by audit) | ✅ green |
| SYNC-08 | Every Razor page renders `a.bgg-credit` → `https://boardgamegeek.com` with logo `img`; logo served from own origin with image content type; no third-party origin | integration | integration | ✅ `CreditTests`, `ContentSecurityPolicyTests` | ✅ green |
| LOC-02 | Parser reads `privateinfo`/`inventorylocation` when present, `null` when absent; `showprivate=1` only when configured; outcome document exists, shape only, passes repo lint | unit + lint | `Category=Bgg`; `build/lint.sh repo-rules`; `bash build/tests/bgg-access-check-test.sh` | ✅ `BggCollectionParserTests`, `CollectionFidelityTests`, `bgg-access-check-test.sh` (outcome doc: manual sign-off, done) | ✅ green |
| SEC-05 | Sentinel token/username never in any public response, header, log, status or hub payload; token only to host `boardgamegeek.com`, redirects not followed; visitor input cannot change username; visitor GETs cause zero BGG calls; `Bgg:BaseUri` ignored outside Development; committed config holds no BGG account and the env example only placeholders; shipped projects never reference the fake BGG | unit + integration + e2e | `Category=Bgg`, `Category=Configuration`, `Category=Secrets`, `CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh` | ✅ `SecretsStayServerSideTests`, `BggTransportTests`, `BggSettingsTests`, `CommittedConfigurationTests` and `ShippedProjectTests` (added by audit), package e2e zip check (added by audit) | ✅ green |
| Live updates | Hub delivers status changes, rejects every invocation, enforces the connection cap, WebSockets/SSE only, small buffers and message size, short timeouts, detailed errors off, Kestrel upgraded-connection cap; vendored client matches pinned SHA-256 and is served as JavaScript; CSP unchanged | integration + unit + node | integration `Category=Live`, unit `Category=Configuration`, node | ✅ `LiveHubTests` (options case added by audit), `LiveLimitsTests` (added by audit), `HubLiveNotifierTests`, `LivePageTests`, `VendoredAssetTests`, `ContentSecurityPolicyTests`, `page-scripts.test.mjs` | ✅ green |
| Layout data | `entryId` on every placement, `isExpansion` flag; two copies → two placements; goldens at the new layout version; renderer writes `data-entry-id` and names unknown-base expansions "{title}, expansion" | unit (goldens) + integration + node | `Category=Layout`, node | ✅ `PlacementIdentityTests`, `LayoutGoldenTests`, `page-scripts.test.mjs` (renderer cases added by audit) | ✅ green |
| Dev switcher | Production ignores `Prototype:Enabled=true`; default config off; Development file on; `?sample=` never echoed | unit + integration | `Category=Configuration`, integration | ✅ `SampleCatalogTests`, `CabinetPageTests`, `LayoutEndpointTests` | ✅ green |
| Cross-cutting | No `style=` or inline script bodies; JS comment lint passes with vendored exclusion; planning-reference lint passes | integration + lint | integration, `build/lint.sh` | ✅ `CabinetPageTests`, `build/lint/checks/10-repo-rules.sh` self-tests | ✅ green |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

### Plan-task map (filled by the planner)

| Task | Requirement(s) | Automated verify | Notes |
|------|----------------|------------------|-------|
| 03-01 T1 | LOC-02, SEC-05 | `bash build/tests/bgg-access-check-test.sh && build/lint.sh repo-rules && build/lint.sh shell` | shape-only self-test |
| 03-01 T2 | LOC-02 | checkpoint (owner prerequisites + run approval) | blocking-human |
| 03-02 T1 | LOC-02 | outcome headings present, no `http` or XML tags | live run over SSH |
| 03-02 T2 | LOC-02 | checkpoint (owner sign-off) | one-way: public commit |
| 03-02 T3 | LOC-02 | sign-off line present and outcome committed | |
| 03-03 T1 | SYNC-01 | `dotnet test --project Cabinet.UnitTests/... --filter-class "*PlacementIdentityTests"` + `node --check` | |
| 03-03 T2 | SYNC-01 | `dotnet test --project Cabinet.UnitTests/... --filter-trait "Category=Layout"` | goldens at version 9 |
| 03-04 T1 | SYNC-08 | `dotnet test --project Cabinet.IntegrationTests/...` | every page renders the credit |
| 03-04 T2 | SYNC-08 | checkpoint (owner supplies the official logo) | blocking-human |
| 03-04 T3 | SYNC-08 | `dotnet test --project Cabinet.IntegrationTests/...` | human-check: legibility |
| 03-05 T1 | SYNC-05 | `dotnet test --project Cabinet.IntegrationTests/...` | being-filled state |
| 03-05 T2 | SYNC-05 | `Category=Configuration` unit + integration | prototype dev-only |
| 03-06 T1 | SYNC-01, SEC-05 | `dotnet restore --locked-mode` + integration `Category=FakeBgg` | fake BGG |
| 03-06 T2 | SYNC-01 | unit `Category=FakeBgg` + lint | scripted handler |
| 03-07 T1 | SYNC-01, SYNC-02, SEC-05 | build + integration `Category=Sync` | tracer |
| 03-07 T2 | SYNC-01 | unit `Snapshot`, `Bgg`; integration `Sync`; locked restore | restart, corrupt files |
| 03-08 T1 | SYNC-02, SYNC-03 | integration `Category=Sync` | cooldown, status |
| 03-08 T2 | SYNC-01 | unit + integration `Category=Sync` | scheduler |
| 03-08 T3 | SYNC-02 | unit `Sync`, `Snapshot` + lint | edges |
| 03-09 T1 | SYNC-01, LOC-02 | unit `Bgg`, `Snapshot`; integration `Sync` | fidelity |
| 03-09 T2 | SEC-05 | unit `Bgg`, `Configuration`; integration `Secrets` | sentinel leaks |
| 03-10 T1 | SYNC-04 | unit `Bgg`; integration `Sync` | failure classes |
| 03-10 T2 | SYNC-04 | unit `Bgg` | 202 polling with fake clock |
| 03-10 T3 | SYNC-04, SYNC-01 | unit + integration `Sync` + lint | shrink guard |
| 03-11 T1 | SYNC-03, SYNC-04, SYNC-05 | `node --test build/tests/page-scripts.test.mjs` + unit/integration `Sync` + lint | status line |
| 03-11 T2 | SYNC-03 | `node --test` + integration `Sync` | edges |
| 03-12 T1 | SYNC-02, SYNC-05 | `node --check` + integration `Sync` + lint | press flow |
| 03-12 T2 | SYNC-02 | `node --test build/tests/page-scripts.test.mjs` | copy and countdown edges |
| 03-13 T1 | SYNC-03, SEC-05 | locked restore + integration `Category=Live` | hub |
| 03-13 T2 | SEC-05 | integration `Live`, unit `Configuration`, full integration | no-invoke, cap |
| 03-14 T1 | SYNC-03 | `node --test` + `node --check` + integration `Live` + lint | fallback path |
| 03-14 T2 | SYNC-03 | checkpoint (owner approves downloads) | blocking-human |
| 03-14 T3 | SYNC-03, SYNC-02 | unit `Configuration` + full integration + `build/lint.sh` | three-engine CSP, screenshots |
| 03-15 T1 | all | draft release exists | PR, tag, attestation |
| 03-15 T2 | all | checkpoint (owner publishes) | blocking-human |
| 03-15 T3 | all | release verify + selfcheck + deployed status | human-check: real collection |

---

## Wave 0 Requirements

- [x] `Cabinet.UnitTests`: add `Microsoft.Extensions.TimeProvider.Testing`; new test folders `Bgg/`, `Sync/`, `Snapshot/`, `Collection/` (guard, cooldown, mapper, box mapping, relative-time case fixture)
- [x] `Cabinet.IntegrationTests`: add `TimeProvider.Testing` and `SignalR.Client`; extend the factory (background sync off by default, service-injection overload, unique state directory per factory)
- [x] Shared test support: scripted HTTP handler, synthetic BGG XML builder, sync harness, wait-until helper
- [x] `Cabinet.FakeBgg` project + `Cabinet.slnx` entry + committed `packages.lock.json`
- [x] Updated `packages.lock.json` for every project whose references change
- [x] Re-recorded layout goldens and bumped layout version
- [x] Spike script (and, manually, the signed-off outcome file)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Location spike with the real token from the container | LOC-02 | Needs the real token and owner approval; output must stay out of the public repo | Owner sets locations on 2–3 games; Claude runs the spike over SSH after approval; owner signs off the shape-only outcome under `.planning/` |
| Live redraw, countdown, button states, no CSP violation in Chromium/Firefox/WebKit | SYNC-02, SYNC-03 | Real browser engines; scratch Playwright kept outside the repo | Scratch Playwright rounds against the fake BGG plus one look on the owner's phone |
| Credit legibility and no layout shift at 320/390/1440 px; header stacking with the dev switcher | SYNC-08 | Visual judgement (UI-SPEC backstops) | Screenshot rounds reviewed by the owner |
| Real collection appears in the deployed cabinet | SYNC-01 | Needs the deployed release and the owner's BGG account | Owner marks a game owned on BGG, then checks the site after a manual sync or within the hour |
| The whole-run limit is 10 minutes | SYNC-04 | `SyncWorker` arms the limit with `CancellationTokenSource.CancelAfter` on the system clock, so the duration cannot be driven by a fake clock without a production change (arm it through the injected `TimeProvider`). What happens when the limit fires (recorded as a timeout, worker frees up, next request runs) is automated in `SyncWorkerTests` | Code review: `RunLimit` in `Cabinet.Service/Sync/SyncWorker.cs` is `TimeSpan.FromMinutes(10)` |

---

## Validation Sign-Off

- [x] All tasks have `<automated>` verify or Wave 0 dependencies
- [x] Sampling continuity: no 3 consecutive tasks without automated verify
- [x] Wave 0 covers all MISSING references
- [x] No watch-mode flags
- [x] Feedback latency < 60s
- [x] `nyquist_compliant: true` set in frontmatter

**Approval:** validated 2026-10-07 (Nyquist audit)

---

## Validation Audit 2026-10-07

| Metric | Count |
|--------|-------|
| Gaps found | 9 |
| Resolved | 9 |
| Escalated | 1 (residual of one gap: the 10-minute run-limit duration, see Manual-Only) |

Baseline before the audit: 877 .NET tests and 47 page-script tests green, lint green. After: 906 .NET tests, 65 page-script tests, lint green, package end-to-end test green. Tests committed in `c7cae25`.

| # | Gap | Requirement | Type | Resolution |
|---|-----|-------------|------|------------|
| 1 | Storage directory resolution order and start-up failure had no test | SYNC-01 | unit | `Cabinet.UnitTests/Sync/StorageLocationTests.cs` |
| 2 | Committed config could carry the BGG username or contact address unnoticed; env example not checked | SEC-05 | unit | `CommittedConfigurationTests` (account keys, env example placeholders and documentation addresses) |
| 3 | Nothing proved the fake BGG never ships | SYNC-01, SEC-05 | unit + e2e | `Cabinet.UnitTests/Configuration/ShippedProjectTests.cs`; zip listing check in `build/tests/package-release-e2e-test.sh` |
| 4 | Run cancelled without a stop and unexpected exception in a run were untested | SYNC-04 | unit | `SyncWorkerTests` (timeout and type-only log, next request still runs); duration escalated |
| 5 | Retry-After rounding only tested at the full window | SYNC-02 | integration | `SyncNowTests` theory (0, 10.4, 195, 599, 599.6 s into the window, raw header) |
| 6 | Hub message size, timeouts, detailed errors, Kestrel cap and connection buffers untested | SEC-05 | unit + integration | `Cabinet.UnitTests/Live/LiveLimitsTests.cs`; `LiveHubTests` connection options case |
| 7 | Renderer `data-entry-id` and unknown-base expansion names only checked by eye | SYNC-01 | node | `page-scripts.test.mjs` renderer cases on a fake DOM |
| 8 | Button states, no request while not pressable, running/cooldown/held-back/Retry-After sentences, name-change and tick rules untested | SYNC-02 | node | `page-scripts.test.mjs` sync button cases |
| 9 | Quiet in-place redraw (one swap, being-filled block removed, focus kept by entry, silent failure, hidden tab) only checked in browser rounds | SYNC-05, SYNC-03 | node | `page-scripts.test.mjs` cabinet page cases (fresh module per test) |

The new JavaScript cases were checked against deliberate faults in mutated copies of the page scripts in a scratch folder (entry id not written, expansion name dropped, name re-set every tick, tick ignoring visibility, note not cleared, press posting during the window, Retry-After ignored, loading line during a redraw, no focus restore, being-filled block kept, no visibility wait, error shown on a failed redraw, no blur); each fault turned at least one new case red. The C# cases were not mutation-run (production code stays untouched in this checkout); their inputs are chosen to discriminate, for example the 10.4 s Retry-After case, where floor rounding would answer 589 instead of 590.

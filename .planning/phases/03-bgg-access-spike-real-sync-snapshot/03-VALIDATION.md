---
phase: 3
slug: bgg-access-spike-real-sync-snapshot
# status lifecycle: draft (seeded by plan-phase) → validated (set by validate-phase §6)
# audit-milestone §5.5 distinguishes NOT-VALIDATED (draft) from PARTIAL (validated + nyquist_compliant: false) (#2117)
status: draft
nyquist_compliant: false
wave_0_complete: false
created: 2026-10-06
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
| SYNC-01 | Start-up run after jitter only when snapshot missing/old; hourly tick enqueues one run; two collection calls with expected query strings; ≥5 s between requests (fake clock) | unit + stub handler | quick, `Category=Sync` | ❌ W0 | ⬜ pending |
| SYNC-01 | End-to-end sync of a synthetic collection (base + expansion, duplicate collid, non-owned filtered) through the real handler chain against the fake host | integration | `dotnet test --project Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj --no-restore` | ❌ W0 | ⬜ pending |
| SYNC-02 | One manual sync per window, persisted across restart; press while running → `running`; cooldown refusal → 429 + `Retry-After`; failed manual sync consumes the window | unit + integration | quick + integration | ❌ W0 | ⬜ pending |
| SYNC-03 | `lastSyncedUtc` moves only on success; first-paint `<time datetime>` and relative text match the shared case table | unit + integration | `Category=Sync` | ❌ W0 | ⬜ pending |
| SYNC-04 | Every failure category leaves snapshot, in-memory state and layout ETag unchanged; empty held back (even twice); halved held back then accepted on same set; small removal accepted; stale rule at 3 h and while held back | unit + integration | `Category=Sync`, `Category=Snapshot` | ❌ W0 | ⬜ pending |
| SYNC-05 | Fresh host: `/` 200 with "being filled" block, layout 200 with one bare section, `/health` healthy; block gone after first success; zero-game first sync shows no block | integration | integration | ❌ W0 | ⬜ pending |
| SYNC-08 | Every Razor page renders `a.bgg-credit` → `https://boardgamegeek.com` with logo `img`; logo served from own origin with image content type; no third-party origin | integration | integration | ❌ W0 | ⬜ pending |
| LOC-02 | Parser reads `privateinfo`/`inventorylocation` when present, `null` when absent; `showprivate=1` only when configured; outcome document exists, shape only, passes repo lint | unit + lint | `Category=Bgg`; `build/lint.sh repo-rules` | ❌ W0 (outcome doc: manual sign-off) | ⬜ pending |
| SEC-05 | Sentinel token/username never in any public response, header, log, status or hub payload; token only to host `boardgamegeek.com`, redirects not followed; visitor input cannot change username; visitor GETs cause zero BGG calls; `Bgg:BaseUri` ignored outside Development | unit + integration | `Category=Bgg`, `Category=Configuration`, integration | ❌ W0 (config test exists) | ⬜ pending |
| Live updates | Hub delivers status changes, rejects every invocation, enforces the connection cap, WebSockets/SSE only; vendored client matches pinned SHA-256 and is served as JavaScript; CSP unchanged | integration + unit | integration | ❌ W0 | ⬜ pending |
| Layout data | `entryId` on every placement, `isExpansion` flag; two copies → two placements; goldens at the new layout version | unit (goldens) + integration | `Category=Layout` | partial (re-record) | ⬜ pending |
| Dev switcher | Production ignores `Prototype:Enabled=true`; default config off; Development file on; `?sample=` never echoed | unit + integration | `Category=Configuration`, integration | partial | ⬜ pending |
| Cross-cutting | No `style=` or inline script bodies; JS comment lint passes with vendored exclusion; planning-reference lint passes | integration + lint | integration, `build/lint.sh` | exists, extend | ⬜ pending |

*Status: ⬜ pending · ✅ green · ❌ red · ⚠️ flaky*

---

## Wave 0 Requirements

- [ ] `Cabinet.UnitTests`: add `Microsoft.Extensions.TimeProvider.Testing`; new test folders `Bgg/`, `Sync/`, `Snapshot/`, `Collection/` (guard, cooldown, mapper, box mapping, relative-time case fixture)
- [ ] `Cabinet.IntegrationTests`: add `TimeProvider.Testing` and `SignalR.Client`; extend the factory (background sync off by default, service-injection overload, unique state directory per factory)
- [ ] Shared test support: scripted HTTP handler, synthetic BGG XML builder, sync harness, wait-until helper
- [ ] `Cabinet.FakeBgg` project + `Cabinet.slnx` entry + committed `packages.lock.json`
- [ ] Updated `packages.lock.json` for every project whose references change
- [ ] Re-recorded layout goldens and bumped layout version
- [ ] Spike script (and, manually, the signed-off outcome file)

---

## Manual-Only Verifications

| Behavior | Requirement | Why Manual | Test Instructions |
|----------|-------------|------------|-------------------|
| Location spike with the real token from the container | LOC-02 | Needs the real token and owner approval; output must stay out of the public repo | Owner sets locations on 2–3 games; Claude runs the spike over SSH after approval; owner signs off the shape-only outcome under `.planning/` |
| Live redraw, countdown, button states, no CSP violation in Chromium/Firefox/WebKit | SYNC-02, SYNC-03 | Real browser engines; scratch Playwright kept outside the repo | Scratch Playwright rounds against the fake BGG plus one look on the owner's phone |
| Credit legibility and no layout shift at 320/390/1440 px; header stacking with the dev switcher | SYNC-08 | Visual judgement (UI-SPEC backstops) | Screenshot rounds reviewed by the owner |
| Real collection appears in the deployed cabinet | SYNC-01 | Needs the deployed release and the owner's BGG account | Owner marks a game owned on BGG, then checks the site after a manual sync or within the hour |

---

## Validation Sign-Off

- [ ] All tasks have `<automated>` verify or Wave 0 dependencies
- [ ] Sampling continuity: no 3 consecutive tasks without automated verify
- [ ] Wave 0 covers all MISSING references
- [ ] No watch-mode flags
- [ ] Feedback latency < 60s
- [ ] `nyquist_compliant: true` set in frontmatter

**Approval:** pending

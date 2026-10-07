---
phase: 03-bgg-access-spike-real-sync-snapshot
verified: 2026-10-07T12:00:00Z
status: passed
score: 5/5 roadmap success criteria verified (plus 8/8 requirement IDs accounted for)
behavior_unverified: 0
overrides_applied: 0
re_verification: false
gaps: []
deferred:
  - truth: "BGG-provided storage locations carried in the synced data (second branch of success criterion 5)"
    addressed_in: "Phase 6 (owner location tools)"
    evidence: "Not applicable as a gap: the signed-off access-check outcome found private inventory locations NOT readable with the token, so the roadmap's own 'or it does not and Phase 6 builds the owner location tools' branch applies. LOC-03 and LOC-04 are mapped to Phase 6 in REQUIREMENTS.md."
advisories:
  - "Safari/WebKit strict-policy check of live updates not run (WebKit would not start in the scratch Playwright). Live push is an enhancement: the page falls back to status polling (60 s fallback, plus polling after a visitor's own press), so the goal does not depend on it."
  - "Reverse proxy passing WebSocket upgrades is unconfirmed. Same fallback applies. The owner's deployed check saw the phone update without reload, which shows live updates work end to end on the deployed route for at least that browser."
---

# Phase 3: BGG Access Spike, Real Sync and Snapshot Verification Report

**Phase Goal:** The owner's real BGG owned collection appears automatically in the deployed cabinet, kept fresh by a polite hourly sync and a guarded "sync now", and the site keeps working when BGG does not.
**Verified:** 2026-10-07
**Status:** passed
**Re-verification:** No, initial verification

## Goal Achievement

Verification was done against the code on the branch head (not the SUMMARY claims), by reading the sync, coordinator, client, store, page and script sources, by confirming each success criterion has behavioral test coverage, and by re-running the suites in this session.

### Observable Truths (roadmap success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | The owner's BGG owned collection (base games and expansions) syncs on its own about hourly and appears in the deployed cabinet by title with nothing entered in the app | VERIFIED | `SyncScheduler` (PeriodicTimer on `Sync:IntervalMinutes`, default 60, plus a jittered start-up sync when stale) calls `SyncCoordinator.TryRequest`; `SyncWorker` drains the channel and calls `SyncRunner.RunAsync`, which calls `BggClient.FetchOwnedAsync` (two calls: `excludesubtype=boardgameexpansion`, then `subtype=boardgameexpansion`, merged by collection id), saves via `SnapshotStore.Save` and swaps `CollectionStore.Replace`. `LayoutEndpoint` serves the layout from `CollectionStore.Current`. Tests: `SyncPipelineTests`, `BackgroundSyncTests`, `CollectionFidelityTests`. Deployed: v0.3.0 installed, health Healthy, first production sync result `changed` (03-15 evidence), and the owner approved the deployed check (real collection visible on desktop and phone). |
| 2 | Any visitor can press "sync now"; one sync per cooldown window across all visitors and across restarts; button shows time remaining; last-synced time visible; no visitor input can supply a username or cause other BGG calls; token and username only in server config | VERIFIED | `POST /cabinet/sync` (`SyncEndpoints.Handle`) takes no request input and answers 202 / 409 / 429 plus `Retry-After`. `SyncCoordinator.TryRequest` holds one global lock, writes `CooldownEndsUtc` to disk via `ISyncStateStore.Save` before queueing, and reloads it in the constructor. Tests: `SyncNowTests.The_window_survives_a_restart`, `A_second_press_inside_the_window_is_refused_with_the_remaining_seconds`, `A_press_with_a_query_and_a_body_asks_BGG_only_for_the_configured_username`, `SecretsStayServerSideTests` (no secret in any log or public response; token on every BGG request; www redirect not followed; visitors reading page/layout cause no BGG request). `BggAuthHandler` attaches the token only to HTTPS `boardgamegeek.com`; primary handler has `AllowAutoRedirect = false`; `BggOptions.ToString()` names the type only. `Bgg:BaseUri` is honoured only in Development. Countdown and relative/exact time: `sync.js`, `status.js`, `Index.cshtml` (`data-cooldown-ends`, `data-last-synced`); 33 node tests cover the page scripts. Owner confirmed the countdown during the shared window on the deployed site. |
| 3 | When BGG is down, throttling, erroring, or returns an empty or shrunken result, visitors keep the last good collection with a "showing last sync from ..." note; the cabinet is never wiped | VERIFIED | `BggClient` classifies 401 (never retried), 429/503 (`Throttled`), other non-200 (`Unavailable`), non-XML (`BadAnswer`), declared-total mismatch (`BadAnswer`), 202 bounded poll (`Queued`), timeouts; any failure returns `CollectionFetchResult.Failed` and never a partial list. `SyncRunner` stores and replaces only on `Fetched` and a guard accept. `ShrinkGuard` holds back an empty answer over shown games always, and a loss of more than half until the next fetch returns the identical entry set. Stored write is atomic temp-file-plus-rename (`AtomicJsonFile`). `SyncStatusText.StaleRecent` / `StaleHeldBack` produce "Showing the last sync from ... Recent syncs haven't gone through." and the held-back variant; mirrored in `copy.js`. Tests: `SyncFailureTests` (layout and stored collection unchanged after failure; failure before any good sync is not an empty collection; next press recovers), `HeldBackTests` (six cases including survival across a restart), `BggFailureTests`, `QueuedAnswerTests`. |
| 4 | Before the first successful sync visitors see an intentional "cabinet is being filled" state, and every public page carries the linked "Powered by BGG" credit | VERIFIED | `Index.cshtml` renders `cabinet-filling` ("The cabinet is being filled.") when `!HasSynced`; startup without BGG keys warns and stays healthy (`SecretsStayServerSideTests.Without_a_token_...`). `_Layout.cshtml` footer holds `<a class="bgg-credit" href="https://boardgamegeek.com"><img src="~/img/powered-by-bgg.svg" alt="Powered by BGG">`; the logo file exists in `wwwroot/img`. Tests: `CabinetPageTests.Default_page_shows_the_being_filled_message_...`, `CreditTests.Every_razor_page_renders_the_linked_credit` (enumerates all Razor pages), logo served from own origin. |
| 5 | The location spike was run with the real token from the LXC and its outcome recorded | VERIFIED | `03-SPIKE-OUTCOME.md`: run 2026-10-06 from the deployed LXC, 13 of 14 allowed requests, exit 0, signed off by the owner. Measured: private info flag accepted but no private info element on 0 of 50 base items and 0 of 15 expansions, so locations are not readable with the token. `Bgg:IncludePrivateInfo` defaults to false and the parser reads no private attribute; owner location tools are the later phase's job, which is the roadmap's stated second branch. The check script is shape-only by construction (`build/bgg-access-check.py`, tested by `build/tests/bgg-access-check-test.sh`). |

**Score:** 5/5 truths verified, 0 present but behavior-unverified.

Behavior-dependent invariants (cooldown persisted across restarts, single-flight, last-good retained on failure, held-back confirmation survives restart) each have a named, passing test; none rests on symbol presence alone.

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Cabinet.Repository/Bgg/BggClient.cs` | Two-call paced client, 202 poll, failure categories | VERIFIED | Substantive (request budget 16, wait schedule 5/10/20/30/30/30 s, total check); registered through `AddHttpClient<ICollectionSource, BggClient>` |
| `Cabinet.Repository/Bgg/BggTransport.cs` | Token host guard, no redirects, honest User-Agent | VERIFIED | `BggAuthHandler` wired with `.AddHttpMessageHandler`; `CreatePrimaryHandler` wired with `.ConfigurePrimaryHttpMessageHandler` |
| `Cabinet.Repository/Bgg/RequestPacer.cs` | At least 5 s between requests | VERIFIED | Registered as `IRequestPacer`; gap floor enforced in `BggSettings` |
| `Cabinet.Service/Sync/SyncCoordinator.cs` | Single-flight, global persisted cooldown | VERIFIED | Wired into endpoint, worker, scheduler, status service |
| `Cabinet.Service/Sync/SyncWorker.cs`, `SyncScheduler.cs`, `SyncStartup.cs` | Hosted services | VERIFIED | All three registered with `AddHostedService` |
| `Cabinet.Domain/Collection/ShrinkGuard.cs` | Empty/shrunk protection | VERIFIED | Called from `SyncRunner` |
| `Cabinet.Repository/Storage/SnapshotStore.cs`, `SyncStateStore.cs`, `AtomicJsonFile.cs` | Atomic stored collection and bookkeeping | VERIFIED | Loaded at start-up by `SyncStartup` / `SyncCoordinator` |
| `Cabinet.Service/Sync/CabinetStatus.cs`, `SyncEndpoints.cs` | Public status and sync routes | VERIFIED | `/cabinet/status` and `/cabinet/sync` mapped in `Program.cs`; status exposes no failure category, count, username or token |
| `Cabinet.Service/Pages/Index.cshtml(.cs)`, `Shared/_Layout.cshtml` | Status line, notes, being-filled state, credit | VERIFIED | Server first paint matches client wording |
| `Cabinet.Service/wwwroot/js/sync.js`, `status.js`, `live.js`, `copy.js`, `cabinet.js` | Button states, countdown, live and fallback refresh | VERIFIED | 454 / 153 / 136 / 193 / 213 lines, loaded as modules by the page |
| `docs/bgg-sync.md`, `docs/bgg-access-check.md`, `deploy/cabinet.env.example` | Operator docs, placeholder-only env example | VERIFIED | Placeholders only |

### Key Link Verification

| From | To | Via | Status |
|------|----|-----|--------|
| `SyncScheduler` / `/cabinet/sync` | `SyncCoordinator.TryRequest` | direct call; same gate for both | WIRED |
| `SyncCoordinator` channel | `SyncWorker` | `Requests.ReadAllAsync` | WIRED |
| `SyncWorker` | `SyncRunner` -> `ICollectionSource` (`BggClient`) | `RunAsync(HeldBack)`; `Func<ICollectionSource>` resolves the typed client | WIRED |
| `SyncRunner` | `SnapshotStore.Save` then `CollectionStore.Replace` | after guard accept and version change | WIRED |
| `CollectionStore.Current` | `/cabinet/layout`, `Index` page | `LayoutFor`, `HasSynced`, `statusService.Current()` | WIRED |
| `SyncWorker` | `ILiveNotifier` -> hub -> `live.js` -> `sync.js` | start and end broadcasts; client redraw on version change | WIRED |
| `BggAuthHandler` | BGG API host only | scheme and host check, header cleared otherwise | WIRED |

### Data-Flow Trace (Level 4)

| Artifact | Data | Source | Real data | Status |
|----------|------|--------|-----------|--------|
| `/cabinet/layout` | cabinet items | `CollectionStore` filled from `SnapshotMapper` over parsed BGG XML | Yes (production first sync `changed`, owner saw the real collection) | FLOWING |
| Index status line | last synced, cooldown, held-back | `SyncStatusService` over persisted `SyncState` | Yes | FLOWING |

### Behavioral Spot-Checks

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Whole .NET suite | `dotnet test --solution Cabinet.slnx` | 807 passed, 0 failed, 0 skipped | PASS |
| Page scripts | `node --test build/tests/page-scripts.test.mjs` | 33 pass, 0 fail | PASS |
| Lint | `bash build/lint.sh` | repo-rules, workflows, shell, secrets, script-tests all PASS | PASS |
| Working tree after runs | `git status --short` | clean | PASS |

### Probe Execution

No phase-declared `probe-*.sh` scripts. The on-host `cabinet-selfcheck` (21/21 PASS) is recorded in the plan 15 summary; not re-run from here (needs the container).

### Requirements Coverage

Every ID in the roadmap's phase 3 requirement list appears in at least one plan's `requirements:` frontmatter, and REQUIREMENTS.md maps no other ID to Phase 3 (no orphans). SYNC-06 and SYNC-07 map to Phase 4, LOC-01 to Phase 7, and are correctly absent.

| Requirement | Source plans | Description | Status | Evidence |
|-------------|--------------|-------------|--------|----------|
| SYNC-01 | 03-03, 03-06, 03-07, 03-08, 03-09, 03-10, 03-15 | Owned collection (base games and expansions) syncs about hourly, nothing entered in the app | SATISFIED | Truth 1 |
| SYNC-02 | 03-07, 03-08, 03-12, 03-14, 03-15 | "Sync now" with one global, persisted cooldown and remaining-time display | SATISFIED | Truth 2 |
| SYNC-03 | 03-08, 03-11, 03-13, 03-14, 03-15 | Visitors see when the collection was last synced | SATISFIED | Truth 2: relative and exact time, first paint plus script; `SyncStatusLineTests`, `StatusEndpointTests` |
| SYNC-04 | 03-10, 03-11, 03-15 | Last good collection kept with a "showing last sync from" note on outage, error or suspicious result | SATISFIED | Truth 3 |
| SYNC-05 | 03-05, 03-11, 03-12, 03-15 | "Cabinet is being filled" state before the first sync | SATISFIED | Truth 4 |
| SYNC-08 | 03-04, 03-15 | Every public page credits BGG with the linked "Powered by BGG" logo | SATISFIED | Truth 4; `CreditTests` |
| LOC-02 | 03-01, 03-02, 03-09, 03-15 | Spike with the real token determines whether inventory location is exposed; outcome recorded | SATISFIED | Truth 5; `03-SPIKE-OUTCOME.md` (answer: not readable) |
| SEC-05 | 03-01, 03-06, 03-07, 03-09, 03-13, 03-15 | Username and token only in server config; visitors cannot supply a username or cause BGG calls beyond the cooldown-guarded sync | SATISFIED | Truth 2; `SecretsStayServerSideTests`, `SyncNowTests` |

REQUIREMENTS.md still shows these eight boxes unchecked and the traceability table "Pending"; that bookkeeping is for the orchestrator to update on phase close, it is not a code gap.

### Anti-Patterns Found

| Check | Result |
|-------|--------|
| `TBD` / `FIXME` / `XXX` in tracked files outside `.planning/` | None |
| `//` comments in any tracked `.cs` file | None |
| Planning references (requirement keys, phase or plan numbers, planning document names, decision IDs) outside `.planning/` and the project instructions | None |
| Personal data | The only hit is the public GitHub handle in the licence line and repository path defaults (`cryptic96/games-cabinet`), inherent to the public repository address and accepted by the owner earlier. No BGG username as a BGG username, no real host or IP, no personal email in the repo. `deploy/cabinet.env.example` holds placeholders only. |
| Stubs / hardcoded empty data on a rendered path | None found; empty initial state in `CollectionStore` is overwritten by `SyncStartup` and `SyncRunner`. |

### Code Review Findings (03-REVIEW.md: 0 critical, 16 warnings, 14 info)

None blocks the goal; the deployed check passed with all of them open. Those worth tracking:

- **WR-04** (`SnapshotStore.Load` and `SyncStateStore.Load` set the file aside on a transient read exception): the closest to the "never wiped" promise. It needs an I/O failure at the exact moment of start-up read, and the next successful sync repopulates, so it does not defeat the criterion, but it is the first warning to fix.
- **WR-02** (missing or unparseable `totalitems` is accepted; skipped malformed entries count toward the total): real BGG answers declared the total on every measured call, and the guard remains in force for the usual case. Documented as a deliberate trade in the review notes.
- **WR-01** (shutdown mid-sync is recorded as a timeout and counted as a failure) and **WR-03** (pacer uses the wall clock): robustness only.
- **WR-05, WR-07, WR-08** (button can stay on "Syncing..." until reload in rare timing, redraw guard timing, no timeout on the press request): UI edge cases; the owner's deployed check of the press flow passed.
- **IN-02** (a permanently rejected token is retried hourly): costs one request per hour, polite.

### Human Verification Required

None outstanding for the goal. The owner's deployed human check (real collection on desktop and phone, desktop sync updating the phone page without reload, countdown during the shared 10-minute window) was performed and approved on 2026-10-07 and is recorded here as passed.

Two owner-facing items remain open and are recorded as advisories, not as blockers, because the page degrades to status polling and the goal does not depend on live push:

1. Safari/WebKit with the strict content security policy (`default-src 'self'`): confirm live updates connect (older WebKit may not treat the same-origin WebSocket as `'self'`); if not, the 60 s polling fallback applies.
2. Reverse proxy passes WebSocket upgrades: confirm once in production; otherwise the same fallback applies.

### Gaps Summary

No gaps. All five roadmap success criteria are met in the code and backed by named passing tests; all eight phase requirement IDs are claimed by plans and satisfied; the deployed release is installed, healthy and has completed a real sync, with the owner's deployed check approved. The location spike's answer (not readable with the token) is the recorded outcome the criterion asks for, and the follow-on owner tools belong to a later phase.

---

_Verified: 2026-10-07_
_Verifier: Claude (gsd-verifier)_

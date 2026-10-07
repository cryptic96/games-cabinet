---
phase: 3
slug: bgg-access-spike-real-sync-snapshot
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
open_below_threshold: 0
asvs_level: 2
block_on: high
created: 2026-10-07
---

# Phase 3 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

Audited at HEAD `37e11fd` on the milestone branch. That head includes two rounds of code-review fixes that landed after the deployed release `v0.3.0` (commit `9c766f9`). Unless noted, line numbers are from committed HEAD and paths are relative to the repository root.

How the audit was run:
- Three `gsd-security-auditor` runs split the register by area: the access check, release and supply chain; the back-end sync; and the front end and public surface.
- Nothing was built or written in the repository.
- No BGG host was contacted, nothing was run over SSH, no env file was read and no release asset was downloaded.
- These offline checks were run and passed:
  - `python3 -I build/bgg-access-check.py --self-test`
  - `build/tests/bgg-access-check-test.sh` (17/17)
  - `build/lint/checks/10-repo-rules.sh`
  - `build/check-github-settings.sh` (13/13)
  - `node --test build/tests/page-scripts.test.mjs` (47/47)
  - a denylist scan (line numbers only) over every file changed since `v0.2.0`, plus the phase's commit and tag messages (0 matches)
- .NET tests were verified by reading them. The recorded full-suite runs are in 03-VERIFICATION and the review-fix reports.
- Server-side and GitHub-UI facts are marked "SUMMARY claim".

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| server env file -> app and access check | The app user reads the token and username on the container. They never pass through the workstation | BGG token, BGG username, contact URL (secrets) |
| server -> BGG XML API | Authenticated HTTPS to `boardgamegeek.com` only, paced and budgeted | Bearer token; username in the query |
| BGG answer -> snapshot, cabinet and printed report | Untrusted XML becomes stored data, page text and a printed shape summary | Untrusted titles, numbers, ids; private fields (not readable with the token) |
| visitor -> `POST /cabinet/sync`, `GET /cabinet/status` | Anonymous. The press accepts no input. The status is a public read | Nothing in; times, flags, version and a four-value result out |
| browser -> page and `/cabinet/layout` | `sample` and `profile` query values, `If-None-Match` | Untrusted short strings; only allowlisted names are acted on |
| internet -> `/cabinet/live` hub | Long-lived anonymous WebSocket or Server-Sent Events connections | No client messages accepted; the public status is pushed |
| layout JSON and status -> browser DOM | Titles, times and flags are written into the page | BGG titles (untrusted text), server times |
| visitor clock -> page | An untrusted local clock drives the relative times and countdowns | Client time, corrected by the server offset |
| state directory -> app at start-up | `snapshot.json` and `sync-state.json` are read back | Files the app wrote itself, treated as untrusted on read |
| third-party asset -> site | The BGG logo SVG and the vendored SignalR client are served to every visitor | Third-party files |
| NuGet and npm registries -> repository and scratch | Test-only NuGet packages; the SignalR client tarball; scratch Playwright | Packages, lock files |
| developer machine -> fake BGG | Loopback-only fake used in development and tests | Invented data |
| printed report and evidence -> public repository | Only the signed-off shape summary and sanitised counts and statuses cross | Shape summaries, counts |
| milestone branch -> main -> release -> container | Pull request, tag, deploy-environment approval, attested artefact pulled and verified by the container | Code, signed release zip, checksum, attestation bundle |

---

## Threat Register

Plan IDs collide: `T-03-SC` appears in 03-07, 03-08, 03-13 and 03-14 with different subjects, so the plan is given in brackets.

| Threat ID | Plan | Category | Component | Severity | Disposition | Mitigation (evidence) | Status |
|-----------|------|----------|-----------|----------|-------------|-----------------------|--------|
| T-03-01 | 03-01 | Information disclosure | access check report output | high | mitigate | `build/bgg-access-check.py:755-760`: the report is one string, `report_leaks` runs before the only `print`, and a leak exits 4. Guard at 220-230 covers `http`, `<`, credentials, username (`contains_name` 205-212), titles and locations. Sentinel self-test at 842-944. Stubbed real-run cases: `build/tests/bgg-access-check-test.sh:114-180`. Guard changed after the run (observation 3) | closed |
| T-03-02 | 03-01 | Information disclosure | access check token transport | high | mitigate | `bgg-access-check.py:662`: a literal `HTTPSConnection("boardgamegeek.com")` with a default TLS context (627). `http.client` never follows redirects; a 3xx is only classified (567-580). The token is attached only in `build_headers` (636-648). The wrong token is a fixed constant (51, 641-642). Test: `bgg-access-check-test.sh:205-210` | closed |
| T-03-03 | 03-01 | Denial of service | BGG quota (access check) | medium | mitigate | Limits at `bgg-access-check.py:46-48,56-58`; spacing at 650-656; cap at 682-686; 202 back-off at 691-697; stop rules at 713-722; self-test at 956-970. The run used 13 of 14 requests (03-SPIKE-OUTCOME). Owner approval of the run: SUMMARY claim | closed |
| T-03-04 | 03-01 | Tampering | hostile XML in a BGG answer (access check) | medium | mitigate | **Partly delivered.** The DOCTYPE/ENTITY refusal (`bgg-access-check.py:243-244`) is a byte search on the raw body. A UTF-16 or UTF-32 body gets past it, and `ET.fromstring` (246) parses it with internal entities expanded (shown by an offline probe). Residual impact is low: expat limits entity amplification, ElementTree resolves no external entities, expanded text reaches only counts and the leak guard, and the tool has already done its one approved run. See Open Items | closed — fixed after the audit (`161f2f8`): bodies with a UTF-16/32 byte-order mark or a NUL byte are refused before parsing, every other body goes through an expat parse whose doctype and entity handlers raise; self-test and script cases cover UTF-16/32 with and without a mark |
| T-03-05 | 03-01 | Information disclosure | access check docs page | low | mitigate | `docs/bgg-access-check.md:107,112,115` use the `<container>` placeholder; no IP, email or real domain. Repo-rules lint passes; the pre-commit denylist hook is active; denylist scan 0 | closed |
| T-03-06 | 03-02 | Information disclosure | committed outcome file | high | mitigate | `03-SPIKE-OUTCOME.md` has no `http` or markup and holds the dated sign-off (line 9). It was committed alone with a noreply author; denylist scan 0. Owner sign-off before commit: SUMMARY claim | closed |
| T-03-07 | 03-02 | Information disclosure | server env file | high | mitigate | The script reads the env file itself (`bgg-access-check.py:139-175`), and its argument parser (980-988) accepts no secret. Every committed `Bgg__*` line is a placeholder. That the executor never read the file: SUMMARY claim (03-01, 03-02, 03-15) | closed |
| T-03-08 | 03-02 | Denial of service | BGG quota (the run) | medium | mitigate | One run reached BGG; the earlier exit-3 attempt sent nothing (SUMMARY claim, consistent with 03-SPIKE-OUTCOME:5-8). Cap and stop rules as in T-03-03 | closed |
| T-03-09 | 03-02 | Repudiation | sign-off | low | mitigate | `03-SPIKE-OUTCOME.md:9` holds the dated owner sign-off; 03-02-SUMMARY records the answer | closed |
| T-03-10 | 03-03 | Tampering | `render.js` title and sub-line text | medium | mitigate | `wwwroot/js/render.js` writes text only via `textContent` (122, 180), `setAttribute('aria-label')` (174) and `.title` (175). Numbers go through `dataset` with `String()` (154-158, 214). No markup-building API anywhere in `wwwroot/js`; the strict CSP is a second layer. No lint enforcement (observation 13) | closed |
| T-03-11 | 03-03 | Information disclosure | `entryId` in layout JSON | low | accept | AR-03-01 | closed (accepted) |
| T-03-12 | 03-04 | Tampering | BGG logo asset | medium | mitigate | `wwwroot/img/powered-by-bgg.svg` holds only `svg`, `title`, `g`, `path` and `polygon` elements: no script, foreignObject, href, `url(`, doctype or entity. It is used only as `<img>` (`Pages/Shared/_Layout.cshtml:15`). The CSP is unchanged (`Hosting/ContentSecurityPolicy.cs:12-13`) and covers the SVG path (`ContentSecurityPolicyTests.cs:25`). Provenance not pinned (observation 12) | closed |
| T-03-13 | 03-04 | Information disclosure | visitor privacy (logo) | medium | mitigate | Served from the site's own origin (`_Layout.cshtml:15`). `CreditTests.cs:75-91` (own origin, `image/*`); `CreditTests.cs:104-117` fails on any foreign `src` or `href` other than the credit link | closed |
| T-03-14 | 03-04 | Spoofing | outbound credit link | low | mitigate | `href="https://boardgamegeek.com" rel="noopener"` (`_Layout.cshtml:15`), asserted at `CreditTests.cs:133-134` | closed |
| T-03-15 | 03-04 | Repudiation | licence compliance | low | mitigate | `CreditTests.cs:16-33,119-131` lists every Razor page endpoint from the endpoint data source and requires the credit on each, with the prototype on and off (62-73) | closed |
| T-03-16 | 03-05 | Information disclosure | prototype scaffolding on the deployed site | medium | mitigate | `Prototype/SampleCatalog.cs:111` enables only when the switch is on and the environment is not Production. The committed default is false (`appsettings.json:42-43`), and deployment runs Production. Tests: `SampleCatalogTests.cs:14-24,63-76` | closed |
| T-03-17 | 03-05 | Tampering | `sample` query value | medium | mitigate | Exact ordinal allowlist (`SampleCatalog.cs:47,55-65`). Only the resolved name is exposed (`Pages/Index.cshtml.cs:83-89`), and `data-sample` is rendered only for an honoured sample (`Index.cshtml:59-66`). The layout endpoint never echoes it. Tests: `CabinetPageTests.cs:226-241` (script-tag value), `LayoutEndpointTests.cs:122-140` | closed |
| T-03-18 | 03-05 | Denial of service | layout building | low | mitigate | Profile allowlist checked first (`Layout/LayoutEndpoint.cs:65-68`). One `Lazy` build per collection state and profile (`Collection/CollectionStore.cs:68-80`). Sample keys are allowlisted pairs (`LayoutCache.cs:29-35`). 304 at 78-89. Tests: `LayoutEndpointTests.cs:105-120,188-204` | closed |
| T-03-19 | 03-06 | Information disclosure | token reaching the fake | medium | mitigate | `Cabinet.FakeBgg/FakeBggServer.cs` never reads Authorization. The token is attached only for HTTPS `boardgamegeek.com` (`Cabinet.Repository/Bgg/BggTransport.cs:53-60`). Tests: `FakeBggServerTests.cs:265-286` (sentinel not echoed), `BggTransportTests.cs:13-28` | closed |
| T-03-20 | 03-06 | Elevation of privilege | fake shipped in a release | medium | mitigate | `Cabinet.Service.csproj:8-11` references only Domain and Repository; `git grep -i fakebgg` over shipped code, build, deploy and workflows finds nothing. `build/package-release.sh:57-64` publishes only the service. The zip check was one-off (observation 10) | closed |
| T-03-21 | 03-06 | Spoofing | fake reachable from the network | low | mitigate | Binds `http://127.0.0.1:{port}` only (`FakeBggServer.cs:36`). Ambient configuration sources are cleared (51-55). Non-loopback callers are refused on the scenario switch (101-107). Test: `FakeBggServerTests.cs:257-263` | closed |
| T-03-22 | 03-06 | Information disclosure | fixtures | medium | mitigate | `Cabinet.FakeBgg/SyntheticBggCollection.cs:37,40,95-104` and `bgg-access-check.py:764-801` hold invented data only. Denylist scan 0; gitleaks in `lint` passed on the phase pull request | closed |
| T-03-23 | 03-07 | Information disclosure | token on redirects or other hosts | high | mitigate | `BggTransport.cs:73-79`: `AllowAutoRedirect = false`, `UseCookies = false`. Auth handler at 49-63; both wired at `Sync/SyncEndpoints.cs:66-75`, the only production HttpClient. A 3xx ends as Unavailable (`BggClient.cs:200-201`). Tests: `BggTransportTests.cs:13-59`, `SecretsStayServerSideTests.cs:62-116` | closed |
| T-03-24 | 03-07 | Information disclosure | username in logs | high | mitigate | `appsettings.json:18` sets `System.Net.Http.HttpClient` to Warning, with no Production override. All 17 production log calls log a category, a count or an exception type name only. `BggOptions.ToString` names the type only (`BggTransport.cs:38`). Test: `SecretsStayServerSideTests.cs:29-60` with the committed filters applied. Wording drift and no level pin (observations 6, 7) | closed |
| T-03-25 | 03-07 | Tampering | visitor-chosen username, host or query | high | mitigate | `SyncEndpoints.cs:113-137`: the handler binds only DI services and uses `HttpContext` only for response headers. Base URI is the default outside Development (`Sync/BggSettings.cs:35`). Fixed query with an escaped configured username (`BggClient.cs:120`). Tests: `SecretsStayServerSideTests.cs:137-161`, `SyncNowTests.cs:115-130`, `BggSettingsTests.cs:19-60` | closed |
| T-03-26 | 03-07 | Tampering | hostile XML (DTD, entities, size) | high | mitigate | `Bgg/BggCollectionParser.cs:44-52`: DTD prohibited, null resolver, 20,000,000-character document limit, `MaxCharactersFromEntities = 0`; root must be `items` (57-60). Response buffer limit of 20,000,000 (`SyncEndpoints.cs:23,72`), applied via `ResponseContentRead` (`BggClient.cs:186`). Tests: `BggFailureTests.cs:33,137`, `BggCollectionParserTests.cs:202-211`. Size limits untested (observation 8) | closed |
| T-03-27 | 03-07 | Denial of service | repeated presses | medium | mitigate | `Sync/SyncCoordinator.cs:49-50`: bounded(1) `Wait` channel, single reader. One lock at 151-178; the only consumer is `SyncWorker.cs:30`. Tests: `SyncPipelineTests.cs:73-92`, `SyncCoordinatorTests.cs:151-163` | closed |
| T-03-28 | 03-07 | Tampering | partial or corrupt snapshot file | medium | mitigate | `Storage/AtomicJsonFile.cs:18-36`: same-directory temp, `CreateNew`, `Flush(true)`, rename with overwrite; strays cleaned (41-54, called at `SyncStartup.cs:39`). Malformed or newer files are set aside (`SnapshotStore.cs:66-90`). An unreadable file is left in place (48-52, review fix WR-04). The only write path is `SyncRunner.cs:87`. Tests: `SnapshotStoreTests.cs:52-131,154`. Double-fault residual: observation 4 | closed |
| T-03-29 | 03-08 | Denial of service | BGG traffic from presses | high | mitigate | `SyncCoordinator.cs:158-170`: every accepted start saves `LastStartedUtc`/`CooldownEndsUtc` before `TryWrite`; nothing is queued if the save throws. State loads at construction (77). The only `TryRequest` callers are `SyncEndpoints.cs:117` and `SyncScheduler.cs:38,50`. Tests: `SyncNowTests.cs:40-113` (429 with Retry-After, restart, failed run), `SyncCoordinatorTests.cs:32-43,136-186` | closed |
| T-03-30 | 03-08 | Denial of service | crash-loop restarts | medium | mitigate | `SyncScheduler.cs:35-39,59-68`: the start-up run happens only when the data is stale and the last start is at least 15 minutes old. The interval timer is armed after the start-up delay (41, review fix IN-04). Interval floor of 15 minutes (`SyncSettings.cs:19,49`). Tests: `SyncSchedulerTests.cs:43-137`, `SyncSettingsTests.cs:56` | closed |
| T-03-31 | 03-08 | Information disclosure | status payload | medium | mitigate | `Sync/CabinetStatus.cs:20-29`: nine fields (times, flags, version, stale-after, four-value result). `LastFailure` and `ConsecutiveFailures` are never read outside the coordinator. No-store; no CORS anywhere. Tests: `StatusEndpointTests.cs:51-81`, `SyncFailureTests.cs:51` | closed |
| T-03-32 | 03-08 | Tampering | corrupt or hostile `sync-state.json` | medium | mitigate | `Storage/SyncStateStore.cs:35-85`: missing or unreadable gives the initial state; a JSON error, null, schema below 1, a negative count or a newer schema is set aside and gives the initial state. The path comes from configuration or `STATE_DIRECTORY`, never from the file. Tests: `SyncStateStoreTests.cs:68-110`. Odd-but-valid values: observation 9 | closed |
| T-03-33 | 03-08 | Spoofing | cross-site POST to `/cabinet/sync` | low | accept | AR-03-02 | closed (accepted) |
| T-03-34 | 03-09 | Information disclosure | token on another host or after a redirect | high | mitigate | Same code as T-03-23. Look-alike host table at `BggTransportTests.cs:13-28` (www, suffix-appended, plain http, loopback). The 301 test goes through the production primary handler (`SecretsStayServerSideTests.cs:83-116`, review fix WR-10), and the second listener sees 0 requests. Also `SecretsStayServerSideTests.cs:118-135`, `SyncFailureTests.cs:136-139` | closed |
| T-03-35 | 03-09 | Information disclosure | credentials in logs or responses | high | mitigate | Sentinel credentials checked over captured logs and the page, layout and sync responses after a good and a failing sync (`SecretsStayServerSideTests.cs:29-60`), plus the no-token path (186-209). Status bodies: `StatusEndpointTests.cs:65-81`. Live payload: `LiveHubTests.cs:148-172` | closed |
| T-03-36 | 03-09 | Tampering | visitor-supplied username or base URI | high | mitigate | `BggSettings.cs:32-60`: the base URI is read only in Development (otherwise ignored and flagged); username, token and contact come only from configuration, and control characters are rejected. Tests: `BggSettingsTests.cs:19-60`, `SecretsStayServerSideTests.cs:137-161`, `SyncNowTests.cs:115-130` | closed |
| T-03-37 | 03-09 | Tampering | hostile or odd titles | medium | mitigate | `BggCollectionParser.cs:86-91,148-167`: removes C0, DEL, C1, U+2028/2029 and the bidi embedding, override and isolate characters (review fix IN-05). 300-character cap that never splits a surrogate pair. Rendered via `textContent` with `dir="auto"`. Tests: `BggCollectionParserTests.cs:62-131` | closed |
| T-03-38 | 03-09 | Information disclosure | private locations | medium | mitigate | `IncludePrivateInfo` false (`appsettings.json:23`), because the spike found locations not readable with the token. `showprivate` and the location are read only when it is on (`BggClient.cs:119`, `BggCollectionParser.cs:110,134-146`). `CabinetItem` has no location field (`SnapshotMapper.cs:30-36`). Test: `CollectionFidelityTests.cs:60-93` | closed |
| T-03-39 | 03-09 | Denial of service | missing credentials after a release | medium | mitigate | NotConfigured before any request (`BggClient.cs:77-80`). One warning, no throw (`SyncStartup.cs:29-36`). `/health` has no BGG check. Test: `SecretsStayServerSideTests.cs:186-209` (warning once, 0 requests, being-filled page, Healthy) | closed |
| T-03-40 | 03-10 | Tampering | poisoned, truncated or challenge answers | high | mitigate | `BggClient.cs:188-207`: status classification; a 200 must be XML. A missing, unparseable or mismatched `totalitems` is BadAnswer (217-219, review fix WR-02). Parse errors are BadAnswer (161-172). A failed expansion call fails the fetch (90-94). `SyncRunner.cs:59-65` returns before any write. Tests: `SyncFailureTests.cs:16-81` (nine answer classes, snapshot byte-identical), `BggDeclaredTotalTests.cs:21-57`. Skipped-entry tolerance: observation 5 | closed |
| T-03-41 | 03-10 | Tampering | empty or shrunken answers wiping the cabinet | high | mitigate | `SyncRunner.cs:67-77` runs the guard before any write. `Domain/Collection/ShrinkGuard.cs:36-57`: an empty answer over shown games is always held back; a loss of more than half needs the same fingerprint twice. Tests: `ShrinkGuardTests.cs:13-123`, `HeldBackTests.cs:23-208` (restart included). Double-fault residual: observation 4 | closed |
| T-03-42 | 03-10 | Denial of service | runaway polling of BGG | medium | mitigate | 16-request budget shared by both calls (`BggClient.cs:23,82,128-131,284-301`); bounded six-poll 202 schedule (26-34,153-158); every request goes through the shared pacer (184). 401 is terminal (194-195). Monotonic 5 s pacer (`RequestPacer.cs:28-65`); 10-minute run limit (`SyncWorker.cs:23,45-46`). Refused-token slow-down (review fix IN-02). Tests: `QueuedAnswerTests`, `BggRetryTests`, `RequestPacerTests`, `RejectedTokenBackoffTests`. Retry deviation: observation 1 | closed |
| T-03-43 | 03-10 | Information disclosure | failure details | medium | mitigate | Logs carry categories only (`SyncRunner.cs:62,71`, `SyncWorker.cs:58`). Details live only in `sync-state.json`. Visitors see a held-back boolean and the four-value result (`CabinetStatus.cs:52-54`). Tests: `StatusEndpointTests.cs:65-81`, `SyncFailureTests.cs:48-51` | closed |
| T-03-44 | 03-10 | Repudiation | operator unaware of a held-back result | low | mitigate | The held-back record with its time is persisted (`Domain/Collection/SyncState.cs:18`, `SyncCoordinator.cs:207,210`). Warning logged (`SyncRunner.cs:71`). Page note (`Pages/SyncStatusText.cs:60`, `copy.js:103,112`). Documented in `docs/bgg-sync.md:175-196`. Tests: `HeldBackTests.cs:23-62,146` | closed |
| T-03-45 | 03-11 | Tampering | page script injection | medium | mitigate | No inline script; two external scripts only (`Index.cshtml:69-72`). State travels in Razor-encoded `data-*` attributes. No markup APIs. Tests: `CabinetPageTests.cs:77-87,278-289`, `LivePageTests.cs:28-48`. The set of DOM sinks is wider than the wording but all are inert (observation 14) | closed |
| T-03-46 | 03-11 | Information disclosure | failure details in visitor text | medium | mitigate | Fixed sentences only (`copy.js:43-191`, `SyncStatusText.cs:12-61`); no failure category in the status. Tests: `CreditTests.cs:49-60`, `SyncStatusLineTests.cs:151-190`, `page-scripts.test.mjs:85-90`. Deviation: the held-back note in the header names BGG (observation 11) | closed |
| T-03-47 | 03-11 | Spoofing | wrong visitor clock | low | mitigate | The server offset comes from `data-server-time` and every status (`sync.js:55,258`). It is applied to the stale and elapsed checks, the countdown and the Retry-After window (118, 138, 376). Skewed-client test: `page-scripts.test.mjs:63-78` | closed |
| T-03-48 | 03-11 | Tampering | supply chain in CI | low | mitigate | `.github/workflows/ci.yml:47-48` runs `node --test` with no setup-node and no npm. The test imports only `node:` builtins. No `package.json`, npm lock file or `node_modules` in any ref's history. Node version not pinned (observation 17) | closed |
| T-03-49 | 03-12 | Denial of service | press loops from the page | medium | mitigate | A running or cooling-down button sends nothing (`sync.js:389-399`); a `pressing` flag blocks concurrent presses (385). Following starts only after a 202 and polls every 5 s with a 10-minute cap (`sync.js:11-12,326-353,409-423`). 15 s abort on the press request (review fix WR-08). Server window under a lock. Tests: `page-scripts.test.mjs:312-460` | closed |
| T-03-50 | 03-12 | Tampering | status values rendered as text | medium | mitigate | Only `COPY` sentences, `Intl`-formatted times and the countdown are written as text. The outcome goes through a fixed table (`sync.js:15-20,298`; `status.js:101-103`). No markup APIs | closed |
| T-03-51 | 03-12 | Information disclosure | outcome detail | low | mitigate | Every non-success result becomes `noteFailed` (`status.js:101-103`). The four-value `SyncResult` (`Domain/Collection/CollectionSnapshot.cs:78-91`). Held-back sentences have no counts. Tests: `page-scripts.test.mjs:126-151`, `LiveHubTests.cs:148-172` | closed |
| T-03-52 | 03-13 | Elevation of privilege | hub invocation | high | mitigate | `Live/CabinetHub.cs:20-40` declares only the lifecycle overrides; detailed errors are off (`LiveEndpoints.cs:81`). Tests: `LiveHubTests.cs:46-74` (every invocation, including `SyncNow`, fails with "Method does not exist"), with no BGG request, nothing running and no window opened (174-183) | closed |
| T-03-53 | 03-13 | Denial of service | connection flood on `/cabinet/live` | high | mitigate | Global cap: `TryAdmit` then `Context.Abort()` (`CabinetHub.cs:23-31`, `LiveConnectionLimiter.cs:21-52`). Kestrel `MaxConcurrentUpgradedConnections` set to the cap (`LiveEndpoints.cs:87`). WebSockets and SSE only (99). 1 KB messages, small buffers (82, 100-101). 15 s keep-alive, 30 s client timeout, 10 s handshake (83-85). Cap validated between 1 and 10000. Tests: `LiveHubTests.cs:76-146`, `LiveSettingsTests.cs:13-72`. Sizing deferred (observation 2) | closed |
| T-03-54 | 03-13 | Information disclosure | broadcast payload | medium | mitigate | The worker broadcasts `SyncStatusService.Current()`, the same record as `/cabinet/status` (`SyncWorker.cs:32,34`; `LiveNotifier.cs:34-50`). Sentinel test: `LiveHubTests.cs:148-172` | closed |
| T-03-55 | 03-13 | Spoofing | cross-site WebSocket to the hub | low | accept | AR-03-03 | closed (accepted) |
| T-03-56 | 03-13 | Denial of service | broadcast failure blocking syncs | medium | mitigate | `Live/LiveNotifier.cs:34-50` catches everything, logs the type name, and gives up after 5 s (review fix IN-03). Broadcasts are awaited with no lock held, and `Complete` runs before the closing broadcast (`SyncWorker.cs:32-34`). Tests: `HubLiveNotifierTests.cs:28-80` | closed |
| T-03-57 | 03-14 | Tampering | vendored SignalR client supply chain | high | mitigate | The SHA-256 of `wwwroot/lib/signalr/signalr.min.js` matches the pin in `Cabinet.UnitTests/Configuration/VendoredAssetTests.cs:10-30` and in NOTICE.md. `.gitattributes:1` sets `-text`. The blob is identical in `v0.3.0`. Independent check: the registry tarball in the local npm cache has the NOTICE's SRI and a byte-identical file. Owner approval of the download: SUMMARY claim | closed |
| T-03-58 | 03-14 | Tampering | CSP weakened for WebSockets | high | mitigate | Policy string unchanged since the previous phase; the exact string is pinned for the page, layout, static files, vendored script, logo, status, 404, 304 and negotiate (`ContentSecurityPolicyTests.cs:15-102`). There is no other policy source. SSE fallback (`LiveEndpoints.cs:99`) and status polling (`live.js:119-129`) are used instead of loosening the policy. Zero violations in Chromium and Firefox (SUMMARY claim); WebKit not run (03-VERIFICATION advisory; Open Items 6) | closed |
| T-03-59 | 03-14 | Denial of service | reconnect storms from many pages | medium | mitigate | **Not effective in the cap-refusal path.** The schedule (`status.js:145-156`: 0, 2, 10 and 30 s, then 60 s) and the visible-only 60 s fallback (`live.js:125-129`) are present. However, an over-cap page is refused by `Context.Abort()` after the handshake (`CabinetHub.cs:25-28`), so the framework's close allows a reconnect. The client reconnects with its retry count reset, so the first delay is 0 ms, and `onreconnected` fires a status request each time (`live.js:79-82`). The page's own back-off (`live.js:91-116`) runs only after `onclose`, which never fires because the retry policy never returns null. The node test fakes the refusal as `onclose` (`page-scripts.test.mjs:742-761`). Confirmed by reading; not measured at runtime. See Open Items | closed — re-measured after the audit: a refusal by `Context.Abort()` sends a close message without reconnect permission, so the browser client fires `onclose` and never auto-reconnects (verified with a raw WebSocket client and the vendored client in Node). Pinned by `LiveCapRefusalTests` with cap 1 and an auto-reconnecting client (`d49e709`); the page's own back-off now has ±20% jitter and no zero first delay (`c759104`); docs corrected (`e5e363e`). The loop described in this row does not occur |
| T-03-60 | 03-14 | Tampering | lint bypass for real code | medium | mitigate | `build/lint/checks/10-repo-rules.sh:20` is an anchored `wwwroot/lib/` prefix applied only to `js_files` (377). The tracked-file and C# lists are unfiltered (375-376), and the planning-reference check still reads the vendored file (381). Self-tests at 306-318 | closed |
| T-03-61 | 03-14 | Information disclosure | screenshots | low | mitigate | No image file was added between `v0.2.0` and HEAD. 03-14-SUMMARY uses a scratch placeholder. That the screenshots were taken against the fake: SUMMARY claim | closed |
| T-03-62 | 03-15 | Elevation of privilege | release publication | high | mitigate | `.github/workflows/release.yml:166-168` puts `publish` behind `environment: deploy`. The environment requires the owner as reviewer and accepts only `v*.*.*` tags. `check-github-settings.sh` passes 13/13 (admin-only tag ruleset, immutable releases, no self-hosted runners). The `v0.3.0` run has one approval from the repository-owner account (that a human clicked: SUMMARY claim). Procedural-only residual: observation 16 | closed |
| T-03-63 | 03-15 | Tampering | artefact in transit | high | mitigate | Publish re-verifies the checksum and attestation before un-drafting (`release.yml:184-213`). The container checks checksum, attestation (`deploy/lib/deploy.sh:49-67`: signer workflow, tag ref, self-hosted denied) and commit on main before unpacking (`deploy/bin/cabinet-deploy:183-196`; unchanged since `v0.2.0`). `v0.3.0` is immutable with three assets; its SLSA provenance names `release.yml` at the tag on GitHub-hosted runners. Draft-check deviation: observation 15 | closed |
| T-03-64 | 03-15 | Information disclosure | evidence and env file | high | mitigate | 03-15-SUMMARY evidence is versions, statuses, counts and one timestamp. No IP, email or real domain in 03-15-SUMMARY or 03-VERIFICATION; denylist scan 0. Env file never read and journal read skipped: SUMMARY claim | closed |
| T-03-65 | 03-15 | Spoofing | merge and tag identity | medium | mitigate | `v0.3.0` is an annotated tag with a noreply tagger, on the pull-request merge commit (an ancestor of `origin/main`). All 100 commits in `v0.2.0..v0.3.0` have noreply authors; all committers are noreply except the web-flow merge (accepted earlier). The 49 commits after the tag are all noreply | closed |
| T-03-66 | 03-15 | Denial of service | first production sync | low | mitigate | Start-up run only when stale and the last start is at least 15 minutes old, after a jittered delay (`SyncScheduler.cs:35-38,59-75`). 5 s pacer floor (`RequestPacer.cs:32`, `BggTransport.cs:32`); singleton pacer; single flight with the shared window. First production sync `changed`: SUMMARY claim. Unreleased fixes: observation 18 | closed |
| T-03-SC (03-07) | 03-07 | Tampering | NuGet `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 (unit tests) | high | mitigate | `Cabinet.UnitTests.csproj:11`; lock file with contentHash (`Cabinet.UnitTests/packages.lock.json:11-14`). `RestorePackagesWithLockFile` in `Directory.Build.props`; locked restore in CI and release (`package-release.sh:55`, `ci.yml:39-43`, `release.yml:38-39`). Legitimacy: 03-RESEARCH | closed |
| T-03-SC (03-08) | 03-08 | Tampering | NuGet `Microsoft.Extensions.TimeProvider.Testing` 10.10.0 (integration tests) | high | mitigate | `Cabinet.IntegrationTests.csproj:13`; lock file at 32-35; same locked restore | closed |
| T-03-SC (03-13) | 03-13 | Tampering | NuGet `Microsoft.AspNetCore.SignalR.Client` 10.0.12 (test only) | high | mitigate | `Cabinet.IntegrationTests.csproj:12`; lock file at 22-25. The package and its transitive dependencies appear only in the integration-test lock file; the service does not reference it | closed |
| T-03-SC (03-14) | 03-14 | Tampering | npm tarball and scratch Playwright | high | mitigate | No npm manifest, lock file or `node_modules` anywhere in history. Owner decision before download: SUMMARY claim. Legitimacy: SignalR in 03-RESEARCH; Playwright 1.63.0 in 02-RESEARCH (citation drift, observation 19) | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

AR-03-01 to AR-03-03 were declared `accept` in the phase's plan threat models; this audit confirmed each rationale against the code at HEAD, and the owner confirmed all three on 2026-10-07. AR-03-04 records the residual of the skipped-entry tolerance the owner chose for the declared-total check. All are low severity, below the `high` block threshold.

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-03-01 | T-03-11 | `entryId` is BGG's collection entry id (`BggCollectionParser.cs:97` → `CubbyArrangement.cs:288,309,341` → `render.js:155`): a number with no other personal field attached, shown only for the owner's public collection. **Unverified assumption:** the acceptance relies on a collection id not being resolvable on BGG's site to the owner's BGG account. If it can be resolved, visitors could derive the BGG username that the repository otherwise keeps out, and this acceptance should be revisited | Owner, 2026-10-07. The owner's BGG account name is not secret (it is already public through the repository's ownership), so an entry id that resolved to it would reveal nothing new; the name itself still never appears in the repository | 2026-10-07 |
| AR-03-02 | T-03-33 | The press handler reads nothing from the request (`SyncEndpoints.cs:113-137`), and there is no CORS, so a cross-site form POST is exactly a visitor press and cannot read the answer. It is bounded by the same persisted global window and single flight (`SyncCoordinator.cs:151-178`). No per-client limiter exists yet; that is deferred to the public-exposure work. Note: the refused-token slow-down applies only to timed syncs, so with a refused token, presses (including cross-site ones) can still cost one BGG request per window, at most six an hour | Owner, 2026-10-07 | 2026-10-07 |
| AR-03-03 | T-03-55 | The hub accepts no invocations (T-03-52) and pushes only the public status (T-03-54); there are no cookies or auth to abuse. The origin allow-list needs the real host name and belongs to the public-exposure work. Residual: with no origin check, a third-party page's visitors can take cap places, which feeds the reconnect loop in T-03-59 | Owner, 2026-10-07 (the reconnect-loop residual no longer applies: see T-03-59) | 2026-10-07 |
| AR-03-04 | T-03-40 | Owner's choice for the declared-total check: an answer without a usable declared total is rejected, but an entry whose ids do not parse is skipped and counted toward the total, with the skipped count logged (count only). A single malformed entry therefore cannot block the whole collection; the cost is that one game can drop out with only a warning, while losses of more than half are still held back by the shrink guard | Owner, 2026-10-07 | 2026-10-07 |

*Accepted risks do not resurface in future audit runs.*

---

## Unregistered Flags

Every summary's `## Threat Flags` reports "None", or maps its notes to registered threats. 03-12-SUMMARY and 03-15-SUMMARY have no such section; their new surface (the page's press, the release) is covered by T-03-49 to T-03-51 and T-03-62 to T-03-66. Every surface added by the review fixes maps to a registered threat. Two changes outside any plan were found:

| Flag | Category | Severity | Evidence | Proposed fix |
|------|----------|----------|----------|--------------|
| UF-03-01: runner label changed outside a plan | Elevation of privilege | low (mitigated) | Commit `7d802a1` moved every workflow to the `ubuntu-26.04` label, and `.github/actionlint.yaml` lists that label under `self-hosted-runner`. If GitHub's hosted pool stopped recognising the label, jobs would wait for a self-hosted runner. Mitigated today: zero registered self-hosted runners (settings check PASS), `--deny-self-hosted-runners` in publish and on the container, and `v0.3.0` provenance showing GitHub-hosted runners | Keep the zero-runner check; remove the actionlint entry once actionlint knows the label |
| UF-03-02: CI no longer runs on branch pushes | Information disclosure | low | The `push` trigger was narrowed to `main` (`986e2eb`, review IN-14). Pushes to the public milestone branch no longer run CI lint (gitleaks over all refs, repo rules) until a pull request exists. The local pre-push hook (denylist, noreply identity) is the only gate in between | Owner decision 2026-10-07: open the pull request before relying on CI for a working branch; the trigger stays on `main` |

---

## Observations (non-blocking)

Back end:

1. **T-03-42, mitigation changed by review fix IN-01.** The register says "stop on 401/429/503". At HEAD, the client stops at once on 401, 403, other 4xx and 3xx. A 429 or any 5xx is retried once per collection call (`BggClient.cs:140-151,196-199,227-253`). The retry goes through the shared pacer, counts against the 16-request budget, and is skipped when `Retry-After` asks for more than 60 s. A sync costs at most two extra requests, so the vector stays closed.
   - Optional hardening: BGG is reported to send 429 without `Retry-After`, so give a header-less 429/503 a longer floor than the 5 s pacer gap (for example 30 s).
2. **T-03-53, limits still to be sized.** The cap applies only after negotiate and handshake. Kestrel's upgraded-connection cap covers WebSockets only; SSE is counted only at the hub. There is no overall or per-client connection limit, and no test asserts that `MaxConcurrentUpgradedConnections` is applied. The plan defers final sizes to the public-exposure work.
3. **T-03-01, guard changed after the run (review fix IN-13).** Names under four characters now match only as whole words (`bgg-access-check.py:205-217`). The guard also skips 1-character values and digit-only values under 4 characters (187-194). The fix report asks for owner sign-off of the threshold. `docs/bgg-access-check.md:70-74` still describes a one-line notice and does not mention the short-name rule.
4. **T-03-28 / T-03-41, a double fault can still wipe the cabinet after the WR-04 fix.**
   - If `snapshot.json` exists but cannot be read at start-up, it is left in place and the cabinet starts empty (`SnapshotStore.cs:48-52`, `SyncStartup.cs:41-46`).
   - The guard then sees no shown games (`ShrinkGuard.cs:36-39`), so the next well-formed answer is accepted as a first sync, even an empty or shrunken one.
   - That answer overwrites the good file (`SyncRunner.cs:87`), which before WR-04 would have been moved aside and kept.
   - Proposed fix: remember that a stored snapshot exists but was not read, and retry the read or hold back empty and shrunken answers until it has been read.
5. **T-03-40, skipped entries are tolerated (the review's WR-02 choice).** An entry whose ids do not parse is dropped and counted toward the declared total (`BggClient.cs:212-219`, `BggCollectionParser.cs:65-75,97-101`). A game can therefore disappear with only a warning count, and the shrink guard catches only losses of more than half. Proposed: record this as an accepted risk if the owner agrees.
6. **T-03-24, wording.** "The client never logs" no longer holds: `BggClient.cs:214` (WR-02) logs a skipped-entry count at Warning, with no username, address or title (`BggDeclaredTotalTests.cs:61-72`). Reword the mitigation to "the client logs counts only".
7. **T-03-24 / T-03-35, the log level is not pinned.** Nothing asserts the committed `System.Net.Http.HttpClient` Warning level. Proposed: a configuration test that the committed level is Warning or higher.
8. **T-03-26, no test for the 20 MB limits.** Both limits are in code (`BggCollectionParser.cs:28,48`; `SyncEndpoints.cs:23,72`) but untested. Proposed: an over-limit scripted answer and a parser character-limit test.
9. **T-03-32 / T-03-28, odd but valid stored values.** The stores accept integer enum values, a far-future `cooldownEndsUtc`, a forged held-back fingerprint or a null title. This needs write access to the state directory, which is outside the threat. Optional: disallow integer enums, clamp the loaded cooldown to now plus the manual cooldown, reject null titles.
10. **T-03-20, no permanent zip check.** At HEAD there is no lasting regression check that the release zip excludes the fake. An uncommitted change adding such a check appeared in `build/tests/package-release-e2e-test.sh` during this audit, from a parallel run; once committed, it closes the gap.

Front end and public surface:

11. **T-03-46, the header names BGG while a result is held back.** The held-back note (`SyncStatusText.cs:60-61`, `copy.js:103-105`) sits inside `<header>` (`Index.cshtml:20`). The header tests cover only the not-yet-synced and "recent syncs" states. No failure category, code or count is shown. The conflict between the copy contract and the plan was flagged in 03-11-SUMMARY. Either change both strings and extend the header test, or reword the mitigation to "no failure category, status code or count".
12. **T-03-12, logo provenance.** The exact source URL is still "to be confirmed by the owner" (03-04-SUMMARY). The SVG has no hash pin, unlike the vendored client. Proposed: a unit test pinning its SHA-256 and/or checking an element allowlist, and record the source URL.
13. **T-03-10 / T-03-45 / T-03-50, no lint rule against markup-building APIs** (carried forward from the previous phase's observation 4). The code is clean at HEAD. Proposed: a repo-rules check over `Cabinet.Service/wwwroot/js` for `innerHTML`, `outerHTML`, `insertAdjacent`, `document.write`, `eval(` and `new Function`.
14. **T-03-45 / T-03-50, wording.** Besides `textContent`, `hidden` and `aria-*`, `sync.js` also writes `title`, `time.dateTime` (raw ISO time), `className`, `dataset.*` with raw server strings (259-288), and elements it creates itself. All are inert sinks; only the mitigation wording needs updating.

Release, process and supply chain:

15. **T-03-63, draft not verified before approval, again.** The owner approved `deploy` before the workstation draft check (03-15-SUMMARY). This repeats the previous phase's observation 1 and open item 4. The vector stays closed: publish verified before un-drafting, the container verifies again, and the release is immutable.
16. **T-03-62, the gate is procedural only.**
    - The deploy environment allows admin bypass (`can_admins_bypass: true`).
    - The project's Claude Code settings have no permission rules, so the previous phase's open item 3 is unresolved.
    - The workstation `gh` login holds a `repo`-scoped token.
    - Nothing technical stops an agent session that holds the owner's login from merging, tagging or approving the deployment.
17. **T-03-48:** the runner's Node version is not pinned. This is within the declared mitigation.
18. **T-03-66, fixes not yet released.** The deployed `v0.3.0` still has the wall-clock pacer (review WR-03) and arms the timer before the start-up delay (IN-04). Both are fixed at HEAD but not released. Neither affects the 5 s floor or single flight.
19. **T-03-SC (03-14), citation drift.** The mitigation cites 03-RESEARCH for both downloads, but that table lists only the SignalR package; Playwright's verdict is in 02-RESEARCH. Playwright's browser binaries come from its CDN and are not covered by npm integrity. Add a Playwright row when it is reused.
20. **Privacy hygiene.** The container's SSH alias appears in committed planning files (this phase's 03-01, 03-02 and 03-15 plans and earlier phases' plans), while the docs use `<container>`. It is a generic alias, not a resolvable hostname, with no denylist hit. Optional: replace it with the placeholder.
21. **Carry-forward from the previous phase (AR-02-04).** The prototype switch is handled: it is off in committed settings and forced off in Production (`SampleCatalog.cs:111`). `noindex` is not handled: there is no robots meta tag, no `X-Robots-Tag` and no `robots.txt`. The site now shows the real collection, so whether it may be indexed is an owner decision before public exposure.
22. **T-03-14 (no fix needed).** The credit link has no `target="_blank"`, so `rel="noopener"` has no effect; reverse-tabnabbing exposure is nil either way.

---

## Open Items (owner decisions)

Resolved on 2026-10-07 after this audit (see "Post-audit fixes"): T-03-59, T-03-04, the accepted risks AR-03-01 to AR-03-04, `noindex`, the T-03-46 wording, the CI push trigger and the follow-up changes. Still open, all owner actions outside the repository's code:

1. **Release gates (observation 16):** turn off admin bypass on the `deploy` environment, and optionally add Claude Code permission rules (ask or deny) for merging pull requests, pushing `v*` tags and approving deployments. These are the owner's settings; no agent changes them.
2. **Draft check before approval (observation 15):** approve `deploy` only after the workstation draft check has passed.
3. **WebKit/Safari (T-03-58):** open the deployed page once in Safari to confirm live updates under the strict policy.

---

## Post-audit fixes (2026-10-07)

Applied in one fix round on the milestone branch after this audit, merged and verified together (937 .NET tests on four runs, 75 page-script tests, all five lint checks, the access-check tests and both end-to-end scripts pass). They reach the server with the next release.

| Item | Resolution | Commit |
|------|------------|--------|
| T-03-59 | Re-measured: no reconnect loop (see the register row); behaviour pinned by tests, client jitter added, docs corrected | `d49e709`, `c759104`, `e5e363e` |
| T-03-04 | Encoding-independent DOCTYPE/entity refusal | `161f2f8` |
| Observation 1 (T-03-42) | Mitigation now reads: stop at once on 401, 403, other 4xx and redirects; retry a 429 or 5xx once per call through the shared pacer within the 16-request budget, never when `Retry-After` exceeds 60 s | wording |
| Observation 3 (T-03-01) | Access-check docs match the guard; the four-character whole-word threshold is accepted | `5ce4294` |
| Observation 4 (T-03-28, T-03-41) | An existing but unreadable snapshot is re-read before each sync; while it stays unreadable, an empty answer is always held back and any other answer must be confirmed by an identical second fetch before it replaces the file | `f23a2ed` |
| Observation 5 (T-03-40) | Recorded as AR-03-04 | — |
| Observation 6 (T-03-24) | Mitigation now reads: the client logs counts only, never the username, an address or a title | wording |
| Observations 7 and 8 (T-03-24, T-03-26, T-03-35) | Tests pin the committed HTTP client log level and the 20 MB transport and XML limits (an oversized transport answer ends as `Unavailable` because the client buffer limit fires before the parser; the XML limit raises `XmlException`) | `72a9236` |
| Observation 10 (T-03-20) | The release-layout end-to-end test fails if the zip contains the fake BGG or a test assembly | `c7cae25` |
| Observation 11 (T-03-46) | Owner kept the note wording that names BGG; mitigation now reads: the header names no failure category, status code or count | wording |
| Observation 12 (T-03-12) | Logo pinned by SHA-256 with a provenance notice (official reversed RGB SVG from BGG's logo pack linked from the XML API terms of use; exact page not recorded); marked `-text` so checkouts keep it byte-identical | `c2a71bf`, `a5e3f80` |
| Observation 13 (T-03-10, T-03-45, T-03-50) | Repo-rules lint forbids `innerHTML`, `outerHTML`, `insertAdjacentHTML`, `document.write` and `.cssText` in page scripts (`eval` and `new Function` stay blocked by the strict policy, which has no `unsafe-eval`) | `e9fdefe` |
| Observation 14 | Wording: the page also writes `title`, `dateTime`, `className` and `dataset` values, all inert sinks | wording |
| Observation 18 (T-03-66) | Fixed at HEAD; ships with the next release | — |
| Observation 20 | Kept: the SSH alias is a local shortcut, not a resolvable hostname or IP, and is already in published history | owner decision |
| Observation 21 | `noindex` meta tag on every page, tested for every Razor page | `9a385fd` |
| UI review warnings | Footer centred, 40rem limits, stale press notes cleared, held-back press sentence covered while the older-sync note shows | `569b3de`, `5c76ca1`, `d95d9da` |

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-07 | 70 | 68 (65 mitigated, 3 accepted) | 2 (both medium, below the high threshold; 0 blocking) | gsd-security-auditor ×3, split by area (ASVS L2, block_on high) |
| 2026-10-07 (post-fix) | 70 | 70 (67 mitigated, 3 accepted; AR-03-04 records an accepted residual) | 0 | orchestrator, from the fix round's evidence |

## Security Audit 2026-10-07
| Metric | Count |
|--------|-------|
| Threats found | 70 |
| Closed | 68 |
| Open | 2 (non-blocking) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed; the two medium threats open at audit time (T-03-04, T-03-59) were closed by the post-audit fix round
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-07; the owner confirmed the accepted risks and the decisions recorded under Post-audit fixes. Remaining owner actions are listed under Open Items

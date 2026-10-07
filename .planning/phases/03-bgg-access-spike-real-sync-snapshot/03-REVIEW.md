---
phase: 03-bgg-access-spike-real-sync-snapshot
reviewed: 2026-10-07T00:00:00Z
depth: standard
files_reviewed: 128
files_reviewed_list:
  - .gitattributes
  - .github/actionlint.yaml
  - .github/workflows/ci.yml
  - .github/workflows/release.yml
  - .gitignore
  - Cabinet.Domain/Collection/BoxFromVersion.cs
  - Cabinet.Domain/Collection/CollectionSnapshot.cs
  - Cabinet.Domain/Collection/ShrinkGuard.cs
  - Cabinet.Domain/Collection/SnapshotMapper.cs
  - Cabinet.Domain/Collection/SyncState.cs
  - Cabinet.Domain/Layout/CabinetLayout.cs
  - Cabinet.Domain/Layout/CabinetLayoutEngine.cs
  - Cabinet.Domain/Layout/CubbyArrangement.cs
  - Cabinet.FakeBgg/BggXml.cs
  - Cabinet.FakeBgg/Cabinet.FakeBgg.csproj
  - Cabinet.FakeBgg/FakeBggProgram.cs
  - Cabinet.FakeBgg/FakeBggScenario.cs
  - Cabinet.FakeBgg/FakeBggServer.cs
  - Cabinet.FakeBgg/SyntheticBggCollection.cs
  - Cabinet.FakeBgg/Testing/ScriptedBggHandler.cs
  - Cabinet.IntegrationTests/BackgroundSyncTests.cs
  - Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj
  - Cabinet.IntegrationTests/CabinetPageTests.cs
  - Cabinet.IntegrationTests/CollectionFidelityTests.cs
  - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
  - Cabinet.IntegrationTests/CreditTests.cs
  - Cabinet.IntegrationTests/FakeBggServerTests.cs
  - Cabinet.IntegrationTests/HeldBackTests.cs
  - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
  - Cabinet.IntegrationTests/Infrastructure/CapturingLoggerProvider.cs
  - Cabinet.IntegrationTests/Infrastructure/NoWaitPacer.cs
  - Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs
  - Cabinet.IntegrationTests/Infrastructure/SyncRounds.cs
  - Cabinet.IntegrationTests/Infrastructure/TemporaryDirectory.cs
  - Cabinet.IntegrationTests/LayoutEndpointTests.cs
  - Cabinet.IntegrationTests/LiveHubTests.cs
  - Cabinet.IntegrationTests/LivePageTests.cs
  - Cabinet.IntegrationTests/SecretsStayServerSideTests.cs
  - Cabinet.IntegrationTests/StatusEndpointTests.cs
  - Cabinet.IntegrationTests/SyncButtonTests.cs
  - Cabinet.IntegrationTests/SyncFailureTests.cs
  - Cabinet.IntegrationTests/SyncNowTests.cs
  - Cabinet.IntegrationTests/SyncPipelineTests.cs
  - Cabinet.IntegrationTests/SyncStatusLineTests.cs
  - Cabinet.IntegrationTests/TestHostPortTests.cs
  - Cabinet.Repository/Bgg/BggClient.cs
  - Cabinet.Repository/Bgg/BggCollectionParser.cs
  - Cabinet.Repository/Bgg/BggTransport.cs
  - Cabinet.Repository/Bgg/RequestPacer.cs
  - Cabinet.Repository/Cabinet.Repository.csproj
  - Cabinet.Repository/Storage/AtomicJsonFile.cs
  - Cabinet.Repository/Storage/SnapshotStore.cs
  - Cabinet.Repository/Storage/SyncStateStore.cs
  - Cabinet.Service/Cabinet.Service.csproj
  - Cabinet.Service/Collection/CollectionStore.cs
  - Cabinet.Service/Collection/StorageLocation.cs
  - Cabinet.Service/Layout/LayoutCache.cs
  - Cabinet.Service/Layout/LayoutEndpoint.cs
  - Cabinet.Service/Live/CabinetHub.cs
  - Cabinet.Service/Live/LiveConnectionLimiter.cs
  - Cabinet.Service/Live/LiveEndpoints.cs
  - Cabinet.Service/Live/LiveNotifier.cs
  - Cabinet.Service/Pages/Index.cshtml
  - Cabinet.Service/Pages/Index.cshtml.cs
  - Cabinet.Service/Pages/Shared/_Layout.cshtml
  - Cabinet.Service/Pages/SyncStatusText.cs
  - Cabinet.Service/Pages/_ViewStart.cshtml
  - Cabinet.Service/Program.cs
  - Cabinet.Service/Prototype/SampleCatalog.cs
  - Cabinet.Service/Sync/BggSettings.cs
  - Cabinet.Service/Sync/CabinetStatus.cs
  - Cabinet.Service/Sync/SyncCoordinator.cs
  - Cabinet.Service/Sync/SyncEndpoints.cs
  - Cabinet.Service/Sync/SyncRunner.cs
  - Cabinet.Service/Sync/SyncScheduler.cs
  - Cabinet.Service/Sync/SyncSettings.cs
  - Cabinet.Service/Sync/SyncStartup.cs
  - Cabinet.Service/Sync/SyncWorker.cs
  - Cabinet.Service/appsettings.Development.json
  - Cabinet.Service/appsettings.json
  - Cabinet.Service/wwwroot/css/site.css
  - Cabinet.Service/wwwroot/js/cabinet.js
  - Cabinet.Service/wwwroot/js/copy.js
  - Cabinet.Service/wwwroot/js/live.js
  - Cabinet.Service/wwwroot/js/render.js
  - Cabinet.Service/wwwroot/js/status.js
  - Cabinet.Service/wwwroot/js/sync.js
  - Cabinet.Service/wwwroot/lib/signalr/NOTICE.md
  - Cabinet.UnitTests/Bgg/BggCollectionParserTests.cs
  - Cabinet.UnitTests/Bgg/BggFailureTests.cs
  - Cabinet.UnitTests/Bgg/BggTestKit.cs
  - Cabinet.UnitTests/Bgg/BggTransportTests.cs
  - Cabinet.UnitTests/Bgg/QueuedAnswerTests.cs
  - Cabinet.UnitTests/Bgg/RequestPacerTests.cs
  - Cabinet.UnitTests/Cabinet.UnitTests.csproj
  - Cabinet.UnitTests/Collection/BoxFromVersionTests.cs
  - Cabinet.UnitTests/Collection/SnapshotMapperTests.cs
  - Cabinet.UnitTests/Configuration/VendoredAssetTests.cs
  - Cabinet.UnitTests/FakeBgg/BggXmlTests.cs
  - Cabinet.UnitTests/Layout/PlacementIdentityTests.cs
  - Cabinet.UnitTests/Live/LiveSettingsTests.cs
  - Cabinet.UnitTests/Prototype/SampleCatalogTests.cs
  - Cabinet.UnitTests/Prototype/SampleGenerationTests.cs
  - Cabinet.UnitTests/Snapshot/SnapshotStoreTests.cs
  - Cabinet.UnitTests/Snapshot/SyncStateStoreTests.cs
  - Cabinet.UnitTests/Sync/BggSettingsTests.cs
  - Cabinet.UnitTests/Sync/InMemorySyncStateStore.cs
  - Cabinet.UnitTests/Sync/ShrinkGuardTests.cs
  - Cabinet.UnitTests/Sync/SyncCoordinatorTests.cs
  - Cabinet.UnitTests/Sync/SyncSchedulerTests.cs
  - Cabinet.UnitTests/Sync/SyncSettingsTests.cs
  - Cabinet.UnitTests/Sync/SyncStatusTextTests.cs
  - Cabinet.slnx
  - README.md
  - build/bgg-access-check.py
  - build/lint/checks/10-repo-rules.sh
  - build/tests/bgg-access-check-test.sh
  - build/tests/fixtures/relative-time-cases.json
  - build/tests/package-release-e2e-test.sh
  - build/tests/page-scripts.test.mjs
  - deploy/cabinet.env.example
  - deploy/tests/cabinet-deploy-e2e-test.sh
  - docs/bgg-access-check.md
  - docs/bgg-sync.md
  - docs/cabinet-layout.md
  - docs/development.md
  - docs/lxc-setup.md
  - docs/vendored-assets.md
findings:
  critical: 0
  warning: 16
  info: 14
  total: 30
status: issues_found
---

# Phase 3: Code Review Report

Reviewed in three parallel parts at standard depth over 128 source files (reference layouts, lock files, images and the vendored SignalR client excluded). Finding numbers run across the parts: back end WR-01 to WR-04 and IN-01 to IN-05, front end WR-05 to WR-08 and IN-06 to IN-09, tests, scripts and CI WR-09 to WR-16 and IN-10 to IN-14.

## Orchestrator notes

- **WR-02 conflicts with a deliberate decision.** Plan 03-09 counts skipped malformed entries toward the declared total so that one bad entry does not reject a complete collection. The finding's alternative (reject on any skipped entry, and on a missing total) trades that resilience for strictness. Treat as a judgement call; a middle way is to keep the comparison but log the skipped count and fail when the total attribute is missing.
- **WR-05 is the second path to a stuck "Syncing..." button** (the first was fixed in plan 03-14). It reproduces only when a sync ends within microseconds, which is the unconfigured mode; the deployed site syncs in about ten seconds.
- No finding is critical. The owner's deployed check passed with these open.

## Part: Back end (domain, repository, service, fake BGG)


**Reviewed:** 2026-10-07
**Depth:** standard
**Files Reviewed:** 46
**Status:** issues_found

**Summary**
The backend is carefully built and matches the measured decision table in most places: two-call split, 5 s pacer with a
floor, 202 schedule of 5/10/20/30/30/30 s with a 16-request budget (worst case 14, so the budget never binds), 401 classified
as never-retried, token attached only to HTTPS `boardgamegeek.com` with redirects off, a 20 MB response cap and a hardened
`XmlReader`, atomic temp-file-plus-rename writes, persisted cooldown written before the request is queued, single-flight via
the `_running` flag plus a capacity-1 channel, and a receive-nothing SignalR hub with a connection cap. Logs, status
payloads and broadcasts carry no token, username, contact URL or failure category. The hard rules are met in all 46 files:
no `//` comments, no planning references, no personal data (greps run over every listed file).

No blocker was found. The four warnings are robustness defects around cancellation classification, the strength of the
totalitems integrity check, the pacer's clock, and destructive handling of a transient read error. The info items are
smaller hardening points.

**Warnings**
### WR-01: Host shutdown (and the run limit) is recorded as a BGG timeout, and the worker's cancellation branches are dead code

**File:** `Cabinet.Repository/Bgg/BggClient.cs:141-144` (with `Cabinet.Service/Sync/SyncWorker.cs:51-58`)
**Issue:** `GetCollectionAsync` ends with `catch (OperationCanceledException) { return Failed(SyncFailure.Timeout); }`, which
swallows every cancellation, including the caller's token. The caller's token is the worker's linked token, so a service
stop or restart during a sync (a deploy, for example) returns `Failed(Timeout)` instead of propagating. `SyncRunner` then
logs "BGG sync failed: Timeout", the worker calls `Complete` with `Failed`, and `sync-state.json` is written with
`LastResult = failed`, `LastFailure = Timeout` and `ConsecutiveFailures + 1` for a run that was merely interrupted. The two
`OperationCanceledException` branches in `SyncWorker.RunOnceAsync` can then never be reached from the BGG path, so the
distinction they were written to draw (run limit vs. shutdown) is lost. The visitor-facing "last result" and the failure
counter are wrong after every deploy that lands mid-sync.
**Fix:** Only a timeout that did not come from the caller is a BGG timeout; let caller cancellation propagate and let the
worker classify it.
```csharp
catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
{
    return Failed(SyncFailure.Timeout);
}
```
In `SyncWorker` keep the existing branches (run limit becomes `Timeout`) and, on real shutdown, skip `coordinator.Complete`
failure accounting (or complete without incrementing the failure counter) so an interrupted run is not counted as failed.

### WR-02: The totalitems integrity check is skipped when the attribute is absent or unparseable, and skipped entries count as parsed

**File:** `Cabinet.Repository/Bgg/BggClient.cs:182-184`, `Cabinet.Repository/Bgg/BggCollectionParser.cs:65-77`
**Issue:** The decision table says to keep the declared total as a hard integrity check on collection calls, and all five
measured collection answers declared one. The code rejects only when `parsed.TotalItems is { } total && total != ...`.
A well-formed `<items>` root with no `totalitems` (or a non-numeric one, which `ReadWholeNumber` turns into null) is
accepted as a complete collection, so the check can be bypassed by exactly the odd answers it exists for. Separately, an
owned entry whose `collid` or `objectid` does not parse is dropped and added to `SkippedItems`, and `SkippedItems` is added
to the parsed count, so the totals match and a game silently disappears from the cabinet with no log line or state change.
The shrink guard only catches losses above half.
**Fix:** Treat a missing or unparseable total as a bad answer on collection calls, and do not let skipped entries satisfy
the total.
```csharp
return parsed.TotalItems is not { } total || total != parsed.Items.Count
    ? Failed(SyncFailure.BadAnswer)
    : new CollectionFetchResult.Fetched(parsed.Items);
```
(A skipped entry then fails the whole fetch, which keeps the last good snapshot; if tolerance is wanted, log the skipped
count by number only.)

### WR-03: The request pacer measures the gap with the wall clock, so a backwards clock step stalls the sync

**File:** `Cabinet.Repository/Bgg/RequestPacer.cs:23,43-50,64`
**Issue:** `_lastEnd` is a `DateTimeOffset` taken from `GetUtcNow()` and the wait is `lastEnd + _gap - now`. If the system
clock is stepped backwards (NTP correction, container resume, host clock change) between two requests, `wait` becomes the
size of the step plus the gap, and the pacer sleeps that long inside a held turn. The worker's 10 minute run limit then
ends the sync as a timeout, and every following sync can repeat it until the clock catches up. A forward step is harmless.
The gap is a duration, so it should use a monotonic source.
**Fix:** Store `_time.GetTimestamp()` and compare with `_time.GetElapsedTime(lastTimestamp)`; also cap the delay at the
configured gap.
```csharp
private long? _lastEndTimestamp;
...
var elapsed = _time.GetElapsedTime(_lastEndTimestamp.Value);
var wait = _gap - elapsed;
if (wait > _gap) { wait = _gap; }
...
_lastEndTimestamp = _time.GetTimestamp();
```

### WR-04: A transient read error moves the last good snapshot aside

**File:** `Cabinet.Repository/Storage/SnapshotStore.cs:46-51,90-106` (same pattern in `Cabinet.Repository/Storage/SyncStateStore.cs:46-51,85-101`)
**Issue:** `Load` treats any `IOException` or `UnauthorizedAccessException` from `ReadAllBytes` as "unreadable" and calls
`SetAside`, which renames `snapshot.json` over `snapshot.json.bad`. A transient failure (a momentary I/O error, a
permissions hiccup during a deploy, a full descriptor table) therefore demotes a perfectly good snapshot: the page starts
as "being filled", `ShrinkGuard` sees `previousCount == 0` and accepts whatever the next fetch returns (including a partial
one), and the previous `.bad` file is overwritten. Moving a file aside is right for content that is damaged or too new, not
for a read that failed before any content was seen.
**Fix:** On a read exception, leave the file where it is, log the category, and return null (or the initial state); only
call `SetAside` for the JSON and schema problems detected after the bytes were read.
```csharp
catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
{
    _logger.LogWarning("The stored collection could not be read ({Reason}) and was left in place.", "unreadable");
    return null;
}
```

**Info**
### IN-01: Transient 5xx and 429 answers are not retried inside a sync, although the decision table says the client relies on resilience defaults

**File:** `Cabinet.Service/Sync/SyncEndpoints.cs:66-75`, `Cabinet.Repository/Bgg/BggClient.cs:168-171`
**Issue:** No resilience handler is registered and the typed client maps 429/503 to `Throttled` and every other non-200
status to `Unavailable` on the first answer. One transient 502 therefore fails the whole sync until the next hourly tick,
and the accepted request has already opened the shared cooldown window, so a visitor's "sync now" is also refused for the
cooldown. This is defensible (failing fast keeps the pacing guarantee) and note that bolting on a standard resilience handler
below `BggClient` would be wrong, because its retries would bypass the pacer's turn and the 5 s gap and the request budget.
**Fix:** Either record in the settings documentation that transient errors are retried only by the next scheduled run, or
retry once inside `GetCollectionAsync` for 429/5xx through the pacer and the `RequestBudget`, with a wait taken from the
`QueuedWaits` schedule.

### IN-02: `ConsecutiveFailures` is persisted but never used, so a rejected token is retried every hour forever

**File:** `Cabinet.Service/Sync/SyncCoordinator.cs:163-170`, `Cabinet.Service/Sync/SyncScheduler.cs:34-43`
**Issue:** The counter is stored and exposed to tests only. A permanent failure (`Unauthorized`, `NotConfigured`) keeps
spending a request per hour, and a persistent `Unauthorized` is the case the spike showed answers 401 for a wrong token.
**Fix:** Skip scheduled (not manual) runs while the last failure is `NotConfigured` or `Unauthorized` and the configuration
has not changed, or back the scheduled interval off with `ConsecutiveFailures`.

### IN-03: The start-of-run broadcast is awaited inside the running window with no time bound

**File:** `Cabinet.Service/Sync/SyncWorker.cs:31`, `Cabinet.Service/Live/LiveNotifier.cs:27`
**Issue:** `PublishAsync` is awaited after `_running` is set and before the fetch starts, and it only observes the host's stop
token. `Clients.All` writes to each connection in turn, so one client with a full output pipe delays the start of the sync
until SignalR drops it (about the 30 s client timeout). It never wedges the coordinator, because `Complete` runs before the
closing broadcast, but it can delay every run.
**Fix:** Bound the broadcast with a short linked timeout (for example 5 s) inside `HubLiveNotifier.PublishAsync`.

### IN-04: The periodic timer starts before the start-up delay, so a long jitter can double up the first runs

**File:** `Cabinet.Service/Sync/SyncScheduler.cs:30-43`
**Issue:** `PeriodicTimer` is created before `Task.Delay(StartupDelay())`. With the defaults (jitter at most 120 s, interval
60 min) this is harmless. With `Sync:StartupJitterMaxSeconds` above `Sync:IntervalMinutes * 60` (both are accepted by
`SyncSettings`: jitter up to 3600 s, interval down to 15 min) a tick is already pending when the start-up request is made,
so a scheduled request follows the start-up one immediately.
**Fix:** Create the timer after the start-up delay, or reject a jitter that is not smaller than the interval in
`SyncSettings.FromConfiguration`.

### IN-05: `CleanTitle` strips only characters below U+0020

**File:** `Cabinet.Repository/Bgg/BggCollectionParser.cs:147-148`
**Issue:** The cleaning removes C0 controls only. DEL, the C1 controls (U+0080 to U+009F), line and paragraph separators
(U+2028, U+2029) and bidirectional override or isolate characters (U+202A to U+202E, U+2066 to U+2069) pass into the
snapshot, the layout JSON and from there the page. BGG titles are untrusted data, and a right-to-left override in a title
can visually reorder the label text of neighbouring UI. Rendering through `textContent` avoids injection, but the stored
value is not as clean as the comment promises.
**Fix:** Drop `char.IsControl` characters and the bidi and separator ranges above, in `RemoveControlCharacters`.

---

_Reviewed: 2026-10-07_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

## Part: Front end (pages, scripts, styles, docs)


**Reviewed:** 2026-10-07
**Depth:** standard
**Files Reviewed:** 22
**Status:** issues_found

**Summary**
The front end is in good shape on the structural rules. No `innerHTML`, `insertAdjacentHTML`, `cssText`, inline script or inline style anywhere in the reviewed files. All BGG text is set through `textContent`. The only style writes are `style.setProperty` calls, which the strict CSP allows. There are no `//` comments in the JS or C#, no planning references, and no personal data introduced in this part. The vendored client's size and SHA-256 match NOTICE.md. The first-paint server text and the client text agree on the relative-time rules. I also checked the `en-NL` exact-time format in Node: it produces `6 October 2026 at 14:32 CEST`, and non-European zones fall back to `GMT-4`-style names, which is acceptable.

The real defects are in the sync-now press flow and the quiet-redraw state machine. There is one way for the button to stay on "Syncing..." until a reload. There is a second live region that the UI contract forbids. There is no timeout on the press request. The redraw guard clears itself too early. The rest is dead code and docs gaps.

**Warnings**
### WR-05: The button can stay on "Syncing..." until reload when a sync finishes before the 202 answer is built

**File:** `Cabinet.Service/wwwroot/js/sync.js:320-341` (also `Cabinet.Service/Sync/SyncEndpoints.cs:119-122`, `Cabinet.Service/wwwroot/js/status.js:134-140`)
**Issue:**
- The 202 answer is `statusService.Current() with { Running = true }`. `Current()` is stamped with the clock after `TryRequest` has already released its lock.
- If the sync completes in between, its "finished" push carries an earlier `serverTimeUtc` than the 202 body.
- The page then drops the push through `isOutdatedStatus`, because `at < newestServerTimeMs`. It keeps the forced `running: true`.
- This is not only theoretical. An unconfigured server is a documented mode (bgg-sync.md says "a sync reports that it is not configured without calling BGG"), and there the run ends in microseconds.
- Recovery does not happen while the live connection is up. `pollOwnSync` only calls `fetchStatus` when `!liveConnected()` (line 327), and live.js's fallback poll runs only when `!connected`.
- When the 10-minute deadline passes (lines 331-335), it sets `ownSyncPending = false` and stops without one last fetch.
- Nothing else re-checks the status. The button reads "Syncing..." (`aria-disabled="true"`) until the next hourly push or a reload, and the visitor's own press never gets its outcome sentence.
- The same dead end follows from any lost push while connected.

**Fix:** Do not trust pushes alone for the visitor's own press. Always fetch inside the poll step, and do a final fetch at the deadline:
```js
const step = async () => {
  await fetchStatus(true);

  if (!ownSyncPending || Date.now() >= deadline) {
    ownSyncPending = false;
    pollTimer = null;
    return;
  }

  pollTimer = window.setTimeout(step, POLL_INTERVAL_MS);
};
```
`fetchStatus` is cheap and the poll only runs for at most ten minutes after the visitor's own press, so `isLiveConnected` is no longer needed. Server side, drop the forced `Running = true` in the 202 body, or build the status once under the coordinator lock so a later timestamp can never carry an earlier state.

### WR-06: A second live region and a passive "Loading the cabinet..." announcement

**File:** `Cabinet.Service/wwwroot/js/cabinet.js:30`, `63-66`, `177-183`
**Issue:**
- The UI contract says the page has exactly one live region, `#sync-note`. It also says the cabinet redraw announces nothing.
- `showLoading()` puts `role="status"` on the loading line, which makes it a second polite live region.
- `phoneQuery.addEventListener('change', load)` (line 211) calls `showLoading()`. A visitor who rotates the phone, or crosses the 40rem width with a screen reader running, hears "Loading the cabinet..." although they pressed nothing.
- `abandonRedraw()` (line 177) uses `.cabinet-message[role="status"]` as a probe for "the first load is still showing". That ties the page's state to an ARIA attribute that should not exist.

**Fix:** Drop `role="status"` from the loading line and mark it with a class or data attribute instead:
```js
message.className = 'cabinet-message cabinet-loading';
```
```js
if (mount.querySelector('.cabinet-loading') !== null) {
```
Keep the loading text for sighted users, but not as a live region.

### WR-07: The redraw guard is cleared by whichever redraw finishes first

**File:** `Cabinet.Service/wwwroot/js/sync.js:206-222`
**Issue:**
- `finally { redrawingVersion = null; }` runs unconditionally. If redraw A (version A) is in flight and a status for version B arrives, B starts and sets `redrawingVersion = B`.
- A is superseded inside `redraw()` (it returns `false` once `latestLoad` has moved on). Its `finally` then resets the guard to `null` while B is still running.
- The next status carrying B passes the `version === redrawingVersion` check and starts a third redraw. That supersedes B and discards its in-flight layout fetch. With steady pushes and catch-ups this repeats.
- `redrawIfChanged(...)` is also called from `applyStatus` without `await` or `.catch`. A rejection from `onCollectionChanged` would be an unhandled promise rejection, and today only `redraw`'s own catch-all prevents that.

**Fix:** Clear the guard only if it still names this redraw, and catch at the call site:
```js
try {
  const drawn = await options.onCollectionChanged();

  if (drawn !== false) {
    shownVersion = version;
  }
} catch {
  return;
} finally {
  if (redrawingVersion === version) {
    redrawingVersion = null;
  }
}
```

### WR-08: The press request has no timeout, so one hung request locks the button and opens the note

**File:** `Cabinet.Service/wwwroot/js/sync.js:372-426`
**Issue:**
- `fetch('/cabinet/sync', { method: 'POST' })` has no `AbortController` or timeout. On a stalled mobile connection it can hang for minutes.
- While it hangs, `pressing` stays `true`, so every later press returns silently (line 373). The visitor gets no sentence and no state change.
- `ownSyncPending` (set at line 390) also stays `true`. Any status with `running === false`, such as the hourly sync ending, then writes an outcome sentence into the live note. That breaks the contract rule "only a visitor's own press writes to it".

**Fix:** Abort the request after a bounded time and treat it as the offline case:
```js
const controller = new AbortController();
const timer = window.setTimeout(() => controller.abort(), PRESS_TIMEOUT_MS);

try {
  const response = await fetch('/cabinet/sync', { method: 'POST', signal: controller.signal });
  ...
} finally {
  window.clearTimeout(timer);
  pressing = false;
}
```
The existing `catch` already maps the abort to `noteOffline` and clears `ownSyncPending`.

**Info**
### IN-06: The focus restore after a redraw can scroll the page

**File:** `Cabinet.Service/wwwroot/js/cabinet.js:156-163`
**Issue:** `same.focus()` scrolls the element into view by default. The contract wants the redraw to be quiet and the keyboard position kept. If the visitor has scrolled away from the focused box, or the layout moved it, the page jumps.
**Fix:** `same.focus({ preventScroll: true });`

### IN-07: Dead and duplicated code

**File:** `Cabinet.Service/wwwroot/js/status.js:101-103`, `Cabinet.Service/wwwroot/js/copy.js:29-37` and `48`, `Cabinet.Service/wwwroot/js/cabinet.js:69-70` vs `101-102` and `189-197`, `Cabinet.Service/wwwroot/js/sync.js:290-306`
**Issue:**
- `wholeMinutesLeft` is exported and tested but no page script calls it. `waitPhrase` in copy.js re-implements the same ceil, so the tested function is not the one that runs.
- `COPY.notSynced` is used only by its own test. The server writes that text.
- The layout URL is built twice in cabinet.js.
- The status fetch exists twice, in cabinet.js `fetchStatus` and in sync.js `fetchStatus(ownPress)`.

**Fix:**
- Delete `wholeMinutesLeft` (and its test), or have `waitPhrase` use it.
- Drop `COPY.notSynced`.
- Extract one `layoutUrl()` helper.
- Let sync.js `fetchStatus` and cabinet.js share one status reader.

### IN-08: Docs gaps and one misleading sentence

**File:** `docs/cabinet-layout.md` (invented-collections section), `docs/bgg-sync.md:92-93`, `docs/development.md` (running the checks, prerequisites)
**Issue:**
- cabinet-layout.md says that with the switch off there is "no status line". The synced collection's sync status line is shown whenever no sample is shown (`ShowSyncBlock => !ShowingSample`). Only the "Invented collection of N items" line is meant.
- bgg-sync.md says pages show a note after "3 hours". That is the default of `Sync__StaleAfterHours` (1 to 168), not a fixed value.
- The live channel is not documented in the guides. There is no mention of `/cabinet/live`, the `Live:MaxConnections` cap (default 100), or that the 101st open page silently falls back to once-a-minute polling. There is also no word on what a reverse proxy needs for WebSockets or server-sent events.
- development.md's "Running the checks" omits `node --test build/tests/page-scripts.test.mjs`, which CI runs. The prerequisites do not mention Node at all.

**Fix:**
- Reword the first two statements.
- Add a short "Live updates" section to bgg-sync.md (route, cap setting, fallback behaviour, proxy note).
- Add the Node test command and a Node prerequisite to development.md.

### IN-09: The press flow has no automated coverage

**File:** `build/tests/page-scripts.test.mjs:22`
**Issue:** The node tests load only copy.js and status.js (the modules without imports). The press flow in sync.js and the reconnect loop in live.js are exactly where WR-05, WR-07 and WR-08 live, and none of it is exercised by this file. A browser-level test with a scripted status (a sync that ends before the 202 answer) would have caught WR-05.
**Fix:** Add a Playwright case (or make `initSyncStatus` testable with an injected `fetch` and `document`) for press, 202, 409, 429 and offline, including a push that arrives before the 202 body.

---

_Reviewed: 2026-10-07_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

## Part: Tests, scripts and CI


**Reviewed:** 2026-10-07
**Depth:** standard
**Files Reviewed:** 60
**Status:** issues_found

**Summary**
No blockers. The access-check script keeps its stated guarantees (bare host hard-coded in `HTTPSConnection`, certificate verification on, redirects never followed, token only on calls that opt in, hard request cap and 6 s spacing, whole-report guard before anything is printed, DOCTYPE/ENTITY bodies refused before parsing). Workflows pin every action by SHA, use `permissions: {}` at the top with per-job grants, `persist-credentials: false`, and pass tag/version values through `env:` rather than interpolating into scripts. Fixtures are synthetic; no `//` comments, block comments or planning references were found in the reviewed C#. The `#`/`//` hard rules hold in the tests.

The real problems are test-quality ones: assertions that silently turn into no-ops, two tests that cannot detect the regression their name claims, negative assertions that rely on real-time sleeps, an installer end-to-end run that disables `set -e` inside the code under test, and a repo-rules lint that does not cover several reference kinds the project rules forbid.

**Warnings**
### WR-09: `?.Should()` on a nullable header turns the assertion into a no-op

**File:** `Cabinet.IntegrationTests/CabinetPageTests.cs:42`, `:60`, `:74`; `Cabinet.IntegrationTests/CreditTests.cs:89`; `Cabinet.IntegrationTests/LayoutEndpointTests.cs:34`; `Cabinet.IntegrationTests/LivePageTests.cs:47`, `:55`; `Cabinet.IntegrationTests/SyncButtonTests.cs:86`; `Cabinet.IntegrationTests/SyncStatusLineTests.cs:147`; `Cabinet.IntegrationTests/SyncPipelineTests.cs:89`
**Issue:** `response.Content.Headers.ContentType?.MediaType.Should().Be("text/css")` short-circuits the whole chain when `ContentType` is null. If the server stops sending a content type (static-asset middleware reordered, fingerprinted route served by a fallback), the test still passes. The same applies to `second.Headers.CacheControl?.NoStore.Should().BeTrue()`, which is the only check of the 409 response's no-store header. These tests exist precisely to prove the served asset type, so a missing header is the case they must catch.
**Fix:** Assert non-null first, then the value (sibling tests in `StatusEndpointTests.cs:60-62` already use `!`):
```csharp
response.Content.Headers.ContentType.Should().NotBeNull();
response.Content.Headers.ContentType!.MediaType.Should().Be("text/css");
second.Headers.CacheControl.Should().NotBeNull();
second.Headers.CacheControl!.NoStore.Should().BeTrue();
```
Better, extract one helper (`AssertMediaType(response, expected)`) used by all ten sites.

### WR-10: The "redirect to the www host is not followed" test cannot detect an auto-redirect regression

**File:** `Cabinet.IntegrationTests/SecretsStayServerSideTests.cs:83-99` (and the `moved-to-the-www-host` row in `Cabinet.IntegrationTests/SyncFailureTests.cs:126`)
**Issue:** The test replaces the primary handler through `ConfigurePrimaryHttpMessageHandler(() => handler)`. Production wires `BggTransport.CreatePrimaryHandler` at `Cabinet.Service/Sync/SyncEndpoints.cs:74`, which is exactly the thing that sets `AllowAutoRedirect = false`. A scripted handler never follows redirects, so the assertion `handler.Requests.Should().ContainSingle()` is true whatever production does. `BggTransportTests.The_primary_handler_never_follows_redirects` only checks the factory method in isolation, not that the app registers it. Deleting line 74 of `SyncEndpoints.cs` (or switching to the default handler, which follows redirects and could carry the token to the `www` host) would leave every test green.
**Fix:** Add a test that goes through the real wiring: start the local fake server (already available via `FakeBggServer`) answering 301 with a `Location` pointing at a second counting loopback listener, build the client from the service registration without replacing the primary handler (only point `Bgg:BaseUri` at the fake, which the Development/Testing environment honours), run a sync, and assert the second listener received no request. Alternatively resolve `IHttpMessageHandlerFactory` from the booted host and assert the innermost handler is a `SocketsHttpHandler` with `AllowAutoRedirect == false`.

### WR-11: Negative assertions proven by real-time sleeps (pass under load even when behaviour is broken)

**File:** `Cabinet.UnitTests/Sync/SyncSchedulerTests.cs:30-34, 46-49, 68-72, 101-105, 138-146`; `Cabinet.UnitTests/Bgg/RequestPacerTests.cs:117-122`; `Cabinet.IntegrationTests/LiveHubTests.cs:168-174`; `Cabinet.UnitTests/FakeBgg/BggXmlTests.cs:219-221`
**Issue:** "Nothing happened" is established by sleeping a fixed 100-150 ms of real time (`SettleAsync`, `StaysPending`) and then checking state. On a busy CI box the scheduler/pacer continuation may not have run within that window, so a regression that fires too early (startup request despite a recent success, a second queued request during a run, the pacer releasing early) still passes. Conversely `TryConnectAsync` treats "not closed within 3 real seconds" as "admitted", so a slow close from the server makes the cap test report an admitted connection (false failure), and every genuinely admitted connection costs a full 3 s sleep. `BggXmlTests` checks `pending.IsCompleted` immediately after `Advance` with no wait at all.
**Fix:** Replace sleeps with an observable signal. For the scheduler, inject a coordinator spy (or a `TaskCompletionSource` completed from the request channel's writer) and use `await Task.WhenAny(signal, Task.Delay(timeout))` only for the positive case; for negatives, advance the fake clock past the next tick, then `await Task.Yield()` in a bounded loop that waits until the scheduler is parked again (for example by awaiting a second tick consumed counter). For the live cap test, wait for the specific outcome: `page.Closed` completing is the refusal; for admission, send one broadcast and wait for `Received.Count >= 1` instead of a blind 3 s.

### WR-12: Installer end-to-end test runs the installer with `set -e` suppressed

**File:** `deploy/tests/cabinet-deploy-e2e-test.sh:160-162`, `:194-196`, `:207-210`, `:219-221`
**Issue:** `(cmd_install ...) >log 2>&1 || INSTALL_EXIT=$?` (and the same for `cmd_poll`) puts the function in the left side of a `||` list. Bash ignores `errexit` for everything executed inside such a compound command, including inside the subshell and even if the code calls `set -e` itself. Production runs `cabinet-deploy` under `set -euo pipefail` (`deploy/bin/cabinet-deploy:9`). Steps in `cmd_install` that rely on errexit rather than an explicit `|| cabinet_die` (for example the bare `verify_checksum "$artifact" "$checksum_file"` call) will, in this test, fall through and continue installing, while on the server they abort. The test therefore cannot prove that a failing verification step stops the install, and it can pass against behaviour that differs from production.
**Fix:** Run the function with errexit genuinely on, outside any `||`/`if` context:
```bash
run_capturing_status() {
  local __var="$1"; shift
  local rc=0
  set +e
  ( set -e; "$@" )
  rc=$?
  set -e
  printf -v "$__var" '%s' "$rc"
}
run_capturing_status INSTALL_EXIT cmd_install v0.0.1 --from-dir "$ASSETS" >"${TMP}/install-0.0.1.log" 2>&1
```
(or invoke the installer in its own `bash` process via `bash deploy/bin/cabinet-deploy install ...` with the stubs exported through a sourced shim).

### WR-13: Repo-rules lint does not cover several reference kinds the project forbids

**File:** `build/lint/checks/10-repo-rules.sh:10-16, 51-58`
**Issue:** The hard rules forbid requirement keys, decision IDs, review-finding IDs, phase/plan/wave/milestone numbers and planning document names outside the planning folder. The lint checks requirement keys (a closed prefix list that happens to match today's requirements file), `D-NN`, `Phase N` and planning file/dir names, but not: review-finding IDs (`CR-01`, `WR-10`, `IN-12`, `BL-01`), plan numbers (`03-15`), `Plan N`, `Wave N`, `milestone`, spelled-out phases, or uppercase `PHASE 3` with a separator other than the optional class. A new requirement prefix added later is also silently unchecked. The C# comment check likewise only looks for `//`; `/* ... */` block comments in `.cs` files pass, although the rule is "`///` only". Today the tree is clean (verified by grep), so this is an enforcement gap rather than a current violation, but the lint is the only gate and CI trusts it.
**Fix:** Extend the patterns and keep self-test cases for each new one (building the bad strings by concatenation as the existing self-test does):
```bash
REVIEW_FINDING_PATTERN='\b(CR|WR|IN|BL)-[0-9]{2}\b'
PLAN_NUMBER_PATTERN='\b[0-9]{2}-[0-9]{2}\b'
PHASE_WORD_PATTERN='\b[Pp]hase[-_ ]?([0-9]+|one|two|three|four|five|six|seven|eight|nine|ten)\b'
PLAN_WAVE_PATTERN='\b([Pp]lan|[Ww]ave|[Mm]ilestone)[-_ ]?[0-9]+\b'
CS_BLOCK_COMMENT_PATTERN='/\*'
```
(allowing `/**` is not needed in C#). Derive the requirement-key prefixes from the planning requirements file at lint time instead of hard-coding them, or use a generic `\b[A-Z][A-Z0-9]{1,5}-[0-9]{2}\b` with an allowlist of known false positives.

### WR-14: Package end-to-end test aborts silently (or fragilely) because of `pipefail` in the stylesheet extraction

**File:** `build/tests/package-release-e2e-test.sh:113-114`
**Issue:** `STYLESHEET="$(grep -oE ... | grep -oE ... | head -n 1 | sed ...)"` runs under `set -euo pipefail`. If the page has no stylesheet link, the first `grep` exits 1, the pipeline fails and `errexit` ends the script at the assignment, so the friendly `fail "hello page has no stylesheet link"` on the next line is dead code and the application log is never printed. Conversely, `head -n 1` closing the pipe early can make the preceding `grep` die with SIGPIPE (status 141) when the page lists several stylesheets and the output spans more than one write, which also aborts the script even though a stylesheet exists. It works today only because the output is tiny and written in one chunk.
**Fix:** Do the selection in one tool and tolerate no-match explicitly:
```bash
STYLESHEET="$(sed -nE 's/.*<link[^>]*rel="stylesheet"[^>]*href="([^"]+)".*/\1/p' <<<"$PAGE" | sed -n '1p' || true)"
```
(or append `|| true` to the pipeline) so the `[ -n "$STYLESHEET" ] || fail ...` check is the one that reports.

### WR-15: Test host leaks on construction failure and when a loop body fails

**File:** `Cabinet.IntegrationTests/SyncStatusLineTests.cs:157-168`; `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs:95, 176-190`
**Issue:** `The_header_never_names_the_data_source` builds both factories eagerly inside the array initialiser (two hosts, real Kestrel listeners, a scheduler), then disposes each only inside its own loop iteration. If the first iteration's assertion fails, the second factory is never disposed and its storage directory is deleted underneath it by `using var storage`; if building the second throws, the first leaks. In the factory itself, `StartOnFreePorts()` is called from the constructor, so a failure on the final attempt throws out of the constructor and no caller can dispose it; and after `_realHost = realHost` is assigned in `CreateHost`, a throw from `testHost.Start()` or `WaitUntilApplicationStarted` (the 30 s timeout) leaves a running real host that nothing owns. Leaked Kestrel hosts keep ports and background services alive into later tests in the same process.
**Fix:** Make the test two sequential `await using` blocks (or a local async function taking a factory-producing delegate). In `CreateHost`, wrap everything after `_realHost = realHost` in the existing `try`/`catch` so a failure disposes `realHost` and `testHost` and clears `_realHost`; in the constructor, catch, call `Dispose()` on this instance and rethrow.

### WR-16: Weak assertions that would pass if the behaviour broke

**File:** `Cabinet.IntegrationTests/LiveHubTests.cs:76-84`; `Cabinet.IntegrationTests/SecretsStayServerSideTests.cs:127-143`; `Cabinet.IntegrationTests/LiveHubTests.cs:50-60`
**Issue:** `A_client_restricted_to_long_polling_cannot_connect` asserts `ThrowAsync<Exception>()`, which passes for any failure (host not up, refused connection, bug in the test itself), not for "the server does not offer long polling". `Visitors_reading_the_page_and_the_layout_cause_no_bgg_request` runs against a host whose background sync is disabled and checks the request count right after the loop, so a visitor-triggered asynchronous sync that finishes after the check would be missed; with background sync off and no wait, it mostly proves nothing about visitors. `No_method_can_be_invoked_on_the_hub_and_nothing_reaches_BGG` checks `handler.Requests.Should().BeEmpty()` immediately, with the same timing blind spot (the hub exception itself is the meaningful assertion there).
**Fix:** For long polling, assert on the specific failure (for example the negotiate response lacks `LongPolling` in `availableTransports`, via `POST /cabinet/live/negotiate?negotiateVersion=1`, or the exception message names the transport). For the visitor test, issue the visits, then advance the fake clock past the interval and cooldown on a host with `Sync:BackgroundEnabled=true` and assert still no requests, or wait on `SyncHarness.WaitUntil` for "status unchanged" before asserting. Drop the trailing immediate-empty assertion in the hub test or precede it with a short bounded wait on a status poll.

**Info**
### IN-10: Hand-rolled `ManualTimeProvider` duplicates `FakeTimeProvider` and is not thread-safe

**File:** `Cabinet.UnitTests/FakeBgg/BggXmlTests.cs:294-344`
**Issue:** The unit-test project already references `Microsoft.Extensions.TimeProvider.Testing` and uses `FakeTimeProvider` in the sibling tests. The private provider mutates a plain `List<ManualTimer>` and a non-volatile `_now` from the handler's thread (`CreateTimer`) while the test thread reads `TimerCount` and enumerates in `Advance`, a data race; `Change` ignores its arguments, so any later `Change` call is silently dropped; there is no `GetTimestamp` override.
**Fix:** Delete the class and use `new FakeTimeProvider(start)`; wait for the pending timer with the provider's own scheduling, for example loop until `fake.GetTimerCount()`-style state or just `Advance` after `Task.Yield` as in `QueuedAnswerTests.Drive`.

### IN-11: Duplicated test helpers

**File:** `Cabinet.IntegrationTests/CollectionFidelityTests.cs:181-189`, `Cabinet.IntegrationTests/SyncPipelineTests.cs:573-581` (private `TemporaryDirectory` copies of `Infrastructure/TemporaryDirectory.cs`); `Cabinet.IntegrationTests/CollectionFidelityTests.cs:146-170`, `SyncPipelineTests.cs:583-596`, `SecretsStayServerSideTests.cs:214-227` (own `WaitUntil` loops, with 10 s, 15 s and 15 s deadlines, instead of `SyncHarness.WaitUntil`); `Cabinet.UnitTests/Sync/BggSettingsTests.cs:169-177` and `Cabinet.UnitTests/Bgg/BggTestKit.cs:8-20` (`ImmediatePacer` mirrors `NoWaitPacer`); `FindServiceDirectory` copied into `LiveSettingsTests`, `SampleCatalogTests` and `SyncSettingsTests`.
**Fix:** Use the shared `Infrastructure` types and a single `FindServiceDirectory` helper (a small `RepositoryPaths` class in the unit-test project). Differing timeouts make the polling behave inconsistently across files.

### IN-12: Tests build `HostingEnvironment` from an internal namespace

**File:** `Cabinet.UnitTests/Prototype/SampleCatalogTests.cs:4, 83`; `Cabinet.UnitTests/Sync/BggSettingsTests.cs:7, 144`
**Issue:** `Microsoft.Extensions.Hosting.Internal.HostingEnvironment` is framework-internal and may change or disappear between .NET releases; both production parsers only need `IHostEnvironment`.
**Fix:** Use a three-line private `TestEnvironment : IHostEnvironment` (or NSubstitute-free stub) in the test project instead.

### IN-13: Access-check guard can make the tool unusable for a short username, and `run_check`'s credential list is untested

**File:** `build/bgg-access-check.py:200-208, 734-735`; `build/tests/bgg-access-check-test.sh`
**Issue:** `report_leaks` withholds the report when the lower-cased username appears anywhere as a substring. For a username of two or three letters (or a dictionary word such as one that appears in "stats", "status", "items") every run exits 4 with no output. This fails safe, so it is not a leak, but the operator gets no shape data and no hint why. Separately, the offline test suite exercises the guard only through `run_self_test` with sentinel credentials; the list actually passed in `run_check` (`[token, username, contact, WRONG_TOKEN]`) has no test, so dropping one of them would not be caught.
**Fix:** Apply whole-word matching to short credentials the way `contains_value` does for values under four characters, and print a distinct hint ("output withheld: it would have contained a sensitive value (username or token)") only in the withheld branch. Add a shell test that runs `--run` with a stubbed transport (the `NO_NETWORK` harness can swap `Transport.exchange`) and asserts exit 4 when the stub echoes the username or token.

### IN-14: Package end-to-end test has no self-gate; CI runs workflows on both push and pull request

**File:** `build/tests/package-release-e2e-test.sh:1-8`; `.github/workflows/ci.yml:3-7, 48-51`
**Issue:** The installer end-to-end script exits early unless `CABINET_E2E=1`, and the lint runner skips `*-e2e-test.sh` unless the variable is set; the packaging end-to-end script has no such guard, so running it directly (or from a different runner) builds a full release and starts the app regardless. In the workflow, `push: {}` plus `pull_request` on `main` runs the full build, test, lint and two end-to-end jobs twice for every pull-request update from an in-repo branch (the concurrency group differs between `refs/heads/...` and `refs/pull/...`).
**Fix:** Add the same `CABINET_E2E` guard at the top of the packaging script for consistency. Restrict `push` to `main` and tags (`push: { branches: [main] }`) so pull-request branches are only built once.

---

_Reviewed: 2026-10-07_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

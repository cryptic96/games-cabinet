---
phase: 03-bgg-access-spike-real-sync-snapshot
fixed_at: 2026-10-07T00:00:00Z
iteration: 1
findings_in_scope: 4
fixed: 3
skipped: 0
changed_approach: 1
status: all_addressed
---

# Phase 3: Audit Fix Report (back end)

**Scope:** the back-end items of the security audit: T-03-59 (server side), the double fault (T-03-28 / T-03-41), the unpinned resource limits (T-03-24, T-03-26, T-03-35), and docs/bgg-sync.md.

**Verification:** run in the isolated worktree after each item: `dotnet test --solution Cabinet.slnx` (934 tests after the last code commit, 0 failed) and `build/lint.sh repo-rules` (pass).

## Item 1: Hub refusal at the connection cap (T-03-59, server side)

**Status:** investigated; the premise did not hold, so the refusal was kept and pinned by tests instead of replaced.
**Commit:** d49e709

**Evidence (measured, not read):**
- Raw WebSocket client against the running hub with a cap of 1: the refusal by `Context.Abort()` already sends `{"type":7}` (a close message with no `allowReconnect`, so no permission to reconnect), then closes.
- The vendored JavaScript client, run in Node with automatic reconnect (a throwaway script, not committed): `Close message received from server`, `onclose` fired, one negotiate request, no `onreconnecting`. The audit's "the framework's close allows a reconnect" does not match SignalR 10 behaviour.
- A `HubException` from the connect callback also sends a close without reconnect permission, but adds an error text to the message and makes the framework log an error with a stack trace for every refused page, which a flood would turn into log noise on the low-power host. So the change was not made.
- Over WebSockets, Kestrel's own upgraded-connection cap refuses the extra connection before the hub is reached (the page's start fails); the hub-level refusal is reached over Server-Sent Events, which is also where a browser falls back to.

**What was done:** `CabinetHub` doc comment now states the behaviour and the reason. New `Cabinet.IntegrationTests/LiveCapRefusalTests.cs` (cap of 1, real Kestrel, a .NET client with `WithAutomaticReconnect`): the refused page's connection ends after a single hub connection attempt (counted by a hub filter, not by sleeping), with no reconnecting or reconnected event, and its `Closed` signal drives the wait; the first page stays connected and still receives broadcasts; the limiter count stays at 1 (the refused connection took no place and freed none of the first's); after the first leaves, a new page is admitted. A wire-level test pins that the close message does not allow reconnecting.

**Note for the owner:** the audit item's proposed server-side fix is therefore not needed; the reconnect loop it describes is not reproduced. The page's own back-off in the browser script is what governs retries after a refusal.

## Item 2: Double fault (unreadable snapshot at start-up)

**Status:** fixed: requires human verification (integrity-check logic)
**Commit:** f23a2ed

**Rule:** the store remembers whether the file on disk exists but was not read (`ISnapshotStore.HasUnreadStoredCollection`, in memory only; a damaged file that was moved aside does not count). Each sync first tries to read the file again; if that works it becomes the shown baseline and the ordinary guard applies. While it still cannot be read the guard runs without a baseline: an empty answer is held back every time (kind `empty`); any other answer is held back (new kind `unverified`) until the next fetch returns exactly the same set of entries. The held-back record is the existing persisted one, so nothing new is stored and the confirmation survives a restart. A failed fetch changes nothing.

**Files:** `Cabinet.Domain/Collection/ShrinkGuard.cs`, `SyncState.cs`, `CollectionSnapshot.cs` (default interface member, so existing fakes are unchanged), `Cabinet.Repository/Storage/SnapshotStore.cs`, `Cabinet.Service/Sync/SyncRunner.cs`.
**Tests:** `Cabinet.UnitTests/Sync/UnreadableSnapshotTests.cs` (real store, locked file, fake clock: empty never replaces, unconfirmed answer held, identical second answer accepted then ordinary rules, different answer does not confirm, confirmation across a restart, file readable again restores the baseline, no file and damaged-file cases unchanged, failed fetch), `ShrinkGuardUnreadableTests.cs`, and two cases in `Snapshot/SnapshotStoreTests.cs`. Six of the new runner tests fail when the runner ignores the flag.

## Item 3: Tests pinning resource limits

**Status:** fixed
**Commit:** 72a9236

- Log level: `Cabinet.UnitTests/Configuration/CommittedLogLevelTests.cs` loads the committed settings (base, Development, Production) into a real logger factory and asserts the HTTP client request categories are disabled at Trace, Debug and Information and enabled at Warning; and that the committed `System.Net.Http.HttpClient` level is Warning or higher.
- 20 MB transport limit: `Cabinet.IntegrationTests/OversizedAnswerTests.cs` serves an answer over the limit through the production client configuration; the sync fails, the layout and the stored file are unchanged. **The failure category is `Unavailable`, not `BadAnswer`**: the transport refuses the body before the reader sees it, and the client maps that to unavailable. The test pins that (it also shows the transport limit, not the reader, is what fires first).
- XML limit: `Cabinet.UnitTests/Bgg/BggDocumentSizeLimitTests.cs`: a document just over 20 million characters raises the malformed-XML error (which the client maps to `BadAnswer`), one just under is read. The XML limit cannot be reached through the transport, since bytes are never fewer than characters.

## Item 4: Docs

**Status:** fixed
**Commit:** e5e363e

`docs/bgg-sync.md`: the cap-row sentence about refused pages now says the page is closed right after connecting without permission to reconnect by itself, follows the slow retry schedule and uses the one-minute status check meanwhile (it does not simply stop; the browser script keeps trying gently). Added the reconnect schedule as asked (brief randomised first retry of a fraction of a second to about a second, then about 2 s, 10 s and 30 s, then about every 60 s, each varied by up to 20 percent). Documented the unreadable-snapshot rule in "Where the data lives".

---

_Fixer: Claude (gsd-code-fixer)_

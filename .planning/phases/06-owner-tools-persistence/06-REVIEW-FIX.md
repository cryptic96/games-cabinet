---
phase: 06-owner-tools-persistence
fixed_at: 2026-10-13T00:00:00Z
review_path: .planning/phases/06-owner-tools-persistence/06-REVIEW.md
iteration: 1
findings_in_scope: 13
fixed: 13
skipped: 0
status: all_fixed
---

# Phase 6: Code Review Fix Report

**Fixed at:** 2026-10-13
**Source review:** .planning/phases/06-owner-tools-persistence/06-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 13 (CR-01, WR-01 to WR-12)
- Fixed: 13
- Skipped: 0

WR-01, WR-02, WR-03, WR-04 and WR-11 follow the guidance of the parallel security audit (ASVS L2) where it differs from the review's own suggestion. Where that is the case it is said below.

Verification ran in the isolated worktree, not the main checkout, so the numbers are not reproducible from the main checkout after the worktree is removed: `dotnet build Cabinet.slnx` clean; unit tests 2407 passed; integration tests 452 passed; browser tests 318 passed, both with the default fonts and with the DejaVu fontconfig mapping; `node --test build/tests/*.test.mjs` 355 passed; `10-repo-rules.sh`, `40-secrets.sh` (full history and working tree) and `50-script-tests.sh` pass.

## Fixed Issues

### CR-01: Finish picker drops a second choice while a save is in flight and never reverts a failed one

**Files modified:** `Cabinet.Service/wwwroot/js/owner-finish.js`, `build/tests/owner-finish.test.mjs`
**Commit:** af644cb
**Applied fix:** A tile chosen while a save is under way is saved after it, and a failed save puts the tile of the last stored finish back. The script remembers the stored value. New node tests cover the queued second choice, the revert, a revert after an earlier success and choosing the checked tile again.
**Status:** fixed: requires human verification (state handling logic).

### WR-01: Client-range wall passes the proxy's own address when no forwarded address was applied

**Files modified:** `Cabinet.Service/Owner/OwnerAccess.cs`, `Cabinet.Service/Hosting/ForwardedHeadersSetup.cs`, `Cabinet.Service/Program.cs`, `deploy/bin/cabinet-selfcheck`, `deploy/tests/sandboxing-test.sh`, `Cabinet.IntegrationTests/Infrastructure/UntrustedPeerHost.cs`, `Cabinet.IntegrationTests/OwnerWallTests.cs`, `Cabinet.UnitTests/Hosting/OwnerAccessTests.cs`
**Commits:** 66a46db (first attempt, replaced), d52ff53 (the audit's design)
**Applied fix:** Follows the security audit, not the review's `X-Original-For` suggestion (a peer can send that header itself). A small middleware before the forwarded-headers step records the connection's own peer address. The owner client-range wall on the owner port now refuses unless that peer is one of the configured known proxies (a wrong known-proxy value fails closed) and the resolved client is in range; in Production it also refuses when the resolved client equals the peer. The framework's default loopback trust is cleared before the configured proxies are added, so the setup's doc comment is true. Development and test hosts keep working because the test factory configures its known proxy and runs outside Production. The self-check's probe line now says "the owner port refuses a request from loopback" and its comment says it proves the refusal, not the forwarded path (IN-04). New tests: known proxy with no usable forwarded address is refused, an in-range peer that is not the configured proxy is refused with and without a header, loopback is not believed unless configured, known-proxy matching unit tests.
**Status:** fixed: requires human verification (access-control logic).

### WR-02: A restored file's revision is unbounded

**Files modified:** `Cabinet.Repository/Storage/OwnerDataExport.cs`, `Cabinet.Service/Owner/OwnerDataService.cs`, `Cabinet.UnitTests/Owner/OwnerRestoreParserTests.cs`
**Commits:** e323c45, 8a09416
**Applied fix:** `WithinLimits` refuses a revision above 1,073,741,823 (far below 2^53-1, so a script can always send it back exactly) and a change count above 200,000. `NextRevision` saturates and the change counter saturates at the integer maximum on save and restore. Tests refuse `long.MaxValue`, 2^53+1, and the two limits plus one, with the over-limits refusal; the limits themselves are accepted.
**Status:** fixed: requires human verification.

### WR-03: Start-up reconcile treats a stale stored collection as an accepted one

**Files modified:** `Cabinet.Service/Owner/CollectionPublisher.cs`, `Cabinet.Service/Sync/SyncStartup.cs`, `Cabinet.Service/Sync/SyncRunner.cs`, `Cabinet.UnitTests/Owner/OwnerExpiryTests.cs`
**Commits:** a203ffa, 4483ea7
**Applied fix:** `Publish` has a `reconcile` flag (default true). The start-up load and the reload of an unreadable stored collection pass false; only a freshly fetched, accepted collection reconciles. Tests: a collection read back from disk neither stamps nor removes a game, and after a restart a game stamped absent 31 days ago keeps its owner data until a fetched collection is judged.
**Status:** fixed: requires human verification.

### WR-04: A failed cache write makes the link worker refetch the picture on every wake

**Files modified:** `Cabinet.Service/Owner/OwnerLinkWorker.cs`, `Cabinet.UnitTests/Owner/OwnerLinkWorkerTests.cs`
**Commits:** 84894d1 (first attempt, replaced), 1ada4f5 (the audit's in-memory backoff)
**Applied fix:** Follows the security audit: setting the last-checked time alone does not help because a link with no failures and no usable copy is always due. The worker now sets the last-checked time before the write and remembers a store failure in memory per game and address. A link whose copy could not be stored waits `LinkHealth.DeadAfterSpan` before it is fetched again unless something asks for it, and a different address is never held back. Nothing is recorded on disk, since that can fail on a full disk too. Test: with the art directory gone, one fetch happens, an hour later no more, and after a day another.
**Status:** fixed: requires human verification.

### WR-05: "confirm-needed" has no words and is not a known refusal key

**Files modified:** `Cabinet.Service/Owner/OwnerResponses.cs`, `Cabinet.Service/Owner/OwnerStrings.cs`, `Cabinet.UnitTests/Owner/OwnerRefusalCodeTests.cs`, `Cabinet.IntegrationTests/OwnerLocationListPostTests.cs`
**Commit:** f5cc9d9
**Applied fix:** Added the key to the known refusals and the words "This location still holds games. Confirm the delete first." The script answer now carries the message, and a form redirect keeps its own code.

### WR-06: gitleaks allowlist for `.md`/`.cshtml` also lets a real address with a capitalised domain through

**Files modified:** `.gitleaks.toml`, `build/lint/checks/40-secrets.sh`
**Commit:** 556cd40
**Applied fix:** The Razor allowance now needs the text before the at sign to end in a hyphen (`^[A-Za-z0-9-]*-@...`), the way markup glues a class name to a member access. The review report itself quotes three made-up capitalised addresses and was committed before this fix, so those three are allowed by exact match; no other capitalised address is. Self-tests added: both known history shapes are accepted and a capitalised address in a `.md` and a `.cshtml` file is rejected (the test builds the address at run time so the script does not contain one). Full history and working tree scans pass.

### WR-07: Deploy self-check never verifies the firewall half of the owner wall

**Files modified:** `deploy/bin/cabinet-selfcheck`, `deploy/tests/sandboxing-test.sh`
**Commit:** dba337c
**Applied fix:** When port 5082 listens, `check_firewall` also requires a rule that admits it from one source address and fails when the rule is missing, has no source address or admits `0.0.0.0/0`. It skips when the owner tools are off. Offline tests cover all five cases and that no address is printed.

### WR-08: Undo posts the run-time revision

**Files modified:** `Cabinet.Service/wwwroot/js/owner-shared.js`, `owner-locations.js`, `owner-placing.js`, `owner-pictures.js`, `owner-link.js`, `owner-games.js`, `build/tests/owner-shared.test.mjs`
**Commit:** 8aa4df6
**Applied fix:** `showRowStatus` remembers the revision of the save its Undo button follows and hands it to the undo action, and every undoer sends it with the undo request, so a change made meanwhile makes the server answer that the change can't be undone. Node tests cover the captured revision and the request body.

### WR-09: The keepalive flush goes through the single-flight queue

**Files modified:** `Cabinet.Service/wwwroot/js/owner-games.js`, `owner-pictures.js`, `build/tests/owner-pickers.test.mjs`
**Commit:** a11d2b9
**Applied fix:** A save that must outlive the page is sent at once, outside the queue. Ordinary saves still wait their turn. New node tests run the real picker script against a stand-in page.

### WR-10: Double activation produces false errors (Undo, Add location, Use this)

**Files modified:** `owner-shared.js` (with the WR-08 commit), `owner-locations.js`, `owner-link.js`, `Cabinet.BrowserTests/OwnerLocationListTests.cs`, `build/tests/owner-shared.test.mjs`
**Commit:** 45513d4; the Undo part landed in the WR-08 commit because it is the same function
**Applied fix:** Undo ignores a second press while the first is being handled; Add location reads the name when the form is submitted and ignores a second submit while the first is under way; Use this marks its button taken at click time. Browser tests press Enter twice on the add form and double-click Undo against a delayed server and check one request and no error.

### WR-11: Owner scripts and stylesheet are served on the public port

**Files modified:** `Cabinet.Service/Owner/OwnerAccess.cs`, `Cabinet.Service/Program.cs`, `Cabinet.IntegrationTests/OwnerWallTests.cs`, `Cabinet.UnitTests/Hosting/OwnerAccessTests.cs`
**Commits:** e643276, e3be3b6
**Applied fix:** The static asset endpoints named like an owner script or the owner stylesheet (including fingerprinted copies) carry the owner-only marker, so the existing owner-only wall answers an empty 404 off the owner port, with no file moves. Tests: every such endpoint carries the marker; the public port answers 404 and the owner port 200 for the scripts and the stylesheet; the leak test no longer leaves owner scripts out and instead fetches every script from the public port, expecting 404 for the owner ones.

### WR-12: Backup banner button name does not contain its visible text on phones

**Files modified:** `Cabinet.Service/Pages/Shared/_OwnerBanner.cshtml`, `Cabinet.BrowserTests/OwnerSettingsTests.cs`
**Commit:** c318f29
**Applied fix:** Removed the fixed label and the `aria-hidden` on the short text, so the visible text is the name at every width ("Download owner data" wide, "Download data" on phones). The phone browser test now expects the short name.

## Skipped Issues

None.

## Notes

- The first browser run after the WR-10 tests hit one "Internal CLR error" crash of the test host while no test had failed; two later full browser runs passed with 318 of 318. It was not reproduced.
- Info findings (IN-01 to IN-14) are out of scope for this run.

---

_Fixed: 2026-10-13_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

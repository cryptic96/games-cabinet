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

---

# Iteration 2 (info findings)

**Fixed at:** 2026-10-13
**Source review:** .planning/phases/06-owner-tools-persistence/06-REVIEW.md
**Iteration:** 2

**Summary:**
- Findings in scope: 14 (IN-01 to IN-14)
- Fixed: 13 (IN-04 was already fixed by the first pass; IN-11 is fixed in part, see below)
- Skipped: 0 outright; one part of IN-11 left as it is on purpose

Verification ran in the isolated worktree, not the main checkout, so the numbers are not reproducible from the main checkout after the worktree is removed. The history scan needed the main repository's git directory mounted (`LINT_GIT_COMMON_DIR`), otherwise it scans zero commits from a worktree. Results: `dotnet build Cabinet.slnx` clean (0 warnings); unit tests 2421 passed; integration tests 452 passed; browser tests 322 passed with the default fonts and 322 passed with the DejaVu fontconfig mapping; `node --test build/tests/*.test.mjs` 355 passed; `10-repo-rules.sh`, `40-secrets.sh` (727 commits, every ref, and the working tree) and `50-script-tests.sh` pass.

## Fixed Issues

### IN-01: Owner-data loader checks null elements only one level deep

**Files modified:** `Cabinet.Repository/Storage/OwnerDataStore.cs`, `Cabinet.UnitTests/Owner/OwnerDataStoreTests.cs`
**Commit:** 09118ce
**Applied fix:** `HasNullMember` also treats a null entry in a stored picture's file list as malformed, so the file is reported unreadable instead of failing later in the link worker or the art clean-up. The test sends a full game with a link picture whose file list holds `null` (unreadable), a real entry and an empty list (both readable), so the readable cases prove the shape itself parses.

### IN-02: Name check does not refuse invisible format characters

**Files modified:** `Cabinet.Domain/Owner/LocationEdits.cs`, `Cabinet.UnitTests/Owner/LocationNamesTests.cs`
**Commit:** e398c67
**Applied fix:** A name is refused, with the existing "ForbiddenCharacter" reason and its existing words, when any character is a control character or has the Unicode Format category. The check walks runes, so a format character outside the basic plane (tag characters) is refused too. This also covers the two bidi ranges the old check listed. Tests added for LRM, RLM, ALM, zero-width space, non-joiner and joiner, word joiner, byte order mark, soft hyphen and a tag character; Hebrew and accented names still pass.

### IN-03: Restore accepts internally inconsistent picture settings

**Files modified:** `Cabinet.Repository/Storage/OwnerDataExport.cs`, `Cabinet.UnitTests/Owner/OwnerRestoreParserTests.cs`
**Commit:** 103a5e7
**Applied fix:** `PictureIsAcceptable` refuses a `Link` choice that has no link, with the same over-limits refusal the other bad shapes get. A game that chooses another picture with no link is still accepted. Test covers both.

### IN-04: Self-check "forged address" probe depends on a default that is not documented

**Already fixed by d52ff53** (the first pass's WR-01 change). The probe line now reads "the owner port refuses a request from loopback", and its comment says it proves the refusal and not the forwarded path, which the integration tests prove. The framework's default loopback trust is cleared in the forwarded-headers setup, so the comment is true. No change in this pass.

### IN-05: Duplicate JSON-accept helper

**Files modified:** `Cabinet.Service/Pages/Owner/Locations.cshtml.cs`
**Commit:** a3f57ab
**Applied fix:** The locations page calls `OwnerResponses.AsksForJson(Request)`; its own copy and the media-type constant only it used are gone. Covered by the existing locations tests.

### IN-06: Gitleaks path allowlists skip content anywhere in the tree

**Files modified:** `.gitleaks.toml`, `build/lint/checks/40-secrets.sh`
**Commit:** 6853a81
**Applied fix:**
- The three build-output path allowlists and the worktree allowlist are anchored: `^Cabinet.X/(bin|obj)/`, `^artifacts/`, `^(Cabinet.X/)?TestResults/` and `^.claude/worktrees/`. A directory with one of those names anywhere else is now scanned. Each pattern also accepts an optional `/repo/` prefix, because a working-tree scan reports absolute paths under the mounted source directory (found by trying the anchored form on a tree that had build output: it stopped skipping `bin` and `obj`).
- Three rules were added: RFC 6598 shared address space (100.64.0.0/10) as a host address and as a CIDR range, and IPv6 unique-local (fc00::/7) and link-local (fe80::/10) addresses. The whole 100.64.0.0/10 block and the bare block starts `fc00::`, `fd00::` and `fe80::` stay allowed, because the review notes quote them; RFC 5737 and RFC 3849 documentation addresses do not match any rule.
- Link-local IPv4 (169.254.0.0/16) was not added: nothing in the tree or history uses it, and the owner network setups the review names are covered by the ranges above. It can be added later without any history impact.
- Before adding the rules the history (718 commits at the time) and the tree were searched for those shapes; only the review report itself matched, through the allowed whole-block spellings. The full history and working-tree scans still pass.
- Self-tests: generated shared-space and IPv6 addresses are refused; addresses just below and above the shared block, the whole block, an IPv6 documentation address and `fd00::/8` are accepted; a private address under the real top-level `artifacts/` and a project's own `obj/` is skipped, and the same under `docs/artifacts/` and `tools/Cabinet.X/bin/` is refused. All test addresses are built at run time so the script itself holds none.

### IN-07: Bulk move acts on ticked rows that are hidden by the search or the unplaced filter

**Files modified:** `Cabinet.Service/wwwroot/js/owner-games.js`, `Cabinet.BrowserTests/OwnerPlacingTests.cs`
**Commit:** 5b509c6
**Applied fix:** The simpler option: every time the search or the not-placed switch is applied, the ticks of the rows it just hid are cleared and one change event tells the dock, so the count, the select-all box and the move all agree with what the owner can see. Ticks on shown rows are kept. Two browser tests: ticking a game, searching for another, ticking that one and moving moves only the shown game; ticking two games and switching on "not placed" unticks the placed one and the dock reads "1 selected". Both tests fail without the change.
**Status:** fixed: requires human verification (selection behaviour).

### IN-08: A location row added by script keeps the template's stale revision

**Files modified:** `Cabinet.Service/wwwroot/js/owner-locations.js`, `Cabinet.BrowserTests/OwnerLocationListTests.cs`
**Commit:** 218f49f
**Applied fix:** `setRevision` now runs after the new row is appended, so the row's own forms carry the revision the add was saved at. Browser test adds a location and checks every revision field of the page equals the server's revision; it fails without the change.

### IN-09: `shortAddress` can throw, and the preview token is not validated

**Files modified:** `Cabinet.Service/wwwroot/js/owner-format.js`, `Cabinet.Service/wwwroot/js/owner-link.js`, `build/tests/owner-format.test.mjs`, `build/tests/owner-link.test.mjs`
**Commit:** 8c77c38
**Applied fix:** A malformed percent sequence in the file name falls back to the raw segment instead of throwing. `checkedPreview` refuses an answer whose token is missing or is not the last segment of the preview address. Node tests cover both.

### IN-10: Dead code in `owner-format.js`

**Files modified:** `Cabinet.Service/wwwroot/js/owner-format.js`, `build/tests/owner-format.test.mjs`
**Commit:** 0555987
**Applied fix:** Removed `megabytes` and `deleteDialogBody`, the text entries `up`, `down`, `keepIt`, `deleteLocation`, `noLocations`, `removeLink`, `downloadData`, `downloadOwnerData` and `downloadFailed`, and the second `checking` entry; `changesBanner`, `compactBanner` and `tinyBanner` are no longer exported. Each name was searched in the scripts, pages and tests first; only node tests referenced them. Those tests were removed or rewritten: the banner wording is now covered through `bannerTexts` in the singular and the plural at every width, and the assertions on entries that remain were kept. The copy-parity test covers a different file (the public page's table), so no string it requires was touched.

### IN-11: Duplicated logic across the owner scripts

**Files modified:** `Cabinet.Service/wwwroot/js/owner-shared.js`, `owner-finish.js`, `owner-locations.js`, `owner-pictures.js`, `owner-placing.js`, `owner-link.js`, `build/tests/owner-shared.test.mjs`, `build/tests/owner-finish.test.mjs`
**Commit:** 2ed7970
**Applied fix:** Moved into `owner-shared.js` and used from there: `showProblem` (the locations and pictures copies were identical; the finish panel keeps its one extra case, a refusal that carries the server's words, and hands the rest to the shared one), `checkedValue`, `setChecked` (also replacing three inline loops in the link script and the finish copy), `dismissOnBackdrop` (locations dialog and the move dialog), the colour pattern `HEX_COLOUR` (three copies) and the key-window and settle times. The shared module now imports the text table from `owner-format.js`, so the two node test loaders that read it alone point that import at its own data URL. New node tests cover the shared problem text, the radio helpers, the backdrop dismiss and the colour pattern.
**Left as it is, on purpose:** generalising `saveSettledValue` to a group of radios. The picture row's copy also flushes on form submit and when focus leaves the whole group, and its flush is shared with the page's background hook, so it is not the same logic as the picker's and merging them would restructure the module. The two share the timing constants now, which is the identical part.
**Status:** fixed: requires human verification (shared behaviour across several pages; the whole owner browser suite passes).

### IN-12: Language toggle markup is duplicated in `Index.cshtml`

**Files modified:** `Cabinet.Service/Pages/Index.cshtml`
**Commit:** 746fb32
**Applied fix:** The toggle is written once as an inline template and rendered in both places (inside the owner-port wrapper, and alone on the public port). The rendered markup is unchanged; the language, owner shell and cabinet tests pass.

### IN-13: Empty header cell in the restore comparison table

**Files modified:** `Cabinet.Service/Owner/OwnerStrings.cs`, `Cabinet.Service/Pages/Shared/_OwnerRestorePanel.cshtml`, `Cabinet.BrowserTests/OwnerRestoreTests.cs`
**Commit:** a706d00
**Applied fix:** The corner cell is now a column header with visually hidden text "What is compared" (new string). The browser test lists three column headers.

### IN-14: The restore check button re-enables while the browser is navigating to the preview

**Files modified:** `Cabinet.Service/wwwroot/js/owner-settings.js`, `Cabinet.BrowserTests/OwnerRestoreTests.cs`
**Commit:** d18bdb4
**Applied fix:** The button is only cleared when the check did not lead to the preview. Browser test watches the button's busy attribute through the check and the navigation and requires it never to clear; it fails without the change.

## Skipped Issues

None.

---

_Fixed: 2026-10-13_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 2_

---
phase: 06-owner-tools-persistence
verified: 2026-10-13T00:00:00Z
status: human_needed
score: 4/4 must-haves verified
behavior_unverified: 0
overrides_applied: 0
re_verification: false
gaps: []
deferred: []
human_verification:
  - test: "Download a real backup of the owner data from the live owner host and store it somewhere off the container"
    expected: "A readable JSON file saved off the container; the banner switches to the downloaded state. Until this happens no backup of the real data exists, because the backup is the owner's own download (no scheduled job), by decision."
    why_human: "The backup is a manual export by design. Code and tests prove the export and the restore work, but only the owner can make and keep the file, and no record shows the owner has done it on the live host."
  - test: "Run one real round trip on the live host: download, then Restore from file, check the three counts in the preview, keep the current data"
    expected: "The preview shows the same locations, placed games and picture choices as the live data, and nothing changes when the owner keeps the current data."
    why_human: "The owner's first-session answer was a general 'it seems to work' with no per-check yes or no. The round trip is proven in-process (download from one server restored into a fresh server on a fresh state directory) but not itemised on the real container."
  - test: "From a device outside the home network and outside the VPN, request the owner hostname and the public hostname's owner paths"
    expected: "The owner hostname is refused by the proxy's address allow-list; the public hostname answers 404 for every owner path."
    why_human: "The route check and the container self-check were run from the home network. Only a request from the open internet proves the proxy-side wall. This is cheap to do before the site is opened to everyone."
---

# Phase 6: Owner Tools & Persistence Verification Report

**Phase Goal:** From the home network or VPN only, the owner can correct box images (and, if BGG cannot supply storage locations, define and assign them), with that data surviving syncs, restarts and releases and being backed up.
**Verified:** 2026-10-13
**Status:** human_needed
**Re-verification:** No, initial verification

## Goal Achievement

The goal is achieved in the code and in the running deployment as far as read-only checks can show. All four roadmap success criteria hold. No criterion is broken by a code review finding. Three items remain for the owner to confirm in real life (above), none of which is a code defect.

### Observable Truths (roadmap success criteria)

| # | Truth | Status | Evidence |
|---|-------|--------|----------|
| 1 | From the home network or VPN the owner can override a game's image with the owned version's image, the base game's image, or a BGG gallery image by link; cabinet and detail card show it; non-BGG links are rejected | VERIFIED | `PictureChoice` has Automatic, OwnedEdition, MainPicture and Link (`Cabinet.Domain/Owner/OwnerData.cs`). `SnapshotMapper` forces the art from each choice (`ForcedArt` at lines 303-305). `CollectionPublisher.Replace` builds the visitor view with `OwnerOverlay.From(...)`, and `CardRecords` takes its cover from the same mapped item's `Art`, so cabinet and detail card cannot disagree. `OwnerLinkFetcher.CheckAsync` classifies in order: blank, not a web address, a game-site page, other host (`options.Policy.Allows`), and only then fetches through the token-less source; each refusal is its own `LinkProblem`. Tests: `OwnerPictureChoiceTests.Choosing_the_main_picture_changes_the_card_cover...`, `OwnerLinkFetcherTests` (other-host refusal), `OwnerLinkTests.A_stored_link_with_no_copy_is_fetched_without_credentials_and_every_card_shows_it`, restore test `A_link_on_another_host_in_an_uploaded_file_is_refused_and_never_fetched`. |
| 2 | Owner screens and endpoints answer only to home-network or VPN clients; a request through the public route is refused | VERIFIED | Four independent layers. (a) Separate owner listener on its own port, with the start-up refusing a half-configured owner endpoint, a shared port, no range or no known proxy (`OwnerAccess.FromConfiguration`). (b) Path guard: a first segment of `owner` is an empty 404 on every other port (`UseOwnerPathGuard`). (c) Endpoint marker: every page under the owner folder and the preview route carry `OwnerOnlyAttribute` and are 404 off the owner port (`UseOwnerOnlyRoutes`). (d) Client-range wall: on the owner port a client outside the configured ranges gets 403 (`UseOwnerClientRange`, placed after forwarded headers). Outside the application: nftables admits 5082 only from the proxy address (`deploy/nftables/cabinet.nft.in`), and the owner router keeps the home/VPN allow-list permanently (`deploy/traefik/cabinet.yml.example`). Tests in `OwnerWallTests` cover the public-port 404, path tricks, a Host header naming the owner port, a forged forwarded address from an untrusted peer, an out-of-range address, per-request checking on a kept-alive connection, and a test that fails if any `/owner` route lacks the marker. Deployment evidence (summaries, not re-run by the verifier): container self-check 24/24 including both walls; route check 6/6 PASS. |
| 3 | Overrides and locations survive syncs, restarts and new releases, and a backup exists from which a fresh container can be restored | VERIFIED (backup is manual by decision) | Owner data lives in its own `owner-data.json` in the systemd state directory (outside any release directory), applied as an overlay on the synced collection, which is never changed (`CollectionPublisher` header). Writes are atomic; an unreadable or newer-format file is never overwritten except by an explicit restore, which first keeps a copy. Ten earlier versions are kept. Tests: `OwnerPersistenceTests` (a sync that renames another game leaves the owner file untouched; a save made during a sync survives the commit; a new host on the same state directory shows the location without a new sync; a held-back answer never starts the expiry clock), `OwnerLinkTests.A_sync_round_long_past_the_cleanup_grace_keeps_the_files_of_a_link...`, `OwnerRestoreTests.A_download_from_one_server_restores_into_a_fresh_server_and_its_cards_show_the_locations_and_the_link_picture_again`, `OwnerBackupTests` (download, reminder banner, count since last download). Restore caps size (2 MB) and depth, shape-checks the whole file and applies the link policy. The backup is the owner's downloaded file (export and import, accepted risk recorded as the owner's choice); there is no scheduled backup. See Human Verification items 1 and 2. |
| 4 | Conditional: if BGG cannot supply locations, the owner can define their own list and assign one to each game | VERIFIED | The BGG access check recorded that the private location is not readable with the token (0 of 50 base items and 0 of 15 expansions carried one; recorded in the BGG access outcome document), so the condition applies and the tools were required. Built: `LocationEdits` (add, rename, reorder, delete with confirm, names validated), `Locations` page, bulk move from the games list, the row picker, and the single-game view. Tests: `OwnerLocationTests`, `OwnerLocationListPostTests`, `OwnerBulkMovePostTests`, `OwnerGamesListPageTests`; `OwnerPersistenceTests.A_host_started_on_seeded_owner_data_shows_the_seeded_location_on_the_card` shows an assigned location on every visitor's card. |

**Score:** 4/4 truths verified (0 present but behavior-unverified).

### Required Artifacts

| Artifact | Expected | Status | Details |
|----------|----------|--------|---------|
| `Cabinet.Service/Owner/OwnerAccess.cs` | Both walls and the settings checks | VERIFIED | 297 lines, wired in `Program.cs` (client range, path guard, owner-only routes in that order) |
| `Cabinet.Service/Owner/OwnerEndpoints.cs` | Owner-only marker for the whole folder, antiforgery, preview route | VERIFIED | Convention applies the marker and a 64 KB body limit to every owner page; preview is token-staged, never a path |
| `Cabinet.Service/Owner/OwnerDataService.cs` + `Cabinet.Repository/Storage/OwnerDataStore.cs` | Single writer, atomic store, earlier versions, restore | VERIFIED | Substantive and covered by integration tests |
| `Cabinet.Repository/Storage/OwnerDataExport.cs` | Download and restore check | VERIFIED | Strict shape, size and link-policy checks |
| `Cabinet.Service/Owner/OwnerLinkFetcher.cs`, `OwnerLinkWorker.cs` | Token-less, allowlisted, capped fetch and background upkeep | VERIFIED | Level 3 wired in `AddCabinetOwner`; copies kept when a link goes dead |
| `Cabinet.Service/Owner/CollectionPublisher.cs` | Single place that overlays owner data | VERIFIED | Used by sync, start-up and every owner change |
| `Cabinet.Service/Pages/Owner/{Pictures,Locations,Settings}.cshtml(.cs)` and `wwwroot/js/owner-*.js` | The owner screens | VERIFIED | Present, substantive, node suite green |
| `deploy/nftables/cabinet.nft.in`, `deploy/traefik/cabinet.yml.example`, `deploy/bin/cabinet-selfcheck`, `build/check-owner-routes.sh` | Network wall and its checks | VERIFIED | Placeholders only (documentation address ranges and example domains) |

### Key Link Verification

| From | To | Via | Status | Details |
|------|----|-----|--------|---------|
| Owner write | Visitor card | `OwnerDataService.Changed` to `CollectionPublisher.Republish` to `CollectionStore` | WIRED | Overlay applied to a copy; synced collection never mutated |
| Sync commit | Owner data | `CollectionPublisher.Publish` then expiry reconcile | WIRED | Sync cannot overwrite owner data (tests above) |
| Owner link | Cover | `OwnerLinkWorker` to stored record to `ForcedArt(OwnerLink)` | WIRED | Fetch uses the token-less source with the allowlisted host |
| Owner pages | Public port | Path guard and marker | NOT REACHABLE (intended) | 404 off the owner port |

### Data-Flow Trace (Level 4)

| Artifact | Data variable | Source | Real data | Status |
|----------|---------------|--------|-----------|--------|
| Detail card cover | `CardCover` | mapped item `Art` after overlay | Yes, same item as the cabinet | FLOWING |
| Card location | assigned location name | `OwnerOverlay` from `owner-data.json` | Yes | FLOWING |

### Behavioral Spot-Checks (run by the verifier, local only)

| Behavior | Command | Result | Status |
|----------|---------|--------|--------|
| Unit suite | `dotnet test --project Cabinet.UnitTests` | 2389 passed, 0 failed | PASS |
| Integration suite (walls, persistence, restore, links, backup) | `dotnet test --project Cabinet.IntegrationTests` | 442 passed, 0 failed | PASS |
| Node suite (owner scripts) | `node --test build/tests/*.test.mjs` | 346 passed, 0 failed | PASS |
| Lint | `bash build/lint.sh` | repo-rules, workflows, shell, secrets pass; script-tests FAIL only on the owner-route check test, see Anti-Patterns | PASS with note |
| Owner-route check test, hermetic | same test with the two hostname variables unset | 19 checks, 0 failures | PASS |

The browser suite was not re-run by the verifier (it needs a browser download); it was reported green at the last code commit and on the pull request.

### Probe Execution

No phase-declared probes beyond the shell self-tests above. The live self-check and route check were not run by the verifier (no access to the live hosts, by instruction).

### Requirements Coverage

| Requirement | Source Plans | Description | Status | Evidence |
|-------------|-------------|-------------|--------|----------|
| IMG-02 | 06-14 to 06-21, 06-30 | Override a game's image with owned version, base game, or a BGG gallery link (BGG links only) | SATISFIED | Truth 1 |
| LOC-03 | 06-08, 06-09, 06-11, 06-30 | Owner defines their own storage locations | SATISFIED | Truth 4 |
| LOC-04 | 06-08, 06-12, 06-13, 06-19, 06-30 | Owner assigns a location to each game | SATISFIED | Truth 4 |
| OWN-01 | 06-01, 06-02, 06-06, 06-30 | Owner tools only from the home network or VPN | SATISFIED in code and by self-check; outside-the-network request is Human Verification item 3 | Truth 2 |
| OWN-02 | 06-10, 06-22, 06-24, 06-25 | Owner data survives syncs, restarts, releases; backed up | SATISFIED for survival; backup is the owner's manual download (Human Verification items 1 and 2) | Truth 3 |

All five requirement IDs from the roadmap are claimed by plans and accounted for. No orphaned requirements. Bookkeeping note: the five boxes in `REQUIREMENTS.md`, its traceability rows, and the Phase 6 box in `ROADMAP.md` are still unticked and read "Pending"; update them when the phase is closed.

### Code Review Findings, Classified Against the Criteria

None of the findings breaks a success criterion. Classification of those that touch one:

| Finding | Touches | Classification | Reasoning |
|---------|---------|----------------|-----------|
| WR-03 start-up expiry uses the stored collection | Truth 3 (restarts) | WARNING, fix before relying on long outages | Confirmed in code: `SyncStartup` calls `Publish`, which always reconciles. It only forgets a game's data when the game was already stamped absent, the stamp is 30 days old by the wall clock, and the server is restarted without a fresh accepted collection (a long outage or a long sync failure). Ordinary restarts, syncs and releases keep the data; the tests above prove it. Not a gap, but a real data-loss edge on a rare path. |
| WR-02 restore accepts an unbounded revision | Truth 3 (backed up) | WARNING | Needs a hand-edited file. A genuine download restores correctly (round-trip test). Does not make the real backup unusable. |
| WR-01 wall two passes the proxy's own address when no forwarded address was applied | Truth 2 | WARNING | Confirmed in code. It is defence in depth failing open in an abnormal case; the normal path appends the forwarded address, and three other layers hold (separate port, nftables source restriction, proxy allow-list). Fix by refusing when the forwarded address was not applied. |
| WR-07 self-check does not prove the firewall half | Truth 2 | WARNING | A drift would be invisible to the check; the other walls still hold. |
| WR-11 owner scripts and stylesheet are fetchable on the public port | Truth 2 | WARNING | Confirmed (static assets are mapped for every port; the path guard covers only the `owner` segment). It discloses handler names and owner copy, no data and no working endpoint. The criterion says screens and endpoints, which stay refused. Worth moving the assets under the guarded prefix before the site goes public. |
| CR-01 finish picker drops a second choice | none | Not a phase criterion | Only one finish exists (`Finishes.Allowed` holds one value), so the race cannot occur yet; it becomes live when more finishes arrive and should be fixed there. |
| WR-04, WR-05, WR-06, WR-08 to WR-10, WR-12, all info items | none of the four criteria | Advisory | Polish, undo and double-activation hazards, one wrong refusal message, a gitleaks allowlist that is wider than intended (WR-06, relevant to the public-repository rule, worth fixing soon), accessibility naming. No criterion depends on them. |

### Anti-Patterns Found

| File | Line | Pattern | Severity | Impact |
|------|------|---------|----------|--------|
| `build/tests/check-owner-routes-test.sh` | missing-hostname cases | Test is not hermetic: it fails when the two route-check hostname variables are exported in the calling shell (as the owner's workstation profile now does). Passes with them unset, and CI does not set them. | Info | Makes the local lint summary read FAIL on a configured workstation. Fix by unsetting both variables inside the test. |
| Debt markers | n/a | No TBD, FIXME or XXX in the owner code, scripts or deploy files (only `mktemp` templates match) | none | |
| Planning references outside `.planning/` | n/a | None found in source, deploy, build, docs or README | none | |
| Stubs | n/a | None found; every owner screen is backed by a store and tests | none | |

### Human Verification Required

1. **Make and keep one real backup.** Download the owner data from the live owner host and store the file off the container. Expected: a readable JSON file; the reminder banner clears. Why human: the backup is a manual export by decision, so the "backup exists" half of the third criterion is true only once the owner has made one.
2. **One real restore preview.** Upload that file to Restore from file on the live host, compare the three counts, then keep the current data. Expected: counts match, nothing changes. Why human: the owner's first session was confirmed in general terms only, with no itemised result; the round trip is proven in-process, not on the container.
3. **Outside-the-network request.** From a device that is neither on the home network nor on the VPN, try the owner hostname and the public hostname's owner paths. Expected: the owner hostname is refused by the proxy; owner paths on the public hostname are 404. Why human: the recorded checks ran from the home network.

### Gaps Summary

No gaps. The four success criteria are met by code, tests and deployment evidence, and the five requirements are accounted for. The status is `human_needed` rather than `passed` only because the backup is a manual action whose existence cannot be shown from the repository, the owner's first-session confirmation was not itemised, and the proxy-side wall has not been exercised from outside the home network. Recommended follow-ups outside this phase's criteria: WR-03, WR-01 and WR-11 (before the site goes public), WR-06 (public-repository privacy scan), and making the owner-route check test hermetic.

---

_Verified: 2026-10-13_
_Verifier: Claude (gsd-verifier)_

---
phase: 06-owner-tools-persistence
plan: 25
subsystem: owner-tools
tags: [restore, recovery, owner-data, unreadable, razor-pages, focus]
requires:
  - phase: 06-24
    provides: RestoreAsync with RestoreSource.EarlierVersion, OwnerDataExport.Check, the staged restore candidates and the Settings restore panel
provides:
  - the unreadable-owner-data panel on every owner page, with the earlier versions by date and Use this version
  - Settings handler CheckVersion (a version read by its place in the store's own list)
  - OwnerDataService.EarlierVersions and ReadEarlierVersion
  - a shared restore panel partial rendered by Settings and by the unreadable state
  - focus to the file input after Keep current data
affects: [owner shell, Settings, restore]
tech-stack:
  added: []
  patterns:
    - "the owner layout swaps the page body for a recovery panel with IgnoreBody while the stored data is unreadable; pages keep their own markup untouched"
    - "an earlier version is never named by the request: the post carries a position in the server's own list and the bytes then go through the same strict check as an uploaded file"
key-files:
  created:
    - Cabinet.Service/Pages/Shared/_OwnerUnreadable.cshtml
    - Cabinet.Service/Pages/Shared/_OwnerRestorePanel.cshtml
    - Cabinet.BrowserTests/OwnerRecoveryTests.cs
  modified:
    - Cabinet.Service/Pages/Owner/OwnerPage.cs
    - Cabinet.Service/Pages/Shared/_OwnerLayout.cshtml
    - Cabinet.Service/Pages/Owner/Settings.cshtml
    - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Owner/OwnerDataService.cs
    - Cabinet.Service/wwwroot/js/owner-settings.js
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.IntegrationTests/OwnerRestoreTests.cs
    - Cabinet.IntegrationTests/Infrastructure/OwnerDataSeed.cs
    - build/tests/owner-restore.test.mjs
key-decisions:
  - "The restore panel markup moved into a shared partial so the unreadable state on Settings shows the same form, preview, notices and restored line as the normal page; the layout renders it after the recovery panel when the page is Settings."
  - "CheckVersion binds the position as a nullable integer: a value that is not a whole number, a negative one and one past the end all read as no version and answer 422 with the damaged-file words, and nothing is staged."
  - "A kept copy that is not an owner-data file at all is reported with the damaged-file words, since the server made it; a newer-format copy keeps the newer-format words."
  - "Keep current data redirects to the file input's fragment and the script focuses it, so the focus rule holds with a full page load and without script."
patterns-established:
  - "OwnerPage exposes OwnerDataUnreadable and EarlierVersions through the request services, so layouts and partials ask the page instead of injecting the writer"
requirements-completed: [OWN-02]
duration: ~95 min
completed: 2026-10-13
status: complete
actuals:
  tokens: 40000
  tasks: 2
  commits: 2
---

# Phase 6 Plan 25: Recovery when the stored file can't be read Summary

**Every owner page now shows `Your owner data couldn't be read` with `Restore from a file` and the earlier versions by date, `Use this version` goes through the same check, preview and confirm as a file, and the damaged bytes are never touched until the owner restores.**

## Accomplishments

- The owner layout, when the store reports damaged or newer-format data, renders the new `_OwnerUnreadable` panel (page heading, panel heading, body, link to the Settings restore panel, earlier versions as a list of `time` elements with `Use this version` buttons) and calls `IgnoreBody()`. Plate and bar stay; the banner, backup panel, change log and every page's own content are left out. On Settings the shared restore panel follows, so the check, preview, `Restore file`, `Keep current data` and notices work in that state.
- `CheckVersion(int? index)` reads the version through `OwnerDataService.ReadEarlierVersion` (an index into the store's own listing, IO errors answer null), runs `OwnerDataExport.Check`, stages the result as `RestoreSource.EarlierVersion` and redirects to the same preview. After the confirm the damaged bytes are kept as the newest earlier version (proved byte for byte) and the change log reads `Restored an earlier version`.
- Task 2: `Keep current data` now lands on the file input (redirect to its fragment plus `focusFileInputAfterKeep`); focus after `Check file` (preview heading) and after `Restore file` (restored-status heading) is proven in a browser, and the version preview table fits 390 and 320 px with no sideways scroll.
- Public cabinet is unaffected: `/cabinet/cards` answers 200 with no location and no owner picture while the file is unreadable.

## Task Commits

1. Task 1 (tracer): damaged or newer-format file shows the recovery panel on every page, Use this version restores, damaged bytes kept - `3fc8d73`
2. Task 2: restore focus and the table on a small phone - `1edb979`

The tracer's verify (integration restore tests, browser recovery tests) was run end to end and green before Task 2.

## Verification

- Integration: 432 pass (23 in the restore class; new: both unreadable kinds on all three pages with byte comparison and a refused write, Use this version end to end, five out-of-range or malformed indexes, a damaged kept copy, missing antiforgery token, public cards). Browser: 150 owner browser tests pass (12 in the new recovery class). Unit: 2380 pass. Node: 331 pass. `10-repo-rules`, `40-secrets` and `50-script-tests` pass.
- Screenshots reviewed at 1440, 390 and 320 wide (all three pages, both file kinds, the preview): version rows are rule-separated with the button at the end, the preview table fits 320 with wrapping values.

## Deviations from Plan

**1. [Rule 3 - Blocking] Shared restore panel partial**
- **Issue:** the layout ignores the page body while unreadable, but Settings must still offer the restore form and its preview.
- **Fix:** moved the restore section of Settings into `Cabinet.Service/Pages/Shared/_OwnerRestorePanel.cshtml` (not in the plan's file list) and render it from Settings and from the layout. **Commit:** `3fc8d73`.

**2. [Rule 2 - Missing critical] A non-integer or absent version position**
- Binding `int` would turn text into position 0 and check the newest version; the handler binds `int?` and refuses it instead (test with a path-shaped value).

**3. Plan wording adjusted:** "the source label is `an earlier version`" is the existing log line `Restored an earlier version`; no new label was needed. The earlier-version integration cases check cover URLs as empty (the synthetic collection has no automatic art), meaning no owner picture is applied.

## Known Stubs

None.

## Notes for later work

- In the unreadable state the comparison table's server column reads `Not saved yet` and zeros, because there is no readable data to compare with; a word such as `Can't be read` would be clearer.
- The owner-address cabinet strip line `Owner data couldn't be read.` with `Fix it` from the UI contract is not part of this plan's files and was not added here.

## Self-Check: PASSED

- Files exist: `_OwnerUnreadable.cshtml`, `_OwnerRestorePanel.cshtml`, `OwnerRecoveryTests.cs`; commits `3fc8d73` and `1edb979` exist.

---
phase: 06-owner-tools-persistence
plan: 22
subsystem: owner-backup
tags: [backup, export, banner, change-log, viewer-local-times, vanilla-js, browser-tests]
requires:
  - phase: 06-07
    provides: "Owner shell, bar, Settings page stub"
  - phase: 06-10
    provides: "OwnerData, OwnerDataStore, single writer, undo slot, change count"
provides:
  - "OwnerDataExport.Write and FileName: indented strict-shape document without fetched link records"
  - "OwnerDataService.RecordDownloadAsync and Backup (BackupState): download as a system write"
  - "Settings handlers Download (attachment) and BackupState (JSON), Backup and Recent changes panels"
  - "_OwnerBanner partial rendered by _OwnerLayout on every owner page; owner-settings.js on every owner page"
  - "owner-format.js banner helpers and whenText; owner-shared.js 'owner-saved' document event"
affects: [owner-restore, finishes]
actuals:
  tokens: 60000
  tasks: 3
  commits: 2
tech-stack:
  added: []
  patterns:
    - "The banner element is always rendered when there is data and readable, with hidden at zero changes, so script can reveal it without a reload"
    - "Server renders every time as time datetime plus a UTC fallback text; one script rewrites them in the viewer's zone"
    - "postOwner dispatches 'owner-saved' on a saved answer; the backup script listens and refreshes the banner"
key-files:
  created:
    - Cabinet.Repository/Storage/OwnerDataExport.cs
    - Cabinet.Service/Pages/Shared/_OwnerBanner.cshtml
    - Cabinet.Service/wwwroot/js/owner-settings.js
    - Cabinet.IntegrationTests/OwnerBackupTests.cs
    - Cabinet.IntegrationTests/Infrastructure/SometimesFailingStore.cs
    - Cabinet.BrowserTests/OwnerSettingsTests.cs
  modified:
    - Cabinet.Repository/Storage/OwnerDataStore.cs
    - Cabinet.Service/Owner/OwnerDataService.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Pages/Owner/Settings.cshtml
    - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
    - Cabinet.Service/Pages/Shared/_OwnerLayout.cshtml
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/js/owner-shared.js
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.IntegrationTests/Infrastructure/OwnerSession.cs
    - build/tests/owner-format.test.mjs
key-decisions:
  - "Never downloaded with data shows the never-downloaded sentence (as the UI contract's state table says), so a fresh host's first changes read 'N changes are only on this server'; 'since your last download' appears after the first download"
  - "On Settings the banner lives inside the Backup panel (always the full sentence) and the layout skips its under-the-bar copy; with no changes the panel shows the quiet 'Downloaded ...' line and a secondary button"
  - "The change-log divider is placed by count (as many entries above it as changes since the download), not by timestamp, so it always agrees with the banner"
  - "A download whose record cannot be stored answers a redirect with done=download-failed instead of a file, so the owner is never handed a backup the server does not know about"
  - "Phone banner is full-bleed (negative margin equal to the 8px phone body padding) to give the one-line text room"
requirements-completed: []
duration: about 2h
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 22: Backups - download, reminder banner, change log and local times Summary

**One-button backup of all owner data as a readable JSON attachment, a reminder banner on every owner page that counts changes since the last download and fits a phone in one 44px line, a ledger-style recent-changes list with a download divider, and every time shown in the viewer's own clock.**

## What was built

- **Export.** `OwnerDataExport.Write` serialises the stored document with the store's strict options, indented, with the fetched link records removed (only the link address stays) and the download time and a zero change count written as they will be after the download. `FileName` gives `cabinet-owner-data-{yyyy-MM-dd}.json`.
- **Download bookkeeping.** `OwnerDataService.RecordDownloadAsync` runs under the single-writer lock as a system write: no log line, same revision, no announcement, undo slot cleared. It returns null while the data is unreadable or the save fails. `Backup` exposes a `BackupState` (readable, has data, count, last download).
- **Settings page.** Backup panel (helper, banner or quiet `Downloaded ...` line plus secondary button, `Nothing to back up yet.`, footnote with the kept-version count), Recent changes panel (last 20 newest first, divider, empty text), handler `Download` (attachment, `no-store`, antiforgery by default) and `BackupState` (JSON, `no-store`). A failed download redirects with `done=download-failed` and the alert `Couldn't prepare the download. Try again.`
- **Banner.** `_OwnerBanner` is rendered by the owner layout under the bar. Full sentence on wide screens and on Settings; below 40rem one 44px line with `N changes not downloaded` and a compact button (visible `Download data`, accessible name `Download owner data`).
- **Script.** `owner-settings.js` (loaded by the layout on every owner page) rewrites `time[data-owner-when]` into `today at 21:14` / `yesterday at ...` / `9 October at ...` (capitalised at the start of a line), shows `Preparing...` on an `aria-disabled` button during a download, refreshes the banner, quiet line and the divider from `Settings?handler=BackupState` 1.5 s after a download, after any saved change (`owner-saved` event from `postOwner`) and on window focus, and drops pending Undo buttons on download. A failed state fetch leaves the page as it is.
- **Words.** `OwnerStrings` (server) and `OWNER_TEXT`/helpers in `owner-format.js` (script) carry the same copy; `whenText(when, now, {capitalise, timeZone})` is pure and tested in Node.

## Verification

- `node --test build/tests/*.test.mjs`: 325 pass. Unit 2313, integration 401, browser 263 all pass.
- `10-repo-rules.sh`, `40-secrets.sh` (working tree: no leaks), `50-script-tests.sh` pass.
- New integration tests (13): banner counts and singular, never-downloaded sentence, nothing-yet state, attachment headers and indented body, strict round trip through the store, link record absent by parsing (and still present in memory and on disk), no new log entry and same revision after a download, Undo ended, no-token 400, storage failure redirect with the banner kept, BackupState JSON, public port has none of it.
- New browser tests (20): banner at 1440 on Pictures and Locations, phone banner at 390 and 320 (height at most 44, one line, button at least 44 and never cut), Settings banner always full at 1440 and 390, never-downloaded sentence, empty state, download with busy state and no reload, repeated press ignored, failed state fetch, failed download, quiet line and divider after a download, live banner up and down with a saved change and Undo, 23-change log (20 shown, count 23), divider position, single entry, ledger layout at 1440 and 390 with a long entry, times in a fixed viewer zone and fixed page clock, no-script download and UTC fallback text. Console errors empty and request hosts loopback-only wherever asserted.
- Screenshots at 1440 and 390 were looked at (banner on Locations, phone banner, Settings with banner, Settings after a download, 20-entry log with divider): the ledger reads clearly, the time column sits beside the text on desktop and above it on the phone, the long entry wraps without overflow.

## Deviations from Plan

**1. [Interpretation] Never-downloaded banner wording in the first tracer test.** The task text expected `2 changes since your last download` after two location changes on a fresh host, but the plan's own must-haves and the UI contract say a host that was never downloaded shows `You haven't downloaded your owner data yet. {n} changes are only on this server.`. Followed the contract; the tests assert the never-downloaded sentence on a fresh host and `since your last download` after a download. Commit e45f1a3.

**2. [Rule 3 - Blocking] Razor renders a null `data-*` value as an empty attribute.** The banner's `data-full` flag was present (empty) on every page, which disabled the phone styles. CSS now matches `[data-full="true"]`. Commit 4d6a50f.

**3. [Rule 2 - Missing critical functionality] `owner-saved` event.** The plan only refreshed the banner after a download; a banner that stays at zero after a script save would mislead the owner about backup staleness, so `postOwner` dispatches `owner-saved` on a saved answer and the backup script refreshes. Small addition to `owner-shared.js`. Commit 4d6a50f.

**4. [Scope note] Task 3's server markup shipped with Task 1.** The Recent changes panel is part of the Settings page written for the tracer, so the Task 1 commit already holds it; Task 2 and Task 3 are one commit because their scripts, styles and tests share files.

**5. [Known limit] 320px with a wide fallback font.** The compact line `23 changes not downloaded` plus the `Download data` button needs about 350px in the headless browser's fallback font, so at 320px the text ends in an ellipsis there (no wrapping, no sideways scroll, button never cut). With typical phone fonts it fits; at 390px it never truncates (asserted).

## Auth gates

None.

## Known Stubs

None.

## Threat Flags

None beyond the plan's register: the download and state handlers sit under the owner folder (port wall, antiforgery on the post, no-store answers); the banner, log and handlers are absent on the public port (integration test).

## Notes for later plans

- `requirements-completed` is left empty: the restore half of the backup requirement is still ahead, so the requirement is not yet complete.
- The restore panel and the finish tile should be added to `Settings.cshtml` between the page heading and the Backup panel / after it as the UI contract orders them; the banner already lives inside the Backup panel there.

## Self-Check: PASSED

- Files exist: OwnerDataExport.cs, _OwnerBanner.cshtml, owner-settings.js, OwnerBackupTests.cs, SometimesFailingStore.cs, OwnerSettingsTests.cs.
- Commits exist: e45f1a3, 4d6a50f.

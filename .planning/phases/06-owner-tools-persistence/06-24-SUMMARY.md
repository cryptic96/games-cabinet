---
phase: 06-owner-tools-persistence
plan: 24
subsystem: owner-tools
tags: [restore, backup, owner-data, upload, strict-parse, razor-pages]
requires:
  - phase: 06-22
    provides: OwnerDataExport, the backup panel, the banner and owner-settings.js
  - phase: 06-23
    provides: the Settings page with the finish panel
provides:
  - OwnerDataExport.Check with RestoreCheck, RestoreProblem, MaxRestoreBytes, MaxGames
  - OwnerDataService.RestoreAsync and RestoreSource
  - the restore panel on Settings (check, preview, restore, keep current data)
  - OwnerStrings restore copy (summary, fetching line, per-refusal words)
affects: [owner data unreadable panel, earlier-version restore]
tech-stack:
  added: []
  patterns:
    - "an upload is read once into a buffer bounded by the size rule, checked whole, staged under a server-made token, and only a confirming post replaces data"
    - "a page model that needs a larger request limit than the folder default carries its own limit attribute and the folder convention steps aside"
key-files:
  created:
    - Cabinet.UnitTests/Owner/OwnerRestoreTests.cs
    - Cabinet.UnitTests/Owner/OwnerRestoreParserTests.cs
    - Cabinet.IntegrationTests/OwnerRestoreTests.cs
    - Cabinet.BrowserTests/OwnerRestoreTests.cs
    - build/tests/owner-restore.test.mjs
  modified:
    - Cabinet.Repository/Storage/OwnerDataExport.cs
    - Cabinet.Domain/Owner/OwnerData.cs
    - Cabinet.Service/Owner/OwnerDataService.cs
    - Cabinet.Service/Owner/OwnerEndpoints.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Pages/Owner/Settings.cshtml
    - Cabinet.Service/Pages/Owner/Settings.cshtml.cs
    - Cabinet.Service/wwwroot/js/owner-settings.js
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/css/owner.css
    - Cabinet.IntegrationTests/Infrastructure/OwnerSession.cs
    - Cabinet.IntegrationTests/OwnerWallTests.cs
key-decisions:
  - "The restore panel is its own paper panel (id restore, h3) after Backup, as the UI contract places it, and is shown in every data state including unreadable and empty."
  - "A check answers a script with JSON (token or refusal words) and a form without script with a redirect to the preview (success) or the page itself with status 422 (refusal); the preview is always rendered server-side from the token, so the script only navigates."
  - "Restore itself is a plain form post that renders the page with the restored line; no PRG, so the summary and fetch count come from the checked document rather than from a query string."
  - "Fetched link records are always dropped from a restored document, so a stored copy with art file names is also safe to restore: every link is fetched again."
  - "Location ids must match the generator's shape (8 lowercase hex); names go through LocationNames.Validate and must already be trimmed."
patterns-established:
  - "OwnerData figures (locations, placed games, picture choices) live in the domain as OwnerDataFigures so the preview and the restored line agree"
requirements-completed: [OWN-02]
duration: ~110 min
completed: 2026-10-12
status: complete
actuals:
  tokens: 65000
  tasks: 2
  commits: 2
---

# Phase 6 Plan 24: Restore from a file Summary

**Strict, all-or-nothing restore of a downloaded owner-data file: checked whole, previewed next to the server's data, applied only on confirmation with the current data kept as an earlier version first, and linked pictures fetched again.**

## Accomplishments

- `OwnerDataExport.Check(bytes, policy)` refuses everything it cannot fully accept with a typed problem (`NoFile`, `TooLarge`, `NotOwnerData`, `Damaged`, `Newer`, `OverLimits`): 2 MB size rule, `MaxDepth = 16`, kind and schema version read first, strict deserialisation with the store's options, 50 locations, name rules (length, control and bidi characters, case-insensitive duplicates, edge spaces), id shape and uniqueness, game references, duplicate games, 5000 games, positive game ids, change-log caps, and every link through `ArtUrl.Canonical` and `ArtSourcePolicy.Allows`. Accepted documents have their fetched records removed and games sorted.
- `OwnerDataService.RestoreAsync` keeps an earlier version first (unconditionally, also for an unreadable file, copying its bytes; a failure to keep or save changes nothing), stores the document under a revision above both, adds an uncounted `Restored from file` line, stamps games the collection does not list with the restore time (no stamping while there is no collection yet), clears Undo, announces the change and asks the link worker to run.
- Settings restore panel: labelled file field, `Check file`, preview (`This file contains`, summary, hidden-caption table with `th scope`, `Not in your collection now` row only above zero, older-format line hook, destructive warning, refetch note, `Restore file` and `Keep current data`), restored status line and `Fetching {n} picture(s) in the background.`, inline `role="alert"` refusals. The page model carries a 3 MB request and form limit; the owner folder convention now yields to a model that carries its own limit. A 2 MB + 1 byte file gets its message; 50 MB answers 413.
- `owner-settings.js` posts the file with `fetch`, `FormData` and the antiforgery header, shows `Checking...` on an `aria-disabled` button, writes refusals with `textContent`, follows only a token of the server's shape, and moves focus to the preview or restored heading. Without script the same form posts normally.

## Task Commits

1. Task 1 (tracer): download from one server, restore into a fresh one, cards show locations and the link picture again - `bfe0177`
2. Task 2: the restore check refuses everything it cannot fully accept - `5df3cfd`

The tracer's verify (unit and integration restore tests, round trip across two hosts with different state directories, the second proven empty first) was run end to end and was green before expanding.

## Verification

- Unit: 2380 pass (14 restore service/strings tests, 43 check tests). Integration: 421 pass (12 restore tests incl. failure matrix with byte-for-byte unchanged data, 413, 400 without token, public port 404). Browser: restore tests at 1440, 390 and 320 widths plus the existing owner browser tests pass. Node: 330 pass. `10-repo-rules`, `40-secrets`, `50-script-tests` pass.
- Screenshots reviewed at desktop and 320/390 phone widths (preview, restored line, refusal): the table fits 320 px with wrapping values, no sideways scroll, destructive colour only on the warning line and `Restore file`.

## Deviations from Plan

**1. [Rule 1 - Bug] Existing body-limit test targeted the Settings page**
- **Issue:** `OwnerWallTests` proved the 64 KB owner default by posting 70 KB to `/owner/settings`, which the plan deliberately raises to 3 MB.
- **Fix:** the test now posts to `/owner/locations`, which keeps the default. **Commit:** `5df3cfd`.

**2. [Rule 3 - Blocking] The folder convention would have overridden the page's limit**
- **Issue:** the convention appended a 64 KB request-size filter after the model's own attribute, so the later filter would win.
- **Fix:** the convention adds its default only when the page model carries its own `RequestSizeLimit`. **Files:** `OwnerEndpoints.cs`.

**3. [Rule 2] Records are stripped and a failed keep aborts the restore**
- A restored document's link records are removed (a stored copy would otherwise carry art file names a hostile file could choose), and a restore stops when the earlier version cannot be kept.

**4. Plan wording adjusted:** "host B's earlier-versions list holds the pre-restore data" cannot hold for a truly empty state directory (there is no file to keep); the round trip test proves the empty start and the keep-first behaviour is proven separately with existing data and with an unreadable file kept byte for byte.

## Known Stubs

None.

## Notes for later work

- The earlier-version restore (`Use this version`) reuses `RestoreAsync` with `RestoreSource.EarlierVersion` (log line `Restored an earlier version`); its panel is not part of this plan.
- The change-log divider after a restore sits above the restore line (counts do not include it), by the uncounted-line decision.
- The picture-choice proof in the integration round trip is at data level (choices and link address restored; link picture arrives and is the card's cover) because the synthetic collection serves no BGG pictures.

## Self-Check: PASSED

- Files exist: OwnerRestoreTests (unit, parser, integration, browser), owner-restore.test.mjs; commits `bfe0177` and `5df3cfd` exist.

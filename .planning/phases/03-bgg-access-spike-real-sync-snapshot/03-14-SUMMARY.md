---
phase: 03-bgg-access-spike-real-sync-snapshot
plan: 14
subsystem: live-updates
tags: [signalr, vendored-asset, csp, playwright, live-redraw, lint]
requires:
  - phase: 03-bgg-access-spike-real-sync-snapshot
    provides: live hub and status broadcasts (03-13), quiet redraw and status line (03-12)
provides:
  - "live.js startLive with the reconnect schedule and status-polling fallback; shouldRedraw and reconnectDelayMs in status.js; quiet redraw on a new snapshot version"
  - "Official SignalR browser client 10.0.11 vendored byte-identical at wwwroot/lib/signalr/signalr.min.js with NOTICE.md, -text attribute, pinned SHA-256 unit test and update docs"
  - "Lint helper filter_vendored_js (comment rule skips only wwwroot/lib/) with self-tests"
  - "isOutdatedStatus ordering guard in status.js and sync.js (fix for a stuck Syncing button)"
affects: [release plan deployed live-update check, end-of-phase screenshot review]
tech-stack:
  added: ["@microsoft/signalr 10.0.11 (vendored file only, no package manager, no Node toolchain in the repository)"]
  patterns:
    - "Third-party browser script vendored unchanged, pinned by hash in a unit test, excluded only from the comment lint"
    - "Statuses are applied in server-time order; an older status never overwrites a newer one"
key-files:
  created:
    - Cabinet.Service/wwwroot/js/live.js
    - Cabinet.Service/wwwroot/lib/signalr/signalr.min.js
    - Cabinet.Service/wwwroot/lib/signalr/NOTICE.md
    - Cabinet.UnitTests/Configuration/VendoredAssetTests.cs
    - docs/vendored-assets.md
    - .gitattributes
  modified:
    - Cabinet.Service/wwwroot/js/sync.js
    - Cabinet.Service/wwwroot/js/status.js
    - Cabinet.Service/wwwroot/js/cabinet.js
    - Cabinet.Service/Pages/Index.cshtml
    - build/lint/checks/10-repo-rules.sh
    - build/tests/page-scripts.test.mjs
    - Cabinet.IntegrationTests/LivePageTests.cs
    - Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs
key-decisions:
  - "Owner approved both downloads (answer approve-both): the signalr package into the session scratchpad, and playwright 1.63.0 with Chromium, Firefox and WebKit into the scratchpad for the engine checks and screenshots"
  - ".gitleaks.toml needed no change: the secrets check passed with the vendored file present"
  - "The press cycle starts when the press is sent, and statuses are ordered by server time, so a sync that ends within milliseconds still writes its outcome note"
requirements-completed: [SYNC-03, SYNC-02]
duration: ~95 min (including download and engine runs)
completed: 2026-10-07
status: complete
actuals:
  tokens: 28000
  tasks: 3
  commits: 3
---

# Phase 3 Plan 14: Live updates on the page Summary

Every open page now receives pushed status through Microsoft's official SignalR browser client, vendored unchanged and pinned by hash, and redraws the cabinet quietly in place when another page's sync changes the collection, under the unchanged strict policy, with zero policy violations and zero console errors in Chromium and Firefox. WebKit could not start on this machine (see below).

## Task commits

| Task | Name | Commit |
| ---- | ---- | ------ |
| 1 | An open page catches up and redraws quietly when another sync changes the collection (tracer) | 31cc790 |
| 2 | Owner approves downloads (decision checkpoint) | none, resolved: "approve-both" |
| 3 | Official client vendored and checked | a748a6a |
| 3 (deviation) | Stuck "Syncing" button fix found in the browser check | c2ab19e |

## Owner decision (Task 2)

The owner answered **approve-both**, after being told exactly what each download is: (1) `npm pack @microsoft/signalr@10.0.11` from the npm registry into the session scratchpad (about 1 MB), of which only `package/dist/browser/signalr.min.js` enters the repository; (2) `playwright@1.63.0` with Chromium, Firefox and WebKit into the scratchpad (about 1.3 GB on disk), used only for the engine checks and the screenshots. Nothing was installed into the repository (no package.json, node_modules or lock file).

## Vendored client verification

- Registry integrity `sha512-FulOJ2EEtKvLQcswe/U7v8pzyXyk7Jua5xgWJPPwyQU/2Z9ORvKcVjyO75VvLsem+CJ1ORRexJ+7Bz1FCei2aw==` equals the tarball's own SHA-512 and the value approved in Task 2.
- `signalr.min.js`: 47,668 bytes, SHA-256 `97e9b97e642a72e5a470917147a2bf79f86cad829a5c6786adb10614d248bb95`, copied unchanged (no source map). The package ships no licence file; the notice records the MIT licence the package declares, with the .NET Foundation copyright used in the upstream sources.
- `.gitattributes` has `Cabinet.Service/wwwroot/lib/** -text`; `git check-attr` confirms `text: unset`.
- `VendoredAssetTests` pins the hash and checks the notice names the same hash, version and licence.
- Lint: `filter_vendored_js` is applied only in the `js_files` pipeline; two self-tests prove the vendored path is dropped, the normal path is kept, and a `//` comment under a normal path is still detected. The planning-reference check still reads the vendored file. `build/lint.sh` passes, including the secrets check.
- The page references exactly one third-party script, the vendored client, as a classic same-origin script before the module script (integration test). The policy string is unchanged.

## Browser checks

Run against the fake BGG (`normal`, 65 then 400 items, then `unavailable` and `shrunk`) and the app in Development on loopback with dummy credentials; real BGG was never contacted. Two pages open in one browser; Sync now pressed in page A; page B observed.

| Engine | Ran | Version | Live transport | CSP violations | Console errors | Page errors |
| ------ | --- | ------- | -------------- | -------------- | -------------- | ----------- |
| Chromium | yes | 153.0.8010.12 | WebSocket on both pages | 0 | 0 | 0 |
| Firefox | yes | 155.0 | WebSocket on both pages | 0 | 0 | 0 |
| WebKit | **no** | n/a | n/a | not measured | not measured | not measured |

**WebKit did not start:** the Playwright WebKit build needs the system library `libmanette-0.2.so.0`, which is missing on this machine (`error while loading shared libraries`). Per the download rules no system package was installed. The strict-policy-with-live-updates check in WebKit (including the same-origin WebSocket matching under `'self'`) is therefore **not yet done**; it needs `libmanette-0.2-0` on the machine, or Safari/iOS checked by the owner. Server-Sent Events and status polling remain the fallbacks if a WebKit version refuses the socket.

Live redraw results, identical in Chromium and Firefox:

- First sync: B went from the being-filled message to the drawn cabinet (64 boxes) with no loading line seen and the note empty (broadcasts never write to the note); A showed "Collection updated. BGG can take a few minutes to show recent edits."
- Growth from 65 to 400 items with B scrolled to 409 px and a box focused: B swapped to 399 boxes in 9 sections; the loading line never appeared, scroll position sampled every frame stayed at one value (409, no jump), focus stayed on the same box, no note was written.
- No horizontal scroll at 320, 390 or 1440 px for any state, including the 400-item cabinet (vertical scroll only).
- Failed state (fake `unavailable`): note "BGG didn't respond. The last collection is still showing."
- Held-back state (fake `shrunk`, Chromium): note "BGG returned far fewer games than before, so the last collection is still showing."
- Stale state (state file seeded to five hours ago, Chromium): "Synced 5 hours ago" and "Showing the last sync from 7 October 2026 at 03:31 CEST. Recent syncs haven't gone through."

## Screenshots

All in `<session scratchpad>/live-check/shots/<engine>/`, named `<state>-<width>.png`. All data is invented; none is committed.

- Chromium and Firefox: `filling` (being filled), `syncing`, `cooldown` (page A after its press, button disabled with the countdown and the outcome note), `synced` (page B, which only received the broadcast), `synced-400` (400-item cabinet), `failed-note`; each at 320, 390 and 1440.
- Chromium only: `held-back-note` and `stale-note` at 320, 390 and 1440.
- Crops in both engines: `synced-expansion-label-390.png`, `synced-expansion-label-1440.png`, `synced-footer-credit-390.png`, `synced-footer-credit-1440.png`.

The first-sync swap is an instant in-place replacement with no loading flash and no scroll jump (backstop row: measured above; the owner judges the feel from the screenshots and the live pages).

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 1 - Bug] A sync that ended before the press answer arrived left the button on "Syncing..." with no outcome note**
- **Found during:** Task 3 (browser check, failed-note state; first Chromium run timed out waiting for the note)
- **Issue:** The fake's refused sync finished within 2 ms, so both pushed statuses (running, then finished) reached the page before the answer to the press. The older answer then overwrote the newer status, and the press cycle only began after the finish, so with the live connection up nothing ever polled: the button stayed on "Syncing..." and no outcome note was written. The same can happen with any very fast sync (for example not configured, or an instant refusal).
- **Fix:** `isOutdatedStatus` ignores a status older (by server time) than one already taken; the press cycle starts when the press is sent, so an early finish writes its outcome note and the later 202 does not clear it. Tests for the ordering rule added to the page-script suite.
- **Files modified:** Cabinet.Service/wwwroot/js/status.js, Cabinet.Service/wwwroot/js/sync.js, build/tests/page-scripts.test.mjs
- **Verification:** `node --test` 33 passing; Chromium and Firefox reruns show the note; `dotnet test --solution Cabinet.slnx` 807 passing; `build/lint.sh` passes.
- **Commit:** c2ab19e

### Other notes

- `Bgg:MinRequestGapSeconds` (minimum 5) and `Sync:StartupJitterMaxSeconds` (minimum 10) cannot be lowered for local runs, so the checks used the defaults and `Sync:ManualCooldownMinutes=1`; this lengthened the runs but changed nothing in the tested code.
- Playwright's own host-requirements check flagged the missing WebKit library while installing; the three browsers still downloaded to the scratchpad. `PLAYWRIGHT_SKIP_VALIDATE_HOST_REQUIREMENTS` was tried only to see whether WebKit could run without changing the system; it cannot.

## Authentication gates

None.

## Known Stubs

None.

## Threat Flags

None: no new endpoint, auth path or trust boundary beyond the planned vendored script and the existing hub.

## Open items for the owner and later work

- Run the strict-policy-with-live-updates check in WebKit (needs `libmanette-0.2-0` on a machine with sudo, or Safari on a Mac/iPhone).
- Confirm that Traefik passes WebSocket upgrades on the real route during the release plan's deployed check.
- The held-back note wording that names BGG is an open owner decision; the strings were left unchanged.
- Review the screenshot set with the end-of-phase checks.

## Self-Check: PASSED

- Vendored file hash and size verified; `git log` shows 31cc790, a748a6a and c2ab19e on `milestone/v1-games-cabinet`.
- Ports 6180, 6181 and 6190 are free; the fake BGG, the app and all Playwright browsers started for the checks were stopped.

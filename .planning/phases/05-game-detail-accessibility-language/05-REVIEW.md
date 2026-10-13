---
phase: 05-game-detail-accessibility-language
reviewed: 2026-10-12T00:00:00Z
depth: standard
files_reviewed: 88
files_reviewed_list: "git diff --name-only 965df1b..ef6ee75, excluding .planning/, lock files, PNGs and layout golden files"
findings:
  critical: 0
  warning: 3
  info: 8
  total: 11
status: issues_found
---

# Phase 5: Code Review Report

**Reviewed:** 2026-10-12
**Depth:** standard (shipped server, client and workflow code read in full; tests and docs given a lighter pass)
**Files Reviewed:** 88
**Status:** issues_found

## Summary

No critical or security findings. Verified specifically:

- **Text and markup safety.** Every record value reaches the page through `textContent`. Cover URLs are constrained by `/^\/art\/[0-9a-f]{16}-[0-9]{1,4}\.webp$/`, which matches the real file names (`ArtProcessor.cs:159`). The BGG link is a fixed host plus a digits-only id. There is no `innerHTML` anywhere, and styles go through CSSOM only, so the `default-src 'self'` policy holds.
- **Redirects and cookies.** `LanguageEndpoint` accepts only `en`/`nl` and redirects only to `/` or a sample name the catalog itself returns, so there is no open redirect.
- **Cache validators.** `EntityTags` handles weak, wildcard and malformed `If-None-Match` values. The cards content-hash ETag is consistent with the `SnapshotMapper.Version` card-visible field set, including expansion-to-base titles via `ExpansionRefs`.
- **BGG and endpoints.** The new routes make no BGG calls and add no CORS, and the cards payload carries only the fields the page displays.
- **Project rules.**
  - No `//` comments in C#, JS, CSS or `.mjs` in scope.
  - No planning references outside `.planning/`.
  - No personal data, real hostnames, IPs or real locations; test and sample data is synthetic.
- **Workflows.** Actions are pinned by SHA, `persist-credentials: false` is set, permissions are minimal per job, and provenance is re-verified before publishing.
- **Client state machine.** The history step, close gate, open/close races, redraw-while-open and profile reload in `card-flow.js`, `detail.js` and `cabinet.js` were traced with no reproducible race found.

## Warnings

### WR-01: A single failed card-data request leaves every card incomplete for the whole page session, with a misleading message

**File:** `Cabinet.Service/wwwroot/js/cabinet.js:93-122`; `card-view.js:326-329`; `copy.js:92` (`noDetails`)

**Issue:**
- `fetchCards` swallows every failure, through the `catch { return; }` and the `!response.ok` early return.
- It never retries on a network failure; the only second attempt is for an ETag mismatch.
- `load()` resets `cardRecords` to an empty map. After one flaky request, the following holds until the next collection change or a reload:
  - the games list stays title-only;
  - every card opens incomplete, with no players, time, weight, designers or location;
  - the card says "More details arrive after the next sync." That is false here, because the data exists and a reload fixes it.

**Fix:** Retry once or twice with a short delay. Alternatively, refetch lazily from `recordFor` when `cardRecords.size === 0`. Give a fetch-failure incomplete record its own note, such as "Couldn't load details. Reload to try again."

### WR-02: `--ignore-exit-code 8` applies to the whole solution run, so a project that runs zero tests passes CI and releases

**File:** `.github/workflows/ci.yml:46`; `.github/workflows/release.yml:45`; `docs/development.md:214`

**Issue:**
- Exit code 8 means zero tests ran. It is ignored so the browser project, which the trait filter excludes entirely, doesn't fail the run.
- The flag applies to every project, so a broken trait filter or a test project that discovers no tests would also pass silently.
- `release.yml` has no browser, lint or JS-test gate. It relies on branch protection requiring those checks.

**Fix:** Run the non-browser projects explicitly (`dotnet test --project Cabinet.UnitTests …`, `--project Cabinet.IntegrationTests …`) instead of ignoring exit code 8. Alternatively, add `--minimum-expected-tests <N>`. Confirm that `browser-tests` and `lint` are required status checks on `main`.

### WR-03: The fast-flick sheet test depends on real-time gesture speed and can fail on a loaded runner

**File:** `Cabinet.BrowserTests/SheetDragTests.cs:96-109`; `detail.js:614-622`

**Issue:**
- The 80 px flick is below the 25% distance needed to close, so closing depends on release speed.
- `releaseSpeed` only counts samples within the last 80 ms. If the CDP mouse steps land more than 80 ms apart, the measured speed is 0 and the sheet springs back.
- The non-parallel collection removes other xunit tests from the picture, but not CPU contention.

**Fix:** Make the gesture deterministic, for example with synthetic pointer events whose `timeStamp` values are chosen. Alternatively, cover `dragOutcome`/`releaseSpeed` in `card-flow.test.mjs` and keep this browser test distance-based.

## Info

### IN-01: Duplicated constants and helpers that must change together

**Files:**
- `render.js:19` and `card-view.js:9`: the `ART_PATH` regex.
- `cabinet.js:18` and `detail.js:12`, plus the CSS `@media (max-width: 40rem)` blocks: the phone breakpoint.
- `format.js:18` and `games-list.js:20`: `isKnown`.
- `card-css.test.mjs` and `cabinet-css.test.mjs`: `withoutComments`.
- `CardsEndpoint.Handle` and `LayoutEndpoint.Handle`: the profile check, ETag header and 304 logic.

**Fix:** Share one definition each.

### IN-02: The sample-mode strings in the page are English-only, even on the Dutch page

**File:** `Cabinet.Service/Pages/Index.cshtml:172, 176, 179-193`

**Issue:** These strings only appear with the prototype switch on, which is a development setting.

**Fix:** Move them into `SiteText`, or note that they are intentionally not localised.

### IN-03: The skip-link comment claims a no-script target that only the script creates

**File:** `games-list.js:9-10, 353-358`

**Fix:** Correct the comment, or give the section a stable id that the script moves.

### IN-04: The language cookie's `Secure` attribute depends on forwarded-headers configuration

**File:** `Cabinet.Service/Language/LanguageEndpoint.cs:54`

**Issue:** `Secure = context.Request.IsHttps` is only true behind the proxy when `X-Forwarded-Proto` is honoured. The impact is low: it is a functional cookie.

**Fix:** Set `Secure` unconditionally outside Development and Testing, or add a forwarded-proto test.

### IN-05: The strict equality between `body.layout` and the response ETag assumes a proxy never weakens the tag

**File:** `cabinet.js:112`

**Issue:** If a proxy turns the strong tag into `W/"…"`, the cards are never accepted.

**Fix:** Compare with the leading `W/` stripped.

### IN-06: An early return in `openCard` leaves the close flags set

**File:** `detail.js:386-388`; flag handling at `431-434` and `544-548`

**Issue:** If a forced fade-close was requested during an open that returns early, the next card that opens closes itself immediately. This is hard to reach in practice.

**Fix:** Reset `closeWhenSettled` and `forcedPath` in `finally` when the dialog is not open.

### IN-07: The inotify cleanup test is environment-sensitive

**File:** `Cabinet.IntegrationTests/TestHostCleanupTests.cs:407`

**Issue:** The test fails with `DOTNET_USE_POLLING_FILE_WATCHER=1` set, where no inotify instance is created. Its 5 s drop-back wait is also timing-based.

**Fix:** Skip when the polling watcher is set, or when the count did not rise.

### IN-08: Test hygiene

**Files:**
- `PullOutTests.cs:276`: `Task.Delay(50)` without the test cancellation token.
- `CabinetWebApplicationFactory.cs:98-105, 240-256`: `Dispose` can throw from `ThrowIfAnyRefused()` and mask a constructor start-up exception.
- `ci.yml:50`: `node --test` runs on the runner's unpinned Node.

**Fix:** Pass the token. Swallow or aggregate the guard exception in the failed-construction path. Add `actions/setup-node` with a pinned version.

---

_Reviewed: 2026-10-12_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

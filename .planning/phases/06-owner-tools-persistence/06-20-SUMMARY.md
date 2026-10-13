---
phase: 06-owner-tools-persistence
plan: 20
subsystem: owner-pictures
tags: [link-preview, staged-items, owner-endpoint, razor-page, vanilla-js, browser-tests]
requires:
  - phase: 06-17
    provides: "OwnerLinkFetcher.CheckAsync, LinkCheck, ProcessedLink"
  - phase: 06-19
    provides: "Pictures page rows, single-game view, owner-pictures.js"
provides:
  - "StagedItems<T>: in-memory, token-addressed, capped (8) and expiring (15 min on the injected clock) staging"
  - "GET /owner/preview/{token} on the owner port: staged 240 px webp, no-store, nosniff, 404 otherwise"
  - "LinkPreview.Build: box, shape source, verdict, spine and shortened label through SnapshotMapper.Explain with a one-game link override"
  - "PictureEdits.UseLink, and the CheckLink, UseLink and DiscardPreview handlers"
  - "The link disclosure, the mini-shelf preview (script and no-script), Use this with Undo, Discard preview"
affects: [pictures, owner-links]
actuals:
  tokens: 70000
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns:
    - "The preview is made by the cabinet's own mapper and drawn with the cabinet's own .placement rules; the script sets only custom properties"
    - "A staged item is addressed by a server-made token; any text that is not 32 lowercase hex characters is never looked up"
    - "Files are written before the owner data is changed, so a record never points at missing files"
key-files:
  created:
    - Cabinet.Service/Owner/StagedItems.cs
    - Cabinet.Domain/Owner/LinkPreview.cs
    - Cabinet.Service/Pages/Owner/_LinkPreview.cshtml
    - Cabinet.Service/Pages/Owner/LinkPreviewModel.cs
    - Cabinet.Service/wwwroot/js/owner-link.js
    - Cabinet.IntegrationTests/OwnerLinkPreviewTests.cs
    - Cabinet.BrowserTests/OwnerLinkFlowTests.cs
    - Cabinet.UnitTests/Owner/LinkPreviewTests.cs
    - Cabinet.UnitTests/Owner/StagedItemsTests.cs
    - build/tests/owner-link.test.mjs
  modified:
    - Cabinet.Service/Owner/OwnerEndpoints.cs
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Program.cs
    - Cabinet.Domain/Owner/PictureEdits.cs
    - Cabinet.Domain/Owner/PictureRows.cs
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.Service/wwwroot/js/owner-pictures.js
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/css/owner.css
key-decisions:
  - "The disclosure's class is pic-linkbox, not pic-link: pic-link already names the page's underlined text links"
  - "The link script is a new module, owner-link.js, so owner-pictures.js stays readable; it receives the page's row helpers"
  - "Without script the preview shelf is an inline SVG sized by attributes, because the strict CSP forbids style attributes; with script the shelf is the cabinet's .placement boxes"
  - "A failed check, from any cause, answers one generic refusal key (link-failed) for now; per-problem wording is left for the next plan"
  - "A used preview that was never staged or has run out answers link-expired and changes nothing"
requirements-completed: [IMG-02]
duration: about 2h
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 20: Paste a gallery picture link, preview it on a mini shelf, use it Summary

**An owner-pasted picture link is fetched and processed, previewed from the site's own origin as the face-out box and the spine at cabinet scale, and, once the owner presses Use this, stored so every visitor's card shows it.**

## What was built

- **Staging.** `StagedItems<T>` holds the processed picture under a `RandomNumberGenerator.GetHexString(32, lowercase: true)` token, drops the oldest at 8, forgets after 15 minutes on `TimeProvider`, and never looks up a text of any other shape. No file, no `blob:` or `data:` URL.
- **Preview endpoint.** `OwnerEndpoints.MapCabinetOwner` maps `GET /owner/preview/{token}` with the owner-only marker; answers `image/webp`, `Cache-Control: no-store`, `X-Content-Type-Options: nosniff`; 404 on the public port and for unknown, malformed and expired tokens.
- **LinkPreview.** Built with `SnapshotMapper.Explain` and a one-game `Link` override, so box, size source (BGG, picture, estimate), verdict, colours, edges, fit, spine width (never under the cabinet's least thickness) and the engine-shortened spine label are the cabinet's own. A unit test proves the preview equals what `ToCabinetItems` draws once the link is chosen.
- **Handlers.** `CheckLink` (token-less picture source through the existing fetcher; JSON for script, redirect to `?game={id}&preview={token}#game-{id}` without), `UseLink` (writes the variants with `ArtCache.Write`, then `PictureEdits.UseLink` through the single writer, then forgets the staged item; JSON carries the filled Your link tile), `DiscardPreview`. All posts keep antiforgery.
- **UI.** `<details class="pic-linkbox">` per row with the field, hint and `Check link` (reads `Checking...`, `aria-disabled`, field read-only while busy); the preview (heading with focus, sub-line, shelf, verdict tag, shape source, 3D note, disclaimer, `Use this`, `Discard preview`); `Use this` fills the tile, checks the radio, adds `Yours`, closes the disclosure, shows `Saved` with Undo (Undo restores tile, choice and mark) and focuses the Your link radio; `Discard preview` focuses the field.

## Verification

- Unit 2284 passed (before the UI step; domain unchanged since), integration 383 passed, browser 222 passed, `node --test build/tests/*.test.mjs` 310 passed.
- `10-repo-rules.sh`, `40-secrets.sh` (working tree clean after replacing a hex literal with a computed one) and `50-script-tests.sh` pass.
- Screenshots (scratch directory, not committed) at 1440, 390 and 320 px were looked at: shelf, captions on the board, tile after use, no sideways scroll, controls at least 44 px. One change came from review: the shelf is capped at 24rem so it does not read as a long empty cubby on wide screens.

## Deviations from Plan

### Auto-fixed / judgement calls

**1. [Rule 3 - Blocking] Disclosure class renamed** - `pic-link` is already the underlined text-link class on this page, so the disclosure is `pic-linkbox`. Commit 637a90a.

**2. [Rule 2 - Missing critical functionality] Strict CSP forbids style attributes** - the no-script path cannot size `.placement` boxes, so the server draws the shelf contents as inline SVG (size and colour attributes only). Script replaces nothing on a server-drawn preview; it only wires its buttons and focuses its heading. The script path uses the real `.placement` boxes. Commit 637a90a.

**3. [Rule 3 - Blocking] `PictureRows.FileOf` made internal** so `LinkPreview` picks the same 240 px file as the tiles; the preview carries the file name so the page needs no second lookup. Commit 1e44530.

**4. New refusal keys** - `link-failed` and `link-expired` added to the fixed set of refusal codes and to `OwnerStrings.Message`, so the no-script redirect and the JSON answer carry fixed words, never request text. Commit 1e44530.

**5. Extra tests beyond the plan** - `StagedItemsTests` and `LinkPreviewTests` (unit), `owner-link.test.mjs` (server answer validation). Red-first was honoured at API level (tests were written against members that did not exist yet and did not compile); the browser suite passed on its first run once the UI existed.

## Known Stubs

None. The per-problem link wording (blank, not a web address, gallery page link, other host, not found, too large and so on), `Remove link` and the 3D note's browser tests are the next plan's; every failure shows one generic inline message for now.

## Threat Flags

None. Threat register items are covered: SSRF by the existing fetcher policy (a test proves an other-host link makes no request); tokens validated by shape before lookup (unit and integration tests, including a path-like token); preview 404 on the public port; fixed media type, `nosniff`, `no-store`; antiforgery on every post (a post without the token is 400).

## Self-Check: PASSED

- Files exist: StagedItems.cs, LinkPreview.cs, _LinkPreview.cshtml, LinkPreviewModel.cs, owner-link.js, the four new test files.
- Commits found: 1e44530, 637a90a.

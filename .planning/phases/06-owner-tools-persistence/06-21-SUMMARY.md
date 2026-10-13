---
phase: 06-owner-tools-persistence
plan: 21
subsystem: owner-pictures
tags: [link-errors, link-preview, remove-link, undo, vanilla-js, browser-tests]
requires:
  - phase: 06-20
    provides: "StagedItems, LinkPreview, CheckLink/UseLink/DiscardPreview handlers, link disclosure and preview UI"
  - phase: 06-17
    provides: "OwnerLinkFetcher, LinkProblem, LinkCheck"
provides:
  - "OwnerStrings.LinkError: exact copy for every LinkProblem, with the picture host and sizes only (never pasted text)"
  - "One refusal key per problem (link-address, link-page, link-host, link-not-found, ...) for redirects and JSON answers"
  - "CheckLink JSON carries problem and message; the no-script path shows the same words (size in MB from a numeric query)"
  - "PictureEdits.RemoveLink and the RemoveLink handler; Undo fetches the link again"
  - "Current link line, Remove link button, state-aware Your link tile (picture, fetching, none)"
  - "owner-format.js: megabytes, shortAddress, leadAndRest"
affects: [pictures, owner-links]
actuals:
  tokens: 85000
  tasks: 3
  commits: 1
tech-stack:
  added: []
  patterns:
    - "A refusal's words are fixed copy plus the host and sizes; the script splits the lead sentence into a strong element and writes it with textContent"
    - "The row's Yours and Fetching marks follow the choice and fetching flags every answer carries, so every path (choose, undo, remove) updates them the same way"
    - "After an undo, a link whose copy was tidied away is forgotten by a system write so it shows Fetching, and the link worker is asked to fetch it now"
key-files:
  created:
    - Cabinet.UnitTests/Owner/LinkErrorTests.cs
    - Cabinet.UnitTests/Owner/RemoveLinkTests.cs
  modified:
    - Cabinet.Service/Owner/OwnerStrings.cs
    - Cabinet.Service/Owner/OwnerResponses.cs
    - Cabinet.Service/Owner/OwnerLinkFetcher.cs
    - Cabinet.Domain/Owner/PictureEdits.cs
    - Cabinet.Service/Pages/Owner/Pictures.cshtml
    - Cabinet.Service/Pages/Owner/Pictures.cshtml.cs
    - Cabinet.Service/wwwroot/js/owner-link.js
    - Cabinet.Service/wwwroot/js/owner-pictures.js
    - Cabinet.Service/wwwroot/js/owner-format.js
    - Cabinet.Service/wwwroot/css/owner.css
    - build/tests/owner-format.test.mjs
    - Cabinet.IntegrationTests/OwnerLinkPreviewTests.cs
    - Cabinet.BrowserTests/OwnerLinkFlowTests.cs
key-decisions:
  - "Megabytes are millions of bytes with at most one decimal, as the plan's Node example requires, so the 12 MiB default limit reads 12.6 MB"
  - "Remove link is a second submit button in the link form (formaction), after Check link, so Enter in the field still checks the link and the button sits next to it"
  - "The link form is novalidate: native URL validation would block a pasted non-address before the server could answer with its own message"
  - "Removing a link that was the choice also clears Checked, so the game asks for a look again by the normal rule"
  - "Staging expiry, cap and token-shape tests and the preview shape-source unit tests already existed from the previous plan and were kept, not duplicated"
requirements-completed: [IMG-02]
duration: about 2h
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 21: Links that go wrong, and links that stay Summary

**Every pasted-link problem answers in its own words inline, the preview's verdict, shape source, 3D note and disclaimer are proven in a real browser, the Your link tile stays a choice after switching away, and Remove link removes the link and its copy in one undoable, logged step.**

## What was built

- **Per-problem messages.** `OwnerStrings.LinkError(problem, host, bytes, limit)` holds the exact copy for all ten problems. Each problem has a refusal key (`LinkKey`); redirects and JSON answers carry only keys from a fixed set. The `CheckLink` JSON adds `problem` and `message`; the script shows the lead sentence in semibold, sets `aria-invalid` and `aria-describedby`, and returns focus to the field. Without script the page notice shows the same words (a too-large picture's size comes from a numeric query value shown only as MB).
- **Remove link.** `PictureEdits.RemoveLink` drops the link and its record, falls back to Automatic when the link was the choice, and logs `Removed the picture link of {title}`. The handler answers `link-removed` (status `Link removed` with Undo). After an Undo, a link whose stored files are gone is forgotten by a system write so the tile and mark read `Fetching...` / `Fetching your link...`, and `RequestLinkWork()` asks the worker to fetch it.
- **Stored link stays a choice.** The tile is drawn by one function for the three states (picture, fetching, none). The disclosure shows `Current link: {host}/.../{file}` (full address in `title`, ellipsis) and `Remove link` while a link is stored. `Yours` and `Fetching your link...` marks now follow the `choice` and `fetching` flags of every answer, so switching to Automatic removes `Yours` and picking the link tile restores it.
- **Preview contents.** Already rendered by the previous plan; now covered in a browser for 3D shot with a BGG size, 3D shot without, flat cover without a size, the disclaimer, and the spine's shortened label.

## Verification

- Unit 2313, integration 388, browser 243 passed; `node --test build/tests/*.test.mjs` 314 passed.
- `10-repo-rules.sh`, `40-secrets.sh` (working tree, no leaks) and `50-script-tests.sh` pass.
- Browser cases: ten refusals (blank, not an address, gallery page link, other host, 404, timeout via the fake clock, too large with sizes, unreadable bytes, too many pixels, very thin picture), each asserting the exact copy, `role="alert"`, `aria-invalid`, focus in the field and an unchanged revision; no-script messages; three preview variants; switching tiles; Remove link and Undo (script and no script); Undo with pruned files showing `Fetching...` then the stored copy; replacing a link drops the old copy from the owner data's references.
- Screenshots (scratch directory, not committed) at 1440, 390 and 320 px were looked at: error under the field, stored-link line and `Remove link`, removed state, fetching tile, previews. One change came from review: at 320 px the `Current link:` label wrapped, so it is now a non-shrinking label and only the address is cut with an ellipsis. No sideways scroll; controls at least 44 px.

## Deviations from Plan

**1. [Rule 3 - Blocking] Link form made `novalidate`** - the `type="url"` field would stop a non-address before the server could answer with its own message. Commit 7c65ecd.

**2. [Rule 1 - Bug] `Yours` mark did not follow a tile switch** - the script never updated it after choosing Automatic; the answers now carry `choice` and the row applies it. Commit 7c65ecd.

**3. Single commit** - the three tasks share files (the page model, link script, browser test class), so they went in one commit rather than three that would not build alone.

**4. Red-first only partly honoured** - the unit tests for the new members were written before them and did not compile; the browser tests were written alongside the UI. The first browser run failed only on two test-setup faults (a status that already read `Saved`, and a start-up sync that waits on a jitter timer), fixed in the tests.

**5. `megabytes` and `shortAddress` in the script** - required by the plan's Node tests. `shortAddress` is used as the fallback for the stored-link line; `megabytes` is exported and tested but the messages are composed on the server, so nothing in the page calls it.

## Known Stubs

None. Not done, by design of the existing page: the Your link tile does not poll, so after an Undo of a removal it reads `Fetching...` until the page is loaded again, when the stored copy shows.

## Threat Flags

None. Register items: staging is capped (8) and expires (15 minutes) from the previous plan; messages are fixed copy with host and sizes only and written with `textContent` (a unit test proves no pasted text appears); `RemoveLink` and Undo keep antiforgery (a post without the token is 400, and the page does not exist on the public port); no address is logged.

## Self-Check: PASSED

- Files exist: LinkErrorTests.cs, RemoveLinkTests.cs, and the modified files listed above.
- Commit found: 7c65ecd.

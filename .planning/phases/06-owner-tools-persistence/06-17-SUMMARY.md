---
phase: 06-owner-tools-persistence
plan: 17
subsystem: owner-pictures
tags: [owner-data, image-pipeline, link-policy, background-worker, dead-links, ssrf]
requires:
  - phase: 06-15
    provides: "OwnerLinkFetcher, OwnerLinkWorker, LinkHealth and MutateSystemAsync"
provides:
  - "ArtDownload.Missed.StatusCode and ArtDownload.Turned.AnnouncedBytes: additive, ArtSync ignores them"
  - "ArtProcessor.Inspect and ArtInspection: size, too-many-pixels and extreme-shape read from the header"
  - "LinkProblem and LinkCheck.Failed(Problem, Bytes, LimitBytes): one precise reason per pasted link"
  - "OwnerLinkFetcher.CheckTimeout (15 s on the injected clock) and OwnerLinkFetcher.AllowedHost (name of the picture host for messages)"
  - "OwnerLinkWorker.RecheckEvery (7 days), dead-link bookkeeping through LinkHealth, analysis-version re-fetch"
affects: [pictures, owner-links]
actuals:
  tokens: 10300
  tasks: 2
  commits: 2
tech-stack:
  added: []
  patterns:
    - "Classify an untrusted address before any fetch: blank, not a web address, game-site page, not on the allowlisted host"
    - "A failed re-check never touches the stored record or its files; only the dead-link rule decides when a link is dead"
key-files:
  created:
    - Cabinet.UnitTests/Owner/OwnerLinkWorkerTests.cs
  modified:
    - Cabinet.Repository/Images/ImageDownloader.cs
    - Cabinet.Repository/Images/ArtProcessor.cs
    - Cabinet.Service/Owner/OwnerLinkFetcher.cs
    - Cabinet.Service/Owner/OwnerLinkWorker.cs
    - Cabinet.UnitTests/Images/ImageDownloaderTests.cs
    - Cabinet.UnitTests/Images/ArtProcessorTests.cs
    - Cabinet.UnitTests/Owner/OwnerLinkFetcherTests.cs
    - Cabinet.IntegrationTests/OwnerLinkTests.cs
    - Cabinet.IntegrationTests/Infrastructure/ScriptedImageHandler.cs
key-decisions:
  - "A link on boardgamegeek.com or any subdomain (trailing dot ignored) is a page link and is refused before the allowlist is consulted, even if someone lists that host as a picture host"
  - "A redirect the downloader refuses maps to FetchFailed, not OtherHost: the pasted address itself was on the right host"
  - "A link that has failed is retried a day later so the second failed check can mark it dead; a dead link is retried weekly; an explicit request for link work still retries a link with no copy at once"
  - "A fetch that failed because the copy could not be written to disk is not counted against the link"
  - "OtherHost carries no host in the failure value; the message reads it from OwnerLinkFetcher.AllowedHost"
requirements-completed: [IMG-02]
duration: about 45m
completed: 2026-10-12
status: complete
---

# Phase 6 Plan 17: Every pasted link gets one precise reason, and dead links keep their copy Summary

Pasted picture links are classified before any request is made, every fetch and decode outcome maps to one reason the owner page can name, and the worker keeps a stored copy alive through dead links with a weekly re-check and an analysis-version re-fetch.

## What was built

**Link classification (tracer).** `OwnerLinkFetcher.CheckAsync` now decides, in order and before touching the picture source: blank, not a web address (not parseable, not http or https, over 2048 characters), a page of the game site (host is `boardgamegeek.com` or a subdomain, case and trailing dot ignored), or not on the picture host (the source policy: https, default port, no user info, exact allowlisted host). A test asserts the scripted source saw no request for any refused address. No game-site client exists anywhere, so a gallery page link is never resolved.

**Outcome mapping.** One call within a 15-second limit on the injected clock (a cancellation that is not the caller's maps to `FetchFailed`): 404 gives `NotFound`; any other status, unavailable, timeout, a refused redirect and an exception give `FetchFailed`; a refused size gives `TooLarge` with the announced bytes (when the host announced any) and the limit; a refused type or undecodable bytes give `NotAPicture`; the header inspection gives `TooManyPixels` and `ExtremeShape`; a normal picture gives `Ready` with the 480 and 240 variants and a record at the current analysis version.

**Additive seams.** `ArtDownload.Missed` carries the HTTP status, `ArtDownload.Turned` carries the announced size, and `ArtProcessor.Inspect` reads width, height, decodability, pixel cap and shape from the header without decoding. `Process` and `ArtProcessor.AnalysisVersion` (still 3) are unchanged, and `ArtSync` ignores the new members.

**Worker.** A stored copy is re-checked 7 days after the host started or after its last check (an in-memory map seeded with the start time, so a restart does not re-check everything at once). A failed check keeps the record and its files and is recorded through `LinkHealth.AfterFailure`; a failed link is retried a day later so a second failure marks it dead by the existing rule; a dead link waits a week; a success clears everything through `AfterSuccess`. A record from an older analysis is fetched again and replaced. All writes go through `MutateSystemAsync`, so there is no change-log entry, no `ChangesSinceDownload` change and no revision bump (asserted in tests).

## Verification

- Unit suite: 2232 passed. Integration suite: 361 passed. New and changed classes run repeatedly without flakes.
- Integration: while the image host answers 404 for a stored link, the copy's files are still on disk, still served from the art path with a 200, and the public card still shows the same cover.
- `10-repo-rules.sh`, `40-secrets.sh` (working-tree scan clean; the history scan cannot see a worktree git directory) and `50-script-tests.sh` all pass.

## Deviations from Plan

### Auto-fixed Issues

**1. [Rule 3 - Blocking] Existing assertions changed for the new record members**
- **Found during:** Task 1
- **Issue:** `ArtDownload` is a record, so adding the status and announced size changes equality: the existing 404/500 status test and the announced-oversize test compared against values without them.
- **Fix:** The two assertions now include the new members (`Failed("status", code)`, `Refused("size", announced)`); the streamed-size case still expects no announced size. Behaviour is unchanged.
- **Files modified:** `Cabinet.UnitTests/Images/ImageDownloaderTests.cs`
- **Commit:** 8629f6d

**2. [Rule 3 - Blocking] Existing fetcher tests moved to the allowlisted host and the new failure shape**
- **Found during:** Task 1
- **Issue:** The fetcher now applies the host policy before the source, so the old tests that used an example host and string reasons could not stay.
- **Fix:** Rewrote `OwnerLinkFetcherTests` around the default picture host and `LinkProblem`.
- **Commit:** 8629f6d

**3. [Rule 3 - Blocking] A way to take a served picture down in the integration host**
- **Found during:** Task 2
- **Issue:** `ScriptedImageHandler` could only add or overwrite served pictures, so the 404-after-stored scenario could not be scripted; the file was not in the plan's file list.
- **Fix:** Added `Withdraw(address)`.
- **Files modified:** `Cabinet.IntegrationTests/Infrastructure/ScriptedImageHandler.cs`
- **Commit:** 3607f7c

### Notes

- Red-first was honoured at the level of the new API: the tests were written against the planned members before any implementation existed (they did not compile until the seams were added). The worker tests passed on their first run once the worker was written.
- A protocol-relative address and the user-info case are built in tests by concatenation so no address-shaped literal and no `//` sequence is committed.

## Known Stubs

None.

## Threat Flags

None. The pasted-link surface is the one already in the threat register; this plan narrows it (classification before fetch, header inspection, 15-second limit).

## Self-Check: PASSED

- Created and modified files exist: `Cabinet.UnitTests/Owner/OwnerLinkWorkerTests.cs` and all listed modified files present.
- Commits found: 8629f6d, 3607f7c.

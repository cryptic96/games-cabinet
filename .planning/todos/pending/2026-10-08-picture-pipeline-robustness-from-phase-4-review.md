---
created: 2026-10-08T00:00:00.000Z
updated: 2026-10-09T00:00:00.000Z
title: Picture pipeline and enrichment robustness (phase 4 code review)
area: sync
severity: major
files:
  - Cabinet.Repository/Images/ImageDownloader.cs
  - Cabinet.Service/Sync/ArtSync.cs
  - Cabinet.Service/Sync/SyncRunner.cs
  - Cabinet.Domain/Collection/EnrichmentPlanner.cs
  - Cabinet.Service/Sync/EnrichmentSync.cs
  - Cabinet.Domain/Collection/BoxShape.cs
  - Cabinet.Domain/Collection/SnapshotMapper.cs
---

## Problem

The phase 4 code review (`.planning/phases/04-enrichment-box-images-shape/04-REVIEW.md`) found one critical issue and four warnings in the sync and mapping path. The owner chose to log them instead of fixing them before closing phase 4.

- **FIXED in 593468e — Critical (services CR-01):** `ImageDownloader` catches only `HttpRequestException` and timeouts. A failure while reading the picture body (an `IOException` such as a connection drop mid-download, an `InvalidDataException`, a `UriFormatException`) escapes. `ArtSync` has no per-picture catch, so `SyncRunner` stops the whole picture step ("Box pictures stopped early"). No record is written for that picture, so it is first in line again on every run and can starve the pictures after it.
  - Fix: the downloader maps `IOException` (incl. `HttpIOException`) and `InvalidDataException` to a failed download and reads a redirect target with `Uri.TryCreate` (an unusable `Location` is a failure by status); `ArtSync` wraps each picture so any exception except cancellation of the run token becomes a `Failed` record with the usual retry time and the step goes on. Tests: `ImageDownloaderTests` (broken body x3, unusable Location), `ArtSyncTests` (seven exception kinds, retry time, run cancellation), `BoxArtTests` broken-body integration test. Debug notes: `.planning/debug/picture-pipeline-robustness.md`.
- **FIXED in 9502203 — Services WR-01:** the picture body read has no deadline (`HttpClient.Timeout` does not cover the body with `ResponseHeadersRead`). A stalling image host holds the run until the overall run limit.
  - Fix: every picture request, headers and body together, runs under its own time limit (a `CancellationTokenSource` on the injected `TimeProvider`, linked with the run token); on expiry that picture is a failed download ("timeout") with the usual failed record and the run goes on. Validated setting `Images:DownloadTimeoutSeconds` (5 to 60, default 30), carried on `ArtLimits`; each redirect is a request of its own. Tests: `ImageDownloaderTests` (stalled body, silent headers, run cancellation), `ImageSettingsTests`, `BoxArtTests` stalled-body integration test on fake time. Not done: the review's optional extra of committing pending picture records when a run is cancelled (service stop or whole-run limit); with the per-request limit a stalled host no longer reaches the run limit.
- **Domain WR-04:** a game the details source never describes stays "new" and is requested again in every hourly run. Record the attempt.
- **Domain WR-02:** `BoxShape.FromCover` has no plausibility check, so a flat cover with an extreme aspect ratio can give a box side of a few millimetres, or zero.
- **Domain WR-03:** `SnapshotMapper.Explain` keeps the first of several entries sharing a collection id, so the result depends on source order, contrary to its documentation.

Related, fixed in the same pass (security audit T-04-16, escalated to high), f3f64dc: the analysis working copy is never scaled up and is capped at 96 x 960 pixels, pictures with one side more than ten times the other are refused from the header as undecodable, and `ArtProcessor.Process` turns any exception into an undecodable outcome. `AnalysisVersion` stays 3 (ordinary pictures give byte-identical facts).

Still open: domain WR-04, WR-02 and WR-03 above.

## Solution

Treat any failure while fetching or decoding one picture as a failed record for that picture (with the usual retry time), and give the body read its own deadline. Record details attempts so a missing game backs off. Add the plausibility check to `FromCover`, and make `Explain` order-independent. Each fix comes with a failing test first. All of these change server behaviour, so they ship in the next release.

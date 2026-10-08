---
created: 2026-10-08T00:00:00.000Z
updated: 2026-10-08T00:00:00.000Z
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

- **Critical (services CR-01):** `ImageDownloader` catches only `HttpRequestException` and timeouts. A failure while reading the picture body (an `IOException` such as a connection drop mid-download, an `InvalidDataException`, a `UriFormatException`) escapes. `ArtSync` has no per-picture catch, so `SyncRunner` stops the whole picture step ("Box pictures stopped early"). No record is written for that picture, so it is first in line again on every run and can starve the pictures after it.
- **Services WR-01:** the picture body read has no deadline (`HttpClient.Timeout` does not cover the body with `ResponseHeadersRead`). A stalling image host holds the run until the overall run limit.
- **Domain WR-04:** a game the details source never describes stays "new" and is requested again in every hourly run. Record the attempt.
- **Domain WR-02:** `BoxShape.FromCover` has no plausibility check, so a flat cover with an extreme aspect ratio can give a box side of a few millimetres, or zero.
- **Domain WR-03:** `SnapshotMapper.Explain` keeps the first of several entries sharing a collection id, so the result depends on source order, contrary to its documentation.

## Solution

Treat any failure while fetching or decoding one picture as a failed record for that picture (with the usual retry time), and give the body read its own deadline. Record details attempts so a missing game backs off. Add the plausibility check to `FromCover`, and make `Explain` order-independent. Each fix comes with a failing test first. All of these change server behaviour, so they ship in the next release.

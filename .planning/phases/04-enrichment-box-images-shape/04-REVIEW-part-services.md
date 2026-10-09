---
phase: 04-enrichment-box-images-shape
reviewed: 2026-10-08T00:00:00Z
depth: standard
files_reviewed: 49
files_reviewed_list:
  - Cabinet.FakeBgg/BggXml.cs
  - Cabinet.FakeBgg/Cabinet.FakeBgg.csproj
  - Cabinet.FakeBgg/FakeBggProgram.cs
  - Cabinet.FakeBgg/FakeBggServer.cs
  - Cabinet.FakeBgg/SyntheticArt.cs
  - Cabinet.FakeBgg/SyntheticBggCollection.cs
  - Cabinet.Repository/Bgg/BggCollectionParser.cs
  - Cabinet.Repository/Bgg/BggThingClient.cs
  - Cabinet.Repository/Bgg/BggThingParser.cs
  - Cabinet.Repository/Bgg/RequestPacer.cs
  - Cabinet.Repository/Images/ArtAnalysis.cs
  - Cabinet.Repository/Images/ArtCache.cs
  - Cabinet.Repository/Images/ArtProcessor.cs
  - Cabinet.Repository/Images/ArtUrl.cs
  - Cabinet.Repository/Images/BackdropMask.cs
  - Cabinet.Repository/Images/ImageDownloader.cs
  - Cabinet.Repository/Images/SubjectShape.cs
  - Cabinet.Repository/Storage/SnapshotStore.cs
  - Cabinet.Service/Collection/ArtFiles.cs
  - Cabinet.Service/Collection/CollectionStore.cs
  - Cabinet.Service/Layout/ArtSettings.cs
  - Cabinet.Service/Layout/LayoutEndpoint.cs
  - Cabinet.Service/Layout/LayoutSettings.cs
  - Cabinet.Service/Program.cs
  - Cabinet.Service/Review/ReviewSheet.cs
  - Cabinet.Service/Review/ReviewSheetCommand.cs
  - Cabinet.Service/Review/ReviewSheetModel.cs
  - Cabinet.Service/Sync/ArtSync.cs
  - Cabinet.Service/Sync/EnrichmentSettings.cs
  - Cabinet.Service/Sync/EnrichmentSync.cs
  - Cabinet.Service/Sync/ImageSettings.cs
  - Cabinet.Service/Sync/SyncEndpoints.cs
  - Cabinet.Service/Sync/SyncRunner.cs
  - Cabinet.Service/Sync/SyncStartup.cs
  - Cabinet.Service/appsettings.json
  - Cabinet.Service/wwwroot/css/cabinet.css
  - Cabinet.Service/wwwroot/js/copy.js
  - Cabinet.Service/wwwroot/js/render.js
  - README.md
  - build/bgg-access-check.py
  - build/tests/bgg-access-check-test.sh
  - build/tests/page-scripts.test.mjs
  - deploy/provision.d/10-packages.sh
  - deploy/tests/provision-logic-test.sh
  - docs/bgg-access-check.md
  - docs/bgg-sync.md
  - docs/cabinet-layout.md
  - docs/development.md
  - docs/review-sheet.md
findings:
  critical: 1
  warning: 4
  info: 6
  total: 11
status: issues_found
---

# Phase 4: Code Review Report (services, pipeline and ops part)

**Reviewed:** 2026-10-08
**Depth:** standard
**Files Reviewed:** 49
**Status:** issues_found

## Summary

The security-critical surface holds up. The token is attached only by `BggAuthHandler`, and that handler is wired only
onto the collection and details clients. The picture client uses a separate handler chain with no auth handler,
`AllowAutoRedirect = false` and a per-hop policy check (https, default port, no user info, exact host). `/art` is served
through a `PhysicalFileProvider` rooted at the art directory, with `.webp` as the only mapped type. The BGG thing parser
prohibits DTDs, removes the resolver and caps the document size. `render.js` builds everything with `textContent` and
`style.setProperty`, validates the picture path, fit, sizes and colours, and never uses `innerHTML`. The access-check
script only talks to `boardgamegeek.com` and the one known image host, and guards its report before printing it. No
planning references, `//` comments or personal data were found in the listed files.

The failure-isolation story for the picture step, which the code and docs both promise, is not complete. One
unhandled exception type from the download path aborts the whole picture step and repeats on every run. The review-sheet
command also has a counting bug and a layout bug, and it can mutate live state.

## Critical Issues

### CR-01: A body-read failure from one picture aborts the whole picture step on every run (not isolated)

**File:** `Cabinet.Repository/Images/ImageDownloader.cs:163-170,192-211` and `Cabinet.Service/Sync/ArtSync.cs:80,159`
**Issue:** `FetchOnceAsync` catches only `HttpRequestException` and a non-requested `OperationCanceledException`. The body
is streamed with `HttpCompletionOption.ResponseHeadersRead`, so a connection reset, a truncated body or a bad
`Content-Encoding` surfaces during `body.ReadAsync` as `IOException` (in .NET 7 and later `HttpIOException` derives from
`IOException`, not from `HttpRequestException`). A broken gzip stream surfaces as `InvalidDataException`. A malformed
`Location` surfaces as `UriFormatException` at line 157. None of these is caught in the downloader, and `ArtSync.FetchAsync`
has no catch either. The exception reaches the generic catch at `SyncRunner.cs:270`, which logs "Box pictures stopped
early" and ends the step.

Consequences:
1. No `ImageRecord` is written for the offending address, so it is still "due".
2. Pictures are walked in a deterministic order (`CollectionId`, then `GameId`). The same picture is therefore tried first
   on every run, fails the same way and blocks every picture after it. A single flaky file permanently starves the rest of
   the collection of art.
3. Up to nine fetched and stored pictures since the last commit are lost (files were written to disk, records were not
   committed), and `Prune` is skipped.

This contradicts the stated contract in `ArtSync` and `SyncRunner` ("A picture that goes wrong is only recorded; it never
fails the run") and in `docs/bgg-sync.md`.

**Fix:**
```csharp
catch (Exception exception) when (exception is HttpRequestException or IOException or InvalidDataException or UriFormatException)
{
    return (ArtDownload.Failed("unavailable"), null);
}
```
Also wrap the per-picture body of the loop in `ArtSync.RunAsync` so any non-cancellation exception from
`source.DownloadAsync` or `ArtProcessor.Process` records an `ImageStatus.Failed` record for that address and continues:
```csharp
try { images[address] = await FetchAsync(source, address, counts, cancellationToken); }
catch (Exception exception) when (exception is not OperationCanceledException)
{
    counts.Failed++;
    images[address] = new ImageRecord(address, ImageStatus.Failed, time.GetUtcNow());
}
```

## Warnings

### WR-01: A slow picture body has no deadline; the 10-minute run limit then fails the whole run and drops uncommitted records

**File:** `Cabinet.Repository/Images/ImageDownloader.cs:152,192-211`, `Cabinet.Service/Sync/SyncEndpoints.cs:93`, `Cabinet.Service/Sync/SyncWorker.cs:48-58`
**Issue:** `HttpClient.Timeout` (60 s) only bounds the time to the response headers when `ResponseHeadersRead` is used. The
body loop is bounded only by the caller's token. A host that sends headers and then dribbles bytes (or stalls) holds the
picture pacer lease and the run until `SyncWorker.RunLimit` (10 minutes) cancels it. The six-minute `ExtrasDeadline`
only gates the start of a download, so it does not help. When the limit fires, `OperationCanceledException` propagates
out of `FetchPicturesAsync` (its catch excludes cancellation), and `RunOnceAsync` reports `SyncResult.Failed` with
`SyncFailure.Timeout`. This happens even though the collection was already stored and shown, and it loses the up-to-nine
uncommitted picture records. `ArtSync`'s doc comment ("a slow host cannot hold the run") and `docs/bgg-sync.md` ("a slow
host cannot hold a sync back") are therefore not true for slow bodies.
**Fix:** Give each download its own deadline that covers the body read:
```csharp
using var perFile = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
perFile.CancelAfter(DownloadTimeout);
```
Pass `perFile.Token` to `SendAsync` and `ReadAsync`, and map a `perFile`-only cancellation to `ArtDownload.Failed("timeout")`.
In `ArtSync.RunAsync`, commit pending records in a `finally` so a cancelled run keeps what it fetched.

### WR-02: The review-sheet summary line undercounts 3D verdicts

**File:** `Cabinet.Service/Review/ReviewSheetCommand.cs:250-255` (with `ReviewSheetModel.cs:377-384`)
**Issue:** `Count(rows, "3D shot")` compares with `==`, but `VerdictWord` also produces `"3D shot, cut-out"` and `"3D shot,
tight crop"`. Those rows are counted in none of the four buckets (flat, 3D shot, unsure, no verdict), so the printed
counts do not add up to the number of games. `docs/review-sheet.md` promises "how many verdicts ... of each kind".
**Fix:** Count by prefix for the 3D bucket, for example
`rows.Count(row => row.Verdict.StartsWith("3D shot", StringComparison.Ordinal))`, or expose the verdict as an enum on
`ReviewRow` and count that.

### WR-03: Long verdict text overruns the next column on the review sheet

**File:** `Cabinet.Service/Review/ReviewSheet.cs:313,43-44`
**Issue:** The verdict is drawn unclipped in a 150 px column in 17 px bold. `"3D shot, tight crop"` and `"3D shot,
cut-out"` measure well over 150 px, so they are drawn across the start of the "Chosen" column, and the chosen text is
painted over them. These are the two verdicts the review most needs to read.
**Fix:** Wrap the verdict onto two lines (split at the comma) or widen `VerdictWidth` and shrink another column. A clip
rectangle with `Ellipsised(...)` would also be acceptable.

### WR-04: The "read-only" review-sheet command can rename the live collection file

**File:** `Cabinet.Service/Review/ReviewSheetCommand.cs:82`, `Cabinet.Repository/Storage/SnapshotStore.cs:93-116`
**Issue:** The operator command calls `SnapshotStore.Load()`, which moves `snapshot.json` to `snapshot.json.bad` when the
file fails to parse, is malformed or has a newer schema than the running binary. Running the command against the live
state directory (as the docs instruct, as the service user) can therefore set aside the production collection, for
example when the installed binary is older than the one that wrote the file, or `--state` points at a live directory
during a deploy window. A command documented as reading the collection should not change it.
**Fix:** Add a non-mutating load for read-only callers (a `Load(setAside: false)` overload, or a separate `Peek()`), and
use it here. At minimum, document the side effect in `docs/review-sheet.md`.

## Info

### IN-01: The decode is not actually scaled for formats without scaled decoding, so a 36-megapixel cap means a ~144 MB transient buffer

**File:** `Cabinet.Repository/Images/ArtProcessor.cs:42-45,98-109`
**Issue:** `Decode` relies on `GetScaledDimensions`. Skia returns the full size for codecs without scaled decode (PNG in
particular), so `SKBitmap.Decode` allocates width x height x 4 bytes, up to 144 MB at the default `MaxMegapixels = 36`.
The class doc ("the decode itself is scaled so a large picture never fills memory") is only true for JPEG and WebP. On the
small shared host this is a transient spike inside the public site's process.
**Fix:** Soften the doc comment and the docs, or lower the default pixel cap, or decode PNG with a sub-sampled approach.

### IN-02: `docs/bgg-sync.md` misstates the stored widths for mid-size pictures

**File:** `docs/bgg-sync.md` ("Box art" section: "a smaller picture keeps its own width"), `Cabinet.Repository/Images/ArtProcessor.cs:62-64,111-129`
**Issue:** The code gives a 240 px file only for a source between 240 and 480 px wide; only a source narrower than 240 px
keeps its own width. The doc suggests any picture under 480 px keeps its width.
**Fix:** Say "a picture narrower than 480 pixels gets only the 240 pixel file, and one narrower than 240 pixels keeps its
own width".

### IN-03: Unused member `BackdropMask.Build`

**File:** `Cabinet.Repository/Images/BackdropMask.cs:29-30`
**Issue:** `Build` has no caller anywhere in the repository (`Analyse` is used directly).
**Fix:** Remove it.

### IN-04: A response with no Content-Type is accepted as a picture

**File:** `Cabinet.Repository/Images/ImageDownloader.cs:180-185`
**Issue:** The type check is `mediaType is not null && !StartsWith("image/")`, so a missing header skips the check. The
doc comment says a body that is "not a picture" is refused. Decoding catches junk later, so this is low impact, but the
check is weaker than described, and it spends the full download before the decode refuses it.
**Fix:** Refuse a missing media type too, or note in the comment that the decoder is the real gate.

### IN-05: Inconsistent image-host suffix matching in the access-check script

**File:** `build/bgg-access-check.py:1254` (`classify_redirect_host`) versus `:929` (`classify_image_url`)
**Issue:** `classify_redirect_host` uses `host.endswith("geekdo-images.com")`, which also matches an unrelated host such as
`evilgeekdo-images.com`. `classify_image_url` correctly requires a dot boundary. Classification output only, and no request
is ever sent to such a host, but the two should agree.
**Fix:** Use `host == IMAGE_HOST_SUFFIX or host.endswith("." + IMAGE_HOST_SUFFIX)` in both places.

### IN-06: Review-sheet command edge cases: ignored option, uncaught path errors, split surrogate pairs

**File:** `Cabinet.Service/Review/ReviewSheetCommand.cs:176-184,229-247`, `Cabinet.Service/Review/ReviewSheet.cs:430-445`
**Issue:**
- `--bold-font` is silently ignored when `--font` is not given.
- `TryWrite` catches only `IOException` and `UnauthorizedAccessException`, so an `--out` value with invalid path
  characters (`ArgumentException` / `NotSupportedException`) ends in an unhandled-exception stack trace instead of exit
  code 5.
- `Ellipsised` truncates by UTF-16 code unit, so a title with an astral character (emoji) can be cut through a surrogate
  pair and draw a replacement glyph.
**Fix:** Reject `--bold-font` without `--font` as a usage error, add `ArgumentException or NotSupportedException` to the
catch, and step back one char when the cut lands on a low surrogate.

---

_Reviewed: 2026-10-08_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

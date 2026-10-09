---
status: awaiting_human_verify
trigger: "Fix three confirmed defects in the picture pipeline: T-04-16 oversized analysis working copy (escalated to high), CR-01 a mid-download failure stops the picture step, WR-01 no deadline on the picture body read. Test-first."
created: 2026-10-09T00:00:00Z
updated: 2026-10-09T10:15:00Z
---

## Current Focus

hypothesis: three independent defects, each confirmed by a failing test and fixed (f3f64dc, 593468e, 9502203)
test: full gates on 9502203 plus the planning commit
expecting: owner confirms in a real sync (next release) that a broken or stalled picture is recorded and the run goes on
next_action: owner verification on the server after the next release; then archive this session and add the knowledge-base entry
reasoning_checkpoint:
  hypothesis: "T-04-16: ArtAnalysis.WorkingCopy scales every picture to 96 px wide with an uncapped height, so a hairline picture becomes a working copy of over 150 M pixels, and ArtProcessor's narrow catch filter lets SkiaSharp's plain System.Exception escape. CR-01: ImageDownloader catches only HttpRequestException and timeouts and ArtSync has no per-picture catch, so a body-read IOException/InvalidDataException or a UriFormatException from the Location ends the whole picture step with no record. WR-01: nothing bounds a request once ResponseHeadersRead returns, so a stalled body holds the run until the run limit."
  confirming_evidence:
    - "GC.GetAllocatedBytesForCurrentThread around Analyse(1x16383) = 5,385,588,792 B and Process(1x16383) = Done before the fix; 4,360 B and Undecodable after"
    - "Injected System.Exception/OutOfMemoryException/IndexOutOfRangeException/NullReferenceException escaped Process before the fix"
    - "ArtSync unit: seven exception kinds escaped RunAsync with no record; integration: first run requested only version-900001"
    - "Stalled body and silent headers never completed with the fake clock past 30 s; integration run never ended"
  falsification_test: "If ordinary pictures changed features, the 35-line facts dump would differ (it is byte-identical); if the catch-alls were the only fix, the shape/working-copy and deadline tests would still fail (mutants M2-M4, M12-M15 killed)"
  fix_rationale: "Each fix sits at the root cause: the working-copy size function, the processor's exception boundary, the downloader's exception classification plus a per-picture boundary in ArtSync, and a per-request time limit on the injected clock"
  blind_spots: "Real SocketsHttpHandler timing on a slow host was not exercised (scripted streams only); a PNG at the pixel cap is still decoded whole (about 144 MB at 36 MP); pending picture records are still dropped if the whole run is cancelled"
  candidate_causes:
    - "code: working-copy sizing, catch filters, missing per-picture boundary, missing request deadline"
    - "config: no time limit setting for picture requests (now Images:DownloadTimeoutSeconds)"
    - "data: hostile or degenerate picture headers (hairline shapes) and hosts that break off or stall"
  and_gate: "yes for T-04-16 (a degenerate shape AND the upscaling copy AND the narrow catch together turn a tiny file into a crash or a multi-GB peak); no for CR-01 and WR-01 (each single missing guard suffices)"
plan:
  - T-04-16: ArtAnalysis.WorkingSize never wider than 96 or the picture, never taller than 960, aspect kept; ArtAnalysis.HasUsableShape (longer side at most 10x the shorter) checked by ArtProcessor on the header before decoding -> Undecodable; ArtProcessor catches every non-cancellation exception -> Undecodable; seam: Process overload taking the measure step
  - CR-01: ImageDownloader maps IOException (HttpIOException) and InvalidDataException to Failed("unavailable"); a Location that cannot be combined becomes Failed("status") via Uri.TryCreate; ArtSync wraps each picture so any exception except run-token cancellation becomes a Failed record and the loop continues
  - WR-01: per-request deadline (headers and body) from a linked CancellationTokenSource on the injected TimeProvider; validated setting Images:DownloadTimeoutSeconds (5..60, default 30) carried on ArtLimits
baseline:
  - full gate at d7f679e: 1958 tests, 0 failed
  - facts dump of every synthetic kind + 11 sizes saved to scratchpad facts-before.txt for the byte-identical comparison
bug_class: Bohrbug (all three are deterministic given the input)
symptoms_prefilled: true
goal: find_and_fix
tdd_mode: true

## Symptoms

expected: |
  - A degenerate picture (1x16383, 4x16383, extreme aspect) is analysed on a small working copy or refused as unusable; the process never allocates hundreds of MB.
  - Any exception from decode, resize, analysis or encode becomes an undecodable outcome for that one picture.
  - Any failure fetching one picture (body-read IOException, InvalidDataException, UriFormatException, a stalled body) gives a failed record for that picture and the run moves on to the next picture.
actual: |
  - ArtAnalysis.WorkingCopy always scales to 96 px wide; height follows the aspect ratio. 1x16383 PNG (225 bytes) -> 96x1572768 working copy; auditor measured 4.1 GB peak, 5.4 GB allocated, 11 s CPU, outcome Done. 4x16383 -> 1.0 GB peak. Production container has 1 GB.
  - SkiaSharp throws plain System.Exception when pixel allocation fails; ArtProcessor's catch filter (ArgumentException/InvalidOperationException/IOException/NotSupportedException) does not catch it.
  - ImageDownloader.FetchOnceAsync catches only HttpRequestException and non-run OperationCanceledException; IOException/HttpIOException, InvalidDataException, UriFormatException escape. ArtSync has no per-picture catch; SyncRunner logs "Box pictures stopped early" and ends the step; no record written, so the same picture blocks the queue every run.
  - HttpClient.Timeout does not cover the body with ResponseHeadersRead; a stalling body holds the run until SyncWorker.RunLimit; run recorded Failed/Timeout and uncommitted records lost.
errors: "System.Exception from SkiaSharp allocation; IOException/HttpIOException mid-body; 'Box pictures stopped early: <type>' warning"
reproduction: "Synthetic: ArtProcessor.Process on a 1x16383 PNG; scripted image handler that throws IOException mid-body or stalls the body"
started: "since the picture pipeline landed"

## Eliminated

## Evidence

- timestamp: 2026-10-09T00:00:00Z
  checked: Cabinet.Repository/Images/ArtAnalysis.cs WorkingCopy (lines 130-140)
  found: width is always AnalysisWidth (96); height = round(source.Height * 96 / source.Width) with no cap; no guard against upscaling
  implication: a 1-px-wide source yields a 96 x (96 * height) working copy; for height 16383 that is 96 x 1,572,768 = 151 M pixels, 604 MB per Rgba8888 bitmap plus SKColor[] copy and masks

- timestamp: 2026-10-09T00:00:00Z
  checked: Cabinet.Repository/Images/ArtProcessor.cs Process catch filter (line 92)
  found: catches only ArgumentException, InvalidOperationException, IOException, NotSupportedException
  implication: SkiaSharp's plain System.Exception on allocation failure escapes Process

- timestamp: 2026-10-09T00:00:00Z
  checked: Cabinet.Repository/Images/ImageDownloader.cs FetchOnceAsync (142-171), ReadAnswerAsync (173-212); Cabinet.Service/Sync/ArtSync.cs RunAsync (80), FetchAsync (148-179); SyncRunner.FetchPicturesAsync (248-274)
  found: only HttpRequestException and a non-run OperationCanceledException are mapped to a failed download; ArtSync awaits FetchAsync with no catch; SyncRunner catches non-cancellation exceptions around the whole step
  implication: one throwing picture ends the whole step with no record, so it is retried first on every run (CR-01)

- timestamp: 2026-10-09T00:00:00Z
  checked: ImageDownloader body read uses HttpCompletionOption.ResponseHeadersRead and only the run token
  found: no per-download deadline covers the body read
  implication: a stalled body holds the run until SyncWorker.RunLimit (WR-01)

- timestamp: 2026-10-09T00:00:00Z
  checked: probe of new Uri(new Uri("https://cf.example.org/a.png"), new Uri("//", UriKind.RelativeOrAbsolute)) and ten similar Location values
  found: every one throws UriFormatException on combine
  implication: a host answering a redirect with Location "//" escapes ImageDownloader.FetchOnceAsync today (CR-01 third trigger); Uri.TryCreate(base, relative, out) avoids the exception

- timestamp: 2026-10-09T00:00:00Z
  checked: Cabinet.Service/Sync/SyncEndpoints.cs image client registration; Cabinet.IntegrationTests/Infrastructure/SyncHarness.cs
  found: ImageDownloader is a typed client built by ActivatorUtilities (the test harness re-registers it the same way); ArtLimits is a DI singleton from ImageOptions.Limits; TimeProvider is registered (fake clock in tests)
  implication: a deadline can reach the downloader through ArtLimits plus a TimeProvider constructor parameter with no new DI registration

- timestamp: 2026-10-09T00:00:00Z
  checked: existing test ArtProcessorTests.A_very_wide_source_is_scaled_to_its_own_ratio_and_never_to_zero_height (2000x3)
  found: with a 10:1 shape limit this picture is refused, so the test's expectation changes by design; the zero-height guard in variant sizing is unreachable through Process once the shape limit holds (480 * 1/10 = 48)
  implication: rewrite that test to the new rule (refused) and keep a ratio test at the limit (2000x200 -> 480x48, 240x24)

- timestamp: 2026-10-09T00:00:00Z
  checked: T-04-16 RED run (new ArtWorkingCopyTests + ArtProcessorTests) against behaviour-preserving seams (WorkingSize = old formula, HasUsableShape = true, Process overload with the old catch filter)
  found: 29 of 86 failed. Managed allocation measured by GC.GetAllocatedBytesForCurrentThread around ArtAnalysis.Analyse: 1x16383 -> 5,385,588,792 bytes; 4x16383 -> 1,346,522,280 bytes (matches the audit's 5.4 GB allocated). ArtProcessor.Process 1x16383 -> 5,385,592,736 bytes and outcome Done. 1001x100, 100x1001, 2000x3 -> Done. Injected System.Exception("Unable to allocate pixels for the bitmap."), OutOfMemoryException, IndexOutOfRangeException, NullReferenceException all escape Process; ArgumentException is already caught.
  implication: T-04-16 reproduced deterministically; root cause confirmed as the upscaling, uncapped working-copy size plus the narrow catch filter

- timestamp: 2026-10-09T00:00:00Z
  checked: T-04-16 GREEN after the fix (commit f3f64dc); facts dump of every synthetic kind + 11 sizes compared with the baseline; post-fix measurement
  found: ArtWorkingCopyTests, ArtProcessorTests, ArtAnalysisTests, ReviewCaseVerdictTests 169/169 pass; full unit project 1802/1802 pass; the 35-line facts dump (features, colours and content-hashed file names) is byte-identical to the baseline. After the fix ArtProcessor.Process allocates 4,360 B for 1x16383 and 1,840 B for 4x16383 (refused from the header, nothing decoded); fed straight to ArtAnalysis.Analyse they get a 1x960 working copy and allocate 575,992 B and 574,712 B. A 600x800 picture keeps its 96x128 copy.
  implication: fix confirmed; AnalysisVersion stays 3 because ordinary pictures (at least 96 px wide, usable shape) get exactly the same working copy

- timestamp: 2026-10-09T00:00:00Z
  checked: manual mutation of the T-04-16 fix sites (no Stryker configured), each run against ArtWorkingCopyTests + ArtProcessorTests
  found: M1 shape boundary <= to < killed (11 failed); M2 drop never-scale-up killed (5); M3 drop height cap killed (4); M4 drop shape check in Process killed (7); M5 narrow the catch back killed (4)
  implication: the regression tests assert the root cause, not just the symptom

- timestamp: 2026-10-09T00:00:00Z
  checked: SKCodec.GetScaledDimensions for PNG 2000x2000 and 6000x6000, JPEG and WebP 2000x2000
  found: PNG returns the full size (no scaled decode); JPEG returns 1000x1000 (eighths); WebP returns 960x960
  implication: the processor's old class summary ("the decode itself is scaled") was only true for JPEG and WebP; a PNG at the 36 MP cap is decoded whole (about 144 MB of pixels). Summary and docs corrected; the decode bound itself is the existing pixel cap and is left as is

- timestamp: 2026-10-09T00:00:00Z
  checked: CR-01 RED run on f3f64dc (no production change): ImageDownloaderTests new cases, new ArtSyncTests, BoxArtTests broken-body integration test
  found: 12 of 27 unit cases failed: HttpIOException, IOException, InvalidDataException escape ImageDownloader.DownloadAsync; Location "//" throws UriFormatException; every one of seven exception kinds (incl. plain System.Exception and a stray TaskCanceledException not from the run token) escapes ArtSync.RunAsync with no record. The cancellation guard (run token cancelled -> OCE propagates) passes, as it should. Integration: first run requested only version-900001 ("Expected firstRun {version-900001} to contain version-900002").
  implication: CR-01 reproduced at the downloader, the step and the whole sync

- timestamp: 2026-10-09T00:00:00Z
  checked: CR-01 GREEN after the fix (commit 593468e) and manual mutation of its sites
  found: ArtSyncTests + ImageDownloaderTests 27/27, BoxArtTests 13/13; full unit project 1815/1815 (seen during M11's unfiltered run, apart from the 7 cases M11 broke). Mutants: M6 drop IOException from the downloader catch killed (2); M7 drop InvalidDataException killed (1); M8 back to throwing new Uri(base, location) killed (1); M9 narrow the ArtSync catch to IOException killed (5); M10 also swallow run cancellation killed (1); M11 drop counts.Failed++ killed (7). A lint rule reads the string "//" as a line comment, so the unit test uses the equally unusable Location "///" (the probe showed it throws the same UriFormatException).
  implication: fix confirmed; the tests pin both layers (downloader classification and the ArtSync safety net)

- timestamp: 2026-10-09T00:00:00Z
  checked: WR-01 RED run with plumbing only (ArtLimits.RequestTimeout, ImageDownloader TimeProvider parameter, Images:DownloadTimeoutSeconds 5..60 default 30) and no deadline in the request
  found: ImageDownloaderTests stalled body and silent headers both fail with TimeoutException after 10 s of real waiting (the fake clock moved past 30 s and nothing happened); run-cancellation guard passes; ImageSettingsTests pass. Integration: with Images:DownloadTimeoutSeconds=10 and the fake clock moved 10 s after the stall, the run never ended ("The condition did not hold within 15 seconds").
  implication: WR-01 reproduced; nothing bounds a request once its headers are in (and, with fake time, nothing bounds the headers either)

- timestamp: 2026-10-09T00:00:00Z
  checked: WR-01 GREEN after the fix (commit 9502203) and manual mutation of its sites
  found: ImageDownloaderTests + ImageSettingsTests + ArtSyncTests 76/76, BoxArtTests 14/14. Mutants: M12 body read on the run token only killed (1); M13 headers on the run token only killed (1); M14 limit doubled killed (2); M15 limit one second short killed (1); M16 default 31 s killed (1). The MSBuild multi-node build crashes intermittently in this sandbox with "Internal CLR error (0x80131506)" (also seen at baseline before any change); a retry always succeeded and every result above was taken on a clean build.
  implication: fix confirmed; the stalled-body test pins the exact limit from both sides

## Resolution

root_cause: "T-04-16: ArtAnalysis.WorkingCopy always scaled to 96 px wide with the height following the aspect ratio (upscaling narrow pictures, no height cap), AND ArtProcessor's catch filter did not include SkiaSharp's plain System.Exception; CR-01: ImageDownloader classified only HttpRequestException and timeouts, new Uri(target, location) could throw, and ArtSync had no per-picture exception boundary; WR-01: no time limit covered a picture request after ResponseHeadersRead returned"
fix: |
  f3f64dc  ArtAnalysis.WorkingSize: width min(96, picture), height capped at 960 (width rescaled), never upscaled; ArtAnalysis.HasUsableShape (longer side <= 10x shorter) checked by ArtProcessor from the header before decoding -> Undecodable; ArtProcessor.Process catches every exception -> Undecodable; Process overload taking the measure step (test seam); summary of the processor corrected (PNG has no scaled decode).
  593468e  ImageDownloader maps IOException (incl. HttpIOException) and InvalidDataException to Failed("unavailable") and uses Uri.TryCreate for the redirect target (Failed("status") when unusable); ArtSync.AttemptAsync wraps each picture: any exception except run-token cancellation -> Failed record at the attempt time, warning with the exception type only, loop continues.
  9502203  Per-request time limit (headers and body) via CancellationTokenSource(ArtLimits.RequestTimeout, TimeProvider) linked with the run token; Images:DownloadTimeoutSeconds 5..60 default 30 in ImageOptions/ImageSettings/appsettings.json/docs; ArtLimits gains RequestTimeout; ImageDownloader gains a TimeProvider parameter.
oracle_type: "specified (allocation budget, exact working-copy sizes, exact outcomes and records) plus derived (working copy of ordinary pictures equals the old formula; facts dump byte-identical)"
analysis_version: "stays 3: pictures at least 96 px wide with a usable shape get the identical working copy, so their features, colours and file names are unchanged (35-line dump identical)"
verification:
  target_test: { result: pass }
  mutation_check: { result: pass, reason_if_skipped: "Stryker not configured; replaced by 16 hand-seeded mutants at the fix sites", mutant_killed: "16 of 16 (M1-M16)" }
  no_op_deletion: { result: pass, deletion_justified_by_rca: true, note: "the only narrowing-to-broadening change is ArtProcessor's catch, justified by the RCA (SkiaSharp throws System.Exception)" }
  adjacent_tests: { result: pass, suites_run: ["dotnet test --solution Cabinet.slnx --no-build under unshare -rn: 2051/2051", "node --test build/tests/page-scripts.test.mjs: 92/92", "build/lint.sh: repo-rules, workflows, shell, secrets, script-tests all PASS"] }
  revert_and_reconfirm: { result: pass, bug_returned_on_revert: true, fixed_on_reapply: true, note: "each RED run executed the tests against the pre-fix logic (behaviour-preserving seams only); the mutants re-reverted each fix site individually and every one brought a failure back" }
  guardrail_verdict: accepted
files_changed:
  - Cabinet.Repository/Images/ArtAnalysis.cs
  - Cabinet.Repository/Images/ArtProcessor.cs
  - Cabinet.Repository/Images/ImageDownloader.cs
  - Cabinet.Service/Sync/ArtSync.cs
  - Cabinet.Service/Sync/ImageSettings.cs
  - Cabinet.Service/appsettings.json
  - Cabinet.Domain/Collection/CollectionSnapshot.cs
  - Cabinet.FakeBgg/Testing/TroubledBodies.cs
  - Cabinet.IntegrationTests/BoxArtTests.cs
  - Cabinet.IntegrationTests/Infrastructure/ScriptedImageHandler.cs
  - Cabinet.UnitTests/Images/ArtWorkingCopyTests.cs
  - Cabinet.UnitTests/Images/ArtProcessorTests.cs
  - Cabinet.UnitTests/Images/ImageDownloaderTests.cs
  - Cabinet.UnitTests/Sync/ArtSyncTests.cs
  - Cabinet.UnitTests/Sync/ImageSettingsTests.cs
  - Cabinet.UnitTests/Images/ArtAnalysisTests.cs
  - Cabinet.UnitTests/Images/ReviewCaseVerdictTests.cs
  - Cabinet.UnitTests/Review/ReviewFixture.cs
  - docs/bgg-sync.md
residual:
  - "A PNG within the pixel cap is decoded whole (PNG has no scaled decode): about 144 MB of pixels at the default 36 MP, up to 400 MB at the 100 MP maximum setting. Bounded by the existing cap; not changed here."
  - "Pending picture records (up to nine) are still dropped when the whole run is cancelled by a service stop or the run limit; the review's optional commit-in-finally was not done."
  - "Each redirect hop has its own time limit, so one picture can take up to four limits (2 minutes at the default) plus pacer gaps."
  - "The MSBuild multi-node build in this sandbox crashes intermittently with Internal CLR error (0x80131506), before and after the change; retries succeed."

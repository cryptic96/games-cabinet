---
status: awaiting_human_verify
trigger: "Post-merge browser-test flake: full browser suite fails 2-3 random tests per run with a 30 s TimeoutException in CabinetPageTest.GotoCabinetAsync waiting for '#cabinet .placement, .cabinet-message:not(.cabinet-loading)'. Wave 5 merge cf6828f (92 tests) is green; wave 6 (111 tests) flakes."
created: 2026-10-10T15:00:00Z
updated: 2026-10-10T16:20:00Z
---

## Current Focus

hypothesis: CONFIRMED - every CabinetWebApplicationFactory leaks one inotify instance (the serving host's web root PhysicalFileProvider watcher, created by asp-append-version and never disposed). Across a full browser run the leak plus the desktop session's own inotify use crosses the per-user limit of 256; from then on a new host either fails to build (appsettings reloadOnChange watcher, ~100 ms failure) or its Index page returns 500 (file version watcher), so the ready selector never matches (30 s timeout).
bug_class: Mandelbug (environment-dependent resource exhaustion; which tests fail depends on run order and on how many inotify instances the rest of the desktop holds)
test: Dispose the hosts' environment file providers in the factory; re-run the probe (expect count back to 0 after dispose) and the full suite 3x, also under an inotify hog.
expecting: test-process inotify count returns to ~0 after each factory; no inotify errors even with 80 instances hogged.
next_action: Owner confirms the full browser suite is green on their machine (and in CI), then archive this session to resolved/ and add the knowledge-base entry.

reasoning_checkpoint:
  hypothesis: "The browser suite times out in GotoCabinetAsync because the per-user inotify limit (256) is exhausted: each test factory leaks the serving host's web root PhysicalFileProvider watcher (ASP.NET Core never disposes IWebHostEnvironment file providers), so after ~100+ factories plus the desktop's ~85-140 instances new watchers fail and the Index page 500s."
  confirming_evidence:
    - "Failing run log: exactly 3 Kestrel 500s with 'inotify instances has been reached' from DefaultFileVersionProvider at Index.cshtml line 76, matching 3 timeouts."
    - "Probe: inotify fds 0 -> 2 (start) -> 3 (first page) -> 1 after DisposeAsync -> 1 after GC; one leaked per factory."
    - "KeyboardTests alone: count ends at 26 for 26 tests; HEAD full run: 104 still held after the last test."
    - "Wave 5 (normally green) fails 24/92 with the same 30 s timeouts and ~100 ms host-build inotify failures when 80 instances are hogged."
    - "Bisect: pull-out tip alone (103 tests) peaks at 242-243 user instances and passes; spine-label tip alone (100 tests) peaks at 230 and passes; together (111) cross 256."
  falsification_test: "If, after disposing the environment file providers, the test-process inotify count still grows per factory, or the suite still fails with 80 instances hogged, the hypothesis (or the fix) is wrong."
  fix_rationale: "The factory owns the hosts it builds in a long-lived test process, so it must release what they hold; disposing the environment file providers removes the leak itself instead of hiding it behind a bigger limit, polling watchers or less parallelism."
  blind_spots: "Chromium also holds a few inotify instances per browser process; the limit can still be reached if the desktop session alone approaches 256. The listen-time EADDRINUSE flake is a separate cause and is handled separately."
  candidate_causes:
    - "environment: per-user inotify limit (256) shared with the desktop session"
    - "code: test factory never disposes the host's web root file provider (leak)"
    - "code: wave 6 product changes (view transition, decode, label rule) hanging the page - eliminated by bisect and by wave 5 reproducing under an inotify hog"
    - "environment: CPU contention from heavier tests - eliminated: failures are 500s/IOExceptions, not slowness"
  and_gate: "yes - the failure needs both the per-factory leak (code) and a per-user limit with limited headroom (environment); the test count grew in wave 6, which is why it appeared then. Removing the leak is sufficient."

## Symptoms

expected: All 111 browser tests pass on every full run.
actual: 2-3 random tests per full run time out after 30 s in GotoCabinetAsync; the page never draws a box and never shows a cabinet message. One run had an 86 ms failure in the phone longest-title card test. Tests pass individually.
errors: System.TimeoutException Timeout 30000ms exceeded, waiting for Locator("#cabinet .placement, .cabinet-message:not(.cabinet-loading)")
reproduction: dotnet test --project Cabinet.BrowserTests --no-build (full suite)
started: after the wave 6 merge (pull-out animation + spine label rule); wave 5 merge cf6828f is green.

## Eliminated

- hypothesis: A wave 6 product change (view transition, cover decode, label rule) hangs the page before first draw.
  evidence: Each plan tip alone passes its full suite twice (pull-out 103/103, spine-label 100/100); wave 5 fails the same way when 80 inotify instances are hogged; the stuck pages are server 500s, so no page script runs at all.
  timestamp: 2026-10-10T15:30:00Z

- hypothesis: Plain CPU contention from the heavier sample-400 tests slows first draw past 30 s.
  evidence: Every failure has a matching inotify IOException (500 or host-build failure); the ~100 ms failures cannot be slowness.
  timestamp: 2026-10-10T15:30:00Z

## Evidence

- timestamp: 2026-10-10T15:00:00Z
  checked: scratchpad/browser-run.log (one failing full run, 3 failures)
  found: Exactly 3 Kestrel "unhandled exception" entries, each System.IO.IOException "The configured user limit (256) on the number of inotify instances has been reached" thrown from PhysicalFilesWatcher.TryEnableFileSystemWatcher via DefaultFileVersionProvider.AddFileVersionToPath at Pages/Index.cshtml line 76 (asp-append-version). 3 failures in the run, 3 such 500s.
  implication: The stuck pages are 500 responses for the Index page itself, so no cabinet script ever runs and the ready selector can never match.

- timestamp: 2026-10-10T15:00:00Z
  checked: /proc/sys/fs/inotify/max_user_instances and inotify fds held by the desktop session
  found: limit 256 per user; desktop processes already hold about 82 inotify instances; 16 cores; the run started 111 factories (222 "Now listening" lines, 2 listeners each).
  implication: The test process has roughly 170 instances of headroom; anything that keeps more than that alive at once fails.

- timestamp: 2026-10-10T15:10:00Z
  checked: Full HEAD browser run with a 0.2 s sampler of inotify fds in the test process and for the whole user (diag/head-run1.*)
  found: 2 failures. (1) KeyboardTests handled-key test: 30 s ready-selector timeout with one "inotify instances has been reached" Kestrel 500 in the log. (2) LanguageSwitchTests Dutch-notes test: SocketException "Address already in use" thrown from Socket.Listen inside Kestrel start (CabinetWebApplicationFactory.CreateHost line 183), 1.2 s. Test-process inotify count climbed monotonically to 106 and was still 104 after all tests had finished; the user total peaked at 250 of 256.
  implication: Inotify instances are leaked, not merely held by concurrent tests. A second, independent flake exists: a port clash that surfaces at listen() as a raw SocketException, which the factory's bind-retry filter (IOException wrapping AddressInUseException) does not catch.

- timestamp: 2026-10-10T15:12:00Z
  checked: KeyboardTests class alone (26 tests) with the sampler
  found: test-process inotify count rose 6, 12, 19, 25 and ended at 26 - exactly one per test (one factory per test). Desktop session held ~88 more (code, claude-desktop, chrome-headless, Discord...).
  implication: Each factory leaks exactly one inotify instance for the life of the test process.

- timestamp: 2026-10-10T15:15:00Z
  checked: Temporary probe test counting /proc/self/fd inotify links through one factory lifecycle
  found: start 0; after factory start 2 (appsettings reloadOnChange watchers, one per host); after GET /?sample=65 3 (asp-append-version -> DefaultFileVersionProvider -> serving host IWebHostEnvironment.WebRootFileProvider.Watch); after second GET 3; after factory DisposeAsync 1; after full GC 1. WebRootFileProvider is a PhysicalFileProvider, distinct per host.
  implication: The config watchers are released on dispose, but the serving host's web root PhysicalFileProvider (and its FileSystemWatcher/inotify instance) is never disposed by the host or the factory. 111 factories leak 111 instances; with the desktop's ~85-140 in use, the per-user limit of 256 is crossed late in the run. Wave 5's 92 factories stayed under it.

- timestamp: 2026-10-10T15:25:00Z
  checked: Wave 5 worktree full browser run while a helper process held 80 inotify instances (diag/w5-hog80.log)
  found: 24 of 92 failed - 30 s ready-selector timeouts plus ~70-160 ms failures whose message is the same inotify IOException thrown from FileConfigurationProvider (appsettings reloadOnChange) while the host builds.
  implication: The latent bug is in wave 5 already; wave 6 only added hosts. The ~86 ms failure seen earlier is the host-build flavour of the same exhaustion.

- timestamp: 2026-10-10T15:30:00Z
  checked: Bisect - pull-out tip 1e15535 and spine-label tip 98c64b2 (each on cf6828f), two full runs each with the sampler
  found: pull-out 103/103 twice, final test-process inotify 104, user peak 242/243; spine-label 100/100 twice, final 92, user peak 230.
  implication: Neither plan breaks anything on its own; together they add enough leaking hosts to cross 256.


root_cause:
fix:
verification:
files_changed: []

- timestamp: 2026-10-10T15:45:00Z
  checked: Probe after the fix (factory disposes both hosts' environment file providers)
  found: inotify fds 0 -> 2 -> 3 (page) -> 0 after dispose -> 0 after GC.
  implication: The leak is gone at its source.

- timestamp: 2026-10-10T16:00:00Z
  checked: Full browser runs after the fix, with the sampler
  found: 111/111 three times in a row (31.6 s, 31.8 s, 32.0 s); test-process inotify peak 31-36, back to ~3 at the end; user peak 173-176 (was 250 and failing). With 50 extra inotify instances held by a helper process: 111/111, user peak 230.
  implication: The run no longer depends on how much inotify headroom the desktop leaves.

## Resolution

root_cause: "Per-user inotify exhaustion caused by a test-infrastructure leak: ASP.NET Core never disposes IWebHostEnvironment's web root/content root file providers, and the first asp-append-version lookup on a page starts a FileSystemWatcher (one inotify instance on Linux) on the serving host's web root provider, so every CabinetWebApplicationFactory leaked one instance for the life of the test process. 111 browser tests leaked ~110; together with the desktop session's ~85-140 instances that crossed the 256 per-user limit late in the run, after which new hosts failed to build (~100 ms failure, appsettings reload watcher) or their index page returned 500 (file-version watcher) so GotoCabinetAsync timed out. Wave 6 only raised the host count from 92 to 111; neither plan has a product defect. Independently: a listen-time port clash (SocketException AddressAlreadyInUse from Socket.Listen) escaped the factory's bind-only retry filter."
fix: "CabinetWebApplicationFactory remembers each started host's web root and content root file providers (flattening composites) and disposes them after both hosts stop (from Dispose(bool), which the async dispose also reaches). Separately, the start retry now treats a bare SocketException(AddressAlreadyInUse) like the IOException/AddressInUseException Kestrel throws for bind-time clashes."
oracle_type: specified (process inotify handle count returns to its starting value after a factory is disposed; truth table for the port-clash shapes)
verification:
  target_test: { result: pass, test: "Cabinet.IntegrationTests/TestHostCleanupTests.cs:A_disposed_host_lets_go_of_every_inotify_instance_its_page_started; TestHostPortTests port-clash predicate tests" }
  mutation_check: { result: pass, reason_if_skipped: "no Stryker configured; hand-seeded mutants instead", mutant_killed: "removing KeepEnvironmentFileProviders(realHost) -> killed; removing KeepEnvironmentFileProviders(testHost) -> killed; removing the SocketException arm of IsPortClash -> killed. The dispose call lives only in Dispose(bool) after a mutant showed a DisposeAsync copy was redundant." }
  no_op_deletion: { result: pass, deletion_justified_by_rca: "n/a - additive change" }
  adjacent_tests: { result: pass, suites_run: ["dotnet test --solution Cabinet.slnx --no-build --filter-not-trait Category=Browser (2192 passed)", "node --test build/tests/*.test.mjs (221 passed)", "bash build/lint.sh (all PASS)", "browser suite 3x 111/111"] }
  revert_and_reconfirm: { result: pass, bug_returned_on_revert: true, fixed_on_reapply: true, detail: "factory change reverted -> cleanup test fails 'Expected 0, but found 1'; reapplied -> passes" }
  guardrail_verdict: accepted
files_changed:
  - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
  - Cabinet.IntegrationTests/TestHostCleanupTests.cs
  - Cabinet.IntegrationTests/TestHostPortTests.cs
commits: [0730a3e, 9f0c067]

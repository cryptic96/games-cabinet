---
phase: 08-public-hardening-go-public
fixed_at: 2026-10-25T00:00:00Z
review_path: .planning/phases/08-public-hardening-go-public/08-REVIEW.md
iteration: 1
findings_in_scope: 8
fixed: 8
skipped: 0
status: all_fixed
---

# Phase 8: Code Review Fix Report

**Fixed at:** 2026-10-25
**Source review:** .planning/phases/08-public-hardening-go-public/08-REVIEW.md
**Iteration:** 1

**Summary:**
- Findings in scope: 8 (1 critical, 7 warnings; info findings are out of scope)
- Fixed: 8
- Skipped: 0

Three fixes are partial by design and say so below (WR-01, WR-04, WR-07): each fixes the part of the finding that is the code's to fix and leaves the part that needs an owner decision.

## Fixed Issues

### CR-01: The per-address connect window is bypassed by connecting without a negotiate request

**Files modified:** `Cabinet.Service/Limits/LiveConnectWindow.cs` (new), `Cabinet.Service/Limits/LiveConnectPolicy.cs`, `Cabinet.Service/Limits/ClientLimits.cs`, `Cabinet.IntegrationTests/LiveAddressCapTests.cs`
**Commit:** 58d8c2c
**Status:** fixed: requires human verification (logic change)
**Applied fix:** The connect window moved out of the hub's endpoint policy into a global limiter, `LiveConnectWindow`, which counts a request against the per-address window when it is the negotiate request or a `GET` on the live route that carries no connection `id`. The endpoint policy keeps only the concurrency lane for `GET` and no limit for everything else, so Server-Sent Events message posts (which are `POST` with an `id`) are never counted.

Budget meaning, stated plainly: `LiveConnectsPerWindow` is still a number of new connections per address per window. A negotiated connect is counted once, on its negotiate request; the connect request that follows carries the `id` the negotiation issued and is not counted again, so a negotiate-then-connect pair costs one place, as before. A client that skipped negotiation has no `id` and is counted on its connect request instead. A connect with an `id` that was never issued is answered by the hub with no connection opened. The refusal stays quiet: 429, `Retry-After`, `no-store`, no body, no log line. The global limiter replenishes lazily (no timer per address).

Tests added to `LiveAddressCapTests`: a SignalR client with `SkipNegotiation = true` on WebSockets is admitted twice and refused the third time under a window of two, a plain `GET` then shows the 429 headers and an empty body, another address is unaffected, and no Warning-level log line is written; and two negotiated WebSocket pages exhaust a window of two (proving a pair is counted once). With the counting disabled for id-less connects the first new test fails, so it does prove the window refuses a negotiate-less client.

### WR-01: Host filtering is fail-open, never provisioned and never verified on the host

**Files modified:** `deploy/bin/cabinet-selfcheck`, `deploy/tests/sandboxing-test.sh`, `docs/deploy.md`
**Commit:** 3e72adf
**Applied fix:** Added `check_host_filter` to the selfcheck: it sends a request naming a host the site does not serve (a documentation-domain name) to the public port and fails unless the answer is `400`, naming `AllowedHosts` in the env file as the fix. The committed default stays `*`, as designed. Tests cover the pass, a served unlisted host (status 200), no answer, and that the check is wired into `main`; the deploy doc row for `AllowedHosts` now says the selfcheck does this.
**Not done:** The review's optional startup warning in Production when the list is a wildcard, and the same probe in `build/check-public-routes.sh`. The latter cannot work through the reverse proxy, which routes by `Host` and would answer for a foreign host itself, so the host-local selfcheck is the right place.

### WR-02: The selfcheck outbound-block probe cannot tell the firewall's refusal from any fast failure

**Files modified:** `deploy/bin/cabinet-selfcheck`, `deploy/tests/sandboxing-test.sh`, `docs/lxc-setup.md`, `docs/go-public.md`
**Commit:** 9ae75ea
**Status:** fixed: requires human verification (logic change; the counter read has only been exercised against stubbed `nft` output, not a live ruleset)
**Applied fix:** `check_egress` now reads the IPv4 reject rule's packet counter before and after the probe and passes only when it rose. A fast failure from a real host or a missing route no longer passes. The probe address is the network address itself for a single-address entry and the address after it otherwise, so a `/32` first entry is probed inside the set. A rule with no packet counter fails with a message saying the probe cannot be confirmed. The test stub for `nft` now returns a different listing on its second call; tests cover counted, slow-but-counted, fast-but-uncounted, connected-but-uncounted and no-counter cases, plus the probe-address cases. Docs describe the counter as the evidence.

### WR-03: The burst check counts `Retry-After` lines across all responses

**Files modified:** `build/check-public-routes.sh`, `build/tests/check-public-routes-test.sh`
**Commit:** 5541aa6
**Status:** fixed: requires human verification (logic change; the per-answer header split runs against the stub's header dump, not a real curl run)
**Applied fix:** The combined header dump is split per answer (each answer starts at its status line), and each of the app's own cooldown refusals must carry its own `Retry-After`; another refusal's header no longer stands in. Answers that got no HTTP response are skipped when aligning headers with bodies. Two tests added: shared-window refusals with the header plus an app refusal without it now fails, and app refusals that all carry the header pass even when a shared refusal has none.

### WR-04: Rate-limiter partitions are unbounded and each fixed-window partition owns a timer

**Files modified:** `Cabinet.Service/Limits/SyncPressPolicy.cs` (the live window is covered by CR-01's new limiter)
**Commit:** 513a787
**Applied fix:** Every fixed-window limiter now sets `AutoReplenishment = false`, so an idle address holds no timer and the partitioned limiter reclaims its window after it expires. The sync press and connect window tests still pass.
**Not done, needs an owner decision:** The global backstop (a size for a global token bucket on the live-connect lane) and a lower cap than 3600 seconds on the two window settings. Both are numbers the owner should choose from the measured first visit, and changing the cap could reject a value already in a server env file.

### WR-05: The "every upload handler has a size limit" test accepts a method-level attribute that Razor Pages ignores

**Files modified:** `Cabinet.IntegrationTests/KestrelLimitsTests.cs`
**Commit:** 9e10bb3
**Applied fix:** The guard now accepts the size-limit attribute on the declaring page model only, with a reason in the assertion message. The existing upload page already carries it on its class.
**Not done:** Asserting the effective endpoint metadata, and widening the scan to handlers that read the request body without binding a parameter. Both are the review's "better" option rather than the required fix.

### WR-06: The Server-Sent Events ping test asserts a property it never observes and relies on a fixed sleep

**Files modified:** `Cabinet.IntegrationTests/Infrastructure/LiveRefusalWatch.cs`, `Cabinet.IntegrationTests/LiveAddressCapTests.cs`
**Commit:** 036bd64
**Applied fix:** The refusal watch now also counts messages posted to the live route (a `POST` that is not a negotiate request and was not refused). The test waits until at least four have arrived instead of sleeping three seconds, then asserts no live request was refused in the meantime and the connection is still up.

### WR-07: The default blocked IPv6 ranges leave the home network reachable over a global prefix

**Files modified:** `deploy/bin/cabinet-selfcheck`, `deploy/tests/sandboxing-test.sh`, `deploy/provision.conf.example`, `docs/lxc-setup.md`
**Commit:** 28ad9dd
**Status:** fixed: requires human verification (logic change; the host's addresses come from a stub in the tests)
**Applied fix:** Added `check_egress_ipv6_coverage` to the selfcheck, run after the egress check. It reads the IPv6 ranges of the loaded output reject rule and the host's global addresses (`ip -6`) and fails with a count, never an address, when any address lies outside every range. A host with no global address passes. The containment test is a small bash routine that handles compressed addresses and prefix lengths that are not a multiple of sixteen bits, with unit tests. No prefix is hard-coded and the shipped default list is unchanged. The docs now say the block applies to new connections only (already-open connections survive a ruleset load).
**Not done:** Printing the detected prefix at provision time; the selfcheck failure message already names the setting to change.

## Skipped Issues

None. Info findings (IN-01 to IN-08) are out of scope for this run.

## Verification

Run in the isolated worktree the orchestrator provided, not in the main checkout. A worktree run is not reproducible from the main checkout after merge; rerun there if the numbers matter.

- Unit suite: 3010 passed, 0 failed.
- Integration suite: all passed except `TestHostCleanupTests` (the inotify-instance test, which fails under the polling file watcher variable by design of the test run).
- Shell tests run: `deploy/tests/sandboxing-test.sh`, `deploy/tests/render-templates-test.sh`, `deploy/tests/provision-logic-test.sh`, `build/tests/check-public-routes-test.sh`, all passing.
- Lint: `10-repo-rules.sh` and `30-shell.sh` pass; the `40-secrets.sh` working-tree scan finds no leaks. Its full-history scan cannot read a linked worktree's git directory (0 commits scanned), so the history scan was not exercised.
- Browser tests were not run: the only browser-facing change is an additive property on the shared refusal watch, and the solution builds with no warnings.

---

_Fixed: 2026-10-25_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

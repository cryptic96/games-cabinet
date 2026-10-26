---
phase: 08-public-hardening-go-public
fixed_at: 2026-10-26T00:00:00Z
review_path: .planning/phases/08-public-hardening-go-public/08-REVIEW-2.md
iteration: 1
findings_in_scope: 7
fixed: 7
skipped: 0
status: all_fixed
---

# Phase 8: Code Review Fix Report (second review)

**Fixed at:** 2026-10-26
**Source review:** .planning/phases/08-public-hardening-go-public/08-REVIEW-2.md
**Iteration:** 1

**Summary:**
- Findings in scope: 7 (1 critical, 4 warning, 2 info; the info findings were included by instruction)
- Fixed: 7
- Skipped: 0

The work ran directly in the agent worktree the harness had already created (branch in the `worktree-agent-*` namespace, starting at 595dc52), so no second worktree was made. Each finding is one commit; one small extra test commit follows the first finding (see below). Where a fix is a logic change, it is marked for human verification.

## Fixed Issues

### CR-01: A trailing slash on the negotiate path bypasses both the per-address and the site-wide connect window

**Files modified:** `Cabinet.Service/Limits/LiveConnectWindow.cs`, `Cabinet.IntegrationTests/Infrastructure/LiveRefusalWatch.cs`, `Cabinet.IntegrationTests/LiveAddressCapTests.cs`, `Cabinet.UnitTests/Limits/LiveConnectWindowTests.cs`
**Commit:** 6a3331d (plus 90443fa for a test spelling change)
**Status:** fixed: requires human verification (logic change on a live security control)
**Applied fix:** `CountsAsNewConnection` now trims trailing slashes from the path and compares the negotiate suffix case-insensitively, then counts any other request on the live route that carries no connection `id`, whatever its method. A request that carries an `id` (connect after negotiate, SSE message posts, close) is still not counted. `LiveConnectPolicy` has no suffix test (it only limits concurrent GETs), so it needed no change. The `LiveRefusalWatch` test helper no longer uses a negotiate suffix at all: a message is a POST that carries an `id`.
**Tests (written first, seen failing):** a unit theory over 23 method/path/query combinations (slash, double slash, case, OPTIONS/PUT/DELETE without id, id in either letter case, off-route paths), integration theories that the trailing-slash, upper-case, mixed-case and double-slash forms are refused at the third request per address and at the third request site-wide, and that pages are unaffected by a spent live window. Before the fix the trailing-slash rows failed, as the review measured (9 unit rows and 4 integration rows red).
**Extra commit:** 90443fa spells the double-slash paths as a constant, because the repository's comment rule reads a literal double slash inside a string as a comment.

### WR-02: The site-wide limiter is asked after the per-address one, so a site-wide refusal still spends the address's own window

**Files modified:** `Cabinet.Service/Limits/LiveConnectWindow.cs`, `Cabinet.Service/Limits/LiveConnectWindowLimiter.cs` (new), `Cabinet.UnitTests/Limits/LiveConnectWindowTests.cs`
**Commit:** dbc07f1
**Status:** fixed: requires human verification (new limiter replaces the chained fixed-window limiters)
**Applied fix:** Chained fixed-window limiters cannot satisfy both orderings. A fixed window never refunds a permit, so site-first would let an address that is already refused spend the site's budget (the property the existing test pins), and address-first spends the visitor's permit when the site refuses. A single limiter now checks both windows and spends both under one lock, so a refusal by either spends nothing from the other. It measures the windows on a `TimeProvider` (the system clock in production), holds no timer, and drops an address's window once it has expired; entries exist only for admitted requests, so the table is bounded by the site budget per window. The refusal carries a `RetryAfter` of the wait until the window that refused it resets (the longer, when both refused), which the existing 429 handler already reads. `CreateLimiter` takes an optional clock for tests; its callers are unchanged.
**Tests (written first, seen failing):** with a fake clock, an address refused twice by the site keeps its own budget after the site window resets; an address refused by its own window spends nothing of the site's budget; the refusal names the right wait; non-connection requests spend nothing; an expired window admits a full budget again. No sleeping.

### WR-01: The stalled-page recovery can silently convert an intermittent real failure into a pass

**Files modified:** `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs`, `Cabinet.BrowserTests/StalledPageTests.cs`
**Commit:** 42f48ce
**Status:** fixed: requires human verification (test infrastructure logic)
**Applied fix:** After a timeout, the page is asked to evaluate a trivial script within 3 seconds. Only a page that does not answer (or whose call fails because it is closed or crashed) is replaced and the work retried on a fresh page; a page that answers ends the test with the original timeout, and the last attempt still throws. Every replacement is written to the test output and the console as well as to `stalled-pages.txt`, which is still only written when `CABINET_SCREENSHOT_DIR` is set.
**Tests (written first, seen failing):** a Playwright wait that times out on a responsive page runs once and throws the timeout; a run that outlasts its budget on a responsive page runs once; a failed assertion inside the work runs once and is not a timeout. The two existing recovery tests (a stuck page is replaced, a page stuck on every attempt ends with the timeout) still pass.

### WR-03: The egress packet-counter evidence can be satisfied by unrelated traffic, and the two reads are not tied to the probe

**Files modified:** `deploy/bin/cabinet-selfcheck`, `deploy/tests/sandboxing-test.sh`
**Commit:** 5292387
**Status:** fixed: requires human verification (not exercised against a real nftables host)
**Applied fix:** The check now fails when the probe connected, whatever the counter says; fails when the probe did not fail at once (a timeout cannot show the reject rule answered it); reads the table again immediately before the probe and immediately after it, with nothing in between; and fails when the counter rose by more than three packets for one probe, or not at all. It still prints no address. The nft stub in the test switches to the "after" listing from the third call, because the check now reads the table three times.
**Tests (written first, seen failing):** timeout with a rising counter now fails; connected with a rising counter fails and says the connection was opened; refused with an unchanged counter fails; refused with a counter that rose by 40 fails and says other traffic reaches the blocked ranges, with no address printed.

### WR-04: The origin test for admitted requests asserts only "not 403"

**Files modified:** `Cabinet.IntegrationTests/LiveOriginTests.cs`
**Commit:** e896565
**Applied fix:** Negotiate rows require 200 when admitted and 403 when refused. WebSocket rows that are admitted connect to a path with no connection `id` (the id-less path is a real upgrade, where the earlier `?id=unknown` path would give the hub's 404) and require an open socket with a 101; refused rows require the `WebSocketException` and a 403. The empty catch is gone, so a transport failure fails the test. Not run against a mutation; the stronger assertions pass against the current host (27 origin tests).

### IN-01: The selfcheck reports "no global IPv6 address" when the `ip` command itself fails

**Files modified:** `deploy/bin/cabinet-selfcheck`, `deploy/tests/sandboxing-test.sh`
**Commit:** 9d8f69a
**Applied fix:** `host_global_ipv6_addresses` now fails when `ip` fails (a missing binary included) and prints nothing; a clean run with no output still means no global address. `check_egress_ipv6_coverage` fails with "could not read the global IPv6 addresses of this host" instead of passing. The test adds an `ip` stub and covers the real function (addresses listed without prefix length, clean empty run, failing `ip`) and the check's failure message.

### IN-02: The runtime heap hard limit has no margin note, and the site-wide trade-off is not written down

**Files modified:** `docs/lxc-setup.md`, `docs/bgg-sync.md`
**Commit:** 8212934
**Applied fix:** `docs/lxc-setup.md` now explains the margins: the 314 MiB measured peak is the whole process, the 384 MiB heap limit sits at least 70 MiB above it, a worst-case decode brings the process to about 458 MiB against the 480 MiB reclaim point and 600 MiB ceiling, and a heap limit turns an overshoot into an out-of-memory error in the run that hit it. `docs/bgg-sync.md` now says how new connections are counted (by what the request asks for, not by its path spelling), that a refusal by either window spends nothing of the other, and that about ten addresses at their full allowance can spend the whole site budget for a window. The documents carry no planning references.
**Not done:** the review also suggested logging the heap limit in the host limits report and having the selfcheck compare it with the unit. The instruction for this finding was a documentation note, so no code was added for that.

## Skipped Issues

None.

## Verification

All gates ran in the isolated agent worktree (the fix branch), not in the main checkout, with `DOTNET_USE_POLLING_FILE_WATCHER=1` because another full run was going on in the main checkout. The numbers are reproducible from the branch the orchestrator merges, not from the main checkout before the merge.

| Gate | Result |
|---|---|
| Unit suite (`Cabinet.UnitTests`) | 3067 passed, 0 failed |
| Integration suite (`Cabinet.IntegrationTests`, `TestHostCleanupTests` excluded) | 629 passed, 0 failed |
| `deploy/tests/sandboxing-test.sh` | all checks passed |
| `StalledPageTests` (browser), normal fonts | 5 passed |
| `StalledPageTests` (browser), DejaVu fontconfig | 5 passed |
| `LookContainmentTests` (browser), normal fonts | 65 passed |
| `LookContainmentTests` (browser), DejaVu fontconfig | 65 passed |
| `build/lint/checks/10-repo-rules.sh` | passed |
| `build/lint/checks/30-shell.sh` | passed (shellcheck run in a container; see below) |

**Not verified:**
- The egress check against a real nftables ruleset and a real blocked-range probe; only the stubbed paths ran. In particular the three-packet ceiling for one probe is a judgement, not a measurement.
- The new connect-window limiter under real concurrency and with a real clock beyond the integration tests; the unit tests use a fake clock and a single thread.
- The other browser test classes and the owner-facing suites were not run here.
- `TestHostCleanupTests` were excluded as instructed (they do not suit the polling file watcher variable).

---

_Fixed: 2026-10-26_
_Fixer: Claude (gsd-code-fixer)_
_Iteration: 1_

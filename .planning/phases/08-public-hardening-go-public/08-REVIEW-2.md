---
phase: 08-public-hardening-go-public
reviewed: 2026-10-26T00:00:00Z
depth: standard
files_reviewed: 23
files_reviewed_list:
  - Cabinet.Service/Limits/ClientLimits.cs
  - Cabinet.Service/Limits/LiveConnectPolicy.cs
  - Cabinet.Service/Limits/LiveConnectWindow.cs
  - Cabinet.Service/Limits/LiveOriginGuard.cs
  - Cabinet.Service/Limits/SyncPressPolicy.cs
  - Cabinet.Service/appsettings.json
  - deploy/systemd/cabinet.service
  - deploy/bin/cabinet-selfcheck
  - deploy/traefik/cabinet.yml.example
  - deploy/provision.conf.example
  - build/check-public-routes.sh
  - Cabinet.UnitTests/Limits/ClientLimitSettingsTests.cs
  - Cabinet.UnitTests/Limits/LiveOriginGuardTests.cs
  - Cabinet.IntegrationTests/LiveAddressCapTests.cs
  - Cabinet.IntegrationTests/LiveOriginTests.cs
  - Cabinet.IntegrationTests/KestrelLimitsTests.cs
  - Cabinet.IntegrationTests/Infrastructure/LiveRefusalWatch.cs
  - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
  - Cabinet.BrowserTests/LookContainmentTests.cs
  - Cabinet.BrowserTests/StalledPageTests.cs
  - deploy/tests/sandboxing-test.sh
  - deploy/tests/render-templates-test.sh
  - build/tests/check-public-routes-test.sh
findings:
  critical: 1
  warning: 4
  info: 2
  total: 7
status: issues_found
---

# Phase 8: Code Review Report (second pass)

**Reviewed:** 2026-10-26
**Depth:** standard (diff 86ba964..HEAD, plus empirical probes of the live-connect window against a scratch copy of HEAD)
**Files Reviewed:** 23
**Status:** issues_found

## Summary

The second-pass fixes mostly hold up. I checked these and found them sound:
- The origin guard now compares the scheme as well as host and port. The `wss` and `ws` schemes count as their HTTP counterparts, and a scheme downgrade is refused.
- `AutoReplenishment = false` still replenishes. The partitioned limiter's own timer does it, which I confirmed by letting a one-second window expire (200, 429, wait, 200).
- The per-answer `Retry-After` split in the burst check lines up with the answers.
- The IPv6 range arithmetic is correct, including prefixes that are not a multiple of 16 bits.

One real bypass remains in the very control this round was meant to close: the connect window can be skipped with a trailing slash on the negotiate path. I reproduced it. The other findings are weaker checks and tests that could pass while behaviour is broken.

## Critical Issues

### CR-01: A trailing slash on the negotiate path bypasses both the per-address and the site-wide connect window

**File:** `Cabinet.Service/Limits/LiveConnectWindow.cs:28-41`
**Issue:** `CountsAsNewConnection` identifies a negotiate request with `Path.Value.EndsWith("/negotiate")`. For every other request it counts only a `GET` without an `id`. Endpoint routing also matches `POST /cabinet/live/negotiate/` (trailing slash). That request:
- does not end with `/negotiate`, so the first branch is false;
- is a `POST`, so the `GET` branch is false;
- is therefore classified as "not counted" and is exempt from both windows.

Measured against HEAD in the integration harness, with a per-address window of 2 and POSTs to `/cabinet/live/negotiate?negotiateVersion=1`:

| Path | Answers |
|---|---|
| `/cabinet/live/negotiate` (normal) | 200, 200, 429, 429, ... |
| `/cabinet/live/negotiate/` | 200, 200, 200, 200, 200, 200 |

Each 200 allocates a pending hub connection record. The 600 per 60 s site-wide backstop and the per-address window therefore do not apply to this variant. The only remaining limit is the proxy's request rate (60/s average, burst 800), which is far above the intended 60 per minute. The first-round fix closed the negotiate-less `GET`. The negotiate path is still matched by a string suffix that routing does not respect. The test infrastructure (`LiveRefusalWatch`) and the old policy share the same suffix test, and no test covers the slash form.

**Fix:** Do not decide by the exact path text. Normalise the trailing slash, and count by what a request does rather than by method:
```csharp
var path = (request.Path.Value ?? string.Empty).TrimEnd('/');
if (path.EndsWith(NegotiateSuffix, StringComparison.OrdinalIgnoreCase))
{
    return true;
}

return string.IsNullOrEmpty(request.Query[ConnectionIdKey].ToString());
```
The second branch counts any live-route request that carries no connection `id`. That covers a `POST` or `DELETE` without one, which the hub rejects without opening anything. A `POST` or `GET` that does carry an `id` stays uncounted, as intended. Add a theory in `LiveAddressCapTests` that posts negotiate to `/negotiate/`, `/Negotiate` and `//negotiate` under a window of 2 and asserts the third answer is 429. Apply the same normalisation to `LiveRefusalWatch.NegotiateSuffix`.

## Warnings

### WR-01: The stalled-page recovery can silently convert an intermittent real failure into a pass

**File:** `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs:121-143` (used from `Cabinet.BrowserTests/LookContainmentTests.cs:104-108`)
**Issue:** Playwright reports its own wait expiry (`WaitForSelector`, `Locator.WaitFor`, screenshot timeout) as `System.TimeoutException`. This is the same type the recovery catches, so every wait timeout is treated as "the renderer stalled". That includes a cabinet that never draws because of a real, intermittent defect (a race in first draw, a missing selector). The view is retried on a fresh page up to three times and the test then passes. The only trace is `stalled-pages.txt`, and that is written only when `CABINET_SCREENSHOT_DIR` is set. Nothing reads or gates on that file. The doc comment says only a timeout retries, but no test pins the other half: that a failed assertion inside the work is not retried. A stall that keeps recurring would go unnoticed.

**Fix:**
- Make a recovered stall visible to the run, not just to a file. For example, write a warning with the test's output helper, or have the CI step that collects screenshots fail or annotate when `stalled-pages.txt` is non-empty.
- Narrow what counts as a stall. Treat only the outer `WaitAsync(budget)` timeout as a stall, and let a Playwright wait timeout end the test. Alternatively, retry only when a liveness probe on the page also times out, such as `page.EvaluateAsync("1")` with a 2 s budget.
- Add a test that an assertion failure thrown inside the work runs once and propagates.

### WR-02: The site-wide limiter is asked after the per-address one, so a site-wide refusal still spends the address's own window

**File:** `Cabinet.Service/Limits/LiveConnectWindow.cs:50`
**Issue:** `PartitionedRateLimiter.CreateChained(perAddress, siteWide)` acquires the per-address permit first. When the site window then refuses, the per-address permit is already spent, and a disposed fixed-window lease does not refund it. While the site budget is exhausted, for example during a flood from other addresses, a legitimate visitor's automatic reconnect attempts each burn one of that visitor's 60 per-window permits. When the site window resets, the visitor may already be refused by their own window. The documented property, "an address that is already refused spends nothing of the site's budget", covers only the opposite order. No test covers the case where the site refuses first and the address window is then checked.

**Fix:** Either accept this explicitly and document it, or order the check so a refusal costs nothing. Check the site window with `GetAvailablePermits` before the per-address acquire, or use one custom limiter that acquires the site permit first and releases it by a token bucket refund on per-address refusal. At minimum add a test: site window of 1, per-address window of 2, one address refused by the site twice, then the site resets, and the address should still have its permits.

### WR-03: The egress packet-counter evidence can be satisfied by unrelated traffic, and the two reads are not tied to the probe

**File:** `deploy/bin/cabinet-selfcheck:480-495`
**Issue:** The check passes when the reject rule's packet counter rose between two `nft list` reads. The counter is shared by every packet that hits the rule, so any other outbound attempt to a blocked range makes the check pass even if the probe itself was not rejected by that rule. Examples are the application's own refused attempts and a stray scan of a home-network address. In that case the probe could have connected through some other path and the check would still pass on the `after > before` comparison alone. The `connected` outcome is also ignored once the counter rose. The risk is small on a quiet host, but this check is the one that is supposed to catch a broken outbound block.

**Fix:**
- Fail when the probe connected, whatever the counter says: `[[ "$outcome" == connected ]] && fail ...`.
- Prefer a more specific counter. Give the probe address its own counter rule, or compare `after - before` with the number of packets the probe is expected to send (a SYN, possibly retried) and fail when the difference is much larger.
- Optionally repeat the probe once and require the counter to rise by at least the same amount.

### WR-04: The origin test for admitted requests asserts only "not 403", so it passes when the request fails for any other reason

**File:** `Cabinet.IntegrationTests/LiveOriginTests.cs:145-196`
**Issue:** Both new theories assert `status != Forbidden` for the admitted rows. In the WebSocket test, a connection that fails before any HTTP answer leaves `HttpStatusCode` at its default (0), and `WebSocketException` is swallowed by an empty catch, so the row passes. A 400 from host filtering, a 429 from a window, a 404 or a 500 pass in the same way. A regression that turns every live request into a 400 or 500 would leave all the "admitted" rows green. The refused rows (403) are checked properly.

**Fix:**
- For the negotiate theory, assert `HttpStatusCode.OK` for admitted rows.
- For the WebSocket theory, assert that the upgrade succeeded (`socket.State == WebSocketState.Open`) or that the status is 101. Drop the empty catch, or assert on its exception.

## Info

### IN-01: The selfcheck reports "no global IPv6 address" when the `ip` command itself fails

**File:** `deploy/bin/cabinet-selfcheck:549` (`host_global_ipv6_addresses`), `check_egress_ipv6_coverage` at line 570
**Issue:** `ip -6 -o addr show scope global 2>/dev/null | awk ...` hides every failure: a missing binary, a restricted PATH, or a kernel error. The output is then empty, and `check_egress_ipv6_coverage` passes with "this host has no global IPv6 address". That is a check that passes while broken. Today `ip` is next to `nft` on the same PATH, so this is a robustness gap rather than a live defect.
**Fix:** Capture the command's exit status and fail with a message when it is non-zero. Treat only a clean run with empty output as "no global address".

### IN-02: The runtime heap hard limit has no on-host proof that it was applied, and no margin check against the measured worst case

**File:** `deploy/systemd/cabinet.service:15,20-23` and `deploy/tests/sandboxing-test.sh`
**Issue:** The unit sets `DOTNET_GCHeapHardLimit=0x18000000` (384 MiB). The measured peak is about 314 MiB for the whole process, and one worst-case picture decode adds about 144 MiB. The tests check only that the unit text contains the line and that 384 is less than `MemoryHigh`. A managed-heap hard limit turns an overshoot into an `OutOfMemoryException` inside the sync or picture rebuild, not a gradual slow-down, and nothing verifies on the host that the runtime took the value (for example by logging `GCHeapHardLimit` in the host limits report, which already prints the runtime's memory view). Also, the whole-process figure of 314 MiB includes unmanaged memory, so the heap's own margin is not shown anywhere. The same site-wide design that protects the host also lets a flood from about ten addresses use the whole 600 per 60 s live budget, which turns live updates off for everyone for that window. This is an accepted trade-off, but it is not written down next to the setting.
**Fix:** Log `GCMemoryInfo.TotalAvailableMemoryBytes` or the configured hard limit in the host limits report and have the selfcheck compare it with the unit. Record the heap's measured peak next to the limit. Note the site-wide trade-off in the deploy doc.

---

_Reviewed: 2026-10-26_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

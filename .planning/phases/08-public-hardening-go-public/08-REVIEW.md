---
phase: 08-public-hardening-go-public
reviewed: 2026-10-25T00:00:00Z
depth: standard
files_reviewed: 46
files_reviewed_list:
  - Cabinet.Service/Limits/ClientAddress.cs
  - Cabinet.Service/Limits/ClientLimits.cs
  - Cabinet.Service/Limits/LiveConnectPolicy.cs
  - Cabinet.Service/Limits/LiveOriginGuard.cs
  - Cabinet.Service/Limits/SyncPressPolicy.cs
  - Cabinet.Service/Hosting/HostLimits.cs
  - Cabinet.Service/Hosting/SecurityHeaders.cs
  - Cabinet.Service/Live/LiveEndpoints.cs
  - Cabinet.Service/Program.cs
  - Cabinet.Service/Sync/SyncEndpoints.cs
  - Cabinet.Service/appsettings.json
  - deploy/bin/cabinet-selfcheck
  - deploy/nftables/cabinet.nft.in
  - deploy/provision.sh
  - deploy/provision.d/50-firewall.sh
  - deploy/provision.conf.example
  - deploy/cabinet.env.example
  - deploy/systemd/cabinet.service
  - deploy/traefik/cabinet.yml.example
  - build/check-public-routes.sh
  - build/scan-history.sh
  - Cabinet.UnitTests/Hosting/HostLimitSettingsTests.cs
  - Cabinet.UnitTests/Limits/ClientAddressTests.cs
  - Cabinet.UnitTests/Limits/ClientLimitSettingsTests.cs
  - Cabinet.IntegrationTests/HostFilteringTests.cs
  - Cabinet.IntegrationTests/KestrelLimitsTests.cs
  - Cabinet.IntegrationTests/LiveAddressCapTests.cs
  - Cabinet.IntegrationTests/LiveOriginTests.cs
  - Cabinet.IntegrationTests/SecurityHeadersTests.cs
  - Cabinet.IntegrationTests/SyncRateLimitTests.cs
  - Cabinet.IntegrationTests/Infrastructure/CapturingLoggerProvider.cs
  - Cabinet.IntegrationTests/Infrastructure/LiveRefusalWatch.cs
  - Cabinet.IntegrationTests/Infrastructure/UntrustedPeerHost.cs
  - Cabinet.BrowserTests/Infrastructure/LiveHearing.cs
  - Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs
  - Cabinet.BrowserTests/LiveRefusalTests.cs
  - Cabinet.BrowserTests/HeaderConsoleTests.cs
  - Cabinet.BrowserTests/CreditVisibilityTests.cs
  - Cabinet.BrowserTests/CardSyncTests.cs
  - Cabinet.BrowserTests/LiveLookTests.cs
  - build/tests/check-public-routes-test.sh
  - build/tests/scan-history-test.sh
  - deploy/tests/render-templates-test.sh
  - deploy/tests/provision-logic-test.sh
  - deploy/tests/sandboxing-test.sh
findings:
  critical: 1
  warning: 7
  info: 8
  total: 16
status: issues_found
---

# Phase 8: Code Review Report

**Reviewed:** 2026-10-25
**Depth:** standard
**Files Reviewed:** 46
**Status:** issues_found

## Summary

The partition key (`ClientAddress`) is sound. It reads only `Connection.RemoteIpAddress`, which the forwarded-headers middleware rewrites only for the configured known proxy. The spoofing test uses a real untrusted peer, so it proves what it claims. The Origin guard fails closed on a missing port, a duplicate header and an opaque origin, and runs before the rate limiter. The security-header set is assigned rather than appended, and tests check each header is sent exactly once. The firewall template is ordered correctly (loopback, replies, ICMPv6, resolver exemption, then the two rejects), and every substituted value is shape-validated before it reaches nft.

Reading the code against the claims turned up one limiter bypass, which I reproduced empirically, and several places where a check or test can pass while the behaviour it vouches for is broken:

- **CR-01:** the per-address connect-window limit is bypassed by any WebSocket client that skips negotiation. The SignalR client supports this directly.
- The host-filtering protection is fail-open and nothing provisions or verifies it.
- The selfcheck outbound-block probe, the burst script's `Retry-After` check and the upload-limit guard test can each go green on a broken system.
- The new rate limiters can hold an unbounded number of partitions.

Project-rule scan of the changed non-planning files found no `//` comments, no planning references and no personal data.

## Critical Issues

### CR-01: The per-address connect window is bypassed by connecting without a negotiate request

**File:** `Cabinet.Service/Limits/LiveConnectPolicy.cs:34-57` (also `Cabinet.IntegrationTests/LiveAddressCapTests.cs:68-86`)
**Issue:** `LiveConnectPolicy` counts new connections in a fixed window only on the `/negotiate` request. The WebSocket upgrade and SSE `GET` fall into the concurrency lane, which bounds simultaneous connections but not the rate of new ones. SignalR accepts a WebSocket upgrade on the hub route with no negotiate request and no `id` (the client option `skipNegotiation: true`, valid when WebSockets are the only transport tried). I verified this with a minimal SignalR host that maps the hub with `WebSockets | ServerSentEvents`. Three `ClientWebSocket.ConnectAsync("ws://127.0.0.1:5399/live")` calls with no negotiate all returned 101 and `Open`.

A client can therefore connect, close and reconnect at any rate. The `LiveConnectsPerWindow` budget (default 60 per minute) is never consulted, and it never charges those connections to the address. Only the concurrency cap (20) and the proxy's per-address rate limit remain. The class summary says "reconnect churn is slowed", and the test says new connections "are counted per window on the negotiate request only". The test therefore documents the gap instead of closing it, and no test drives a negotiate-less upgrade.

**Fix:** Count the connect request too. `AddPolicy` allows one partition per request, so the simplest option is to register a chained limiter that charges the window on both the negotiate request and the connect `GET`:
```csharp
limiter.GlobalLimiter = PartitionedRateLimiter.CreateChained(
    PartitionedRateLimiter.Create<HttpContext, string>(context =>
        IsLiveConnectOrNegotiate(context)
            ? RateLimitPartition.GetFixedWindowLimiter($"connect|{ClientAddress.PartitionKey(context)}", _ => windowOptions)
            : RateLimitPartition.GetNoLimiter("other")));
```
Keep the concurrency lane as it is. Apply the window to every `GET` whose path is the hub route (WebSocket or SSE) as well as `/negotiate`, so a negotiated connect is counted once or twice consistently. Document the chosen count. Add a test that opens N negotiate-less `ClientWebSocket` connections under `LiveConnectsPerWindow=2` and expects the third to get 429.

## Warnings

### WR-01: Host filtering is fail-open, never provisioned and never verified on the host

**File:** `Cabinet.Service/appsettings.json:2`, `deploy/provision.d/20-accounts.sh:17-24`, `deploy/bin/cabinet-selfcheck:240-270`
**Issue:** The committed default is `"AllowedHosts": "*"`. The env file the provisioner writes contains only `ASPNETCORE_ENVIRONMENT` and `ReverseProxy__KnownProxies__0`, and the provisioner never rewrites an existing env file. A host provisioned before this phase, or a new one where the owner forgets the manual `AllowedHosts` line, runs with no host filtering and gives no sign of it. The Origin guard is Host-relative, so its protection against DNS-rebinding style pages depends on that filter. Neither `cabinet-selfcheck` nor `check-public-routes.sh` sends a request with a foreign `Host` and expects 400. The docs say loopback must stay in the list, but nothing checks that the list exists.
**Fix:** Add a selfcheck probe next to `check_owner_wall`:
```bash
status="$(http_status "http://127.0.0.1:5080/" -H "Host: foreign.invalid")"
[[ "$status" == "400" ]] && pass "the app refuses a host that is not listed" \
  || fail "the app answers status ${status} to an unlisted host; set AllowedHosts in the env file"
```
Also consider failing startup, or logging a Warning, in Production when `AllowedHosts` is `*`.

### WR-02: The selfcheck outbound-block probe cannot tell the firewall's refusal from any fast failure, and probes outside a /32

**File:** `deploy/bin/cabinet-selfcheck:365-380, 391-402` (pinned by `deploy/tests/sandboxing-test.sh`, "the real probe reports a failed connection as refused when it fails at once")
**Issue:** `egress_probe` reports `refused` for any connect failure that takes under 2 seconds. A real LAN host answering port 80 with a RST, a "network unreachable" from a missing route, or a transient error all read as `refused`, so the check passes even if the reject rule is not matching. The test stubs `timeout` to return 1 immediately and asserts `refused`, which enshrines the weakness. `first_blocked_host` adds one to the network address. For a first blocked entry that is a `/32` (or the last address of a `/31`) the probe address is outside the blocked set, so a correctly configured host fails the check. The rule already carries a `counter`, which is a more reliable signal.
**Fix:** Read the rule's packet counter before and after the probe and require it to increase:
```bash
before="$(nft list chain inet cabinet_filter output | grep -E 'ip daddr \{[^}]+\}.*reject' | sed -n 's/.*packets \([0-9]*\).*/\1/p' | head -n1)"
```
Run the probe, read the counter again, and pass only if `after > before`. Derive the probe address only from a network wider than a `/31`, or skip a first entry that is a `/32`.

### WR-03: The burst check counts `Retry-After` lines across all responses, so unrelated 429s can satisfy it

**File:** `build/check-public-routes.sh:318-325`
**Issue:** `retry_after` is the number of `Retry-After` headers in the combined header dump of every press. The pass condition is `cooldown > 0 && retry_after >= cooldown`. In a real run, presses made before the per-address limit trips are refused by the shared cooldown with a status object, and those 429s also carry `Retry-After`. Their headers can make the count reach the number of limiter refusals even when the limiter's own refusals omit the header. The stubbed test never mixes the two kinds, so it cannot catch this.
**Fix:** Capture headers per response (a separate `-D` file for each URL, as is already done for `-o`). Pass only if every body classified `cooldown` has its own `Retry-After`.

### WR-04: Rate-limiter partitions are unbounded and each fixed-window partition owns a timer

**File:** `Cabinet.Service/Limits/SyncPressPolicy.cs:26-33`, `Cabinet.Service/Limits/LiveConnectPolicy.cs:36-54`, `Cabinet.Service/Limits/ClientLimits.cs:47-53`
**Issue:** Keys are per client address (per /64 for IPv6), and a partition lives for its window (up to 3600 s by configuration, 60 s by default). A caller that rotates source addresses, such as an IPv6 range owner stepping through /64s or a botnet, creates one partition per request. Each `FixedWindowRateLimiter` is built with the default `AutoReplenishment = true`, so each partition allocates its own timer. State is bounded only by request rate times window, and the only outer bound is the proxy's rate limit, which is itself keyed per /64. On the 600M-capped host this is a memory-pressure path, and it is a limiter-state leak by the review brief's definition.
**Fix:** Set `AutoReplenishment = false` on every fixed-window option (the partitioned limiter then replenishes lazily and reclaims idle partitions with no per-partition timer). Add a cheap global backstop with `PartitionedRateLimiter.CreateChained`, for example a global token bucket for the live-connect lane. Cap `LiveWindowSeconds` and `SyncWindowSeconds` lower than 3600, or document the memory cost of large values.

### WR-05: The "every upload handler has a size limit" test accepts a method-level attribute that Razor Pages ignores

**File:** `Cabinet.IntegrationTests/KestrelLimitsTests.cs:175-184`
**Issue:** The assertion passes when `RequestSizeLimitAttribute` is on the handler method or on the declaring type. Filter attributes on Razor Pages handler methods are not honoured; they apply only on the page model class or through a convention. A future handler with a method-level `[RequestSizeLimit]` would satisfy the test while Kestrel's 64 KB default still applied (or, with a larger intended limit, the upload would still be refused). That is a green test with broken behaviour. The scan also finds only handlers that bind `IFormFile` or `Stream`, so a handler reading `Request.Form.Files` or `Request.Body` is invisible to it.
**Fix:** Require the attribute on the declaring type only:
```csharp
handler => handler.DeclaringType!.IsDefined(typeof(RequestSizeLimitAttribute), inherit: true)
```
Better, also assert the effective endpoint metadata (`IRequestSizeLimitMetadata`) from the endpoint data source for each page with an upload handler.

### WR-06: The Server-Sent Events ping test asserts a property it never observes and relies on a fixed sleep

**File:** `Cabinet.IntegrationTests/LiveAddressCapTests.cs:88-102`
**Issue:** The test claims the page "outlives several pings under a window of one connect". It sleeps 3 real seconds, presses sync, waits for two received messages, then asserts the connection is still `Connected`. Nothing observes the number of ping POSTs the server received. On a slow or paused runner few or no pings may have occurred, and the test would still pass, which defeats the regression it guards (messages being counted as connects). The fixed `Task.Delay` is also the timing assumption the brief asks to avoid.
**Fix:** Count the POSTs that reach `/cabinet/live` (a small middleware or the existing `LiveRefusalWatch` pattern) and wait until that counter reaches a threshold such as 4, instead of sleeping. Then assert the connection is still up and no 429 was observed on the live route.

### WR-07: The default blocked IPv6 ranges leave the home network reachable over a global prefix

**File:** `deploy/provision.conf.example:26-30`, `deploy/nftables/cabinet.nft.in:50-51`, `deploy/bin/cabinet-selfcheck:405-430`
**Issue:** The shipped IPv6 list is only `fc00::/7,fe80::/10`. The "append your global prefix" step is a comment, and the validator accepts the default as complete. On a dual-stack home network, which is common, LAN hosts are reachable on the ISP-delegated prefix and the new outbound block does not cover them. The selfcheck only proves that some `ip6 daddr` reject rule exists (`check_egress`), not that the host's own global prefix is covered. Existing connections also survive the ruleset load, because `ct state established,related accept` precedes the rejects and `flush ruleset` does not clear conntrack. That is acceptable at provision time but worth stating.
**Fix:** Have the selfcheck compare the host's global IPv6 prefixes (`ip -6 -o addr show scope global`) with the loaded `ip6 daddr` set. Fail or warn when a global prefix is not covered, and make `provision.sh` print the detected prefix at provision time.

## Info

### IN-01: The "send" lane of the live policy is entirely unmetered

**File:** `Cabinet.Service/Limits/LiveConnectPolicy.cs:57`
**Issue:** Any non-negotiate, non-GET request on `/cabinet/live` (including an anonymous `POST` with no valid `id`) gets `GetNoLimiter("send")`. It is cheap (the hub declares no callable method, bodies are capped at 64 KB, and the request is rejected quickly), so the exposure is small, but it is a deliberate hole.
**Fix:** Consider a generous per-address window for this lane so a flood from one address is bounded in the app as well as at the proxy.

### IN-02: A refused live connection reports the whole window as `Retry-After`

**File:** `Cabinet.Service/Limits/LiveConnectPolicy.cs:60-72`
**Issue:** The concurrency limiter supplies no `RetryAfter` metadata, so the fallback is the 60-second window. A refusal for "20 connections already open" tells the client to wait a minute even though a place may free in a second.
**Fix:** Use a short fixed value (for example 5 s) for the concurrency lane, or omit the header there.

### IN-03: No signal when no known proxy is configured

**File:** `Cabinet.Service/Hosting/ForwardedHeadersSetup.cs:24-36` (with `Cabinet.Service/appsettings.json:79-81`)
**Issue:** An empty `KnownProxies` is silently accepted. In Production behind Traefik, every visitor then shares the proxy's address as the partition key, so one noisy client exhausts the sync budget (5 per minute) and the live budget (20 connections) for the whole site. `https` is also not applied, so the Origin guard compares `https://host` with port 80 and refuses every live connection with 403. The provisioner writes the value, so this only bites hand-edited env files.
**Fix:** Log a Warning at startup, or fail, when `ASPNETCORE_ENVIRONMENT=Production` and `KnownProxies` is empty. The selfcheck could also send one request with `X-Forwarded-For` and compare behaviour.

### IN-04: The sync press endpoint has no origin check

**File:** `Cabinet.Service/Sync/SyncEndpoints.cs:158`
**Issue:** The Origin guard covers only the live route. A third-party page can make a visitor's browser `POST /cabinet/sync`, spending that visitor's per-address budget and, once per cooldown, starting a sync. The global cooldown caps the load on BGG, so the impact is small.
**Fix:** Apply the same Origin check (or `Sec-Fetch-Site`) to `POST /cabinet/sync`, run before the limiter so a foreign page spends no budget.

### IN-05: The owner router lists its allow-list before the security headers

**File:** `deploy/traefik/cabinet.yml.example:117-125` (pinned by `deploy/tests/render-templates-test.sh`)
**Issue:** The public router puts the headers middleware first so the proxy's own refusals carry HSTS, and the comment says why. The owner router puts `cabinet-lan-only` first, so its 403 for an outside caller has no HSTS header. This is inconsistent, and the test pins the inconsistency.
**Fix:** Put `cabinet-security-headers` first on the owner router too and update the test.

### IN-06: A test pins the missing security headers on a host-filtering refusal

**File:** `Cabinet.IntegrationTests/SecurityHeadersTests.cs:519-532`
**Issue:** The 400 from host filtering is produced before `UseSecurityHeaders`, and the test asserts the CSP is absent. The class comment explains the reasoning. A later change that moves the middleware to cover that response would fail this test.
**Fix:** Assert only that the body is empty, or drop the negative assertion.

### IN-07: The "closed ports" check probes the proxy, not the cabinet host

**File:** `build/check-public-routes.sh:214-223`
**Issue:** The public hostname resolves to the reverse proxy, so ports 5080-5082 are checked on the proxy's address. A pass says nothing about the cabinet host's own firewall (that is the selfcheck's job on the host).
**Fix:** Say so in the check's label and header comment.

### IN-08: The "a quiet refusal writes no log line" claim is tested only at Warning and above

**File:** `Cabinet.IntegrationTests/LiveAddressCapTests.cs:136` and the class comments of `SyncPressPolicy` and `LiveConnectPolicy`
**Issue:** The test checks only `CapturingLoggerProvider.Problems` (Warning or above). The policies' comments say a refusal writes nothing to the log. The rate limiter's own rejection logging is at a lower level, so the claim and the test differ in scope.
**Fix:** Reword the comments to "no Warning or Error line", or assert on `Lines` filtered to the rate-limiter category.

---

_Reviewed: 2026-10-25_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

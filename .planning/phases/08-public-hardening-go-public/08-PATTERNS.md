# Phase 8: Public Hardening & Go-Public - Pattern Map

**Mapped:** 2026-10-20
**Files analyzed:** 22 (new or modified)
**Analogs found:** 21 / 22

All paths are repo-relative. Placeholders only: RFC 5737 addresses, `example.com`. No planning references may enter any file outside `.planning/`; C# comments are `///` only, JS comments `/** */` only.

## File Classification

| New/Modified File | Role | Data Flow | Closest Analog | Match |
|---|---|---|---|---|
| `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` (grow into security headers, or add `SecurityHeaders.cs` beside it) | middleware | request-response | itself | exact |
| `Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs` (+ new header test file) | test | request-response | itself | exact |
| `Cabinet.Service/Hosting/ClientRateLimits.cs` (new: limiter registration, settings, partition key) | middleware / config | request-response | `Live/LiveEndpoints.cs` (`LiveSettings`) + `Hosting/ForwardedHeadersSetup.cs` | role-match |
| `Cabinet.Service/Sync/SyncEndpoints.cs` (per-client policy, 429 shape) | route | request-response | itself (`Handle`) | exact |
| `Cabinet.Service/Live/LiveEndpoints.cs` (per-address option, origin list, validation) | config/route | event-driven | itself | exact |
| `Cabinet.Service/Live/LiveConnectionLimiter.cs` (per-address count) | service | event-driven | itself | exact |
| `Cabinet.Service/Live/CabinetHub.cs` (pass address to limiter) | hub | pub-sub | itself | exact |
| `Cabinet.Service/Program.cs` (pipeline, Kestrel limits, AllowedHosts) | config | request-response | itself | exact |
| `Cabinet.Service/appsettings.json` | config | n/a | itself | exact |
| `Cabinet.IntegrationTests/Infrastructure/UntrustedPeerHost.cs` (add limiter) | test infra | request-response | itself | exact |
| new limiter tests (`SyncClientLimitTests.cs`, `LiveClientLimitTests.cs`, origin tests) | test | request-response / event-driven | `SyncNowTests.cs`, `LiveCapRefusalTests.cs`, `OwnerWallTests.cs` | exact |
| `Cabinet.UnitTests/Live/LiveSettingsTests.cs` (+ new keys) | test | config validation | itself | exact |
| `deploy/systemd/cabinet.service` | config | n/a | itself | exact |
| `deploy/nftables/cabinet.nft.in` (output chain) | config | n/a | itself | exact |
| `deploy/provision.d/50-firewall.sh` + `deploy/provision.sh` (`provision_render_template` validation) | provisioning | batch | itself | exact |
| `deploy/provision.conf.example` | config | n/a | itself | exact |
| `deploy/cabinet.env.example` | config | n/a | itself | exact |
| `deploy/tests/render-templates-test.sh`, `provision-logic-test.sh`, `sandboxing-test.sh` | test | n/a | themselves | exact |
| `deploy/bin/cabinet-selfcheck` (limits + egress check) | utility | request-response | `check_firewall` / `check_owner_port_firewall` | exact |
| `deploy/traefik/cabinet.yml.example` | config | n/a | itself | exact |
| `build/check-go-public.sh` (optional probe helper) + `build/tests/check-go-public-test.sh` | utility + test | request-response | `build/check-owner-routes.sh` + its test | exact |
| `docs/lxc-setup.md`, `docs/deploy.md`, new generic go-public checklist doc | docs | n/a | existing section structure | role-match |
| Browser tests (flake hardening, header/CSP checks) under `Cabinet.BrowserTests/` | test | request-response | `Infrastructure/CabinetPageTest.cs`, `LiveLookWebKitTests.cs` | exact |
| Container cap numbers in docs (Proxmox command) | docs | n/a | none in repo (owner-only) | no analog |

## Pattern Assignments

### Security headers: `Cabinet.Service/Hosting/ContentSecurityPolicy.cs` (middleware, request-response)

**Analog:** itself (lines 1-27). One static class, one `const`, one `Use...` extension, setting the header before `await next(context)` so every response (404s, static files, negotiate) carries it.

```csharp
public const string Policy =
    "default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'";

public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app)
{
    ArgumentNullException.ThrowIfNull(app);
    return app.Use(async (context, next) =>
    {
        context.Response.Headers.ContentSecurityPolicy = Policy;
        await next(context);
    });
}
```

Copy: a sibling `public const string` per header value (`nosniff`, referrer policy, permissions policy, COOP, CORP), set in the same middleware (or a `UseSecurityHeaders` sibling registered at the same first position). Class-level `///` summary explains why headers live with the app. Note: static files and `MapStaticAssets` may overwrite some headers; set before `next` and test static assets and box art explicitly. Frame denial, nosniff and referrer move out of Traefik (remove `contentTypeNosniff`, `frameDeny`, `referrerPolicy` from `cabinet-security-headers`; keep the `sts*` keys).

**Test analog:** `Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs`:
- lines 18-42: one `[Theory]` listing every response type (page, sample page, layout, cards, js, signalr lib, svg, status, 404s) with `new CabinetWebApplicationFactory(PrototypeOn)` and `factory.CreatePublicClient()`; copy the `[InlineData]` list and assert each new header per row.
- lines 44-54 redirect case (`LanguageTests.PlainClient(factory)`), lines 56-70 live negotiate (`PostAsync("/cabinet/live/negotiate?negotiateVersion=1", null, ...)`), plus the "never inline/eval/other origins" loop (lines 72-90).
- Both ports: use `factory.CreateOwnerClient()` (owner port needs the owner settings from `Infrastructure/OwnerSession.cs` / `OwnerWallTests.cs`) for the owner-port rows.
- Pattern for every test: `await using var factory = ...; using var client = ...; using var response = await client.GetAsync(path, TestContext.Current.CancellationToken);` then FluentAssertions.

### App-side rate limiter: new `Cabinet.Service/Hosting/ClientRateLimits.cs` (config + middleware)

No in-repo `AddRateLimiter` exists yet (no analog for the limiter itself). Copy structure from two analogs.

**Settings pattern** (`Cabinet.Service/Live/LiveEndpoints.cs` lines 7-48): a `sealed record` options type with a `Default...` const, a static `...Settings.FromConfiguration(IConfiguration)` that reads `"Section:Key"` string constants, absent key gives default, `int.TryParse(..., NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, ...)` plus range check, and throws a message that names the full key:

```csharp
if (!int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var value)
    || value < Smallest || value > Largest)
{
    throw new InvalidOperationException($"{MaxConnectionsKey} must be a whole number between {Smallest} and {Largest}.");
}
```

Registration validates now: `var options = LiveSettings.FromConfiguration(configuration); services.AddSingleton(options);` (lines 74-76) so a typo stops startup. Unit tests copy `Cabinet.UnitTests/Live/LiveSettingsTests.cs`: committed-appsettings-binds test (lines 13-21 via `RepositoryPaths.ServiceDirectory()`), missing-key default, `[Theory]` in-range, and out-of-range/NaN/empty asserting `.WithMessage("*Live:MaxConnections*")` (lines 38-49).

**Partition key:** the client address after forwarded headers, i.e. `context.Connection.RemoteIpAddress` once `UseForwardedHeaders` ran (see `Hosting/ForwardedHeadersSetup.cs` lines 21-36: `ForwardLimit = 1`, known proxies cleared and refilled from `ReverseProxy:KnownProxies`, bad entry throws `InvalidOperationException`). Never read `X-Forwarded-For` directly.

**Pipeline placement** (`Cabinet.Service/Program.cs` lines 67-95): order today is `UseContentSecurityPolicy` -> `UseConnectionPeer` -> `UseForwardedHeaders` -> `UseOwnerClientRange` -> health -> `UseOwnerPathGuard` -> `UseCabinetArt` -> `UseRouting` -> `UseOwnerOnlyRoutes` -> maps. Insert `app.UseRateLimiter()` after `UseRouting()` (line 94) and after `UseForwardedHeaders` (line 69), attach policies with `.RequireRateLimiting(...)` on `MapPost(SyncRoute...)` and `MapHub<CabinetHub>(...)`. Services go beside `builder.Services.AddCabinetLive(...)` (line 50); Kestrel limits join the existing `builder.Services.Configure<KestrelServerOptions>(options => options.AddServerHeader = false);` (line 43), or `LiveEndpoints` style `services.Configure<KestrelServerOptions>(...)` (line 87). Note `MaxConcurrentUpgradedConnections` is already set from `Live:MaxConnections` there.

### `Cabinet.Service/Sync/SyncEndpoints.cs` (route, request-response): 429 shape

**Analog:** itself, `Handle` lines 168-192 and `WholeSecondsUntil` 194-195. The per-client refusal must produce the identical shape so the page's status line and countdown work:

```csharp
case SyncRequestResult.CoolingDown cooling:
    context.Response.Headers.RetryAfter = WholeSecondsUntil(cooling.Until, time.GetUtcNow())
        .ToString(CultureInfo.InvariantCulture);
    return Results.Json(
        new { outcome = "cooldown", status = statusService.Current() },
        statusCode: StatusCodes.Status429TooManyRequests);
```

Use limiter `OnRejected` that sets `Retry-After` (from `MetadataName.RetryAfter` lease metadata, whole seconds, min 1, same `InvariantCulture` formatting), `Cache-Control: no-store` (as lines 163 and 170), and writes JSON `{ outcome = "cooldown", status = ... }` with status 429; resolve `SyncStatusService` from `context.HttpContext.RequestServices`. Endpoint map style: `endpoints.MapPost(SyncRoute, Handle);` (line 155) gets `.RequireRateLimiting(policyName)`.

**Test analog:** `Cabinet.IntegrationTests/SyncNowTests.cs` lines 17-64: `ScriptedBggHandler.ForCollection(SyntheticBggCollection.Create(3))`, `SyncHarness.CreateFactory(handler, SyncHarness.NewClock())`, `client.PostAsync(SyncHarness.SyncRoute, content: null, TestContext.Current.CancellationToken)`, assert `HttpStatusCode.TooManyRequests`, `second.Headers.RetryAfter!.Delta`, body `outcome`. Class carries `[Trait("Category", "Sync")]`. For a different address, use `UntrustedPeerHost` (below) or send `X-Forwarded-For` from loopback (loopback is trusted in the full factory).

### Live channel: `Live/LiveEndpoints.cs`, `LiveConnectionLimiter.cs`, `CabinetHub.cs`

**Analog:** themselves.
- `LiveOptions(int MaxConnections)` record (lines 9-13): add `MaxConnectionsPerClient` (default about 20) and an allowed-origins list; extend `LiveSettings.FromConfiguration` with new key constants (`Live:MaxConnectionsPerClient`, `Live:AllowedOrigins:N`) in the same validate-and-name-the-key style. Origins come from env (`Live__AllowedOrigins__0=https://cabinet.example.com`); put placeholders in `deploy/cabinet.env.example`, never real hosts.
- `MapCabinetLive` (lines 93-105): the `MapHub<CabinetHub>(Route, connection => {...})` callback is where `Transports`, buffer sizes are set; chain `.RequireRateLimiting(...)` on the returned builder. Origin check: either an endpoint filter / small middleware on `LiveEndpoints.Route` returning 403 before negotiate, or in `connection` options. Both transports (SSE and WebSockets) pass through negotiate, so a check at negotiate plus connect covers both.
- `LiveConnectionLimiter` (lines 8-68): `Lock _gate`, `HashSet<string> _admitted`, `TryAdmit(connectionId)` idempotent for already-admitted ids. For per-address counting change to `Dictionary<string, string>` (id -> address) or a second `Dictionary<string, int>`, keep every mutation inside `lock (_gate)`, keep `IsAdmitted`/`Release` semantics ("never admitted frees nothing"), add a `TryAdmit(connectionId, clientAddress)` overload and a count-by-address accessor for tests.
- `CabinetHub.OnConnectedAsync` (lines 26-34): `if (!limiter.TryAdmit(Context.ConnectionId)) { Context.Abort(); }` then `base.OnConnectedAsync()`. Do not throw (class summary explains why: log noise and reconnect permission). Get the address with `Context.GetHttpContext()?.Connection.RemoteIpAddress`. Keep `OnDisconnectedAsync` releasing.

**Tests:** `Cabinet.IntegrationTests/LiveCapRefusalTests.cs`: `[Trait("Category", "Live")]`, settings dictionary `["Live:MaxConnections"] = "1"`, `CreateCountingFactory(handler, settings, attempts)`, `ReconnectingPage` over `HttpTransportType.WebSockets` and `ServerSentEvents` theory (lines 26-31), `factory.ServingServices.GetRequiredService<LiveConnectionLimiter>()` and `limiter.Count`, `KestrelUpgradeWatch(factory.PublicPort)`. Copy the theory shape for the per-address cap with `Live:MaxConnectionsPerClient=1`, covering both transports. Also `Cabinet.UnitTests/Live/LiveLimitsTests.cs` for limiter unit behaviour, and `Cabinet.IntegrationTests/ContentSecurityPolicyTests.cs` lines 56-70 for a negotiate request (add `Origin` header cases: allowed 200, foreign 403, absent per decision).

### Untrusted-peer test host: `Cabinet.IntegrationTests/Infrastructure/UntrustedPeerHost.cs`

**Analog:** itself. In-memory `TestServer` host that runs the real `ForwardedHeadersSetup.Configure` and lets the test choose the peer address and local port. Key lines:

```csharp
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Configuration.AddInMemoryCollection(settings);   // "ReverseProxy:KnownProxies:0" = knownProxy
builder.Services.Configure<ForwardedHeadersOptions>(
    options => ForwardedHeadersSetup.Configure(options, builder.Configuration));
var app = builder.Build();
app.UseConnectionPeer();
app.UseForwardedHeaders();
app.UseOwnerClientRange();
app.MapGet("/probe", ...);
```

Extend with the limiter registration and `UseRouting`/`UseRateLimiter` + a rate-limited probe route so tests can show that (a) a forged `X-Forwarded-For` from an untrusted peer cannot dodge or poison another client's bucket, (b) two different forwarded clients from the trusted proxy get separate buckets. Existing consumer: `OwnerWallTests.cs` (shows `GetProbeAsync(peer, localPort, forwardedFor)` usage).

### `Cabinet.Service/Program.cs` additions

Keep first four lines of the pipeline order. Kestrel limits (`MaxConcurrentConnections`, `MaxRequestBodySize`, `RequestHeadersTimeout`, `KeepAliveTimeout`, `MinRequestBodyDataRate`) via `builder.Services.Configure<KestrelServerOptions>(...)` as at lines 43/87 (read values from config with the validate-and-name-the-key settings pattern). `AllowedHosts` is a config key in `appsettings.json` (currently `*`); production value comes from the env file (`AllowedHosts=cabinet.example.com;owner.example.com` placeholder in `deploy/cabinet.env.example`). Test with a host-header request through `CabinetWebApplicationFactory` (ports are loopback; set `Host` header explicitly) and an in-process test that the committed default still lets tests pass.

### `deploy/systemd/cabinet.service`

**Analog:** itself. Directives are grouped in `[Service]` with a blank line before the sandbox block (lines 18-29). Add a labelled group (resource limits) using `MemoryHigh=`, `MemoryMax=`, `CPUQuota=`, optionally `TasksMax=`, sized below container caps. No `#` comments exist in this file; if needed use `#` lines (unit files allow them; the `//` rule is C#-only) but keep placeholders-free and planning-free.

**Tests:** `deploy/tests/sandboxing-test.sh` reads `APP_UNIT="${DEPLOY_DIR}/systemd/cabinet.service"` (line 23) and checks directives in the unit; add assertions in the same style for the new directives. `deploy/bin/cabinet-selfcheck` line 347-348 (`--property=NoNewPrivileges=yes --property=ProtectSystem=strict` passed to a check of the running unit) is the pattern for confirming effective limits (`systemctl show cabinet --property=MemoryMax,CPUQuotaPerSecUSec`), and `has_line` / `pass` / `fail` helpers (lines 49-56) are the reporting style.

### `deploy/nftables/cabinet.nft.in` (config) + `provision.d/50-firewall.sh` + `provision.sh`

**Analog:** itself. Today (lines 33-35): `chain output { type filter hook output priority 0; policy accept; }`. Template tokens use `@CABINET_..._@` and rendering is by `provision_render_template` (`deploy/provision.sh` lines 283-324), which per token runs `provision_validate_safe_value` (no semicolon, brace, newline), then a `case "$name"` branch per token for typed validation:

```bash
case "$name" in
  CABINET_TRAEFIK_IP)        provision_validate_ipv4 "$value" || provision_die "..." ;;
  CABINET_ADMIN_SSH_SOURCES) provision_validate_cidr_list "$value" || provision_die "..." ;;
esac
```

Add new tokens (e.g. `CABINET_EGRESS_BLOCKED_RANGES` as a CIDR list, `CABINET_EGRESS_ALLOWED_HOSTS` or resolver address(es)) with their own `case` branch reusing `provision_validate_cidr_list` / `provision_validate_ipv4`, and pass them in `50-firewall.sh` lines 23-25 exactly like the existing pairs (`"CABINET_TRAEFIK_IP=${CABINET_TRAEFIK_IP:-}"`). Rule shape follows the input chain syntax (`ip saddr { @LIST@ } accept`): output chain `oif lo accept`, `ct state established,related accept`, accept resolver/NTP exceptions first (`ip daddr @RESOLVER@ udp dport 53 accept` and tcp), then `ip daddr { @BLOCKED@ } drop` (or `reject`), then accept the rest. Keep the dry-run `nft -c -f` before load (lines 27-31); update the log line at 41. An empty-list guard is needed: `{ }` with an empty list is invalid nft; require non-empty or make the rule conditional (provision refuses placeholders, see `provision.conf.example` header).

**`deploy/provision.conf.example`:** copy the comment-then-`KEY=CHANGE-ME` pattern (lines 7-13); document the format ("comma-separated CIDR list such as 192.0.2.0/24,198.51.100.0/24"). Provisioning refuses to apply while `CHANGE-ME` remains; check the loader at `provision-logic-test.sh` lines 129-148, 229-252 which enumerates the known keys (`unset CABINET_TRAEFIK_IP CABINET_ADMIN_SSH_SOURCES CABINET_GITHUB_REPO`), so new keys must be added to the key list in `provision.sh` and to those test cases.

**Tests:**
- `deploy/tests/render-templates-test.sh` lines 84-135 (render valid/invalid values, one `subshell_succeeds` + `pass`/`failtest` per case, covering bad IP, bad CIDR, semicolon, brace, newline) and the rendered-ruleset block (lines ~147-170 in its numbering of the nft assertions): `assert_eq`/`assert_matches`/`assert_lacks`. Update the now-false assertion `'hook output priority 0; policy accept;'` (output chain accepts by default) and the `grep -c 'tcp dport'` count of 3, and add `assert_matches` for the new drop rule, DNS exception and established rule. Keep `assert_lacks '@[A-Z][A-Z0-9_]*@'` and the no-`5081` check.
- `deploy/tests/provision-logic-test.sh`: loader tests for new keys.
- `selfcheck`: add `check_egress` modelled on `check_firewall` lines 179-200 (`ruleset="$(nft list table inet cabinet_filter 2>&1)"`, regex on `hook output priority [^;]*; policy ...`, `pass`/`fail`, print no address) and register it in `main` (lines 385-396 call list).

### `deploy/traefik/cabinet.yml.example`

**Analog:** itself. Middlewares block lines 34-53; public router `cabinet` lines 56-65 lists `middlewares: [cabinet-lan-only, cabinet-security-headers]`; owner router `cabinet-owner` lines 67-76 must keep `cabinet-lan-only` and get NO rate limit (decision: owner router unlimited). Add:

```yaml
    cabinet-rate-limit:
      rateLimit:
        average: 100     # placeholder; sized from a measured first visit
        period: 1s
        burst: 200
        sourceCriterion:
          ipStrategy:
            depth: 0
    cabinet-in-flight:
      inFlightReq:
        amount: 50
        sourceCriterion:
          ipStrategy:
            depth: 0
```

and add both names to the public router only. Header comment (lines 7-16) must be updated: the public router later drops `cabinet-lan-only`; keep the explanation about `trustedIPs`/`notAppendXForwardedFor` (lines 18-22) because per-client limits key on the same address. Reduce `cabinet-security-headers` to HSTS keys only (lines 46-53), and update the comment at lines 43-45. Numbers are Claude's-discretion placeholders; YAML comments are allowed in this file (the `//` rule is C#-only). All hosts stay `example.com`, addresses RFC 5737.

### `deploy/cabinet.env.example`

**Analog:** itself (placeholder style lines 1-18). Pattern: commented explanatory block, `Key__Sub__0=placeholder`, optional lines commented out with "uncomment together" guidance. Add `AllowedHosts=cabinet.example.com;owner.example.com`, `Live__AllowedOrigins__0=https://cabinet.example.com`, `Live__AllowedOrigins__1=https://owner.example.com`, and per-address cap overrides, all placeholders.

### `build/check-go-public.sh` (optional) + `build/tests/check-go-public-test.sh`

**Analog:** `build/check-owner-routes.sh` (93 lines) and `build/tests/check-owner-routes-test.sh` (150 lines).

Script pattern: header `###` doc block saying hostnames come from env vars and are never printed; `set -euo pipefail`; `pass()` / `fail()` print `PASS - ...` / `FAIL - ...` with status codes only; `fetch HOST PATH` uses `curl -s --max-time 15 -o "$BODY_FILE" -w '%{http_code}'` and defaults `STATUS="${STATUS:-000}"`; `expect_status label host path wanted`; `main` returns 2 on missing env vars and `[[ "$FAILED" -eq 0 ]]` at the end; `WORK_DIR="$(mktemp -d)"; trap 'rm -rf "$WORK_DIR"' EXIT`.

```bash
fetch() {
  local host="$1" path="$2"
  STATUS="$(curl -s --max-time 15 -o "$BODY_FILE" -w '%{http_code}' "https://${host}${path}" 2>/dev/null || true)"
  STATUS="${STATUS:-000}"
}
```

For header checks add a `fetch_headers` using `curl -sI`/`-D` into a file, then `grep -qiF`. For the external probe: `/health` on public, owner (from outside, expect 404/000/403), and the ops port via `curl --connect-timeout` expecting `000`.

Test pattern: a stub `curl` in `${WORK_DIR}/bin` that parses `-o`, `-w`, `--max-time`, and the URL and answers from `STUB_TABLE` (tab-separated URL, status, body; unknown URL prints `000`); `write_table` builds a healthy table and takes `URL=STATUS=BODY` overrides; `run_check` clears env with `env -u ... PATH="${STUB_BIN}:${PATH}"`; `check description expected actual` counts failures; assertions that exit code is 0 on healthy, one named FAIL per deviation, exit 2 without hostnames, and that the report never prints either hostname (`count_matching "$OWNER_HOST"` is 0). Test hostnames: `owner.example.com`, `cabinet.example.org`. A stub for header output needs the table extended with a headers column. The script is picked up by `build/lint/checks/30-shell.sh` and `50-script-tests.sh`; check that new scripts/tests are listed or auto-discovered there.

### Docs: `docs/lxc-setup.md`, `docs/deploy.md`, new go-public checklist

**Analog:** existing structure.
- `docs/lxc-setup.md`: numbered `## N. Title` sections (1-12, plus `5a`, `7a`). Section 6 (lines 157-175, "Install the Traefik route") currently states the headers middleware sets HSTS, nosniff, frame denial and referrer policy and that making the site public is removing the allow-list middleware: update both to the new split (Traefik HSTS only; app sends the rest; add the rate-limit middlewares). Section 7a (lines 182-226) is the model for a numbered owner-steps list with fenced `text` blocks and `{your-lan-range}` placeholders: use it for the Proxmox cap command (placeholders `{ctid}`, `{cores}`, `{memory-mb}`, `{swap-mb}`, `{cpu-units}`), the SSH restriction step, and the egress ranges in `provision.conf`. Section 10 (selfcheck, line 252) should mention the new checks.
- `docs/deploy.md`: `## Checking a published release from a workstation` (line 204) and `## Switching the owner tools on after a release` (line 215) are the models for the route-check flow; add the burst-test and go-public/rollback flow beside them.
- New generic checklist (e.g. `docs/go-public-checklist.md`): `docs/releasing.md` / `docs/github-repository-settings.md` show checklist-style docs. Placeholders only, no planning references, reusable for re-checks, explain plainly where each rate limit sits (Traefik counts per internet address and refuses a flood early; the app adds stricter limits on sync-now and the live channel).

### Browser tests: `Cabinet.BrowserTests/Infrastructure/CabinetPageTest.cs` and `LiveLookWebKitTests.cs`

**Analog `CabinetPageTest`:** abstract class deriving `PageTest` (Playwright xunit.v3). `StartAsync(settings, configureServices)` or `StartAsync(CabinetWebApplicationFactory)` stores `_factory`, sets `Accept-Encoding: identity`, wires `Page.Console`/`Page.PageError`/`Page.Request` into locked recorders; exposes `ConsoleErrors`, `RequestHosts`, `PublicPort`, `OwnerPort`; cabinet-ready selector `"#cabinet .placement, .cabinet-message:not(.cabinet-loading)"`; screenshots under `CABINET_SCREENSHOT_DIR`; the base disposes the host with the page (inotify limit: every host must dispose its watchers). Header or CSP-under-new-headers browser checks derive from this and assert `ConsoleErrors` empty and `RequestHosts` same-origin only.

**CSP-violation listening pattern:** `Cabinet.BrowserTests/LightsTests.cs` line 73: `document.addEventListener('securitypolicyviolation', (event) => window.__dom.violations.push(event.violatedDirective));` installed with an init script; same in `FilterAccessTests` and `FilterDockMemoryTests`. After COOP/CORP/Permissions-Policy are added these must stay green.

**Live-spy helper (`LiveLookWebKitTests.cs` lines 54-77):** `const string LiveSpy = """ ... """` installed with `page.AddInitScriptAsync(LiveSpy)` (line 102). It defines `window.signalR` with getter/setter, wraps `HubConnectionBuilder.prototype.build`, increments `rec.built` and, via `connection.on('statusChanged', ...)`, `rec.heard`. Hardening recipe from the class `<remarks>` (lines 20-26): wait for a condition, never a fixed time; re-send `live.PublishAsync(status.Current(), ct)` (line ~146) until `heard` advances before publishing the real change; read end state with retrying Playwright assertions (`Expect(...)`); skip via `Assert.SkipUnless(Environment.GetEnvironmentVariable(EnableVariable) == "1", SkipReason)` for engine-specific tests. Copy this approach (not new infrastructure) for the flaky todo tests: `LiveLookTests`, `CardSyncTests`, `BackButtonTests`, `SheetDragTests`, `DefaultLookIdentityTests`. Run geometry checks under the DejaVu fontconfig (CI font differs); do not run browser suites in parallel (inotify limit).

## Shared Patterns

### Settings are validated at startup, naming the key
**Source:** `Cabinet.Service/Live/LiveEndpoints.cs` lines 19-48 and 69-77; test `Cabinet.UnitTests/Live/LiveSettingsTests.cs`.
**Apply to:** every new limit/Kestrel/origin setting; `Add...` methods call `FromConfiguration` eagerly.

### Client address comes only from the forwarded-headers setup
**Source:** `Cabinet.Service/Hosting/ForwardedHeadersSetup.cs` (lines 16-37), wired at `Program.cs` lines 58-59 and 67-70.
**Apply to:** limiter partition key, per-address live cap. Test only with `UntrustedPeerHost` (untrusted peer plus forged header) and the full factory (loopback is trusted).

### One middleware, one test theory per cross-cutting header
**Source:** `ContentSecurityPolicy.cs` + `ContentSecurityPolicyTests.cs` lines 18-42.
**Apply to:** all new headers, on public and owner ports, all response types.

### Quiet refusal, existing response shape
**Source:** `SyncEndpoints.Handle` lines 179-185 (429 + `Retry-After` + JSON `outcome=cooldown`); `CabinetHub` aborts rather than throws (class summary).
**Apply to:** sync limiter rejection; live refusals; no new copy or pages.

### Templates: token validation + logic tests
**Source:** `deploy/provision.sh` lines 283-324; `deploy/tests/render-templates-test.sh` lines 84-135.
**Apply to:** every new `@TOKEN@` in `cabinet.nft.in` (add a typed `case` branch, add render and refusal tests).

### Check scripts print PASS/FAIL, never hostnames or addresses
**Source:** `deploy/bin/cabinet-selfcheck` lines 49-50 (`pass`/`fail`); `build/check-owner-routes.sh` lines 28-33.
**Apply to:** new selfcheck checks and the probe helper; tests assert hostnames never appear in output.

### Placeholders and rule hygiene
RFC 5737 addresses (`192.0.2.0/24`, `198.51.100.0/24`), `example.com` / `example.org`; `Cabinet A`/`Shelf B` style names; no `//` in C#, `/** */` in JS; no planning identifiers in docs, tests, comments or script text.

## No Analog Found

| File | Role | Data Flow | Reason |
|---|---|---|---|
| ASP.NET `AddRateLimiter` policies (inside `ClientRateLimits.cs`) | middleware | request-response | No built-in rate limiter exists in the repo yet; use settings/registration/test patterns above and RESEARCH.md examples for the limiter API |
| Proxmox cap command, measured numbers | docs | n/a | Owner-only, outside the repo; document with placeholders in `docs/lxc-setup.md` |

## Metadata

**Analog search scope:** `Cabinet.Service/{Hosting,Live,Sync}`, `Program.cs`, `Cabinet.IntegrationTests` (+ `Infrastructure`), `Cabinet.UnitTests/Live`, `Cabinet.BrowserTests` (+ `Infrastructure`), `deploy/**`, `build/check-owner-routes.sh` and its test, `docs/lxc-setup.md`, `docs/deploy.md`.
**Files scanned:** about 40 (key ones read in full or targeted ranges).
**Pattern extraction date:** 2026-10-20

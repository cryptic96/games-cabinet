# Phase 1: Repo, Guardrails & Walking-Skeleton Deploy - Pattern Map

**Mapped:** 2026-10-04
**Files analyzed:** about 70 new files (grouped below)
**Analogs found:** about 62 / 70 (the rest have no analog or are new designs)

This repository has no application code yet. Every analog lives in the sibling reference checkout (`ing-dashboard`, GitHub `cryptic96/ing-dashboard`), referred to below as `ing-dashboard:<path>`. Rename map applied everywhere: `ledger`->`cabinet`, `LEDGER_`->`CABINET_`, `Ledger.`->`Cabinet.`, `ledger_`->`cabinet_`. Action column: keep (verbatim), adapt (rename and trim), drop, new.

The full per-file keep/adapt/drop inventory with line counts is in the Port Inventory table of the phase research document. This map adds the structural excerpts and the analog pointers the planner needs for each plan action.

## File Classification

| New file | Role | Data flow | Analog (ing-dashboard) | Action | Match |
|----------|------|-----------|------------------------|--------|-------|
| `Directory.Build.props`, `global.json` | config | n/a | same names | keep | exact |
| `Cabinet.slnx` | config | n/a | `Ledger.slnx` | adapt (no Dashboards project) | exact |
| `Cabinet.Domain/BuildInfo.cs` | model (pure) | transform | none (design in research Code Examples) | new | none |
| `Cabinet.Repository/Images/ImageSmoke.cs` | utility | file-I/O, transform | none (SkiaSharp, D-17) | new | none |
| `Cabinet.Service/Program.cs` | host entry | request-response | `Ledger.Service/Program.cs` | rewrite small | role-match |
| `Cabinet.Service/Hosting/OpsEndpoint.cs` | utility | config | `Ledger.Service/Hosting/OpsEndpoint.cs` | keep, rename | exact |
| `Cabinet.Service/appsettings*.json` | config | n/a | `Ledger.Service/appsettings*.json` | adapt | exact |
| `Cabinet.Service/Pages/Index.cshtml`, `_ViewImports`, `wwwroot/css/site.css` | component | request-response | none | new | none |
| `Cabinet.UnitTests/*`, `Cabinet.IntegrationTests/*` | test | request-response | `Ledger.UnitTests`, `Ledger.IntegrationTests` | adapt | role-match |
| `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs` | test infra | request-response | `LedgerWebApplicationFactory.cs` | adapt | exact |
| `.github/workflows/ci.yml`, `release.yml` | config | batch | same names | adapt | exact |
| `.github/dependabot.yml`, `zizmor.yml` | config | n/a | same names | adapt / keep | exact |
| `.gitleaks.toml`, `.gitignore` | config | n/a | same names | adapt | exact |
| `.githooks/pre-commit`, `commit-msg`, `pre-push` | hook | batch | none (design in research Pattern 6) | new | none |
| `build/lint.sh`, `build/lint/compose.yaml`, `checks/10,20,30,40,50` | lint framework | batch | same names | adapt (20 keep, 60 drop) | exact |
| `build/package-release.sh` | script | file-I/O | same | adapt | exact |
| `build/validate-release-tag.sh` (+ test) | script | transform | same | keep | exact |
| `build/verify-published-release.sh` | script | request-response | same | adapt | exact |
| `build/check-github-settings.sh` | script | request-response | same | adapt + extend | exact |
| `build/tests/githooks-test.sh` | test | batch | `build/tests/validate-release-tag-test.sh` (shape only) | new | partial |
| `deploy/bin/cabinet-deploy` | installer | event-driven (timer), file-I/O | `deploy/bin/ledger-deploy` | adapt + extend | exact |
| `deploy/lib/common.sh`, `deploy/lib/deploy.sh` | library | file-I/O | same | adapt | exact |
| `deploy/bin/cabinet-selfcheck` | script | request-response | `deploy/bin/ledger-selfcheck` (738 lines) | replace with trimmed new | partial |
| `deploy/provision.sh`, `provision.d/10,20,40,50` | provisioning | batch | same | adapt (30, 60 dropped) | exact |
| `deploy/systemd/cabinet.service`, `cabinet-deploy-poll.service`, `.timer` | config | n/a | `ledger.service`, `ledger-deploy-poll.*` | adapt | exact |
| `deploy/nftables/cabinet.nft.in`, `deploy/traefik/cabinet.yml.example` | config | n/a | `ledger.nft.in`, `ledger.yml.example` | adapt | exact |
| `deploy/*.example`, `deploy/versions.env` | config | n/a | same | adapt | exact |
| `deploy/tests/*` (logic, provision, templates, sandboxing, versions, tamper, host-guard) | test | batch | same names | adapt / keep | exact |
| `docs/*.md`, `README.md`, `LICENSE` | docs | n/a | `docs/*.md` | adapt / new | role-match |

## Pattern Assignments

### `Cabinet.Service/Program.cs` (host entry, request-response)

**Analog:** `ing-dashboard:Ledger.Service/Program.cs` (127 lines). Keep only these parts: `builder.Logging.AddSystemdConsole()` in Production (line 37), `Configure<ForwardedHeadersOptions>` with `XForwardedFor | XForwardedProto` and KnownProxies from `ReverseProxy:KnownProxies` (line 65), `app.UseForwardedHeaders()` first in the pipeline (line 97), `app.UseHealthChecks("/health", opsPort, ...)` (line 108), and the trailing partial class (lines 126-127):

```csharp
/// <summary>Entry point for the ledger host, exposed as a partial class so the integration test factory can boot it in-process.</summary>
public partial class Program;
```

Drop repository, ingestion, auth, DataProtection and Prometheus wiring. Add the `image-smoke` subcommand before `WebApplication.CreateBuilder`, and make the health response writer emit JSON with `status`, `version` and `commit` (core shape is in the research Code Examples "Program.cs core"). Replace the raw `new Uri(...).Port` with `OpsEndpoint.FromConfiguration(app.Configuration)`.

### `Cabinet.Service/Hosting/OpsEndpoint.cs` (utility, config)

**Analog:** `ing-dashboard:Ledger.Service/Hosting/OpsEndpoint.cs`. Keep verbatim apart from the namespace. Core pattern:

```csharp
public static int FromConfiguration(IConfiguration configuration)
{
    var url = configuration[ConfigurationKey];   // "Kestrel:Endpoints:Ops:Url"
    ...
    if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !IsLoopbackHost(uri.Host))
        throw new InvalidOperationException($"{ConfigurationKey} must use a loopback host (127.0.0.1, ::1 or localhost).");
    return uri.Port;
}
```

Comment style: `///` summaries on type and members only. Its unit test (`OpsEndpointTests`) ports along with it.

### `Cabinet.Service/appsettings.json` (config)

**Analog:** `ing-dashboard:Ledger.Service/appsettings.json` lines 5 and 8 (listener URLs). Cabinet uses endpoint names `Web` (`http://0.0.0.0:5080`) and `Ops` (`http://127.0.0.1:5081`); full snippet is in the research Code Examples. Drop the EF log filter and the connection string. Do not add a test asserting the committed ops port (the rehearsal breaks it on purpose).

### `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs` (test infra)

**Analog:** `ing-dashboard:Ledger.IntegrationTests/Infrastructure/LedgerWebApplicationFactory.cs`. Keep: `WebApplicationFactory<Program>` subclass, two free loopback ports picked in the constructor, `CreateApiClient()`/`CreateOpsClient()` (rename the first to a public-client name), `builder.UseEnvironment("Testing")`, in-memory configuration for the two `Kestrel:Endpoints:*:Url` keys, and the two-host trick (`CreateHost` builds the TestServer host and a real Kestrel host). Drop: connection string, certificate environment variables, database fixture, scheduler flag, `CapturingLoggerProvider` unless a test needs it. Test project csproj shape and package versions are in the research Code Examples ("Test project shape").

### `Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs` (test)

**Analog:** `ing-dashboard:Ledger.UnitTests/Configuration/CommittedConfigurationTests.cs`. Keep (secret-shaped value scan of committed `appsettings*.json`), rename namespace.

### `Cabinet.Repository/Images/ImageSmoke.cs` and its test (utility, transform)

**Analog:** none. ImageSharp examples in the research are superseded by the SkiaSharp decision. Use the API calls recorded in the research Open Questions note: `SKBitmap.Decode`, `bitmap.Resize(new SKImageInfo(w, h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))`, `SKImage.FromBitmap(...).Encode(SKEncodedImageFormat.Webp, 80)`, decode the bytes back, require expected dimensions and the `RIFF`/`WEBP` markers. Packages: `SkiaSharp` 4.153.1 and `SkiaSharp.NativeAssets.Linux.NoDependencies` 4.153.1 pinned together. Keep the public shape from the research example (static class, `Run()` returning width, height, byte count, throwing `InvalidOperationException` on mismatch, `///` docs). Subcommand output line: `PASS image-smoke {w}x{h} webp {n} bytes`. Add a large noisy synthetic bitmap case so the encoder does real work.

### `.github/workflows/ci.yml` (config, batch)

**Analog:** `ing-dashboard:.github/workflows/ci.yml` (77 lines). Keep layout:

```yaml
on:
  push: {}
  pull_request:
    branches:
      - main
permissions: {}
concurrency:
  group: ${{ github.workflow }}-${{ github.ref }}
  cancel-in-progress: true
jobs:
  build-test:
    runs-on: ubuntu-24.04
    timeout-minutes: 20
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@3d3c42e5aac5ba805825da76410c181273ba90b1 # v7.0.1
        with:
          persist-credentials: false
      - uses: actions/setup-dotnet@a98b56852c35b8e3190ac28c8c2271da59106c68 # v6.0.0
        with:
          global-json-file: global.json
```

Drop the `services.postgres` block and the database/efbundle env vars. Job ids `build-test` and `lint` are the required status check names and must not change. Everything pinned by SHA with a version comment (zizmor `hash-pin`).

### `.github/workflows/release.yml` (config, batch)

**Analog:** `ing-dashboard:.github/workflows/release.yml` (152 lines). Keep: `permissions: {}` at top, `concurrency` with `cancel-in-progress: false`, `build` job (validate tag, package, test, attest, copy bundle, `gh release create "$TAG" --draft --verify-tag ...`), and the `publish` job:

```yaml
  publish:
    needs: build
    environment: deploy
    permissions:
      contents: write
    ...
          sha256sum -c "ledger-$VERSION.zip.sha256"
          gh attestation verify "release/ledger-$VERSION.zip" \
            --bundle "release/ledger-$VERSION.zip.sigstore.json" \
            --repo "$GITHUB_REPOSITORY" \
            --signer-workflow "$GITHUB_REPOSITORY/.github/workflows/release.yml" \
            --source-ref "refs/tags/$TAG" \
            --deny-self-hosted-runners
          gh release edit "$TAG" --repo "$GITHUB_REPOSITORY" --draft=false --latest
```

Asset names become `cabinet-$VERSION.zip`, `.sha256`, `.zip.sigstore.json`. Remove migration/postgres plumbing. Draft notes are edited before approval for the rehearsal release.

### `build/lint.sh` and `build/lint/checks/*.sh` (lint framework, batch)

**Analog:** `ing-dashboard:build/lint.sh` (94 lines) and `build/lint/checks/`. Skeleton: iterate `build/lint/checks/[0-9][0-9]-*.sh`, derive names, run each, aggregate failures; checks self-test before scanning. Required change: the reference builds `EXTRA_MOUNT="-v $GIT_COMMON_DIR_ABS:$GIT_COMMON_DIR_ABS:ro"` as an unquoted string and exports `LINT_COMPOSE="docker compose ... $EXTRA_MOUNT"`; this checkout's directory name contains a space, so build it as a bash array and quote every expansion. Per check: `10-repo-rules.sh` adapt (new key prefixes, JS `//` rule, LICENSE check, keep bracket/`printf` split tricks so it never matches itself); `20-workflows.sh` and its 4 fixtures keep verbatim; `30-shell.sh` add `.githooks/*`; `40-secrets.sh` drop IBAN tests; `50-script-tests.sh` rename env var, globs `deploy/tests/*-test.sh build/tests/*-test.sh`, skip `*-network-test.sh` unless enabled; `60-observability.sh` drop. `compose.yaml`: keep actionlint, zizmor, shellcheck, gitleaks services with the pinned digests; drop promtool and grafana. gitleaks must also scan all refs (`--log-opts="--all"`), not just `HEAD`.

### `build/package-release.sh` (script, file-I/O)

**Analog:** `ing-dashboard:build/package-release.sh` (114 lines). Keep strict semver and 40-hex checks, `dotnet restore --locked-mode`, `dotnet publish <Service csproj> -r linux-x64 --self-contained false -p:Version -p:SourceRevisionId -p:ContinuousIntegrationBuild=true`, copy of `deploy/` excluding `./tests`, then:

```bash
find . -type f | sed 's|^\./||' | sort | zip -X -q "$ZIP_PATH" -@
sha256sum "$(basename "$ZIP_PATH")" > "$(basename "$CHECKSUM_PATH")"
```

Drop `dotnet tool restore`, `dotnet ef migrations bundle`, the migrations manifest. Manifest becomes `jq -n --arg version ... --arg commit ... '{version: $version, commit: $commit}'`. Zip now carries `libSkiaSharp.so` (about 12 MB).

### `deploy/lib/common.sh` (library)

**Analog:** `ing-dashboard:deploy/lib/common.sh` (186 lines). Keep `*_log` (timestamped line on stderr), `*_die` (log `ERROR:` and `exit 1`), `*_load_conf` (never sources the file; allow-listed keys only; refuses non-root-owned or group/world-writable files; relocated-root test mode via `*_DEPLOY_ROOT`), and `*_semver_gt` (numeric `MAJOR.MINOR.PATCH` compare with `10#` arithmetic):

```bash
cabinet_semver_gt() {
  local a="$1" b="$2"
  IFS='.' read -r a_major a_minor a_patch <<< "$a"
  IFS='.' read -r b_major b_minor b_patch <<< "$b"
  if (( 10#$a_major != 10#$b_major )); then (( 10#$a_major > 10#$b_major )); return; fi
  ...
}
```

Drop the textfile-metrics and email helpers. Allow-list: `CABINET_GITHUB_REPO`, `CABINET_SIGNER_WORKFLOW`, `CABINET_KEEP_RELEASES`, `CABINET_HEALTH_TIMEOUT_SECONDS`, `CABINET_OPS_URL`.

### `deploy/lib/deploy.sh` (library, file-I/O)

**Analog:** `ing-dashboard:deploy/lib/deploy.sh` (605 lines). Keep these functions:
- `*_verify_attestation` (lines 17-53): `env -u GH_TOKEN -u GITHUB_TOKEN -u GH_ENTERPRISE_TOKEN GH_CONFIG_DIR="$(mktemp -d)" gh attestation verify "$artifact" --bundle ... --repo ... --signer-workflow "${repo}/${signer_workflow}" --source-ref ... --deny-self-hosted-runners --format json`, then extract `.[0].verificationResult.signature.certificate.sourceRepositoryDigest`. Keep the header comment but word it precisely: no GitHub credential and no GitHub API call, but Sigstore must be reachable.
- `*_commit_on_branch` (unauthenticated compare API).
- `*_activate_release` (lines 250-276): records the outgoing version in `${state_dir}/previous`, then `ln -s "$target" "$tmp_link"; mv -T "$tmp_link" "$current_link"` for the atomic swap.
- `*_prune_releases`.
- `*_wait_for_health` (lines 338-376): deadline loop with `curl --fail --silent --max-time 5 "${ops_url}/health"`, `sleep 2`. Adapt: today it requires the body `Healthy` and then `/metrics` containing the version. Cabinet: parse the JSON body with `jq` and require `.status == "Healthy"` and `.version == $expected`; drop Grafana/Prometheus arguments.
- `*_install_verified_release`, `*_rollback_release`: keep structure, drop migrations, provisioning install, metrics, email. A rollback whose own health check fails reports `failed` (the reference swallows it with `|| true`).

Drop: `*_pending_migrations`, `*_read_applied_migrations`, `*_rollback_decision`, `*_install_provisioning`, metrics and email renderers. Replace `*_fetch_latest_tag` with the status-aware fetch. Add rejected-version helpers (design in research Pattern 2: `cabinet_record_rejected_version`, `cabinet_clear_rejected_version`, `cabinet_is_rejected`, state file `/var/lib/cabinet-deploy/state/rejected`, temp-file-then-`mv`).

### `deploy/bin/cabinet-deploy` (installer, event-driven)

**Analog:** `ing-dashboard:deploy/bin/ledger-deploy` (252 lines). Keep skeleton:

```bash
set -euo pipefail
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# lib dir is ../lib in a checkout, /usr/local/lib/<app> once installed
DEPLOY_ROOT="${LEDGER_DEPLOY_ROOT:-}"          # relocatable root for tests
LOCK_PATH="${DEPLOY_ROOT}/run/ledger-deploy/deploy.lock"
STATE_DIR="${DEPLOY_ROOT}/var/lib/ledger-deploy/state"
RELEASES_DIR="${DEPLOY_ROOT}/opt/ledger/releases"
CURRENT_LINK="${DEPLOY_ROOT}/opt/ledger/current"
STRICT_SEMVER_TAG_REGEX='^v[0-9]+\.[0-9]+\.[0-9]+$'
```

Functions: `usage`, `require_root`, `load_configuration`, `acquire_lock`, `active_release_version`, `download_release_asset`, `cmd_verify`, `cmd_install`, `cmd_rollback`, `cmd_poll`, `main` with a `case` over `poll|install|rollback|verify|-h|--help`.

Required changes:
1. The file ends with an unconditional `main "$@"`. Guard it so tests can source the file: `if [ "${BASH_SOURCE[0]}" = "$0" ]; then main "$@"; fi`.
2. `cmd_poll` today (lines 183-216) uses `curl --fail` and turns any error into a die, and compares only against the active version. Replace with the status-aware fetch (research Pattern 3 sketch: `--dump-header`, `--write-out '%{http_code}'`, 200 parse, 404 quiet, 403/429 with `x-ratelimit-remaining: 0` or `retry-after` quiet, others die; log `x-ratelimit-remaining`), then the compare order: not newer than active -> up to date; not newer than rejected -> log skipping rolled-back release; else `cmd_install "$tag"`.
3. `cmd_install` writes the rejected marker on `rolled_back` and `failed` outcomes only, clears it on success; manual `install vX.Y.Z` ignores the marker.
4. Remove metrics calls and email.

### `deploy/systemd/cabinet.service` (config)

**Analog:** `ing-dashboard:deploy/systemd/ledger.service` (33 lines). Keep the whole hardening block as is:

```ini
[Service]
User=cabinet
Group=cabinet
WorkingDirectory=/opt/cabinet/current/app
ExecStart=/usr/bin/dotnet /opt/cabinet/current/app/Cabinet.Service.dll
EnvironmentFile=/etc/cabinet/cabinet.env
StateDirectory=cabinet
Restart=always
NoNewPrivileges=yes
PrivateTmp=yes
ProtectSystem=strict
ProtectHome=yes
ProtectKernelTunables=yes
ProtectKernelModules=yes
ProtectControlGroups=yes
RestrictNamespaces=yes
LockPersonality=yes
RestrictAddressFamilies=AF_UNIX AF_INET AF_INET6
CapabilityBoundingSet=
SystemCallArchitectures=native
```

Drop `After=postgresql.service` and `Wants=postgresql.service` (keep `After=network.target`). `RestrictAddressFamilies` stays as in the reference.

### `deploy/systemd/cabinet-deploy-poll.service` and `.timer` (config)

**Analog:** `ing-dashboard:deploy/systemd/ledger-deploy-poll.service` (24 lines) and `.timer`. Service: `ReadWritePaths=/opt/cabinet /var/lib/cabinet-deploy`, keep `RuntimeDirectory` for the lock. Timer reference is `OnBootSec=2min`, `OnUnitActiveSec=5min`, `RandomizedDelaySec=30`, `WantedBy=timers.target`; cabinet uses `3min`, `10min`, `120`.

### `deploy/nftables/cabinet.nft.in` (config)

**Analog:** `ing-dashboard:deploy/nftables/ledger.nft.in` (34 lines). Keep header, `flush ruleset`, `table inet cabinet_filter`, default-drop `input` and `forward`, accept `output`, loopback/established/invalid/ICMP rules. Replace the two app rules:

```
tcp dport 22 ip saddr { @CABINET_ADMIN_SSH_SOURCES@ } accept
tcp dport 5080 ip saddr @CABINET_TRAEFIK_IP@ accept
```

### `deploy/provision.sh` and `provision.d/*` (provisioning, batch)

**Analog:** `ing-dashboard:deploy/provision.sh` (425 lines) plus `provision.d/10-packages.sh`, `20-accounts.sh`, `40-services.sh`, `50-firewall.sh`. Keep: idempotent numbered modules; first run writes `/etc/cabinet/provision.conf` from the example and stops; second run applies; validators (ipv4, cidr, cidr-list, safe-value), `render_template`, `version_ge`, `key_fingerprint`, library-only mode (so tests source it). Shrink the allow-lists to `CABINET_TRAEFIK_IP`, `CABINET_ADMIN_SSH_SOURCES`, `CABINET_GITHUB_REPO`. `10-packages`: package list `ca-certificates curl gnupg jq unzip nftables unattended-upgrades tzdata` plus the runtime package and `gh`; keep the fingerprint-pinned GitHub CLI key handling and the `GH_CLI_MIN_VERSION` assertion. `20-accounts`: one `cabinet` user, `/opt/cabinet/releases` 755, `/etc/cabinet` root:cabinet 750, `/var/lib/cabinet-deploy` 700, env file rendered once and never overwritten. `40-services`: install bins, libs and units, render `deploy.conf`, enable the poll timer, start `cabinet.service` only when a release exists. Copy the GitHub CLI key fingerprint value from the reference `deploy/versions.env` at implementation time (do not repeat it in prose files; the generic scanner flags it outside `deploy/versions.env`).

### `deploy/traefik/cabinet.yml.example` (config)

**Analog:** `ing-dashboard:deploy/traefik/ledger.yml.example` (76 lines). One router with `Host(...)` placeholder (`cabinet.example.com`), the LAN/VPN `ipAllowList` middleware and the security-headers middleware, one service pointing at a documentation-range address (RFC 5737) port 5080. No real hostnames or ranges.

### `deploy/tests/*` (test, batch)

**Analogs:** same names under `ing-dashboard:deploy/tests/`. Harness shape to copy (from `ledger-deploy-logic-test.sh`):

```bash
export LEDGER_DEPLOY_ROOT; LEDGER_DEPLOY_ROOT="$(mktemp -d)"
trap 'rm -rf "${LEDGER_DEPLOY_ROOT}"' EXIT
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$LEDGER_DEPLOY_ROOT"
source "${REPO_ROOT}/deploy/lib/common.sh"
source "${REPO_ROOT}/deploy/lib/deploy.sh"
FAILURES=0
check() { # description expected actual -> PASS:/FAIL: line, increments FAILURES
```

`deploy/tests/lib/host-guard.sh` keep verbatim (PATH stand-ins for `systemctl` and `pkexec` that log and refuse; assert at the end that `host_guard_calls` is empty). Extend the installer logic test with: rejected-version record/read/clear, `is_rejected` below/equal/above, poll with latest equal to rejected installs nothing (stub `cmd_install`), poll with newer latest installs, poll status cases (200, 404, 403 with and without rate-limit signals, 5xx), install success clears marker, manual install ignores it. `verify-rejects-tampered-artifact-network-test.sh` and its public fixture: keep. `sandboxing-test.sh`: keep poll-unit assertions; replace the apikey section with the image-smoke wrapper properties. Drop backup, bank-key, grafana, postgresql, restore and selfcheck-logic tests.

### `deploy/bin/cabinet-selfcheck` (script, request-response)

**Analog:** `ing-dashboard:deploy/bin/ledger-selfcheck` (738 lines), as a model for check/report structure only; write a new script about a quarter the size: units and timer active, file modes, listeners (public on 5080, ops on loopback only), nftables policy, `/health` JSON version equals the `current` symlink version, no GitHub credential or runner on the host, image smoke. Image smoke is run via `systemd-run --pipe --wait --collect` as user `cabinet` with the hardening properties used by the reference's `deploy/bin/ledger-apikey` (`NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome`, `PrivateTmp`, `PrivateDevices`, kernel and cgroup protections, `RestrictNamespaces`, `LockPersonality`, empty `CapabilityBoundingSet`, `RestrictAddressFamilies=AF_UNIX`) and `--working-directory=/opt/cabinet/current/app` running `dotnet Cabinet.Service.dll image-smoke`. Pitfall to apply to every check: under `pipefail` never write `cmd | grep -q`; capture output in a variable then match.

### `.githooks/*` and `build/tests/githooks-test.sh` (hook, batch)

**Analog:** none for the hooks (design in research Pattern 6: denylist path `${CABINET_DENYLIST_FILE:-${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt}`, warn and exit 0 when absent; pre-commit scans added lines and staged names; commit-msg scans the message; pre-push scans unpushed commits). For the test, copy the self-contained shell harness shape of `ing-dashboard:build/tests/validate-release-tag-test.sh` (throwaway repos in a temp dir, `check`-style PASS/FAIL, non-zero exit on failure). Use only invented strings in the test.

### `build/check-github-settings.sh` (script, request-response)

**Analog:** `ing-dashboard:build/check-github-settings.sh` (226 lines). Keep the 11 checks and read-back style; add the main-ruleset check (active, `~DEFAULT_BRANCH`, rules `deletion`, `non_fast_forward`, `pull_request` with 0 required approvals and merge methods `merge` and `squash`, `required_status_checks` listing `build-test` and `lint`). Ruleset JSON is in the research Pattern 7.

### `docs/*.md`, `README.md`, `LICENSE` (docs)

**Analogs:** `ing-dashboard:docs/github-repository-settings.md` (apply plus read-back command per control), `deploy.md`, `releasing.md`, `lxc-setup.md`. Adapt; add main-ruleset section, rejected-version skipping and quiet poll statuses, precise Sigstore wording. New: `docs/development.md` (hook install, lint, privacy rules), `README.md`, `LICENSE` (MIT, `Copyright (c) 2026 cryptic96`). No planning references in any of them.

## Shared Patterns

### Script prologue and logging
**Source:** `ing-dashboard:deploy/bin/ledger-deploy` lines 8-27 and `deploy/lib/common.sh` lines 22-30.
**Apply to:** every shell script under `deploy/` and `build/`.
`set -euo pipefail`, `SCRIPT_DIR` resolution, library discovery with a checkout-relative or installed fallback, `*_log` to stderr with a UTC timestamp, `*_die` for fatal errors; shellcheck-clean (`-x`, `# shellcheck source=` hints).

### Relocatable root for offline tests
**Source:** `ing-dashboard:deploy/bin/ledger-deploy` (`DEPLOY_ROOT="${LEDGER_DEPLOY_ROOT:-}"`) and `deploy/tests/lib/host-guard.sh`.
**Apply to:** installer, libraries, all offline tests. Never touch the real host; never reach `systemctl` or polkit.

### Config loading without sourcing
**Source:** `ing-dashboard:deploy/lib/common.sh` `*_load_conf`.
**Apply to:** installer, provisioning, anything reading `deploy.conf` or `provision.conf`.

### Self-matching-proof lint patterns
**Source:** `ing-dashboard:build/lint/checks/10-repo-rules.sh` (bracketed character such as `\.plannin[g]/`, `printf` splits in self-test fixtures).
**Apply to:** every lint rule, `.gitignore`, `.gitleaks.toml`, docs. Nothing in the repo may contain a match for its own rules.

### SHA-pinned, least-privilege workflows
**Source:** `ing-dashboard:.github/workflows/ci.yml` and `release.yml`, `.github/zizmor.yml` (`unpinned-uses` -> `hash-pin`).
**Apply to:** both workflows and any later workflow. Pinned SHAs (verified in research): checkout `3d3c42e5aac5ba805825da76410c181273ba90b1` (v7.0.1), setup-dotnet `a98b56852c35b8e3190ac28c8c2271da59106c68` (v6.0.0), attest-build-provenance `4d101475d8b20a2381f78447822ac1eab6504dd8` (v4.2.2).

### C# conventions
**Source:** `ing-dashboard:Ledger.Service/Hosting/OpsEndpoint.cs`.
`///` XML doc on types and members only, no `//` comments, file-scoped namespaces, nullable enabled, warnings as errors, `Directory.Build.props` copied verbatim, `packages.lock.json` committed, `dotnet restore --locked-mode` in packaging.

### Privacy placeholders
Use `example.com`, `example.org` and RFC 5737 addresses (192.0.2.0/24, 198.51.100.0/24, 203.0.113.0/24) in every template, test and doc. Never copy hostnames, ranges, names, emails or local paths from the reference. Excerpts in this document are structural only.

## No Analog Found

| File | Role | Data flow | Reason |
|------|------|-----------|--------|
| `Cabinet.Domain/BuildInfo.cs` | model | transform | Not in reference; design in research Code Examples |
| `Cabinet.Repository/Images/ImageSmoke.cs` | utility | file-I/O | SkiaSharp is new (D-17); use the API calls listed in the research Open Questions note |
| `Cabinet.Service/Pages/*`, `wwwroot/css/site.css` | component | request-response | Reference has no Razor Pages; needs `_ViewImports.cshtml` with `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`, `asp-append-version="true"` on the stylesheet link, `MapStaticAssets` plus `MapRazorPages().WithStaticAssets()` |
| `.githooks/*` | hook | batch | No hooks in the reference; research Pattern 6 design |
| Rejected-version memory, status-aware poll fetch | installer logic | event-driven | Absent from reference installer; research Patterns 2 and 3 |
| `deploy/bin/cabinet-selfcheck` | script | request-response | Reference script is a size and structure model only |
| History scrub procedure, go-live commands | procedure | n/a | Operational steps, not code; research Patterns 5, 7, 8 |

## Metadata

**Analog search scope:** the reference checkout's root config, `.github/`, `build/`, `deploy/`, `docs/`, `Ledger.Service/`, `Ledger.IntegrationTests/Infrastructure/`, `Ledger.UnitTests/Configuration/`.
**Pattern extraction date:** 2026-10-04

# Phase 1: Repo, Guardrails & Walking-Skeleton Deploy - Research

**Researched:** 2026-10-04
**Domain:** Public-repo guardrails, attested pull-based release pipeline, LXC provisioning, minimal .NET 10 skeleton (port of the ing-dashboard deploy model)
**Confidence:** HIGH for the port inventory and the skeleton (prototyped and run this session); MEDIUM for GitHub go-live ordering (needs live read-back); LOW for owner infrastructure details that cannot be probed from here

<user_constraints>
## User Constraints (from CONTEXT.md)

### Locked Decisions

### Names & licence
- **D-01:** The repository is `games-cabinet` under the owner's personal GitHub account `cryptic96` (same account as ing-dashboard). The owner also asked about using their organization, but its name is their real name, which would put that name into the repo URL, every release download link and the attestation signer identity (`cryptic96/games-cabinet/.github/workflows/release.yml`). That conflicts with the no-names rule, so the personal account was chosen. — **Reversibility:** costly — transferring later changes the signer workflow identity pinned in the server's deploy config and every hard-coded repo slug in workflows, docs and scripts; GitHub redirects URLs but attestation verification would fail until the server config is updated.
- **D-02:** Short product naming, following ing-dashboard's `Ledger` pattern: `Cabinet.slnx`, `Cabinet.Domain`, `Cabinet.Repository`, `Cabinet.Service`, `Cabinet.UnitTests`, `Cabinet.IntegrationTests`. On the server: Linux user/group `cabinet`, `cabinet.service`, `cabinet-deploy-poll.service`/`.timer`, installer `cabinet-deploy`, paths `/opt/cabinet` (releases + `current` symlink), `/etc/cabinet` (env and deploy config), `/var/lib/cabinet` (systemd `StateDirectory`). — **Reversibility:** costly — namespaces, unit names, paths and provisioning scripts all carry the name, and renaming on a running LXC means migrating state directories.
- **D-03:** Licence is **MIT**, copyright line `Copyright (c) 2026 cryptic96` (GitHub handle, not a real name, consistent with the noreply commit identity and the no-personal-data rule). This also satisfies ImageSharp's split-licence condition. — **Reversibility:** one-way — code published under MIT stays MIT-licensed for anyone who obtained it.

### Privacy before first push
- **D-04:** `.planning/` stays tracked and goes public (GSD workflow and `commit_docs` unchanged), but before the first push the planning files are scrubbed of homelab specifics: the exact hardware model, local filesystem paths (refer to the reference project as "ing-dashboard" or by its GitHub slug, never by local path), names of neighbouring homelab services, and the pointer to the owner's private environment notes. Rewrite them to generic wording (e.g. "a low-power mini PC shared with other guests"). The same applies to `.claude/CLAUDE.md`. — **Reversibility:** one-way — once pushed, history is effectively permanent on GitHub.
- **D-05:** The 6 existing local commits are **rewritten in place** (filter-repo-style replace-text across every commit, keeping commits, dates and messages), not squashed. After rewriting, scan the **full history**, not just the tip, before the first push. All 6 commits already carry the GitHub noreply identity, so no identity rewrite is needed. — **Reversibility:** one-way after first push.
- **D-06:** A committed pre-commit/pre-push hook reads an owner-specific denylist from a file **outside the repo** (e.g. under `~/.config/games-cabinet/`), holding the real BGG username, domain, IP ranges and storage-location names, and blocks commits containing any of them. The denylist file itself is never committed. CI keeps the generic scanner rules copied from ing-dashboard (gitleaks plus private IPv4 ranges, email addresses other than noreply/example, token shapes). The hook must degrade gracefully (warn, not fail) when the denylist file is absent, so CI and fresh clones work.

### LXC base & LAN access
- **D-07:** New unprivileged **Ubuntu 24.04 LTS** LXC (`nesting=1` so systemd sandboxing works), same as ing-dashboard, so provisioning, the .NET runtime package and the `gh` install from GitHub's apt repository carry over unchanged.
- **D-08:** Starting size **1 core / 1 GB RAM / 8 GB disk**. Hard caps are tuned from real load in the hardening phase.
- **D-09:** Until go-public, the hello page is reached through a **LAN-only Traefik route**: internal hostname + TLS via the existing Traefik, with a LAN/VPN `ipAllowList`, exactly like ing-dashboard. Forwarded headers and the known-proxy setup are exercised now; go-public later means removing the allowlist and adding public DNS. The Traefik dynamic config lives on the Traefik container; the repo ships only a template with placeholders plus a documented step.
- **D-10:** Admin access is **SSH with key-only auth**; nftables allows SSH only from admin/VPN ranges held in the server-side provision config (never in the repo). App port accepted only from the Traefik source. The ops/health listener is loopback-only and never routed.

### Release rehearsal
- **D-11:** The rollback rehearsal uses a **real broken tag**: a release whose health check deliberately fails is tagged, approved and installed by the poller on the LXC, fails health and rolls back to the previous release; then a good follow-up release is shipped. The broken release stays in the public, immutable release list, labelled in its release notes as a rollback rehearsal. Because tags may only point at commits on `main`, the breaking change and its revert both land through pull requests. No test switch for forcing unhealthy is added to production code. — **Reversibility:** one-way — immutable releases cannot be deleted or re-tagged once published.
- **D-12:** The poller must **remember a rolled-back version and skip it** until a newer release is published. ing-dashboard's installer does not do this: after a rollback, every poll sees the broken release as newer than the active one and reinstalls it, failing and rolling back each cycle.
- **D-13:** Version numbering: **0.x until go-public**. The hello page is `v0.1.0`, the rehearsal releases follow as `0.1.x`, features bump the minor, and `v1.0.0` is cut when the site opens to the public.
- **D-14:** The poll timer runs **every 10 minutes with randomized jitter**, unauthenticated (no GitHub credentials on the server). Roughly 18 calls/hour from the shared public IP together with ing-dashboard's 5-minute poller, well under GitHub's 60/hour unauthenticated limit. A 404 from `releases/latest` before the first release, and a 403/429 from rate limiting, are logged quietly and never counted as a failure. Log `x-ratelimit-remaining`.
- **D-15:** GitHub repository settings are **applied by Claude**: during the go-live step, Claude presents the full list of `gh api` commands once, the owner approves the whole list in chat, Claude runs them, then runs the read-back check script. The settings guide in `docs/` still documents each control with an apply and a read-back command (as in ing-dashboard), so the settings stay reproducible. Settings: protected `main` ruleset (PR required, 0 required approvals for a solo owner, required status checks, no force-push, no deletion), `v*` tag ruleset restricted to admin, `deploy` environment with the owner as required reviewer (`prevent_self_review` false) and a tag-only deployment policy, outside-contributor workflow approval, read-only default workflow token, SHA-pinned actions enforcement, secret scanning + push protection, Dependabot, immutable releases, and zero registered self-hosted runners.
- **D-16:** Ordering constraints for the go-live step: create the repo **empty** (no GitHub-generated README/licence), push `main` first, then the milestone branch, open a PR so CI runs once, **then** add required status checks with the now-known check names. The `deploy` environment and its reviewer must exist and be read back **before the first tag**, or the first run auto-creates it unprotected and publishes without approval.

### Carried forward (locked at project level; not re-discussed)
- Mirror ing-dashboard's pull deploy: SHA-pinned CI and release workflows with `permissions: {}` at top level, strict-semver tag validation, tag must be on `main`, attested zip + sha256 + sigstore bundle on a **draft** release, `publish` job gated by the `deploy` environment re-verifying checksum and attestation. A root-owned installer with `poll` / `install` / `rollback` / `verify`, offline `gh attestation verify` with GitHub tokens unset and `--deny-self-hosted-runners`, attested commit confirmed on `main`, downgrade refusal, `releases/{version}` plus an atomic `current` symlink. Assert a `gh` version floor in provisioning.
- Remove everything database-related and monitoring-related from the reference: PostgreSQL, EF, efbundle, migration manifest and migration-aware rollback branch, backups and `age`, Grafana, Prometheus, node-exporter, Data Protection certificate. Rollback is always allowed. `release-manifest.json` shrinks to version + commit.
- `/health` lives only on a loopback ops Kestrel listener, reports the running version, and **never depends on BGG**; an empty or missing snapshot is healthy. The installer's health check is "`GET` loopback `/health` returns healthy and reports the expected version."
- Deploy outcomes are visible via the journal and `/health` only; deploy/sync failure email is deferred to v2.
- Framework-dependent `linux-x64` publish, ASP.NET Core runtime from the distribution archive, no trimming. `Directory.Build.props` and `global.json` copied from the reference (nullable, warnings-as-errors, deterministic, lock files, `dotnet restore --locked-mode`, Microsoft.Testing.Platform runner).
- The image-processing smoke test proves ImageSharp decodes, resizes and encodes WebP inside the real LXC.
- Repo conventions: no planning references outside `.planning/`, `///` XML doc comments only in C# (`/** */` doc blocks only in JS), synthetic-only fixtures, `example.com` placeholders, personal config only in the server env file.

### Claude's Discretion
- What the hello page shows (minimal; showing the running version is useful for rehearsal checks).
- The shape of the image smoke test (an installer selfcheck step, an app subcommand, or a test run on the LXC), as long as it runs inside the real LXC against the deployed release.
- Which ing-dashboard lint checks to port and how to extend them: planning references, C# `//` comments, `runs-on` (no self-hosted), gitleaks, shellcheck, zizmor/actionlint, script tests; add the JS comment rule.
- Number of kept releases, timer `OnBootSec`, health-check timeout, Kestrel ports.
- Inbound nftables rules beyond D-10.

### Deferred Ideas (OUT OF SCOPE)
- Outbound (egress) firewall allowlist for the LXC (GitHub, Sigstore, BGG API, image CDN only): consider in the hardening phase.
- Upgrading both this LXC and ing-dashboard's to Ubuntu 26.04 LTS together at a later point.
- Deploy and sync failure email: already tracked as a v2 requirement.
</user_constraints>

<phase_requirements>
## Phase Requirements

| ID | Description | Research Support |
|----|-------------|------------------|
| OPS-01 | Public GitHub repo with protected `main`, OSI licence, no personal data in code, docs, fixtures or git history | History rewrite procedure (filter-repo verified on a synthetic repo), denylist hook design, gitleaks baseline (no leaks with the ported generic config), main ruleset JSON (shape verified against a working reference ruleset), scrub inventory, go-live ordering |
| OPS-02 | Every PR runs build, tests, lint on GitHub-hosted runners | ci.yml port, lint framework port (docker-compose tool images, digests verified to resolve), pinned action SHAs verified against upstream tags, required-check names (`build-test`, `lint`) |
| OPS-03 | Semver tag builds an attested draft release, published only after approval in a protected deploy environment | release.yml port, tag validation script, `deploy` environment + tag policy commands (verified read-back shape), immutable releases, ordering lessons from the reference go-live |
| OPS-04 | Server pulls newest published release, verifies attestation, installs, health-checks, rolls back; no self-hosted runner | Installer port inventory, rejected-version design, poll HTTP-status design, health-contract (JSON with version), rehearsal sequence, host-guard test pattern |
| OPS-05 | LXC creatable and provisionable from documented repeatable scripts | provision.sh module port (keep 10/20/40/50), versions.env pins re-verified, nftables/Traefik templates, selfcheck + image smoke design, owner checkpoint list |
</phase_requirements>

## Project Constraints (from CLAUDE.md)

Treated with the same authority as locked decisions. [VERIFIED: .claude/CLAUDE.md "Hard rules"]

- **No planning references outside `.planning/`**: no requirement keys, decision IDs, phase/plan/wave/milestone numbers, or planning document names in README, `docs/`, code, `///` comments, string literals, log/exception messages, test names, scripts, config or workflow files. Git commit messages are the only exception. The ported lint rule must be extended with this project's requirement prefixes (see Port Inventory).
- **Comments: `///` only** in C#. No `//` comments. For JS the equivalent is `/** */` blocks only (CONTEXT carries this forward).
- **Public repository, no personal data anywhere**: no names or emails (other than GitHub noreply), BGG usernames/tokens, real domains/hostnames, homelab IPs or ranges, real storage-location names, recorded BGG responses. Use `example.com`/`example.org` and RFC 5737 documentation addresses. Test data synthetic.
- **Never commit to `main`**; work on `milestone/v<N>-<name>` or `feature/<short-description>`; lands via pull request. The one exception that the go-live step needs: the initial empty-history `main` push happens before the protecting ruleset exists (D-16).
- **BGG API etiquette**: no BGG calls in this phase; the token must never be sent anywhere but the BGG host (relevant later).
- **Commit identity**: cryptic96 GitHub noreply identity in public repos (project memory).
- Commit and PR attribution: end commit messages with the attribution line supplied by the harness.

## Summary

Phase 1 is mostly a mechanical port of ing-dashboard's pipeline (CI, release, lint, packaging, installer, provisioning, systemd units, nftables and Traefik templates, and their logic tests) with `ledger` renamed to `cabinet` and everything database-, monitoring-, backup- and mail-related deleted. The skeleton, the dual-listener health contract, the ImageSharp round trip, the Razor Pages hello page with fingerprinted static assets, and `dotnet test --solution` under Microsoft.Testing.Platform were all prototyped and run in a scratch solution this session (all green; versions and outputs quoted below). The port inventory in this document tells the planner, file by file, what to keep, adapt or drop.

Five findings change the plan relative to the project-level research and the context document, and the planner should treat them as first-class tasks:

1. **ImageSharp 4.x refuses to build in Release without a Six Labors licence key.** Verified: `dotnet build -c Release` and `dotnet publish -c Release` of a project referencing `SixLabors.ImageSharp` 4.1.2 fail with `error : No Six Labors license found`; Debug builds only warn (and the warning does not trip warnings-as-errors). The release packaging script publishes in Release, so a 4.x reference breaks the release pipeline until the owner applies for a free community key and the build is wired to supply it. `SixLabors.ImageSharp` 3.1.12 builds, publishes and runs the identical WebP round trip with no key (verified). Recommendation: pin 3.1.12 for this phase, apply for the community key in parallel as an owner prerequisite, and revisit when the image pipeline is built. This needs owner confirmation (Open Question 1).
2. **The local history has 8 commits, not 6** (7 branch commits beyond `main`'s single initial commit, verified with `git rev-list`). The rewrite and the scans must run over all refs, not a fixed count.
3. **The reference poller turns 404/403/429 into failures and has no memory of a rolled-back version.** Both D-12 and D-14 need real changes in the ported installer; designs are given below with the exact state file and the decision points.
4. **The owner's GitHub profile has a display name set that differs from the login** (checked as booleans only through the API; the value was not recorded). The reference project's go-live found that a web-UI merge commit carries the profile name and email. For a pseudonymous repo this must be resolved before the first merge on GitHub (Pitfall 3).
5. **A conditional-request (ETag) optimisation does not help the unauthenticated poller**: GitHub only exempts 304 responses from the primary rate limit when the request is authorised. Do not add ETag plumbing.

**Primary recommendation:** Execute in this order: (a) owner-gated privacy gate first (denylist + hook, scrub, history rewrite, full-history scan), (b) skeleton + lint/guardrail port + release pipeline port + deploy tree port in parallel waves, all verified offline with the ported logic tests, (c) one go-live checkpoint where Claude applies the approved `gh api` list and the owner performs the Proxmox, Traefik, DNS and approval steps, (d) the three-release rehearsal (good hello page, deliberately broken release, good follow-up) with journal evidence.

## Architectural Responsibility Map

| Capability | Primary Tier | Secondary Tier | Rationale |
|------------|-------------|----------------|-----------|
| Build, test, lint on PR | CI/CD (GitHub-hosted runners) | Developer workstation (same `build/lint.sh`) | Untrusted PR code may only run on GitHub-hosted runners |
| Attestation, draft release, approval gate | CI/CD + GitHub repo settings (`deploy` environment) | — | Approval is a GitHub-side control; nothing on the LXC takes part |
| Repo protections (rulesets, env, immutability, scanning) | GitHub repo settings | Read-back script on workstation | Cannot be expressed in repo files; applied and verified with `gh api` |
| Artifact download, attestation verify, install, rollback | Host/OS (root installer on LXC) | — | Must run on the server with no GitHub credential, before unpacking |
| Poll scheduling and rejected-version memory | Host/OS (systemd timer + installer state dir) | — | State lives in the root-owned deploy state directory |
| Health contract (`/health` JSON with version) | API/Backend (ops Kestrel listener, loopback) | Host/OS (installer reads it) | Version must come from the running binary, not the symlink |
| Hello page | Frontend Server (Razor Pages on public listener) | CDN/Static (`MapStaticAssets`) | Exercises the real page/asset pipeline through the symlinked release path |
| LAN-only exposure | Reverse proxy (Traefik allowlist) | Host/OS (nftables admits app port only from proxy) | Proxy decides what is routed; firewall is defence in depth |
| Image smoke test | Host/OS (sandboxed `systemd-run` of the deployed app) | API/Backend (app subcommand) | Must run inside the real LXC against the deployed release |
| Privacy gate (denylist, scrub, history scan) | Developer workstation | CI (generic gitleaks rules) | The denylist holds real values, so it can never be in CI or the repo |

## Standard Stack

### Core
| Library / Tool | Version | Purpose | Why Standard |
|----------------|---------|---------|--------------|
| .NET SDK | 10.0.112 (`rollForward: latestFeature`, test runner `Microsoft.Testing.Platform`) | Build | Copy the reference `global.json` verbatim. [VERIFIED: ing-dashboard `global.json`: `"version": "10.0.112"`, `"rollForward": "latestFeature"`, `"runner": "Microsoft.Testing.Platform"`; `dotnet --version` here prints 10.0.112] |
| ASP.NET Core runtime | 10.0.12 via apt package `aspnetcore-runtime-10.0` | Run the framework-dependent publish on the LXC | [VERIFIED: archive.ubuntu.com `noble-updates/main` and `noble-security/main` list `Version: 10.0.12-0ubuntu1~24.04.1`; local runtime list also shows 10.0.12] |
| `SixLabors.ImageSharp` | **3.1.12** (recommended) or 4.1.2 (needs licence key) | Decode, resize, encode WebP (smoke test now, image pipeline later) | Managed, no native assets (published output contained only `SixLabors.ImageSharp.dll`). 3.1.12: [VERIFIED: NuGet, published 2025-10-29; local Release publish and run succeeded; `dotnet list package --vulnerable` reported none]. 4.1.2: [VERIFIED: NuGet, published 2026-09-14; Release build fails without a key] |
| `xunit.v3` | 4.0.1 | Test framework | [VERIFIED: NuGet latest stable 4.0.1; prototype ran 5 tests green under MTP] |
| `FluentAssertions` | 8.11.0 | Assertions | [VERIFIED: NuGet latest stable 8.11.0] Xceed community licence applies to v8 (free for open source), noted in project stack research |
| `Microsoft.AspNetCore.Mvc.Testing` | 10.0.12 | Integration tests on real Kestrel sockets | [VERIFIED: NuGet latest 10.0.12; prototype integration test passed] |
| GitHub CLI `gh` | apt `stable`: 2.102.0; floor `GH_CLI_MIN_VERSION=2.49.0` | Offline `gh attestation verify --bundle` on the LXC | [VERIFIED: cli.github.com apt index lists `Version: 2.102.0`; ing-dashboard `deploy/versions.env:34` `GH_CLI_MIN_VERSION=2.49.0`] |

### Supporting
| Library / Tool | Version | Purpose | When to Use |
|----------------|---------|---------|-------------|
| gitleaks (container) | v8.30.1 pinned by digest | Full-history and tree secret scan in lint | Always; image digest resolves (`docker manifest inspect` OK) |
| zizmor (container) | 1.30.1 pinned by digest | Workflow security audit | Always |
| actionlint (container) | 1.7.12 pinned by digest | Workflow syntax and expression lint | Always |
| shellcheck (container) | v0.11.0 pinned by digest | Shell lint, `-x` | Always |
| `actions/checkout` | v7.0.1 = `3d3c42e5aac5ba805825da76410c181273ba90b1` | CI/release checkout | [VERIFIED: `git ls-remote` tag SHA matches; GitHub latest release is v7.0.1] |
| `actions/setup-dotnet` | v6.0.0 = `a98b56852c35b8e3190ac28c8c2271da59106c68` | SDK from `global.json` | [VERIFIED: tag SHA matches; latest is v6.0.0] |
| `actions/attest-build-provenance` | v4.2.2 = `4d101475d8b20a2381f78447822ac1eab6504dd8` | Build provenance attestation | [VERIFIED: tag SHA matches; latest is v4.2.2] |
| `git-filter-repo` | 2.47.0-3 (Ubuntu apt candidate) or the upstream single file | Replace-text history rewrite | Privacy gate only; not installed locally yet |

Omit for now (add when first used): `NSubstitute`, `Microsoft.Extensions.TimeProvider.Testing`, `Microsoft.Extensions.Http.Resilience`, Playwright, MockHttp. The skeleton needs none of them.

### Alternatives Considered
| Instead of | Could Use | Tradeoff |
|------------|-----------|----------|
| ImageSharp 3.1.12 | ImageSharp 4.1.2 + free community key | Current major, but needs the owner to apply, a secret wired into release and CI builds (and Dependabot's separate secret store), and fork PRs would fail Release steps. 3.x is not guaranteed future security fixes (last 3.x release is 2025-10) |
| ImageSharp | SkiaSharp 4.153.1 + NoDependencies Linux assets (MIT, native) | No key, actively maintained, but native assets in the release and a libc compatibility check; contradicts the locked "ImageSharp smoke test" wording |
| `git filter-repo` | `git filter-branch --tree-filter` | Built in (no install) and fine for 8 commits, but deprecated, slower and leaves `refs/original` to clean up |
| 10-minute API poll | Poll the non-API `https://github.com/{repo}/releases/latest` redirect | Avoids the API rate limit entirely, but the decision is locked (D-14); mention only |

**Installation (dev workstation, privacy gate only):**
```bash
sudo apt install git-filter-repo
```

**Version verification performed this session:**
```bash
curl -s https://api.nuget.org/v3-flatcontainer/sixlabors.imagesharp/index.json   # 3.1.12, 4.0.0, 4.1.0, 4.1.1, 4.1.2
curl -s https://api.nuget.org/v3-flatcontainer/xunit.v3/index.json               # latest stable 4.0.1
apt-cache policy git-filter-repo                                                   # Candidate: 2.47.0-3
```

## Package Legitimacy Audit

The `package-legitimacy` seam supports only npm, pypi and crates (`Usage: gsd-tools package-legitimacy check --ecosystem <npm|pypi|crates>`), so NuGet packages were audited by hand through the NuGet search API (verified-prefix owner, downloads, repository). All are well-known, prefix-reserved packages already used by the reference project. None run install scripts (NuGet packages do not execute postinstall scripts; ImageSharp ships an MSBuild targets file, inspected: it only runs the licence-validation task discussed above).

| Package | Registry | Age | Downloads | Source Repo | Verdict | Disposition |
|---------|----------|-----|-----------|-------------|---------|-------------|
| SixLabors.ImageSharp | NuGet | years (4.1.2 on 2026-09-14) | ~316M total | github.com/SixLabors/ImageSharp | OK (verified owner `sixlabors`) | Approved; pin 3.1.12 unless the owner opts into 4.x |
| xunit.v3 | NuGet | years | ~50M | xunit org | OK (verified owner `xunit`) | Approved |
| FluentAssertions | NuGet | years | ~780M | fluentassertions / Xceed | OK (verified) | Approved |
| Microsoft.AspNetCore.Mvc.Testing | NuGet | years | ~402M | Microsoft | OK (verified Microsoft owners) | Approved |

**Packages removed due to SLOP verdict:** none
**Packages flagged as suspicious:** none
Tool downloaded for research only (not a project dependency): upstream `git-filter-repo` single file from the official `newren/git-filter-repo` repository, run against a synthetic scratch repo.

## Architecture Patterns

### System Architecture Diagram

```
 Developer workstation                          GitHub (public repo, hosted runners only)
 +-----------------------------+   push/PR    +----------------------------------------------+
 | .githooks (denylist outside | -----------> | ci.yml: build-test (package + dotnet test)   |
 | repo, warn if absent)       |              |         lint (docker tools, gitleaks --all)  |
 | build/lint.sh               |              +----------------------------------------------+
 +-----------------------------+                                 | merge via PR (main protected)
                                                                  v
        owner pushes tag vX.Y.Z (tag ruleset: admin only) -> release.yml
        validate-release-tag (strict semver, commit on main)
              -> package-release.sh (restore --locked-mode, publish linux-x64, manifest {version, commit}, zip, sha256)
              -> dotnet test -> attest-build-provenance -> sigstore bundle copied beside zip
              -> DRAFT release (zip, sha256, sigstore.json)
              -> publish job, environment "deploy" (required reviewer = owner)  <-- human approval
              -> re-verify checksum + attestation -> publish (immutable)
                                                                  |
                       public release download URLs (no credential) |
                                                                  v
 LXC (unprivileged Ubuntu 24.04, nesting=1)   Proxmox host: owner runs `pct create`
 +----------------------------------------------------------------------------------+
 | cabinet-deploy-poll.timer (10 min + jitter) -> cabinet-deploy poll               |
 |   GET api.github.com releases/latest (unauthenticated, log x-ratelimit-remaining)|
 |   404 / rate-limited -> quiet exit 0 ; latest <= active -> exit 0                |
 |   latest <= rejected -> log "skipping rolled-back release", exit 0               |
 |   else install: download zip+bundle -> gh attestation verify (tokens unset,      |
 |        --deny-self-hosted-runners) -> attested commit on main (compare API)      |
 |        -> unzip to releases/<ver> -> atomic `current` symlink -> restart         |
 |        -> GET 127.0.0.1 ops /health : status Healthy AND version == expected     |
 |        fail -> reactivate previous, write `rejected` state ; ok -> clear `rejected`|
 | cabinet.service: public listener 0.0.0.0:5080 (nft: Traefik only), ops 127.0.0.1:5081|
 +----------------------------------------------------------------------------------+
        ^ LAN/VPN client -> Traefik (TLS, ipAllowList) -> 5080 hello page
```

### Recommended Project Structure
```
games-cabinet/
├── Cabinet.slnx
├── Directory.Build.props, global.json, LICENSE (MIT), README.md, .gitignore, .gitleaks.toml
├── .githooks/                  pre-commit, commit-msg, pre-push (denylist from outside the repo)
├── .github/workflows/          ci.yml, release.yml ; dependabot.yml, zizmor.yml
├── Cabinet.Domain/             BuildInfo (pure)
├── Cabinet.Repository/         Images/ImageSmoke (only ImageSharp consumer)
├── Cabinet.Service/            Program.cs, Hosting/OpsEndpoint.cs, Pages/Index, wwwroot, appsettings*.json
├── Cabinet.UnitTests/ , Cabinet.IntegrationTests/
├── build/                      lint.sh, lint/{compose.yaml,checks,fixtures}, package-release.sh,
│                               validate-release-tag.sh, verify-published-release.sh,
│                               check-github-settings.sh, tests/
├── deploy/                     provision.sh, provision.d/, bin/{cabinet-deploy,cabinet-selfcheck},
│                               lib/{common.sh,deploy.sh}, systemd/, nftables/, traefik/, *.example,
│                               versions.env, tests/ (+fixtures, lib/host-guard.sh)
└── docs/                       deploy.md, releasing.md, lxc-setup.md, github-repository-settings.md, development.md
```

### Port Inventory (ing-dashboard -> games-cabinet)

Rename map: `ledger`->`cabinet`, `LEDGER_`->`CABINET_`, `Ledger.`->`Cabinet.`, table `ledger_filter`->`cabinet_filter`, `/opt/ledger`->`/opt/cabinet`, `/var/lib/ledger-deploy`->`/var/lib/cabinet-deploy`, `/etc/ledger`->`/etc/cabinet`. Line counts are from the reference. [VERIFIED: files read this session]

| Reference file (ing-dashboard) | Action | Cabinet target | What changes |
|---|---|---|---|
| `Directory.Build.props` | Keep verbatim | same | none |
| `global.json` | Keep verbatim | same | none |
| `.config/dotnet-tools.json` | Drop | — | only `dotnet-ef` |
| `.gitignore` | Adapt | same | existing file already has the planning-cache glob written as `.plannin[g]/research/.cache/`; add `.env`, `*.env`, `!.env.example`, `!deploy/tests/fixtures/*.env`, `!deploy/versions.env`, `bin/`, `obj/`, `artifacts/`, `TestResults/`, `.vs/`, `.idea/`, `*.user`, `!deploy/bin/` |
| `.gitleaks.toml` | Adapt | same | drop the Dutch IBAN rule and the three `generic-api-key` allowlists tied to reference docs; keep `private-ipv4`, `email-address`, the versions fingerprint allowlist, fixtures/build-output allowlists (`Ledger\.` -> `Cabinet\.`). Optional extra rule for internal hostname suffixes (see Pitfall 12). Baseline: the generic rules over the current 7 non-empty commits found no leaks |
| `Ledger.slnx` | Adapt | `Cabinet.slnx` | no Dashboards project |
| `Ledger.Service/Program.cs` | Rewrite small | `Cabinet.Service/Program.cs` | drop repository/ingestion/auth/DataProtection/Prometheus; keep ForwardedHeaders + KnownProxies, `UseHealthChecks("/health", opsPort, ...)`, `AddSystemdConsole` in Production, `public partial class Program`; add `image-smoke` subcommand before the host is built |
| `Ledger.Service/Hosting/OpsEndpoint.cs` (+ `OpsEndpointTests`) | Keep, rename | same | loopback-only guard on `Kestrel:Endpoints:Ops:Url` |
| `Ledger.Service/Hosting/ProductionConfigurationValidator.cs` | Drop | — | all keys are ledger-specific |
| `Ledger.Service/appsettings.json` | Adapt | same | keep the two listeners (see Code Examples), drop EF log filter; `appsettings.Production.json` drops the connection string |
| `Ledger.IntegrationTests/Infrastructure/LedgerWebApplicationFactory.cs` | Adapt | `CabinetWebApplicationFactory` | drop DB/cert plumbing; keep the two-host trick (`CreateHost` builds a TestServer host and a real Kestrel host) and the two free loopback ports. Prototype of the trimmed class passed |
| `Ledger.UnitTests/Configuration/CommittedConfigurationTests.cs` | Keep, rename | same | secret-shaped value scan of committed `appsettings*.json` |
| Domain/Repository/Ingestion/Auth/Metrics/Health/Cli/Security/Dashboards code, EF migrations | Drop | — | not part of the skeleton |
| `.github/workflows/ci.yml` (77 lines) | Adapt | same | drop `services.postgres`, `ConnectionStrings__TestAdmin`, `LEDGER_EFBUNDLE`; sln name; `LEDGER_LINT_NETWORK` -> `CABINET_LINT_NETWORK`. Job ids stay `build-test` and `lint` (these are the required-check names). Triggers `push: {}` and `pull_request: branches: [main]` |
| `.github/workflows/release.yml` (152 lines) | Adapt | same | same removals; asset names `cabinet-<version>.zip`, `.sha256`, `.zip.sigstore.json`; keep `permissions: {}` at top, per-job minimal permissions, `--draft --verify-tag`, `publish` job `environment: deploy`, re-verify with `--signer-workflow` and `--deny-self-hosted-runners` |
| `.github/dependabot.yml` | Adapt | same | keep github-actions, nuget, docker-compose (`/build/lint`); drop the Prometheus ignore; add an ignore for SixLabors.ImageSharp major updates while pinned to 3.x |
| `.github/zizmor.yml` | Keep verbatim | same | `unpinned-uses` -> `hash-pin` |
| `build/lint.sh` (94 lines) | Adapt | same | rename env vars; **make `EXTRA_MOUNT` quote-safe**: it builds an unquoted `-v $GIT_COMMON_DIR_ABS:...` string, and this checkout's directory name contains a space, so a linked-worktree run (GSD executors use worktrees) would split the path |
| `build/lint/compose.yaml` | Adapt | same | drop `promtool` and `grafana` services; keep actionlint, zizmor, shellcheck, gitleaks (digests verified to resolve) |
| `build/lint/checks/10-repo-rules.sh` (196 lines) | Adapt | same | requirement-key pattern prefixes become the ones used here (CAB, SYNC, EXP, DET, FILT, LOC, IMG, OWN, I18N, A11Y, OPS, SEC, NIGHT, SHARE, CABX, OPSX); keep decision-id, phase-word, planning-filename and planning-dir patterns with the bracket/`printf` split tricks so the script never matches itself; drop the `/Migrations/` exclusion; **add the JS rule** (`//` outside `/** */` in `*.js`/`*.mjs`, URLs excluded via the same `(^|[^/:])//([^/]|$)` shape) with self-tests; add a LICENSE presence/MIT check |
| `build/lint/checks/20-workflows.sh` (170) | Keep verbatim | same | self-tests use the four fixtures |
| `build/lint/fixtures/*.yml` (4) | Keep verbatim | same | |
| `build/lint/checks/30-shell.sh` (74) | Adapt | same | candidate list adds `.githooks/*`; names otherwise the same |
| `build/lint/checks/40-secrets.sh` (208) | Adapt | same | self-tests generate Dutch IBAN values; drop that part, keep token/private-IP/email/shallow-clone tests. Add a self-test for any new custom rule |
| `build/lint/checks/50-script-tests.sh` (78) | Adapt | same | env var rename; globs `deploy/tests/*-test.sh build/tests/*-test.sh`; `*-network-test.sh` skipped unless the network variable is 1 |
| `build/lint/checks/60-observability.sh` (346) | Drop | — | Grafana/Prometheus only |
| `build/package-release.sh` (114) | Adapt | same | delete `dotnet tool restore`, `dotnet ef migrations bundle`, `MIGRATIONS_JSON`; manifest is `jq -n --arg version ... --arg commit ... '{version: $version, commit: $commit}'`; keep strict-semver and 40-hex checks, `dotnet restore --locked-mode`, `dotnet publish ... -r linux-x64 --self-contained false -p:Version -p:SourceRevisionId -p:ContinuousIntegrationBuild=true`, tar copy of `deploy/` excluding `./tests`, zip + sha256 |
| `build/validate-release-tag.sh` (41) | Keep verbatim | same | strict regex `^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$`, tag commit must be reachable from `origin/main` |
| `build/tests/validate-release-tag-test.sh` (139) | Keep | same | |
| `build/verify-published-release.sh` (110) | Adapt | same | asset names; sources the installer libs, so it follows the installer renames |
| `build/check-github-settings.sh` (226) | Adapt + extend | same | keep the 11 checks; **add** main-ruleset check (active, `~DEFAULT_BRANCH`, rules include `deletion`, `non_fast_forward`, `pull_request` with `required_approving_review_count == 0`, `required_status_checks` listing the two check names) |
| `deploy/bin/ledger-deploy` (252) | Adapt + extend | `deploy/bin/cabinet-deploy` | see D-12 and D-14 patterns; add a source guard so tests can source it |
| `deploy/lib/common.sh` (186) | Adapt | same | drop textfile-metrics and msmtp email helpers; keep `*_log`, `*_die`, safe `*_load_conf` (allow-list: `CABINET_GITHUB_REPO`, `CABINET_SIGNER_WORKFLOW`, `CABINET_KEEP_RELEASES`, `CABINET_HEALTH_TIMEOUT_SECONDS`, `CABINET_OPS_URL`), `*_semver_gt` |
| `deploy/lib/deploy.sh` (605) | Adapt heavily | same | drop migrations, provisioning install, metrics, email, `*_rollback_decision`; keep `verify_attestation`, `commit_on_branch`, `fetch_latest_tag` (replace with the status-aware fetch), `activate_release`, `prune_releases`; health wait parses JSON; add rejected-version functions |
| `deploy/bin/ledger-selfcheck` (738) | Replace with trimmed new script | `deploy/bin/cabinet-selfcheck` | about a quarter of the size: services/timer, file modes, listeners, firewall, `/health` JSON vs `current` symlink version, no GitHub credential/runner on the host, image smoke |
| `deploy/bin/ledger-apikey`, `-backup`, `-bank-key`, `-restore`, `lib/backup.sh` | Drop | — | the `systemd-run` sandbox property list in `ledger-apikey` is the model for the image-smoke wrapper |
| `deploy/provision.sh` (425) | Adapt | same | allow-lists shrink to `CABINET_TRAEFIK_IP`, `CABINET_ADMIN_SSH_SOURCES`, `CABINET_GITHUB_REPO`; drop email/hostport/age validators; keep ipv4/cidr/cidr-list/safe-value validators, `render_template`, `version_ge`, `key_fingerprint`, library-only mode |
| `deploy/provision.d/10-packages.sh` (141) | Adapt | same | package list: `ca-certificates curl gnupg jq unzip nftables unattended-upgrades tzdata` + `$DOTNET_RUNTIME_PACKAGE` + `gh`; keep fingerprint-pinned GitHub CLI key, expired-key handling, `gh` version floor assert and the unused-MTA purge; drop PGDG, Grafana, Prometheus, `age`, `msmtp`, node-exporter |
| `deploy/provision.d/20-accounts.sh` (120) | Adapt | same | one `cabinet` user, dirs (`/opt/cabinet/releases` 755, `/etc/cabinet` root:cabinet 750, `/var/lib/cabinet-deploy` 700), env file renderer with `ASPNETCORE_ENVIRONMENT=Production` and `ReverseProxy__KnownProxies__0`; drop the certificate, migrator/backup users |
| `deploy/provision.d/30-postgresql.sh`, `60-grafana-accounts.sh` | Drop | — | |
| `deploy/provision.d/40-services.sh` (220) | Adapt | same | install bins/libs/units, render `deploy.conf`, unattended-upgrades, enable the poll timer, enable `cabinet.service` (start only when a release exists); drop msmtp/grafana/prometheus/backup parts |
| `deploy/provision.d/50-firewall.sh` (43) | Adapt | same | table and template rename |
| `deploy/systemd/ledger.service` (33) | Adapt | `cabinet.service` | drop postgres `After/Wants`; keep hardening block and `StateDirectory`; paths per D-02 |
| `deploy/systemd/ledger-deploy-poll.service` (24) | Adapt | same | `ReadWritePaths=/opt/cabinet /var/lib/cabinet-deploy`; keep `RuntimeDirectory` for the lock |
| `deploy/systemd/ledger-deploy-poll.timer` (10) | Adapt | same | 10 min + jitter (see D-14 pattern) |
| other systemd units, grafana drop-in | Drop | — | |
| `deploy/nftables/ledger.nft.in` (34) | Adapt | `cabinet.nft.in` | single app port rule (see Code Examples) |
| `deploy/traefik/ledger.yml.example` (76) | Adapt | `cabinet.yml.example` | one router `Host(...)` (no `PathPrefix`), same allowlist + security-headers middlewares, one service, placeholders from RFC 5737 / `example.com` |
| `deploy/deploy.conf.example`, `provision.conf.example`, `ledger.env.example` | Adapt | | drop notify/textfile/SMTP/Grafana/API-domain keys |
| `deploy/versions.env` | Adapt | same | keep `DOTNET_RUNTIME_PACKAGE=aspnetcore-runtime-10.0`, `GH_CLI_KEY_URL`, `GH_CLI_KEY_FINGERPRINT` (copy the 40-hex value from the reference file; not repeated here because the generic gitleaks rule flags it outside `deploy/versions.env`), `GH_CLI_MIN_VERSION=2.49.0` (fingerprint re-verified this session: the key file holds one expired and one valid primary key, and the valid one matches the reference pin); drop PG, Grafana, Prometheus pins |
| `deploy/tests/ledger-deploy-logic-test.sh` (191) | Adapt + extend | `cabinet-deploy-logic-test.sh` | drop migration/metrics/email cases; keep semver, activate, prune, rollback-reachability, textless host-guard assertions; add rejected-version and poll-status cases |
| `deploy/tests/provision-logic-test.sh` (226), `render-templates-test.sh` (235) | Adapt | same | drop the email/age/SMTP/grafana cases; keep parser, validators, key-fingerprint, template-rendering cases |
| `deploy/tests/sandboxing-test.sh` (125) | Adapt | same | keep the poll-unit directive and write-path assertions; replace the apikey section with the image-smoke wrapper properties |
| `deploy/tests/versions-network-test.sh` (309) | Adapt | same | validate only the remaining pins |
| `deploy/tests/verify-rejects-tampered-artifact-network-test.sh` (187) + `fixtures/public-attested-artifact.*` | Keep | same | the fixture is an unrelated public GitHub CLI release artifact and its bundle, so it carries no project data |
| `deploy/tests/lib/host-guard.sh` (29) | Keep verbatim | same | PATH stand-ins that record and refuse `systemctl` and `pkexec` |
| other `deploy/tests/*` (backup, bank-key, grafana, postgresql, restore, selfcheck-logic) | Drop | — | |
| `docs/github-repository-settings.md` (244) | Adapt + extend | same | add the main ruleset section with apply and read-back; keep the other 9 sections |
| `docs/lxc-setup.md` (276) | Rewrite short | same | steps: `pct create`, get scripts (needs `git`), provision twice, Traefik file, DNS, repo settings, first release, selfcheck |
| `docs/deploy.md` (105), `docs/releasing.md` (90) | Adapt | same | describe rejected-version skipping and quiet poll statuses; correct the "fully offline" wording (Pitfall 13) |
| `docs/monitoring.md`, `backup-restore.md`, `rest-api.md`, `bank-link.http` | Drop | — | |
| new | Add | `docs/development.md`, `README.md`, `LICENSE` | hook installation, lint, privacy rules, MIT text with `Copyright (c) 2026 cryptic96` |

### Pattern 1: Skeleton with dual Kestrel listeners and a version-reporting health endpoint
**What:** public listener on 5080, loopback ops listener on 5081; `/health` is mapped only on the ops port; the JSON body carries status, version and commit parsed from the assembly's informational version.
**When to use:** the whole phase; this is what the installer polls.
**Verified this session:** prototype published with `-p:Version=0.1.0 -p:SourceRevisionId=0123...`, run from a symlinked release path with `ASPNETCORE_ENVIRONMENT=Production`: ops `/health` returned `{"status":"Healthy","version":"0.1.0","commit":"0123456789abcdef0123456789abcdef01234567"}`, public `/health` returned 404, `/` rendered the page with a fingerprinted CSS href (`/css/site.<hash>.css`, HTTP 200). Tests: unit `BuildInfo.Parse` theory and `ImageSmoke.Run`, integration `Health_is_served_on_ops_port_only` (real sockets via the two-host factory), `dotnet test --solution Cabinet.slnx` all green.

Ports and config reused from the reference: `"Url": "http://0.0.0.0:5080"` and `"Url": "http://127.0.0.1:5081"` [VERIFIED: ing-dashboard `Ledger.Service/appsettings.json:5,8`]. Using `Web` and `Ops` as endpoint names is the planner's choice.

Notes the planner must carry into tasks:
- Version parsing must be a pure, unit-tested function. The reference's go-live found MSBuild puts `SourceRevisionId` in the informational version (`0.1.0+<sha>`), not in assembly metadata [CITED: ing-dashboard 01-12 summary, "Build info reported commit `unknown`"]; the prototype confirms the `+` form.
- Health must never read the snapshot, BGG or the state directory. An empty or missing state is healthy.
- Razor Pages needs `_ViewImports.cshtml` with `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers` and a stylesheet link using `~/css/site.css` with `asp-append-version="true"`; `@Assets[...]` is a Razor Components feature and fails in a page (`CS0103`, hit in the prototype).
- The skeleton must not carry a switch that forces unhealthy (D-11).

### Pattern 2: Rejected-version memory (D-12), design
**What:** a one-line state file in the installer's root-owned state directory records the highest version that failed its health check and was rolled back. Poll compares against both the active and the rejected version.
**Where the state lives:** `${STATE_DIR}/rejected` with `STATE_DIR="${DEPLOY_ROOT}/var/lib/ledger-deploy/state"` in the reference [VERIFIED: ing-dashboard `deploy/bin/ledger-deploy:26`], i.e. `/var/lib/cabinet-deploy/state/rejected` after the rename; the installer already keeps its `previous` file there (`printf '%s\n' "$previous_version" > "${state_dir}/previous"`, `deploy/lib/deploy.sh:263`), the poll unit already has that directory writable, and it survives reboots. File content is one plain `X.Y.Z` plus newline; the write is temp-file-then-`mv` like the `current` swap.
**Poll compare order:** (1) latest not newer than active -> "up to date", exit 0; (2) latest not newer than rejected -> log "release X was rolled back after a failed health check; waiting for a newer release", exit 0; (3) otherwise install.
**When it is written:** the install result is `rolled_back` (health failed, previous release reactivated) or `failed` (health failed with nothing to roll back to). Do not write it for download, attestation or Sigstore-reachability failures (they can be transient and are retried each cycle).
**When it clears:** on any successful install, and implicitly when a newer release arrives (the compare in step 2 stops matching). `cabinet-deploy install vX.Y.Z` deliberately ignores the marker (operator override); a successful manual install clears it.
**Reference behaviours to keep in mind:** after a rollback `ledger_activate_release` records the broken version as `previous` (it writes the outgoing version each time), so prune protects the broken release directory until a later install; the reference counts a rollback whose own health check also fails as `rolled_back` (`... || true`), the port should report that as `failed` and log it at error level.
**Tests (offline, in the installer logic test):** record/read/clear round trip; `is_rejected` for versions below, equal and above; poll with latest equal to rejected installs nothing (stubbed `cmd_install` records no call); poll with a newer latest calls install; install success clears the file; manual install ignores it.

### Pattern 3: Poll HTTP statuses and rate-limit logging (D-14), design
**Reference today:** `curl --fail --silent --show-error --max-time 30 "$latest_url"` [VERIFIED: ing-dashboard `deploy/bin/ledger-deploy:189`]; any HTTP error exits non-zero and `ledger_die` ends the run, so a 404 before the first release or a rate limit fails the unit every cycle.
**Port:** replace with a status-aware fetch (curl `--dump-header` to a temp file, `--output` to a temp file, `--write-out '%{http_code}'`, `--user-agent`):
- 200: parse `.tag_name`, require the strict semver tag shape (keep the reference check).
- 404: log "no published release yet", exit 0.
- 403 or 429 with `x-ratelimit-remaining: 0` or a `retry-after` header: log quietly with the reset time, exit 0. A 403 without those signals is a real failure.
- 5xx, network error, anything else: failure, exit 1 (the unit shows failed until the next good poll).
- Always log `x-ratelimit-remaining` when the header is present.
**Rationale:** GitHub's documented unauthenticated limit is 60 requests per hour per originating IP, answered with 403 or 429 and `x-ratelimit-remaining: 0` [CITED: docs.github.com/rest/using-the-rest-api/rate-limits-for-the-rest-api]. The ETag route is a dead end: a 304 is exempt only "if a 304 response is returned and the request was made while correctly authorized" [CITED: docs.github.com best practices].
**Timer:** reference has `OnBootSec=2min`, `OnUnitActiveSec=5min`, `RandomizedDelaySec=30` [VERIFIED: ing-dashboard `deploy/systemd/ledger-deploy-poll.timer:5-7`]. Cabinet: `OnBootSec=3min`, `OnUnitActiveSec=10min`, `RandomizedDelaySec=120` (planner's discretion on the exact numbers). Each install adds one more unauthenticated call (the `compare` API check), still far below the limit.
**Testability:** the installer must only run `main "$@"` when executed, not when sourced (the reference calls `main "$@"` unconditionally at the bottom), so tests can source it and call `cmd_poll` with the HTTP function and `cmd_install` redefined.

### Pattern 4: Image smoke test (recommended shape)
**Recommendation:** an app subcommand plus a small root wrapper that runs it in the production sandbox.
- `dotnet Cabinet.Service.dll image-smoke` is handled at the top of `Program.cs` before the web host is built (same position as the reference's `apikey` subcommand). It calls `Cabinet.Repository.Images.ImageSmoke.Run()`: draw a synthetic bitmap, resize to a cabinet-realistic width, encode WebP at quality 80, decode the bytes back, and require the expected dimensions and the `RIFF`/`WEBP` markers. Prototype output: `PASS image-smoke 240x180 webp 166 bytes`.
- A root-owned wrapper (in `cabinet-selfcheck`) runs it with `systemd-run --pipe --wait --collect` as user `cabinet` with the same hardening properties the reference's `ledger-apikey` passes (`NoNewPrivileges`, `ProtectSystem=strict`, `ProtectHome`, `PrivateTmp`, `PrivateDevices`, kernel/cgroup protections, `RestrictNamespaces`, `LockPersonality`, empty `CapabilityBoundingSet`, `RestrictAddressFamilies=AF_UNIX`), `--working-directory=/opt/cabinet/current/app`. So it exercises the real deployed binaries, the real runtime package, the 1 GB container and an unprivileged LXC sandbox.
- Use a larger, noisy synthetic image (for example 2400x1800 seeded noise) in addition to the flat fill so the lossy encoder does real work and peak memory is exercised; keep it under a few seconds.
- The same `ImageSmoke.Run()` also runs as a unit test in CI.
**Rejected:** (a) making image success part of the installer's health gate (couples deploys to imaging); (b) running `dotnet test` on the LXC (needs the SDK and CI-style code on the server); (c) a hidden HTTP endpoint (public surface). The installer may log the smoke result after install without gating on it; `cabinet-selfcheck` and the provisioning checklist are the proof points.

### Pattern 5: History scrub and rewrite (D-04, D-05), procedure
**Tool:** `git filter-repo --replace-text`. Verified on a synthetic scratch repo: `regex:(?m)^\*\*Homelab\.\*\* .*$==>replacement` replaced the same paragraph whose wording differed between two commits, a `literal:` entry replaced a substring, the rewritten history had 0 residual matches, the reflog was empty and no garbage objects remained. [VERIFIED: scratch run this session]
1. Preconditions: clean tree, `git remote -v` empty (it is), note that `main` holds only the initial commit and the milestone branch holds the rest.
2. Back up outside the repo and outside any synced folder: `git clone --mirror <repo> <backup-dir>`. The backup contains the real data; delete it after verification and never push it.
3. Install the tool (`sudo apt install git-filter-repo`, owner action, or use the upstream single file).
4. Write the expressions file **outside the repo** (it contains the sensitive strings), for example next to the denylist. Use `(?m)`-prefixed `regex:` entries keyed on each affected line's distinctive prefix so every historical version of the line is covered, and `literal:` entries for single terms.
5. Dry run in a throwaway clone (`git clone --no-local`), run the rewrite, then run the checks below. When clean, run the same command in the real repo with `--force` (it refuses a non-fresh clone otherwise); all refs are rewritten, dates and messages are preserved, old objects are pruned.
6. Checks over **every** commit and ref (not just the tip): denylist grep over `git rev-list --all` with `git grep -I -i -F -f <denylist> <commit>`; the same patterns over `git log --all --format=%B` and over `git log --all --format='%an %ae %cn %ce'`; gitleaks via the pinned container with `--log-opts="--all"` (the ported lint script scans `HEAD` only, which misses other branches); `git fsck --lost-found`; confirm `git reflog` and `git count-objects -v` show nothing left.
7. Repeat the same checks as a gate immediately before the first push, because commits made during the phase (including generated plan documents) are new history.
**Scrub inventory (locations only, values are in the owner's denylist):**
- `.planning/PROJECT.md`: the homelab paragraph (hardware model, neighbouring guest names and roles, network vendor, pointer to the owner's private notes with a local path), the reference-project paragraph (local path), the "not the model" paragraph (local path and neighbouring project name), the `Proxmox host` mentions in the constraints and checklist, and the neighbouring project in the out-of-scope list.
- `.claude/CLAUDE.md`: the `Proxmox host` hosting constraint and the copied stack text that names neighbouring services (lines near the persistence table and near the hosting/privacy constraints).
- `.planning/REQUIREMENTS.md`: the out-of-scope row naming the neighbouring project.
- `.planning/research/*.md`: CPU model in the pitfalls table, neighbouring database host rejected in the stack and architecture documents, the pitfalls text naming neighbouring guests, the summary's homelab line.
- `.planning/phases/01-.../01-CONTEXT.md` canonical references use sibling-relative paths to the reference checkout; rewrite as `ing-dashboard:<path>` (GitHub slug form).
- Re-grep after the rewrite with the owner's denylist; also grep for `/mnt/`, `/home/` and `~/` style paths.

### Pattern 6: Denylist hook (D-06)
**What:** committed `.githooks/pre-commit`, `commit-msg`, `pre-push` (extensionless, executable, linted with shellcheck) installed by `git config core.hooksPath .githooks` (documented in `docs/development.md`; a tiny install script optional). Each reads the denylist from `${CABINET_DENYLIST_FILE:-${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt}`, one fixed string per line, `#` comments and blanks ignored, case-insensitive.
- pre-commit: scan added lines of `git diff --cached -U0` and staged file names.
- commit-msg: scan the message file (messages are history too).
- pre-push: read the ref lines on stdin and scan the commits not yet on the remote (`git rev-list <sha> --not --remotes`), patches and messages, as the safety net for `--no-verify`.
- Absent or unreadable file: print a warning and exit 0 (CI, fresh clones, other contributors).
- Never print more than file, line number and "denylist match #N" in CI-visible output; local output may show the match.
**Test:** `build/tests/githooks-test.sh` builds a throwaway repo with a synthetic denylist (invented strings) and asserts block, allow, warn-when-absent and message-scan behaviour. The test and hook sources contain only synthetic strings.
**Note:** the GSD commit helper runs `git commit`, so the hook also guards planning commits.

### Pattern 7: Go-live sequence and settings (D-15, D-16)
Ordering that avoids every trap seen in the reference go-live and the project pitfalls:
1. Local gate green: lint, tests, privacy checks, history scan clean.
2. Owner prerequisites done (profile display name and email privacy resolved; see Pitfall 3).
3. `gh repo create cryptic96/games-cabinet --public` with **no** README, licence or gitignore (so histories stay related); add the remote.
4. Push `main` (initial commit only, no ruleset yet), then push the milestone branch, open the PR so CI runs once.
5. Read the check names from the run (`gh api repos/<repo>/commits/<sha>/check-runs --jq '.check_runs[].name'`); the reference's runs show `build-test` and `lint` with app id `15368` [VERIFIED: gh api read-back of ing-dashboard check runs]. Then apply the main ruleset with those checks.
6. Create and read back the `deploy` environment, the tag ruleset, the other settings, then run the read-back script. All of this before the first tag, otherwise the first run auto-creates the environment unprotected [CITED: project pitfall research, GitHub docs].
7. Merge the PR through the web UI (see Pitfall 3), then create the tag on the merge commit.
8. Immutable releases must be enabled before the first publish.
Main ruleset JSON, based on the working reference ruleset read back this session (`bypass_actors: []`, `~DEFAULT_BRANCH`, rules `deletion`, `non_fast_forward`, `pull_request` with 0 approvals) plus `required_status_checks` from the documented rules API:
```json
{
  "name": "Protect Main Branch",
  "target": "branch",
  "enforcement": "active",
  "bypass_actors": [],
  "conditions": { "ref_name": { "include": ["~DEFAULT_BRANCH"], "exclude": [] } },
  "rules": [
    { "type": "deletion" },
    { "type": "non_fast_forward" },
    { "type": "pull_request", "parameters": {
        "required_approving_review_count": 0,
        "dismiss_stale_reviews_on_push": true,
        "require_code_owner_review": false,
        "require_last_push_approval": false,
        "required_review_thread_resolution": false,
        "allowed_merge_methods": ["merge", "squash"] } },
    { "type": "required_status_checks", "parameters": {
        "strict_required_status_checks_policy": false,
        "required_status_checks": [
          { "context": "build-test", "integration_id": 15368 },
          { "context": "lint", "integration_id": 15368 } ] } }
  ]
}
```
Apply with `gh api --method POST repos/{owner}/{repo}/rulesets --input <file>`; update with `PUT .../rulesets/{id}`. Other controls: reuse the reference guide's apply and read-back commands (tag ruleset restricting `refs/tags/v*` creation/update/deletion with only the Admin repository role, actor id 5, as bypass; `deploy` environment with one required reviewer, `prevent_self_review=false`, custom deployment policy plus a `v*.*.*` tag policy; fork-PR approval `all_external_contributors`; default workflow permission `read` with `can_approve_pull_request_reviews=false`; `sha_pinning_required=true`; secret scanning, push protection and Dependabot security updates; vulnerability alerts; immutable releases; empty runner list). The reference read-back values confirm the working shapes: environment protection rules `required_reviewers` (1 reviewer, `prevent_self_review` false) and `branch_policy`, `custom_branch_policies: true`, `sha_pinning_required: true`, immutable releases `enabled: true`, tag ruleset rules `creation`, `update`, `deletion` with bypass actor `RepositoryRole` 5. [VERIFIED: gh api read-backs of ing-dashboard]

### Pattern 8: Rollback rehearsal sequence (D-11, D-12, D-13)
1. `v0.1.0`: hello page. Tag on main, build, attestation, draft, owner approval, publish; the poll timer (or a manual `cabinet-deploy poll`) installs it; hello page loads from a LAN client through Traefik; `cabinet-selfcheck` passes including the image smoke.
2. `v0.1.1` (deliberately broken, no switch in production code): a pull request changes the committed ops listener port in `appsettings.json` to a different loopback port, so the app starts and serves the public port but the installer's health check against the configured ops URL never succeeds. CI stays green because tests set their own ports; do not add any test that asserts the committed ops port. After the build creates the draft, set the draft's release notes to label it a rollback rehearsal **before** approving (published releases are immutable; whether notes remain editable afterwards is not verified here, so edit while draft). Approve; the poller installs, health times out, the installer reactivates `0.1.0`, writes the rejected marker. Evidence: journal lines, `/health` reporting `0.1.0`, the `current` symlink, the state file.
3. Run `cabinet-deploy poll` twice more and confirm the log says it is skipping the rolled-back release and the service was not restarted again (compare `ActiveEnterTimestamp`).
4. `v0.1.2`: a pull request reverts the port. Tag, approve; the poller installs it (newer than the rejected version), health passes, the marker is cleared.

### Anti-Patterns to Avoid
- **A test switch in production code to force unhealthy:** forbidden by the rehearsal decision; break the release with an ordinary diff.
- **Health that depends on anything external:** the first deploy has no snapshot and a BGG outage would roll back good releases.
- **Treating every `curl --fail` failure in the poller as an alert:** quiet 404 and rate-limit handling, loud real failures.
- **Describing verification as air-gapped:** `gh` fetches Sigstore's public trust root (Pitfall 13).
- **Repeating the denylist inside any committed file** (lint config, tests, docs): it would publish what it protects.

## Don't Hand-Roll

| Problem | Don't Build | Use Instead | Why |
|---------|-------------|-------------|-----|
| Artifact provenance verification | Custom signature/hash logic | `gh attestation verify --bundle ... --signer-workflow ... --source-ref ... --deny-self-hosted-runners` with GitHub tokens unset | Sigstore chain, certificate identity and workflow pinning are subtle |
| Secret scanning of history | Grep-only scripts | gitleaks (pinned container) plus the owner-side denylist for what generic rules cannot know | Entropy and token-shape rules are mature |
| Workflow security review | Manual review | zizmor + actionlint | Expression-injection and unpinned-action classes |
| History rewriting | `sed` over `.git` objects, manual rebases | `git filter-repo --replace-text` | Rewrites all refs, preserves dates, prunes old objects (verified) |
| APT key trust | `apt-key`, blind `curl | sh` | `gpg --show-keys` fingerprint pin + `signed-by` keyring (reference module) | Handles expired transitional keys |
| Static asset fingerprinting | Hand-hashed filenames | `MapStaticAssets` + `asp-append-version` | Verified fingerprinted CSS from the published output |
| Health reporting | Custom TCP/`curl` heuristics in the unit | ASP.NET Core `AddHealthChecks` + `UseHealthChecks` bound to the ops port | Port-bound middleware keeps it off the public listener (verified: 404 on the public port) |
| Image encoding | Own WebP encoder | ImageSharp | Obvious |
| Atomic release switch | Directory copy over `current` | `ln -s` to a temp name then `mv -T` | Single atomic rename (reference function) |

**Key insight:** every control in this phase already exists in a proven form in ing-dashboard; the work is renaming, deleting what does not apply, and closing the four behavioural gaps found here (rejected-version memory, quiet poll statuses, version-aware health, privacy gate).

## Common Pitfalls

### Pitfall 1: ImageSharp 4.x breaks Release builds without a licence key
**What goes wrong:** `release` packaging (`dotnet publish -c Release`) and any Release build fail with `error : No Six Labors license found. Set $(SixLaborsLicenseKey), set $(SixLaborsLicenseFile), or add a 'sixlabors.lic' file to the project/workspace` [VERIFIED: reproduced here with 4.1.2 under warnings-as-errors; Debug only warns, `Build succeeded`].
**Why it happens:** version 4 is the first with build-time enforcement for direct dependencies; the project stack research only considered the licence text, not the build task. The check is skipped for transitive consumers only.
**How to avoid:** pin 3.1.12 for now (verified to build, publish and run the round trip with no key), add a Dependabot ignore for the major, and treat the community key as an owner prerequisite if 4.x is wanted later. If 4.x is chosen, supply the key through `SixLaborsLicenseKey` from a secret in the release and CI jobs (never committed; Dependabot PRs use a separate secret store), and note forks cannot build Release.
**Warning signs:** green Debug test run, red release job.

### Pitfall 2: First provisioning has no release to clone from
**What goes wrong:** the docs say clone at the latest release tag, but none exists before the first release, and `releases/latest` returns 404.
**How to avoid:** clone `main` (or the milestone branch) for the very first run after the repo is public; install `git` first (not assumed present in the template; verify). The 404 from `releases/latest` must be a quiet no-op in the poller (Pattern 3).

### Pitfall 3: Web-UI merge commit carries the owner's profile name and email
**What goes wrong:** the reference's first merge created through the web UI carried the operator's real name and email, and had to be rewritten with a force-push before tagging [CITED: ing-dashboard 01-11 and 01-12 summaries]. Here the profile display name is set and differs from the login (booleans checked through the API; value not recorded).
**How to avoid:** before the first PR merge: set the profile name to the handle or clear it, enable "Keep my email addresses private" and "Block command line pushes that expose my email", then verify after the first merge with `git log --format='%an %ae %cn %ce' origin/main`. Whether the web merge author name always comes from the profile name is [ASSUMED]; the post-merge verification settles it. An immutable release pins whatever commit is tagged, so check before tagging.

### Pitfall 4: `deploy` environment auto-created unprotected
**What goes wrong:** a workflow that references a missing environment creates it with no protection, so the first tag publishes without approval [CITED: project pitfall research]. **How to avoid:** D-16 ordering; the read-back script's environment checks gate the first tag.

### Pitfall 5: Required status checks cannot be chosen before the checks exist
**How to avoid:** apply the main ruleset without `required_status_checks`, open the PR, read the check names (`build-test`, `lint`), then add them (Pattern 7).

### Pitfall 6: `curl --fail` poll failures and the rollback loop
**What goes wrong:** quiet cases become failures, and a rolled-back release is reinstalled every cycle. **How to avoid:** Patterns 2 and 3.

### Pitfall 7: Under `pipefail`, `cmd | grep -q` gives false results
**What goes wrong:** the reference's first host run produced a false "policy is not drop" because `grep -q` closed the pipe early [CITED: ing-dashboard 01-11 summary]. **How to avoid:** capture command output into a variable first, then match; apply to every check in `cabinet-selfcheck` and installer helpers.

### Pitfall 8: Offline tests reaching the real `systemctl`
**What goes wrong:** a test stand-in answered one query wrongly and two polkit prompts appeared on the developer machine [CITED: ing-dashboard 01-12 summary]. **How to avoid:** keep `deploy/tests/lib/host-guard.sh` and source it first in every offline test, and assert at the end that the recorded call list is empty.

### Pitfall 9: Planning-reference lint matching its own source
**How to avoid:** keep the reference's technique: patterns with grouped alternations or one bracketed character (`\.plannin[g]/`), and `printf` splits in self-test fixtures, so no script, config or doc contains a match. Extend the key-prefix list when adding it. Also `.gitignore` must use the bracket form.

### Pitfall 10: Immutable releases cannot be repaired
**How to avoid:** enable the setting before the first publish, edit draft notes before approval, never re-upload to a published release, and expect the broken rehearsal release to stay listed.

### Pitfall 11: Quoting with a space in the checkout path
**What goes wrong:** this checkout's directory name contains a space; any unquoted path expansion in the ported scripts (the `EXTRA_MOUNT` string in `build/lint.sh` is one) breaks in a linked worktree. **How to avoid:** build mount arguments as an array and quote every expansion; shellcheck covers most cases.

### Pitfall 12: Generic gitleaks rules miss homelab-shaped data
**What goes wrong:** gitleaks found nothing in the current history, yet that history contained hardware and neighbouring-service names (the rules cannot know them). **How to avoid:** the denylist hook plus owner review; optionally add a generic rule for internal-looking hostnames (suffixes such as `.lan`, `.home.arpa`, `.internal`, and `.local` followed by `:`, `/`, whitespace, a quote or end of line, so `appsettings.local.json` does not match), with a self-test.

### Pitfall 13: "Offline" attestation wording
**What goes wrong:** the reference's release guide says no network call is needed once both files are on disk, while its installer comment states `gh` "does fetch Sigstore's public trust root, so an unreachable Sigstore instance fails verification rather than skipping it" [VERIFIED: ing-dashboard `deploy/lib/deploy.sh` header comment of the verify function]. The `gh` manual documents `--custom-trusted-root` as the fully offline route [CITED: cli.github.com/manual/gh_attestation_verify]. **How to avoid:** word the docs precisely: no GitHub credential and no GitHub API call, but Sigstore must be reachable (fail closed).

### Pitfall 14: Quiet failures from `releases/latest` ordering
`releases/latest` returns the most recently published non-draft, non-prerelease release, not the highest semver; the strict greater-than comparisons against active and rejected keep a late lower-numbered publish harmless.

## Code Examples

Verified in the scratch prototype this session unless marked design.

### Dual listeners (`appsettings.json`)
```json
{
  "Kestrel": {
    "Endpoints": {
      "Web": { "Url": "http://0.0.0.0:5080" },
      "Ops": { "Url": "http://127.0.0.1:5081" }
    }
  }
}
```

### Program.cs core (health port-bound, image-smoke before host build)
```csharp
using System.Reflection;
using Cabinet.Domain;
using Cabinet.Repository.Images;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

if (args.Length > 0 && args[0] == "image-smoke")
{
    var result = ImageSmoke.Run();
    Console.WriteLine($"PASS image-smoke {result.Width}x{result.Height} webp {result.Bytes} bytes");
    return 0;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHealthChecks();
builder.Services.AddRazorPages();
var buildInfo = BuildInfo.Parse(Assembly.GetEntryAssembly()!
    .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
builder.Services.AddSingleton(buildInfo);
var app = builder.Build();

var opsPort = new Uri(app.Configuration["Kestrel:Endpoints:Ops:Url"]!).Port;
app.UseHealthChecks("/health", opsPort, new HealthCheckOptions
{
    ResponseWriter = (context, result) =>
    {
        context.Response.ContentType = "application/json";
        return context.Response.WriteAsJsonAsync(new { status = result.Status.ToString(), version = buildInfo.Version, commit = buildInfo.Commit });
    }
});

app.UseRouting();
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

await app.RunAsync();
return 0;

/// <summary>Entry point, exposed so integration tests can boot the host.</summary>
public partial class Program;
```
Production code should also bind `ReverseProxy:KnownProxies` into `ForwardedHeadersOptions` (reference does this with `ForwardLimit = 1` and `XForwardedFor | XForwardedProto`) and call `UseForwardedHeaders()` first, and replace the raw port parse with the reference's loopback-validating `OpsEndpoint.FromConfiguration`.

### BuildInfo (Domain, pure)
```csharp
namespace Cabinet.Domain;

/// <summary>Version and commit of the running build, parsed from the informational version.</summary>
public sealed record BuildInfo(string Version, string Commit)
{
    /// <summary>Splits an informational version such as 0.1.0+abc123 into version and commit.</summary>
    public static BuildInfo Parse(string? informationalVersion)
    {
        if (string.IsNullOrWhiteSpace(informationalVersion))
        {
            return new BuildInfo("0.0.0", "unknown");
        }

        var plus = informationalVersion.IndexOf('+');
        return plus < 0
            ? new BuildInfo(informationalVersion, "unknown")
            : new BuildInfo(informationalVersion[..plus], informationalVersion[(plus + 1)..]);
    }
}
```

### ImageSmoke (Repository), runs on both 3.1.12 and 4.1.2 unchanged
```csharp
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Cabinet.Repository.Images;

/// <summary>Draws a synthetic bitmap, resizes it and round-trips it through WebP to prove the imaging stack works on this host.</summary>
public static class ImageSmoke
{
    /// <summary>Runs the round trip and returns the decoded size, or throws when any step misbehaves.</summary>
    public static (int Width, int Height, int Bytes) Run()
    {
        using var source = new Image<Rgba32>(640, 480, Color.SaddleBrown);
        source.Mutate(context => context.Resize(new ResizeOptions { Size = new Size(240, 0), Mode = ResizeMode.Max }));
        using var stream = new MemoryStream();
        source.Save(stream, new WebpEncoder { Quality = 80 });
        var bytes = stream.ToArray();
        var info = Image.Identify(bytes);
        if (info.Width != 240 || info.Height != 180 || info.Metadata.DecodedImageFormat?.Name != "Webp")
        {
            throw new InvalidOperationException("WebP round trip returned an unexpected image.");
        }

        return (info.Width, info.Height, bytes.Length);
    }
}
```
(Format name casing is `Webp` on 3.1.12, `WEBP` printed by an earlier 4.1.2 run; compare case-insensitively if both versions must pass.)

### Test project shape (verified restore, build, run)
```xml
<PropertyGroup><IsPackable>false</IsPackable><OutputType>Exe</OutputType><IsTestProject>true</IsTestProject></PropertyGroup>
<PackageReference Include="FluentAssertions" Version="8.11.0" />
<PackageReference Include="xunit.v3" Version="4.0.1" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.12" />   <!-- integration project only -->
<Using Include="Xunit" />
```
Run: `dotnet test --solution Cabinet.slnx` (MTP runner from `global.json`). `dotnet restore --locked-mode` succeeded against the generated `packages.lock.json` files (commit them).

### Nftables rule change (reference lines to adapt)
Reference rules: `tcp dport 22 ip saddr { @LEDGER_ADMIN_SSH_SOURCES@ } accept` and `tcp dport { 5080, 3000 } ip saddr @LEDGER_TRAEFIK_IP@ accept` [VERIFIED: ing-dashboard `deploy/nftables/ledger.nft.in:23-24`]. Cabinet: same SSH rule and `tcp dport 5080 ip saddr @CABINET_TRAEFIK_IP@ accept`; keep default-drop input and forward, accept output.

### Systemd poll timer (design values)
```ini
[Timer]
OnBootSec=3min
OnUnitActiveSec=10min
RandomizedDelaySec=120
```

### Rejected-version helpers and poll decision (design sketch)
```bash
cabinet_record_rejected_version() {
  local version="$1" state_dir="$2" tmp
  mkdir -p "$state_dir"
  tmp="$(mktemp "${state_dir}/.rejected.XXXXXX")"
  printf '%s\n' "$version" > "$tmp"
  mv -f "$tmp" "${state_dir}/rejected"
}

cabinet_clear_rejected_version() {
  rm -f "${1}/rejected"
}

cabinet_is_rejected() {
  local version="$1" state_dir="$2" rejected=""
  [ -f "${state_dir}/rejected" ] && rejected="$(cat "${state_dir}/rejected")"
  [ -n "$rejected" ] || return 1
  ! cabinet_semver_gt "$version" "$rejected"
}
```

### Poll fetch (design sketch)
```bash
status="$(curl --silent --show-error --max-time 30 --user-agent cabinet-deploy \
  --dump-header "$headers" --output "$body" --write-out '%{http_code}' "$latest_url")" || status=000
remaining="$(awk -F': ' 'tolower($1)=="x-ratelimit-remaining"{gsub("\r","",$2); print $2}' "$headers")"
[ -z "$remaining" ] || cabinet_log "github api rate limit remaining: ${remaining}"
case "$status" in
  200) tag="$(jq -r '.tag_name // empty' "$body")" ;;
  404) cabinet_log "no published release yet"; return 0 ;;
  403|429) if [ "$remaining" = "0" ] || grep -qi '^retry-after:' "$headers"; then
             cabinet_log "rate limited by github, retrying next cycle"; return 0
           fi
           cabinet_die "github returned ${status}" ;;
  *) cabinet_die "polling failed with status ${status}" ;;
esac
```

### Denylist hook core (design sketch)
```bash
denylist="${CABINET_DENYLIST_FILE:-${XDG_CONFIG_HOME:-$HOME/.config}/games-cabinet/denylist.txt}"
if [ ! -r "$denylist" ]; then
  echo "warning: denylist not found, skipping personal-data check" >&2
  exit 0
fi
patterns="$(grep -vE '^[[:space:]]*(#|$)' "$denylist" || true)"
[ -n "$patterns" ] || exit 0
added="$(git diff --cached -U0 --no-color | grep -E '^\+[^+]' || true)"
if grep -n -i -F -f <(printf '%s\n' "$patterns") <<< "$added"; then
  echo "blocked: staged content matches the personal-data denylist" >&2
  exit 1
fi
```

### Filter-repo expressions file syntax (verified; use synthetic values in any committed example)
```
regex:(?m)^\*\*Homelab\.\*\* .*$==>**Homelab.** A low-power mini PC shared with other guests.
literal:SomeSpecificTerm==>generic term
```

## State of the Art

| Old Approach | Current Approach | When Changed | Impact |
|--------------|------------------|--------------|--------|
| ImageSharp free with OSS licence text only | v4 enforces a build-time licence key for direct dependencies (community key is free for OSS) | 4.0.0 (2026-05-12) | Release builds fail without a key; pin 3.1.12 or obtain a key |
| Per-repo settings by hand in the UI | Rulesets + environments + immutable releases applied and read back with `gh api` | reference project, 2026-09 | Reproducible, scriptable (D-15) |
| Mutable release assets | Immutable releases (draft then publish) | GA 2025-10-28 | No repair after publish; rehearse on drafts |
| `Microsoft.NET.Test.Sdk` + VSTest | xunit.v3 test projects as executables on Microsoft.Testing.Platform, `dotnet test --solution` | .NET 10 | Verified working with the copied `global.json` |
| `.sln` | `.slnx` | .NET 10 default | Used by the reference; restore and test verified |
| Hand-hashed static files | `MapStaticAssets` with build-time fingerprinting | .NET 9+ | Verified from the published output |

**Deprecated/outdated:** `git filter-branch` (the git project recommends filter-repo); `actions` referenced by tag instead of SHA (blocked by the pinning setting).

## Owner and manual actions versus autonomous work

| Item | Who | Notes |
|------|-----|-------|
| Register the BGG application (non-commercial) | Owner, day one, independent of this phase | Gates a later phase; approval can take a week or more |
| Apply for a Six Labors community key | Owner, optional | Only if 4.x is chosen |
| Create the denylist file (real username, domain, ranges, location names) | Owner | File outside the repo; Claude never sees it unless the owner shares it |
| Install `git-filter-repo` (sudo) | Owner or approved command | Fallback: upstream single file or `filter-branch` |
| GitHub profile display name and email privacy | Owner | Before the first PR merge |
| Approve history rewrite and run it | Claude after owner approval (destructive, one-way after push) | Backup mirror first |
| Port, skeleton, lint, workflows, deploy tree, docs, offline tests | Claude, autonomous | All verified offline; lint needs docker (available) |
| Create the empty public repo, push, apply settings, read back | Claude after the single approval of the command list (D-15) | Includes creating the repo (the list should include it) |
| `pct create` on the Proxmox host | Owner (checkpoint) | Commands documented; values (id, bridge, address) are the owner's |
| Fill `/etc/cabinet/provision.conf` with real addresses, run provisioning twice | Owner, or Claude over SSH if the owner grants temporary access | Real values only on the server |
| Traefik dynamic config file and DNS record | Owner (checkpoint) | Template ships in the repo; real hostname never committed |
| Push release tags | Owner confirms each (one-way) | Tag ruleset allows only the admin role |
| Approve the `deploy` environment per release | Owner (the human gate, never delegated) | Three approvals for the rehearsal |
| Verify the hello page from a LAN client | Owner or Claude from a LAN workstation | |

## Suggested plan decomposition (dependencies, not a mandate)

1. Privacy gate: denylist, hooks + test, scrub wording, history rewrite, full-history scan (owner-gated; first, so later commits are clean by construction).
2. Skeleton: solution, projects, tests, LICENSE, README, lock files, hello page, health contract.
3. Guardrails: lint framework port + JS and licence rules, gitleaks/zizmor/dependabot config, docs/development.
4. Release pipeline: ci.yml, release.yml, package/validate/verify/check scripts, tests, settings guide.
5. Deploy tree: installer with rejected-version and quiet poll, libs, provisioning modules, units, templates, selfcheck + image smoke wrapper, logic tests, docs.
6. Go-live checkpoint: repo creation, pushes, PR, ruleset in two steps, settings list, read-back, merge, first tag.
7. LXC checkpoint: create, provision, Traefik/DNS, first release install, selfcheck.
8. Rehearsal: broken release, rollback, skip evidence, fix release, final selfcheck, evidence recorded.
Plans 2 to 5 can run in parallel after 1; 5 depends on 2 for the health contract and the published layout; 6 needs 1 to 5; 7 and 8 need 6.

## Assumptions Log

| # | Claim | Section | Risk if Wrong |
|---|-------|---------|---------------|
| A1 | The GitHub web-merge author name comes from the profile display name | Pitfall 3 | A real name could still reach history after the profile is cleaned; mitigated by the verify-before-tag step |
| A2 | ImageSharp 3.x will receive security fixes if one is needed | Standard Stack, Pitfall 1 | Untrusted image decoding on an unpatched major; owner may prefer 4.x with a key or SkiaSharp |
| A3 | A Six Labors community key is granted to an MIT public repo and its latency is acceptable | Open Question 1 | If refused or slow, 3.1.12 or Skia remains the path |
| A4 | Editing release notes is possible while the release is a draft and cannot be done after publish | Pattern 8, Pitfall 10 | If notes are editable after publish the labelling step is easier; if the build overwrites notes it must be adjusted |
| A5 | `git` is not present in the Ubuntu LXC template | Pitfall 2 | Docs include an unnecessary `apt install git` step |
| A6 | Info-ZIP `unzip` refuses `../` path traversal entries and the attested artifact is only built by our workflow | Security Domain | A malicious zip would still need a valid attestation first |
| A7 | Pulling the four lint images from Docker Hub/GHCR from hosted runners keeps working as in the reference | Standard Stack | CI lint step flaky on pull limits |
| A8 | The Proxmox template file name and `pct` parameters from the reference apply to this host | Owner actions | Owner adjusts the documented command |
| A9 | ASVS category mapping below uses the 4.0.3 numbering | Security Domain | Labels differ if the project later adopts 5.0 numbering |
| A10 | The owner's host can reach Sigstore's public trust root endpoints and GitHub from the new LXC | Environment Availability | First install fails closed; egress or DNS fix required |

## Open Questions

> **Resolved at planning time (owner decisions, recorded as D-17 and D-18 in CONTEXT.md, which take precedence over this document):**
> - Question 1: the owner chose **SkiaSharp 4.153.1 + SkiaSharp.NativeAssets.Linux.NoDependencies 4.153.1** instead of either ImageSharp option. Every ImageSharp recommendation, code example and Dependabot ignore in this document is superseded. Verified by the orchestrator in a scratch project: Release `linux-x64` framework-dependent publish with warnings-as-errors succeeds; decode → `SKBitmap.Resize(new SKImageInfo(w,h), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))` → `SKImage.FromBitmap(...).Encode(SKEncodedImageFormat.Webp, 80)` → decode-back round trip works (RIFF/WEBP header, expected dimensions and pixel); output contains `SkiaSharp.dll` and a ~12 MB `libSkiaSharp.so` (glibc ≥ 2.27, libstdc++, libgcc_s; no fontconfig). Release zip size grows by ~12 MB; nothing extra is needed in provisioning on Ubuntu 24.04. Dependabot should group both SkiaSharp packages so they move together.
> - Question 2: allow **merge and squash**, decided per PR; rebase stays disabled.
> - Questions 3–5: follow the recommendations below.

1. **ImageSharp 3.1.12 or 4.1.2 with a community key?** *(resolved: SkiaSharp, see note above)*
   - What we know: 4.x fails Release builds without a key (verified); 3.1.12 works with no key and no known advisories today; the project stack research selected 4.1.2 without this finding.
   - What's unclear: the owner's appetite for a secret in CI plus an application with unknown latency; long-term security fixes for 3.x.
   - Recommendation: pin 3.1.12 in this phase, add the Dependabot major ignore, record the decision, and revisit when the image pipeline phase starts. Needs explicit owner confirmation before planning locks it.
2. **Merge method for PRs into `main`.**
   - What we know: tags must be on `main`; the reference used merge commits for the milestone branch and a squash alternative exists.
   - What's unclear: whether the owner wants squash for feature PRs and a merge commit for milestone PRs.
   - Recommendation: allow `merge` and `squash` in the ruleset (as in the JSON above) and decide per PR.
3. **Hello page content and Razor Pages versus a plain endpoint.**
   - Recommendation: Razor Pages with one stylesheet; it validates the real page and static-asset pipeline from the symlinked release path at no extra risk.
4. **Does the first LXC provisioning clone from `main` or the milestone branch?**
   - Recommendation: `main` after the first PR merge; the milestone branch only if provisioning must precede the merge.
5. **Which extra generic gitleaks rules are wanted (internal hostname suffixes)?**
   - Recommendation: add the one rule with a self-test; hostnames and ranges stay in the owner's denylist.

## Environment Availability

| Dependency | Required By | Available | Version | Fallback |
|------------|------------|-----------|---------|----------|
| .NET SDK | build, tests, prototype | ✓ | 10.0.112 | — |
| Docker + compose | lint framework (all tool images) | ✓ | 29.8.2 / v5.6.0 | none locally; CI has docker |
| gh (authenticated as the owner account) | go-live settings, read-back scripts | ✓ | 2.102.0 | — |
| git | everything | ✓ | 2.53.0 | — |
| jq, zip, unzip, python3, nft | scripts and tests | ✓ | jq 1.8.1, python 3.14.4, nft 1.1.6 | — |
| git-filter-repo | privacy gate | ✗ | apt candidate 2.47.0-3 | upstream single file (verified to work) or `git filter-branch` |
| gitleaks, shellcheck, actionlint, zizmor (native) | lint | ✗ native | — | run through the pinned containers (digests verified to resolve; gitleaks container run succeeded here) |
| NuGet, GitHub, archive.ubuntu.com, cli.github.com | restore, API, pins | ✓ reachable from the workstation | — | — |
| Proxmox host, Traefik container, LXC, router DNS | OPS-04/05 live checks | not probeable from here | — | owner checkpoints |
| Sigstore trust-root reachability from the LXC | offline verify | unknown until first install | — | fail closed; fix egress |

**Missing dependencies with no fallback:** none for the autonomous work. Live checks depend on the owner's infrastructure.
**Missing dependencies with fallback:** git-filter-repo, native lint tools.

## Validation Architecture

### Test Framework
| Property | Value |
|----------|-------|
| Framework | xunit.v3 4.0.1 on Microsoft.Testing.Platform (.NET 10 `dotnet test --solution`), FluentAssertions 8.11.0; shell logic tests under `deploy/tests` and `build/tests` run by `build/lint.sh script-tests` |
| Config file | `global.json` (runner), `Directory.Build.props`; `xunit.runner.json` only if parallelism must be disabled |
| Quick run command | `dotnet test --solution Cabinet.slnx --no-restore` and `bash deploy/tests/cabinet-deploy-logic-test.sh` |
| Full suite command | `build/lint.sh` (docker; includes all script tests) then `dotnet test --solution Cabinet.slnx` |

Prototype timing: restore plus both test projects ran in about 1 s of test time on this machine.

### Phase Requirements to Test Map
| Req ID | Behavior | Test Type | Automated Command | File Exists? |
|--------|----------|-----------|-------------------|-------------|
| OPS-01 | MIT LICENSE present with the copyright line; no secret/PII-shaped content in tree or history | lint | `build/lint.sh repo-rules secrets` | ❌ Wave 0 |
| OPS-01 | Denylist hook blocks, allows, warns when absent, scans messages | script test | `bash build/tests/githooks-test.sh` | ❌ Wave 0 |
| OPS-01 | Full-history owner-denylist scan clean before first push | manual-only (denylist cannot be in CI or repo) | documented command in the privacy gate plan | n/a |
| OPS-01 | Protected `main` ruleset, tag ruleset, immutable releases, no runners | live read-back | `build/check-github-settings.sh` | ❌ Wave 0 |
| OPS-02 | Workflows are valid, SHA-pinned, `runs-on: ubuntu-24.04` only | lint | `build/lint.sh workflows repo-rules` | ❌ Wave 0 |
| OPS-02 | A PR runs `build-test` and `lint` on hosted runners | live | `gh pr checks <pr>` after the go-live PR | n/a |
| OPS-03 | Strict semver tag and main-ancestry validation | script test | `bash build/tests/validate-release-tag-test.sh` | ❌ Wave 0 |
| OPS-03 | Package script produces zip with `app/`, `deploy/` and manifest `{version, commit}` | CI step + local run | `build/package-release.sh --version 0.0.0 --commit <40-hex> --output artifacts/release` | ❌ Wave 0 |
| OPS-03 | Draft is attested and only publishes after approval | live | `build/verify-published-release.sh v0.1.0` and `check-github-settings.sh` (environment checks) | ❌ Wave 0 |
| OPS-04 | activate/prune, semver compare, rejected-version record/skip/clear, quiet 404/403/429 poll handling | script test | `bash deploy/tests/cabinet-deploy-logic-test.sh` | ❌ Wave 0 |
| OPS-04 | Tampered or mismatched artifact refused | network script test | `CABINET_LINT_NETWORK=1 bash deploy/tests/verify-rejects-tampered-artifact-network-test.sh` | ❌ Wave 0 |
| OPS-04 | Poll unit sandboxing and write paths | script test | `bash deploy/tests/sandboxing-test.sh` | ❌ Wave 0 |
| OPS-04 | `/health` on ops port only, JSON status and version, public port 404, healthy with empty state | integration | `dotnet test --solution Cabinet.slnx --no-restore` | ❌ Wave 0 |
| OPS-04 | Broken release rolls back, rejected release skipped, good follow-up installs | live rehearsal | journal and `/health` evidence (checkpoint) | n/a |
| OPS-05 | Provision config parsing, validators, template rendering | script test | `bash deploy/tests/provision-logic-test.sh && bash deploy/tests/render-templates-test.sh` | ❌ Wave 0 |
| OPS-05 | Version pins and key fingerprint still match real sources | network script test | `CABINET_LINT_NETWORK=1 bash deploy/tests/versions-network-test.sh` | ❌ Wave 0 |
| OPS-05 | Provisioned host passes selfcheck including the image smoke | live on the LXC | `cabinet-selfcheck` | ❌ Wave 0 |
| OPS-05 | ImageSmoke round trip | unit | `dotnet test --solution Cabinet.slnx --no-restore` | ❌ Wave 0 |

### Sampling Rate
- **Per task commit:** the quick commands relevant to the touched area.
- **Per wave merge:** `build/lint.sh` and `dotnet test --solution Cabinet.slnx`.
- **Phase gate:** CI green on the go-live PR, `check-github-settings.sh` all PASS, `verify-published-release.sh` all PASS, selfcheck clean on the LXC, rehearsal evidence recorded, before `/gsd-verify-work`.

### Wave 0 Gaps
- [ ] Entire solution, test projects, lock files: greenfield (`Cabinet.slnx` and projects).
- [ ] `build/lint*` framework, fixtures, compose file, new JS and licence rules.
- [ ] `deploy/tests/*` ported and extended tests, `deploy/tests/lib/host-guard.sh`, fixtures.
- [ ] `build/tests/githooks-test.sh`, `.githooks/*`.
- [ ] `build/check-github-settings.sh` with the main ruleset check.
- [ ] Framework install: none (SDK present); `git-filter-repo` for the privacy gate.

## Security Domain

Security enforcement is enabled (ASVS level 2 per config).

### Applicable ASVS Categories
| ASVS Category | Applies | Standard Control |
|---------------|---------|-----------------|
| V1 Architecture / threat model | yes | Documented pull-based model: no CI-executed code on the server, root installer is the only mutator, ops listener loopback-only |
| V2 Authentication | no (no user auth in this phase) | Admin access is SSH key-only (documented), enforced by provisioning and firewall |
| V3 Session Management | no | — |
| V4 Access Control | yes (infrastructure) | LAN/VPN `ipAllowList` at Traefik, nftables source restriction, SSH sources, GitHub environment reviewer, tag ruleset admin-only |
| V5 Input Validation | yes | Strict semver regex for tags, allow-listed `KEY=VALUE` config loaders that refuse backticks and `$(`, template values rejecting `;`, braces and newlines |
| V6 Cryptography | yes | Never hand-rolled: Sigstore attestation verification through `gh`, apt key fingerprint pins, TLS terminated at Traefik |
| V7 Error Handling and Logging | yes | No secrets in logs (no secrets exist on the box in this phase); quiet expected statuses, loud real failures; journald only |
| V8 Data Protection | yes | No personal data in repo or history (privacy gate), env file root-owned and mode-restricted |
| V10 Malicious Code / supply chain | yes | SHA-pinned actions enforced by repo setting and zizmor, locked-mode restore with lock files, immutable releases, attestation with `--deny-self-hosted-runners`, Dependabot |
| V14 Configuration | yes | `permissions: {}` workflow default, read-only default token, systemd hardening block, default-drop nftables, no trimming |

### Known Threat Patterns for this stack
| Pattern | STRIDE | Standard Mitigation |
|---------|--------|---------------------|
| Workflow expression injection via ref names | Tampering / Elevation | Pass `github.ref_name` through `env` only, strict semver check before use (reference pattern; zizmor/actionlint enforce) |
| Mutable action tags | Tampering | Full-SHA pins plus the repository setting that requires them |
| Publishing without approval (environment auto-created unprotected) | Elevation | Create and read back `deploy` before the first tag |
| Tampered artifact or wrong-branch tag at install | Tampering | Offline attestation verify before unpack; attested commit must be on `main`; downgrade refusal; checksum re-verified at publish |
| Re-install loop of a bad release | DoS (self) | Rejected-version memory |
| Config-file command injection on the server | Elevation | Allow-list parsers that never evaluate values (ported) |
| Installer privilege abuse | Elevation | Sandboxed poll unit with narrow `ReadWritePaths`; root-owned directories 700/755 |
| Zip path traversal in an unpacked artifact | Tampering | Only attested artifacts are unpacked; Info-ZIP strips `../` (assumed, A6) |
| Forwarded-header spoofing | Spoofing | Trust `X-Forwarded-*` only from the configured Traefik address (`KnownProxies`), nftables admits the app port only from that address |
| Personal data in a public repo | Information disclosure | Denylist hook, gitleaks generic rules, history rewrite and full-history scan, synthetic-only fixtures |
| Rate-limit exhaustion of the shared public IP | DoS | 10-minute jittered unauthenticated poll, quiet 403/429 handling, rate-limit logging |
| SSH exposure | Spoofing / Elevation | Key-only auth and nftables source allow-list from server-side config |

## Sources

### Primary (HIGH confidence)
- ing-dashboard repository files read this session: `.github/workflows/ci.yml`, `release.yml`, `dependabot.yml`, `zizmor.yml`, `.gitleaks.toml`, `Directory.Build.props`, `global.json`, `build/**`, `deploy/**` (installer, libs, provisioning modules, units, nftables and Traefik templates, tests), `Ledger.Service/**`, `Ledger.IntegrationTests/Infrastructure/LedgerWebApplicationFactory.cs`, `docs/**`, and its planning summaries for plans 11 and 12 (go-live lessons).
- `gh api` read-backs of the reference repository (main ruleset, tag ruleset, `deploy` environment, Actions permissions, immutable releases, check runs, releases and asset names).
- Local prototypes run this session (scratch solution with all five projects, publish and run from a symlinked release path; ImageSharp 3.1.12 and 4.1.2 Release build experiments; filter-repo replace-text on a synthetic repo; gitleaks container over the current history).
- NuGet API (versions, owners, downloads), archive.ubuntu.com package index (`aspnetcore-runtime-10.0` 10.0.12), cli.github.com apt index and signing key (fingerprint re-verified), `git ls-remote` for action tag SHAs, `docker manifest inspect` for lint image digests.
- GitHub docs: REST rate limits (https://docs.github.com/en/rest/using-the-rest-api/rate-limits-for-the-rest-api), best practices (https://docs.github.com/en/rest/using-the-rest-api/best-practices-for-using-the-rest-api), repository rules REST (https://docs.github.com/en/rest/repos/rules), `gh attestation verify` manual (https://cli.github.com/manual/gh_attestation_verify).

### Secondary (MEDIUM confidence)
- Six Labors licence file (https://raw.githubusercontent.com/SixLabors/ImageSharp/main/LICENSE), licence enforcement post (https://sixlabors.com/posts/licence-enforcement-changes/), ImageSharp 4.0.0 announcement (https://sixlabors.com/posts/announcing-imagesharp-400/), ImageSharp discussion 3129 on the key requirement (https://github.com/SixLabors/ImageSharp/discussions/3129).
- Project research documents (pitfalls 13 and 14, stack, architecture) for decisions already taken.

### Tertiary (LOW confidence)
- Search-result summaries on 3.x security-patch continuity (flagged A2).

## Metadata

**Confidence breakdown:**
- Standard stack: HIGH, every version re-queried this session; ImageSharp licensing behaviour reproduced locally.
- Architecture and port inventory: HIGH, every referenced file read and the skeleton prototyped end to end.
- Go-live ordering and GitHub settings: MEDIUM, command shapes verified against a working reference repository but not executed against the new repository.
- Pitfalls: HIGH for items reproduced or taken from the reference's own go-live record; MEDIUM for the profile-name merge identity (needs post-merge verification).
- Owner infrastructure steps: LOW, cannot be probed from here.

**Research date:** 2026-10-04
**Valid until:** 2026-11-03 (GitHub settings APIs, action SHAs and Six Labors licensing policy can move; re-verify action SHAs and the ImageSharp decision at plan time if later)

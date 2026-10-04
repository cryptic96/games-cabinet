# Phase 1: Repo, Guardrails & Walking-Skeleton Deploy - Context

**Gathered:** 2026-10-03
**Status:** Ready for planning

<domain>
## Phase Boundary

A public GitHub repository with enforced guardrails, and a hello page that travels the full release path: semver tag → attested draft release → owner approval in the protected `deploy` environment → the LXC's pull timer finds it, verifies the attestation offline, installs it and health-checks it. A deliberately broken release must roll back automatically. A fresh LXC can be created and provisioned from documented, repeatable scripts, and an image-processing smoke test passes inside it. The router stays LAN-only; public exposure belongs to the hardening phase.

Requirements: OPS-01, OPS-02, OPS-03, OPS-04, OPS-05.

Not in this phase: any BGG call, the cabinet layout, sync, images beyond the smoke test, public exposure, rate limits, CSP and resource caps.

</domain>

<decisions>
## Implementation Decisions

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

### Decided at planning time (after research)
- **D-17:** The image library is **SkiaSharp 4.153.1** with **SkiaSharp.NativeAssets.Linux.NoDependencies 4.153.1** (both MIT, pinned to the same version), replacing ImageSharp. Research reproduced that ImageSharp 4.x fails every Release build and publish without a Six Labors licence key, which would break the release pipeline. The owner chose SkiaSharp over pinning ImageSharp 3.1.12 or wiring a licence key into CI. Verified at planning time: `dotnet publish -c Release -r linux-x64 --self-contained false` with warnings-as-errors succeeds; decode, resize, WebP encode at quality 80 and decode-back round trip works (`SKBitmap.Decode`, `SKBitmap.Resize` with `SKSamplingOptions`, `SKImage.Encode(SKEncodedImageFormat.Webp, 80)`); the publish output carries `SkiaSharp.dll` plus a ~12 MB native `libSkiaSharp.so` that needs only glibc ≥ 2.27, libstdc++ and libgcc_s (all present on Ubuntu 24.04). The image smoke test, the package/release zip expectations, Dependabot grouping (keep both SkiaSharp packages on the same version) and any ImageSharp wording in this phase's docs follow this decision; the project stack research's ImageSharp choice is superseded. — **Reversibility:** cheap now — only the smoke test uses the library in this phase.
- **D-18:** The protected `main` ruleset allows both **merge commits and squash merges**; the method is chosen per PR (merge commit for milestone PRs, squash for small feature PRs). Rebase merges stay disabled.

### Carried forward (locked at project level; not re-discussed)
- Mirror ing-dashboard's pull deploy: SHA-pinned CI and release workflows with `permissions: {}` at top level, strict-semver tag validation, tag must be on `main`, attested zip + sha256 + sigstore bundle on a **draft** release, `publish` job gated by the `deploy` environment re-verifying checksum and attestation. A root-owned installer with `poll` / `install` / `rollback` / `verify`, offline `gh attestation verify` with GitHub tokens unset and `--deny-self-hosted-runners`, attested commit confirmed on `main`, downgrade refusal, `releases/{version}` plus an atomic `current` symlink. Assert a `gh` version floor in provisioning.
- Remove everything database-related and monitoring-related from the reference: PostgreSQL, EF, efbundle, migration manifest and migration-aware rollback branch, backups and `age`, Grafana, Prometheus, node-exporter, Data Protection certificate. Rollback is always allowed. `release-manifest.json` shrinks to version + commit.
- `/health` lives only on a loopback ops Kestrel listener, reports the running version, and **never depends on BGG**; an empty or missing snapshot is healthy. The installer's health check is "`GET` loopback `/health` returns healthy and reports the expected version."
- Deploy outcomes are visible via the journal and `/health` only; deploy/sync failure email is deferred to v2.
- Framework-dependent `linux-x64` publish, ASP.NET Core runtime from the distribution archive, no trimming. `Directory.Build.props` and `global.json` copied from the reference (nullable, warnings-as-errors, deterministic, lock files, `dotnet restore --locked-mode`, Microsoft.Testing.Platform runner).
- The image-processing smoke test proves the image library (SkiaSharp, see D-17) decodes, resizes and encodes WebP inside the real LXC.
- Repo conventions: no planning references outside `.planning/`, `///` XML doc comments only in C# (`/** */` doc blocks only in JS), synthetic-only fixtures, `example.com` placeholders, personal config only in the server env file.

### Claude's Discretion
- What the hello page shows (minimal; showing the running version is useful for rehearsal checks).
- The shape of the image smoke test (an installer selfcheck step, an app subcommand, or a test run on the LXC), as long as it runs inside the real LXC against the deployed release.
- Which ing-dashboard lint checks to port and how to extend them: planning references, C# `//` comments, `runs-on` (no self-hosted), gitleaks, shellcheck, zizmor/actionlint, script tests; add the JS comment rule.
- Number of kept releases, timer `OnBootSec`, health-check timeout, Kestrel ports.
- Inbound nftables rules beyond D-10.

</decisions>

<canonical_refs>
## Canonical References

**Downstream agents MUST read these before planning or implementing.**

### Scope and rules
- `.planning/ROADMAP.md` § Phase 1 — goal, owner prerequisites, success criteria
- `.planning/REQUIREMENTS.md` § Repository & Release — OPS-01..OPS-05
- `.planning/PROJECT.md` — constraints and key decisions (deployment model, privacy, no database)
- `.claude/CLAUDE.md` § Hard rules — planning-reference ban, `///`-only comments, no personal data, branching, BGG etiquette

### Research (decisions above take precedence)
- `.planning/research/SUMMARY.md` § Phase 1 and owner action list
- `.planning/research/ARCHITECTURE.md` §6 Deployment Architecture (what to keep, what to delete, public-exposure deltas) and § Recommended Project Structure
- `.planning/research/PITFALLS.md` Pitfall 13 (copying the release pipeline without its assumptions) and Pitfall 14 (personal data leaking into a public repo)
- `.planning/research/STACK.md` § Core Framework, § Release and Deploy Fit

### Reference project (public GitHub repository `cryptic96/ing-dashboard`, files written as `ing-dashboard:<path>`)
- `ing-dashboard:docs/deploy.md`, `docs/lxc-setup.md`, `docs/releasing.md` — the deploy model to mirror
- `ing-dashboard:docs/github-repository-settings.md` and `build/check-github-settings.sh` — every GitHub-side control with apply and read-back commands
- `ing-dashboard:.github/workflows/ci.yml`, `.github/workflows/release.yml`, `.github/dependabot.yml`, `.github/zizmor.yml`, `.gitleaks.toml`
- `ing-dashboard:build/lint.sh`, `build/lint/checks/*.sh`, `build/package-release.sh`, `build/validate-release-tag.sh`, `build/verify-published-release.sh`
- `ing-dashboard:deploy/provision.sh`, `deploy/provision.d/` (keep `10-packages`, `20-accounts`, `40-services`, `50-firewall`; drop `30-postgresql`, `60-grafana-accounts`), `deploy/versions.env`, `deploy/*.example`
- `ing-dashboard:deploy/bin/ledger-deploy` and `deploy/lib/deploy.sh`, `deploy/lib/common.sh` — installer; note the missing "skip a rolled-back version" behaviour (D-12)
- `ing-dashboard:deploy/systemd/ledger.service`, `ledger-deploy-poll.service`, `ledger-deploy-poll.timer`
- `ing-dashboard:deploy/nftables/ledger.nft.in`, `deploy/traefik/ledger.yml.example`
- `ing-dashboard:deploy/tests/` — logic tests for installer, provisioning and template rendering
- `ing-dashboard:Directory.Build.props`, `global.json`
- `ing-dashboard:.planning/phases/01-secure-platform-release-pipeline/01-11-SUMMARY.md` and `01-12-SUMMARY.md` — go-live lessons (missing `deploy` environment, ruleset ordering, history rewrite, rehearsal evidence)

</canonical_refs>

<code_context>
## Existing Code Insights

### Reusable Assets
- No application code exists in this repo yet; it holds only planning docs, `.gitignore` and `.claude/CLAUDE.md`.
- ing-dashboard supplies nearly everything this phase needs: the workflows, lint framework, packaging and tag-validation scripts, provisioning framework, installer, systemd units, nftables and Traefik templates, and their logic tests. Port them with `ledger` → `cabinet` renames and remove the database/monitoring/backup parts.

### Established Patterns
- Layered `.slnx` (Domain / Repository / Service + test projects) with `Directory.Build.props` (nullable, warnings-as-errors, deterministic, lock files) and `global.json` pinning the SDK and the Microsoft.Testing.Platform runner.
- Idempotent `provision.sh` running numbered modules; the first run writes `/etc/<app>/provision.conf` from an example and stops, the second run applies. Config templates are rendered from it; the env file is never overwritten.
- Dual Kestrel listeners: public app port, loopback-only ops port for `/health`.
- Lint checks are self-testing shell scripts under `build/lint/checks/`, run by `build/lint.sh` in CI.

### Integration Points
- Proxmox host: documented `pct create` step for the unprivileged container.
- Traefik container: a dynamic-config file for the LAN-only router, from the repo's placeholder template.
- GitHub: repository settings applied via `gh api` (D-15), the `deploy` environment, immutable releases, Sigstore public-good instance for attestations.
- The ing-dashboard poller shares the same public IP for unauthenticated GitHub API calls (D-14).

</code_context>

<specifics>
## Specific Ideas

- The owner wants the repo identity pseudonymous end to end: `cryptic96` as owner, copyright holder and commit identity; no real name in URLs, licence or history.
- The rollback rehearsal should be real, not simulated: an actual broken release travelling the full download → verify → install → health-fail → rollback path on the LXC.
- Last time (ing-dashboard) the owner applied the GitHub settings by hand and Claude created the `deploy` environment with approval; this time the owner prefers Claude to apply the full set after a single approval.

</specifics>

<deferred>
## Deferred Ideas

- Outbound (egress) firewall allowlist for the LXC (GitHub, Sigstore, BGG API, image CDN only): consider in the hardening phase.
- Upgrading both this LXC and ing-dashboard's to Ubuntu 26.04 LTS together at a later point.
- Deploy and sync failure email: already tracked as a v2 requirement.

</deferred>

---

*Phase: 01-repo-guardrails-walking-skeleton-deploy*
*Context gathered: 2026-10-03*

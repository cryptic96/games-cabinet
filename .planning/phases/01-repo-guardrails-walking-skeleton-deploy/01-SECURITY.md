---
phase: 01
slug: repo-guardrails-walking-skeleton-deploy
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 2
created: 2026-10-04
---

# Phase 01 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Workstation → public repository | Commits, tags and pushes leave the owner's machine for a public GitHub repository | Source, history, identities; personal data must never cross |
| Pull request → main | Changes reach `main` only through a pull request with required checks | Code and workflow changes |
| GitHub Actions → release | Tag-triggered build, attestation and draft; publish gated by the `deploy` environment | Release artefacts, provenance, OIDC signing identity |
| GitHub release → server | The container pulls published releases unauthenticated and verifies them offline before unpacking | Release zip, checksum, Sigstore bundle |
| Root installer → application | The root installer activates releases and health-checks the unprivileged app | Release files, configuration, health state |
| Reverse proxy → application | Only the reverse proxy may reach the app port; the ops/health listener is loopback-only | HTTP requests, forwarded headers |
| Owner admin → server | Key-only SSH from the owner's workstation; a passwordless-sudo admin account exists until go-public | Shell access |

---

## Threat Register

80 threats from the `<threat_model>` blocks of plans 01-01 to 01-15 (T-01-SC appears in two plans and is kept as two rows).

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| T-01-011 | Information disclosure | git history (all refs) | high | mitigate | `build/scan-history.sh` six checks over all refs incl. gitleaks `--all`; history rewritten and scanned clean | closed |
| T-01-012 | Information disclosure | denylist, expressions file, scan output | high | mitigate | Private files mode 600 outside the repo; scan output masks any matching path (NUL-delimited, unquoted paths) | closed |
| T-01-013 | Tampering | rewritten history | medium | mitigate | Dry run in a mirror, owner approval, commit/date/subject equality, offline backup | closed |
| T-01-014 | Spoofing | commit identities | medium | mitigate | noreply identity check over all refs, incl. annotated taggers | closed |
| T-01-015 | Information disclosure | backup mirror | medium | mitigate | Backup outside the repo with no remote; deleted after the clean public scan | closed |
| T-01-021 | Information disclosure | pre-commit, commit-msg | high | mitigate | Fixed-string case-insensitive scan of added lines incl. binary content (`--text --no-textconv`) and file names | closed |
| T-01-022 | Information disclosure | pre-push (hooks skipped with --no-verify) | high | mitigate | Rescans every unpushed commit incl. two-parent merges (remerge-diff) and binaries; refuses merges with more than two parents; checks tag objects | closed |
| T-01-023 | Information disclosure | hook output | medium | mitigate | Only label, line and pattern number printed; safe labels for paths | closed |
| T-01-024 | Spoofing | commit identity | medium | mitigate | pre-push enforces noreply author/committer/tagger regardless of denylist | closed |
| T-01-025 | Tampering | denylist patterns | low | mitigate | `grep -F` fixed strings everywhere | closed |
| T-01-026 | Denial of service | clones and CI without a denylist | low | accept | Warn-and-allow; CI secrets scan as fallback (AR-01) | closed |
| T-01-031 | Spoofing | ForwardedHeaders | medium | mitigate | XFF/XFP only, ForwardLimit 1, single known proxy, app port only from the proxy | closed |
| T-01-032 | Information disclosure | /health | medium | mitigate | Ops endpoint loopback-only, public 404 tested on real sockets | closed |
| T-01-033 | Denial of service | health contract | medium | mitigate | No external health checks; tested | closed |
| T-01-034 | Elevation of privilege | production switches | medium | mitigate | No force-unhealthy path; the rehearsal break was a settings change | closed |
| T-01-041 | Information disclosure | repository content and history | high | mitigate | gitleaks over tree and all refs with `--redact`; fixtures not exempt; private network ranges detected; self-tests | closed |
| T-01-042 | Tampering | lint tool images | medium | mitigate | Tag plus digest pins, Dependabot updates via PR | closed |
| T-01-043 | Repudiation | planning references | low | mitigate | repo-rules lint with self-tests | closed |
| T-01-044 | Elevation of privilege | shell scripts | medium | mitigate | shellcheck over all tracked shell files | closed |
| T-01-045 | Tampering | self-hosted runner targeting | high | mitigate | Lint refuses any runs-on other than the hosted image; zero runners | closed |
| T-01-051 | Elevation of privilege | provision.conf loader | high | mitigate | Never sourced; refuses unknown keys, substitutions, bad owner/mode; values validated | closed |
| T-01-052 | Tampering | template rendering | medium | mitigate | Safe-value checks; fails on unreplaced placeholders | closed |
| T-01-053 | Tampering | GitHub CLI apt source | high | mitigate | Key fingerprint pin, signed-by, version floor, network test in CI | closed |
| T-01-054 | Spoofing | SSH | high | mitigate | Default-drop firewall, SSH only from admin sources, key-only | closed |
| T-01-055 | Information disclosure | ops listener | medium | mitigate | Only SSH and the app port have firewall rules; ops bound to loopback | closed |
| T-01-056 | Information disclosure | templates and examples | medium | mitigate | Placeholders and documentation ranges; the public handle is owner-confirmed (AR-07) | closed |
| T-01-057 | Denial of service | offline tests | low | mitigate | Host guard records and fails privileged calls | closed |
| T-01-061 | Tampering | image decoding | low | accept | Synthetic in-memory images only (AR-02) | closed |
| T-01-062 | Tampering | release packaging | medium | mitigate | Semver/commit validation, locked restore, zip plus sha256, attested | closed |
| T-01-063 | Elevation of privilege | image-smoke subcommand | low | accept | Runs before the host; sandboxed in selfcheck (AR-03) | closed |
| T-01-SC (01-06) | Tampering | NuGet installs | high | mitigate | Exact versions, committed lock files, locked restore, no vulnerable packages | closed |
| T-01-071 | Tampering | workflow files | high | mitigate | actionlint and zizmor (hash-pin, online audits with token) with self-tests | closed |
| T-01-072 | Tampering | dependency and image updates | medium | mitigate | Dependabot PRs behind required checks | closed |
| T-01-081 | Information disclosure | /health exposure regression | medium | mitigate | Real-socket integration test in required check | closed |
| T-01-082 | Denial of service | health contract regression | medium | mitigate | Integration test | closed |
| T-01-083 | Information disclosure | committed configuration | medium | mitigate | Secret-free configuration test with detector self-test | closed |
| T-01-SC (01-08) | Tampering | NuGet installs (test projects) | high | mitigate | Exact pins, lock files, locked restore; tests run outside the attestation job | closed |
| T-01-091 | Elevation of privilege | release publish | high | mitigate | `environment: deploy` with owner reviewer and tag policy | closed |
| T-01-092 | Tampering | workflow expressions | high | mitigate | ref_name only via env, validated before use | closed |
| T-01-093 | Tampering | third-party actions | high | mitigate | Full-SHA pins, repository SHA-pinning requirement | closed |
| T-01-094 | Tampering | release artefact | high | mitigate | Separate test/package/attest jobs; only attest has write and id-token and runs no test code; digest re-checked; publish re-verifies | closed |
| T-01-095 | Elevation of privilege | default token | medium | mitigate | `permissions: {}`, per-job grants, no persisted credentials, read-only default | closed |
| T-01-096 | Repudiation | releases | medium | mitigate | Drafts only; immutable releases | closed |
| T-01-097 | Elevation of privilege | settings checker | low | mitigate | GET-only, tested with a refusing stub | closed |
| T-01-101 | Tampering | downloaded release zip | high | mitigate | Private copy; checksum bound to the artefact; attestation (signer, tag ref, hosted runners); commit on main; all before unzip | closed |
| T-01-102 | Spoofing | release origin | high | mitigate | Unauthenticated API compare; strict semver | closed |
| T-01-103 | Elevation of privilege | GitHub credentials on the server | high | mitigate | Token variables unset, throwaway gh config, no credentials on host | closed |
| T-01-104 | Denial of service | rollback loop | high | mitigate | Rejected marker before download, only rises; skip without restart proven live | closed |
| T-01-105 | Denial of service | API rate limit | medium | mitigate | 10-minute jittered timer; quiet 403/429 | closed |
| T-01-106 | Tampering | downgrade | medium | mitigate | Install and poll require a strictly newer version | closed |
| T-01-107 | Elevation of privilege | deploy.conf parsing | high | mitigate | Never sourced; numeric values validated as unsigned integers at load before any activation | closed |
| T-01-108 | Tampering | zip path traversal / --from-dir | medium | mitigate | Every entry point verifies and unpacks a private mode-700 copy | closed |
| T-01-109 | Tampering | concurrent runs | medium | mitigate | flock for poll, install and rollback | closed |
| T-01-111 | Elevation of privilege | poll service | high | mitigate | Systemd sandbox asserted by tests and live | closed |
| T-01-112 | Elevation of privilege | app service | high | mitigate | Unprivileged user, empty capability set, strict protection | closed |
| T-01-113 | Elevation of privilege | image smoke as root selfcheck | medium | mitigate | Runs as the app user in a sandbox | closed |
| T-01-114 | Spoofing | SSH | high | mitigate | Selfcheck asserts key-only; firewall limits sources | closed |
| T-01-115 | Elevation of privilege | GitHub-side code on host | high | mitigate | Selfcheck: no runner, no credentials; zero runners | closed |
| T-01-116 | Information disclosure | docs | medium | mitigate | Placeholders only; secrets lint; public handle owner-confirmed (AR-07) | closed |
| T-01-117 | Repudiation | false PASS under pipefail | medium | mitigate | Selfcheck captures output before matching | closed |
| T-01-121 | Information disclosure | first push | high | mitigate | Pre-push gate; fresh public clone scanned 6/6 | closed |
| T-01-122 | Information disclosure | merge-commit identity | high | accept | The original go-live merge commit (non-noreply email) is unreachable from every ref but retrievable by SHA; owner accepts permanently (AR-05) | closed |
| T-01-123 | Elevation of privilege | deploy environment | high | mitigate | Created with reviewer and tag policy before any release run | closed |
| T-01-124 | Tampering | main branch | high | mitigate | Ruleset: PR, required checks, no deletion/force-push, no bypass | closed |
| T-01-125 | Tampering | release tags | high | mitigate | Admin-only tag ruleset; immutable releases | closed |
| T-01-126 | Elevation of privilege | fork PRs and token | medium | mitigate | Outside-contributor approval, read-only token, SHA pinning | closed |
| T-01-127 | Repudiation | settings drift | low | mitigate | Settings read-back 13/13 | closed |
| T-01-131 | Elevation of privilege | release publication | high | mitigate | One approved deploy approval per release run | closed |
| T-01-132 | Tampering | artefact in transit | high | mitigate | Container verifies before unpacking | closed |
| T-01-133 | Information disclosure | evidence in SUMMARY | medium | mitigate | Sanitised SUMMARY | closed |
| T-01-134 | Spoofing | LAN route | medium | mitigate | App port only from the proxy; proxy allow-list verified: a client outside it gets 403, inside gets 200 | closed |
| T-01-135 | Denial of service | Sigstore unreachable | low | accept | Verification fails closed (AR-04) | closed |
| T-01-141 | Denial of service | broken release on the server | high | mitigate | Automatic rollback proven live; previous stays the last good release; manual rollback restores on failure and refuses the rejected release; malformed config stops before activation | closed |
| T-01-142 | Elevation of privilege | hidden unhealthy switch | medium | mitigate | None exists | closed |
| T-01-143 | Repudiation | broken release in public list | low | mitigate | Release notes label the rehearsal | closed |
| T-01-144 | Information disclosure | evidence in SUMMARY | medium | mitigate | Sanitised | closed |
| T-01-151 | Denial of service | stale rejected marker | medium | mitigate | Cleared on a successful newer install | closed |
| T-01-152 | Elevation of privilege | settings drift | medium | mitigate | 13/13 PASS | closed |
| T-01-153 | Elevation of privilege | runners or credentials on server | high | mitigate | None present | closed |
| T-01-154 | Information disclosure | rehearsal commits and notes | medium | mitigate | Scans 6/6; display name owner-accepted (AR-06) | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-01 | T-01-026 | Hooks warn and allow on clones without a denylist; CI secrets scanning is the fallback | owner (plan) | 2026-10-04 |
| AR-02 | T-01-061 | Image decoding in this phase handles only synthetic in-memory images | owner (plan) | 2026-10-04 |
| AR-03 | T-01-063 | The image-smoke subcommand runs before the web host and is sandboxed when run by the selfcheck | owner (plan) | 2026-10-04 |
| AR-04 | T-01-135 | If Sigstore is unreachable, verification fails closed and no release is installed | owner (plan) | 2026-10-04 |
| AR-05 | T-01-122 | The original go-live merge commit carrying a non-noreply email is retrievable by SHA on GitHub; accepted permanently, no purge requested | owner | 2026-10-04 |
| AR-06 | T-01-154 | The owner's first name appears as author display name on GitHub-created squash and merge commits (emails are noreply) | owner (UAT) | 2026-10-04 |
| AR-07 | T-01-056, T-01-116 | The owner's account handle is public by design (repository owner, licence holder, release signer); example configs keep the real repository slug | owner (UAT) | 2026-10-04 |
| AR-08 | (unregistered) | A key-only admin account with passwordless sudo exists on the server, pinned to the owner's workstation, for development. **Expires: revoke or downgrade before the site becomes public** | owner | 2026-10-04 |

*Accepted risks do not resurface in future audit runs.*

---

## Residual Hardening Follow-ups (non-blocking)

- pre-push lets two-parent merges through unscanned on git older than 2.36 (no `--remerge-diff`); fail closed or require a minimum git version.
- A lightweight tag pointing directly at a blob or tree is pushed unscanned; refuse refs that do not resolve to a commit or annotated tag.
- Denylist matching is byte- and locale-dependent (NFD, Latin-1, non-ASCII case under `LC_ALL=C`); set a UTF-8 locale in the scripts.
- Private networks in netmask notation or as a bare `.0` address are not matched by gitleaks; gitleaks git mode does not diff merge commits.
- `10#` prefixes in installer arithmetic are not a second defence; every caller must validate first (the loader does).
- After a failed manual rollback, the restored running release is marked rejected; misleading but no outage.
- `commit-msg` blanks `#` lines even with verbatim cleanup; pre-push catches it.
- Global gitleaks path allowlists apply in git mode too; agent worktrees are now ignored in `.gitignore`.
- The app unit's sandbox is weaker than the selfcheck's; the selfcheck's credential search covers three fixed paths.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-04 | 80 | 71 | 9 (7 blocking) | gsd-security-auditor (full register, ASVS L2) |
| 2026-10-04 | 7 | 5 | 2 (2 blocking) | gsd-security-auditor (after first code-review fix round) |
| 2026-10-04 | 2 | 2 | 0 | gsd-security-auditor (after second fix round) |
| 2026-10-04 | 80 | 80 | 0 | orchestrator consolidation (T-01-134 closed by live allow-list check; T-01-122 accepted) |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-04

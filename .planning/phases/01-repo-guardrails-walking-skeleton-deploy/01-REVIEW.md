---
phase: 01-repo-guardrails-walking-skeleton-deploy
reviewed: 2026-10-04T19:30:04Z
depth: standard
files_reviewed: 77
files_reviewed_list:
  - .githooks/commit-msg
  - .githooks/lib/denylist.sh
  - .githooks/pre-commit
  - .githooks/pre-push
  - .github/dependabot.yml
  - .github/workflows/ci.yml
  - .github/workflows/release.yml
  - .github/zizmor.yml
  - .gitignore
  - .gitleaks.toml
  - Cabinet.Domain/BuildInfo.cs
  - Cabinet.Domain/Cabinet.Domain.csproj
  - Cabinet.IntegrationTests/Cabinet.IntegrationTests.csproj
  - Cabinet.IntegrationTests/HealthEndpointTests.cs
  - Cabinet.IntegrationTests/HelloPageTests.cs
  - Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs
  - Cabinet.Repository/Cabinet.Repository.csproj
  - Cabinet.Repository/Images/ImageSmoke.cs
  - Cabinet.Service/Cabinet.Service.csproj
  - Cabinet.Service/Hosting/OpsEndpoint.cs
  - Cabinet.Service/Pages/Index.cshtml
  - Cabinet.Service/Pages/_ViewImports.cshtml
  - Cabinet.Service/Program.cs
  - Cabinet.Service/appsettings.Production.json
  - Cabinet.Service/appsettings.json
  - Cabinet.Service/wwwroot/css/site.css
  - Cabinet.UnitTests/BuildInfoTests.cs
  - Cabinet.UnitTests/Cabinet.UnitTests.csproj
  - Cabinet.UnitTests/Configuration/CommittedConfigurationTests.cs
  - Cabinet.UnitTests/Hosting/OpsEndpointTests.cs
  - Cabinet.UnitTests/Images/ImageSmokeTests.cs
  - Cabinet.slnx
  - Directory.Build.props
  - global.json
  - build/check-github-settings.sh
  - build/lint.sh
  - build/lint/checks/10-repo-rules.sh
  - build/lint/checks/20-workflows.sh
  - build/lint/checks/30-shell.sh
  - build/lint/checks/40-secrets.sh
  - build/lint/checks/50-script-tests.sh
  - build/lint/compose.yaml
  - build/lint/lib.sh
  - build/package-release.sh
  - build/scan-history.sh
  - build/tests/check-github-settings-test.sh
  - build/tests/githooks-test.sh
  - build/tests/package-release-e2e-test.sh
  - build/tests/validate-release-tag-test.sh
  - build/validate-release-tag.sh
  - build/verify-published-release.sh
  - deploy/bin/cabinet-deploy
  - deploy/bin/cabinet-selfcheck
  - deploy/cabinet.env.example
  - deploy/deploy.conf.example
  - deploy/lib/common.sh
  - deploy/lib/deploy.sh
  - deploy/nftables/cabinet.nft.in
  - deploy/provision.conf.example
  - deploy/provision.d/10-packages.sh
  - deploy/provision.d/20-accounts.sh
  - deploy/provision.d/40-services.sh
  - deploy/provision.d/50-firewall.sh
  - deploy/provision.sh
  - deploy/systemd/cabinet-deploy-poll.service
  - deploy/systemd/cabinet-deploy-poll.timer
  - deploy/systemd/cabinet.service
  - deploy/tests/cabinet-deploy-e2e-test.sh
  - deploy/tests/cabinet-deploy-logic-test.sh
  - deploy/tests/lib/host-guard.sh
  - deploy/tests/provision-logic-test.sh
  - deploy/tests/render-templates-test.sh
  - deploy/tests/sandboxing-test.sh
  - deploy/tests/verify-rejects-tampered-artifact-network-test.sh
  - deploy/tests/versions-network-test.sh
  - deploy/traefik/cabinet.yml.example
  - deploy/versions.env
findings:
  critical: 2
  warning: 13
  info: 12
  total: 27
status: issues_found
---

# Phase 01: Code Review Report

**Reviewed:** 2026-10-04T19:30:04Z
**Depth:** standard
**Files Reviewed:** 77
**Status:** issues_found

## Narrative Findings (AI reviewer)

## Summary

I gave the root installer (`deploy/bin/cabinet-deploy`, `deploy/lib/*.sh`), provisioning, the release and CI workflows, the git hooks, the history scanner, the lint suite and the .NET host the most attention. The C# surface is small and sound: the ops port filter, the loopback-only guard and BuildInfo parsing are correct. The workflows have no expression-injection paths, because every `${{ }}` that reaches a shell goes through `env:`. The attestation pins the signer workflow, the tag ref and GitHub-hosted runners, and the installer also checks that the commit is on main. Together these make a solid supply-chain gate.

The problems are in the installer's state handling and in how it uses config values:

- **State handling (CR-01).** After an automatic rollback, the installer records the failed release as `previous`. A later plain `cabinet-deploy rollback` then reinstalls the broken build and leaves the site down.
- **Config values (CR-02).** Numeric config values go straight into bash arithmetic without being checked. That allows command execution from a config file the installer says it never runs. It also means a simple typo such as `60s` stops the installer after it switches releases but before the health check, with no rollback.

The personal-data guardrails also have real gaps: binary files are never scanned, nobody checks the tagger identity, merge resolutions are skipped, the scanner prints raw paths, and some gitleaks allowlists are too broad. The online zizmor audits never run in CI because the token is not passed into the container.

I verified CR-02 by running the same arithmetic pattern in bash (see the CR-02 notes). I traced CR-01 through `cabinet_activate_release`. No existing test asserts what `previous` holds after an automatic rollback.

## Critical Issues

### CR-01: Auto-rollback records the rejected release as `previous`, so `cabinet-deploy rollback` reinstalls it and leaves the site down

**File:** `deploy/lib/deploy.sh:240-244`, `deploy/lib/deploy.sh:343`, `deploy/lib/deploy.sh:414-418`, `deploy/bin/cabinet-deploy:203-227`
**Issue:** `cabinet_activate_release` always writes the release the `current` link points to into `state/previous` before it swaps the link. During an automatic rollback, `cabinet_rollback_release` calls `cabinet_activate_release` while `current` still points at the release that just failed. Here is what happens when 1.0.0 is active and 1.1.0 fails its health check:

1. Activating 1.1.0 writes `previous=1.0.0`.
2. The health check fails, and the rollback activates 1.0.0. That writes `previous=1.1.0`, the broken release.
3. The operator runs `cabinet-deploy rollback` with no version, which is the documented "go back" command. `cmd_rollback` reads `previous=1.1.0` and activates the broken release again.
4. `cabinet_rollback_release` returns 1 when the health check fails, but it never restores the release it replaced. The site stays on the broken 1.1.0.
5. `cmd_rollback` records nothing as rejected, because `active(1.0.0) > target(1.1.0)` is false.
6. Every later poll sees `latest == active == 1.1.0` and logs "up to date".

So a single command turns a self-healed incident into an outage that polling never fixes. Also, from then on the prune protection guards the broken release directory instead of the last good one.

**Fix:** Do not record `previous` when the activation is itself a rollback. If an explicit rollback fails its health check, restore the release it replaced. Refuse to roll back to a rejected version unless the operator names it explicitly.

```bash
cabinet_activate_release() {
  local version="$1" releases_dir="$2" current_link="$3" state_dir="$4" record_previous="${5:-yes}"
  ...
  if [ "$record_previous" = yes ] && [ -L "$current_link" ]; then
    printf '%s\n' "$(basename "$(readlink -f "$current_link")")" > "${state_dir}/previous"
  fi
  ...
}

cabinet_rollback_release() {
  ...
  local replaced=""
  [ -L "$current_link" ] && replaced="$(basename "$(readlink -f "$current_link")")"
  cabinet_activate_release "$version" "$releases_dir" "$current_link" "$state_dir" no
  cabinet_restart_app
  if ! cabinet_wait_for_health "$ops_url" "$version" "$health_timeout"; then
    cabinet_log "ERROR: release ${version} did not report healthy; restoring ${replaced}"
    [ -n "$replaced" ] && cabinet_activate_release "$replaced" "$releases_dir" "$current_link" "$state_dir" no \
      && cabinet_restart_app
    return 1
  fi
}
```

In `cmd_rollback`, refuse a no-argument rollback when `previous` is at or below the rejected version (`cabinet_is_rejected "$version"`). Add a logic test that checks `state/previous` after an automatic rollback.

### CR-02: Numeric config values are evaluated as bash arithmetic: command execution from the config file, and a malformed value aborts the installer after activation with no rollback

**File:** `deploy/lib/common.sh:44-93`, `deploy/bin/cabinet-deploy:48-58`, `deploy/lib/deploy.sh:281`, `deploy/lib/deploy.sh:311-312`
**Issue:** `cabinet_load_conf` promises the file is "never sourced". It reads `CABINET_KEEP_RELEASES` and `CABINET_HEALTH_TIMEOUT_SECONDS` as free text. `load_configuration` takes the env var `CABINET_HEALTH_INTERVAL_SECONDS` without validation either. These values are later used as bare names in arithmetic: `$(( $(date +%s) + timeout_seconds ))` and `$(( total > keep ? total - keep : 0 ))`. Bash evaluates the variable's *value* as an arithmetic expression, which includes array subscripts containing `$(...)`.

Running the same pattern in bash confirmed two problems:

1. **Code execution.** A value like `PATH[$(cmd)0]` runs `cmd` as root. That breaks the loader's "never executes the file" contract and the allow-list design meant to give defence in depth.
2. **Fail-open on typos.** The function runs inside `|| install_status=$?`, so `set -e` is off, but an arithmetic expansion error still ends the whole script. A value of `CABINET_HEALTH_TIMEOUT_SECONDS=60s` printed `value too great for base` and exited 1 *after* `cabinet_activate_release` and `cabinet_restart_app` had already run. The new release stays active without a health check, without a rollback and without a rejected marker. A non-numeric `CABINET_KEEP_RELEASES` hits `set -u` or evaluates to 0 after a successful install, so pruning removes every release except the active and previous ones.

**Fix:** Validate every numeric key once, at load time, before any state changes. Never feed unvalidated text to `$(( ))`.

```bash
cabinet_require_uint() {
  local name="$1" value="${!1}"
  [[ "$value" =~ ^[1-9][0-9]{0,5}$ ]] || cabinet_die "${name} must be a positive integer"
}

load_configuration() {
  ...
  cabinet_load_conf "$CONF_PATH"
  cabinet_require_uint CABINET_KEEP_RELEASES
  cabinet_require_uint CABINET_HEALTH_TIMEOUT_SECONDS
  CABINET_HEALTH_INTERVAL_SECONDS="${CABINET_HEALTH_INTERVAL_SECONDS:-2}"
  cabinet_require_uint CABINET_HEALTH_INTERVAL_SECONDS
  [[ "$CABINET_OPS_URL" =~ ^http://(127\.0\.0\.1|\[::1\]|localhost):[0-9]{1,5}$ ]] \
    || cabinet_die "CABINET_OPS_URL must be a loopback http URL"
}
```

Inside the arithmetic, also expand as `$(( now + 10#${timeout_seconds} ))` so a value is never looked up as a variable name.

## Warnings

### WR-01: Checksum verification trusts the file names inside the downloaded `.sha256`

**File:** `deploy/bin/cabinet-deploy:109-116` (also `.github/workflows/release.yml:117`, `build/verify-published-release.sh:105`)
**Issue:** `sha256sum --check --status cabinet-X.zip.sha256` checks whichever files the checksum file *lists*. It is not bound to `$artifact`. A `.sha256` that lists a different file with a correct hash, or an absolute path such as a root-readable system file, passes without the artifact ever being hashed. The attestation step still protects the zip, so the checksum layer gives no independent assurance. The installer's log message "checksum mismatch ... refusing to unpack" suggests it does.
**Fix:** Compare the artifact's own hash with the first field of the file:

```bash
local expected actual
expected="$(awk 'NR==1 {print $1}' "$checksum_file")"
[[ "$expected" =~ ^[0-9a-f]{64}$ ]] || cabinet_die "malformed checksum file"
actual="$(sha256sum -- "$artifact" | awk '{print $1}')"
[ "$expected" = "$actual" ] || cabinet_die "checksum mismatch for $(basename "$artifact"), refusing to unpack"
```

### WR-02: `install --from-dir` verifies and unpacks from a caller-chosen path (TOCTOU as root)

**File:** `deploy/bin/cabinet-deploy:147-176`, `deploy/lib/deploy.sh:376`
**Issue:** With `--from-dir`, root checks the checksum and attestation of `${from_dir}/cabinet-X.zip` in place, then `unzip`s the same path later. If the directory can be written by a less-privileged account, for example a home directory, `/tmp` or a shared upload directory, that account can swap the zip between the attestation check and `unzip`. Root then installs and runs unverified code as the service.
**Fix:** First copy the three files into a fresh root-owned directory, for example `mktemp -d "${DOWNLOAD_DIR}/manual.XXXXXX"` with mode 700. Verify the copy and unpack only the copy.

### WR-03: The rejected-version watermark can be lowered, so a known-bad release gets installed again

**File:** `deploy/lib/deploy.sh:187-196`, `deploy/bin/cabinet-deploy:183-186`, `deploy/bin/cabinet-deploy:222-225`
**Issue:** `cabinet_record_rejected_version` overwrites the marker unconditionally, and any successful install clears it. Example: 1.1.0 is active, 1.2.0 fails and is rolled back (`rejected=1.2.0`), then the operator rolls back to 1.0.0. `cmd_rollback` records `rejected=1.1.0`. On the next poll, 1.2.0 is newer than 1.1.0, so the installer tries the known-bad 1.2.0 again and takes another restart cycle on the live site. A manual install of any version below the watermark clears the marker in the same way.
**Fix:** Keep the maximum. Only write the marker when the new value is greater than the recorded one:

```bash
local current; current="$(cabinet_read_rejected_version "$state_dir")"
if [ -n "$current" ] && ! cabinet_semver_gt "$version" "$current"; then return 0; fi
```

Clear it only when the installed version is at or above the recorded one.

### WR-04: The download directory is never pruned

**File:** `deploy/bin/cabinet-deploy:150-157`
**Issue:** Every release stays forever in `/var/lib/cabinet-deploy/downloads/<version>/`: the zip, which carries SkiaSharp native assets, plus its checksum and bundle. `CABINET_KEEP_RELEASES` limits only the unpacked releases. On a small LXC, this slowly fills the state volume.
**Fix:** Delete `${download_dir}` once `cabinet_install_verified_release` returns, whatever the outcome. Or download into a `mktemp -d` under `DOWNLOAD_DIR` and remove it with a `trap`.

### WR-05: Release build runs the whole test suite with `contents: write` and `id-token: write`, between packaging and attestation

**File:** `.github/workflows/release.yml:15-60`
**Issue:** The `build` job packages the zip on line 43, then runs `dotnet test` on line 48, then attests on line 53, all in the same workspace and with OIDC token minting available to every step. Test code and every test-only NuGet package, including its MSBuild targets and analyzers, run after the artifact exists and before it is signed. Any of them can rewrite `artifacts/release/cabinet-X.zip` and its `.sha256`, and the provenance then certifies the tampered bytes. The test dependency set is now part of the release trust boundary, and it grows as more packages are added (Playwright, NSubstitute).
**Fix:** Split the work into jobs:

1. A `test` job with `contents: read` only.
2. A `package` job with `needs: test`, `contents: read`, no tests, that uploads the zip as a workflow artifact.
3. An `attest-and-draft` job that downloads that artifact and holds the only `id-token: write` / `attestations: write` / `contents: write` permissions.

Optionally, hash the zip in `package` and check it again in the attest job.

### WR-06: zizmor's online audits never run in CI because `GH_TOKEN` is not passed into the container

**File:** `build/lint/lib.sh:10-15`, `build/lint/compose.yaml:9-14`, `build/lint/checks/20-workflows.sh:29-35`
**Issue:** `zizmor_extra_args` drops `--offline` when `GH_TOKEN` is set on the host, and CI sets it in `ci.yml` for exactly this reason. But `docker compose run` passes no host environment unless the service declares it, and neither `compose.yaml` nor `lint_compose` forwards `GH_TOKEN`. zizmor inside the container never sees a token, so the online audits (impostor-commit, ref-confusion, known-vulnerable-actions) are skipped while lint reports PASS. Hash pinning is the main control here, and the impostor-commit check is what catches a hash that points at a fork commit.
**Fix:** Declare the variable on the service, which passes it through without a value in the file:

```yaml
  zizmor:
    environment:
      - GH_TOKEN
```

Add a self-test that fails when the token is present but zizmor reports offline mode.

### WR-07: Binary files bypass every personal-data scan

**File:** `.githooks/pre-commit:28`, `.githooks/pre-push:74-75`, `build/scan-history.sh:135`, `build/scan-history.sh:139`
**Issue:** The hooks feed `git diff` / `git diff-tree -p` output to the denylist. For any file git considers binary (NUL bytes, or `binary` in `.gitattributes`), that output is just `Binary files ... differ`. `scan-history.sh` uses `git grep -I`, which explicitly skips binary blobs. Screenshots and images are named in the project's personal-data rule, and PNG/JPEG/WebP metadata (tEXt/EXIF author or hostname fields) is a typical leak. None of the four layers checks it.
**Fix:** Scan the blob bytes. In the hooks, pass `--text` to `git diff` / `git diff-tree`. In `scan-history.sh`, drop `-I` and use `git grep -a`. Or add a separate pass that greps each added binary blob with `git cat-file blob <sha> | grep -a -i -F -f "$PATTERN_FILE"`. Reports must still show only locations.

### WR-08: Annotated tag objects are never checked for identity, and pre-push never scans them

**File:** `.githooks/pre-push:28-35`, `.githooks/pre-push:43-58`, `build/scan-history.sh:188-201`
**Issue:** Release tags are annotated objects pushed to the public repository. They carry a tagger name and email plus a message. `pre-push` resolves a pushed tag to its commits with `git rev-list`, so the tag object itself is never identity-checked or scanned against the denylist. `check_noreply_identities` in `scan-history.sh` reads only `git log` commit identities. A release tag made with a personal email, for example from a machine without the repository-local identity, passes both the local gate and the history audit.
**Fix:** In `pre-push`, when `git cat-file -t "$local_sha"` is `tag`, check that `git for-each-ref`/`cat-file -p` tagger email ends with the noreply suffix, and scan the tag message and tagger with `denylist_scan`. In `scan-history.sh`, add the tagger email from `for-each-ref --format='%(taggeremail)' refs/tags` to the noreply check.

### WR-09: Merge commits' own content (conflict resolutions) is never scanned

**File:** `.githooks/pre-push:64-66`
**Issue:** The comment reasons that merged-in commits are scanned one by one. But text written while *resolving* a conflict, or added in an "evil merge", exists only in the merge commit. Both are skipped, so personal data introduced while resolving a conflict reaches the public remote unscanned.
**Fix:** Scan what the merge introduced compared with an automatic remerge (`git show --remerge-diff --format= "$commit"`, git 2.36 or later). Or, at minimum, scan `git diff-tree -p --cc "$commit"`, with the same added-lines filtering.

### WR-10: `scan-history.sh` prints raw paths, which can echo a denylisted value

**File:** `build/scan-history.sh:139-140`, `build/scan-history.sh:213-216`
**Issue:** For content hits the finding is `$hit`, a raw `commit:path`. The absolute-path check prints `commit:path:line`. If the path itself contains a denylisted value, for example a fixture named after a real location or person, the scanner prints it to the terminal and to any captured log. The hooks deliberately hide such paths with `denylist_safe_label`, and the scanner header promises not to print the denylist. The file-name check on lines 143-146 already avoids printing names.
**Fix:** Source `.githooks/lib/denylist.sh`, or duplicate its logic, and pass every path through `denylist_safe_label` before printing. When a path matches, print `commit <sha> file number N` instead.

### WR-11: gitleaks allowlists all of `deploy/tests/fixtures/` for every rule

**File:** `.gitleaks.toml:69-73`
**Issue:** The allowlist is meant for "public Sigstore bundle fixtures", but its path pattern covers every file under `deploy/tests/fixtures/`, for every rule: email, private IPv4, internal hostname and secrets. `.gitignore` also un-ignores `deploy/tests/fixtures/*.env`. Fixtures are the most likely place for copied-in real data, so this turns off the safety net exactly where it is needed.
**Fix:** Narrow it to the specific files and rules, for example `paths = ['''^deploy/tests/fixtures/[^/]+\.sigstore\.jsonl?$''']`, scoped with `[[rules.allowlists]]` under only the rule ids that actually fire on the bundle.

### WR-12: The `private-ipv4` rule deliberately ignores `.0` addresses, so homelab network ranges are not detected

**File:** `.gitleaks.toml:7-14`
**Issue:** The last octet must start with 1-9, so `10.x.y.0/24` and `192.168.z.0/24` never match. The project rules explicitly forbid "homelab IP addresses or **network ranges**", and the real `CABINET_ADMIN_SSH_SOURCES` value is exactly a CIDR network address. If it leaked into docs or tests, gitleaks would stay silent.
**Fix:** Add a separate CIDR rule (`\b(?:10|172\.(?:1[6-9]|2\d|3[01])|192\.168)\.\d{1,3}\.\d{1,3}/\d{1,2}\b`) with an allowlist that covers only the three RFC 1918 block descriptions (`10.0.0.0/8`, `172.16.0.0/12`, `192.168.0.0/16`).

### WR-13: The owner's real account handle is committed in example and tooling files

**File:** `deploy/deploy.conf.example:8`, `deploy/provision.conf.example:16`, `build/check-github-settings.sh:14`, `build/lint/checks/10-repo-rules.sh:30`
**Issue:** The repository owner's real handle appears in four tracked files besides `LICENSE`. The owner has noted that this handle is also their BGG username, and the project rules forbid BGG usernames anywhere in the repository, with examples limited to placeholders. `provision.conf.example` calls itself a "Placeholder-only template" and says provisioning "refuses to apply while a placeholder remains". Yet its `CABINET_GITHUB_REPO` holds a real value that passes `provision_validate_repo_slug`. A forked deployment that leaves it unchanged would silently poll and trust another person's releases.
**Fix:** Use `CABINET_GITHUB_REPO=CHANGE-ME` in both examples, which fails the slug check by design. In `check-github-settings.sh`, require `CABINET_GITHUB_REPO` or derive it with `gh repo view --json nameWithOwner`. In `10-repo-rules.sh`, check the copyright line's shape (`^Copyright \(c\) [0-9]{4} .+$`) instead of hard-coding the holder. Whether `LICENSE` itself names the handle is the owner's call.

## Info

### IN-01: Re-provisioning silently overwrites operator-tuned `deploy.conf`

**File:** `deploy/provision.d/40-services.sh:148-155`
**Issue:** `deploy.conf` is re-rendered from the example on every run, so local changes to `CABINET_KEEP_RELEASES` or `CABINET_HEALTH_TIMEOUT_SECONDS` are reset without warning. `deploy.conf.example` tells the operator to "copy" and edit it.
**Fix:** Keep existing keys when they are present, or document that the file is fully managed and remove the "copy to" instruction.

### IN-02: The deploy config loader silently ignores unknown or misspelled keys

**File:** `deploy/lib/common.sh:72-85`
**Issue:** A typo such as `CABINET_KEEP_RELEASE=10` is dropped and the default applies. `provision.sh` rejects unknown keys, so the two loaders behave differently.
**Fix:** Call `cabinet_die` on an unknown `CABINET_*` key, the way `_provision_parse_kv_file` does.

### IN-03: `cabinet-selfcheck` reads `CABINET_OPS_URL` without stripping the quotes the installer accepts

**File:** `deploy/bin/cabinet-selfcheck:124-131`
**Issue:** `CABINET_OPS_URL="http://127.0.0.1:5081"` is valid for `cabinet_load_conf`, but the selfcheck curls the literal quoted string and reports a false FAIL.
**Fix:** Source `common.sh` and use `cabinet_load_conf`, or strip one matching pair of surrounding quotes.

### IN-04: The "deterministic" release zip is not reproducible

**File:** `build/package-release.sh:80-83`
**Issue:** `zip -X` still stores each file's DOS mtime, and `sort` follows the caller's locale. Two builds of the same commit produce different zips.
**Fix:** Before zipping, `find "$STAGE_DIR" -exec touch -h -d "@${SOURCE_DATE_EPOCH:-0}" {} +`, and use `LC_ALL=C sort`.

### IN-05: The comment-rule lint misses C# block comments and Razor/CSS comments

**File:** `build/lint/checks/10-repo-rules.sh:20`, `build/lint/checks/10-repo-rules.sh:282`
**Issue:** `/* ... */` in `.cs`, `@* ... *@` in `.cshtml` and CSS comments are not checked, and `////` passes the pattern. The project rule is "`///` only".
**Fix:** Add `/\*` for `*.cs`, `@\*` for `*.cshtml`, and decide on a rule for CSS.

### IN-06: The gitleaks image is pinned in two places and only one is tracked by Dependabot

**File:** `build/scan-history.sh:20`, `build/lint/compose.yaml`
**Issue:** Dependabot bumps the compose pin. The copy in `scan-history.sh` drifts.
**Fix:** Read the image reference from `compose.yaml`, or run through `lint_compose`.

### IN-07: The integration test port picker can return the same port twice

**File:** `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs:21-22`, `Cabinet.IntegrationTests/Infrastructure/CabinetWebApplicationFactory.cs:110-117`
**Issue:** Each listener is stopped before the next port is requested, so `PublicPort == OpsPort` can happen, which makes the test flaky. The bash e2e test loops to avoid this, but the C# factory does not.
**Fix:** Hold both listeners open while picking ports, or loop until the ports differ.

### IN-08: The installer probes a generic `/usr/local/lib/common.sh` before its own library directory

**File:** `deploy/bin/cabinet-deploy:13-17`
**Issue:** Once installed in `/usr/local/sbin`, `${SCRIPT_DIR}/../lib/common.sh` resolves to `/usr/local/lib/common.sh`. Any unrelated file at that common path would be sourced by the root installer instead of `/usr/local/lib/cabinet/common.sh`.
**Fix:** Probe `${SCRIPT_DIR}/../lib/cabinet/common.sh` or a repository marker, such as `${SCRIPT_DIR}/../provision.sh`, before using the checkout layout.

### IN-09: The app unit's sandbox is weaker than the one the selfcheck calls "the production sandbox"

**File:** `deploy/systemd/cabinet.service:18-28`, `deploy/bin/cabinet-selfcheck:216-237`
**Issue:** `cabinet.service` lacks `PrivateDevices=yes`, `RestrictSUIDSGID=yes`, `RestrictRealtime=yes`, `ProtectProc=invisible` and a `SystemCallFilter=@system-service`. The image smoke therefore runs under different restrictions from the app.
**Fix:** Add these settings to the unit and build the selfcheck's `systemd-run` properties from the same list.

### IN-10: Attestation output is parsed with stderr mixed in

**File:** `deploy/lib/deploy.sh:32-48`
**Issue:** `2>&1` sends any gh warning (TUF, update notice, deprecation) into the JSON that `jq` parses. This fails closed, but it would block every deploy after a gh upgrade that adds a stderr notice.
**Fix:** Capture stderr to a separate temp file, and log it only on failure.

### IN-11: A missing option value makes the installer exit silently

**File:** `deploy/bin/cabinet-deploy:85-87`, `deploy/bin/cabinet-deploy:127`
**Issue:** When the option is the last argument, `shift 2` fails under `set -e`, and the script exits 1 with no message, for example `install v1.2.3 --from-dir`.
**Fix:** `[ $# -ge 2 ] || cabinet_die "--from-dir needs a directory"` before shifting.

### IN-12: `pre-push` excludes commits reachable from *any* remote, not just the push target

**File:** `.githooks/pre-push:31`, `.githooks/pre-push:33`
**Issue:** `--not --remotes` treats commits on another configured remote, such as a private mirror, as already published. They are not scanned when pushed to the public origin for the first time.
**Fix:** Use `--not --remotes="$remote_name"`. The remote name is `$1` of the hook.

---

_Reviewed: 2026-10-04T19:30:04Z_
_Reviewer: Claude (gsd-code-reviewer)_
_Depth: standard_

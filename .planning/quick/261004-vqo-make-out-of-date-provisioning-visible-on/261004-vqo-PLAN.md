---
phase: quick-261004-vqo
plan: 01
type: execute
wave: 1
depends_on: []
files_modified:
  - deploy/lib/common.sh
  - deploy/bin/cabinet-deploy
  - deploy/provision.d/40-services.sh
  - deploy/tests/cabinet-deploy-logic-test.sh
  - deploy/bin/cabinet-selfcheck
  - deploy/tests/sandboxing-test.sh
  - build/tests/package-release-e2e-test.sh
  - docs/deploy.md
  - docs/lxc-setup.md
autonomous: true
requirements:
  - quick-261004-vqo

estimate:
  tokens: 48000
  raw_tokens: 48000
  tasks: 3
  confidence: low

must_haves:
  truths:
    - "A cabinet-deploy poll on a host whose installed scripts (/usr/local/sbin), libraries (/usr/local/lib/cabinet) or systemd units (/etc/systemd/system) are missing or differ from the active release's own deploy/ copy (/opt/cabinet/current/deploy) logs exactly one line containing 'WARNING: provisioning is out of date; re-run deploy/provision.sh from the active release'"
    - "With drift present, poll install decisions, rollback behaviour and exit codes are exactly what they are without drift (up to date -> 0 and no install, newer tag -> install by tag, 500 -> 1, 404 -> 0)"
    - "A poll on a host whose installed copies match, or whose active release has no deploy/ directory, or with no active release at all, logs no provisioning warning"
    - "cabinet-selfcheck prints a single PASS when every installed script, library and unit matches the active release, and one FAIL line per installed file that is missing or differs, naming that file"
    - "cabinet-selfcheck FAILs when there is no active release deploy/ copy to compare against, and when the loaded deploy library cannot perform the comparison"
    - "Rendered or operator-owned files (deploy.conf, cabinet.env, provision.conf, nftables.conf) and installed files the release no longer ships are never compared"
    - "Neither the poll nor the selfcheck ever writes to, copies from or executes anything in the release's deploy/ tree, and provisioning is never run automatically"
    - "docs/deploy.md and docs/lxc-setup.md explain the warning, the selfcheck check, exactly what is compared, what is not, and how to re-run provisioning from the active release"
  artifacts:
    - path: "deploy/lib/common.sh"
      provides: "cabinet_provisioning_drift RELEASE_DEPLOY_DIR INSTALL_ROOT (prints drifted installed paths; returns 0 match, 1 drift, 2 nothing to compare against)"
      contains: "cabinet_provisioning_drift()"
    - path: "deploy/bin/cabinet-deploy"
      provides: "cmd_poll logs the single out-of-date warning line when drift exists"
      contains: "provisioning is out of date; re-run deploy/provision.sh from the active release"
    - path: "deploy/bin/cabinet-selfcheck"
      provides: "check_provisioning_current, called from main"
      contains: "check_provisioning_current"
    - path: "deploy/tests/cabinet-deploy-logic-test.sh"
      provides: "relocated-root tests for the comparison function and the poll warning"
    - path: "deploy/tests/sandboxing-test.sh"
      provides: "relocated-root tests for the selfcheck provisioning check"
    - path: "docs/deploy.md"
      provides: "rewritten 'When to re-run provisioning' section"
    - path: "docs/lxc-setup.md"
      provides: "updated section 10 bullet list and section 12"
  key_links:
    - from: "deploy/bin/cabinet-deploy cmd_poll"
      to: "cabinet_provisioning_drift in deploy/lib/common.sh"
      via: "call with \"${CURRENT_LINK}/deploy\" and \"$DEPLOY_ROOT\", status captured so set -e never trips"
      pattern: "cabinet_provisioning_drift \"\\$\\{CURRENT_LINK\\}/deploy\""
    - from: "deploy/bin/cabinet-selfcheck"
      to: "deploy/lib/common.sh"
      via: "same lib-dir resolution as cabinet-deploy: ../lib next to the script, else /usr/local/lib/cabinet"
      pattern: "/usr/local/lib/cabinet"
    - from: "deploy/bin/cabinet-selfcheck main"
      to: "check_provisioning_current"
      via: "direct call in the check sequence"
      pattern: "^  check_provisioning_current$"
    - from: "cabinet_provisioning_drift source globs"
      to: "deploy/provision.d/40-services.sh install destinations"
      via: "identical mapping bin/* -> usr/local/sbin, lib/*.sh -> usr/local/lib/cabinet, systemd/*.service|*.timer -> etc/systemd/system"
      pattern: "usr/local/lib/cabinet"
---

<objective>
Make out-of-date provisioning visible on the server. The installed installer
(`/usr/local/sbin/cabinet-deploy`), the other installed scripts, the installed
shell libraries (`/usr/local/lib/cabinet/*.sh`) and the installed systemd units
(`/etc/systemd/system/cabinet.service`, `cabinet-deploy-poll.service`,
`cabinet-deploy-poll.timer`) are compared byte-for-byte with the active
release's own `deploy/` copy under `/opt/cabinet/current/deploy`.
`cabinet-deploy poll` logs one warning line per run while they differ, and
`cabinet-selfcheck` FAILs naming each differing file. Nothing is ever copied or
run automatically: the operator re-runs provisioning from the active release.

Purpose: releases only update the application by design, so a release that
changed the installer kept running the old installer with no signal at all.
This closes that blind spot without weakening the trust model.

Output: one shared, read-only comparison function in `deploy/lib/common.sh`,
the poll warning, the selfcheck check, relocated-root tests for both (each
failing without the change), a packaging assertion that releases keep shipping
`deploy/`, and updated operator docs.

Verified facts the executor can rely on (do not re-investigate):
- Releases already ship `deploy/`: `build/package-release.sh` lines 72-75 copy
  the whole `deploy/` tree except `tests/` into the zip, and
  `cabinet_install_verified_release` unpacks the whole zip into
  `/opt/cabinet/releases/<version>`, so `/opt/cabinet/current/deploy/{bin,lib,systemd}`
  exists for every release. No packaging change is needed; Task 3 only pins
  this with an assertion.
- `deploy/provision.d/40-services.sh` installs verbatim (via
  `services_install_file`, `cmp -s` identity): `deploy/bin/*` ->
  `/usr/local/sbin/<name>`, `deploy/lib/*.sh` -> `/usr/local/lib/cabinet/<name>`,
  `deploy/systemd/*.service` and `deploy/systemd/*.timer` ->
  `/etc/systemd/system/<name>`. These are the only files compared.
- Rendered files are NOT verbatim and must never be compared:
  `/etc/cabinet/deploy.conf` (rendered from `deploy.conf.example` with the
  repository), `/etc/nftables.conf` (rendered from `nftables/cabinet.nft.in`),
  `/etc/cabinet/cabinet.env`, `/etc/cabinet/provision.conf`,
  `/etc/apt/apt.conf.d/51cabinet-unattended-upgrades`.
- `cabinet-deploy` resolves its libraries from `../lib` next to the script when
  `../lib/common.sh` exists, else `/usr/local/lib/cabinet`. The selfcheck must
  use the same rule.
</objective>

<execution_context>
@$HOME/.claude/gsd-core/workflows/execute-plan.md
@$HOME/.claude/gsd-core/templates/summary.md
</execution_context>

<context>
@.planning/STATE.md
@.claude/CLAUDE.md
@deploy/lib/common.sh
@deploy/bin/cabinet-deploy
@deploy/bin/cabinet-selfcheck
@deploy/provision.d/40-services.sh
@deploy/tests/cabinet-deploy-logic-test.sh
@deploy/tests/sandboxing-test.sh
@deploy/tests/lib/host-guard.sh
@build/package-release.sh
@build/tests/package-release-e2e-test.sh
@docs/deploy.md
@docs/lxc-setup.md

Hard repository rules that apply to every line written below (enforced by
`build/lint.sh repo-rules` and `shell`):
- No planning references anywhere outside `.planning/`: no task or plan IDs,
  no "quick", no phase/plan/wave numbers, no planning document names, in code,
  comments, test descriptions, log messages or docs. Release versions are not
  to be mentioned in docs either; describe behaviour in plain, timeless language.
- No personal data: only standard system paths and `example-owner/example-repo`
  style placeholders.
- Shell comment style follows each file's existing convention: `#` blocks in
  `deploy/lib/common.sh`, `deploy/bin/cabinet-deploy` and
  `deploy/tests/cabinet-deploy-logic-test.sh`; `###` blocks in
  `deploy/bin/cabinet-selfcheck`, `deploy/provision.d/40-services.sh` and
  `deploy/tests/sandboxing-test.sh`. Every new function gets a doc comment.
- Everything must pass shellcheck (`shellcheck -x -P SCRIPTDIR`); a newly
  sourced file needs a `# shellcheck source=deploy/lib/common.sh` directive.
</context>

<tasks>

<task type="tracer" tdd="true">
  <name>Task 1: Shared drift comparison and the one-line poll warning, end to end</name>
  <files>deploy/lib/common.sh, deploy/bin/cabinet-deploy, deploy/provision.d/40-services.sh, deploy/tests/cabinet-deploy-logic-test.sh</files>
  <behavior>
    All in a new section of deploy/tests/cabinet-deploy-logic-test.sh, under the existing relocated root (CABINET_DEPLOY_ROOT=${WORK}/root) and host guard:
    - Identical installed copies: cabinet_provisioning_drift "${CURRENT_LINK}/deploy" "$CABINET_DEPLOY_ROOT" returns 0 and prints nothing.
    - Installed usr/local/sbin/cabinet-deploy changed: returns 1 and prints exactly that installed path.
    - Installed usr/local/lib/cabinet/deploy.sh changed: returns 1 and prints that path.
    - Installed etc/systemd/system/cabinet.service changed: returns 1 and prints that path.
    - Installed etc/systemd/system/cabinet-deploy-poll.timer deleted: returns 1 and prints that path.
    - Two files drifted: prints two lines.
    - Installed etc/cabinet/deploy.conf differing from the release's deploy.conf.example (true already: load_configuration_for_tests writes it): returns 0 (rendered config is not compared).
    - An extra installed library the release does not ship (usr/local/lib/cabinet/retired.sh): returns 0.
    - Active release without a deploy/ directory: returns 2, prints nothing. No current link at all: returns 2.
    - Poll, identical copies, latest tag equal to active: P_RC 0, no install, log has no "provisioning is out of date".
    - Poll, installer drifted, latest equal to active: P_RC 0, no install, log still contains "up to date at 0.0.5", log contains "WARNING: provisioning is out of date; re-run deploy/provision.sh from the active release" on exactly one line, exactly one HTTP request recorded.
    - Poll, installer drifted, newer tag v0.0.6: cmd_install stub called with exactly "v0.0.6", P_RC 0, warning on exactly one line.
    - Poll, installer drifted, fetch status 500: P_RC 1 (unchanged), warning still logged once. Fetch status 404: P_RC 0, no install.
    - Poll, active release has no deploy/ directory: no warning.
    - After a drifted poll, the drifted installed installer's sha256 is unchanged and the release's deploy/ tree checksum listing is unchanged (nothing copied in either direction).
  </behavior>
  <action>
    RED first. In deploy/tests/cabinet-deploy-logic-test.sh add a section headed "# --- Provisioning drift ---" (match the existing "# --- Name ----" rule style) inserted after the existing "poll with a network error exits 1" check and before "# --- Install outcomes and the rejected marker". At this point the cmd_install stub, run_poll, set_active, HTTP_CALLS, the cabinet_http_get fixture seam and a cleared rejected marker are all in effect; reuse them. Add a helper make_provisioned_host VERSION that calls set_active VERSION, removes ${CABINET_DEPLOY_ROOT}/usr and ${CABINET_DEPLOY_ROOT}/etc/systemd, then creates ${CURRENT_LINK}/deploy/{bin,lib,systemd} filled by copying the repository's own ${REPO_ROOT}/deploy/bin/*, deploy/lib/*.sh, deploy/systemd/*.service, deploy/systemd/*.timer and deploy/deploy.conf.example, and installs identical copies of the same bin, lib and unit files under ${CABINET_DEPLOY_ROOT}/usr/local/sbin, ${CABINET_DEPLOY_ROOT}/usr/local/lib/cabinet and ${CABINET_DEPLOY_ROOT}/etc/systemd/system. Drift is simulated by appending a line to (or deleting) an installed copy. Capture the function's status with "|| D_RC=$?" because the file runs under set -e. Count warning lines with a grep -c over the captured log (guard with || true). Implement every case in the behavior block with plain-language check descriptions (no planning references). At the end of the section call set_active 0.0.5 and remove ${CABINET_DEPLOY_ROOT}/usr and ${CABINET_DEPLOY_ROOT}/etc/systemd so the following sections see exactly the state they saw before. Extend the file's header comment to mention the provisioning drift comparison and the out-of-date warning. Run the test now and confirm the new checks FAIL (function missing, no warning); note this in the summary.

    GREEN. In deploy/lib/common.sh add cabinet_provisioning_drift RELEASE_DEPLOY_DIR INSTALL_ROOT, with a "#" doc comment that states: it compares, byte for byte, every file provisioning installs verbatim with the active release's copy; the mapping mirrors deploy/provision.d/40-services.sh (bin/* to INSTALL_ROOT/usr/local/sbin, lib/*.sh to INSTALL_ROOT/usr/local/lib/cabinet, systemd/*.service and systemd/*.timer to INSTALL_ROOT/etc/systemd/system); rendered files (deploy.conf, nftables.conf, the env files) are deliberately excluded because they are not installed verbatim; installed files the release no longer ships are not reported because provisioning does not remove them either; it only reads. Behaviour: when RELEASE_DEPLOY_DIR is not a directory, print nothing and return 2. Otherwise iterate the same four globs 40-services.sh uses, skip anything that is not a regular file (same "[ -f ] || continue" idiom, no nullglob change), and for each source print the installed path on its own line to stdout when that installed path is not a regular file or "cmp -s" reports a difference. Return 1 when at least one path was printed, else 0. The function must never write, copy, move, source or execute any file, must not call cabinet_die, and must keep every failing command inside a condition so callers under set -e are safe. INSTALL_ROOT is an empty string on a real host. Update the common.sh header comment to say it also holds the provisioning comparison shared by the installer and the selfcheck.

    In deploy/bin/cabinet-deploy cmd_poll, as its very first step (before cabinet_fetch_latest_release), call cabinet_provisioning_drift "${CURRENT_LINK}/deploy" "$DEPLOY_ROOT" with stdout sent to /dev/null and its status captured into a local via "|| drift_status=$?"; when the status is 1, call cabinet_log "WARNING: provisioning is out of date; re-run deploy/provision.sh from the active release". Status 0 and 2 log nothing. Do not touch cmd_install, cmd_rollback, cmd_verify, main or any return value; the warning must never alter an exit code. Add a short "#" comment above the call explaining the warning only signals and never provisions or copies anything (trust model: provisioning is an operator action run from the active release).

    In deploy/provision.d/40-services.sh add one "###" sentence to the services_install_scripts / services_install_libraries / services_install_units area (for example to the services_install_file doc block or the module header) noting that cabinet_provisioning_drift in deploy/lib/common.sh mirrors these destinations and must be kept in step with them. No behaviour change in that file.

    Run the logic test until green, then the linters.
  </action>
  <verify>
    <automated>bash deploy/tests/cabinet-deploy-logic-test.sh && build/lint.sh shell repo-rules script-tests</automated>
  </verify>
  <done>
    The logic test passes with the new drift section (and was observed failing before the implementation); the poll logs exactly one warning line when an installed script, library or unit differs from the active release's deploy/ copy and none otherwise; every pre-existing poll, install and rollback check still passes unchanged; the final host-guard check still reports no systemctl or pkexec call; shell, repo-rules and script-tests lint pass.
  </done>
</task>

<task type="auto" tdd="true">
  <name>Task 2: cabinet-selfcheck FAILs per drifted file against the active release</name>
  <files>deploy/bin/cabinet-selfcheck, deploy/tests/sandboxing-test.sh</files>
  <behavior>
    In deploy/tests/sandboxing-test.sh, after the selfcheck has been sourced, with SELFCHECK_ROOT pointed at ${WORK_DIR}/provision-root and the host guard active:
    - Static: the selfcheck file has the exact line "  check_provisioning_current" (main runs the check), and its --help output mentions the active release.
    - Identical installed copies: run_selfcheck_function check_provisioning_current gives "1 0".
    - Installed usr/local/sbin/cabinet-deploy changed: "0 1", run.out names usr/local/sbin/cabinet-deploy and contains "re-run deploy/provision.sh from the active release".
    - Installed usr/local/lib/cabinet/common.sh changed: "0 1", run.out names it.
    - Installed etc/systemd/system/cabinet-deploy-poll.service changed: "0 1", run.out names it.
    - Installer and one unit changed together: "0 2".
    - Installed etc/systemd/system/cabinet.service missing: "0 1".
    - Only the rendered etc/cabinet/deploy.conf differs from the release's deploy.conf.example: "1 0".
    - Active release has no deploy/ directory: "0 1". No current link at all: "0 1".
    - With cabinet_provisioning_drift unavailable (saved with declare -f, unset -f, restored with eval after the case): "0 1" and run.out says the deploy library cannot compare provisioning.
    - After a drifted check the drifted installed installer's content is unchanged.
  </behavior>
  <action>
    RED first. In deploy/tests/sandboxing-test.sh: next to the existing static selfcheck checks (around "selfcheck compares health with the current symlink target") add a check that the selfcheck contains the exact line "  check_provisioning_current" (use the existing file_has_line helper) and a check that the --help output (help.txt) mentions "active release". Then add a "###"-headed section "Selfcheck: installed provisioning matches the active release" placed after the existing "unset -f systemctl pgrep getent" line and before the final "the whole test made no systemctl or pkexec call" check. Set SELFCHECK_ROOT="${WORK_DIR}/provision-root" (a plain assignment after sourcing; the selfcheck must not read it from the environment). Add a helper make_selfcheck_host that removes SELFCHECK_ROOT, creates ${SELFCHECK_ROOT}/opt/cabinet/releases/1.0.0/deploy/{bin,lib,systemd}, symlinks ${SELFCHECK_ROOT}/opt/cabinet/current to that release, copies the repository's deploy/bin/*, deploy/lib/*.sh, deploy/systemd/*.service, deploy/systemd/*.timer and deploy/deploy.conf.example into the release copy, installs identical bin, lib and unit copies under ${SELFCHECK_ROOT}/usr/local/sbin, ${SELFCHECK_ROOT}/usr/local/lib/cabinet and ${SELFCHECK_ROOT}/etc/systemd/system, and writes a differing ${SELFCHECK_ROOT}/etc/cabinet/deploy.conf (CABINET_GITHUB_REPO=example-owner/example-repo). Implement every case in the behavior block with run_selfcheck_function and the existing "PASSED FAILED" string checks. Update the file's top "###" comment to mention the provisioning comparison. Run the test and confirm the new checks FAIL before the implementation; note it in the summary.

    GREEN. In deploy/bin/cabinet-selfcheck:
    - Near the top, after the readonly constants, resolve the library directory with the same rule as cabinet-deploy, using variable names that cannot clobber a sourcing test's SCRIPT_DIR (for example SELFCHECK_BIN_DIR and SELFCHECK_LIB_DIR): the "../lib" directory next to this script when "../lib/common.sh" exists there, otherwise /usr/local/lib/cabinet. Source "${SELFCHECK_LIB_DIR}/common.sh" with a "# shellcheck source=deploy/lib/common.sh" directive and stderr discarded; the file runs without set -e, so a missing library must not abort the run (the check below reports it).
    - Add a plain, non-exported global SELFCHECK_ROOT="" with a "###" doc: the prefix under which the provisioning comparison looks up the active release and the installed files; always empty on a real host and never read from the environment, so the root-run selfcheck cannot be redirected; the offline tests reassign it after sourcing.
    - Add check_provisioning_current with a "###" doc. If declare -F finds no cabinet_provisioning_drift, fail "the installed deploy library cannot compare provisioning; re-run deploy/provision.sh from the active release" and return. Otherwise capture the output and status of cabinet_provisioning_drift "${SELFCHECK_ROOT}/opt/cabinet/current/deploy" "$SELFCHECK_ROOT" into variables (never pipe into grep, per the file's header rule). Status 0: pass "the installed scripts, libraries and units match the active release". Status 2: fail "there is no active release deploy directory to compare the installed scripts, libraries and units against". Status 1: one fail line per printed path, worded "<path> does not match the active release; re-run deploy/provision.sh from the active release". The check only reads; it never copies, installs or runs anything.
    - Call check_provisioning_current from main directly after check_health, as its own two-space-indented line.
    - Extend usage() so the description also says it checks that the installed scripts, libraries and units match the active release.

    Run the sandboxing test until green, re-run the logic test (the selfcheck now sources common.sh, the logic test sources cabinet-deploy; both must still pass), then the linters.
  </action>
  <verify>
    <automated>bash deploy/tests/sandboxing-test.sh && bash deploy/tests/cabinet-deploy-logic-test.sh && build/lint.sh shell repo-rules script-tests</automated>
  </verify>
  <done>
    The sandboxing test passes with the new section (and was observed failing before the implementation); cabinet-selfcheck reports one PASS when the installed copies match the active release and one FAIL per missing or differing installed script, library or unit, FAILs when there is nothing to compare against or the library lacks the comparison, ignores rendered deploy.conf, never modifies anything; the final host-guard check still reports no systemctl or pkexec call; shell, repo-rules and script-tests lint pass.
  </done>
</task>

<task type="auto">
  <name>Task 3: Document the drift signal and pin the release's deploy/ copy</name>
  <files>docs/deploy.md, docs/lxc-setup.md, build/tests/package-release-e2e-test.sh</files>
  <action>
    build/tests/package-release-e2e-test.sh: next to the existing zip-listing assertions (the grep -qx lines against LISTING), add assertions, in the same style and with the same fail helper, that the zip contains deploy/bin/cabinet-deploy, deploy/bin/cabinet-selfcheck, deploy/lib/common.sh, deploy/lib/deploy.sh, deploy/systemd/cabinet.service, deploy/systemd/cabinet-deploy-poll.service, deploy/systemd/cabinet-deploy-poll.timer and deploy/provision.sh, and that no entry starts with deploy/tests/. These pin the comparison source the server relies on. Messages in plain language, no planning references.

    docs/deploy.md: rewrite the "## When to re-run provisioning" section (keep the heading) in the document's existing plain style. Cover: the installer only ever changes the application and never runs provisioning or copies anything from a release into system paths, by design, so anything the one-time setup owns is updated only when an operator re-runs provisioning; how drift becomes visible: every poll compares the installed copies with the active release's own deploy/ directory and, while any differ, logs one line "WARNING: provisioning is out of date; re-run deploy/provision.sh from the active release" (visible with journalctl -u cabinet-deploy-poll), and the warning changes nothing else (installs, rollbacks and the exit status behave exactly as before); cabinet-selfcheck fails with one line naming each installed file that differs or is missing; a small table of what is compared: /opt/cabinet/current/deploy/bin/* against /usr/local/sbin/, /opt/cabinet/current/deploy/lib/*.sh against /usr/local/lib/cabinet/, /opt/cabinet/current/deploy/systemd/*.service and *.timer against /etc/systemd/system/, byte for byte, the same comparison provisioning itself uses to decide what to rewrite; what is not compared and still needs judgement from the release notes: /etc/cabinet/deploy.conf (rendered from deploy.conf.example), /etc/nftables.conf (rendered from the firewall template), /etc/cabinet/cabinet.env, /etc/cabinet/provision.conf, operating-system packages, and installed files a newer release no longer ships; the fix: run /opt/cabinet/current/deploy/provision.sh as root, then cabinet-selfcheck, in a bash fenced block like the rest of the doc; and that the warning and the check come from the installed installer and selfcheck themselves, so a host whose installed copies predate the comparison stays silent until provisioning is re-run once. Also extend the first bullet of "## Where outcomes appear" so the poll journal list includes the out-of-date provisioning warning.

    docs/lxc-setup.md: in "## 10. Run the selfcheck" add a bullet to the check list: the installed installer, its libraries and the systemd units match, byte for byte, the copies shipped in the active release's deploy/ directory. In "## 12. When to re-run provisioning": make /opt/cabinet/current/deploy/provision.sh the primary command once a release is installed, explaining that running from the active release is what makes the installed copies match what the poll and the selfcheck compare against (a source checkout only works when it is checked out at the active release's tag; keep the git-checkout form for the case where no release is installed yet); state that drift of the installed scripts, libraries and units no longer has to be tracked by hand because every poll warns and cabinet-selfcheck fails until provisioning is re-run, and link to the deploy guide section ([the deploy guide](deploy.md#when-to-re-run-provisioning)); keep the existing sentences about never overwriting cabinet.env and provision.conf and about only changed files being rewritten.

    Docs use only standard system paths and placeholders; no planning references, no release version numbers, no personal data.
  </action>
  <verify>
    <automated>build/lint.sh shell repo-rules script-tests && grep -c 'provisioning is out of date; re-run deploy/provision.sh from the active release' docs/deploy.md && grep -c 'opt/cabinet/current/deploy/provision.sh' docs/lxc-setup.md && grep -c 'deploy/lib/common.sh' build/tests/package-release-e2e-test.sh</automated>
  </verify>
  <done>
    docs/deploy.md "When to re-run provisioning" quotes the exact warning line, lists what is and is not compared and gives the re-run command; docs/lxc-setup.md section 10 lists the new check and section 12 makes the active release's provision.sh the primary re-run path and links the deploy guide; the packaging e2e test asserts the release zip carries the compared deploy/ files and no deploy/tests/ (when the .NET SDK is available, CABINET_E2E=1 bash build/tests/package-release-e2e-test.sh passes); shell, repo-rules and script-tests lint pass.
  </done>
</task>

</tasks>

<threat_model>
## Trust Boundaries

| Boundary | Description |
|----------|-------------|
| verified release tree -> root tooling | /opt/cabinet/current/deploy is attested, root-owned release content; the poll (root, sandboxed unit) and the selfcheck (root, operator-run) now read it |
| operator environment -> root selfcheck | anything the selfcheck reads from its environment could redirect what it inspects |
| installed system paths (/usr/local/sbin, /usr/local/lib/cabinet, /etc/systemd/system) | owned by provisioning only; must never be written by the poll or the selfcheck |

## STRIDE Threat Register

| Threat ID | Category | Component | Severity | Disposition | Mitigation Plan |
|-----------|----------|-----------|----------|-------------|-----------------|
| T-q261004-01 | Elevation of Privilege | cabinet_provisioning_drift / cmd_poll | high | mitigate | The comparison only runs cmp -s and file tests; it never copies, installs, sources or executes release files, and provisioning stays an operator action. Tests assert the drifted installed file and the release tree are byte-identical after a poll and after a selfcheck run |
| T-q261004-02 | Denial of Service | cmd_poll exit status under set -e | high | mitigate | Drift status is captured with "|| drift_status=$?" and only ever produces a log line; tests assert unchanged exit codes and install decisions for up-to-date, newer-tag, 404 and 500 outcomes with drift present |
| T-q261004-03 | Spoofing | cabinet-selfcheck SELFCHECK_ROOT seam | medium | mitigate | SELFCHECK_ROOT is a plain in-file global initialised to empty and never read from the environment, so a root-run selfcheck cannot be pointed at a fake tree; only sourcing tests reassign it |
| T-q261004-04 | Repudiation | stale provisioning going unnoticed | medium | mitigate | One warning line per poll in the poll unit journal plus a per-file FAIL in the selfcheck; a selfcheck whose library lacks the comparison FAILs instead of silently passing |
| T-q261004-05 | Information Disclosure | warning and FAIL lines | low | accept | They name only standard system paths already documented publicly; the journal is root/adm readable |
| T-q261004-06 | Tampering | symlinks inside the release deploy/ tree | low | accept | The tree is attestation-verified, root-owned content unpacked by the installer; the comparison only reads through it and never writes |
</threat_model>

<verification>
- bash deploy/tests/cabinet-deploy-logic-test.sh passes, including the provisioning drift section; it ends with no host-guard calls.
- bash deploy/tests/sandboxing-test.sh passes, including the selfcheck provisioning section; it ends with no host-guard calls.
- Both new test sections were run before the implementation and failed (recorded in the summary).
- build/lint.sh shell repo-rules script-tests passes (shellcheck clean, no planning references, all script tests green).
- grep shows the exact warning text in deploy/bin/cabinet-deploy and docs/deploy.md, and check_provisioning_current called from main in deploy/bin/cabinet-selfcheck.
- git diff shows no change to cmd_install, cmd_rollback, cmd_verify or main in deploy/bin/cabinet-deploy, and no behaviour change in deploy/provision.d/40-services.sh (doc comment only).
</verification>

<success_criteria>
- A host whose installed installer, libraries or units differ from /opt/cabinet/current/deploy gets exactly one "provisioning is out of date" warning per poll, and cabinet-selfcheck FAILs naming each differing file.
- Install, rollback and poll exit-code behaviour are byte-for-byte the same as before for every existing test case.
- No code path copies release files into system paths or runs provisioning automatically.
- Rendered files are excluded from the comparison.
- Operator docs explain the signal, the comparison scope and the re-run command.
</success_criteria>

<output>
Create `.planning/quick/261004-vqo-make-out-of-date-provisioning-visible-on/261004-vqo-SUMMARY.md` with `status: complete` in its frontmatter when done, recording the RED runs of both test files and the final green runs.
</output>

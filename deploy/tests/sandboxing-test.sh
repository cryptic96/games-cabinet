#!/usr/bin/env bash
###
### Proves the units are confined and the services module is safe to load:
### the installer unit carries its sandboxing directives and its narrow
### write allow-list, the app unit carries the full hardening block and no
### database dependency, the timer carries the polling schedule, and
### sourcing the services module defines its functions without ever calling
### systemctl. The selfcheck's comparison of the installed scripts, libraries
### and units with the active release is exercised against a relocated root.
### Offline: nothing on the host is touched.
###
set -uo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
HOST_GUARD_DIR="$(mktemp -d)"
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$HOST_GUARD_DIR" "$WORK_DIR"' EXIT
host_guard_install "$HOST_GUARD_DIR"

DEPLOY_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
APP_UNIT="${DEPLOY_DIR}/systemd/cabinet.service"
POLL_UNIT="${DEPLOY_DIR}/systemd/cabinet-deploy-poll.service"
POLL_TIMER="${DEPLOY_DIR}/systemd/cabinet-deploy-poll.timer"
SERVICES_MODULE="${DEPLOY_DIR}/provision.d/40-services.sh"
SELFCHECK="${DEPLOY_DIR}/bin/cabinet-selfcheck"

FAILURES=0

check() {
  local description="$1" expected="$2" actual="$3"
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected [%s], got [%s])\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

file_has_line() {
  local file="$1" line="$2"
  if grep -qxF -- "$line" "$file"; then
    echo 1
  else
    echo 0
  fi
}

file_has_text() {
  local file="$1" text="$2"
  if grep -qF -- "$text" "$file"; then
    echo 1
  else
    echo 0
  fi
}

count_lines_matching() {
  local file="$1" count
  shift
  count="$(grep -c "$@" -- "$file" || true)"
  echo "${count:-0}"
}

###
### Installer unit
###
for directive in \
  NoNewPrivileges=yes ProtectSystem=strict ProtectHome=read-only PrivateTmp=yes \
  ProtectKernelTunables=yes ProtectKernelModules=yes ProtectControlGroups=yes \
  RestrictNamespaces=yes LockPersonality=yes RestrictRealtime=yes \
  RestrictSUIDSGID=yes SystemCallArchitectures=native \
  RuntimeDirectory=cabinet-deploy RuntimeDirectoryPreserve=yes Type=oneshot; do
  check "poll unit has ${directive}" "1" "$(file_has_line "$POLL_UNIT" "$directive")"
done

check "poll unit runs the installer's poll command" "1" \
  "$(file_has_line "$POLL_UNIT" "ExecStart=/usr/local/sbin/cabinet-deploy poll")"
check "poll unit may write exactly the release tree and the installer state" "1" \
  "$(file_has_line "$POLL_UNIT" "ReadWritePaths=/opt/cabinet /var/lib/cabinet-deploy")"
check "poll unit has a single write allow-list line" "1" \
  "$(count_lines_matching "$POLL_UNIT" '^ReadWritePaths=')"
check "poll unit does not restrict capabilities (the installer runs as root and switches users)" "0" \
  "$(count_lines_matching "$POLL_UNIT" '^CapabilityBoundingSet=')"
check "poll unit does not restrict address families (the installer reaches GitHub)" "0" \
  "$(count_lines_matching "$POLL_UNIT" '^RestrictAddressFamilies=')"

###
### App unit
###
for directive in \
  User=cabinet Group=cabinet \
  WorkingDirectory=/opt/cabinet/current/app \
  ExecStart=/usr/bin/dotnet\ /opt/cabinet/current/app/Cabinet.Service.dll \
  EnvironmentFile=/etc/cabinet/cabinet.env StateDirectory=cabinet Restart=always \
  After=network.target WantedBy=multi-user.target \
  NoNewPrivileges=yes PrivateTmp=yes ProtectSystem=strict ProtectHome=yes \
  ProtectKernelTunables=yes ProtectKernelModules=yes ProtectControlGroups=yes \
  RestrictNamespaces=yes LockPersonality=yes \
  RestrictAddressFamilies=AF_UNIX\ AF_INET\ AF_INET6 \
  CapabilityBoundingSet= SystemCallArchitectures=native; do
  check "app unit has ${directive}" "1" "$(file_has_line "$APP_UNIT" "$directive")"
done

check "app unit orders after nothing but the network" "1" "$(count_lines_matching "$APP_UNIT" '^After=')"
check "app unit declares no dependency on any other unit" "0" \
  "$(count_lines_matching "$APP_UNIT" -E '^(Requires|Requisite|BindsTo|PartOf|Upholds|Wants)=')"
check "app unit orders after no service unit" "0" "$(count_lines_matching "$APP_UNIT" -E '^After=.*\.service')"

###
### Poll timer
###
check "timer fires 3 minutes after boot" "1" "$(file_has_line "$POLL_TIMER" "OnBootSec=3min")"
check "timer repeats every 10 minutes" "1" "$(file_has_line "$POLL_TIMER" "OnUnitActiveSec=10min")"
check "timer adds up to 120 seconds of random delay" "1" "$(file_has_line "$POLL_TIMER" "RandomizedDelaySec=120")"
check "timer is wanted by the timers target" "1" "$(file_has_line "$POLL_TIMER" "WantedBy=timers.target")"

###
### Services module in library mode
###
# shellcheck source=deploy/provision.d/40-services.sh
if CABINET_PROVISION_LIB_ONLY=1 source "$SERVICES_MODULE"; then
  echo "PASS: sourcing the services module in library mode succeeds"
else
  echo "FAIL: sourcing the services module in library mode succeeds"
  FAILURES=$((FAILURES + 1))
fi

set -uo pipefail
set +e

for function_name in \
  services_install_file services_render_example_overrides services_install_scripts \
  services_install_libraries services_install_units services_unit_changed \
  services_install_deploy_conf services_enable_unattended_upgrades \
  services_enable_and_start; do
  if declare -F "$function_name" > /dev/null; then
    check "services module defines ${function_name}" "1" "1"
  else
    check "services module defines ${function_name}" "1" "0"
  fi
done

check "sourcing the services module made no systemctl or pkexec call" "" "$(host_guard_calls)"

###
### Services module behaviour on throwaway files
###
CURRENT_USER="$(id -un)"
CURRENT_GROUP="$(id -gn)"

printf 'first\n' > "${WORK_DIR}/source.txt"
services_install_file 644 "$CURRENT_USER" "$CURRENT_GROUP" "${WORK_DIR}/source.txt" "${WORK_DIR}/installed.txt"
check "install writes a file that does not exist yet" "written" "$SERVICES_LAST_INSTALL"
check "install copies the content" "first" "$(cat "${WORK_DIR}/installed.txt")"

services_install_file 644 "$CURRENT_USER" "$CURRENT_GROUP" "${WORK_DIR}/source.txt" "${WORK_DIR}/installed.txt"
check "install leaves identical content alone" "unchanged" "$SERVICES_LAST_INSTALL"

printf 'second\n' > "${WORK_DIR}/source.txt"
services_install_file 600 "$CURRENT_USER" "$CURRENT_GROUP" "${WORK_DIR}/source.txt" "${WORK_DIR}/installed.txt"
check "install rewrites changed content" "written" "$SERVICES_LAST_INSTALL"
check "install applies the requested mode" "600" "$(stat -c '%a' "${WORK_DIR}/installed.txt")"
check "install leaves no staging file behind" "1" "$(find "$WORK_DIR" -name 'installed.txt*' | wc -l | tr -d ' ')"

# shellcheck disable=SC2034 # read by services_unit_changed
SERVICES_CHANGED_UNITS=" a.service b.timer"
if services_unit_changed b.timer; then
  check "a changed unit is reported as changed" "1" "1"
else
  check "a changed unit is reported as changed" "1" "0"
fi
if services_unit_changed c.service; then
  check "an unchanged unit is not reported as changed" "0" "1"
else
  check "an unchanged unit is not reported as changed" "0" "0"
fi

RENDERED="${WORK_DIR}/deploy.conf"
services_render_example_overrides "${DEPLOY_DIR}/deploy.conf.example" "$RENDERED" \
  "CABINET_GITHUB_REPO=example-owner/example-repo"
check "deploy.conf takes the repository from the override" "1" \
  "$(file_has_line "$RENDERED" "CABINET_GITHUB_REPO=example-owner/example-repo")"
check "deploy.conf keeps the other example keys" "1" \
  "$(file_has_line "$RENDERED" "CABINET_KEEP_RELEASES=3")"

if (services_render_example_overrides "${DEPLOY_DIR}/deploy.conf.example" "${WORK_DIR}/refused.conf" \
  "CABINET_NO_SUCH_KEY=value") > /dev/null 2>&1; then
  check "an override for a key the example lacks is refused" "refused" "accepted"
else
  check "an override for a key the example lacks is refused" "refused" "refused"
fi
check "a refused render writes no output" "no" "$([ -e "${WORK_DIR}/refused.conf" ] && echo yes || echo no)"

if (services_render_example_overrides "${DEPLOY_DIR}/deploy.conf.example" "${WORK_DIR}/unsafe.conf" \
  "CABINET_GITHUB_REPO=a;b") > /dev/null 2>&1; then
  check "an override with a semicolon is refused" "refused" "accepted"
else
  check "an override with a semicolon is refused" "refused" "refused"
fi

###
### Selfcheck: sandbox properties, as written and as actually passed
###
for property in \
  NoNewPrivileges=yes ProtectSystem=strict ProtectHome=yes PrivateTmp=yes \
  PrivateDevices=yes ProtectKernelTunables=yes ProtectKernelModules=yes \
  ProtectControlGroups=yes RestrictNamespaces=yes LockPersonality=yes \
  CapabilityBoundingSet= RestrictAddressFamilies=AF_UNIX \
  Environment=DOTNET_NOLOGO=1; do
  check "selfcheck passes ${property} to systemd-run" "1" \
    "$(file_has_text "$SELFCHECK" "--property=${property} ")"
done
check "selfcheck runs the smoke as the service user" "1" "$(file_has_text "$SELFCHECK" "--uid=cabinet")"
check "selfcheck runs the smoke from the release's app directory" "1" \
  "$(file_has_text "$SELFCHECK" "--working-directory=/opt/cabinet/current/app")"
check "selfcheck runs the image-smoke command" "1" "$(file_has_text "$SELFCHECK" "Cabinet.Service.dll image-smoke")"
check "selfcheck compares health with the current symlink target" "1" \
  "$(file_has_text "$SELFCHECK" "readlink -f /opt/cabinet/current")"
check "selfcheck runs the provisioning comparison" "1" \
  "$(file_has_line "$SELFCHECK" "  check_provisioning_current")"

if "$SELFCHECK" --help > "${WORK_DIR}/help.txt" 2>&1; then
  check "selfcheck --help exits 0" "1" "1"
else
  check "selfcheck --help exits 0" "1" "0"
fi
check "selfcheck --help prints usage" "1" "$(file_has_text "${WORK_DIR}/help.txt" "Usage: cabinet-selfcheck")"
check "selfcheck --help mentions the active release" "1" "$(file_has_text "${WORK_DIR}/help.txt" "active release")"

if [ "$(id -u)" -ne 0 ]; then
  if "$SELFCHECK" > /dev/null 2>&1; then
    check "selfcheck refuses to run without root" "refused" "ran"
  else
    check "selfcheck refuses to run without root" "refused" "refused"
  fi
fi

# shellcheck source=deploy/bin/cabinet-selfcheck
source "$SELFCHECK"
set -uo pipefail
set +e

STUB_BIN="${WORK_DIR}/stub-bin"
mkdir -p "$STUB_BIN"
export STUB_ARGS_LOG="${WORK_DIR}/stub-args.log"

for stub in systemd-run ss nft sshd; do
  cat > "${STUB_BIN}/${stub}" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "${STUB_ARGS_LOG}"
case "$(basename "$0")" in
  systemd-run) printf '%s\n' "${STUB_SYSTEMD_RUN_OUTPUT:-}" ;;
  ss) printf '%s\n' "${STUB_SS_OUTPUT:-}" ;;
  nft) printf '%s\n' "${STUB_NFT_OUTPUT:-}" ;;
  sshd) printf '%s\n' "${STUB_SSHD_OUTPUT:-}" ;;
esac
STUB
  chmod +x "${STUB_BIN}/${stub}"
done
export PATH="${STUB_BIN}:${PATH}"

###
### Runs the selfcheck function $1 with fresh counters and reports the
### counters through RUN_PASSED and RUN_FAILED; its report lands in run.out.
###
run_selfcheck_function() {
  PASSED=0
  FAILED=0
  : > "$STUB_ARGS_LOG"
  "$1" > "${WORK_DIR}/run.out" 2>&1
  RUN_PASSED="$PASSED"
  RUN_FAILED="$FAILED"
}

STUB_SYSTEMD_RUN_OUTPUT="PASS image-smoke 480x360 webp 1234 bytes"
export STUB_SYSTEMD_RUN_OUTPUT
run_selfcheck_function check_image_smoke
check "image smoke passes when the binary prints its PASS line" "1 0" "${RUN_PASSED} ${RUN_FAILED}"
check "image smoke echoes the PASS line in the report" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "PASS image-smoke 480x360 webp 1234 bytes")"

SMOKE_ARGS="$(cat "$STUB_ARGS_LOG")"
for property in \
  NoNewPrivileges=yes ProtectSystem=strict ProtectHome=yes PrivateTmp=yes \
  PrivateDevices=yes ProtectKernelTunables=yes ProtectKernelModules=yes \
  ProtectControlGroups=yes RestrictNamespaces=yes LockPersonality=yes \
  CapabilityBoundingSet= RestrictAddressFamilies=AF_UNIX; do
  case " ${SMOKE_ARGS} " in
    *" --property=${property} "*) check "the smoke run carries ${property}" "1" "1" ;;
    *) check "the smoke run carries ${property}" "1" "0" ;;
  esac
done
case "$SMOKE_ARGS" in
  *"--uid=cabinet --gid=cabinet --working-directory=/opt/cabinet/current/app"*"/usr/bin/dotnet Cabinet.Service.dll image-smoke")
    check "the smoke run is the service user running the binary's smoke command" "1" "1" ;;
  *) check "the smoke run is the service user running the binary's smoke command" "1" "0" ;;
esac

STUB_SYSTEMD_RUN_OUTPUT="FAIL image-smoke no decoder"
run_selfcheck_function check_image_smoke
check "image smoke fails when the binary reports a failure" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SYSTEMD_RUN_OUTPUT=""
run_selfcheck_function check_image_smoke
check "image smoke fails when the binary prints nothing" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SS_OUTPUT=$'LISTEN 0 512 0.0.0.0:5080 0.0.0.0:*\nLISTEN 0 512 127.0.0.1:5081 0.0.0.0:*'
export STUB_SS_OUTPUT
run_selfcheck_function check_listeners
check "listeners pass with 5080 public and 5081 on loopback" "2 0" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SS_OUTPUT=$'LISTEN 0 512 0.0.0.0:5080 0.0.0.0:*\nLISTEN 0 512 0.0.0.0:5081 0.0.0.0:*'
run_selfcheck_function check_listeners
check "listeners fail when the ops port is bound beyond loopback" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SS_OUTPUT=$'LISTEN 0 512 127.0.0.1:5081 0.0.0.0:*'
run_selfcheck_function check_listeners
check "listeners fail when nothing serves the public port" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t}\n}'
export STUB_NFT_OUTPUT
run_selfcheck_function check_firewall
check "firewall passes with a default-drop input chain" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy accept;\n\t}\n}'
run_selfcheck_function check_firewall
check "firewall fails with a default-accept input chain" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SSHD_OUTPUT=$'port 22\npasswordauthentication no\nkbdinteractiveauthentication no'
export STUB_SSHD_OUTPUT
run_selfcheck_function check_ssh
check "ssh passes when passwords and keyboard-interactive are off" "2 0" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SSHD_OUTPUT=$'passwordauthentication yes\nkbdinteractiveauthentication no'
run_selfcheck_function check_ssh
check "ssh fails when password authentication is on" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

RUNNER_HOME="${WORK_DIR}/runner-home"
mkdir -p "$RUNNER_HOME"

# shellcheck disable=SC2329 # invoked by the selfcheck function under test
systemctl() { return 0; }
# shellcheck disable=SC2329
pgrep() { return 1; }
# shellcheck disable=SC2329
getent() { printf 'builder:x:1000:1000::%s:/bin/bash\n' "$RUNNER_HOME"; }

run_selfcheck_function check_no_actions_runner
check "no runner passes when nothing runner-shaped exists" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

mkdir -p "${RUNNER_HOME}/actions-runner"
run_selfcheck_function check_no_actions_runner
check "a runner directory in an account's home fails the check" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
rmdir "${RUNNER_HOME}/actions-runner"

# shellcheck disable=SC2329
pgrep() { echo "4242 Runner.Listener"; return 0; }
run_selfcheck_function check_no_actions_runner
check "a running runner process fails the check" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

# shellcheck disable=SC2329
systemctl() { echo "actions.runner.example.service loaded active running"; return 0; }
# shellcheck disable=SC2329
pgrep() { return 1; }
run_selfcheck_function check_no_actions_runner
check "a runner unit fails the check" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
unset -f systemctl pgrep getent

###
### Selfcheck: installed provisioning matches the active release
###
SELFCHECK_ROOT="${WORK_DIR}/provision-root"
SC_SBIN="${SELFCHECK_ROOT}/usr/local/sbin"
SC_LIB="${SELFCHECK_ROOT}/usr/local/lib/cabinet"
SC_UNITS="${SELFCHECK_ROOT}/etc/systemd/system"
SC_RELEASE="${SELFCHECK_ROOT}/opt/cabinet/releases/1.0.0"

###
### Builds an active release carrying the repository's own deploy tree and a
### host whose installed scripts, libraries and units are identical copies,
### with a rendered deploy.conf that differs from the release's example.
###
make_selfcheck_host() {
  local file
  rm -rf "${SELFCHECK_ROOT:?}"
  mkdir -p "${SC_RELEASE}/deploy/bin" "${SC_RELEASE}/deploy/lib" "${SC_RELEASE}/deploy/systemd" \
    "$SC_SBIN" "$SC_LIB" "$SC_UNITS" "${SELFCHECK_ROOT}/etc/cabinet"
  ln -s "$SC_RELEASE" "${SELFCHECK_ROOT}/opt/cabinet/current"
  for file in "${DEPLOY_DIR}"/bin/*; do
    cp "$file" "${SC_RELEASE}/deploy/bin/"
    cp "$file" "${SC_SBIN}/"
  done
  for file in "${DEPLOY_DIR}"/lib/*.sh; do
    cp "$file" "${SC_RELEASE}/deploy/lib/"
    cp "$file" "${SC_LIB}/"
  done
  for file in "${DEPLOY_DIR}"/systemd/*.service "${DEPLOY_DIR}"/systemd/*.timer; do
    cp "$file" "${SC_RELEASE}/deploy/systemd/"
    cp "$file" "${SC_UNITS}/"
  done
  cp "${DEPLOY_DIR}/deploy.conf.example" "${SC_RELEASE}/deploy/deploy.conf.example"
  printf 'CABINET_GITHUB_REPO=example-owner/example-repo\n' > "${SELFCHECK_ROOT}/etc/cabinet/deploy.conf"
}

make_selfcheck_host
run_selfcheck_function check_provisioning_current
check "provisioning check passes when every installed file matches the release" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

printf '# local edit\n' >> "${SC_SBIN}/cabinet-deploy"
INSTALLER_SUM_BEFORE="$(sha256sum "${SC_SBIN}/cabinet-deploy")"
run_selfcheck_function check_provisioning_current
check "provisioning check fails when the installed installer differs" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "provisioning failure names the installer" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "usr/local/sbin/cabinet-deploy")"
check "provisioning failure says to re-run provisioning from the active release" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "re-run deploy/provision.sh from the active release")"
check "the provisioning check leaves the drifted installer untouched" "$INSTALLER_SUM_BEFORE" \
  "$(sha256sum "${SC_SBIN}/cabinet-deploy")"

make_selfcheck_host
printf '# local edit\n' >> "${SC_LIB}/common.sh"
run_selfcheck_function check_provisioning_current
check "provisioning check fails when an installed library differs" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "provisioning failure names the library" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "usr/local/lib/cabinet/common.sh")"

make_selfcheck_host
printf '# local edit\n' >> "${SC_UNITS}/cabinet-deploy-poll.service"
run_selfcheck_function check_provisioning_current
check "provisioning check fails when an installed unit differs" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "provisioning failure names the unit" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "etc/systemd/system/cabinet-deploy-poll.service")"

make_selfcheck_host
printf '# local edit\n' >> "${SC_SBIN}/cabinet-deploy"
printf '# local edit\n' >> "${SC_UNITS}/cabinet.service"
run_selfcheck_function check_provisioning_current
check "provisioning check reports one failure per differing file" "0 2" "${RUN_PASSED} ${RUN_FAILED}"

make_selfcheck_host
rm -f "${SC_UNITS}/cabinet.service"
run_selfcheck_function check_provisioning_current
check "provisioning check fails when an installed unit is missing" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

make_selfcheck_host
run_selfcheck_function check_provisioning_current
check "provisioning check ignores the rendered deploy.conf" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

make_selfcheck_host
rm -rf "${SC_RELEASE}/deploy"
run_selfcheck_function check_provisioning_current
check "provisioning check fails when the active release has no deploy directory" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

make_selfcheck_host
rm -f "${SELFCHECK_ROOT}/opt/cabinet/current"
run_selfcheck_function check_provisioning_current
check "provisioning check fails when there is no active release" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

make_selfcheck_host
DRIFT_FUNCTION="$(declare -f cabinet_provisioning_drift)"
unset -f cabinet_provisioning_drift
run_selfcheck_function check_provisioning_current
eval "$DRIFT_FUNCTION"
check "provisioning check fails when the deploy library cannot compare" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "provisioning failure explains the missing comparison" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "cannot compare provisioning")"

check "the whole test made no systemctl or pkexec call" "" "$(host_guard_calls)"

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "All checks passed."
  exit 0
else
  echo "${FAILURES} check(s) failed."
  exit 1
fi

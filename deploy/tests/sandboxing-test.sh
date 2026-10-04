#!/usr/bin/env bash
###
### Proves the units are confined and the services module is safe to load:
### the installer unit carries its sandboxing directives and its narrow
### write allow-list, the app unit carries the full hardening block and no
### database dependency, the timer carries the polling schedule, and
### sourcing the services module defines its functions without ever calling
### systemctl. Offline: nothing on the host is touched.
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

check "the whole test made no systemctl or pkexec call" "" "$(host_guard_calls)"

echo ""
if [ "$FAILURES" -eq 0 ]; then
  echo "All checks passed."
  exit 0
else
  echo "${FAILURES} check(s) failed."
  exit 1
fi

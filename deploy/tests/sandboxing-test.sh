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
  CapabilityBoundingSet= SystemCallArchitectures=native \
  MemoryHigh=450M MemoryMax=600M MemorySwapMax=0 CPUQuota=80% CPUWeight=50 \
  TasksMax=512 LimitNOFILE=4096 \
  Environment=DOTNET_gcServer=0 Environment=DOTNET_gcConcurrent=0; do
  check "app unit has ${directive}" "1" "$(file_has_line "$APP_UNIT" "$directive")"
done

APP_MEMORY_HIGH_MIB="$(sed -n 's/^MemoryHigh=\([0-9]*\)M$/\1/p' "$APP_UNIT")"
APP_MEMORY_MAX_MIB="$(sed -n 's/^MemoryMax=\([0-9]*\)M$/\1/p' "$APP_UNIT")"
APP_CPU_QUOTA_PERCENT="$(sed -n 's/^CPUQuota=\([0-9]*\)%$/\1/p' "$APP_UNIT")"
check "app unit keeps MemoryHigh below MemoryMax" "below" \
  "$([ "${APP_MEMORY_HIGH_MIB:-0}" -lt "${APP_MEMORY_MAX_MIB:-0}" ] && echo below || echo not-below)"
check "app unit keeps the CPU quota below one whole core" "below" \
  "$([ "${APP_CPU_QUOTA_PERCENT:-100}" -lt 100 ] && echo below || echo not-below)"

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
export STUB_NFT_CALLS="${WORK_DIR}/stub-nft-calls"

for stub in systemd-run ss nft sshd curl; do
  cat > "${STUB_BIN}/${stub}" <<'STUB'
#!/usr/bin/env bash
printf '%s\n' "$*" >> "${STUB_ARGS_LOG}"
case "$(basename "$0")" in
  systemd-run) printf '%s\n' "${STUB_SYSTEMD_RUN_OUTPUT:-}" ;;
  ss) printf '%s\n' "${STUB_SS_OUTPUT:-}" ;;
  nft)
    calls="$(cat "$STUB_NFT_CALLS" 2>/dev/null || echo 0)"
    calls=$((calls + 1))
    printf '%s' "$calls" > "$STUB_NFT_CALLS"
    if [ "$calls" -ge 2 ] && [ -n "${STUB_NFT_OUTPUT_AFTER:-}" ]; then
      printf '%s\n' "$STUB_NFT_OUTPUT_AFTER"
    else
      printf '%s\n' "${STUB_NFT_OUTPUT:-}"
    fi
    ;;
  sshd) printf '%s\n' "${STUB_SSHD_OUTPUT:-}" ;;
  curl)
    case "$*" in
      *unlisted.example.org*) printf '%s' "${STUB_CURL_STATUS_UNLISTED:-000}" ;;
      *:5082*) printf '%s' "${STUB_CURL_STATUS_OWNER:-000}" ;;
      *:5080*) printf '%s' "${STUB_CURL_STATUS_APP:-000}" ;;
      *) printf '000' ;;
    esac
    ;;
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
  : > "$STUB_NFT_CALLS"
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
check "listeners pass with 5080 public, 5081 on loopback and the owner tools off" "3 0" "${RUN_PASSED} ${RUN_FAILED}"
check "listeners say the owner tools are off when nothing listens on 5082" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "the owner tools are off (nothing listens on port 5082)")"

STUB_SS_OUTPUT=$'LISTEN 0 512 0.0.0.0:5080 0.0.0.0:*\nLISTEN 0 512 127.0.0.1:5081 0.0.0.0:*\nLISTEN 0 512 0.0.0.0:5082 0.0.0.0:*'
run_selfcheck_function check_listeners
check "listeners pass three checks with the owner port listening" "3 0" "${RUN_PASSED} ${RUN_FAILED}"
check "listeners report the owner port when it listens" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "the owner port 5082 is listening")"

STUB_SS_OUTPUT=$'LISTEN 0 512 0.0.0.0:5080 0.0.0.0:*\nLISTEN 0 512 0.0.0.0:5081 0.0.0.0:*'
run_selfcheck_function check_listeners
check "listeners fail when the ops port is bound beyond loopback" "2 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SS_OUTPUT=$'LISTEN 0 512 127.0.0.1:5081 0.0.0.0:*'
run_selfcheck_function check_listeners
check "listeners fail when nothing serves the public port" "2 1" "${RUN_PASSED} ${RUN_FAILED}"

OWNER_LISTENING_SS=$'LISTEN 0 512 0.0.0.0:5080 0.0.0.0:*\nLISTEN 0 512 127.0.0.1:5081 0.0.0.0:*\nLISTEN 0 512 0.0.0.0:5082 0.0.0.0:*'
OWNER_OFF_SS=$'LISTEN 0 512 0.0.0.0:5080 0.0.0.0:*\nLISTEN 0 512 127.0.0.1:5081 0.0.0.0:*'

STUB_SS_OUTPUT="$OWNER_LISTENING_SS"
STUB_CURL_STATUS_APP=404
STUB_CURL_STATUS_OWNER=403
export STUB_CURL_STATUS_APP STUB_CURL_STATUS_OWNER
run_selfcheck_function check_owner_wall
check "owner wall passes when the public port hides the owner pages and the owner port refuses a request from loopback" \
  "2 0" "${RUN_PASSED} ${RUN_FAILED}"
check "owner wall says the owner port refuses a request from loopback" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "the owner port refuses a request from loopback")"
check "owner wall sends the forged forwarded address to the owner port" "1" \
  "$(file_has_text "$STUB_ARGS_LOG" "X-Forwarded-For: 203.0.113.7")"
check "owner wall never prints the forged address" "0" \
  "$(file_has_text "${WORK_DIR}/run.out" "203.0.113.7")"

STUB_CURL_STATUS_OWNER=200
run_selfcheck_function check_owner_wall
check "owner wall fails when the owner port serves a request from loopback" "1 1" "${RUN_PASSED} ${RUN_FAILED}"
check "owner wall reports the status code of the failed probe" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "status 200")"

STUB_CURL_STATUS_OWNER=000
run_selfcheck_function check_owner_wall
check "owner wall fails when the owner port gives no answer" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_CURL_STATUS_APP=200
STUB_CURL_STATUS_OWNER=403
run_selfcheck_function check_owner_wall
check "owner wall fails when the public port serves an owner page" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_CURL_STATUS_APP=403
run_selfcheck_function check_owner_wall
check "owner wall fails when the public port answers 403 instead of 404" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SS_OUTPUT="$OWNER_OFF_SS"
STUB_CURL_STATUS_APP=404
STUB_CURL_STATUS_OWNER=200
run_selfcheck_function check_owner_wall
check "owner wall skips the forged probe when the owner port is off" "2 0" "${RUN_PASSED} ${RUN_FAILED}"
check "owner wall says the owner tools are off when nothing listens" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "owner tools are off")"
check "owner wall does not call the owner port when it is off" "0" \
  "$(file_has_text "$STUB_ARGS_LOG" ":5082")"

STUB_CURL_STATUS_APP=200
run_selfcheck_function check_owner_wall
check "owner wall still fails an owner page on the public port when the owner port is off" "1 1" \
  "${RUN_PASSED} ${RUN_FAILED}"

check "selfcheck runs the owner wall check" "1" \
  "$(file_has_line "$SELFCHECK" "  check_owner_wall")"

STUB_CURL_STATUS_UNLISTED=400
export STUB_CURL_STATUS_UNLISTED
run_selfcheck_function check_host_filter
check "host filter passes when an unlisted host gets a 400" "1 0" "${RUN_PASSED} ${RUN_FAILED}"
check "host filter sends the unlisted host to the public port" "1" \
  "$(file_has_text "$STUB_ARGS_LOG" "Host: unlisted.example.org")"

STUB_CURL_STATUS_UNLISTED=200
run_selfcheck_function check_host_filter
check "host filter fails when an unlisted host is served" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "host filter reports the status code and names the setting" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "status 200 to a request for an unlisted host")"

STUB_CURL_STATUS_UNLISTED=000
run_selfcheck_function check_host_filter
check "host filter fails when the app gives no answer" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

check "selfcheck runs the host filter check" "1" \
  "$(file_has_line "$SELFCHECK" "  check_host_filter")"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t}\n}'
export STUB_NFT_OUTPUT
run_selfcheck_function check_firewall
check "firewall passes with a default-drop input chain" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy accept;\n\t}\n}'
run_selfcheck_function check_firewall
check "firewall fails with a default-accept input chain" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

OWNER_RULE_TABLE=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t\ttcp dport 5080 ip saddr 192.0.2.10 accept\n\t\ttcp dport 5082 ip saddr 192.0.2.10 accept\n\t}\n}'
STUB_SS_OUTPUT="$OWNER_LISTENING_SS"
STUB_NFT_OUTPUT="$OWNER_RULE_TABLE"
run_selfcheck_function check_firewall
check "firewall passes both checks when the owner port is admitted from one address" "2 0" "${RUN_PASSED} ${RUN_FAILED}"
check "firewall never prints the proxy address" "0" "$(file_has_text "${WORK_DIR}/run.out" "192.0.2.10")"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t\ttcp dport 5080 ip saddr 192.0.2.10 accept\n\t}\n}'
run_selfcheck_function check_firewall
check "firewall fails when the owner port listens but no rule admits it" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t\ttcp dport 5082 accept\n\t}\n}'
run_selfcheck_function check_firewall
check "firewall fails when the owner port rule has no source address" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t\ttcp dport 5082 ip saddr 0.0.0.0/0 accept\n\t}\n}'
run_selfcheck_function check_firewall
check "firewall fails when the owner port rule admits every address" "1 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_SS_OUTPUT="$OWNER_OFF_SS"
STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t}\n}'
run_selfcheck_function check_firewall
check "firewall asks nothing about the owner port when the owner tools are off" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

###
### Selfcheck: the outbound block and DNS
###
EGRESS_TABLE=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t}\n\tchain output {\n\t\ttype filter hook output priority filter; policy accept;\n\t\toif "lo" accept\n\t\tct state established,related accept\n\t\tmeta l4proto ipv6-icmp accept\n\t\tip daddr { 192.0.2.53 } udp dport 53 accept\n\t\tip daddr { 192.0.2.53 } tcp dport 53 accept\n\t\tip daddr { 192.0.2.0/24, 198.51.100.0/24 } counter packets 0 bytes 0 reject with icmpx admin-prohibited\n\t\tip6 daddr { 2001:db8:1::/64 } counter packets 0 bytes 0 reject with icmpx admin-prohibited\n\t}\n}'
EGRESS_TABLE_COUNTED="${EGRESS_TABLE/counter packets 0/counter packets 1}"
EGRESS_PROBE_LOG="${WORK_DIR}/egress-probe.log"
STUB_EGRESS_OUTCOME=refused
REAL_EGRESS_PROBE="$(declare -f egress_probe)"
REAL_RESOLVE_NAME="$(declare -f resolve_name)"

# shellcheck disable=SC2329 # replaces the selfcheck helper under test
egress_probe() {
  printf '%s\n' "$1" >> "$EGRESS_PROBE_LOG"
  echo "$STUB_EGRESS_OUTCOME"
}

STUB_NFT_OUTPUT="$EGRESS_TABLE"
STUB_NFT_OUTPUT_AFTER="$EGRESS_TABLE_COUNTED"
export STUB_NFT_OUTPUT_AFTER
: > "$EGRESS_PROBE_LOG"
run_selfcheck_function check_egress
check "egress passes twice when the reject rule counted the probe" "2 0" "${RUN_PASSED} ${RUN_FAILED}"
check "egress probes the first host of the first blocked network" "192.0.2.1" "$(cat "$EGRESS_PROBE_LOG")"
check "egress says the reject rule counted the connection" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "counted by the reject rule")"
check "egress report prints no address" "0" \
  "$(count_lines_matching "${WORK_DIR}/run.out" -E '[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+|[0-9a-f]+:[0-9a-f:]+')"

STUB_EGRESS_OUTCOME=timeout
run_selfcheck_function check_egress
check "egress trusts the rule counter when the probe was slow to fail" "2 0" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT_AFTER="$EGRESS_TABLE"
STUB_EGRESS_OUTCOME=refused
run_selfcheck_function check_egress
check "egress fails when the probe fails at once but the reject rule counted nothing" "1 1" "${RUN_PASSED} ${RUN_FAILED}"
check "egress says the reject rule did not count the connection" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "not counted by the reject rule")"

STUB_EGRESS_OUTCOME=connected
run_selfcheck_function check_egress
check "egress fails when the probe connects and nothing was counted" "1 1" "${RUN_PASSED} ${RUN_FAILED}"
STUB_EGRESS_OUTCOME=refused

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain output {\n\t\ttype filter hook output priority filter; policy accept;\n\t\tip daddr { 192.0.2.0/24 } counter reject\n\t\tip6 daddr { 2001:db8:1::/64 } counter reject\n\t}\n}'
STUB_NFT_OUTPUT_AFTER="$STUB_NFT_OUTPUT"
run_selfcheck_function check_egress
check "egress fails when the reject rule shows no packet counter" "1 1" "${RUN_PASSED} ${RUN_FAILED}"
check "egress says the probe cannot be confirmed without a counter" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "cannot be confirmed")"
unset STUB_NFT_OUTPUT_AFTER

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t}\n\tchain output {\n\t\ttype filter hook output priority filter; policy accept;\n\t}\n}'
: > "$EGRESS_PROBE_LOG"
run_selfcheck_function check_egress
check "egress fails with no output reject rule" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "egress does not probe when no reject rule is loaded" "" "$(cat "$EGRESS_PROBE_LOG")"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain output {\n\t\ttype filter hook output priority filter; policy accept;\n\t\tip daddr { 192.0.2.0/24 } counter packets 0 bytes 0 reject with icmpx admin-prohibited\n\t}\n}'
run_selfcheck_function check_egress
check "egress fails when the IPv6 reject rule is missing" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain input {\n\t\ttype filter hook input priority filter; policy drop;\n\t\tip daddr { 192.0.2.0/24 } counter reject\n\t\tip6 daddr { 2001:db8:1::/64 } counter reject\n\t}\n\tchain output {\n\t\ttype filter hook output priority filter; policy accept;\n\t}\n}'
run_selfcheck_function check_egress
check "egress ignores reject rules outside the output chain" "0 1" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT="$EGRESS_TABLE"
check "selfcheck runs the egress check after the firewall check" "1" "$(file_has_line "$SELFCHECK" "  check_egress")"
check "selfcheck runs the DNS check after the egress check" "1" "$(file_has_line "$SELFCHECK" "  check_dns")"

check "selfcheck runs the IPv6 coverage check after the egress check" "1" \
  "$(file_has_line "$SELFCHECK" "  check_egress_ipv6_coverage")"

check "ipv6_groups expands a compressed address" "2001 0db8 0001 0000 0000 0000 0000 0001 " "$(ipv6_groups 2001:db8:1::1)"
check "ipv6_groups expands a leading double colon" "0000 0000 0000 0000 0000 0000 0000 0001 " "$(ipv6_groups ::1)"
check "ipv6_groups ignores a prefix length" "fc00 0000 0000 0000 0000 0000 0000 0000 " "$(ipv6_groups fc00::/7)"
check "ipv6_groups rejects text that is not an address" "" "$(ipv6_groups not-an-address)"
check "ipv6_groups rejects too many groups" "" "$(ipv6_groups 1:2:3:4:5:6:7:8:9)"
check "an address inside a /64 is in range" "0" "$(ipv6_in_range 2001:db8:1:2::5 2001:db8:1:2::/64 && echo 0 || echo 1)"
check "an address outside a /64 is not in range" "1" "$(ipv6_in_range 2001:db8:1:3::5 2001:db8:1:2::/64 && echo 0 || echo 1)"
check "an address in a wider documentation block is in range of its /32" "0" "$(ipv6_in_range 2001:db8:3::1 2001:db8::/32 && echo 0 || echo 1)"
check "a global address is not in fc00::/7" "1" "$(ipv6_in_range 2001:db8::1 fc00::/7 && echo 0 || echo 1)"
check "a prefix that is not a multiple of sixteen bits is honoured" "0" "$(ipv6_in_range 2001:db8:0:7fff::1 2001:db8:0:7000::/52 && echo 0 || echo 1)"
check "a prefix that is not a multiple of sixteen bits excludes what lies beyond it" "1" "$(ipv6_in_range 2001:db8:0:8000::1 2001:db8:0:7000::/52 && echo 0 || echo 1)"

V6_TABLE=$'table inet cabinet_filter {\n\tchain output {\n\t\ttype filter hook output priority filter; policy accept;\n\t\tip daddr { 192.0.2.0/24 } counter packets 0 bytes 0 reject with icmpx admin-prohibited\n\t\tip6 daddr { fc00::/7, fe80::/10, 2001:db8:1::/48, 2001:db8:3::/48 } counter packets 0 bytes 0 reject with icmpx admin-prohibited\n\t}\n}'
STUB_NFT_OUTPUT="$V6_TABLE"

# shellcheck disable=SC2329 # replaces the selfcheck helper under test
host_global_ipv6_addresses() { printf '%s\n' "$STUB_GLOBAL_IPV6"; }

STUB_GLOBAL_IPV6=$'2001:db8:1:5::10\n2001:db8:3::10'
run_selfcheck_function check_egress_ipv6_coverage
check "ipv6 coverage passes when every global address is inside a blocked range" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

STUB_GLOBAL_IPV6=$'2001:db8:2:5::10\n2001:db8:3::10'
run_selfcheck_function check_egress_ipv6_coverage
check "ipv6 coverage fails when a global address is outside every blocked range" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "ipv6 coverage failure names the setting to change" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "CABINET_EGRESS_BLOCKED_RANGES6")"
check "ipv6 coverage failure counts the uncovered addresses" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "1 of 2 global IPv6 addresses")"
check "ipv6 coverage failure prints no address" "0" \
  "$(count_lines_matching "${WORK_DIR}/run.out" -E '[0-9a-f]+:[0-9a-f:]+')"

STUB_GLOBAL_IPV6=""
run_selfcheck_function check_egress_ipv6_coverage
check "ipv6 coverage passes when the host has no global IPv6 address" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

STUB_NFT_OUTPUT=$'table inet cabinet_filter {\n\tchain output {\n\t\ttype filter hook output priority filter; policy accept;\n\t}\n}'
STUB_GLOBAL_IPV6="2001:db8:1:5::10"
run_selfcheck_function check_egress_ipv6_coverage
check "ipv6 coverage fails when no IPv6 range is blocked and the host has a global address" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
STUB_NFT_OUTPUT="$EGRESS_TABLE"

# shellcheck disable=SC2329 # replaces the selfcheck helper under test
resolve_name() { return 0; }
run_selfcheck_function check_dns
check "dns passes when the name resolves" "1 0" "${RUN_PASSED} ${RUN_FAILED}"

# shellcheck disable=SC2329
resolve_name() { return 2; }
run_selfcheck_function check_dns
check "dns fails when the name does not resolve" "0 1" "${RUN_PASSED} ${RUN_FAILED}"
check "dns failure points at the resolver setting" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "CABINET_EGRESS_RESOLVERS")"
unset -f egress_probe resolve_name
eval "$REAL_EGRESS_PROBE"
eval "$REAL_RESOLVE_NAME"

STUB_TIMEOUT_STATUS=1
# shellcheck disable=SC2329 # stands in for the timeout command the probe runs
timeout() { return "$STUB_TIMEOUT_STATUS"; }
check "the real probe reports a failed connection as refused when it fails at once" "refused" \
  "$(egress_probe 192.0.2.1)"
STUB_TIMEOUT_STATUS=0
check "the real probe reports an open connection as connected" "connected" "$(egress_probe 192.0.2.1)"
# shellcheck disable=SC2329
timeout() { sleep 2.1; return 1; }
check "the real probe reports a slow failure as a timeout" "timeout" "$(egress_probe 192.0.2.1)"
unset -f timeout
check "first_blocked_host adds one to the network address" "192.0.2.1" \
  "$(first_blocked_host 'ip daddr { 192.0.2.0/24, 198.51.100.0/24 } reject')"
check "first_blocked_host probes the address itself when the entry is one address" "192.0.2.255" \
  "$(first_blocked_host 'ip daddr { 192.0.2.255/32 } reject')"
check "first_blocked_host probes a single address written without a prefix" "192.0.2.9" \
  "$(first_blocked_host 'ip daddr { 192.0.2.9 } reject')"
check "first_blocked_host stays inside a two-address network" "192.0.2.1" \
  "$(first_blocked_host 'ip daddr { 192.0.2.0/31 } reject')"
check "first_blocked_host carries into the next octet" "192.0.3.0" \
  "$(first_blocked_host 'ip daddr { 192.0.2.255/24 } reject')"

###
### Selfcheck: the unit's limits against the loaded unit file and the container
###
LIMITS_FIXTURE_UNIT="${WORK_DIR}/limits-cabinet.service"
LIMITS_UNIT_WITHOUT_CPU="${WORK_DIR}/limits-no-cpu.service"
cp "${DEPLOY_DIR}/systemd/cabinet.service" "$LIMITS_FIXTURE_UNIT"
grep -v "^CPUQuota=" "${DEPLOY_DIR}/systemd/cabinet.service" > "$LIMITS_UNIT_WITHOUT_CPU"
STUB_UNIT_PATH="$LIMITS_FIXTURE_UNIT"
LIMITS_GOOD_SHOW=$'MemoryHigh=471859200\nMemoryMax=629145600\nMemorySwapMax=0\nCPUQuotaPerSecUSec=800ms\nCPUWeight=50\nTasksMax=512'
STUB_SYSTEMCTL_SHOW_OUTPUT="$LIMITS_GOOD_SHOW"
STUB_CONTAINER_MEMORY_KIB=1048576
STUB_CONTAINER_SWAP_KIB=524288
STUB_CONTAINER_CORES=1

# shellcheck disable=SC2329 # invoked by the selfcheck function under test
systemctl() {
  printf 'systemctl %s\n' "$*" >> "$STUB_ARGS_LOG"
  case "$*" in
    *"--value"*) printf '%s\n' "$STUB_UNIT_PATH" ;;
    *) printf '%s\n' "$STUB_SYSTEMCTL_SHOW_OUTPUT" ;;
  esac
}
# shellcheck disable=SC2329
container_memory_kib() { echo "$STUB_CONTAINER_MEMORY_KIB"; }
# shellcheck disable=SC2329
container_swap_kib() { echo "$STUB_CONTAINER_SWAP_KIB"; }
# shellcheck disable=SC2329
container_cores() { echo "$STUB_CONTAINER_CORES"; }

run_selfcheck_function check_limits
check "limits pass when the effective values match the unit and sit below the container" "9 0" \
  "${RUN_PASSED} ${RUN_FAILED}"
check "limits report the container's memory, swap and cores as numbers" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "the container shows 1024 MiB memory, 512 MiB swap and 1 cores")"
check "limits read the effective values with one systemctl show" "1" \
  "$(count_lines_matching "$STUB_ARGS_LOG" -F 'systemctl show cabinet.service --property=MemoryHigh,MemoryMax,MemorySwapMax,CPUQuotaPerSecUSec,CPUWeight,TasksMax')"

STUB_SYSTEMCTL_SHOW_OUTPUT=$'MemoryHigh=471859200\nMemoryMax=infinity\nMemorySwapMax=0\nCPUQuotaPerSecUSec=800ms\nCPUWeight=50\nTasksMax=512'
run_selfcheck_function check_limits
check "limits fail when MemoryMax is infinity" "1" "$(file_has_text "${WORK_DIR}/run.out" "FAIL - MemoryMax")"
check "limits count one failure for an infinite MemoryMax" "1" "$RUN_FAILED"

STUB_SYSTEMCTL_SHOW_OUTPUT=$'MemoryHigh=471859200\nMemoryMax=629145600\nMemorySwapMax=infinity\nCPUQuotaPerSecUSec=800ms\nCPUWeight=50\nTasksMax=512'
run_selfcheck_function check_limits
check "limits fail when swap is not capped at zero" "1" "$(file_has_text "${WORK_DIR}/run.out" "FAIL - MemorySwapMax")"

STUB_SYSTEMCTL_SHOW_OUTPUT=$'MemoryHigh=471859200\nMemoryMax=629145600\nMemorySwapMax=0\nCPUQuotaPerSecUSec=500ms\nCPUWeight=50\nTasksMax=512'
run_selfcheck_function check_limits
check "limits fail when the effective CPU quota differs from the unit file" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "FAIL - CPUQuota")"

STUB_SYSTEMCTL_SHOW_OUTPUT="$LIMITS_GOOD_SHOW"
STUB_UNIT_PATH="$LIMITS_UNIT_WITHOUT_CPU"
run_selfcheck_function check_limits
check "limits fail when the unit file has no CPUQuota" "1" "$(file_has_text "${WORK_DIR}/run.out" "FAIL - CPUQuota")"
STUB_UNIT_PATH="$LIMITS_FIXTURE_UNIT"

STUB_CONTAINER_MEMORY_KIB=524288
run_selfcheck_function check_limits
check "limits fail when the container has less memory than MemoryMax" "1" "$RUN_FAILED"
check "limits name the margin when the container is too small" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "128 MiB margin")"

STUB_CONTAINER_MEMORY_KIB=716800
run_selfcheck_function check_limits
check "limits fail when MemoryMax leaves less than 128 MiB for the rest of the container" "1" "$RUN_FAILED"
check "limits name the margin when it is not left over" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "128 MiB margin")"
STUB_CONTAINER_MEMORY_KIB=1048576

STUB_SYSTEMCTL_SHOW_OUTPUT=$'MemoryHigh=471859200\nMemoryMax=629145600\nMemorySwapMax=0\nCPUQuotaPerSecUSec=1s\nCPUWeight=50\nTasksMax=512'
sed -i 's/^CPUQuota=80%$/CPUQuota=100%/' "$LIMITS_FIXTURE_UNIT"
run_selfcheck_function check_limits
check "limits fail with one core and a quota of a whole core" "1" "$RUN_FAILED"
check "limits say the CPU quota reaches the container's cores" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "FAIL - the CPU quota")"

STUB_SYSTEMCTL_SHOW_OUTPUT=$'MemoryHigh=471859200\nMemoryMax=629145600\nMemorySwapMax=0\nCPUQuotaPerSecUSec=1.2s\nCPUWeight=50\nTasksMax=512'
sed -i 's/^CPUQuota=100%$/CPUQuota=120%/' "$LIMITS_FIXTURE_UNIT"
run_selfcheck_function check_limits
check "limits fail with one core and a quota above a whole core" "1" \
  "$(file_has_text "${WORK_DIR}/run.out" "FAIL - the CPU quota")"

STUB_CONTAINER_CORES=2
STUB_SYSTEMCTL_SHOW_OUTPUT=$'MemoryHigh=471859200\nMemoryMax=629145600\nMemorySwapMax=0\nCPUQuotaPerSecUSec=1s\nCPUWeight=50\nTasksMax=512'
sed -i 's/^CPUQuota=120%$/CPUQuota=100%/' "$LIMITS_FIXTURE_UNIT"
run_selfcheck_function check_limits
check "limits pass with two cores and a quota of a whole core" "9 0" "${RUN_PASSED} ${RUN_FAILED}"
check "limits report the core count" "1" "$(file_has_text "${WORK_DIR}/run.out" "and 2 cores")"

STUB_CONTAINER_CORES=1
STUB_SYSTEMCTL_SHOW_OUTPUT="$LIMITS_GOOD_SHOW"
cp "${DEPLOY_DIR}/systemd/cabinet.service" "$LIMITS_FIXTURE_UNIT"
run_selfcheck_function check_limits
check "limits report names no address" "0" \
  "$(count_lines_matching "${WORK_DIR}/run.out" -E '[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+|[0-9a-f]*:[0-9a-f:]+/[0-9]+')"
check "selfcheck runs the limits check after the units check" "1" "$(file_has_line "$SELFCHECK" "  check_limits")"
unset -f systemctl container_memory_kib container_swap_kib container_cores

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

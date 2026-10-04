#!/usr/bin/env bash
# Proves the decision logic of the installer without root, network, systemd
# or the .NET SDK: version comparison, configuration loading, atomic
# activation, pruning, rollback, health-acceptance outcomes, the
# rejected-version memory, the poll decisions and the quiet poll statuses.
# Everything runs against a relocated temporary root; the service manager and
# polkit stand-ins from the host guard record any call that slips through.
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

WORK="$(mktemp -d)"
trap 'rm -rf "${WORK}"' EXIT
export CABINET_DEPLOY_ROOT="${WORK}/root"
mkdir -p "$CABINET_DEPLOY_ROOT"
# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$WORK"

# shellcheck source=deploy/bin/cabinet-deploy
source "${REPO_ROOT}/deploy/bin/cabinet-deploy"

FAILURES=0

check() {
  local description="$1"
  local expected="$2"
  local actual="$3"
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected [%s], got [%s])\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

contains() {
  case "$1" in
    *"$2"*) printf 'yes' ;;
    *) printf 'no' ;;
  esac
}

count_lines() {
  if [ -f "$1" ]; then
    wc -l < "$1" | tr -d ' '
  else
    printf '0'
  fi
}

# --- cabinet_semver_gt ---------------------------------------------------

check "1.10.0 > 1.9.0" "0" "$(cabinet_semver_gt 1.10.0 1.9.0; echo $?)"
check "2.0.0 > 1.99.99" "0" "$(cabinet_semver_gt 2.0.0 1.99.99; echo $?)"
check "1.2.3 is not greater than 1.2.3" "1" "$(cabinet_semver_gt 1.2.3 1.2.3; echo $?)"
check "0.0.9 is not greater than 0.0.10" "1" "$(cabinet_semver_gt 0.0.9 0.0.10; echo $?)"

# --- cabinet_load_conf -----------------------------------------------------

CONF_FILE="${WORK}/loader.conf"
printf '%s\n' \
  'CABINET_GITHUB_REPO="example-owner/example-repo"' \
  "CABINET_KEEP_RELEASES='7'" \
  'CABINET_UNLISTED_KEY=ignored' \
  "\$(touch ${WORK}/injected)" \
  > "$CONF_FILE"
chmod 600 "$CONF_FILE"
CABINET_GITHUB_REPO="" CABINET_KEEP_RELEASES=""
cabinet_load_conf "$CONF_FILE"
check "loader strips double quotes" "example-owner/example-repo" "$CABINET_GITHUB_REPO"
check "loader strips single quotes" "7" "$CABINET_KEEP_RELEASES"
check "loader ignores keys outside the allow-list" "unset" "${CABINET_UNLISTED_KEY-unset}"
check "loader never executes the file" "absent" "$([ -e "${WORK}/injected" ] && echo present || echo absent)"

chmod 666 "$CONF_FILE"
LOADER_EXIT=0
(cabinet_load_conf "$CONF_FILE") >/dev/null 2>&1 || LOADER_EXIT=$?
check "loader refuses a world-writable file" "1" "$LOADER_EXIT"
chmod 620 "$CONF_FILE"
LOADER_EXIT=0
(cabinet_load_conf "$CONF_FILE") >/dev/null 2>&1 || LOADER_EXIT=$?
check "loader refuses a group-writable file" "1" "$LOADER_EXIT"

# --- cabinet_activate_release ----------------------------------------------

ACTIVATE_ROOT="${WORK}/activate"
mkdir -p "${ACTIVATE_ROOT}/releases/1.0.0" "${ACTIVATE_ROOT}/releases/1.1.0" "${ACTIVATE_ROOT}/state"
ln -s "${ACTIVATE_ROOT}/releases/1.0.0" "${ACTIVATE_ROOT}/current"

cabinet_activate_release "1.1.0" "${ACTIVATE_ROOT}/releases" "${ACTIVATE_ROOT}/current" "${ACTIVATE_ROOT}/state"

check "activation repoints current at the new release" "1.1.0" \
  "$(basename "$(readlink -f "${ACTIVATE_ROOT}/current")")"
check "activation records the previous version" "1.0.0" "$(cat "${ACTIVATE_ROOT}/state/previous")"
check "no temporary link remains after activation" "1" \
  "$(find "${ACTIVATE_ROOT}" -maxdepth 1 -name 'current*' | wc -l | tr -d ' ')"

# --- cabinet_prune_releases -------------------------------------------------

PRUNE_ROOT="${WORK}/prune"
mkdir -p "${PRUNE_ROOT}/state"
for v in 1.0.0 1.1.0 1.2.0 1.3.0 1.4.0 1.5.0 1.6.0; do
  mkdir -p "${PRUNE_ROOT}/releases/${v}"
done
ln -s "${PRUNE_ROOT}/releases/1.6.0" "${PRUNE_ROOT}/current"
printf '1.5.0\n' > "${PRUNE_ROOT}/state/previous"

cabinet_prune_releases "${PRUNE_ROOT}/releases" "${PRUNE_ROOT}/current" "${PRUNE_ROOT}/state" 3

REMAINING="$(find "${PRUNE_ROOT}/releases" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort -V | tr '\n' ' ')"
check "prune with seven releases and keep 3 removes the four oldest" "1.4.0 1.5.0 1.6.0 " "$REMAINING"

PRUNE_PROTECT_ROOT="${WORK}/prune-protect"
mkdir -p "${PRUNE_PROTECT_ROOT}/state"
for v in 1.0.0 1.1.0 1.2.0 1.3.0; do
  mkdir -p "${PRUNE_PROTECT_ROOT}/releases/${v}"
done
ln -s "${PRUNE_PROTECT_ROOT}/releases/1.3.0" "${PRUNE_PROTECT_ROOT}/current"
printf '1.0.0\n' > "${PRUNE_PROTECT_ROOT}/state/previous"
cabinet_prune_releases "${PRUNE_PROTECT_ROOT}/releases" "${PRUNE_PROTECT_ROOT}/current" "${PRUNE_PROTECT_ROOT}/state" 1
check "prune never removes the active or previous release" "1.0.0 1.3.0 " \
  "$(find "${PRUNE_PROTECT_ROOT}/releases" -mindepth 1 -maxdepth 1 -printf '%f\n' | sort -V | tr '\n' ' ')"

# --- Seams shared by the install and rollback cases -------------------------

RESTART_LOG="${WORK}/restarts.log"
HEALTH_LOG="${WORK}/health.log"
HEALTH_SEQUENCE=()

cabinet_restart_app() {
  printf 'restart\n' >> "$RESTART_LOG"
}

cabinet_wait_for_health() {
  printf '%s\n' "$2" >> "$HEALTH_LOG"
  local result="${HEALTH_SEQUENCE[0]:-1}"
  HEALTH_SEQUENCE=("${HEALTH_SEQUENCE[@]:1}")
  return "$result"
}

make_release_zip() {
  local zip="$1" manifest_version="$2"
  local stage
  stage="$(mktemp -d "${WORK}/zip-stage.XXXXXX")"
  mkdir -p "${stage}/app"
  printf '{"version":"%s","commit":"0123456789abcdef0123456789abcdef01234567"}\n' "$manifest_version" \
    > "${stage}/release-manifest.json"
  printf 'placeholder\n' > "${stage}/app/placeholder.txt"
  (cd "$stage" && find . -type f | sed 's|^\./||' | sort | zip -X -q "$zip" -@)
}

fresh_core_root() {
  CORE_ROOT="$(mktemp -d "${WORK}/core.XXXXXX")"
  mkdir -p "${CORE_ROOT}/releases/1.0.0" "${CORE_ROOT}/state"
  printf '{"version":"1.0.0"}\n' > "${CORE_ROOT}/releases/1.0.0/release-manifest.json"
  ln -s "${CORE_ROOT}/releases/1.0.0" "${CORE_ROOT}/current"
  : > "$RESTART_LOG"
  : > "$HEALTH_LOG"
}

run_core_install() {
  local version="$1" active="$2" zip="$3"
  CORE_RC=0
  CORE_LOG="${WORK}/core.log"
  cabinet_install_verified_release "v${version}" "$version" "$zip" "$active" \
    "${CORE_ROOT}/releases" "${CORE_ROOT}/current" "${CORE_ROOT}/state" 3 \
    "http://127.0.0.1:0" 5 2>"$CORE_LOG" || CORE_RC=$?
  CORE_OUTPUT="$(cat "$CORE_LOG")"
}

# --- cabinet_install_verified_release outcomes ------------------------------

make_release_zip "${WORK}/cabinet-1.1.0.zip" "1.1.0"

fresh_core_root
HEALTH_SEQUENCE=(0)
run_core_install 1.1.0 1.0.0 "${WORK}/cabinet-1.1.0.zip"
check "healthy install returns success" "0" "$CORE_RC"
check "healthy install activates the new release" "1.1.0" "$(basename "$(readlink -f "${CORE_ROOT}/current")")"
check "healthy install restarts the application once" "1" "$(count_lines "$RESTART_LOG")"
check "health is awaited for the new version" "1.1.0" "$(head -n 1 "$HEALTH_LOG")"
check "the previous release is remembered" "1.0.0" "$(cat "${CORE_ROOT}/state/previous")"

fresh_core_root
HEALTH_SEQUENCE=(1 0)
run_core_install 1.1.0 1.0.0 "${WORK}/cabinet-1.1.0.zip"
check "unhealthy install with a healthy previous release reports rolled back" "$CABINET_INSTALL_ROLLED_BACK" "$CORE_RC"
check "rolled back install leaves the previous release active" "1.0.0" "$(basename "$(readlink -f "${CORE_ROOT}/current")")"
check "rolled back install restarts for the install and for the rollback" "2" "$(count_lines "$RESTART_LOG")"
check "rollback awaits health for the previous version" "1.0.0" "$(tail -n 1 "$HEALTH_LOG")"
check "rolled back install is not logged as a failed rollback" "no" "$(contains "$CORE_OUTPUT" "rollback to 1.0.0 failed")"
check "an automatic rollback keeps the last good release as the previous one" "1.0.0" "$(cat "${CORE_ROOT}/state/previous")"

fresh_core_root
HEALTH_SEQUENCE=(1 1)
run_core_install 1.1.0 1.0.0 "${WORK}/cabinet-1.1.0.zip"
check "a rollback that is itself unhealthy reports failed" "$CABINET_INSTALL_FAILED" "$CORE_RC"
check "a failed rollback is logged at error level" "yes" "$(contains "$CORE_OUTPUT" "ERROR: rollback to 1.0.0 failed")"

fresh_core_root
rm -f "${CORE_ROOT}/current"
HEALTH_SEQUENCE=(1)
run_core_install 1.1.0 "" "${WORK}/cabinet-1.1.0.zip"
check "unhealthy first install with nothing to return to reports failed" "$CABINET_INSTALL_FAILED" "$CORE_RC"
check "a first install is not rolled back" "1" "$(count_lines "$RESTART_LOG")"

fresh_core_root
make_release_zip "${WORK}/cabinet-1.2.0-mismatch.zip" "9.9.9"
MISMATCH_EXIT=0
(
  cabinet_install_verified_release "v1.2.0" "1.2.0" "${WORK}/cabinet-1.2.0-mismatch.zip" "1.0.0" \
    "${CORE_ROOT}/releases" "${CORE_ROOT}/current" "${CORE_ROOT}/state" 3 "http://127.0.0.1:0" 5
) >/dev/null 2>&1 || MISMATCH_EXIT=$?
check "a manifest naming another version is refused" "1" "$MISMATCH_EXIT"
check "a refused manifest leaves the active release" "1.0.0" "$(basename "$(readlink -f "${CORE_ROOT}/current")")"
check "a refused manifest leaves no staging directory" "0" \
  "$(find "${CORE_ROOT}/releases" -maxdepth 1 -name '.staging-*' | wc -l | tr -d ' ')"
check "a refused manifest never restarts the application" "0" "$(count_lines "$RESTART_LOG")"

# --- cabinet_rollback_release -------------------------------------------------

fresh_core_root
mkdir -p "${CORE_ROOT}/releases/1.1.0"
ln -sfn "${CORE_ROOT}/releases/1.1.0" "${CORE_ROOT}/current"
HEALTH_SEQUENCE=(0)
ROLLBACK_RC=0
cabinet_rollback_release "1.0.0" "${CORE_ROOT}/releases" "${CORE_ROOT}/current" "${CORE_ROOT}/state" \
  "http://127.0.0.1:0" 5 2>/dev/null || ROLLBACK_RC=$?
check "rollback reactivates an earlier release" "1.0.0" "$(basename "$(readlink -f "${CORE_ROOT}/current")")"
check "rollback of a healthy earlier release succeeds" "0" "$ROLLBACK_RC"
check "a rollback does not record the release it left as the previous one" "" \
  "$(cat "${CORE_ROOT}/state/previous" 2>/dev/null || true)"

HEALTH_SEQUENCE=(1)
ROLLBACK_RC=0
ROLLBACK_LOG="$(cabinet_rollback_release "1.1.0" "${CORE_ROOT}/releases" "${CORE_ROOT}/current" "${CORE_ROOT}/state" \
  "http://127.0.0.1:0" 5 2>&1)" || ROLLBACK_RC=$?
check "rollback to an unhealthy release fails" "1" "$ROLLBACK_RC"
check "rollback failure is logged at error level" "yes" "$(contains "$ROLLBACK_LOG" "ERROR:")"
check "an unrequested restore leaves the unhealthy target active" "1.1.0" \
  "$(basename "$(readlink -f "${CORE_ROOT}/current")")"

ln -sfn "${CORE_ROOT}/releases/1.0.0" "${CORE_ROOT}/current"
: > "$RESTART_LOG"
HEALTH_SEQUENCE=(1)
ROLLBACK_RC=0
cabinet_rollback_release "1.1.0" "${CORE_ROOT}/releases" "${CORE_ROOT}/current" "${CORE_ROOT}/state" \
  "http://127.0.0.1:0" 5 yes 2>/dev/null || ROLLBACK_RC=$?
check "a failed rollback that asked for a restore reports failure" "1" "$ROLLBACK_RC"
check "a failed rollback puts the release it replaced back" "1.0.0" \
  "$(basename "$(readlink -f "${CORE_ROOT}/current")")"
check "restoring the replaced release restarts the application again" "2" "$(count_lines "$RESTART_LOG")"

MISSING_EXIT=0
(cabinet_rollback_release "0.0.1" "${CORE_ROOT}/releases" "${CORE_ROOT}/current" "${CORE_ROOT}/state" \
  "http://127.0.0.1:0" 5) >/dev/null 2>&1 || MISSING_EXIT=$?
check "rollback to a release that is not on disk is refused" "1" "$MISSING_EXIT"

# --- Rejected-version memory -------------------------------------------------

REJECT_STATE="${WORK}/reject-state"
check "nothing is rejected when no marker exists" "" "$(cabinet_read_rejected_version "$REJECT_STATE")"
check "no version is rejected when no marker exists" "1" "$(cabinet_is_rejected 0.0.2 "$REJECT_STATE"; echo $?)"

cabinet_record_rejected_version "0.0.5" "$REJECT_STATE"
check "record then read returns the version" "0.0.5" "$(cabinet_read_rejected_version "$REJECT_STATE")"
check "the marker holds exactly one line" "1" "$(count_lines "${REJECT_STATE}/rejected")"
check "no temporary file is left in the state directory" "0" \
  "$(find "$REJECT_STATE" -maxdepth 1 -name '.rejected.*' | wc -l | tr -d ' ')"
check "a version below the recorded one is rejected" "0" "$(cabinet_is_rejected 0.0.4 "$REJECT_STATE"; echo $?)"
check "the recorded version itself is rejected" "0" "$(cabinet_is_rejected 0.0.5 "$REJECT_STATE"; echo $?)"
check "a version above the recorded one is not rejected" "1" "$(cabinet_is_rejected 0.0.6 "$REJECT_STATE"; echo $?)"

cabinet_record_rejected_version "0.0.9" "$REJECT_STATE"
check "recording again replaces the marker" "0.0.9" "$(cabinet_read_rejected_version "$REJECT_STATE")"
cabinet_record_rejected_version "0.0.4" "$REJECT_STATE"
check "recording a lower version keeps the higher marker" "0.0.9" "$(cabinet_read_rejected_version "$REJECT_STATE")"
cabinet_record_rejected_version "0.0.9" "$REJECT_STATE"
check "recording the same version keeps the marker" "0.0.9" "$(cabinet_read_rejected_version "$REJECT_STATE")"
cabinet_clear_rejected_version "$REJECT_STATE" "0.0.7"
check "installing a version below the marker keeps it" "0.0.9" "$(cabinet_read_rejected_version "$REJECT_STATE")"
cabinet_clear_rejected_version "$REJECT_STATE" "0.0.9"
check "installing the marked version clears it" "" "$(cabinet_read_rejected_version "$REJECT_STATE")"
cabinet_record_rejected_version "0.0.9" "$REJECT_STATE"
cabinet_clear_rejected_version "$REJECT_STATE" "0.1.0"
check "installing a version above the marker clears it" "" "$(cabinet_read_rejected_version "$REJECT_STATE")"
cabinet_record_rejected_version "0.0.9" "$REJECT_STATE"
cabinet_clear_rejected_version "$REJECT_STATE"
check "clear removes the marker" "" "$(cabinet_read_rejected_version "$REJECT_STATE")"
check "nothing is rejected after clearing" "1" "$(cabinet_is_rejected 0.0.1 "$REJECT_STATE"; echo $?)"

# --- Latest release fetch statuses ---------------------------------------------

FIXTURE_STATUS=200
FIXTURE_HEADERS=""
FIXTURE_BODY=""
HTTP_CALLS="${WORK}/http-calls.log"
: > "$HTTP_CALLS"

cabinet_http_get() {
  printf 'call\n' >> "$HTTP_CALLS"
  printf '%b' "$FIXTURE_HEADERS" > "$2"
  printf '%s' "$FIXTURE_BODY" > "$3"
  printf '%s' "$FIXTURE_STATUS"
}

fetch() {
  F_RC=0
  F_OUT="$(cabinet_fetch_latest_release example-owner/example-repo 2>"${WORK}/fetch.log")" || F_RC=$?
  F_LOG="$(cat "${WORK}/fetch.log")"
}

FIXTURE_STATUS=200 FIXTURE_BODY='{"tag_name":"v1.2.3"}' FIXTURE_HEADERS='HTTP/2 200\r\n'
fetch
check "200 with a strict tag returns the tag" "v1.2.3" "$F_OUT"
check "200 with a strict tag succeeds" "0" "$F_RC"

for malformed in 'v1.2' '1.2.3' 'v01.2.3' 'v1.2.3-rc1' ''; do
  FIXTURE_STATUS=200 FIXTURE_BODY="{\"tag_name\":\"${malformed}\"}"
  fetch
  check "200 with the malformed tag '${malformed}' fails" "1" "$F_RC"
done

FIXTURE_STATUS=200 FIXTURE_BODY='not json at all'
fetch
check "200 with an unreadable body fails" "1" "$F_RC"

FIXTURE_STATUS=404 FIXTURE_BODY='{"message":"Not Found"}' FIXTURE_HEADERS='HTTP/2 404\r\n'
fetch
check "404 is a quiet outcome" "$CABINET_STATUS_QUIET" "$F_RC"
check "404 logs that no release is published yet" "yes" "$(contains "$F_LOG" "no published release yet")"
check "404 is not logged as an error" "no" "$(contains "$F_LOG" "ERROR")"

FIXTURE_STATUS=403 FIXTURE_BODY='{}' FIXTURE_HEADERS='HTTP/2 403\r\nX-RateLimit-Remaining: 0\r\nX-RateLimit-Reset: 1700000000\r\n'
fetch
check "403 with no remaining requests is a quiet outcome" "$CABINET_STATUS_QUIET" "$F_RC"
check "the rate limit reset time is logged" "yes" "$(contains "$F_LOG" "2023-11-14T22:13:20Z")"
check "a quiet rate limit is not logged as an error" "no" "$(contains "$F_LOG" "ERROR")"

FIXTURE_STATUS=429 FIXTURE_HEADERS='HTTP/2 429\r\nretry-after: 60\r\n'
fetch
check "429 with a retry-after header is a quiet outcome" "$CABINET_STATUS_QUIET" "$F_RC"

FIXTURE_STATUS=403 FIXTURE_HEADERS='HTTP/2 403\r\nx-ratelimit-remaining: 12\r\n'
fetch
check "403 with requests remaining and no retry-after fails" "1" "$F_RC"
check "403 without a rate limit signal is logged as an error" "yes" "$(contains "$F_LOG" "ERROR")"

FIXTURE_STATUS=403 FIXTURE_HEADERS='HTTP/2 403\r\n'
fetch
check "403 without any rate limit header fails" "1" "$F_RC"

FIXTURE_STATUS=500 FIXTURE_HEADERS='HTTP/2 500\r\n'
fetch
check "500 fails" "1" "$F_RC"

FIXTURE_STATUS=000 FIXTURE_HEADERS=''
fetch
check "a network error fails" "1" "$F_RC"

FIXTURE_STATUS=200 FIXTURE_BODY='{"tag_name":"v2.0.0"}' FIXTURE_HEADERS='HTTP/2 200\r\nx-ratelimit-remaining: 41\r\n'
fetch
check "the remaining rate limit is logged when present" "yes" "$(contains "$F_LOG" "github api rate limit remaining: 41")"
FIXTURE_HEADERS='HTTP/2 200\r\n'
fetch
check "no rate limit line is logged when the header is absent" "no" "$(contains "$F_LOG" "rate limit remaining")"

# --- Poll decisions --------------------------------------------------------------

load_configuration_for_tests() {
  mkdir -p "${CABINET_DEPLOY_ROOT}/etc/cabinet"
  printf 'CABINET_GITHUB_REPO=example-owner/example-repo\n' > "${CABINET_DEPLOY_ROOT}/etc/cabinet/deploy.conf"
  chmod 600 "${CABINET_DEPLOY_ROOT}/etc/cabinet/deploy.conf"
  load_configuration
}
load_configuration_for_tests

# --- Numeric configuration is validated before anything is activated ---------------

CONF_PATH="${WORK}/numeric.conf"
INJECTION_MARKER="${WORK}/arithmetic-injected"

load_with_conf() {
  printf '%s\n' 'CABINET_GITHUB_REPO=example-owner/example-repo' "$@" > "$CONF_PATH"
  chmod 600 "$CONF_PATH"
  LOAD_RC=0
  LOAD_LOG="$( (load_configuration) 2>&1 )" || LOAD_RC=$?
}

load_with_conf 'CABINET_KEEP_RELEASES=5' 'CABINET_HEALTH_TIMEOUT_SECONDS=30'
check "whole-number settings are accepted" "0" "$LOAD_RC"

for bad_value in '60s' '' '0' '-3' '1.5' '1234567' ' 7' "PATH[\$(touch ${INJECTION_MARKER})0]"; do
  load_with_conf "CABINET_HEALTH_TIMEOUT_SECONDS=${bad_value}"
  check "timeout '${bad_value}' is refused at load time" "1" "$LOAD_RC"
  check "the timeout refusal names the setting" "yes" "$(contains "$LOAD_LOG" "CABINET_HEALTH_TIMEOUT_SECONDS must be a positive whole number")"
  load_with_conf "CABINET_KEEP_RELEASES=${bad_value}"
  check "release count '${bad_value}' is refused at load time" "1" "$LOAD_RC"
  check "the release count refusal names the setting" "yes" "$(contains "$LOAD_LOG" "CABINET_KEEP_RELEASES must be a positive whole number")"
done
check "no numeric setting is ever evaluated as arithmetic" "absent" \
  "$([ -e "$INJECTION_MARKER" ] && echo present || echo absent)"

LOAD_RC=0
(CABINET_HEALTH_INTERVAL_SECONDS="PATH[\$(touch ${INJECTION_MARKER})0]"; load_with_conf 'CABINET_KEEP_RELEASES=5'; [ "$LOAD_RC" -eq 1 ]) || LOAD_RC=$?
check "a malformed health interval from the environment is refused" "0" "$LOAD_RC"
check "the health interval is never evaluated as arithmetic" "absent" \
  "$([ -e "$INJECTION_MARKER" ] && echo present || echo absent)"

CABINET_HEALTH_INTERVAL_SECONDS=""
unset CABINET_HEALTH_INTERVAL_SECONDS
load_with_conf 'CABINET_KEEP_RELEASES=5'
check "the health interval defaults to a valid number" "0" "$LOAD_RC"

CONF_PATH="${CABINET_DEPLOY_CONF:-${CABINET_DEPLOY_ROOT}/etc/cabinet/deploy.conf}"
load_configuration_for_tests

REAL_CMD_INSTALL="$(declare -f cmd_install)"
INSTALL_CALLS="${WORK}/install-calls.log"
cmd_install() {
  printf '%s\n' "$*" >> "$INSTALL_CALLS"
}

set_active() {
  rm -rf "${CABINET_DEPLOY_ROOT}/opt/cabinet"
  mkdir -p "${RELEASES_DIR}/$1"
  ln -s "${RELEASES_DIR}/$1" "$CURRENT_LINK"
}

run_poll() {
  local tag="$1"
  : > "$INSTALL_CALLS"
  : > "$HTTP_CALLS"
  FIXTURE_STATUS=200 FIXTURE_BODY="{\"tag_name\":\"${tag}\"}" FIXTURE_HEADERS='HTTP/2 200\r\nx-ratelimit-remaining: 50\r\n'
  P_RC=0
  P_LOG="$(cmd_poll 2>&1 >/dev/null)" || P_RC=$?
}

set_active 0.0.5
cabinet_record_rejected_version "0.0.7" "$STATE_DIR"
run_poll v0.0.7
check "poll with the latest equal to the rejected version succeeds" "0" "$P_RC"
check "poll skips the rejected version without installing" "0" "$(count_lines "$INSTALL_CALLS")"
check "the skip names the version as rolled back after a failed health check" "yes" \
  "$(contains "$P_LOG" "release 0.0.7 was rolled back after a failed health check; waiting for a newer release")"
check "a skip makes exactly one request" "1" "$(count_lines "$HTTP_CALLS")"

run_poll v0.0.6
check "poll with the latest below the rejected version does not install" "0" "$(count_lines "$INSTALL_CALLS")"

run_poll v0.0.8
check "poll with a release newer than active and rejected succeeds" "0" "$P_RC"
check "poll installs the newer release by tag" "v0.0.8" "$(cat "$INSTALL_CALLS")"

run_poll v0.0.5
check "poll with the latest equal to the active version does not install" "0" "$(count_lines "$INSTALL_CALLS")"
check "poll reports being up to date" "yes" "$(contains "$P_LOG" "up to date at 0.0.5")"

run_poll v0.0.4
check "poll with a latest older than active does not install" "0" "$(count_lines "$INSTALL_CALLS")"
check "a late lower publish exits successfully" "0" "$P_RC"

cabinet_clear_rejected_version "$STATE_DIR"
run_poll v0.0.6
check "poll installs when nothing is rejected" "v0.0.6" "$(cat "$INSTALL_CALLS")"

rm -rf "${CABINET_DEPLOY_ROOT}/opt/cabinet"
run_poll v0.0.1
check "poll installs the first release when none is active" "v0.0.1" "$(cat "$INSTALL_CALLS")"

set_active 0.0.5
: > "$INSTALL_CALLS"
FIXTURE_STATUS=404 FIXTURE_BODY='{}' FIXTURE_HEADERS='HTTP/2 404\r\n'
P_RC=0
P_LOG="$(cmd_poll 2>&1 >/dev/null)" || P_RC=$?
check "poll with no published release exits 0" "0" "$P_RC"
check "poll with no published release logs it" "yes" "$(contains "$P_LOG" "no published release yet")"
check "poll with no published release installs nothing" "0" "$(count_lines "$INSTALL_CALLS")"

FIXTURE_STATUS=403 FIXTURE_BODY='{}' FIXTURE_HEADERS='HTTP/2 403\r\nx-ratelimit-remaining: 0\r\n'
P_RC=0
P_LOG="$(cmd_poll 2>&1 >/dev/null)" || P_RC=$?
check "poll while rate limited exits 0" "0" "$P_RC"
check "poll while rate limited installs nothing" "0" "$(count_lines "$INSTALL_CALLS")"

FIXTURE_STATUS=403 FIXTURE_BODY='{}' FIXTURE_HEADERS='HTTP/2 403\r\n'
P_RC=0
P_LOG="$(cmd_poll 2>&1 >/dev/null)" || P_RC=$?
check "poll with an unexplained 403 exits 1" "1" "$P_RC"

FIXTURE_STATUS=500 FIXTURE_BODY='{}' FIXTURE_HEADERS='HTTP/2 500\r\n'
P_RC=0
P_LOG="$(cmd_poll 2>&1 >/dev/null)" || P_RC=$?
check "poll with a server error exits 1" "1" "$P_RC"

FIXTURE_STATUS=000 FIXTURE_BODY='' FIXTURE_HEADERS=''
P_RC=0
P_LOG="$(cmd_poll 2>&1 >/dev/null)" || P_RC=$?
check "poll with a network error exits 1" "1" "$P_RC"

# --- Install outcomes and the rejected marker -------------------------------------

eval "$REAL_CMD_INSTALL"

INSTALL_STATUS_TO_RETURN=0
INSTALL_CORE_CALLS="${WORK}/install-core-calls.log"
INSTALL_CORE_ARTIFACTS="${WORK}/install-core-artifacts.log"
: > "$INSTALL_CORE_ARTIFACTS"
cabinet_install_verified_release() {
  printf '%s\n' "$1" >> "$INSTALL_CORE_CALLS"
  printf '%s\n' "$3" >> "$INSTALL_CORE_ARTIFACTS"
  return "$INSTALL_STATUS_TO_RETURN"
}

# shellcheck disable=SC2329
cabinet_verify_attestation() {
  printf '%s' "0123456789abcdef0123456789abcdef01234567"
}

# shellcheck disable=SC2329
cabinet_commit_on_branch() {
  return 0
}

make_assets() {
  local version="$1" dir="$2"
  mkdir -p "$dir"
  printf 'synthetic artifact %s\n' "$version" > "${dir}/cabinet-${version}.zip"
  (cd "$dir" && sha256sum "cabinet-${version}.zip" > "cabinet-${version}.zip.sha256")
  printf 'placeholder bundle\n' > "${dir}/cabinet-${version}.zip.sigstore.json"
}

run_install() {
  I_RC=0
  : > "$INSTALL_CORE_CALLS"
  I_LOG="$( (cmd_install "$@") 2>&1 >/dev/null)" || I_RC=$?
}

make_assets 0.0.6 "${WORK}/assets-0.0.6"
make_assets 0.0.7 "${WORK}/assets-0.0.7"

set_active 0.0.5

INSTALL_STATUS_TO_RETURN="$CABINET_INSTALL_ROLLED_BACK"
run_install v0.0.6 --from-dir "${WORK}/assets-0.0.6"
check "a rolled back install exits non-zero" "1" "$I_RC"
check "a rolled back install records the version as rejected" "0.0.6" "$(cabinet_read_rejected_version "$STATE_DIR")"
check "a rolled back install is logged at error level" "yes" "$(contains "$I_LOG" "ERROR:")"

cabinet_clear_rejected_version "$STATE_DIR"
INSTALL_STATUS_TO_RETURN="$CABINET_INSTALL_FAILED"
run_install v0.0.6 --from-dir "${WORK}/assets-0.0.6"
check "a failed install exits non-zero" "1" "$I_RC"
check "a failed install records the version as rejected" "0.0.6" "$(cabinet_read_rejected_version "$STATE_DIR")"

INSTALL_STATUS_TO_RETURN=0
run_install v0.0.7 --from-dir "${WORK}/assets-0.0.7"
check "a successful install exits 0" "0" "$I_RC"
check "a successful install clears the rejected marker" "" "$(cabinet_read_rejected_version "$STATE_DIR")"

cabinet_record_rejected_version "0.0.6" "$STATE_DIR"
INSTALL_STATUS_TO_RETURN=0
run_install v0.0.6 --from-dir "${WORK}/assets-0.0.6"
check "a manual install of the rejected version proceeds" "v0.0.6" "$(cat "$INSTALL_CORE_CALLS")"
check "a successful manual install clears the marker" "" "$(cabinet_read_rejected_version "$STATE_DIR")"

: > "$INSTALL_CORE_ARTIFACTS"
run_install v0.0.6 --from-dir "${WORK}/assets-0.0.6"
check "a manual install verifies and unpacks a private copy, not the caller's files" "no" \
  "$(contains "$(cat "$INSTALL_CORE_ARTIFACTS")" "${WORK}/assets-0.0.6")"
check "the private copy is under the download directory" "yes" \
  "$(contains "$(cat "$INSTALL_CORE_ARTIFACTS")" "${DOWNLOAD_DIR}/")"
check "a finished install leaves nothing in the download directory" "0" \
  "$(find "$DOWNLOAD_DIR" -mindepth 1 | wc -l | tr -d ' ')"

download_release_asset() {
  cp "${WORK}/assets-0.0.6/$3" "$4"
}
run_install v0.0.6
check "a downloaded install reaches the install core" "v0.0.6" "$(cat "$INSTALL_CORE_CALLS")"
check "a finished downloaded install leaves nothing in the download directory" "0" \
  "$(find "$DOWNLOAD_DIR" -mindepth 1 | wc -l | tr -d ' ')"

mkdir -p "${DOWNLOAD_DIR}/0.0.1"
printf 'left behind by an earlier run\n' > "${DOWNLOAD_DIR}/0.0.1/cabinet-0.0.1.zip"
run_install v0.0.6 --from-dir "${WORK}/assets-0.0.6"
check "an install clears downloads left behind by earlier runs" "0" \
  "$(find "$DOWNLOAD_DIR" -mindepth 1 | wc -l | tr -d ' ')"

run_install v0.0.5 --from-dir "${WORK}/assets-0.0.6"
check "installing the active version is refused" "1" "$I_RC"
check "a refused downgrade never reaches the install core" "0" "$(count_lines "$INSTALL_CORE_CALLS")"

printf 'tampered\n' >> "${WORK}/assets-0.0.7/cabinet-0.0.7.zip"
run_install v0.0.7 --from-dir "${WORK}/assets-0.0.7"
check "an artifact that does not match its checksum is refused" "1" "$I_RC"
check "a checksum mismatch never reaches the install core" "0" "$(count_lines "$INSTALL_CORE_CALLS")"

REFUSED_ROOT="${WORK}/refused-root"
mkdir -p "${REFUSED_ROOT}/etc/cabinet" "${REFUSED_ROOT}/run/cabinet-deploy"
printf 'CABINET_GITHUB_REPO=example-owner/example-repo\n' > "${REFUSED_ROOT}/etc/cabinet/deploy.conf"
chmod 600 "${REFUSED_ROOT}/etc/cabinet/deploy.conf"
REFUSED_EXIT=0
CABINET_DEPLOY_ROOT="$REFUSED_ROOT" "${REPO_ROOT}/deploy/bin/cabinet-deploy" install v0.0.7 \
  --from-dir "${WORK}/assets-0.0.7" >/dev/null 2>&1 || REFUSED_EXIT=$?
check "the installer refuses an artifact that does not match its checksum" "1" "$REFUSED_EXIT"
check "a refused install leaves nothing in the download directory" "0" \
  "$(find "${REFUSED_ROOT}/var/lib/cabinet-deploy/downloads" -mindepth 1 | wc -l | tr -d ' ')"

make_assets 0.0.9 "${WORK}/assets-0.0.9"
printf 'unrelated file\n' > "${WORK}/assets-0.0.9/other.txt"
(cd "${WORK}/assets-0.0.9" && sha256sum other.txt > cabinet-0.0.9.zip.sha256)
printf 'tampered\n' >> "${WORK}/assets-0.0.9/cabinet-0.0.9.zip"
run_install v0.0.9 --from-dir "${WORK}/assets-0.0.9"
check "a checksum file that lists another file cannot vouch for the artifact" "1" "$I_RC"
check "that checksum file never reaches the install core" "0" "$(count_lines "$INSTALL_CORE_CALLS")"

CHECKSUM_ARTIFACT="${WORK}/assets-0.0.6/cabinet-0.0.6.zip"
CHECKSUM_FILE="${CHECKSUM_ARTIFACT}.sha256"
check "a checksum file for the artifact itself is accepted" "0" "$(cabinet_verify_checksum "$CHECKSUM_ARTIFACT" "$CHECKSUM_FILE" 2>/dev/null; echo $?)"
ARTIFACT_HASH="$(sha256sum "$CHECKSUM_ARTIFACT" | awk '{print $1}')"
printf '%s  /etc/hostname\n' "$ARTIFACT_HASH" > "${WORK}/path.sha256"
check "a checksum file naming another path is refused" "1" "$(cabinet_verify_checksum "$CHECKSUM_ARTIFACT" "${WORK}/path.sha256" 2>/dev/null; echo $?)"
printf '%s  cabinet-0.0.6.zip\n%s  other.txt\n' "$ARTIFACT_HASH" "$ARTIFACT_HASH" > "${WORK}/two-lines.sha256"
check "a checksum file with more than one entry is refused" "1" "$(cabinet_verify_checksum "$CHECKSUM_ARTIFACT" "${WORK}/two-lines.sha256" 2>/dev/null; echo $?)"
printf 'not-a-hash  cabinet-0.0.6.zip\n' > "${WORK}/malformed.sha256"
check "a checksum file without a valid hash is refused" "1" "$(cabinet_verify_checksum "$CHECKSUM_ARTIFACT" "${WORK}/malformed.sha256" 2>/dev/null; echo $?)"
check "a missing checksum file is refused" "1" "$(cabinet_verify_checksum "$CHECKSUM_ARTIFACT" "${WORK}/absent.sha256" 2>/dev/null; echo $?)"

make_assets 0.0.8 "${WORK}/assets-0.0.8"
# shellcheck disable=SC2329
cabinet_verify_attestation() {
  return 1
}
run_install v0.0.8 --from-dir "${WORK}/assets-0.0.8"
check "an artifact whose attestation fails is refused" "1" "$I_RC"
check "a failed attestation never reaches the install core" "0" "$(count_lines "$INSTALL_CORE_CALLS")"
check "a failed attestation does not write a rejected marker" "" "$(cabinet_read_rejected_version "$STATE_DIR")"

cabinet_verify_attestation() {
  printf '%s' "0123456789abcdef0123456789abcdef01234567"
}
# shellcheck disable=SC2329
cabinet_commit_on_branch() {
  return 1
}
run_install v0.0.8 --from-dir "${WORK}/assets-0.0.8"
check "an attested commit that is not on main is refused" "1" "$I_RC"
check "a commit outside main never reaches the install core" "0" "$(count_lines "$INSTALL_CORE_CALLS")"
cabinet_commit_on_branch() {
  return 0
}

run_install not-a-version
check "a tag that is not strict semver is refused" "1" "$I_RC"

# --- Manual rollback ---------------------------------------------------------------

cabinet_wait_for_health() {
  return 0
}
rm -rf "${CABINET_DEPLOY_ROOT}/opt/cabinet" "${STATE_DIR}"
mkdir -p "${RELEASES_DIR}/0.0.5" "${RELEASES_DIR}/0.0.6" "$STATE_DIR"
ln -s "${RELEASES_DIR}/0.0.6" "$CURRENT_LINK"
printf '0.0.5\n' > "${STATE_DIR}/previous"
ROLLBACK_RC=0
(cmd_rollback) >/dev/null 2>&1 || ROLLBACK_RC=$?
check "rollback without a version returns to the previous release" "0.0.5" \
  "$(basename "$(readlink -f "$CURRENT_LINK")")"
check "manual rollback succeeds" "0" "$ROLLBACK_RC"
check "manual rollback marks the release it left as skipped by polls" "0.0.6" "$(cabinet_read_rejected_version "$STATE_DIR")"

ROLLBACK_RC=0
(cmd_rollback 0.0.5) >/dev/null 2>&1 || ROLLBACK_RC=$?
check "rolling back to the active release is refused" "1" "$ROLLBACK_RC"
ROLLBACK_RC=0
(cmd_rollback not-a-version) >/dev/null 2>&1 || ROLLBACK_RC=$?
check "rollback of a malformed version is refused" "1" "$ROLLBACK_RC"

# --- Manual rollback after an automatic rollback ------------------------------------

rm -rf "${CABINET_DEPLOY_ROOT}/opt/cabinet" "${STATE_DIR}"
mkdir -p "${RELEASES_DIR}/0.0.5" "${RELEASES_DIR}/0.0.6" "$STATE_DIR"
ln -s "${RELEASES_DIR}/0.0.5" "$CURRENT_LINK"
cabinet_activate_release 0.0.6 "$RELEASES_DIR" "$CURRENT_LINK" "$STATE_DIR"
cabinet_rollback_release 0.0.5 "$RELEASES_DIR" "$CURRENT_LINK" "$STATE_DIR" "http://127.0.0.1:0" 5 2>/dev/null
cabinet_record_rejected_version 0.0.6 "$STATE_DIR"
: > "$RESTART_LOG"
ROLLBACK_RC=0
(cmd_rollback) >/dev/null 2>&1 || ROLLBACK_RC=$?
check "a manual rollback after an automatic rollback is refused" "1" "$ROLLBACK_RC"
check "that manual rollback leaves the good release active" "0.0.5" "$(basename "$(readlink -f "$CURRENT_LINK")")"
check "that manual rollback never restarts the application" "0" "$(count_lines "$RESTART_LOG")"

printf '0.0.6\n' > "${STATE_DIR}/previous"
ROLLBACK_RC=0
ROLLBACK_LOG="$( (cmd_rollback) 2>&1 )" || ROLLBACK_RC=$?
check "a recorded previous release that failed its health check is not reactivated" "1" "$ROLLBACK_RC"
check "the refusal tells the operator to name a version" "yes" "$(contains "$ROLLBACK_LOG" "name the version to roll back to")"
check "the refused release stays inactive" "0.0.5" "$(basename "$(readlink -f "$CURRENT_LINK")")"

# --- A manual rollback never lowers the rejected version -------------------------------

rm -rf "${CABINET_DEPLOY_ROOT}/opt/cabinet" "${STATE_DIR}"
mkdir -p "${RELEASES_DIR}/0.0.4" "${RELEASES_DIR}/0.0.6" "$STATE_DIR"
ln -s "${RELEASES_DIR}/0.0.6" "$CURRENT_LINK"
cabinet_record_rejected_version 0.0.8 "$STATE_DIR"
(cmd_rollback 0.0.4) >/dev/null 2>&1
check "a manual rollback keeps the higher rejected version" "0.0.8" "$(cabinet_read_rejected_version "$STATE_DIR")"

INSTALL_STATUS_TO_RETURN=0
make_assets 0.0.7 "${WORK}/assets-0.0.7-fresh"
run_install v0.0.7 --from-dir "${WORK}/assets-0.0.7-fresh"
check "a successful install below the rejected version succeeds" "0" "$I_RC"
check "a successful install below the rejected version keeps the marker" "0.0.8" "$(cabinet_read_rejected_version "$STATE_DIR")"

# --- Single run lock ---------------------------------------------------------------

LOCK_ROOT="${WORK}/lock-root"
mkdir -p "${LOCK_ROOT}/etc/cabinet" "${LOCK_ROOT}/run/cabinet-deploy" "${LOCK_ROOT}/opt/cabinet"
printf 'CABINET_GITHUB_REPO=example-owner/example-repo\n' > "${LOCK_ROOT}/etc/cabinet/deploy.conf"
chmod 600 "${LOCK_ROOT}/etc/cabinet/deploy.conf"
exec 8>"${LOCK_ROOT}/run/cabinet-deploy/deploy.lock"
flock -n 8
LOCK_EXIT=0
LOCK_LOG="$(CABINET_DEPLOY_ROOT="$LOCK_ROOT" "${REPO_ROOT}/deploy/bin/cabinet-deploy" install v9.9.9 2>&1)" || LOCK_EXIT=$?
exec 8>&-
check "a second run while the lock is held exits non-zero" "1" "$LOCK_EXIT"
check "a second run says another invocation is running" "yes" "$(contains "$LOCK_LOG" "already running")"
check "a second run changes nothing" "0" "$(find "${LOCK_ROOT}/opt/cabinet" -mindepth 1 | wc -l | tr -d ' ')"

# --- Host guard --------------------------------------------------------------------

check "no check in this file reached the real service manager or polkit" "" "$(host_guard_calls)"

if [ "$FAILURES" -ne 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES" >&2
  exit 1
fi

printf 'All checks passed\n'

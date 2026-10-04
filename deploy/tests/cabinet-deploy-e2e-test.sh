#!/usr/bin/env bash
# End-to-end proof of the installer against real packaged releases. Needs the
# .NET SDK and runs only with CABINET_E2E=1. Only the outside world is
# replaced: attestation verification, the commit-on-main check and the
# service manager restart. Everything else (checksum, unpacking, the symlink
# swap, the health check against a really running application) is the real
# installer code.
set -euo pipefail

if [ "${CABINET_E2E:-0}" != "1" ]; then
  echo "skipping cabinet-deploy end-to-end test (set CABINET_E2E=1 to run)"
  exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"

TMP="$(mktemp -d)"
export CABINET_DEPLOY_ROOT="${TMP}/root"
mkdir -p "$CABINET_DEPLOY_ROOT"
ASSETS="${TMP}/assets"
mkdir -p "$ASSETS"
APP_PID_FILE="${TMP}/app.pid"
RESTART_COUNT_FILE="${TMP}/restart-count"
printf '0' > "$RESTART_COUNT_FILE"

# shellcheck source=deploy/tests/lib/host-guard.sh
source "${SCRIPT_DIR}/lib/host-guard.sh"
host_guard_install "$TMP"

COMMIT="0123456789abcdef0123456789abcdef01234567"
FAILURES=0

stop_app() {
  local pid=""
  [ -f "$APP_PID_FILE" ] && pid="$(cat "$APP_PID_FILE")"
  [ -n "$pid" ] || return 0
  if kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    local waited=0
    while kill -0 "$pid" 2>/dev/null && [ "$waited" -lt 50 ]; do
      sleep 0.2
      waited=$((waited + 1))
    done
    kill -9 "$pid" 2>/dev/null || true
  fi
  rm -f "$APP_PID_FILE"
}

cleanup() {
  stop_app
  rm -rf "$TMP"
}
trap cleanup EXIT

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

free_port() {
  python3 -c 'import socket; s = socket.socket(); s.bind(("127.0.0.1", 0)); print(s.getsockname()[1]); s.close()'
}

WEB_PORT="$(free_port)"
OPS_PORT="$(free_port)"
while [ "$OPS_PORT" = "$WEB_PORT" ]; do OPS_PORT="$(free_port)"; done
MOVED_OPS_PORT="$(free_port)"
while [ "$MOVED_OPS_PORT" = "$WEB_PORT" ] || [ "$MOVED_OPS_PORT" = "$OPS_PORT" ]; do MOVED_OPS_PORT="$(free_port)"; done

# Packs a staged release tree into ASSETS/cabinet-VERSION.zip with its checksum
# and a placeholder bundle.
pack_release() {
  local stage="$1" version="$2"
  local zip="${ASSETS}/cabinet-${version}.zip"
  rm -f "$zip"
  (cd "$stage" && find . -type f | sed 's|^\./||' | sort | zip -X -q "$zip" -@)
  (cd "$ASSETS" && sha256sum "cabinet-${version}.zip" > "cabinet-${version}.zip.sha256")
  printf 'placeholder bundle\n' > "${zip}.sigstore.json"
}

# Points the listeners of a staged release at the given loopback ports.
set_listeners() {
  local stage="$1" web_port="$2" ops_port="$3"
  local settings="${stage}/app/appsettings.json"
  local rewritten
  rewritten="$(jq \
    --arg web "http://127.0.0.1:${web_port}" \
    --arg ops "http://127.0.0.1:${ops_port}" \
    '.Kestrel.Endpoints.Web.Url = $web | .Kestrel.Endpoints.Ops.Url = $ops' "$settings")"
  printf '%s\n' "$rewritten" > "$settings"
}

# Builds a real release with the packaging script and publishes it as test
# assets, with the listeners moved to free loopback ports.
build_release() {
  local version="$1"
  local out="${TMP}/build-${version}"
  bash "${REPO_ROOT}/build/package-release.sh" --version "$version" --commit "$COMMIT" --output "$out" \
    >"${TMP}/package-${version}.log" 2>&1 \
    || { cat "${TMP}/package-${version}.log" >&2; echo "FAIL: packaging ${version}" >&2; exit 1; }
  local stage="${TMP}/stage-${version}"
  mkdir -p "$stage"
  unzip -q "${out}/cabinet-${version}.zip" -d "$stage"
  set_listeners "$stage" "$WEB_PORT" "$OPS_PORT"
  pack_release "$stage" "$version"
}

# shellcheck source=deploy/bin/cabinet-deploy
source "${REPO_ROOT}/deploy/bin/cabinet-deploy"

cabinet_verify_attestation() {
  printf '%s' "$COMMIT"
}

cabinet_commit_on_branch() {
  return 0
}

cabinet_restart_app() {
  stop_app
  printf '%s' "$(( $(cat "$RESTART_COUNT_FILE") + 1 ))" > "$RESTART_COUNT_FILE"
  (
    cd "${CABINET_DEPLOY_ROOT}/opt/cabinet/current/app"
    ASPNETCORE_ENVIRONMENT=Production \
      exec dotnet "${CABINET_DEPLOY_ROOT}/opt/cabinet/current/app/Cabinet.Service.dll"
  ) >>"${TMP}/app.log" 2>&1 &
  printf '%s' "$!" > "$APP_PID_FILE"
}

restart_count() {
  cat "$RESTART_COUNT_FILE"
}

ops_health() {
  curl --silent --max-time 3 "http://127.0.0.1:${OPS_PORT}/health" 2>/dev/null || true
}

mkdir -p "${CABINET_DEPLOY_ROOT}/etc/cabinet"
CONF="${CABINET_DEPLOY_ROOT}/etc/cabinet/deploy.conf"
cat > "$CONF" <<EOF_CONF
CABINET_GITHUB_REPO=example-owner/example-repo
CABINET_OPS_URL=http://127.0.0.1:${OPS_PORT}
CABINET_HEALTH_TIMEOUT_SECONDS=8
EOF_CONF
chmod 600 "$CONF"
export CABINET_HEALTH_INTERVAL_SECONDS=1
load_configuration

build_release 0.0.1

INSTALL_EXIT=0
(cmd_install v0.0.1 --from-dir "$ASSETS") >"${TMP}/install-0.0.1.log" 2>&1 || INSTALL_EXIT=$?
cat "${TMP}/install-0.0.1.log"

check "install of 0.0.1 exits 0" "0" "$INSTALL_EXIT"
check "current resolves to releases/0.0.1" "${CABINET_DEPLOY_ROOT}/opt/cabinet/releases/0.0.1" \
  "$(readlink -f "${CABINET_DEPLOY_ROOT}/opt/cabinet/current")"
check "health reports Healthy" "Healthy" "$(ops_health | jq -r '.status // empty')"
check "health reports version 0.0.1" "0.0.1" "$(ops_health | jq -r '.version // empty')"
check "no rejected marker after a successful install" "absent" \
  "$([ -e "${STATE_DIR}/rejected" ] && echo present || echo absent)"
check "the application was restarted exactly once" "1" "$(restart_count)"
check "no service manager or polkit call was recorded" "" "$(host_guard_calls)"

if [ "$FAILURES" -ne 0 ]; then
  printf '%d check(s) failed\n' "$FAILURES" >&2
  exit 1
fi

echo "All checks passed"

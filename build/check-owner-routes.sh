#!/usr/bin/env bash
###
### Proves both reverse-proxy routers from the owner's own network: the owner
### host serves the owner pages, and the public host hides them. Run it from
### a machine on the home network or VPN, after switching the owner tools on.
###
### Both hostnames come from the environment and are never written down in
### the repository or printed:
###
###   CABINET_OWNER_HOST   the hostname of the owner router
###   CABINET_PUBLIC_HOST  the hostname of the public router
###
### Prints one PASS or FAIL line per check, with status codes only, and exits
### 1 when any check failed, or 2 when a hostname is missing.
###
set -euo pipefail

readonly PREVIEW_PATH="/owner/preview/00000000000000000000000000000000"
readonly PLATE_TEXT="Owner tools"

FAILED=0
WORK_DIR=""

usage() {
  echo "Usage: CABINET_OWNER_HOST=<owner hostname> CABINET_PUBLIC_HOST=<public hostname> check-owner-routes.sh" >&2
}

pass() { echo "PASS - $*"; }

fail() {
  echo "FAIL - $*"
  FAILED=1
}

###
### Fetches https://HOST PATH over TLS and leaves the status code in STATUS
### (000 when nothing answered) and the response body in BODY_FILE.
###
fetch() {
  local host="$1" path="$2"
  STATUS="$(curl -s --max-time 15 -o "$BODY_FILE" -w '%{http_code}' "https://${host}${path}" 2>/dev/null || true)"
  STATUS="${STATUS:-000}"
}

### Passes when the host answers the path with the wanted status.
expect_status() {
  local label="$1" host="$2" path="$3" wanted="$4"
  fetch "$host" "$path"
  if [[ "$STATUS" == "$wanted" ]]; then
    pass "${label} answers ${wanted}"
  else
    fail "${label} answers ${wanted} (status ${STATUS})"
  fi
}

main() {
  local owner_host="${CABINET_OWNER_HOST:-}" public_host="${CABINET_PUBLIC_HOST:-}"
  if [[ -z "$owner_host" || -z "$public_host" ]]; then
    usage
    return 2
  fi

  WORK_DIR="$(mktemp -d)"
  trap 'rm -rf "$WORK_DIR"' EXIT
  BODY_FILE="${WORK_DIR}/body"

  fetch "$owner_host" "/"
  if [[ "$STATUS" != "200" ]]; then
    fail "the owner home page answers 200 (status ${STATUS})"
  elif ! grep -qF -- "$PLATE_TEXT" "$BODY_FILE"; then
    fail "the owner home page shows the '${PLATE_TEXT}' plate (status ${STATUS}, plate missing)"
  else
    pass "the owner home page answers 200 and shows the '${PLATE_TEXT}' plate"
  fi

  expect_status "the owner pictures page" "$owner_host" "/owner/pictures" 200
  expect_status "the owner settings page" "$owner_host" "/owner/settings" 200
  expect_status "the public pictures page" "$public_host" "/owner/pictures" 404
  expect_status "the public preview page" "$public_host" "$PREVIEW_PATH" 404

  fetch "$public_host" "/"
  if [[ "$STATUS" != "200" ]]; then
    fail "the public home page answers 200 (status ${STATUS})"
  elif grep -qF -- "/owner" "$BODY_FILE"; then
    fail "the public home page carries no /owner reference (status ${STATUS}, reference found)"
  else
    pass "the public home page answers 200 and carries no /owner reference"
  fi

  [[ "$FAILED" -eq 0 ]]
}

main "$@"

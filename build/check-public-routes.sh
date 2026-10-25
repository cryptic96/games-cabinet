#!/usr/bin/env bash
###
### Proves the deterministic promises of the public route, the way a visitor
### reaches it:
###
###   - the home page answers 200 over HTTPS and plain HTTP redirects to HTTPS
###   - the home page and a static file each carry the full security header
###     set exactly once, and no Server header
###   - the health page and an owner page answer 404 on the public hostname,
###     and the app, ops and owner ports answer nothing on it
###   - the home page asks search engines not to index it while robots.txt
###     lets them read that request, carries the linked BoardGameGeek credit
###     and holds no owner reference
###   - the owner hostname answers 403 from outside the home network and 200
###     from inside it
###
### Run it from the home network (--from-lan) or from outside it
### (--from-outside); the owner hostname's expected answer differs, so the
### vantage point must be stated.
###
### With --burst it also presses "sync now" a few times through one
### connection and passes only when the app's own per-client limit refuses at
### least one press with a 429, a Retry-After header and a body without a
### status object. The first accepted press starts one real sync and opens the
### shared cooldown window, so run the burst only where that disturbs no
### measurement, and once the proxy's per-address limit is sized from a
### measured first visit: a proxy still on unsized numbers may refuse first,
### and that is reported as such.
###
### Both hostnames come from the environment and are never written down in
### the repository or printed:
###
###   CABINET_PUBLIC_HOST  the hostname of the public router
###   CABINET_OWNER_HOST   the hostname of the owner router
###   CABINET_BURST_COUNT  presses sent by --burst (a whole number from 1 to 20)
###
### Prints one PASS or FAIL line per check, with labels and status codes only,
### and exits 0 when every check passed, 1 when any failed, or 2 when a
### hostname or the vantage point is missing or an option is invalid.
###
set -euo pipefail

readonly STATIC_PATH="/img/powered-by-bgg.svg"
readonly OWNER_PATH="/owner/pictures"
readonly PRESS_PATH="/cabinet/sync"
readonly CLOSED_PORTS=(5080 5081 5082)
readonly REDIRECT_STATUSES=" 301 302 307 308 "

###
### The burst sends 8 presses by default: the smallest count that safely
### exceeds the app's per-client limit of 5 presses per window. The cap keeps
### the burst far below anything the home gateway's intrusion prevention
### reacts to. The live negotiate window is not the target because it is
### sized to admit every phone of a game night.
###
readonly DEFAULT_BURST_COUNT=8
readonly MAX_BURST_COUNT=20

FAILED=0
WORK_DIR=""
BODY_FILE=""
HEADERS_FILE=""
STATUS=""
VANTAGE=""
BURST=0
BURST_COUNT=""

usage_text() {
  echo "Usage: CABINET_PUBLIC_HOST=<public hostname> CABINET_OWNER_HOST=<owner hostname> check-public-routes.sh (--from-lan | --from-outside) [--burst]"
  echo "  --from-lan      run from the home network: the owner hostname must answer 200"
  echo "  --from-outside  run from outside the home network: the owner hostname must answer 403"
  echo "  --burst         also press \"sync now\" CABINET_BURST_COUNT times (default ${DEFAULT_BURST_COUNT}, at most ${MAX_BURST_COUNT})"
}

usage() { usage_text >&2; }

pass() { echo "PASS - $*"; }

fail() {
  echo "FAIL - $*"
  FAILED=1
}

###
### Fetches https://HOST PATH over TLS and leaves the status code in STATUS
### (000 when nothing answered), the response body in BODY_FILE and the
### response headers in HEADERS_FILE.
###
fetch_with_headers() {
  local host="$1" path="$2"
  : > "$BODY_FILE"
  : > "$HEADERS_FILE"
  STATUS="$(curl -s --max-time 15 -o "$BODY_FILE" -D "$HEADERS_FILE" -w '%{http_code}' "https://${host}${path}" 2>/dev/null || true)"
  STATUS="${STATUS:-000}"
}

### Fetches https://HOST PATH like a visitor; only STATUS and BODY_FILE are of interest.
fetch() {
  local host="$1" path="$2"
  : > "$BODY_FILE"
  STATUS="$(curl -s --max-time 15 -o "$BODY_FILE" -w '%{http_code}' "https://${host}${path}" 2>/dev/null || true)"
  STATUS="${STATUS:-000}"
}

### Prints how many lines of the last response headers are named NAME (case-insensitive).
header_count() {
  grep -ic -- "^${1}:" "$HEADERS_FILE" || true
}

### Prints the trimmed value of the first header named NAME in the last response headers.
header_value() {
  grep -i -m 1 -- "^${1}:" "$HEADERS_FILE" | sed -e 's/^[^:]*:[[:space:]]*//' -e 's/[[:space:]]*$//' || true
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

###
### Passes when the last response sent the header NAME exactly once, with a
### value that holds every wanted fragment and, when given, not the forbidden
### one. Arguments: PAGE_LABEL NAME FORBIDDEN WANTED... (FORBIDDEN may be empty).
###
check_header() {
  local page="$1" name="$2" forbidden="$3"
  shift 3
  local count value lowered wanted
  count="$(header_count "$name")"
  if [[ "$count" -eq 0 ]]; then
    fail "${page} sends ${name} exactly once (header missing)"
    return 0
  fi
  if [[ "$count" -gt 1 ]]; then
    fail "${page} sends ${name} exactly once (sent ${count} times)"
    return 0
  fi
  value="$(header_value "$name")"
  lowered="${value,,}"
  for wanted in "$@"; do
    if [[ "$lowered" != *"${wanted,,}"* ]]; then
      fail "${page} sends ${name} with the expected value (${wanted} missing)"
      return 0
    fi
  done
  if [[ -n "$forbidden" && "$lowered" == *"${forbidden,,}"* ]]; then
    fail "${page} sends ${name} with the expected value (${forbidden} must not be set)"
    return 0
  fi
  pass "${page} sends ${name} exactly once with the expected value"
}

### Passes when the last response sent no Server header.
check_no_server_header() {
  local page="$1" count
  count="$(header_count "Server")"
  if [[ "$count" -eq 0 ]]; then
    pass "${page} sends no Server header"
  else
    fail "${page} sends no Server header (found ${count})"
  fi
}

### Runs the whole header set against the last response.
check_header_set() {
  local page="$1"
  check_header "$page" "Strict-Transport-Security" "preload" "max-age="
  check_header "$page" "Content-Security-Policy" "" "default-src 'self'" "frame-ancestors 'none'"
  check_header "$page" "X-Content-Type-Options" "" "nosniff"
  check_header "$page" "Referrer-Policy" ""
  check_header "$page" "X-Frame-Options" "" "DENY"
  check_header "$page" "Permissions-Policy" "" "()"
  check_header "$page" "Cross-Origin-Opener-Policy" "" "same-origin"
  check_header "$page" "Cross-Origin-Resource-Policy" "" "same-origin"
  check_no_server_header "$page"
}

### Fetches a page, checks it answers 200, and runs the header set on it.
check_page_headers() {
  local label="$1" host="$2" path="$3"
  fetch_with_headers "$host" "$path"
  if [[ "$STATUS" != "200" ]]; then
    fail "${label} answers 200 (status ${STATUS})"
    [[ "$STATUS" != "000" ]] || return 0
  else
    pass "${label} answers 200"
  fi
  check_header_set "$label"
}

### Passes when plain HTTP answers with a redirect whose target is HTTPS.
check_https_redirect() {
  local host="$1" status location
  : > "$HEADERS_FILE"
  status="$(curl -s --max-time 15 -o /dev/null -D "$HEADERS_FILE" -w '%{http_code}' "http://${host}/" 2>/dev/null || true)"
  status="${status:-000}"
  location="$(header_value "Location")"
  if [[ "$REDIRECT_STATUSES" != *" ${status} "* ]]; then
    fail "plain HTTP redirects to HTTPS (status ${status})"
  elif [[ "$location" != https://* ]]; then
    fail "plain HTTP redirects to HTTPS (status ${status}, the target is not HTTPS)"
  else
    pass "plain HTTP redirects to HTTPS (status ${status})"
  fi
}

### Passes when nothing answers on the port of the public hostname.
check_port_closed() {
  local host="$1" port="$2" status
  status="$(curl -s --connect-timeout 5 --max-time 8 -o /dev/null -w '%{http_code}' "http://${host}:${port}/" 2>/dev/null || true)"
  status="${status:-000}"
  if [[ "$status" == "000" ]]; then
    pass "port ${port} on the public hostname answers nothing"
  else
    fail "port ${port} on the public hostname answers nothing (status ${status})"
  fi
}

### Checks the home page body for the noindex request, the credit and the absence of owner references.
check_home_body() {
  local body_file="$1"
  if grep -qF -- 'content="noindex"' "$body_file"; then
    pass "the public home page carries the noindex robots meta tag"
  else
    fail "the public home page carries the noindex robots meta tag (tag missing)"
  fi
  if grep -qF -- 'href="https://boardgamegeek.com"' "$body_file"; then
    pass "the public home page carries the linked BoardGameGeek credit"
  else
    fail "the public home page carries the linked BoardGameGeek credit (link missing)"
  fi
  if grep -qF -- "/owner" "$body_file"; then
    fail "the public home page carries no /owner reference (reference found)"
  else
    pass "the public home page carries no /owner reference"
  fi
}

### Passes when robots.txt is absent or does not disallow the whole site.
check_robots() {
  local host="$1"
  fetch "$host" "/robots.txt"
  if [[ "$STATUS" == "404" ]]; then
    pass "robots.txt is absent, so crawlers may read the noindex tag"
  elif [[ "$STATUS" != "200" ]]; then
    fail "robots.txt is absent or open to crawlers (status ${STATUS})"
  elif grep -qiE '^[[:space:]]*Disallow:[[:space:]]*/[[:space:]]*$' "$BODY_FILE"; then
    fail "robots.txt does not disallow the whole site (status ${STATUS}, a Disallow: / line found)"
  else
    pass "robots.txt does not disallow the whole site"
  fi
}

### Passes when the owner hostname answers as the vantage point demands.
check_owner_host() {
  local host="$1"
  if [[ "$VANTAGE" == "lan" ]]; then
    expect_status "the owner hostname seen from the home network" "$host" "/" 200
  else
    expect_status "the owner hostname seen from outside" "$host" "/" 403
  fi
}

### Whole number from 1 to MAX_BURST_COUNT in BURST_COUNT, or a message and return 1.
resolve_burst_count() {
  local raw="${CABINET_BURST_COUNT:-$DEFAULT_BURST_COUNT}"
  if [[ ! "$raw" =~ ^[0-9]+$ ]] || ((10#$raw < 1)); then
    echo "CABINET_BURST_COUNT must be a whole number from 1 to ${MAX_BURST_COUNT}" >&2
    return 1
  fi
  if ((10#$raw > MAX_BURST_COUNT)); then
    echo "CABINET_BURST_COUNT is refused: the burst is capped at ${MAX_BURST_COUNT} presses" >&2
    return 1
  fi
  BURST_COUNT=$((10#$raw))
}

###
### Sends BURST_COUNT presses through one curl call (one connection) and
### judges the answers: the app's own per-client refusal is a 429 with a
### Retry-After header and a JSON body without a status object, the shared
### window's refusal carries a status object, and a proxy refusal has no JSON
### body at all.
###
run_burst() {
  local host="$1" url index status body retry_after
  local cooldown=0 shared=0 proxy=0 refused=0
  local -a args statuses=()
  url="https://${host}${PRESS_PATH}"
  args=(-s -X POST --max-time 60 -D "$HEADERS_FILE" -w '%{http_code}\n')
  for ((index = 1; index <= BURST_COUNT; index++)); do
    : > "${WORK_DIR}/burst-${index}"
    args+=(-o "${WORK_DIR}/burst-${index}" "$url")
  done
  : > "$HEADERS_FILE"
  mapfile -t statuses < <(curl "${args[@]}" 2>/dev/null || true)

  for ((index = 1; index <= ${#statuses[@]}; index++)); do
    status="${statuses[index - 1]}"
    [[ "$status" == "429" ]] || continue
    refused=$((refused + 1))
    body="$(cat "${WORK_DIR}/burst-${index}")"
    if [[ "$body" != "{"* ]]; then
      proxy=$((proxy + 1))
    elif [[ "$body" == *'"status"'* ]]; then
      shared=$((shared + 1))
    elif [[ "$body" == *'"outcome":"cooldown"'* ]]; then
      cooldown=$((cooldown + 1))
    fi
  done

  retry_after="$(header_count "Retry-After")"
  local counts
  counts="$(printf '%s\n' "${statuses[@]}" | grep -v '^$' | sort | uniq -c | awk '{printf "%s%sx%s", sep, $2, $1; sep=" "}' || true)"
  counts="${counts:-none answered}"

  local base="the burst of ${BURST_COUNT} presses is refused by the app's own limit"
  if ((cooldown > 0 && retry_after >= cooldown)); then
    pass "${base}, with a Retry-After header (answers ${counts})"
  elif ((cooldown > 0)); then
    fail "${base} (a 429 came without Retry-After; answers ${counts})"
  elif ((proxy > 0)); then
    fail "${base} (429 from the proxy, no JSON body: the proxy's per-address limit refused the burst before the app; answers ${counts})"
  elif ((shared > 0)); then
    fail "${base} (429 from the shared window only, every refusal carries a status object; answers ${counts})"
  elif ((refused > 0)); then
    fail "${base} (429 with an unrecognised body; answers ${counts})"
  else
    fail "${base} (no 429; answers ${counts})"
  fi
}

parse_arguments() {
  local argument
  for argument in "$@"; do
    case "$argument" in
      --from-lan | --from-outside)
        if [[ -n "$VANTAGE" ]]; then
          usage
          return 2
        fi
        VANTAGE="${argument#--from-}"
        ;;
      --burst) BURST=1 ;;
      -h | --help)
        usage_text
        return 3
        ;;
      *)
        usage
        return 2
        ;;
    esac
  done
  if [[ -z "$VANTAGE" ]]; then
    usage
    return 2
  fi
}

main() {
  local parsed=0
  parse_arguments "$@" || parsed=$?
  if ((parsed == 3)); then
    return 0
  elif ((parsed != 0)); then
    return "$parsed"
  fi

  local public_host="${CABINET_PUBLIC_HOST:-}" owner_host="${CABINET_OWNER_HOST:-}"
  if [[ -z "$public_host" || -z "$owner_host" ]]; then
    usage
    return 2
  fi
  if ((BURST)); then
    resolve_burst_count || return 2
  fi

  WORK_DIR="$(mktemp -d)"
  trap 'rm -rf "$WORK_DIR"' EXIT
  BODY_FILE="${WORK_DIR}/body"
  HEADERS_FILE="${WORK_DIR}/headers"

  check_page_headers "the public home page" "$public_host" "/"
  cp "$BODY_FILE" "${WORK_DIR}/home-body"
  check_page_headers "the static file" "$public_host" "$STATIC_PATH"

  check_https_redirect "$public_host"
  expect_status "the public health page" "$public_host" "/health" 404
  expect_status "the public owner page" "$public_host" "$OWNER_PATH" 404
  local port
  for port in "${CLOSED_PORTS[@]}"; do
    check_port_closed "$public_host" "$port"
  done

  check_home_body "${WORK_DIR}/home-body"
  check_robots "$public_host"
  check_owner_host "$owner_host"

  if ((BURST)); then
    run_burst "$public_host"
  fi

  [[ "$FAILED" -eq 0 ]]
}

main "$@"

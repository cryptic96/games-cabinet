#!/usr/bin/env bash
###
### Proves the public route check against a stubbed curl: it passes only
### when the public route carries every promised header, redirect, closed
### route, noindex tag and credit and the owner hostname answers as its
### vantage point demands; fails with exactly one named check for every
### single deviation; refuses to run without a vantage point or a hostname;
### pins the default burst at 8 presses and refuses more than 20; and never
### prints a hostname. Offline: nothing is fetched.
###
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="${REPO_ROOT}/build/check-public-routes.sh"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

OWNER_HOST="owner.example.com"
PUBLIC_HOST="cabinet.example.org"
PRESS_URL="https://${PUBLIC_HOST}/cabinet/sync"
STATIC_URL="https://${PUBLIC_HOST}/img/powered-by-bgg.svg"

HEADER_NAMES=(
  "Strict-Transport-Security"
  "Content-Security-Policy"
  "X-Content-Type-Options"
  "Referrer-Policy"
  "X-Frame-Options"
  "Permissions-Policy"
  "Cross-Origin-Opener-Policy"
  "Cross-Origin-Resource-Policy"
)

HEALTHY_HEADERS="Strict-Transport-Security: max-age=31536000|Content-Security-Policy: default-src 'self'; base-uri 'self'; form-action 'self'; frame-ancestors 'none'; object-src 'none'|X-Content-Type-Options: nosniff|Referrer-Policy: strict-origin-when-cross-origin|X-Frame-Options: DENY|Permissions-Policy: camera=(), microphone=(), geolocation=()|Cross-Origin-Opener-Policy: same-origin|Cross-Origin-Resource-Policy: same-origin|Content-Type: text/html"
HOME_BODY='<meta name="robots" content="noindex" /><h1>The cabinet</h1><a class="bgg-credit" href="https://boardgamegeek.com" rel="noopener">Powered by BGG</a>'
HOME_HEADERS="$HEALTHY_HEADERS"
STATIC_HEADERS="$HEALTHY_HEADERS"

STUB_BIN="${WORK_DIR}/bin"
mkdir -p "$STUB_BIN"
cat > "${STUB_BIN}/curl" <<'STUB'
#!/usr/bin/env bash
### Answers from the table in STUB_TABLE: one line per answer holding the
### URL, the status code, the body and the response headers (separated by
### the pipe character), separated by tabs; a backslash-n in a body becomes
### a line break. Several lines for one URL are
### answered in order, the last one repeating. Every request is logged to
### STUB_LOG as the method and the URL.
method="GET"
dump=""
wfmt=""
out=""
urls=()
outs=()
while [ "$#" -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -D) dump="$2"; shift 2 ;;
    -w) wfmt="$2"; shift 2 ;;
    -X) method="$2"; shift 2 ;;
    --max-time | --connect-timeout) shift 2 ;;
    -*) shift ;;
    *) urls+=("$1"); outs+=("$out"); out=""; shift ;;
  esac
done
[ -n "$dump" ] && : > "$dump"
for i in "${!urls[@]}"; do
  url="${urls[$i]}"
  printf '%s %s\n' "$method" "$url" >> "$STUB_LOG"
  key="$(printf '%s' "$url" | cksum | cut -d' ' -f1)"
  n=$(( $(cat "${STUB_STATE}/${key}" 2>/dev/null || echo 0) + 1 ))
  echo "$n" > "${STUB_STATE}/${key}"
  line="$(awk -F'\t' -v u="$url" -v n="$n" '$1==u { c++; if (c==n) { print; f=1; exit } last=$0 } END { if (!f && last) print last }' "$STUB_TABLE")"
  status="000"
  body=""
  headers=""
  if [ -n "$line" ]; then
    status="$(awk -F'\t' '{print $2}' <<<"$line")"
    body="$(awk -F'\t' '{print $3}' <<<"$line")"
    headers="$(awk -F'\t' '{print $4}' <<<"$line")"
  fi
  [ -n "${outs[$i]}" ] && printf '%b' "$body" > "${outs[$i]}"
  if [ -n "$dump" ] && [ -n "$line" ]; then
    {
      printf 'HTTP/2 %s\r\n' "$status"
      tr '|' '\n' <<<"$headers" | while IFS= read -r header; do
        [ -n "$header" ] && printf '%s\r\n' "$header"
      done
      printf '\r\n'
    } >> "$dump"
  fi
  [ -n "$wfmt" ] && printf '%b' "${wfmt//"%{http_code}"/$status}"
done
exit 0
STUB
chmod +x "${STUB_BIN}/curl"

FAILURES=0
TESTS_RUN=0

check() {
  local description="$1" expected="$2" actual="$3"
  TESTS_RUN=$((TESTS_RUN + 1))
  if [ "$actual" = "$expected" ]; then
    printf 'PASS: %s\n' "$description"
  else
    printf 'FAIL: %s (expected [%s], got [%s])\n' "$description" "$expected" "$actual"
    FAILURES=$((FAILURES + 1))
  fi
}

export STUB_TABLE="${WORK_DIR}/table.tsv"
export STUB_STATE="${WORK_DIR}/state"
export STUB_LOG="${WORK_DIR}/requests.log"

### Appends one answer row: URL, STATUS, BODY and the pipe-separated HEADERS.
add_row() {
  printf '%s\t%s\t%s\t%s\n' "$1" "$2" "$3" "$4" >> "$STUB_TABLE"
}

### Replaces every answer for URL with one row.
set_row() {
  grep -v -F -- "${1}"$'\t' "$STUB_TABLE" > "${WORK_DIR}/next.tsv" || true
  mv "${WORK_DIR}/next.tsv" "$STUB_TABLE"
  add_row "$1" "$2" "$3" "$4"
}

### Prints the pipe-separated header list H without the header named NAME.
without_header() {
  tr '|' '\n' <<<"$1" | grep -vi -- "^${2}:" | paste -sd'|' -
}

### Prints the pipe-separated header list H with LINE added.
with_header() {
  printf '%s|%s' "$1" "$2"
}

### Prints the pipe-separated header list H with its header named NAME set to VALUE.
with_value() {
  with_header "$(without_header "$1" "$2")" "${2}: ${3}"
}

###
### Writes the table of a fully healthy public route to STUB_TABLE. Each
### argument of the form URL=STATUS=BODY then replaces that URL's answer and
### keeps its headers.
###
write_table() {
  local override url old_headers
  : > "$STUB_TABLE"
  add_row "https://${PUBLIC_HOST}/" 200 "$HOME_BODY" "$HOME_HEADERS"
  add_row "$STATIC_URL" 200 "<svg></svg>" "$STATIC_HEADERS"
  add_row "http://${PUBLIC_HOST}/" 301 "" "Location: https://${PUBLIC_HOST}/"
  add_row "https://${PUBLIC_HOST}/health" 404 "" ""
  add_row "https://${PUBLIC_HOST}/owner/pictures" 404 "" ""
  add_row "https://${PUBLIC_HOST}/robots.txt" 404 "" ""
  add_row "https://${OWNER_HOST}/" 200 "<h1>Owner tools</h1>" ""
  for override in "$@"; do
    url="${override%%=*}"
    old_headers="$(awk -F'\t' -v u="$url" '$1==u { print $4; exit }' "$STUB_TABLE")"
    set_row "$url" "$(cut -d= -f2 <<<"$override")" "$(cut -d= -f3- <<<"$override")" "$old_headers"
  done
}

###
### Runs the route check with the stub on the path; the status lands in
### RUN_STATUS, the report in run.out and the requests in the request log.
###
run_check() {
  RUN_STATUS=0
  rm -rf "$STUB_STATE"
  mkdir -p "$STUB_STATE"
  : > "$STUB_LOG"
  env -u CABINET_OWNER_HOST -u CABINET_PUBLIC_HOST -u CABINET_BURST_COUNT PATH="${STUB_BIN}:${PATH}" "$@" > "${WORK_DIR}/run.out" 2>&1 || RUN_STATUS=$?
}

###
### Runs the check from the home network with both hostnames set. Arguments
### of the form NAME=VALUE join the environment, the others are options.
###
run_lan() {
  local argument
  local -a environment=("CABINET_PUBLIC_HOST=${PUBLIC_HOST}" "CABINET_OWNER_HOST=${OWNER_HOST}") options=(--from-lan)
  for argument in "$@"; do
    if [[ "$argument" == *=* ]]; then
      environment+=("$argument")
    else
      options+=("$argument")
    fi
  done
  run_check "${environment[@]}" bash "$SCRIPT" "${options[@]}"
}

count_matching() {
  local count
  count="$(grep -c -- "$1" "${WORK_DIR}/run.out" || true)"
  echo "${count:-0}"
}

count_requests() {
  local count
  count="$(grep -c -- "$1" "$STUB_LOG" || true)"
  echo "${count:-0}"
}

reset_headers() {
  HOME_HEADERS="$HEALTHY_HEADERS"
  STATIC_HEADERS="$HEALTHY_HEADERS"
}

write_table
run_lan
check "a healthy public route seen from the home network exits 0" "0" "$RUN_STATUS"
check "a healthy public route prints 31 PASS lines" "31" "$(count_matching '^PASS - ')"
check "a healthy public route prints no FAIL line" "0" "$(count_matching '^FAIL - ')"
check "the report never prints the public hostname" "0" "$(count_matching "$PUBLIC_HOST")"
check "the report never prints the owner hostname" "0" "$(count_matching "$OWNER_HOST")"
check "without --burst no press is sent" "0" "$(count_requests '^POST ')"

for name in "${HEADER_NAMES[@]}"; do
  HOME_HEADERS="$(without_header "$HEALTHY_HEADERS" "$name")"
  write_table
  reset_headers
  run_lan
  check "the home page without ${name} exits 1" "1" "$RUN_STATUS"
  check "the home page without ${name} gives exactly one FAIL" "1" "$(count_matching '^FAIL - ')"
  check "the home page without ${name} names the header and the page" "1" \
    "$(count_matching "^FAIL - the public home page .*${name}")"

  STATIC_HEADERS="$(without_header "$HEALTHY_HEADERS" "$name")"
  write_table
  reset_headers
  run_lan
  check "the static file without ${name} gives exactly one FAIL" "1" "$(count_matching '^FAIL - ')"
  check "the static file without ${name} names the header and the page" "1" \
    "$(count_matching "^FAIL - the static file .*${name}")"

  HOME_HEADERS="$(with_header "$HEALTHY_HEADERS" "${name}: duplicate")"
  write_table
  reset_headers
  run_lan
  check "the home page sending ${name} twice gives exactly one FAIL" "1" "$(count_matching '^FAIL - ')"
  check "the home page sending ${name} twice names the header" "1" \
    "$(count_matching "^FAIL - the public home page .*${name}.*twice\|^FAIL - the public home page .*${name}.*2 times")"
done

HOME_HEADERS="$(with_value "$HEALTHY_HEADERS" "Strict-Transport-Security" "max-age=31536000; includeSubDomains; preload")"
write_table
reset_headers
run_lan
check "a Strict-Transport-Security header with the preload flag exits 1" "1" "$RUN_STATUS"
check "a Strict-Transport-Security header with the preload flag gives one named FAIL" "1" \
  "$(count_matching '^FAIL - .*Strict-Transport-Security.*preload')"
check "a Strict-Transport-Security header with the preload flag gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

HOME_HEADERS="$(with_value "$HEALTHY_HEADERS" "Content-Security-Policy" "default-src 'self'")"
write_table
reset_headers
run_lan
check "a policy without frame-ancestors gives one named FAIL" "1" \
  "$(count_matching "^FAIL - .*Content-Security-Policy.*frame-ancestors")"
check "a policy without frame-ancestors gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

HOME_HEADERS="$(with_value "$HEALTHY_HEADERS" "X-Frame-Options" "SAMEORIGIN")"
write_table
reset_headers
run_lan
check "an unexpected X-Frame-Options value gives one named FAIL" "1" \
  "$(count_matching '^FAIL - .*X-Frame-Options')"

STATIC_HEADERS="$(with_header "$HEALTHY_HEADERS" "Server: Kestrel")"
write_table
reset_headers
run_lan
check "a Server header on the static file exits 1" "1" "$RUN_STATUS"
check "a Server header on the static file gives one named FAIL" "1" \
  "$(count_matching '^FAIL - the static file sends no Server header')"
check "a Server header on the static file gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_table "https://${PUBLIC_HOST}/=502=bad gateway"
run_lan
check "a home page answering 502 exits 1" "1" "$RUN_STATUS"
check "a home page answering 502 names the status" "1" \
  "$(count_matching '^FAIL - the public home page answers 200 (status 502)')"

write_table
run_check "CABINET_PUBLIC_HOST=${PUBLIC_HOST}" "CABINET_OWNER_HOST=${OWNER_HOST}" bash "$SCRIPT"
check "no vantage option exits 2" "2" "$RUN_STATUS"
check "no vantage option prints usage" "1" "$(count_matching '^Usage: ')"
check "no vantage option sends no request" "0" "$(wc -l < "$STUB_LOG" | tr -d ' ')"

run_check "CABINET_PUBLIC_HOST=${PUBLIC_HOST}" "CABINET_OWNER_HOST=${OWNER_HOST}" bash "$SCRIPT" --from-lan --from-outside
check "both vantage options exit 2" "2" "$RUN_STATUS"

run_check "CABINET_PUBLIC_HOST=${PUBLIC_HOST}" "CABINET_OWNER_HOST=${OWNER_HOST}" bash "$SCRIPT" --from-lan --unknown
check "an unknown option exits 2" "2" "$RUN_STATUS"

run_check "CABINET_OWNER_HOST=${OWNER_HOST}" bash "$SCRIPT" --from-lan
check "a missing public hostname exits 2" "2" "$RUN_STATUS"
check "a missing public hostname prints usage" "1" "$(count_matching '^Usage: ')"

run_check "CABINET_PUBLIC_HOST=${PUBLIC_HOST}" bash "$SCRIPT" --from-lan
check "a missing owner hostname exits 2" "2" "$RUN_STATUS"
check "a missing owner hostname prints usage" "1" "$(count_matching '^Usage: ')"

run_check bash "$SCRIPT" --help
check "--help exits 0" "0" "$RUN_STATUS"
check "--help prints usage" "1" "$(count_matching '^Usage: ')"

write_table "http://${PUBLIC_HOST}/=200=<h1>The cabinet</h1>"
run_lan
check "plain HTTP answering 200 exits 1" "1" "$RUN_STATUS"
check "plain HTTP answering 200 gives one named FAIL" "1" "$(count_matching '^FAIL - plain HTTP redirects to HTTPS (status 200)')"
check "plain HTTP answering 200 gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_table
set_row "http://${PUBLIC_HOST}/" 302 "" "Location: http://${PUBLIC_HOST}/elsewhere"
run_lan
check "a redirect that stays on HTTP gives one named FAIL" "1" "$(count_matching '^FAIL - plain HTTP redirects to HTTPS.*not HTTPS')"
check "a redirect that stays on HTTP gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

for status in 301 302 307 308; do
  write_table
  set_row "http://${PUBLIC_HOST}/" "$status" "" "Location: https://${PUBLIC_HOST}/"
  run_lan
  check "a ${status} redirect to HTTPS passes" "0" "$RUN_STATUS"
done

write_table "https://${PUBLIC_HOST}/health=200=ok"
run_lan
check "the public health page answering 200 exits 1" "1" "$RUN_STATUS"
check "the public health page answering 200 gives one named FAIL" "1" \
  "$(count_matching '^FAIL - the public health page answers 404 (status 200)')"
check "the public health page answering 200 gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_table "https://${PUBLIC_HOST}/owner/pictures=200=<h1>Pictures</h1>"
run_lan
check "the public owner page answering 200 gives one named FAIL" "1" \
  "$(count_matching '^FAIL - the public owner page answers 404 (status 200)')"
check "the public owner page answering 200 gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

for port in 5080 5081 5082; do
  write_table
  add_row "http://${PUBLIC_HOST}:${port}/" 200 "open" ""
  run_lan
  check "port ${port} answering exits 1" "1" "$RUN_STATUS"
  check "port ${port} answering gives one named FAIL" "1" \
    "$(count_matching "^FAIL - port ${port} on the public hostname answers nothing (status 200)")"
  check "port ${port} answering gives no other FAIL" "1" "$(count_matching '^FAIL - ')"
done

HOME_BODY='<h1>The cabinet</h1><a class="bgg-credit" href="https://boardgamegeek.com" rel="noopener">Powered by BGG</a>'
write_table
HOME_BODY='<meta name="robots" content="noindex" /><h1>The cabinet</h1><a class="bgg-credit" href="https://boardgamegeek.com" rel="noopener">Powered by BGG</a>'
run_lan
check "a home page without the noindex tag exits 1" "1" "$RUN_STATUS"
check "a home page without the noindex tag gives one named FAIL" "1" "$(count_matching '^FAIL - .*noindex')"
check "a home page without the noindex tag gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

HOME_BODY='<meta name="robots" content="noindex" /><h1>The cabinet</h1><a href="https://example.com">Powered</a>'
write_table
HOME_BODY='<meta name="robots" content="noindex" /><h1>The cabinet</h1><a class="bgg-credit" href="https://boardgamegeek.com" rel="noopener">Powered by BGG</a>'
run_lan
check "a home page without the linked credit gives one named FAIL" "1" "$(count_matching '^FAIL - .*BoardGameGeek credit')"
check "a home page without the linked credit gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

HOME_BODY='<meta name="robots" content="noindex" /><a href="https://boardgamegeek.com">BGG</a><a href="/owner/pictures">Owner</a>'
write_table
HOME_BODY='<meta name="robots" content="noindex" /><h1>The cabinet</h1><a class="bgg-credit" href="https://boardgamegeek.com" rel="noopener">Powered by BGG</a>'
run_lan
check "a home page linking the owner area gives one named FAIL" "1" "$(count_matching '^FAIL - .*/owner reference')"
check "a home page linking the owner area gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_table
set_row "https://${PUBLIC_HOST}/robots.txt" 200 'User-agent: *\nDisallow: /' ""
run_lan
check "a robots.txt that disallows the whole site exits 1" "1" "$RUN_STATUS"
check "a robots.txt that disallows the whole site gives one named FAIL" "1" "$(count_matching '^FAIL - robots.txt.*Disallow')"

write_table
set_row "https://${PUBLIC_HOST}/robots.txt" 200 'User-agent: *\nDisallow: /private' ""
run_lan
check "a robots.txt that disallows only a path passes" "0" "$RUN_STATUS"

write_table
set_row "https://${PUBLIC_HOST}/robots.txt" 500 "" ""
run_lan
check "a robots.txt answering 500 gives one named FAIL" "1" "$(count_matching '^FAIL - robots.txt.*(status 500)')"

write_table "https://${OWNER_HOST}/=403=Not available from this address."
run_lan
check "the owner hostname answering 403 from the home network exits 1" "1" "$RUN_STATUS"
check "the owner hostname answering 403 from the home network gives one named FAIL" "1" \
  "$(count_matching '^FAIL - the owner hostname seen from the home network answers 200 (status 403)')"

run_check "CABINET_PUBLIC_HOST=${PUBLIC_HOST}" "CABINET_OWNER_HOST=${OWNER_HOST}" bash "$SCRIPT" --from-outside
check "the owner hostname answering 403 from outside exits 0" "0" "$RUN_STATUS"
check "the owner hostname answering 403 from outside prints 31 PASS lines" "31" "$(count_matching '^PASS - ')"

write_table
run_check "CABINET_PUBLIC_HOST=${PUBLIC_HOST}" "CABINET_OWNER_HOST=${OWNER_HOST}" bash "$SCRIPT" --from-outside
check "the owner hostname answering 200 from outside exits 1" "1" "$RUN_STATUS"
check "the owner hostname answering 200 from outside gives one named FAIL" "1" \
  "$(count_matching '^FAIL - the owner hostname seen from outside answers 403 (status 200)')"
check "the report from outside never prints a hostname" "0" "$(count_matching "example\.\(com\|org\)")"

HOME_BODY='<meta name="robots" content="noindex" /><h1>The cabinet</h1><a class="bgg-credit" href="https://boardgamegeek.com" rel="noopener">Powered by BGG</a>'

### Writes the healthy table plus press answers: each argument is STATUS=BODY=HEADERS.
write_burst_table() {
  local answer
  write_table
  for answer in "$@"; do
    add_row "$PRESS_URL" "$(cut -d= -f1 <<<"$answer")" "$(cut -d= -f2 <<<"$answer")" "$(cut -d= -f3- <<<"$answer")"
  done
}

COOLDOWN_HEADERS="Retry-After: 45|Cache-Control: no-store"
STARTED='{"outcome":"started"}'
BUSY='{"outcome":"busy","status":{"state":"running"}}'
COOLDOWN='{"outcome":"cooldown"}'
SHARED='{"outcome":"cooldown","status":{"state":"idle"}}'

write_burst_table "202=${STARTED}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" \
  "429=${COOLDOWN}=${COOLDOWN_HEADERS}" "429=${COOLDOWN}=${COOLDOWN_HEADERS}" "429=${COOLDOWN}=${COOLDOWN_HEADERS}"
run_lan --burst
check "a burst refused by the app's own limit exits 0" "0" "$RUN_STATUS"
check "a burst refused by the app's own limit prints 32 PASS lines" "32" "$(count_matching '^PASS - ')"
check "a burst without CABINET_BURST_COUNT sends exactly 8 presses" "8" "$(count_requests '^POST ')"
check "the burst presses go to the press address" "8" "$(count_requests "^POST ${PRESS_URL}\$")"
check "the burst report never prints a hostname" "0" "$(count_matching "example\.\(com\|org\)")"
check "the burst report names the status counts" "1" "$(count_matching 'answers 202x1 409x4 429x3')"

write_burst_table "202=${STARTED}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" \
  "429=${SHARED}=${COOLDOWN_HEADERS}" "429=${SHARED}=${COOLDOWN_HEADERS}" "429=${SHARED}=${COOLDOWN_HEADERS}"
run_lan --burst
check "a burst refused only by the shared window exits 1" "1" "$RUN_STATUS"
check "a burst refused only by the shared window gives one FAIL naming it" "1" "$(count_matching '^FAIL - .*shared window')"
check "a burst refused only by the shared window gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_burst_table "202=${STARTED}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" \
  "429=Too Many Requests=" "429=Too Many Requests=" "429=Too Many Requests=Retry-After: 30"
run_lan --burst
check "a burst refused by the proxy exits 1" "1" "$RUN_STATUS"
check "a burst refused by the proxy gives one FAIL naming the proxy" "1" "$(count_matching '^FAIL - .*from the proxy')"
check "a burst refused by the proxy gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_burst_table "202=${STARTED}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" \
  "429=${COOLDOWN}=Cache-Control: no-store" "429=${COOLDOWN}=Cache-Control: no-store" "429=${COOLDOWN}=Cache-Control: no-store"
run_lan --burst
check "a refusal without Retry-After exits 1" "1" "$RUN_STATUS"
check "a refusal without Retry-After gives one FAIL naming the header" "1" "$(count_matching '^FAIL - .*without Retry-After')"
check "a refusal without Retry-After gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_burst_table "202=${STARTED}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" \
  "429=${SHARED}=${COOLDOWN_HEADERS}" "429=${SHARED}=${COOLDOWN_HEADERS}" "429=${COOLDOWN}=Cache-Control: no-store"
run_lan --burst
check "another refusal's Retry-After does not stand in for the app's own missing one, exit 1" "1" "$RUN_STATUS"
check "another refusal's Retry-After does not stand in for the app's own missing one, one FAIL names the header" "1" \
  "$(count_matching '^FAIL - .*without Retry-After')"

write_burst_table "202=${STARTED}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" "409=${BUSY}=" \
  "429=${SHARED}=Cache-Control: no-store" "429=${COOLDOWN}=${COOLDOWN_HEADERS}" "429=${COOLDOWN}=${COOLDOWN_HEADERS}"
run_lan --burst
check "every app refusal with its own Retry-After passes even when a shared refusal has none" "0" "$RUN_STATUS"

write_burst_table "202=${STARTED}=" "409=${BUSY}="
run_lan --burst
check "a burst without any 429 exits 1" "1" "$RUN_STATUS"
check "a burst without any 429 gives one FAIL saying so" "1" "$(count_matching '^FAIL - .*no 429')"
check "a burst without any 429 gives no other FAIL" "1" "$(count_matching '^FAIL - ')"

write_table
run_lan --burst
check "a burst against a press address that answers nothing exits 1" "1" "$RUN_STATUS"
check "a burst against a press address that answers nothing gives one FAIL saying so" "1" "$(count_matching '^FAIL - .*no 429')"

write_burst_table "429=${COOLDOWN}=${COOLDOWN_HEADERS}"
run_lan --burst CABINET_BURST_COUNT=3
check "CABINET_BURST_COUNT=3 sends exactly 3 presses" "3" "$(count_requests '^POST ')"

run_lan --burst CABINET_BURST_COUNT=20
check "CABINET_BURST_COUNT=20 sends exactly 20 presses" "20" "$(count_requests '^POST ')"
check "CABINET_BURST_COUNT=20 passes" "0" "$RUN_STATUS"

run_lan --burst CABINET_BURST_COUNT=21
check "CABINET_BURST_COUNT=21 exits 2" "2" "$RUN_STATUS"
check "CABINET_BURST_COUNT=21 says the burst is capped at 20" "1" "$(count_matching 'capped at 20')"
check "CABINET_BURST_COUNT=21 sends no request" "0" "$(wc -l < "$STUB_LOG" | tr -d ' ')"

run_lan --burst CABINET_BURST_COUNT=0
check "CABINET_BURST_COUNT=0 exits 2" "2" "$RUN_STATUS"

run_lan --burst CABINET_BURST_COUNT=many
check "a non-numeric CABINET_BURST_COUNT exits 2" "2" "$RUN_STATUS"

run_lan CABINET_BURST_COUNT=99
check "an oversized CABINET_BURST_COUNT without --burst is ignored" "0" "$RUN_STATUS"
check "an oversized CABINET_BURST_COUNT without --burst sends no press" "0" "$(count_requests '^POST ')"

echo "----"
echo "${TESTS_RUN} check(s) run, ${FAILURES} failure(s)"
[ "$FAILURES" -eq 0 ]

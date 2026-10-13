#!/usr/bin/env bash
###
### Proves the owner route check against a stubbed curl: it passes only when
### the owner host serves the owner pages and the public host hides them,
### fails with a named check for every single deviation, refuses to run
### without both hostnames, and never prints a hostname. Offline: nothing is
### fetched.
###
set -uo pipefail

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="${REPO_ROOT}/build/check-owner-routes.sh"

WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

OWNER_HOST="owner.example.com"
PUBLIC_HOST="cabinet.example.org"
PREVIEW_PATH="/owner/preview/00000000000000000000000000000000"

STUB_BIN="${WORK_DIR}/bin"
mkdir -p "$STUB_BIN"
cat > "${STUB_BIN}/curl" <<'STUB'
#!/usr/bin/env bash
### Answers from the table in STUB_TABLE: one line per URL holding the URL,
### the status code and the body, separated by tabs.
out=""
url=""
while [ "$#" -gt 0 ]; do
  case "$1" in
    -o) out="$2"; shift 2 ;;
    -w | --max-time) shift 2 ;;
    -*) shift ;;
    *) url="$1"; shift ;;
  esac
done
line="$(grep -F -- "${url}"$'\t' "$STUB_TABLE" | head -n 1)"
if [ -z "$line" ]; then
  printf '000'
  exit 0
fi
IFS=$'\t' read -r _ status body <<<"$line"
[ -n "$out" ] && printf '%s' "$body" > "$out"
printf '%s' "$status"
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

###
### Writes the table of a fully healthy deployment to STUB_TABLE. Each
### argument of the form URL=STATUS=BODY then replaces that URL's answer.
###
write_table() {
  local override url
  {
    printf 'https://%s/\t200\t%s\n' "$OWNER_HOST" "<h1>Owner tools</h1> pictures settings"
    printf 'https://%s/owner/pictures\t200\t%s\n' "$OWNER_HOST" "<h1>Pictures</h1>"
    printf 'https://%s/owner/settings\t200\t%s\n' "$OWNER_HOST" "<h1>Settings</h1>"
    printf 'https://%s%s\t404\t\n' "$PUBLIC_HOST" "/owner/pictures"
    printf 'https://%s%s\t404\t\n' "$PUBLIC_HOST" "$PREVIEW_PATH"
    printf 'https://%s/\t200\t%s\n' "$PUBLIC_HOST" "<h1>The cabinet</h1>"
  } > "$STUB_TABLE"
  for override in "$@"; do
    url="${override%%=*}"
    grep -vF -- "${url}"$'\t' "$STUB_TABLE" > "${WORK_DIR}/next.tsv"
    printf '%s\t%s\t%s\n' "$url" "$(cut -d= -f2 <<<"$override")" "$(cut -d= -f3- <<<"$override")" >> "${WORK_DIR}/next.tsv"
    mv "${WORK_DIR}/next.tsv" "$STUB_TABLE"
  done
}

export STUB_TABLE="${WORK_DIR}/table.tsv"

###
### Runs the route check with the stub on the path; the status lands in
### RUN_STATUS and the report in run.out.
###
run_check() {
  RUN_STATUS=0
  env -u CABINET_OWNER_HOST -u CABINET_PUBLIC_HOST PATH="${STUB_BIN}:${PATH}" STUB_TABLE="$STUB_TABLE" "$@" bash "$SCRIPT" > "${WORK_DIR}/run.out" 2>&1 || RUN_STATUS=$?
}

count_matching() {
  local count
  count="$(grep -c -- "$1" "${WORK_DIR}/run.out" || true)"
  echo "${count:-0}"
}

write_table
run_check "CABINET_OWNER_HOST=${OWNER_HOST}" "CABINET_PUBLIC_HOST=${PUBLIC_HOST}"
check "a healthy deployment exits 0" "0" "$RUN_STATUS"
check "a healthy deployment prints six PASS lines" "6" "$(count_matching '^PASS - ')"
check "a healthy deployment prints no FAIL line" "0" "$(count_matching '^FAIL - ')"
check "the report never prints the owner hostname" "0" "$(count_matching "$OWNER_HOST")"
check "the report never prints the public hostname" "0" "$(count_matching "$PUBLIC_HOST")"

write_table "https://${OWNER_HOST}/owner/pictures=403=Not available from this address."
run_check "CABINET_OWNER_HOST=${OWNER_HOST}" "CABINET_PUBLIC_HOST=${PUBLIC_HOST}"
check "an owner host answering 403 exits 1" "1" "$RUN_STATUS"
check "an owner host answering 403 names the failed check and its status" "1" \
  "$(count_matching '^FAIL - .*pictures.* (status 403)')"

write_table "https://${OWNER_HOST}/=200=<h1>Welcome</h1>"
run_check "CABINET_OWNER_HOST=${OWNER_HOST}" "CABINET_PUBLIC_HOST=${PUBLIC_HOST}"
check "an owner home page without the plate exits 1" "1" "$RUN_STATUS"
check "an owner home page without the plate names the plate check" "1" \
  "$(count_matching '^FAIL - .*Owner tools')"

write_table "https://${PUBLIC_HOST}/owner/pictures=200=<h1>Pictures</h1>"
run_check "CABINET_OWNER_HOST=${OWNER_HOST}" "CABINET_PUBLIC_HOST=${PUBLIC_HOST}"
check "the public host serving the pictures page exits 1" "1" "$RUN_STATUS"
check "the public host serving the pictures page is named as a failure" "1" \
  "$(count_matching '^FAIL - .*public.*pictures.* (status 200)')"

write_table "https://${PUBLIC_HOST}${PREVIEW_PATH}=200=picture"
run_check "CABINET_OWNER_HOST=${OWNER_HOST}" "CABINET_PUBLIC_HOST=${PUBLIC_HOST}"
check "the public host serving a preview exits 1" "1" "$RUN_STATUS"
check "the public host serving a preview is named as a failure" "1" \
  "$(count_matching '^FAIL - .*public.*preview.* (status 200)')"

write_table "https://${PUBLIC_HOST}/=200=<a href=\"/owner/pictures\">Owner</a>"
run_check "CABINET_OWNER_HOST=${OWNER_HOST}" "CABINET_PUBLIC_HOST=${PUBLIC_HOST}"
check "a public home page linking the owner area exits 1" "1" "$RUN_STATUS"
check "a public home page linking the owner area names the check" "1" \
  "$(count_matching '^FAIL - .*public home page.*/owner')"

write_table
run_check "CABINET_PUBLIC_HOST=${PUBLIC_HOST}"
check "a missing owner hostname exits 2" "2" "$RUN_STATUS"
check "a missing owner hostname prints usage" "1" "$(count_matching '^Usage: ')"

run_check "CABINET_OWNER_HOST=${OWNER_HOST}"
check "a missing public hostname exits 2" "2" "$RUN_STATUS"
check "a missing public hostname prints usage" "1" "$(count_matching '^Usage: ')"

echo "----"
echo "${TESTS_RUN} check(s) run, ${FAILURES} failure(s)"
[ "$FAILURES" -eq 0 ]

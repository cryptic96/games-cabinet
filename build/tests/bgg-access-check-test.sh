#!/usr/bin/env bash
set -euo pipefail

# Offline test of build/bgg-access-check.py. Everything here runs on built-in
# synthetic data or against an env file that holds no BoardGameGeek settings,
# so nothing ever talks to the network.

REPO_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
SCRIPT="$REPO_ROOT/build/bgg-access-check.py"

WORK_DIR="$(mktemp -d)"
cleanup() {
  rm -rf "$WORK_DIR"
}
trap cleanup EXIT

PASSED=0
FAILED=0

pass() {
  echo "PASS: $1"
  PASSED=$((PASSED + 1))
}

fail() {
  echo "FAIL: $1" >&2
  FAILED=$((FAILED + 1))
}

status_of() {
  local status=0
  "$@" >"$WORK_DIR/out.txt" 2>"$WORK_DIR/err.txt" || status=$?
  echo "$status"
}

echo "=== self-test ==="
if [ "$(status_of python3 -I "$SCRIPT" --self-test)" = "0" ]; then
  pass "self-test exits 0 in isolated mode"
else
  fail "self-test did not exit 0"
  cat "$WORK_DIR/out.txt" >&2
fi

echo "=== art self-test ==="
if [ "$(status_of python3 -I "$SCRIPT" --self-test --suite art)" = "0" ]; then
  pass "the art self-test exits 0 in isolated mode"
else
  fail "the art self-test did not exit 0"
  cat "$WORK_DIR/out.txt" >&2
fi

echo "=== plan ==="
if [ "$(status_of python3 "$SCRIPT" --plan)" = "0" ]; then
  plan_ok=1
  for label in A B C D E F G H I; do
    if ! grep -q "^$label " "$WORK_DIR/out.txt"; then
      echo "plan output is missing label $label" >&2
      plan_ok=0
    fi
  done
  if grep -q '=' "$WORK_DIR/out.txt"; then
    echo "plan output shows a parameter value" >&2
    plan_ok=0
  fi
  if grep -qi 'http' "$WORK_DIR/out.txt"; then
    echo "plan output shows a URL" >&2
    plan_ok=0
  fi
  if [ "$plan_ok" = "1" ]; then
    pass "plan lists labels A to I with parameter names only"
  else
    fail "plan output is wrong"
  fi
else
  fail "plan did not exit 0"
fi

echo "=== art plan ==="
if [ "$(status_of python3 "$SCRIPT" --plan --suite art)" = "0" ]; then
  art_plan_ok=1
  for label in J K L M1 N; do
    if ! grep -q "^$label " "$WORK_DIR/out.txt"; then
      echo "art plan output is missing label $label" >&2
      art_plan_ok=0
    fi
  done
  if grep -q '=' "$WORK_DIR/out.txt"; then
    echo "art plan output shows a parameter value" >&2
    art_plan_ok=0
  fi
  if grep -qi 'http' "$WORK_DIR/out.txt"; then
    echo "art plan output shows a URL" >&2
    art_plan_ok=0
  fi
  if [ "$art_plan_ok" = "1" ]; then
    pass "the art plan lists labels J, K, L, M1 and N with parameter names only"
  else
    fail "the art plan output is wrong"
  fi
else
  fail "the art plan did not exit 0"
fi

if [ "$(status_of python3 "$SCRIPT" --plan)" = "0" ] && ! grep -q '^J ' "$WORK_DIR/out.txt" && grep -q '^I ' "$WORK_DIR/out.txt"; then
  pass "the plan without a suite is still the access plan"
else
  fail "the plan without a suite is not the access plan"
fi

echo "=== usage ==="
if [ "$(status_of python3 "$SCRIPT")" = "2" ]; then
  pass "no arguments exits 2"
else
  fail "no arguments did not exit 2"
fi

if [ "$(status_of python3 "$SCRIPT" --plan --suite other)" = "2" ]; then
  pass "an unknown suite exits 2"
else
  fail "an unknown suite did not exit 2"
fi

echo "=== not configured ==="
EMPTY_ENV="$WORK_DIR/empty.env"
printf '# no BoardGameGeek settings here\nSomething__Else=1\n' >"$EMPTY_ENV"

NO_NETWORK='
import http.client, runpy, socket, ssl, sys

def refuse(*args, **kwargs):
    raise AssertionError("network used")

socket.socket = refuse
socket.create_connection = refuse
script, env_file = sys.argv[1], sys.argv[2]
sys.argv = [script, "--run", "--env-file", env_file] + sys.argv[3:]
runpy.run_path(script, run_name="__main__")
'

if [ "$(status_of python3 -c "$NO_NETWORK" "$SCRIPT" "$EMPTY_ENV")" = "3" ] && grep -q 'not configured' "$WORK_DIR/out.txt"; then
  pass "an env file without the keys exits 3 without network use"
else
  fail "an env file without the keys did not exit 3 cleanly"
fi

if [ "$(status_of python3 -c "$NO_NETWORK" "$SCRIPT" "$WORK_DIR/missing.env")" = "3" ]; then
  pass "a missing env file exits 3 without network use"
else
  fail "a missing env file did not exit 3 cleanly"
fi

if [ "$(status_of python3 -c "$NO_NETWORK" "$SCRIPT" "$EMPTY_ENV" --suite art)" = "3" ] && grep -q 'not configured' "$WORK_DIR/out.txt"; then
  pass "an art run with an env file without the keys exits 3 without network use"
else
  fail "an art run with an env file without the keys did not exit 3 cleanly"
fi

if [ "$(status_of python3 -c "$NO_NETWORK" "$SCRIPT" "$WORK_DIR/missing.env" --suite art)" = "3" ]; then
  pass "an art run with a missing env file exits 3 without network use"
else
  fail "an art run with a missing env file did not exit 3 cleanly"
fi

TOKEN_ONLY_ENV="$WORK_DIR/token-only.env"
printf 'Bgg__Token=sentinel-token-value\n' >"$TOKEN_ONLY_ENV"
if [ "$(status_of python3 -c "$NO_NETWORK" "$SCRIPT" "$TOKEN_ONLY_ENV")" = "3" ]; then
  pass "an env file without a username exits 3 without network use"
else
  fail "an env file without a username did not exit 3 cleanly"
fi

echo "=== the output guard in a real run ==="
GUARD_DRIVER='
import http.client, importlib.util, socket, ssl, sys

def refuse(*args, **kwargs):
    raise AssertionError("network used")

socket.socket = refuse
socket.create_connection = refuse
script, env_file, payload = sys.argv[1], sys.argv[2], sys.argv[3]
spec = importlib.util.spec_from_file_location("bgg_access_check", script)
module = importlib.util.module_from_spec(spec)
sys.modules["bgg_access_check"] = module
spec.loader.exec_module(module)
if payload == "WRONG_TOKEN":
    payload = module.WRONG_TOKEN

def stub_calls(transport, username, collector, progress):
    return ["call A: status 200", payload], False

def stub_art_calls(api, images, username, collector, progress):
    collector.note_number("5000001")
    collector.note("Example Game One")
    return ["call J: status 200", payload], False

module.run_calls = stub_calls
module.run_art_calls = stub_art_calls
sys.exit(module.main(["--run", "--env-file", env_file] + sys.argv[4:]))
'

write_env() {
  printf 'Bgg__Token=%s\nBgg__Username=%s\nBgg__ContactUrl=%s\n' "$2" "$3" "${4:-https://example.org/sentinel-contact}" >"$1"
}

expect_guard() {
  local name="$1" env_file="$2" payload="$3" expected="$4"
  local actual
  shift 4
  actual="$(status_of python3 -c "$GUARD_DRIVER" "$SCRIPT" "$env_file" "$payload" "$@")"
  if [ "$actual" = "$expected" ]; then
    pass "$name"
  else
    fail "$name (exit $actual, expected $expected)"
  fi
}

LONG_ENV="$WORK_DIR/long-name.env"
write_env "$LONG_ENV" "sentinel-token-value" "sentinel-user-name"
expect_guard "a clean report is printed" "$LONG_ENV" "shape only" 0
if ! grep -q 'shape only' "$WORK_DIR/out.txt"; then
  fail "the clean report was not printed"
fi
expect_guard "a report echoing the token is withheld" "$LONG_ENV" "see sentinel-token-value here" 4
if ! grep -q 'output withheld' "$WORK_DIR/out.txt" || ! grep -q '^hint:' "$WORK_DIR/out.txt"; then
  fail "the withheld run did not explain itself"
fi
if grep -q 'sentinel-token-value' "$WORK_DIR/out.txt"; then
  fail "the withheld run printed the token"
fi
expect_guard "a report echoing the username is withheld" "$LONG_ENV" "see SENTINEL-user-name here" 4
PLAIN_CONTACT_ENV="$WORK_DIR/plain-contact.env"
write_env "$PLAIN_CONTACT_ENV" "sentinel-token-value" "sentinel-user-name" "sentinel-contact-address"
expect_guard "a report echoing the contact address is withheld" "$PLAIN_CONTACT_ENV" "see sentinel-contact-address here" 4
expect_guard "a report echoing the fixed wrong token is withheld" "$LONG_ENV" "WRONG_TOKEN" 4

expect_guard "an art report with only counts is printed" "$LONG_ENV" "items 4" 0 --suite art
expect_guard "an art report echoing a collection id is withheld" "$LONG_ENV" "seen 5000001 here" 4 --suite art
expect_guard "an art report with a longer number is printed" "$LONG_ENV" "seen 50000019 here" 0 --suite art
expect_guard "an art report echoing a title is withheld" "$LONG_ENV" "see Example Game One here" 4 --suite art
expect_guard "an art report echoing a web address is withheld" "$LONG_ENV" "see https://example.org/x here" 4 --suite art
expect_guard "an art report echoing the token is withheld" "$LONG_ENV" "see sentinel-token-value here" 4 --suite art

SHORT_ENV="$WORK_DIR/short-name.env"
write_env "$SHORT_ENV" "sentinel-token-value" "us"
expect_guard "a short username inside longer words does not withhold the report" "$SHORT_ENV" "status items stats" 0
expect_guard "a short username as a whole word is withheld" "$SHORT_ENV" "seen by us today" 4
expect_guard "a short username in another case as a whole word is withheld" "$SHORT_ENV" "seen by US today" 4

SHORT_TOKEN_ENV="$WORK_DIR/short-token.env"
write_env "$SHORT_TOKEN_ENV" "abc" "sentinel-user-name"
expect_guard "a short token inside a longer word is still withheld" "$SHORT_TOKEN_ENV" "abcdef" 4

echo "=== refused document forms ==="
BODY_DRIVER='
import importlib.util, sys

script, encoding, kind = sys.argv[1], sys.argv[2], sys.argv[3]
spec = importlib.util.spec_from_file_location("bgg_access_check", script)
module = importlib.util.module_from_spec(spec)
sys.modules["bgg_access_check"] = module
spec.loader.exec_module(module)
documents = {
    "doctype": "<?xml version=\"1.0\"?><!DOCTYPE items [<!ENTITY invented \"x\">]><items>&invented;</items>",
    "clean": "<?xml version=\"1.0\"?><items totalitems=\"0\"></items>",
}
print(module.classify_body(documents[kind].encode(encoding))[0])
'

expect_body_class() {
  local name="$1" encoding="$2" kind="$3" expected="$4"
  local status actual
  status="$(status_of python3 -I -c "$BODY_DRIVER" "$SCRIPT" "$encoding" "$kind")"
  actual="$(tr -d '[:space:]' <"$WORK_DIR/out.txt")"
  if [ "$status" = "0" ] && [ "$actual" = "$expected" ]; then
    pass "$name"
  else
    fail "$name (exit $status, class '$actual', expected '$expected')"
  fi
}

for encoding in utf-16 utf-16-le utf-16-be utf-32 utf-32-le utf-32-be; do
  expect_body_class "a doctype in $encoding is refused" "$encoding" doctype other
  expect_body_class "a clean document in $encoding is refused" "$encoding" clean other
done
expect_body_class "a doctype in utf-8 is refused" utf-8 doctype other
expect_body_class "a clean utf-8 document is read" utf-8 clean xml:items

echo "=== standard library only ==="
IMPORT_CHECK='
import ast, sys

tree = ast.parse(open(sys.argv[1], encoding="utf-8").read())
modules = set()
for node in ast.walk(tree):
    if isinstance(node, ast.Import):
        modules.update(alias.name.split(".")[0] for alias in node.names)
    elif isinstance(node, ast.ImportFrom) and node.module:
        modules.add(node.module.split(".")[0])
outside = sorted(name for name in modules if name not in sys.stdlib_module_names)
if outside:
    print("non-standard imports: " + ", ".join(outside))
    sys.exit(1)
'
if [ "$(status_of python3 -c "$IMPORT_CHECK" "$SCRIPT")" = "0" ]; then
  pass "every import is a standard-library module"
else
  fail "the script imports a non-standard module"
  cat "$WORK_DIR/out.txt" >&2
fi

echo "=== the host is fixed ==="
if grep -q 'HTTPSConnection("boardgamegeek.com"' "$SCRIPT" && ! grep -qE 'www\.boardgamegeek' "$SCRIPT"; then
  pass "the connection targets the bare host only"
else
  fail "the connection host is not the bare host"
fi

echo
echo "$PASSED passed, $FAILED failed"
if [ "$FAILED" -ne 0 ]; then
  exit 1
fi
echo "All bgg-access-check.py cases passed."

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

echo "=== usage ==="
if [ "$(status_of python3 "$SCRIPT")" = "2" ]; then
  pass "no arguments exits 2"
else
  fail "no arguments did not exit 2"
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
sys.argv = [script, "--run", "--env-file", env_file]
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

TOKEN_ONLY_ENV="$WORK_DIR/token-only.env"
printf 'Bgg__Token=sentinel-token-value\n' >"$TOKEN_ONLY_ENV"
if [ "$(status_of python3 -c "$NO_NETWORK" "$SCRIPT" "$TOKEN_ONLY_ENV")" = "3" ]; then
  pass "an env file without a username exits 3 without network use"
else
  fail "an env file without a username did not exit 3 cleanly"
fi

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

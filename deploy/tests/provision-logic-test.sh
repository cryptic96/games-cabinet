#!/usr/bin/env bash
###
### Logic tests for provision.sh's shared functions and 20-accounts.sh's
### env-file renderer. Sources both files in library mode
### (CABINET_PROVISION_LIB_ONLY=1) so nothing here needs root, network
### access or any package to be installed.
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

# shellcheck source=deploy/provision.sh
CABINET_PROVISION_LIB_ONLY=1 source "${DEPLOY_DIR}/provision.sh"
# shellcheck source=deploy/provision.d/20-accounts.sh
CABINET_PROVISION_LIB_ONLY=1 source "${DEPLOY_DIR}/provision.d/20-accounts.sh"

set -uo pipefail

FAILURES=0
TESTS_RUN=0

pass() {
  TESTS_RUN=$((TESTS_RUN + 1))
  echo "PASS: $*"
}

failtest() {
  TESTS_RUN=$((TESTS_RUN + 1))
  FAILURES=$((FAILURES + 1))
  echo "FAIL: $*"
}

assert_eq() {
  local label="$1" expected="$2" actual="$3"
  if [[ "$expected" == "$actual" ]]; then
    pass "$label"
  else
    failtest "$label (expected [${expected}], got [${actual}])"
  fi
}

###
### Runs "$@" in a subshell so a provision_die-triggered exit only ends the
### subshell, not this test script, and reports whether it succeeded.
###
subshell_succeeds() {
  (
    "$@"
  ) >/dev/null 2>&1
}

assert_accepts() {
  local label="$1"
  shift
  if subshell_succeeds "$@"; then
    pass "$label"
  else
    failtest "$label (expected success, got failure)"
  fi
}

assert_refuses() {
  local label="$1"
  shift
  if subshell_succeeds "$@"; then
    failtest "$label (expected failure, got success)"
  else
    pass "$label"
  fi
}

# shellcheck disable=SC2034 # read via the nameref in _provision_parse_kv_file
TEST_ALLOWED_KEYS=(FOO BAR BAZ)

conf_ok="${WORK_DIR}/conf-ok"
cat >"$conf_ok" <<'EOF'
# a full-line comment
FOO=hello
BAR="hello world"
# another comment

BAZ=192.0.2.10
EOF

if (
  unset FOO BAR BAZ
  _provision_parse_kv_file "$conf_ok" TEST_ALLOWED_KEYS
  [[ "$FOO" == "hello" && "$BAR" == "hello world" && "$BAZ" == "192.0.2.10" ]]
); then
  pass "config parser: accepts allowed KEY=VALUE with quotes and comments"
else
  failtest "config parser: accepts allowed KEY=VALUE with quotes and comments"
fi

conf_unknown="${WORK_DIR}/conf-unknown"
echo "QUUX=nope" >"$conf_unknown"
assert_refuses "config parser: refuses an unknown key" \
  _provision_parse_kv_file "$conf_unknown" TEST_ALLOWED_KEYS

conf_backtick="${WORK_DIR}/conf-backtick"
# shellcheck disable=SC2016 # intentional: writing literal backtick text, not expanding it
echo 'FOO=`id`' >"$conf_backtick"
assert_refuses "config parser: refuses a backtick" \
  _provision_parse_kv_file "$conf_backtick" TEST_ALLOWED_KEYS

conf_subst="${WORK_DIR}/conf-subst"
# shellcheck disable=SC2016 # intentional: writing literal command-substitution text, not expanding it
echo 'FOO=$(id)' >"$conf_subst"
unset FOO
assert_refuses "config parser: refuses command substitution" \
  _provision_parse_kv_file "$conf_subst" TEST_ALLOWED_KEYS
assert_eq "config parser: command substitution attempt did not leak into FOO" \
  "unset" "${FOO:-unset}"

conf_malformed="${WORK_DIR}/conf-malformed"
echo "this is not key=value" >"$conf_malformed"
assert_refuses "config parser: refuses a malformed line" \
  _provision_parse_kv_file "$conf_malformed" TEST_ALLOWED_KEYS

conf_real="${WORK_DIR}/conf-real"
cat >"$conf_real" <<'EOF'
CABINET_TRAEFIK_IP=192.0.2.10
CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24,198.51.100.0/24
CABINET_GITHUB_REPO=example-owner/example-repo
EOF
chmod 600 "$conf_real"

if (
  unset CABINET_TRAEFIK_IP CABINET_ADMIN_SSH_SOURCES CABINET_GITHUB_REPO
  _provision_parse_kv_file "$conf_real" PROVISION_CONF_ALLOWED_KEYS
  [[ "$CABINET_TRAEFIK_IP" == "192.0.2.10" \
    && "$CABINET_ADMIN_SSH_SOURCES" == "192.0.2.0/24,198.51.100.0/24" \
    && "$CABINET_GITHUB_REPO" == "example-owner/example-repo" ]]
); then
  pass "config parser: accepts the three provision.conf keys"
else
  failtest "config parser: accepts the three provision.conf keys"
fi

conf_other_key="${WORK_DIR}/conf-other-key"
echo "LEDGER_TRAEFIK_IP=192.0.2.10" >"$conf_other_key"
assert_refuses "config parser: refuses a key outside the provision.conf allow-list" \
  _provision_parse_kv_file "$conf_other_key" PROVISION_CONF_ALLOWED_KEYS

assert_refuses "provision_load_conf: refuses a file not owned by root" \
  provision_load_conf "$conf_real"

###
### Stand-in for stat that reports a root-owned file with a chosen mode, so
### the mode checks can be exercised without being root.
###
STAT_FAKE_MODE=600
# shellcheck disable=SC2329
stat() {
  case "$*" in
    *%U*) echo root ;;
    *%a*) echo "$STAT_FAKE_MODE" ;;
    *) command stat "$@" ;;
  esac
}

STAT_FAKE_MODE=600
assert_accepts "provision_load_conf: accepts a root-owned 600 file" \
  provision_load_conf "$conf_real"
STAT_FAKE_MODE=664
assert_refuses "provision_load_conf: refuses a group-writable file" \
  provision_load_conf "$conf_real"
STAT_FAKE_MODE=646
assert_refuses "provision_load_conf: refuses a world-writable file" \
  provision_load_conf "$conf_real"
unset -f stat

assert_accepts "provision_validate_ipv4: accepts a documentation address" \
  provision_validate_ipv4 "192.0.2.10"
assert_refuses "provision_validate_ipv4: refuses an octet above 255" \
  provision_validate_ipv4 "192.0.2.256"
assert_refuses "provision_validate_ipv4: refuses too few octets" \
  provision_validate_ipv4 "192.0.2"
assert_refuses "provision_validate_ipv4: refuses a name" \
  provision_validate_ipv4 "example.com"
assert_refuses "provision_validate_ipv4: refuses the example placeholder" \
  provision_validate_ipv4 "CHANGE-ME"

assert_accepts "provision_validate_cidr: accepts a documentation range" \
  provision_validate_cidr "198.51.100.0/24"
assert_refuses "provision_validate_cidr: refuses a prefix above 32" \
  provision_validate_cidr "198.51.100.0/33"
assert_refuses "provision_validate_cidr: refuses a missing prefix" \
  provision_validate_cidr "198.51.100.0"

assert_accepts "provision_validate_cidr_list: accepts two ranges" \
  provision_validate_cidr_list "192.0.2.0/24,203.0.113.0/24"
assert_refuses "provision_validate_cidr_list: refuses one bad entry" \
  provision_validate_cidr_list "192.0.2.0/24,203.0.113.0"
assert_refuses "provision_validate_cidr_list: refuses whitespace around entries" \
  provision_validate_cidr_list "192.0.2.0/24, 203.0.113.0/24"
assert_refuses "provision_validate_cidr_list: refuses the example placeholder" \
  provision_validate_cidr_list "CHANGE-ME"

assert_accepts "provision_validate_repo_slug: accepts owner/name" \
  provision_validate_repo_slug "example-owner/example-repo"
assert_refuses "provision_validate_repo_slug: refuses a missing owner" \
  provision_validate_repo_slug "example-repo"
assert_refuses "provision_validate_repo_slug: refuses a path with three parts" \
  provision_validate_repo_slug "a/b/c"
assert_refuses "provision_validate_repo_slug: refuses a dot-only name" \
  provision_validate_repo_slug "owner/.."

assert_accepts "provision_validate_safe_value: accepts a plain value" \
  provision_validate_safe_value "192.0.2.0/24,198.51.100.0/24"
assert_refuses "provision_validate_safe_value: refuses a semicolon" \
  provision_validate_safe_value "a;b"
assert_refuses "provision_validate_safe_value: refuses an opening brace" \
  provision_validate_safe_value "a{b"
assert_refuses "provision_validate_safe_value: refuses a closing brace" \
  provision_validate_safe_value "a}b"
assert_refuses "provision_validate_safe_value: refuses a newline" \
  provision_validate_safe_value $'a\nb'

# shellcheck disable=SC2329
validate_with() {
  CABINET_TRAEFIK_IP="$1" CABINET_ADMIN_SSH_SOURCES="$2" CABINET_GITHUB_REPO="$3" \
    provision_validate_conf
}
assert_accepts "provision_validate_conf: accepts valid documentation values" \
  validate_with "192.0.2.10" "192.0.2.0/24" "example-owner/example-repo"
assert_refuses "provision_validate_conf: refuses a placeholder proxy address" \
  validate_with "CHANGE-ME" "192.0.2.0/24" "example-owner/example-repo"
assert_refuses "provision_validate_conf: refuses a placeholder SSH range" \
  validate_with "192.0.2.10" "CHANGE-ME" "example-owner/example-repo"
assert_refuses "provision_validate_conf: refuses a malformed repository" \
  validate_with "192.0.2.10" "192.0.2.0/24" "not a slug"

example_conf="${DEPLOY_DIR}/provision.conf.example"
# shellcheck disable=SC2329
validate_example() {
  unset CABINET_TRAEFIK_IP CABINET_ADMIN_SSH_SOURCES CABINET_GITHUB_REPO
  _provision_parse_kv_file "$example_conf" PROVISION_CONF_ALLOWED_KEYS
  provision_validate_conf
}
assert_refuses "provision_validate_conf: refuses the shipped example as it stands" \
  validate_example

if (
  unset CABINET_TRAEFIK_IP CABINET_ADMIN_SSH_SOURCES CABINET_GITHUB_REPO
  _provision_parse_kv_file "$example_conf" PROVISION_CONF_ALLOWED_KEYS
  provision_validate_repo_slug "$CABINET_GITHUB_REPO"
); then
  pass "provision.conf.example: ships a valid repository slug"
else
  failtest "provision.conf.example: ships a valid repository slug"
fi

if provision_version_ge "2.101.0" "2.49.0"; then
  pass "provision_version_ge: 2.101.0 >= 2.49.0"
else
  failtest "provision_version_ge: 2.101.0 >= 2.49.0"
fi

if provision_version_ge "2.49.0" "2.49.0"; then
  pass "provision_version_ge: 2.49.0 >= 2.49.0 (equal)"
else
  failtest "provision_version_ge: 2.49.0 >= 2.49.0 (equal)"
fi

if provision_version_ge "2.10.0" "2.49.0"; then
  failtest "provision_version_ge: 2.10.0 is not >= 2.49.0"
else
  pass "provision_version_ge: 2.10.0 is not >= 2.49.0"
fi

one_active_key=$'pub:-:4096:1:AAAAAAAAAAAAAAAA:1000000000:::-:::scESC::::::23::0:\nfpr:::::::::1111111111111111111111111111111111111111:\nuid:-::::1000000000::HASH::Test Key <test@example.com>::::::::::0:\nsub:-:4096:1:BBBBBBBBBBBBBBBB:1000000000::::::e::::::23:\nfpr:::::::::2222222222222222222222222222222222222222:'

fp="$(printf '%s\n' "$one_active_key" | provision_key_fingerprint)"
assert_eq "provision_key_fingerprint: returns the fpr of a single active primary key" \
  "1111111111111111111111111111111111111111" "$fp"

zero_active_keys=$'pub:e:4096:1:AAAAAAAAAAAAAAAA:1000000000:::-:::scESC::::::23::0:\nfpr:::::::::1111111111111111111111111111111111111111:\nuid:e::::1000000000::HASH::Test Key <test@example.com>::::::::::0:'

if printf '%s\n' "$zero_active_keys" | provision_key_fingerprint >/dev/null 2>&1; then
  failtest "provision_key_fingerprint: refuses zero active primary keys"
else
  pass "provision_key_fingerprint: refuses zero active primary keys (all expired)"
fi

several_active_keys=$'pub:-:4096:1:AAAAAAAAAAAAAAAA:1000000000:::-:::scESC::::::23::0:\nfpr:::::::::1111111111111111111111111111111111111111:\nuid:-::::1000000000::HASH::Test Key <test@example.com>::::::::::0:\npub:-:4096:1:CCCCCCCCCCCCCCCC:1000000000:::-:::scESC::::::23::0:\nfpr:::::::::3333333333333333333333333333333333333333:\nuid:-::::1000000000::HASH::Second Key <second@example.com>::::::::::0:'

if printf '%s\n' "$several_active_keys" | provision_key_fingerprint >/dev/null 2>&1; then
  failtest "provision_key_fingerprint: refuses several active primary keys"
else
  pass "provision_key_fingerprint: refuses several active primary keys"
fi

rendered="$(accounts_render_cabinet_env "192.0.2.10")"
expected_rendered=$'ASPNETCORE_ENVIRONMENT=Production\nReverseProxy__KnownProxies__0=192.0.2.10'
assert_eq "accounts_render_cabinet_env: renders exactly the two expected lines" \
  "$expected_rendered" "$rendered"

if grep -Eq '^[[:space:]]+ca-certificates .* fonts-dejavu-core( |\\)' "${DEPLOY_DIR}/provision.d/10-packages.sh"; then
  pass "10-packages.sh: the base packages include the font the review sheet draws with"
else
  failtest "10-packages.sh: the base packages include the font the review sheet draws with"
fi

host_calls="$(host_guard_calls)"
assert_eq "host guard: no systemctl or pkexec call was recorded" "" "$host_calls"

echo "----"
echo "${TESTS_RUN} test(s) run, ${FAILURES} failure(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  exit 1
fi
exit 0

#!/usr/bin/env bash
###
### Logic tests for provision.sh's template rendering, the rendered firewall
### ruleset and the reverse proxy example. Sources everything in library mode
### (CABINET_PROVISION_LIB_ONLY=1) so nothing here needs root, network access
### or any package to be installed. Placeholder values only.
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
# shellcheck source=deploy/provision.d/10-packages.sh
CABINET_PROVISION_LIB_ONLY=1 source "${DEPLOY_DIR}/provision.d/10-packages.sh"
# shellcheck source=deploy/provision.d/50-firewall.sh
CABINET_PROVISION_LIB_ONLY=1 source "${DEPLOY_DIR}/provision.d/50-firewall.sh"

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
### Succeeds when the text on stdin matches the extended regex "$1".
###
assert_matches() {
  local label="$1" pattern="$2" text="$3"
  if grep -qE -- "$pattern" <<<"$text"; then
    pass "$label"
  else
    failtest "$label"
  fi
}

assert_lacks() {
  local label="$1" pattern="$2" text="$3"
  if grep -qE -- "$pattern" <<<"$text"; then
    failtest "$label"
  else
    pass "$label"
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

template_ok="${WORK_DIR}/template-ok"
output_ok="${WORK_DIR}/output-ok"
printf 'traefik=@CABINET_TRAEFIK_IP@\nssh=@CABINET_ADMIN_SSH_SOURCES@\n' >"$template_ok"

if subshell_succeeds provision_render_template "$template_ok" "$output_ok" \
  "CABINET_TRAEFIK_IP=192.0.2.10" "CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24,198.51.100.0/24"; then
  provision_render_template "$template_ok" "$output_ok" \
    "CABINET_TRAEFIK_IP=192.0.2.10" "CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24,198.51.100.0/24"
  assert_eq "provision_render_template: replaces both tokens with valid values" \
    $'traefik=192.0.2.10\nssh=192.0.2.0/24,198.51.100.0/24' "$(cat "$output_ok")"
else
  failtest "provision_render_template: accepts a valid address and CIDR list"
fi

rm -f "$output_ok"
if subshell_succeeds provision_render_template "$template_ok" "$output_ok" \
  "CABINET_TRAEFIK_IP=999.0.2.10" "CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24"; then
  failtest "provision_render_template: refuses an invalid IPv4 address"
else
  pass "provision_render_template: refuses an invalid IPv4 address"
fi

if subshell_succeeds provision_render_template "$template_ok" "$output_ok" \
  "CABINET_TRAEFIK_IP=192.0.2.10" "CABINET_ADMIN_SSH_SOURCES=192.0.2.0/99"; then
  failtest "provision_render_template: refuses an invalid CIDR"
else
  pass "provision_render_template: refuses an invalid CIDR"
fi

if subshell_succeeds provision_render_template "$template_ok" "$output_ok" \
  "CABINET_TRAEFIK_IP=192.0.2.10" "CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24; drop table"; then
  failtest "provision_render_template: refuses a value containing a semicolon"
else
  pass "provision_render_template: refuses a value containing a semicolon"
fi

if subshell_succeeds provision_render_template "$template_ok" "$output_ok" \
  "CABINET_TRAEFIK_IP=192.0.2.10" "CABINET_ADMIN_SSH_SOURCES={192.0.2.0/24}"; then
  failtest "provision_render_template: refuses a value containing a brace"
else
  pass "provision_render_template: refuses a value containing a brace"
fi

if subshell_succeeds provision_render_template "$template_ok" "$output_ok" \
  "CABINET_TRAEFIK_IP=192.0.2.10" $'CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24\nssh dport 2222 accept'; then
  failtest "provision_render_template: refuses a value containing a newline"
else
  pass "provision_render_template: refuses a value containing a newline"
fi

if [[ -e "$output_ok" ]]; then
  failtest "provision_render_template: writes no output when a value is refused"
else
  pass "provision_render_template: writes no output when a value is refused"
fi

template_missing="${WORK_DIR}/template-missing"
printf 'traefik=@CABINET_TRAEFIK_IP@\nother=@CABINET_UNKNOWN_TOKEN@\n' >"$template_missing"
if subshell_succeeds provision_render_template "$template_missing" "$output_ok" \
  "CABINET_TRAEFIK_IP=192.0.2.10"; then
  failtest "provision_render_template: fails when a token is left unreplaced"
else
  pass "provision_render_template: fails when a token is left unreplaced"
fi

nft_output="${WORK_DIR}/rendered.nft"
provision_render_template "${DEPLOY_DIR}/nftables/cabinet.nft.in" "$nft_output" \
  "CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24,198.51.100.0/24" "CABINET_TRAEFIK_IP=192.0.2.10"
nft_content="$(cat "$nft_output")"

assert_eq "rendered nftables: input and forward chains both have policy drop" \
  "2" "$(grep -c 'policy drop' <<<"$nft_content")"
assert_matches "rendered nftables: output chain accepts by default" \
  'hook output priority 0; policy accept;' "$nft_content"
assert_matches "rendered nftables: table is cabinet_filter" \
  '^table inet cabinet_filter \{' "$nft_content"
assert_matches "rendered nftables: admits SSH only from the configured admin ranges" \
  'tcp dport 22 ip saddr \{ 192\.0\.2\.0/24,198\.51\.100\.0/24 \} accept' "$nft_content"
assert_matches "rendered nftables: admits the app port only from the Traefik address" \
  'tcp dport 5080 ip saddr 192\.0\.2\.10 accept' "$nft_content"
assert_eq "rendered nftables: exactly two tcp dport rules exist" \
  "2" "$(grep -c 'tcp dport' <<<"$nft_content")"
assert_lacks "rendered nftables: no unreplaced token remains" \
  '@[A-Z][A-Z0-9_]*@' "$nft_content"
assert_lacks "rendered nftables: no rule mentions the loopback ops port" \
  '5081' "$nft_content"

traefik_example="$(cat "${DEPLOY_DIR}/traefik/cabinet.yml.example")"
assert_matches "traefik example: carries an ipAllowList middleware" \
  'ipAllowList' "$traefik_example"
assert_matches "traefik example: defines the cabinet router" \
  '^    cabinet:$' "$traefik_example"
assert_matches "traefik example: defines the lan-only middleware" \
  '^    cabinet-lan-only:$' "$traefik_example"
assert_matches "traefik example: defines the security headers middleware" \
  '^    cabinet-security-headers:$' "$traefik_example"
assert_eq "traefik example: has exactly one router" \
  "1" "$(grep -cE '^      rule:' <<<"$traefik_example")"
assert_lacks "traefik example: has no path prefix rule" \
  'PathPrefix' "$traefik_example"

addresses="$(grep -oE '[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+' <<<"$traefik_example" || true)"
foreign_addresses="$(grep -vE '^(192\.0\.2|198\.51\.100|203\.0\.113)\.' <<<"$addresses" || true)"
assert_eq "traefik example: every address is an RFC 5737 documentation address" \
  "" "$foreign_addresses"

hostnames="$(grep -oE '[A-Za-z0-9-]+(\.[A-Za-z0-9-]+)+' <<<"$traefik_example" \
  | grep -vE '^[0-9]+\.[0-9]+\.[0-9]+\.[0-9]+$' || true)"
foreign_hostnames="$(grep -vE '(^|\.)example\.com$' <<<"$hostnames" || true)"
assert_eq "traefik example: every hostname is under example.com" \
  "" "$foreign_hostnames"

# shellcheck disable=SC2016
assert_matches "traefik example: routes the placeholder hostname" \
  'Host\(`cabinet\.example\.com`\)' "$traefik_example"

dpkg-query() {
  printf '%s\t%s\t%s\n' \
    "git" "" "install ok installed" \
    "mail-agent-one" "default-mta, mail-transport-agent" "install ok installed" \
    "mail-agent-two" "mail-transport-agent" "deinstall ok config-files" \
    "curl" "" "install ok installed"
}
assert_eq "installed_mta_packages: lists only installed mail transport agents" \
  "mail-agent-one" "$(installed_mta_packages)"
unset -f dpkg-query

host_calls="$(host_guard_calls)"
assert_eq "host guard: no systemctl or pkexec call was recorded" "" "$host_calls"

echo "----"
echo "${TESTS_RUN} test(s) run, ${FAILURES} failure(s)"
if [[ "$FAILURES" -gt 0 ]]; then
  exit 1
fi
exit 0

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

egress_resolvers="192.0.2.53"
egress_blocked="10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,100.64.0.0/10,169.254.0.0/16"
egress_blocked6="fc00::/7,fe80::/10,2001:db8:1::/64"

nft_output="${WORK_DIR}/rendered.nft"
provision_render_template "${DEPLOY_DIR}/nftables/cabinet.nft.in" "$nft_output" \
  "CABINET_ADMIN_SSH_SOURCES=192.0.2.0/24,198.51.100.0/24" "CABINET_TRAEFIK_IP=192.0.2.10" \
  "CABINET_EGRESS_RESOLVERS=${egress_resolvers}" \
  "CABINET_EGRESS_BLOCKED_RANGES=${egress_blocked}" \
  "CABINET_EGRESS_BLOCKED_RANGES6=${egress_blocked6}"
nft_content="$(cat "$nft_output")"
input_chain="$(sed -n '/chain input/,/^  }/p' <<<"$nft_content")"
output_chain="$(sed -n '/chain output/,/^  }/p' <<<"$nft_content")"

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
assert_matches "rendered nftables: admits the owner port only from the Traefik address" \
  'tcp dport 5082 ip saddr 192\.0\.2\.10 accept' "$nft_content"
assert_eq "rendered nftables: exactly three tcp dport rules exist in the input chain" \
  "3" "$(grep -c 'tcp dport' <<<"$input_chain")"
assert_lacks "rendered nftables: no unreplaced token remains" \
  '@[A-Z][A-Z0-9_]*@' "$nft_content"
assert_lacks "rendered nftables: no rule mentions the loopback ops port" \
  '5081' "$nft_content"

assert_matches "rendered nftables: output chain accepts loopback" \
  'oif lo accept' "$output_chain"
assert_matches "rendered nftables: output chain accepts established and related traffic" \
  'ct state established,related accept' "$output_chain"
assert_matches "rendered nftables: output chain accepts ICMPv6" \
  'meta l4proto ipv6-icmp accept' "$output_chain"
assert_matches "rendered nftables: output chain allows UDP DNS to the configured resolver" \
  'ip daddr \{ 192\.0\.2\.53 \} udp dport 53 accept' "$output_chain"
assert_matches "rendered nftables: output chain allows TCP DNS to the configured resolver" \
  'ip daddr \{ 192\.0\.2\.53 \} tcp dport 53 accept' "$output_chain"
assert_matches "rendered nftables: output chain rejects new connections to the blocked IPv4 ranges" \
  'ip daddr \{ 10\.0\.0\.0/8,172\.16\.0\.0/12,192\.168\.0\.0/16,100\.64\.0\.0/10,169\.254\.0\.0/16 \} counter reject with icmpx type admin-prohibited' "$output_chain"
assert_matches "rendered nftables: output chain rejects new connections to the blocked IPv6 ranges" \
  'ip6 daddr \{ fc00::/7,fe80::/10,2001:db8:1::/64 \} counter reject with icmpx type admin-prohibited' "$output_chain"
assert_lacks "rendered nftables: output chain drops nothing" \
  '\bdrop\b' "$output_chain"

###
### Prints the line number in the output chain of the first line containing
### the fixed text "$1".
###
line_of() {
  grep -nF -- "$1" <<<"$output_chain" | head -n1 | cut -d: -f1
}
rule_order="$(printf '%s\n' \
  "$(line_of 'oif lo accept')" \
  "$(line_of 'ct state established,related accept')" \
  "$(line_of 'meta l4proto ipv6-icmp accept')" \
  "$(line_of 'udp dport 53 accept')" \
  "$(line_of 'tcp dport 53 accept')" \
  "$(line_of 'ip daddr { 10.')" \
  "$(line_of 'ip6 daddr {')")"
assert_eq "rendered nftables: output rules run loopback, replies, ICMPv6, DNS, then the two blocks" \
  "$(sort -n <<<"$rule_order" | tr '\n' ' ')" "$(tr '\n' ' ' <<<"$rule_order")"
assert_eq "rendered nftables: every output rule was found" "7" "$(grep -c '^[0-9]' <<<"$rule_order")"

egress_template="${WORK_DIR}/egress-template"
egress_out="${WORK_DIR}/egress-out"
printf 'r=@CABINET_EGRESS_RESOLVERS@\nb=@CABINET_EGRESS_BLOCKED_RANGES@\nb6=@CABINET_EGRESS_BLOCKED_RANGES6@\n' >"$egress_template"

###
### Renders the egress template with the resolver list, IPv4 range list and
### IPv6 range list given and succeeds when provision_render_template accepts
### them.
###
egress_render_accepts() {
  rm -f "$egress_out"
  subshell_succeeds provision_render_template "$egress_template" "$egress_out" \
    "CABINET_EGRESS_RESOLVERS=$1" "CABINET_EGRESS_BLOCKED_RANGES=$2" "CABINET_EGRESS_BLOCKED_RANGES6=$3"
}

if egress_render_accepts "192.0.2.53,198.51.100.53" "10.0.0.0/8" "fc00::/7"; then
  pass "egress tokens: accept two resolvers and valid ranges"
else
  failtest "egress tokens: accept two resolvers and valid ranges"
fi

for refused in \
  "a resolver given as a CIDR|192.0.2.0/24|10.0.0.0/8|fc00::/7" \
  "an empty resolver list||10.0.0.0/8|fc00::/7" \
  "a resolver name|dns.example.com|10.0.0.0/8|fc00::/7" \
  "a malformed IPv4 range|192.0.2.53|192.0.2.0/33|fc00::/7" \
  "an IPv4 range without a prefix|192.0.2.53|10.0.0.0|fc00::/7" \
  "an IPv6 range without a prefix|192.0.2.53|10.0.0.0/8|fc00::" \
  "an IPv6 prefix above 128|192.0.2.53|10.0.0.0/8|fc00::/129" \
  "an IPv6 range with two shortenings|192.0.2.53|10.0.0.0/8|2001:db8::1::2/64" \
  "an IPv6 range with a group of five digits|192.0.2.53|10.0.0.0/8|fc000::/16" \
  "an IPv4 range given as IPv6|192.0.2.53|10.0.0.0/8|10.0.0.0/8" \
  "a semicolon in the resolver list|192.0.2.53; drop|10.0.0.0/8|fc00::/7" \
  "a brace in the IPv4 range list|192.0.2.53|{10.0.0.0/8}|fc00::/7" \
  "a brace in the IPv6 range list|192.0.2.53|10.0.0.0/8|{fc00::/7}"; do
  IFS='|' read -r label resolvers ranges ranges6 <<<"$refused"
  if egress_render_accepts "$resolvers" "$ranges" "$ranges6"; then
    failtest "egress tokens: refuse ${label}"
  else
    pass "egress tokens: refuse ${label}"
  fi
done

if [[ -e "$egress_out" ]]; then
  failtest "egress tokens: write no output when a value is refused"
else
  pass "egress tokens: write no output when a value is refused"
fi

traefik_example="$(cat "${DEPLOY_DIR}/traefik/cabinet.yml.example")"
assert_matches "traefik example: carries an ipAllowList middleware" \
  'ipAllowList' "$traefik_example"
assert_matches "traefik example: defines the cabinet router" \
  '^    cabinet:$' "$traefik_example"
assert_matches "traefik example: defines the lan-only middleware" \
  '^    cabinet-lan-only:$' "$traefik_example"
assert_matches "traefik example: defines the security headers middleware" \
  '^    cabinet-security-headers:$' "$traefik_example"
assert_eq "traefik example: has exactly two routers" \
  "2" "$(grep -cE '^      rule:' <<<"$traefik_example")"
assert_eq "traefik example: defines the owner router and the owner service" \
  "2" "$(grep -cE '^    cabinet-owner:$' <<<"$traefik_example")"
owner_router="$(sed -n '/^  routers:/,/^  services:/p' <<<"$traefik_example" | sed -n '/^    cabinet-owner:$/,$p')"
# shellcheck disable=SC2016
assert_matches "traefik example: the owner router uses its own placeholder hostname" \
  'Host\(`owner\.example\.com`\)' "$owner_router"
assert_matches "traefik example: the owner router keeps the lan-only allow-list" \
  '- cabinet-lan-only' "$owner_router"
assert_matches "traefik example: the owner router carries the security headers" \
  '- cabinet-security-headers' "$owner_router"
assert_matches "traefik example: the owner router points at the owner service" \
  'service: cabinet-owner' "$owner_router"
assert_matches "traefik example: the owner router uses the certificate resolver" \
  'certResolver: letsencrypt' "$owner_router"
assert_matches "traefik example: the owner service points at port 5082" \
  'url: "http://192\.0\.2\.20:5082"' "$traefik_example"
assert_matches "traefik example: says the owner router keeps its allow-list permanently" \
  'permanently' "$traefik_example"
assert_matches "traefik example: warns about notAppendXForwardedFor" \
  'notAppendXForwardedFor' "$traefik_example"
assert_matches "traefik example: warns about client networks in trustedIPs" \
  'trustedIPs' "$traefik_example"
assert_lacks "traefik example: has no path prefix rule" \
  'PathPrefix' "$traefik_example"

routers_section="$(sed -n '/^  routers:/,/^  services:/p' <<<"$traefik_example")"
public_router="$(sed -n '/^    cabinet:$/,/^    cabinet-owner:$/p' <<<"$routers_section")"
public_middlewares="$(sed -n '/^      middlewares:/,/^      service:/p' <<<"$public_router" | grep -oE '^ +- [a-z-]+$' | sed -E 's/^ +- //' | tr '\n' ' ')"
owner_middlewares="$(sed -n '/^      middlewares:/,/^      service:/p' <<<"$owner_router" | grep -oE '^ +- [a-z-]+$' | sed -E 's/^ +- //' | tr '\n' ' ')"
assert_eq "traefik example: the public router runs headers, allow-list, rate limit, then in-flight limit in that order" \
  "cabinet-security-headers cabinet-lan-only cabinet-rate-limit cabinet-in-flight " "$public_middlewares"
assert_eq "traefik example: the owner router lists only the allow-list and the security headers" \
  "cabinet-lan-only cabinet-security-headers " "$owner_middlewares"
assert_lacks "traefik example: the owner router has no rate limit" \
  'cabinet-rate-limit' "$owner_router"
assert_lacks "traefik example: the owner router has no in-flight limit" \
  'cabinet-in-flight' "$owner_router"

###
### Prints the middleware definition named "$1" from the Traefik example: the
### lines from its four-space key to the next four-space key or the routers.
###
traefik_middleware_block() {
  sed -n '/^  middlewares:/,/^  routers:/p' <<<"$traefik_example" \
    | sed -n "/^    $1:\$/,/^    [a-z#]/p" | sed '1!{/^    [a-z#]/d;}'
}
rate_limit_block="$(traefik_middleware_block cabinet-rate-limit)"
in_flight_block="$(traefik_middleware_block cabinet-in-flight)"
for key in 'rateLimit:' 'average:' 'period:' 'burst:' 'sourceCriterion:' 'ipStrategy:' 'ipv6Subnet: 64'; do
  assert_matches "traefik example: the rate limit sets ${key}" "$key" "$rate_limit_block"
done
for decided in '^ +average: 60$' '^ +period: 1s$' '^ +burst: 800$'; do
  assert_matches "traefik example: the rate limit carries the decided value ${decided}" "$decided" "$rate_limit_block"
done
assert_matches "traefik example: the in-flight limit carries the decided amount" '^ +amount: 200$' "$in_flight_block"
for key in 'inFlightReq:' 'amount:' 'sourceCriterion:' 'ipStrategy:' 'ipv6Subnet: 64'; do
  assert_matches "traefik example: the in-flight limit sets ${key}" "$key" "$in_flight_block"
done
headers_block="$(traefik_middleware_block cabinet-security-headers)"
assert_matches "traefik example: the proxy sets HSTS for one year" \
  '^ +stsSeconds: 31536000$' "$headers_block"
for key in stsPreload stsIncludeSubdomains contentTypeNosniff frameDeny referrerPolicy contentSecurityPolicy; do
  assert_lacks "traefik example: the proxy's headers middleware leaves ${key} to the application" \
    "$key" "$headers_block"
done
assert_eq "traefik example: the headers middleware sets exactly one key" \
  "1" "$(grep -cE '^ {8}[A-Za-z]+:' <<<"$headers_block")"
assert_eq "traefik example: both routers still list the security headers" \
  "2" "$(grep -cE '^ +- cabinet-security-headers$' <<<"$routers_section")"
assert_lacks "traefik example: no source criterion counts by hop depth" \
  '^ *depth:' "$traefik_example"
assert_lacks "traefik example: no errors middleware styles the refusals" \
  '^ *errors:' "$traefik_example"

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

env_example="$(cat "${DEPLOY_DIR}/cabinet.env.example")"
assert_matches "env example: documents the owner endpoint, commented out" \
  '^# *Kestrel__Endpoints__Owner__Url=http://0\.0\.0\.0:5082$' "$env_example"
assert_matches "env example: documents the first owner range, commented out" \
  '^# *Owner__AllowedRanges__0=192\.0\.2\.0/24$' "$env_example"
assert_matches "env example: documents the second owner range, commented out" \
  '^# *Owner__AllowedRanges__1=198\.51\.100\.0/24$' "$env_example"
assert_matches "env example: lists the allowed hosts with the loopback entries, quoted" \
  '^AllowedHosts="cabinet\.example\.com;owner\.example\.com;127\.0\.0\.1;localhost"$' "$env_example"
assert_eq "env example: exactly one AllowedHosts line is active" \
  "1" "$(grep -cE '^AllowedHosts=' <<<"$env_example")"
assert_matches "env example: explains that loopback keeps the health check working" \
  '^#.*health check' "$env_example"
assert_lacks "env example: no owner setting is active" \
  '^(Kestrel__Endpoints__Owner|Owner__)' "$env_example"
assert_matches "env example: says the known proxy must be the Traefik address" \
  'Traefik address' "$env_example"

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

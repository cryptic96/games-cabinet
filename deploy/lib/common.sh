#!/usr/bin/env bash
# Shared logging, safe configuration loading, version comparison and the
# provisioning comparison (installed scripts, libraries and units against the
# active release) used by the deploy tooling and the selfcheck. Sourced, never
# executed directly.

if [ -n "${CABINET_COMMON_SH_LOADED:-}" ]; then
  return 0
fi
CABINET_COMMON_SH_LOADED=1

CABINET_CONF_ALLOWED_KEYS=(
  CABINET_GITHUB_REPO
  CABINET_SIGNER_WORKFLOW
  CABINET_KEEP_RELEASES
  CABINET_HEALTH_TIMEOUT_SECONDS
  CABINET_OPS_URL
)

# shellcheck disable=SC2034
CABINET_STRICT_SEMVER_TAG_REGEX='^v(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'
# shellcheck disable=SC2034
CABINET_STRICT_SEMVER_REGEX='^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$'

# Writes a timestamped line to stderr. journald captures stderr for services
# invoked by systemd; interactive runs simply see it on the terminal.
cabinet_log() {
  printf '%s %s\n' "$(date -u '+%Y-%m-%dT%H:%M:%SZ')" "$*" >&2
}

# Logs an error-prefixed message and exits non-zero.
cabinet_die() {
  cabinet_log "ERROR: $*"
  exit 1
}

# Loads KEY=VALUE pairs from a configuration file into global shell variables,
# without ever sourcing the file. Refuses a file that is not owned by the
# expected privileged user, or that is group- or world-writable. Only keys on
# the fixed allow-list are read; every other line is silently ignored. Values
# may be optionally wrapped in single or double quotes, which are stripped.
#
# Under a relocated test root (CABINET_DEPLOY_ROOT set) the expected owner is
# the current effective user rather than root, since the test root stands in
# for the privileged installation root and is never itself run as root.
cabinet_load_conf() {
  local conf_file="$1"

  if [ ! -f "$conf_file" ]; then
    cabinet_die "configuration file not found: $conf_file"
  fi

  local expected_uid=0
  if [ -n "${CABINET_DEPLOY_ROOT:-}" ]; then
    expected_uid="$(id -u)"
  fi

  local owner_uid
  owner_uid="$(stat -c '%u' "$conf_file")"
  if [ "$owner_uid" != "$expected_uid" ]; then
    cabinet_die "configuration file $conf_file has an unexpected owner"
  fi

  local perm group_digit other_digit
  perm="$(stat -c '%a' "$conf_file")"
  group_digit="${perm: -2:1}"
  other_digit="${perm: -1:1}"
  if (( (10#$group_digit & 2) != 0 )) || (( 10#$other_digit != 0 )); then
    cabinet_die "configuration file $conf_file must not be group- or world-writable or readable by others"
  fi

  local line key value allowed candidate
  while IFS= read -r line || [ -n "$line" ]; do
    [[ "$line" =~ ^([A-Z][A-Z0-9_]*)=(.*)$ ]] || continue
    key="${BASH_REMATCH[1]}"
    value="${BASH_REMATCH[2]}"

    allowed=0
    for candidate in "${CABINET_CONF_ALLOWED_KEYS[@]}"; do
      if [ "$candidate" = "$key" ]; then
        allowed=1
        break
      fi
    done
    [ "$allowed" -eq 1 ] || continue

    if [[ "$value" =~ ^\"(.*)\"$ ]]; then
      value="${BASH_REMATCH[1]}"
    elif [[ "$value" =~ ^\'(.*)\'$ ]]; then
      value="${BASH_REMATCH[1]}"
    fi

    printf -v "$key" '%s' "$value"
  done < "$conf_file"
}

# Exits with an error unless the variable named NAME holds a positive decimal
# integer of at most six digits. Numeric settings must pass this before they
# are used anywhere, because bash evaluates the value of a variable named in
# an arithmetic expression as an expression itself, which can run commands.
cabinet_require_uint() {
  local name="$1"
  local value="${!name-}"

  [[ "$value" =~ ^[1-9][0-9]{0,5}$ ]] || cabinet_die "${name} must be a positive whole number"
}

# Compares two MAJOR.MINOR.PATCH version strings numerically. Succeeds
# (returns 0) when A is strictly greater than B.
cabinet_semver_gt() {
  local a="$1" b="$2"
  local a_major a_minor a_patch b_major b_minor b_patch

  IFS='.' read -r a_major a_minor a_patch <<< "$a"
  IFS='.' read -r b_major b_minor b_patch <<< "$b"

  if (( 10#$a_major != 10#$b_major )); then
    (( 10#$a_major > 10#$b_major ))
    return
  fi
  if (( 10#$a_minor != 10#$b_minor )); then
    (( 10#$a_minor > 10#$b_minor ))
    return
  fi
  (( 10#$a_patch > 10#$b_patch ))
}

# Succeeds (returns 0) when the installed file INSTALLED is not a regular
# file or differs byte for byte from SOURCE. Only reads both files.
cabinet_provisioning_file_differs() {
  local source="$1" installed="$2"

  if [ -f "$installed" ] && cmp -s "$source" "$installed"; then
    return 1
  fi
  return 0
}

# Compares, byte for byte, every file provisioning installs verbatim with the
# copy shipped in the active release, and prints the installed path of each
# file that is missing or differs, one per line, on stdout.
#
# RELEASE_DEPLOY_DIR is the release's own deploy directory. INSTALL_ROOT is
# the prefix the installed files live under; it is empty on a real host and
# only a relocated root in offline tests. The mapping mirrors
# deploy/provision.d/40-services.sh and must be kept in step with it:
# bin/* to usr/local/sbin, lib/*.sh to usr/local/lib/cabinet, and
# systemd/*.service and systemd/*.timer to etc/systemd/system.
#
# Rendered files (deploy.conf, nftables.conf, the env files) are deliberately
# not compared because provisioning does not install them verbatim. Installed
# files the release no longer ships are not reported, because provisioning
# does not remove them either.
#
# Returns 0 when everything matches, 1 when at least one file drifted and 2
# when RELEASE_DEPLOY_DIR is not a directory, so there is nothing to compare
# against. It only reads: it never writes, copies, sources or executes any
# file, and it never exits the caller.
cabinet_provisioning_drift() {
  local release_deploy="$1" install_root="$2"
  local source installed drifted=0

  [ -d "$release_deploy" ] || return 2

  for source in "${release_deploy}"/bin/*; do
    [ -f "$source" ] || continue
    installed="${install_root}/usr/local/sbin/$(basename "$source")"
    if cabinet_provisioning_file_differs "$source" "$installed"; then
      printf '%s\n' "$installed"
      drifted=1
    fi
  done
  for source in "${release_deploy}"/lib/*.sh; do
    [ -f "$source" ] || continue
    installed="${install_root}/usr/local/lib/cabinet/$(basename "$source")"
    if cabinet_provisioning_file_differs "$source" "$installed"; then
      printf '%s\n' "$installed"
      drifted=1
    fi
  done
  for source in "${release_deploy}"/systemd/*.service "${release_deploy}"/systemd/*.timer; do
    [ -f "$source" ] || continue
    installed="${install_root}/etc/systemd/system/$(basename "$source")"
    if cabinet_provisioning_file_differs "$source" "$installed"; then
      printf '%s\n' "$installed"
      drifted=1
    fi
  done

  [ "$drifted" -eq 0 ] || return 1
  return 0
}

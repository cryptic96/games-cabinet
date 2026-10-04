#!/usr/bin/env bash
###
### Re-runnable, read-only check that every pin in deploy/versions.env still
### matches its real source: the runtime package in the Ubuntu 24.04 package
### indexes, the GitHub CLI signing key fingerprint, and a GitHub CLI new
### enough for attestation verification. Named *-network-test.sh so a lint
### harness that skips network tests by name can find it, and it only runs
### when CABINET_LINT_NETWORK=1 is set.
###
### Nothing here installs a package, writes outside a temp directory, or
### runs as anything other than the invoking user.
###
set -euo pipefail

if [[ "${CABINET_LINT_NETWORK:-0}" != "1" ]]; then
  echo "SKIP: set CABINET_LINT_NETWORK=1 to check the pins against their real sources"
  exit 0
fi

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VERSIONS_ENV="${SCRIPT_DIR}/../versions.env"
CURL_MAX_TIME=30
WORK_DIR="$(mktemp -d)"
trap 'rm -rf "$WORK_DIR"' EXIT

ALLOWED_KEYS=(
  DOTNET_RUNTIME_PACKAGE
  GH_CLI_KEY_URL
  GH_CLI_KEY_FINGERPRINT
  GH_CLI_MIN_VERSION
)

FAILURES=0

fail() {
  echo "FAIL: $*" >&2
  FAILURES=$((FAILURES + 1))
}

pass() {
  echo "PASS: $*"
}

###
### Loads KEY=VALUE lines from versions.env into shell variables, refusing
### any key not on the allow-list and never evaluating a value.
###
load_versions_env() {
  local file="$1" line key value is_allowed k
  [[ -f "$file" ]] || { echo "versions.env not found at $file" >&2; exit 1; }
  while IFS= read -r line || [[ -n "$line" ]]; do
    [[ -z "$line" ]] && continue
    [[ "$line" =~ ^[[:space:]]*# ]] && continue
    if [[ "$line" =~ ^([A-Z][A-Z0-9_]*)=(.*)$ ]]; then
      key="${BASH_REMATCH[1]}"
      value="${BASH_REMATCH[2]}"
      value="${value%\"}"
      value="${value#\"}"
      is_allowed=0
      for k in "${ALLOWED_KEYS[@]}"; do
        if [[ "$k" == "$key" ]]; then
          is_allowed=1
          break
        fi
      done
      if [[ "$is_allowed" -ne 1 ]]; then
        echo "versions.env: unknown key '${key}'" >&2
        exit 1
      fi
      printf -v "$key" '%s' "$value"
    else
      echo "versions.env: malformed line: ${line}" >&2
      exit 1
    fi
  done <"$file"
}

###
### Extracts the fingerprint of the single non-expired, non-revoked primary
### key in a downloaded key file (armored or already dearmored), through a
### throwaway GNUPGHOME. Fails if there is not exactly one such key.
###
key_fingerprint() {
  local key_file="$1" gnupg_home raw line validity pending fp="" count=0
  gnupg_home="$(mktemp -d)"
  chmod 700 "$gnupg_home"
  raw="$(GNUPGHOME="$gnupg_home" gpg --batch --with-colons --show-keys "$key_file" 2>/dev/null || true)"
  rm -rf "$gnupg_home"

  pending=0
  while IFS= read -r line; do
    case "$line" in
      pub:*)
        validity="$(cut -d: -f2 <<<"$line")"
        if [[ "$validity" != "e" && "$validity" != "r" ]]; then
          pending=1
        else
          pending=0
        fi
        ;;
      fpr:*)
        if [[ "$pending" -eq 1 ]]; then
          count=$((count + 1))
          fp="$(cut -d: -f10 <<<"$line")"
        fi
        pending=0
        ;;
    esac
  done <<<"$raw"

  if [[ "$count" -ne 1 ]]; then
    echo "expected exactly one active primary key in ${key_file}, found ${count}" >&2
    return 1
  fi
  printf '%s' "$fp"
}

###
### Downloads one Ubuntu 24.04 package index (pocket "$1", component "$2")
### to a temp file. Succeeds when it lists the pinned runtime package, exits
### 1 when it does not and 2 when the index could not be downloaded.
###
index_lists_runtime_package() {
  local pocket="$1" component="$2" gz decompressed
  gz="${WORK_DIR}/${pocket}-${component}.gz"
  decompressed="${WORK_DIR}/${pocket}-${component}.txt"
  curl -fsSL --max-time "$CURL_MAX_TIME" \
    "http://archive.ubuntu.com/ubuntu/dists/${pocket}/${component}/binary-amd64/Packages.gz" \
    -o "$gz" || return 2
  gunzip -c "$gz" >"$decompressed"
  grep -qx "Package: ${DOTNET_RUNTIME_PACKAGE}" "$decompressed" || return 1
}

check_dotnet_package() {
  local pocket component found=0 reachable=0 status
  for pocket in noble-updates noble-security; do
    for component in main universe; do
      status=0
      index_lists_runtime_package "$pocket" "$component" || status=$?
      if [[ "$status" -eq 0 ]]; then
        found=1
        reachable=1
        pass "Ubuntu ${pocket}/${component} lists ${DOTNET_RUNTIME_PACKAGE}"
      elif [[ "$status" -eq 1 ]]; then
        reachable=1
      fi
    done
  done
  if [[ "$reachable" -eq 0 ]]; then
    fail "could not download any Ubuntu noble-updates or noble-security package index"
  elif [[ "$found" -eq 0 ]]; then
    fail "neither Ubuntu noble-updates nor noble-security lists ${DOTNET_RUNTIME_PACKAGE}"
  fi
}

check_key_fingerprint() {
  local label="$1" url="$2" expected="$3" tmp actual
  tmp="${WORK_DIR}/key"
  if ! curl -fsSL --max-time "$CURL_MAX_TIME" "$url" -o "$tmp"; then
    fail "could not download the ${label} signing key from ${url}"
    return
  fi
  if actual="$(key_fingerprint "$tmp")"; then
    if [[ "$actual" == "$expected" ]]; then
      pass "${label} signing key fingerprint matches the pin"
    else
      fail "${label} signing key fingerprint mismatch: expected ${expected}, got ${actual}"
    fi
  else
    fail "${label} signing key at ${url} did not yield exactly one active primary key"
  fi
}

check_gh_cli() {
  local tmp version
  tmp="${WORK_DIR}/gh-packages"
  if ! curl -fsSL --max-time "$CURL_MAX_TIME" \
    "https://cli.github.com/packages/dists/stable/main/binary-amd64/Packages" \
    -o "$tmp"; then
    fail "could not download the GitHub CLI stable package index"
    return
  fi
  version="$(awk '/^Package: gh$/ { found = 1; next } found && /^Version: / { print $2; exit }' "$tmp")"
  if [[ -z "$version" ]]; then
    fail "GitHub CLI stable index does not list package gh"
    return
  fi
  if dpkg --compare-versions "$version" ge "$GH_CLI_MIN_VERSION"; then
    pass "GitHub CLI stable offers gh ${version} (>= ${GH_CLI_MIN_VERSION})"
  else
    fail "GitHub CLI stable offers gh ${version}, below the required ${GH_CLI_MIN_VERSION}"
  fi
}

main() {
  load_versions_env "$VERSIONS_ENV"

  check_dotnet_package
  check_key_fingerprint "GitHub CLI" "$GH_CLI_KEY_URL" "$GH_CLI_KEY_FINGERPRINT"
  check_gh_cli

  if [[ "$FAILURES" -gt 0 ]]; then
    echo "${FAILURES} pin(s) failed verification against their real source." >&2
    exit 1
  fi
  echo "All install-source pins verified against their real source."
}

main "$@"

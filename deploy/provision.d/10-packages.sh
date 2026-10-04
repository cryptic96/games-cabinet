#!/usr/bin/env bash
###
### Idempotent module: installs every package the host needs. Nothing here
### pipes a network download into a shell: the third-party signing key is
### fingerprint-checked, and the third-party repository is added with an
### explicit signed-by keyring. The ASP.NET Core runtime comes from the
### distribution archive.
###
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=deploy/provision.sh
CABINET_PROVISION_LIB_ONLY=1 source "${SCRIPT_DIR}/../provision.sh"

KEYRING_DIR="/usr/share/keyrings"
SOURCES_DIR="/etc/apt/sources.list.d"

###
### Downloads a third-party signing key, refuses to trust it unless its
### fingerprint matches the pin from versions.env, then installs it as a
### dearmored keyring (armored input is dearmored; an already-binary
### keyring, such as the GitHub CLI's, is installed as-is). A key set that
### contains an expired transitional key next to the current one counts as
### exactly one active primary, which is how apt itself trusts it.
###
install_apt_signing_key() {
  local label="$1" key_url="$2" expected_fpr="$3" keyring_path="$4"
  local tmp_key actual_fpr

  if [[ -f "$keyring_path" ]]; then
    return 0
  fi

  tmp_key="$(mktemp)"
  curl -fsSL --max-time 60 "$key_url" -o "$tmp_key"

  if ! actual_fpr="$(gpg --batch --with-colons --show-keys "$tmp_key" 2>/dev/null | provision_key_fingerprint)"; then
    rm -f "$tmp_key"
    provision_die "${label}: signing key at ${key_url} did not yield exactly one active primary key"
  fi
  if [[ "$actual_fpr" != "$expected_fpr" ]]; then
    rm -f "$tmp_key"
    provision_die "${label}: signing key fingerprint mismatch (expected ${expected_fpr}, got ${actual_fpr})"
  fi

  local key_head
  key_head="$(head -c 20 "$tmp_key")"
  if [[ "$key_head" == *"BEGIN PGP"* ]]; then
    gpg --batch --dearmor -o "$keyring_path" "$tmp_key"
  else
    install -m 644 "$tmp_key" "$keyring_path"
  fi
  chmod 644 "$keyring_path"
  rm -f "$tmp_key"
  provision_log "${label}: signing key installed and fingerprint verified"
}

###
### Prints the name of every installed package that provides a mail
### transport agent, one per line. Some container templates ship a local
### one; this host sends no mail, so a listening agent is unused attack
### surface.
###
installed_mta_packages() {
  local pkg provides status
  while IFS=$'\t' read -r pkg provides status; do
    if [[ "$provides" == *mail-transport-agent* && "$status" == "install ok installed" ]]; then
      printf '%s\n' "$pkg"
    fi
  done < <(dpkg-query -W -f='${Package}\t${Provides}\t${Status}\n' 2>/dev/null || true)
}

if [[ "${CABINET_PROVISION_LIB_ONLY:-0}" != "1" ]]; then
  provision_log "apt-get update"
  apt-get update -qq

  provision_log "Installing base packages"
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq \
    ca-certificates curl gnupg jq unzip git nftables unattended-upgrades tzdata \
    "$DOTNET_RUNTIME_PACKAGE"

  mapfile -t mta_packages < <(installed_mta_packages)
  if [[ "${#mta_packages[@]}" -gt 0 ]]; then
    provision_log "Removing the unused local mail transport agent: ${mta_packages[*]}"
    DEBIAN_FRONTEND=noninteractive apt-get purge -y -qq "${mta_packages[@]}"
  fi

  install_apt_signing_key "GitHub CLI" "$GH_CLI_KEY_URL" "$GH_CLI_KEY_FINGERPRINT" \
    "${KEYRING_DIR}/githubcli.gpg"

  if [[ ! -f "${SOURCES_DIR}/github-cli.list" ]]; then
    echo "deb [signed-by=${KEYRING_DIR}/githubcli.gpg] https://cli.github.com/packages stable main" \
      >"${SOURCES_DIR}/github-cli.list"
  fi

  provision_log "apt-get update (with the new source)"
  apt-get update -qq

  provision_log "Installing the GitHub CLI"
  DEBIAN_FRONTEND=noninteractive apt-get install -y -qq gh

  GH_VERSION="$(gh --version | head -n1 | awk '{print $3}')"
  if ! provision_version_ge "$GH_VERSION" "$GH_CLI_MIN_VERSION"; then
    provision_die "gh ${GH_VERSION} is older than the required ${GH_CLI_MIN_VERSION} (attestation verify support)"
  fi

  provision_log "Package installation complete"
fi

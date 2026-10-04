#!/usr/bin/env bash
###
### Idempotent module: installs the platform's scripts, libraries and systemd
### units, renders the non-secret server-side deploy configuration, enables
### automatic security updates and brings up the poll timer and the app
### (starting the app only once a release has actually been installed).
### Never touches the application environment file, which belongs to
### 20-accounts.sh and the operator alone.
###
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DEPLOY_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"
# shellcheck source=deploy/provision.sh
CABINET_PROVISION_LIB_ONLY=1 source "${SCRIPT_DIR}/../provision.sh"

###
### Outcome of the most recent services_install_file call: "written" when
### the destination was created or its content changed, "unchanged" when the
### content was already identical.
###
SERVICES_LAST_INSTALL=""

###
### Installs SOURCE at DESTINATION with the given mode, owner and group.
### Content that is already identical is left in place (its mode and owner
### are still enforced) so a re-run does not touch timestamps or trigger
### needless restarts; changed content is written beside the destination and
### moved into place so a reader never sees a partial file.
###
services_install_file() {
  local mode="$1" owner="$2" group="$3" source="$4" destination="$5"
  local staged

  [[ -f "$source" ]] || provision_die "file to install not found: ${source}"

  if [[ -f "$destination" ]] && cmp -s "$source" "$destination"; then
    chmod "$mode" "$destination"
    chown "${owner}:${group}" "$destination"
    SERVICES_LAST_INSTALL="unchanged"
    return 0
  fi

  staged="${destination}.new.$$"
  install -m "$mode" -o "$owner" -g "$group" "$source" "$staged"
  mv -f "$staged" "$destination"
  SERVICES_LAST_INSTALL="written"
}

###
### Renders OUTPUT from an example env-style file at SOURCE, replacing the
### value of each key named in a following "KEY=VALUE" override argument
### and leaving every other line (comments, and any key without an
### override) exactly as it appears in SOURCE. Refuses to write anything
### if an override names a key that SOURCE does not already define, since
### that would silently drop a value provisioning meant to set.
###
services_render_example_overrides() {
  local source="$1" output="$2"
  shift 2

  [[ -f "$source" ]] || provision_die "example file not found: ${source}"

  local tmp
  tmp="$(mktemp)"
  cp "$source" "$tmp"

  local pair name value escaped_value
  for pair in "$@"; do
    name="${pair%%=*}"
    value="${pair#*=}"

    provision_validate_safe_value "$value" \
      || provision_die "value for ${name} contains a disallowed character (semicolon, brace or newline)"

    if ! grep -qE "^${name}=" "$tmp"; then
      rm -f "$tmp"
      provision_die "${source} has no existing ${name}= line to override"
    fi

    escaped_value="$(printf '%s' "$value" | sed -e 's/[&/\]/\\&/g')"
    sed -i "s/^${name}=.*/${name}=${escaped_value}/" "$tmp"
  done

  mv -f "$tmp" "$output"
}

###
### Installs every script under deploy/bin into /usr/local/sbin (755, root).
###
services_install_scripts() {
  local script
  install -d -m 755 -o root -g root /usr/local/sbin
  for script in "${DEPLOY_DIR}"/bin/*; do
    [[ -e "$script" ]] || continue
    services_install_file 755 root root "$script" "/usr/local/sbin/$(basename "$script")"
  done
}

###
### Installs every shell library under deploy/lib into /usr/local/lib/cabinet
### (644, root), where the installer and the selfcheck look for them.
###
services_install_libraries() {
  local lib
  install -d -m 755 -o root -g root /usr/local/lib/cabinet
  for lib in "${DEPLOY_DIR}"/lib/*.sh; do
    [[ -e "$lib" ]] || continue
    services_install_file 644 root root "$lib" "/usr/local/lib/cabinet/$(basename "$lib")"
  done
}

###
### Installs every unit under deploy/systemd into /etc/systemd/system (644,
### root) and records, in the space-separated SERVICES_CHANGED_UNITS, the
### names of those whose content changed.
###
SERVICES_CHANGED_UNITS=""

services_install_units() {
  local unit name
  SERVICES_CHANGED_UNITS=""
  for unit in "${DEPLOY_DIR}"/systemd/*.service "${DEPLOY_DIR}"/systemd/*.timer; do
    [[ -e "$unit" ]] || continue
    name="$(basename "$unit")"
    services_install_file 644 root root "$unit" "/etc/systemd/system/${name}"
    if [[ "$SERVICES_LAST_INSTALL" == "written" ]]; then
      SERVICES_CHANGED_UNITS="${SERVICES_CHANGED_UNITS} ${name}"
    fi
  done
}

###
### Succeeds when the unit named by $1 is in SERVICES_CHANGED_UNITS.
###
services_unit_changed() {
  local wanted="$1" changed
  for changed in $SERVICES_CHANGED_UNITS; do
    [[ "$changed" == "$wanted" ]] && return 0
  done
  return 1
}

###
### Renders /etc/cabinet/deploy.conf (600, root) from deploy.conf.example,
### taking the repository from provision.conf.
###
services_install_deploy_conf() {
  local rendered
  rendered="$(mktemp)"
  services_render_example_overrides "${DEPLOY_DIR}/deploy.conf.example" "$rendered" \
    "CABINET_GITHUB_REPO=${CABINET_GITHUB_REPO:-}"
  install -m 600 -o root -g root "$rendered" /etc/cabinet/deploy.conf
  rm -f "$rendered"
}

###
### Turns on unattended installation of security updates.
###
services_enable_unattended_upgrades() {
  install -d -m 755 -o root -g root /etc/apt/apt.conf.d
  cat > /etc/apt/apt.conf.d/51cabinet-unattended-upgrades <<'EOF'
APT::Periodic::Update-Package-Lists "1";
APT::Periodic::Unattended-Upgrade "1";
EOF
}

###
### Enables the poll timer and the app. The app is started (or restarted
### when its unit changed) only when a release is installed.
###
services_enable_and_start() {
  systemctl daemon-reload

  systemctl enable --now cabinet-deploy-poll.timer
  if services_unit_changed cabinet-deploy-poll.timer; then
    systemctl restart cabinet-deploy-poll.timer
  fi

  systemctl enable cabinet.service
  if [[ -e /opt/cabinet/current ]]; then
    if services_unit_changed cabinet.service; then
      systemctl restart cabinet.service
    else
      systemctl start cabinet.service
    fi
  else
    provision_log "No release installed yet; cabinet.service is enabled but not started."
  fi
}

if [[ "${CABINET_PROVISION_LIB_ONLY:-0}" != "1" ]]; then
  provision_log "Installing scripts and libraries"
  services_install_scripts
  services_install_libraries

  provision_log "Installing systemd units"
  services_install_units

  provision_log "Rendering the non-secret server-side configuration"
  services_install_deploy_conf

  provision_log "Enabling automatic security updates"
  services_enable_unattended_upgrades

  provision_log "Enabling and starting services"
  services_enable_and_start

  provision_log "Services module complete."
fi

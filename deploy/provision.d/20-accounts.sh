#!/usr/bin/env bash
###
### Idempotent module: creates the cabinet service account, its directories
### and the application env file. Never overwrites the env file once it
### exists, so values an operator added later survive re-provisioning.
###
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=deploy/provision.sh
CABINET_PROVISION_LIB_ONLY=1 source "${SCRIPT_DIR}/../provision.sh"

###
### Renders the application env file content from the given Traefik
### address. Produces exactly these two keys, in this order: the production
### environment name and the reverse proxy address the app trusts for
### forwarded headers.
###
accounts_render_cabinet_env() {
  local traefik_ip="$1"
  cat <<EOF
ASPNETCORE_ENVIRONMENT=Production
ReverseProxy__KnownProxies__0=${traefik_ip}
EOF
}

if [[ "${CABINET_PROVISION_LIB_ONLY:-0}" != "1" ]]; then
  ENV_PATH="/etc/cabinet/cabinet.env"

  provision_log "Creating the service group and user"

  if ! id -u cabinet >/dev/null 2>&1; then
    useradd --system --user-group --no-create-home --home-dir /nonexistent \
      --shell /usr/sbin/nologin cabinet
  fi

  provision_log "Creating directories"

  mkdir -p /opt/cabinet/releases
  chown root:root /opt/cabinet/releases
  chmod 755 /opt/cabinet/releases

  mkdir -p /etc/cabinet
  chown root:cabinet /etc/cabinet
  chmod 750 /etc/cabinet

  mkdir -p /var/lib/cabinet-deploy
  chown root:root /var/lib/cabinet-deploy
  chmod 700 /var/lib/cabinet-deploy

  if [[ ! -f "$ENV_PATH" ]]; then
    provision_log "Writing ${ENV_PATH}"

    render_tmp="$(mktemp)"
    chmod 600 "$render_tmp"
    accounts_render_cabinet_env "${CABINET_TRAEFIK_IP:-}" >"$render_tmp"
    install -m 640 -o root -g cabinet "$render_tmp" "$ENV_PATH"
    rm -f "$render_tmp"
  fi

  provision_log "Application env file: ${ENV_PATH}"
fi

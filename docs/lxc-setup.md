# Building the server container

This describes every one-time, manual step that takes a fresh container from
nothing to a locked-down, running site: creating it, provisioning it, routing
traffic to it, installing the first release and proving the result. Every
path, hostname and address below is a placeholder; replace them with the
server's own values. Values in `{braces}` are a reminder of what still needs
filling in. Real values live only on the server, never in this repository.

## What runs here, and what is reachable from where

The container runs the web application and a timer that installs published
releases. There is no database; the application keeps its state in a
directory it owns.

| What | Where it listens | Who can reach it |
| --- | --- | --- |
| Web application | port 5080 | the reverse proxy only (the firewall drops everyone else) |
| Operations and health endpoint | port 5081, loopback only | processes on the container itself; no route through the reverse proxy |
| SSH | port 22 | the admin address ranges only, with keys only |

Outbound, the container needs HTTPS to GitHub (to find and download releases)
and to the public transparency service that release attestations are checked
against. Everything else inbound is dropped by default.

## Prerequisites

- A shell on the Proxmox host that will run the container.
- Access to the dynamic configuration directory of the existing Traefik
  container, which already has a certificate resolver configured.
- Control of local DNS, to point an internal hostname at the reverse proxy.
- An SSH key pair on your workstation.

## 1. Create the container

On the Proxmox host, create an unprivileged container from an Ubuntu 24.04
template. Replace every `{placeholder}` with the host's own value:

```bash
pct create {ctid} {storage}:vztmpl/ubuntu-24.04-standard_{arch}.tar.zst \
  --hostname cabinet \
  --unprivileged 1 \
  --features nesting=1 \
  --cores 1 \
  --memory 1024 \
  --swap 512 \
  --rootfs {storage}:8 \
  --net0 name=eth0,bridge={bridge},ip={address}/24,gw={gateway} \
  --ssh-public-keys {path-to-your-public-key-file} \
  --onboot 1
pct start {ctid}
pct enter {ctid}
```

One core, 1 GB of memory and an 8 GB disk are a starting point. They are
small on purpose for a shared, low-power host; tune them later from the load
you actually see. Nesting is needed so the service sandboxing that the app
unit and the selfcheck rely on works inside an unprivileged container.

The remaining steps run as root inside the container (`pct enter {ctid}`
puts you there; this also keeps working if a firewall mistake ever locks SSH
out, because it never goes through the network).

## 2. Get the scripts

The very first provisioning happens before any release exists, so use the
main branch. Afterwards use the latest release tag.

```bash
apt-get update
apt-get install -y git
git clone https://github.com/cryptic96/games-cabinet.git /root/cabinet-src
cd /root/cabinet-src
git checkout {main-or-latest-release-tag}
```

## 3. First provisioning run

```bash
deploy/provision.sh
```

The first run only creates `/etc/cabinet/provision.conf` from its example and
stops.

## 4. Fill in provision.conf and run again

Open `/etc/cabinet/provision.conf` and replace every `CHANGE-ME` value:

| Key | Meaning |
| --- | --- |
| `CABINET_TRAEFIK_IP` | the address of the Traefik container; the app trusts forwarded headers only from it and the firewall admits port 5080 only from it |
| `CABINET_ADMIN_SSH_SOURCES` | comma-separated CIDR ranges allowed to reach SSH, for example `192.0.2.0/24,198.51.100.0/24` |
| `CABINET_GITHUB_REPO` | the `owner/name` of the public repository releases are pulled from |

Then run provisioning again:

```bash
deploy/provision.sh
```

Provisioning refuses to apply a placeholder or a malformed value. A second
run installs the packages and the runtime, creates the service account and
its directories, writes `/etc/cabinet/cabinet.env` (once, and never again),
loads the default-drop firewall, installs the installer, the selfcheck and
the systemd units, and enables the poll timer and the application. The
application is not started until the first release is installed.

## 5. Admin access

Provisioning does not change the SSH server's settings, so make it accept
keys only now. The selfcheck fails until both settings are off. The file name
starts with `00` so it wins over any later drop-in:

```bash
printf '%s\n' 'PasswordAuthentication no' 'KbdInteractiveAuthentication no' \
  > /etc/ssh/sshd_config.d/00-key-only.conf
systemctl reload ssh
```

Check that your key works from a second terminal before closing the first.
For convenience, add an alias for the container to the SSH configuration on
your workstation:

```text
Host cabinet
  HostName {address}
  User root
  IdentityFile {path-to-your-private-key-file}
```

## 5a. Add the BoardGameGeek keys

The site needs access to the BoardGameGeek API to fetch the collection.
Provisioning writes `/etc/cabinet/cabinet.env` once and never rewrites it, so
add the keys by hand:

```text
Bgg__Username={your-bgg-username}
Bgg__Token={your-bgg-api-token}
Bgg__ContactUrl={optional-contact-url}
```

`Bgg__ContactUrl` is optional; when set it is added to the `User-Agent` the
server sends to BoardGameGeek. Keep the file at mode `640` owned by
`root:cabinet`, then restart the application:

```bash
sudo systemctl restart cabinet
```

[The sync guide](bgg-sync.md) explains each setting and what the server does
with them. Without the keys the application starts and logs a warning that sync
is not configured.

## 6. Install the Traefik route

On the Traefik container, copy `deploy/traefik/cabinet.yml.example` into its
dynamic configuration directory, drop the `.example` suffix and replace every
placeholder: the allowed LAN and VPN ranges, the hostname and the address of
this container. The route is limited to the LAN and VPN ranges until the site
is deliberately made public; making it public means removing the allow-list
middleware from the real file on the Traefik container, nothing in this
repository.

The route's security-headers middleware sets HSTS, nosniff, frame denial and
the referrer policy. It sets no Content-Security-Policy on purpose: the
application sends its own strict policy with every response, and a second
policy added by the proxy would be enforced alongside it.

The route's service URL must point at this container's own address on port
5080. The two addresses run in opposite directions: the route names this
container, while `CABINET_TRAEFIK_IP` in `provision.conf` names the Traefik
container.

## 7. Add a local DNS record

In your DNS, point the site's hostname (for example `cabinet.example.com`) at
the Traefik container's address.

## 8. Apply the GitHub repository settings

Follow [the repository settings guide](github-repository-settings.md) once,
before the first release.

## 9. The first release

Follow [the releasing guide](releasing.md) to tag, build and approve the first
release. The timer then installs it within about ten minutes. To avoid
waiting, run the poll by hand:

```bash
cabinet-deploy poll
```

Before any release is published the poll logs that there is no published
release yet and succeeds. Watch it work:

```bash
journalctl -u cabinet-deploy-poll -f
```

See [the deploy guide](deploy.md) for what a poll does, what a rollback looks
like and how to go back by hand.

## 10. Run the selfcheck

```bash
cabinet-selfcheck
```

This proves the host end to end and prints PASS or FAIL for each check:

- the application and the poll timer are active and enabled;
- ownership and modes of the configuration, state and release directories;
- the application answers on port 5080 and the operations port 5081 is
  bound to loopback only;
- the firewall's input chain drops by default;
- the health endpoint reports `Healthy` at exactly the version the `current`
  link points at;
- the installed installer, its libraries and the systemd units match, byte
  for byte, the copies shipped in the active release's `deploy/` directory;
- SSH accepts keys only;
- there is no GitHub credential and no Actions runner anywhere on the host;
- the image smoke: the deployed binary runs its imaging self-test as the
  service user inside a transient systemd sandbox with nothing writable, no
  home directory, no namespaces, no capabilities and no network beyond local
  sockets.

The command exits non-zero if any check fails. The selfcheck is only
meaningful once a release is installed; before that the health and image
smoke checks fail by design.

## 11. Logs

Everything logs through the journal:

```bash
journalctl -u cabinet.service -u cabinet-deploy-poll.service
journalctl -u cabinet.service -f
journalctl -u cabinet-deploy-poll.service -f
```

## 12. When to re-run provisioning

Provisioning is idempotent. Re-run it whenever a package pin, a systemd unit,
the firewall template or a server-side value changes. Once a release is
installed, run it from the active release:

```bash
/opt/cabinet/current/deploy/provision.sh
```

Running from the active release is what makes the installed scripts,
libraries and units match what every poll and the selfcheck compare against.
A source checkout gives the same result only when it is checked out at the
active release's tag. Before any release is installed, use the checkout from
the earlier steps:

```bash
cd /root/cabinet-src && git pull && git checkout {main-or-latest-release-tag}
deploy/provision.sh
```

There is no need to track by hand whether the installed scripts, libraries
and units are current: every poll logs a warning and `cabinet-selfcheck`
fails until provisioning is re-run. See
[the deploy guide](deploy.md#when-to-re-run-provisioning) for exactly what is
compared and what is not. A re-run never overwrites `/etc/cabinet/cabinet.env`
or `/etc/cabinet/provision.conf`; edit those by hand. Only files whose content
changed are rewritten, and a changed unit restarts only the service it
describes.

## What this guide never does

It never puts a GitHub credential, a deploy key or a CI runner on the
container: the container only ever pulls releases that are already published
and whose provenance it verifies itself. It never routes the operations port
anywhere. It never asks for a real hostname, address or secret to be written
into this repository; those exist only in server-side files.

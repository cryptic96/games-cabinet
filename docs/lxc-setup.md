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
| Owner tools | port 5082 | the reverse proxy only, and only when the owner tools are switched on |
| Operations and health endpoint | port 5081, loopback only | processes on the container itself; no route through the reverse proxy |
| SSH | port 22 | the admin address ranges only, with keys only |

Outbound traffic to the internet stays open: GitHub releases and the public
transparency service that release attestations are checked against,
BoardGameGeek and its image host, package mirrors, and DNS to the resolver.
New connections from the container into the home network are refused, so a
flaw in the application cannot be used to reach other machines at home.
Everything else inbound is dropped by default.

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

One core, 1 GB of memory and an 8 GB disk are enough for the measured load. They
are small on purpose for a shared, low-power host; change them only if the load
you actually see calls for it. Nesting is needed so the service sandboxing that the app
unit and the selfcheck rely on works inside an unprivileged container.

### Container limits

The container's own limits are set from the Proxmox host, and can be changed
while it runs. Replace every `{placeholder}` with the host's own value:

```bash
pct set {ctid} --cores {cores} --memory {memory-mb} --swap {swap-mb} --cpuunits {cpu-units}
```

The values the unit's limits were checked against, as a worked example (replace
`{ctid}` with the container's id): one core, 1 GiB of memory, 512 MiB of swap
and a CPU weight of 50.

```bash
pct set {ctid} --cores 1 --memory 1024 --swap 512 --cpuunits 50
```

- Give the container a CPU weight (`--cpuunits`) lower than the reverse
  proxy's, so the proxy always wins when the host is busy.
- The container cannot read its own CPU weight, so check it on the host with
  `pct config {ctid}`. Values that wait for a restart show up with
  `pct pending {ctid}`.
- The application unit has limits of its own (memory, swap, CPU share, task
  count and open files), and they sit below the container's, so SSH and the
  deploy timer keep room even when the application is at its ceiling. The
  numbers shipped in the unit are sized from what was measured: the whole
  service peaked at about 314 MiB through a sync, a full picture rebuild of a
  few hundred games and a deliberate flood, and one worst-case picture decode
  adds about 144 MiB on top. The unit shows the values: 480 MiB where the
  kernel starts reclaiming memory, 600 MiB as the hard ceiling, no swap, 80
  percent of one core, a CPU weight of 50, 512 tasks and 4096 open files. The
  runtime does not see the unit's memory limit and reports the host's memory
  instead, so the unit also gives the managed heap a hard limit of 384 MiB
  (`DOTNET_GCHeapHardLimit=0x18000000`), below the point where reclaim starts;
  after provisioning, the start-up line the service logs reports about that
  limit. The selfcheck fails when the unit's memory ceiling leaves less than a
  small margin below the
  container's memory, or when its CPU quota reaches the container's cores.
- The margins are worth knowing before changing any of these numbers. The 314
  MiB peak is the whole process, native memory included, so the managed heap's
  own peak is lower, and the 384 MiB heap limit sits at least 70 MiB above it. A
  worst-case picture decode adds about 144 MiB, which brings the whole process
  to about 458 MiB: inside the 480 MiB reclaim point with 22 MiB to spare, and
  well inside the 600 MiB ceiling. A picture decode and a sync's own peak do not
  normally coincide. A heap limit turns an overshoot into an out-of-memory error
  in the sync or picture rebuild that hit it, not a gradual slowdown, so that run
  fails and the next one tries again. Nothing on the host checks that the runtime
  took the value beyond the start-up line mentioned above, so look at that line
  after changing the limit.

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
| `CABINET_EGRESS_RESOLVERS` | the address, or comma-separated addresses, the container's DNS queries go to, as `resolvectl status` or `/etc/resolv.conf` shows them; DNS to these stays open |
| `CABINET_EGRESS_BLOCKED_RANGES` | comma-separated IPv4 ranges the container may not open new connections to; the example lists the private, shared and link-local blocks, which cover the home network and the common VPN range |
| `CABINET_EGRESS_BLOCKED_RANGES6` | the same for IPv6; when the home network has IPv6, append its own global prefix to the list |

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

### The outbound block

The block refuses new connections from the container to the blocked ranges
with an immediate "administratively prohibited" answer rather than silence, so
a mistaken block fails visibly instead of hanging until a timeout. Replies to
connections that came in (the reverse proxy, SSH sessions), loopback, IPv6
neighbour discovery and DNS to the configured resolvers stay open, and so does
everything on the internet.

The block applies to new connections only. A connection that was already open
when the ruleset loaded keeps working until it closes, so a first provisioning
run on a container that already talks to the home network is not fully in force
until those connections end or the container restarts.

Mind the order. Add the three `CABINET_EGRESS_*` keys to an existing
`provision.conf` before re-running provisioning, because provisioning refuses
an old file that lacks them. The example file lists the keys with a placeholder
resolver, so it is refused as it stands.

Side effects to know about:

- The container can no longer use services on the home network: a local
  package cache, a time server on the home network or home-network monitoring
  that the container would call out to. Packages and releases come from the
  internet.
- The owner tools' link check fetches web pages for the owner, and it cannot
  fetch addresses on the home network.
- A container that gets its address by DHCP needs its DHCP server allowed.
  The server normally sits inside a blocked range, and renewing the lease
  would be refused. Give the container a fixed address, as the creation
  command above does, or split the blocked ranges so that the DHCP server's
  address is not covered.

If the block ever locks you out of something you need, `pct enter {ctid}` on
the Proxmox host gives a root shell on the container without going through
the network, from which you can fix `provision.conf` and re-run provisioning.

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
repository. [The go-public checklist](go-public.md) has the checks to pass
first, the exact steps and how to close the site again.

The public router also carries an outer wall: a rate limit per client address
and a cap on requests being served at once per client address. The proxy
refuses a flood cheaply, before it reaches this container, and the limits
leave room for a group of friends on one Wi-Fi network. The owner router has
neither, on purpose: it is reachable from the home network and VPN only. The
numbers in the example file (60 requests per second on average, a burst of
800 and 200 requests in flight, per client address) come from a measured first
visit and leave room for about 8 simultaneous first visits from one address. The application adds much stricter limits of its own for "sync
now" and the live channel (see [the sync guide](bgg-sync.md)).

The route's security-headers middleware adds only HSTS, because the proxy is
the one that terminates TLS. The application sends the
Content-Security-Policy and every other security header with every response,
and they are tested together with the page; a header set by both would be
sent twice, and a second policy added by the proxy would be enforced
alongside the application's.

The application answers only to the hostnames listed in `AllowedHosts` in the
env file: the public one, the owner one, and loopback. Keep loopback in the
list, or the deploy health check fails and the release rolls back. See
[the deploy guide](deploy.md#host-settings-and-a-release).

The route's service URL must point at this container's own address on port
5080. The two addresses run in opposite directions: the route names this
container, while `CABINET_TRAEFIK_IP` in `provision.conf` names the Traefik
container.

## 7. Add a local DNS record

In your DNS, point the site's hostname (for example `cabinet.example.com`) at
the Traefik container's address.

## 7a. The owner host

The owner tools (correcting box images and managing storage locations) are
served on a port and a hostname of their own and are off by default: a release
installs with the owner port closed until you switch it on. Switching them on
takes four steps, all on your side:

1. Add the owner router. `deploy/traefik/cabinet.yml.example` carries a second
   router, `cabinet-owner`, on its own hostname (the example uses
   `owner.example.com`) and a service pointing at this container's address on
   port 5082. Copy both into the real file next to the public router. The
   owner router uses the same LAN and VPN allow-list and security headers, and
   it keeps that allow-list permanently: when you later remove the allow-list
   from the public router to open the site to everyone, leave it on the owner
   router. The entry point must append the client address to `X-Forwarded-For`
   (Traefik's default); do not enable `notAppendXForwardedFor` and do not list
   client networks under `trustedIPs` in the entry point's `forwardedHeaders`.
2. Add the DNS record. Point the owner hostname at the Traefik container, the
   same way as the public hostname.
3. Switch the endpoint on. Add these lines to `/etc/cabinet/cabinet.env`
   (`deploy/cabinet.env.example` shows them commented out, with placeholder
   ranges), using your own LAN and VPN ranges:

   ```text
   Kestrel__Endpoints__Owner__Url=http://0.0.0.0:5082
   Owner__AllowedRanges__0={your-lan-range}
   Owner__AllowedRanges__1={your-vpn-range}
   ```

   `ReverseProxy__KnownProxies__0` must be the Traefik container's address,
   because the owner tools trust the forwarded client address only from it.
   Then restart the application with `sudo systemctl restart cabinet`.
4. Check it. Run `cabinet-selfcheck` on the container, then
   `build/check-owner-routes.sh` from a machine on your home network or VPN
   (see [the deploy guide](deploy.md)).

The firewall already admits port 5082 from the Traefik container only, the
same as port 5080, so nothing else on the network can reach it. There are two
more walls behind the proxy's allow-list. The firewall is the second: it drops
every other source. The application is the third: it checks the client address
itself against `Owner__AllowedRanges__*` and answers `403` to any address
outside the ranges, and the public port answers `404` for every owner path.
All walls must agree; the selfcheck and the route check each prove part of
that.

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
- the owner port 5082 is listening or, when the owner tools are off, nothing
  listens on it; the public port answers `404` for the owner pages, and when
  the owner port listens a request through loopback that claims an address
  outside the allowed ranges is refused with `403`;
- the firewall's input chain drops by default, and its output chain refuses
  new connections to the blocked ranges: a connection to the first blocked
  network must be counted by the reject rule's own packet counter, and no
  address is printed;
- every global IPv6 address of the container lies inside the blocked IPv6
  ranges, so a home network reachable on its own global prefix is covered; the
  check fails with a count, never an address, when the prefix is missing;
- the application unit's resource limits match the loaded unit file, and sit
  below the container's own memory and cores;
- names resolve through the configured resolvers;
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

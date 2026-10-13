# Switching the owner host on

These are the steps to open the owner tools on the real hosts. Everything here
uses placeholders: `owner.example.com` is your owner hostname,
`cabinet.example.com` is your public hostname, `192.0.2.0/24` is your home
network range and `198.51.100.0/24` is your VPN range. Replace them with your
real values only on the hosts themselves, never in the repository.

Nothing in these steps is done for you: the server env file, the DNS and the
Traefik host are yours. When all five steps are done, tell Claude "owner host
ready".

## Step 1. Server env file: switch the owner endpoint on

On the container, open the env file:

```bash
sudo nano /etc/cabinet/cabinet.env
```

Add these three lines, with your real home range and VPN range in place of the
examples. Add all three together: the endpoint without a range makes the
service refuse to start.

```text
Kestrel__Endpoints__Owner__Url=http://0.0.0.0:5082
Owner__AllowedRanges__0=192.0.2.0/24
Owner__AllowedRanges__1=198.51.100.0/24
```

In the same file, check that the known proxy line holds Traefik's address (the
address Traefik connects from), not the container's own address:

```text
ReverseProxy__KnownProxies__0=<Traefik address>
```

Restart the service:

```bash
sudo systemctl restart cabinet
```

The service refuses to start when a range is malformed, when a range is a
`/0` (everything), or when the known proxy line is missing. A refused start
shows in the journal and in the deploy's health check as a failed or
unhealthy service; read the reason with:

```bash
sudo journalctl -u cabinet -n 30 --no-pager
```

Fix the env lines and restart again.

How you know it worked: this shows the owner port 5082 listening and no FAIL
lines:

```bash
sudo -n cabinet-selfcheck
```

## Step 2. DNS: add the owner hostname (already done)

Status: done, the DNS record for the owner host already exists.

For the record: the owner hostname points at the Traefik container, the same
way the public hostname does (`owner.example.com` next to
`cabinet.example.com`).

How you know it worked: from the workstation, the owner hostname resolves to
the Traefik address:

```bash
getent hosts owner.example.com
```

## Step 3. Traefik host: add the owner router and service

On the Traefik host, open the dynamic configuration file that already holds
the public router. Copy the `cabinet-owner` router and the `cabinet-owner`
service from `deploy/traefik/cabinet.yml.example` next to it, with your real
owner hostname and the container's real address on port 5082:

```yaml
http:
  routers:
    cabinet-owner:
      rule: "Host(`owner.example.com`)"
      entryPoints:
        - websecure
      middlewares:
        - cabinet-lan-only
        - cabinet-security-headers
      service: cabinet-owner
      tls:
        certResolver: letsencrypt

  services:
    cabinet-owner:
      loadBalancer:
        servers:
          - url: "http://<container address>:5082"
```

Keep the `cabinet-lan-only` allow-list on this router, with your home range
and VPN range (for example `192.0.2.0/24` and `198.51.100.0/24`). Keep it
permanently, even when the public router later drops its allow-list.

Do not set `notAppendXForwardedFor` on the entry point, and do not list client
networks under `trustedIPs` in the entry point's `forwardedHeaders`: either
lets a caller forge the address the application checks.

How you know it worked: Traefik picks the file up without errors (check its
log), and from home this answers 200:

```bash
curl -s -o /dev/null -w '%{http_code}' https://owner.example.com/owner/settings
```

From outside the allow-list (for example a phone on mobile data with the VPN
off) the same address answers 403, which is the allow-list doing its job.

## Step 4. Workstation shell: name the two hosts

In your own shell profile on the workstation (never in the repository), keep
the public hostname and add the owner hostname:

```bash
export CABINET_PUBLIC_HOST=cabinet.example.com
export CABINET_OWNER_HOST=owner.example.com
```

Open a new shell so the variables are set.

How you know it worked: both variables are set, without printing their
values:

```bash
test -n "${CABINET_OWNER_HOST:-}" && test -n "${CABINET_PUBLIC_HOST:-}" && echo both set
```

## Step 5. Tell Claude

Reply "owner host ready". Claude then checks, read-only, that the selfcheck
on the container reports the owner port listening and the health answer
`Healthy`, runs the route check against both routers from the workstation,
and then asks you to do your first session on the owner pages. If the service
did not start, the selfcheck's FAIL lines (no values) say what to fix in the
env file.

## A note about the phone and the VPN

If your phone is on the VPN but resolves the owner hostname to the public
address instead of Traefik's internal address, its request reaches Traefik
from outside the allow-list and the owner host answers 403. That is the
allow-list working as intended, not a fault. Fix it by using the home gateway
as the VPN's DNS server, so the phone resolves the owner hostname to the
internal address.

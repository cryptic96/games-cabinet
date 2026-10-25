# Opening the site to the public

This is the checklist for opening the site to everyone on the internet, for
proving that it is safe to leave open, and for closing it again if something
looks wrong. The same page serves every later re-check. Every hostname, address
and path below is a placeholder; real values live only on the servers, never
in this repository.

Until this checklist passes, the public route stays behind the allow-list of
home network and VPN ranges that [the container guide](lxc-setup.md#6-install-the-traefik-route)
sets up.

## What keeps the public site safe

Several walls stand between the internet and the site. None of them is the
only one; each catches what the others might miss.

- **The proxy counts requests per internet address.** The reverse proxy
  refuses a flood cheaply, before it ever reaches the container. Two settings
  do the work: a rate limit per client address and a cap on requests being
  served at once per client address. Both are sized generously on purpose, so
  a group of friends opening the link on one Wi-Fi network all get every
  picture. The numbers in the example file are starting values; they are
  re-sized from a measured first visit.
- **The application adds much stricter limits where a visitor can cause real
  work.** Pressing "sync now" is limited to a handful of presses a minute for
  each client address, and a press over the limit gets the same answer as the
  shared cooldown, so the page shows its usual countdown. The live channel
  limits how many pages one address may hold open and how often it may ask for
  a new connection. The live channel also admits only pages served by the
  site's own hostnames, so a page on another site cannot use it. The limits
  and their settings are in [the sync guide](bgg-sync.md).
- **The application sends the security headers; the proxy adds only HSTS.**
  The application sends the content security policy and the other security
  headers with every response, and they are tested together with the page. The
  proxy terminates TLS, so it is the one that adds `Strict-Transport-Security`.
  A header set in both places would be sent twice.
- **The application answers only to its own hostnames.** `AllowedHosts` lists
  the public and owner hostnames plus loopback; a request naming any other
  host gets a `400`.
- **The owner tools keep their allow-list for good.** The owner router stays
  limited to the home network and VPN ranges, whatever happens to the public
  router. The firewall and the application check the client address again
  behind the proxy.
- **The container and the application unit have resource caps.** A runaway
  site can slow itself down but not starve the other guests on the host or the
  reverse proxy. See [the container guide](lxc-setup.md#container-limits).
- **The container refuses new connections into the home network.** A flaw in
  the application cannot be used to reach other machines at home. See
  [the container guide](lxc-setup.md#4-fill-in-provisionconf-and-run-again).
- **Health and operations are not routed.** The health and operations endpoint
  listens on loopback only and has no route through the proxy.

## Before you open

1. **Release number.** The version that goes public is the one that was
   checked, and the first public release is `1.0.0`. See
   [the releasing guide](releasing.md#version-rules). Do not open the site on a
   `0.x` release, and do not open it on a build other than the one the checks
   below were run against.
2. **Host settings, in order.** Set the server-side values a release depends
   on before the release is installed: the env file keys such as
   `AllowedHosts` first, the `provision.conf` outbound keys next, then the
   release, then provisioning re-run from the active release, then
   `cabinet-selfcheck`. The reasons and the full table are in
   [the deploy guide](deploy.md#host-settings-and-a-release).
3. **Measure on a pre-release.** The caps and the proxy limits ship as starting
   values. On a pre-release, force a picture rebuild and a sync and watch the
   container's memory and CPU, then set the caps with room to spare. Run a
   short controlled flood from the proxy container itself, so the traffic never
   crosses the home gateway or its intrusion prevention, and confirm that the
   proxy refuses the flood while a normal request still succeeds. Hold about
   twenty live streams open during that test, to see that open live
   connections count as requests in flight for as long as they stay open.

## The checklist

Every item names the command or place that proves it. Tick them in order.

1. **Security headers.** From the home network, with both hostnames in the
   environment:

   ```bash
   CABINET_PUBLIC_HOST={public-hostname} CABINET_OWNER_HOST={owner-hostname} \
     build/check-public-routes.sh --from-lan
   ```

   It prints one PASS or FAIL line per check and never prints a hostname. All
   lines must pass.
2. **Rate limits.** Run the same script with `--burst`:

   ```bash
   CABINET_PUBLIC_HOST={public-hostname} CABINET_OWNER_HOST={owner-hostname} \
     build/check-public-routes.sh --from-lan --burst
   ```

   The script sends 8 presses of "sync now" by default and refuses a requested
   count above 20 (set `CABINET_BURST_COUNT` to change it). The application
   answers presses beyond 5 per 60 seconds with a `429`. The first accepted
   press starts one real, cooldown-guarded sync and opens the shared cooldown
   window, so run it where that disturbs no measurement. Also run the flood
   check from the proxy container described above.
3. **Health, operations and owner tools are not routed publicly.** The same
   script checks that `/health` and an owner page answer `404` on the public
   hostname, that the application, operations and owner ports answer nothing on
   it, and that the owner hostname answers `403` from outside the home
   network. Confirm the last part with a phone on mobile data: the public
   hostname's `/health`, the operations port and the owner hostname must all
   fail to load.
4. **The BoardGameGeek credit is on every page.** Look for the linked "Powered
   by BGG" credit in the footer on the cabinet, the empty cabinet, the error
   view and a phone-sized window.
5. **The BoardGameGeek terms, re-read.** Read the two official pages again
   (the page on using the XML API and the XML API terms of use). When the live
   page is behind a bot check, read an archive capture of it. Check the credit,
   the no-ads and no-donations rule, and the server-side-only rule.
6. **No personal data in the repository.** Run `build/scan-history.sh` for the
   full history and `build/lint.sh` for the working tree. Both must pass.
7. **Resource caps applied and verified.** `cabinet-selfcheck` checks the
   application unit's limits. On the Proxmox host, `pct config {ctid}` shows
   the container's own limits and `pct pending {ctid}` shows values waiting for
   a restart.
8. **The outbound firewall is active.** `cabinet-selfcheck` confirms the
   output chain rejects new connections to the blocked ranges, and that a
   connection to the first blocked network is counted by the reject rule.
9. **Search engines are asked not to index.** The home page carries
   `noindex` (the script checks it), and there is no `robots.txt` that blocks
   crawlers, because a crawler that cannot read the page never sees the
   `noindex` request.
10. **Maintenance SSH keys are protected.** Keys used to reach the servers are
    protected by a passphrase and are loaded into an agent only while
    maintenance is under way.
11. **The reverse proxy's own SSH is limited.** Its SSH access accepts the
    admin addresses only, the same as the cabinet container's.

## Opening the site

The site is opened by editing one file on the reverse proxy. Proxy changes take
effect as soon as the file is saved in the watched folder, so work on copies.

1. On the reverse proxy, copy the dynamic file to a folder the file provider
   does not watch. This is the backup. A backup left inside the watched folder
   would be read as a second file.
2. Make a second copy outside the watched folder and edit that one.
3. In the edited copy, remove only the allow-list middleware (called
   `cabinet-lan-only` in the example file) from the **public** router's
   middleware list. Leave the owner router alone; it keeps its allow-list for
   good.
4. Check the edited copy. A typo can make the proxy drop the whole file, which
   takes every route in it offline. Check that the YAML is well formed, that
   the public router still lists the headers, rate-limit and in-flight
   middlewares, and that the owner router still lists the allow-list.
5. Move the edited copy over the live file.
6. Watch the proxy's log accept the file, with no parse error.
7. Probe from outside, with a phone on mobile data: open the site; try
   `/health` on the public hostname, the operations port and the owner
   hostname, which must all fail; then run the SSL and security-header
   scanners of your choice against the public hostname. Keep scanner output out
   of this repository.
8. Run `build/check-public-routes.sh --from-outside` from a machine outside the
   home network.

## Closing it again

Put the allow-list middleware back on the public router, using the backup from
the opening steps. The site is closed as soon as the proxy reloads the file;
nothing in the application or the container changes.

## Re-checking later

Run `build/check-public-routes.sh` and `cabinet-selfcheck` after any change to
the reverse proxy and after every release. A release does not change the
server-side settings, so the checks also show when a setting a release needs is
missing.

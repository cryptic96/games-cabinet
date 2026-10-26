# Server steps for the measurement release (v0.11.0)

This page lists what happens on the server for this release, in the order it has
to happen. Every value in braces is a placeholder; the real value stays on the
server and in chat and is never written into the repository.

Where things stand: the release draft is verified and unchanged (checksum,
signed attestation and manifest all check out; three assets). Nothing is
published. Publishing makes the release permanent, and the container installs
it within about ten minutes. The owner-data format and the layout version do not
change in this release.

Why the order matters: after this release is installed, the app answers only to
the hostnames listed in the server env file. Its deploy health check calls the
app on loopback, so a host list without loopback would make the new version fail
its health check and roll back. That is why step A comes before the approval.

## A. Owner, before approving the deploy: the host list

1. In `/etc/cabinet/cabinet.env` add this line (quoted, because the value holds
   semicolons):

   ```
   AllowedHosts="{public-hostname};{owner-hostname};127.0.0.1;localhost"
   ```

2. Restart the app:

   ```bash
   sudo systemctl restart cabinet
   ```

3. Check the running version still answers on loopback, and that a foreign host
   is refused:

   ```bash
   sudo cabinet-selfcheck
   curl -s -o /dev/null -w '%{http_code}\n' -H 'Host: elsewhere.example.org' http://127.0.0.1:5080/
   ```

   The health line of the selfcheck must show PASS, and the `curl` must print
   `400`.

4. If the health line fails, remove the `AllowedHosts` line, restart the app and
   tell Claude. Do not approve the deploy until both checks pass.

Claude never opens or prints the env file.

## B. Owner: approve the deploy

In GitHub, publish the release draft `v0.11.0`, then approve the `deploy`
environment when the workflow asks. The container's poll timer picks the
release up within about ten minutes, verifies the archive and its attestation
offline, installs it, runs the health check and rolls back if it fails.

Tell Claude when the release is published.

## C. Claude, with the owner's go: the outbound walls

After the install is confirmed (health shows the new version and `Healthy`),
Claude:

1. Reads the container's DNS server (`resolvectl status`) and its global IPv6
   prefix (`ip -6 addr`). The values go only into `provision.conf` on the
   server and into chat.
2. Appends three keys to `/etc/cabinet/provision.conf`:

   ```
   CABINET_EGRESS_RESOLVERS={resolver-address}
   CABINET_EGRESS_BLOCKED_RANGES=10.0.0.0/8,172.16.0.0/12,192.168.0.0/16,100.64.0.0/10,169.254.0.0/16
   CABINET_EGRESS_BLOCKED_RANGES6=fc00::/7,fe80::/10,{home-ipv6-prefix}
   ```

   `{home-ipv6-prefix}` is the home network's own /64, for example
   `2001:db8:1::/64`; it is left out when the home network has no IPv6.
3. Re-runs provisioning from the active release, which reloads the firewall,
   installs the unit with its resource limits and restarts the app once:

   ```bash
   sudo /opt/cabinet/current/deploy/provision.sh
   sudo -n cabinet-selfcheck
   ```

   Every selfcheck line must pass: resource limits, outbound block, DNS and
   provisioning current.
4. Proves the block lets through what the host needs, and refuses the home
   network:
   - a connection to the home network is refused at once;
   - `sudo apt-get update` succeeds (package mirrors reachable);
   - one `sudo cabinet-deploy poll` ends with its normal quiet outcome;
   - `sudo cabinet-deploy verify` on the installed archive and bundle prints the
     attested commit;
   - the start-up sync after the restart ends `changed` or `unchanged`, and
     health still reports `missingArt` 0 (picture downloads work);
   - the start-up log line shows the runtime sees the unit's memory limit.

Evidence kept in the repository is PASS lines, versions, status codes and
counts, never addresses, ranges, hostnames or file contents.

If anything ever locks SSH out, `pct enter {ctid}` from the Proxmox host still
gets in.

## D. Claude, with the owner's go (the next step of the work): the proxy

In the proxy's real dynamic file, Claude adds the outer-wall middlewares to the
public router and reduces the extra headers it sets to HSTS only. A backup of the
file is taken first and kept outside the watched folder.

## Commands Claude will run after the install (read-only unless marked)

Output limited to versions, status codes, counts and PASS lines:

- health and status on loopback (`/health`, `/cabinet/status`)
- the foreign-host request (expects `400`)
- `sudo -n cabinet-selfcheck`
- `systemctl show cabinet` for the limit properties
- `resolvectl status` and `ip -6 addr` (to fill step C)
- `journalctl -u cabinet --since {install-time} | grep 'The runtime sees'`
- `journalctl -u cabinet-deploy-poll --since {install-time}` (outcome lines)

Changing the server, each only with the owner's yes:

1. `sudo systemctl start cabinet-deploy-poll.service`, only if the release is
   still not installed 15 minutes after publication
2. step C: the three keys, provisioning, `apt-get update`, one poll and one
   verify
3. step D: the proxy edit, with its backup

No sync is pressed by hand and the env file is never opened.

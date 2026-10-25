---
created: 2026-10-20T08:30:00.000Z
title: Rotate the reverse proxy's access log
area: operations
severity: minor
files: []
---

## Problem

The reverse-proxy container's access log has no rotation (about 104 MB in October 2026, with several GB free). Once the cabinet is public, and during the controlled floods of the go-public work, it grows faster. Nothing in this repository owns that file: it belongs to the shared proxy container, which serves other sites as well.

## Solution

Owner maintenance item, outside the repository: add a `logrotate` entry (or Traefik's own access-log settings) on the proxy container, sized so a flood cannot fill the disk. The go-public measurements record the log size before and after each flood (`08-MEASUREMENTS.md`, sections "Flood" and "Verification flood on the public version"), which gives the growth rate to size the rotation from.

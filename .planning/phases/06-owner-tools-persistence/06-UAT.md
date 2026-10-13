---
status: complete
phase: 06-owner-tools-persistence
source: [06-VERIFICATION.md]
started: 2026-10-13T00:00:00Z
updated: 2026-10-13T14:13:21Z
---

## Current Test

[testing complete]

## Tests

### 1. Download a real backup of the owner data and keep it off the container
expected: A readable JSON file saved off the container; the banner switches to the downloaded state. Until this happens no backup of the real data exists, because the backup is the owner's own download (no scheduled job), by decision.
result: pass

### 2. One real round trip on the live owner host
expected: Download, then Restore from a file with that download and Check file; the preview shows the same locations, placed games and picture choices as the live data; Keep current data changes nothing.
result: pass

### 3. The proxy-side wall from outside home and the VPN
expected: From a device on another network with the VPN off, the owner hostname is refused by the proxy's address allow-list (403) and the public hostname answers 404 for every owner path.
result: pass

## Summary

total: 3
passed: 3
issues: 0
pending: 0
skipped: 0
blocked: 0

## Gaps

[none]

---
created: 2026-10-26T15:30:00.000Z
title: Serve a small not-found page instead of an empty 404
area: ui
severity: minor
files: []
---

## Problem

Unknown paths on the public site (for example `/health`, which exists only on the loopback operations port) answer 404 with an empty body and no content type. Safari on iOS presents that as an empty file download instead of an error page, which looks broken to a visitor who mistypes an address. Found during the phone probe after going public.

## Solution

Return a tiny static not-found page (HTML, same CSP and headers, the BGG credit, a link back to the cabinet) for unmatched paths on the public listener, keeping the status 404 and the health endpoint unexposed. Add an integration test for the content type and status, and keep `build/check-public-routes.sh` expecting 404.

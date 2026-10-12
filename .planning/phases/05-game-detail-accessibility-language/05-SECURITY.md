---
phase: 05
slug: game-detail-accessibility-language
status: verified
# threats_open = count of OPEN threats at or above workflow.security_block_on severity (the blocking gate)
threats_open: 0
asvs_level: 2
block_on: high
created: 2026-10-12
---

# Phase 05 — Security

> Per-phase security contract: threat register, accepted risks, and audit trail.

---

## Trust Boundaries

| Boundary | Description | Data Crossing |
|----------|-------------|---------------|
| Server to browser (cards) | `GET /cabinet/cards` serves card records shaped for the detail card and the games list, under a `default-src 'self'` CSP, no CORS | BGG-sourced text (titles, designers, mechanics), counts, same-origin `/art` cover addresses; no BGG or CDN addresses, no ranks, families or descriptions |
| Browser to server (language) | `GET /language/{code}` sets one preference cookie and redirects to a fixed target | `en` or `nl` only; sample name only when the catalog honours it |
| Page scripts to DOM | Card, list, keyboard and pull-out scripts render BGG text | Untrusted text written with `textContent` only; styles through CSSOM custom properties with validated values |
| Server to ops port | Health answer gains a missing-art count | Counts only, loopback-bound port |
| CI runner to repository | New browser-test job (Playwright, Chromium with `--with-deps`) | No secrets; `contents: read`; synthetic screenshots only as artifacts |
| Owner workstation to container | Owner-approved read-only commands during the release | Versions, status codes, header values and counts only |
| GitHub release to container | Pull-based deploy of the attested, owner-approved v0.6.0 | Release archive, checksum, attestation bundle |
| Local review artefacts to public repository | Review-round screenshots and the language table | Synthetic data only (samples, fake BGG) |

---

## Threat Register

Audited at commit `73f3aa3`; outside `.planning/` it is identical to the released merge `7edc720` (v0.6.0). Evidence per threat is in the audit verdict summarised below.

| Threat ID | Category | Component | Severity | Disposition | Mitigation | Status |
|-----------|----------|-----------|----------|-------------|------------|--------|
| 05-01/T-05-01-01 | Information disclosure | browser-screenshots artifact | medium | mitigate | Screenshots only to `CABINET_SCREENSHOT_DIR`; Testing host with sync off and real-network guard; no token or username in the browser tests; job env holds no secrets | closed |
| 05-01/T-05-01-02 | Tampering | ci.yml browser-tests job | medium | mitigate | `permissions: {}` / `contents: read`; every action SHA-pinned; push and pull_request only; actionlint and zizmor in lint | closed |
| 05-01/T-05-SC | Tampering | NuGet restore of Playwright | high | mitigate | Version pinned, lock file with content hashes, `--locked-mode` restore; release publishes the service only | closed |
| 05-01/T-05-01-03 | Elevation of privilege | `--with-deps` on runner | low | accept | Throwaway hosted runner, no secrets, only screenshots leave | closed |
| 05-02/T-05-02-01 | Tampering | cards endpoint query | medium | mitigate | Only `profile` and `sample` read, both allowlisted; never echoed; samples off in Production | closed |
| 05-02/T-05-02-02 | Denial of service | card JSON building | medium | mitigate | One lazy build per view and design; samples built once; 304 from cache | closed |
| 05-02/T-05-02-03 | Repudiation (terms) | card payload relaying BGG data | medium | mitigate | Fixed field set, same-origin cover only, allowlist test, no CORS, route undocumented | closed |
| 05-02/T-05-02-04 | Information disclosure | `location` field | high | mitigate | Private info off by default; parser and client gate it; production shows 0 locations | closed |
| 05-02/T-05-02-05 | Tampering (stored XSS) | BGG text in JSON | high | transfer | Transferred to the renderers: JSON content type, `textContent` everywhere, markup-API lint, strict CSP | closed |
| 05-03/T-05-03-01 | Tampering | `EntityTags.MatchesIfNoneMatch` | low | mitigate | Typed header parsing, compare only | closed |
| 05-03/T-05-03-02 | Information disclosure | stale cards after a sync | low | mitigate | Card-visible fields in the collection version; content-hash ETag; tests | closed |
| 05-03/T-05-03-03 | Denial of service | version hashing cost | low | accept | One hash per view and per view and design, never per request | closed |
| 05-04/T-05-04-01 | Tampering (open redirect) | language redirect | high | mitigate | Target is `/` or an escaped allowlisted sample; test with a foreign URL | closed |
| 05-04/T-05-04-02 | Tampering (header injection) | cookie and `Content-Language` | medium | mitigate | Written from constant language records only | closed |
| 05-04/T-05-04-03 | Information disclosure | shared caching | low | mitigate | `Vary: Accept-Language, Cookie`; 303 `no-store`; no output caching | closed |
| 05-04/T-05-04-04 | Tampering (CSRF) | cross-site GET flips language | low | accept | `SameSite=Lax`, preference only | closed |
| 05-04/T-05-04-05 | Denial of service | missing ICU | medium | mitigate | Startup check before `Build()`; deploy health check rolls back | closed |
| 05-04/T-05-04-06 | Information disclosure (privacy) | cookies | low | mitigate | One `HttpOnly` cookie; page sets none; tests | closed |
| 05-05/T-05-05-01 | Tampering | `language.js` request target | low | mitigate | Only server-rendered toggle links, same-origin credentials | closed |
| 05-05/T-05-05-02 | Tampering (stored XSS) | BGG titles in script strings | high | transfer | Plain strings consumed through `textContent`; lint and CSP | closed |
| 05-05/T-05-05-03 | Repudiation | translation drift | low | mitigate | Shared relative-time fixture; key and arity parity test | closed |
| 05-06/T-05-06-01 | Tampering (stored XSS) | `card-view.js` | high | mitigate | `createElement` + `textContent` for every value | closed |
| 05-06/T-05-06-02 | Tampering (link injection) | BGG link | high | mitigate | Fixed host plus digits-only id, otherwise no link; `rel="noopener"` | closed |
| 05-06/T-05-06-03 | Tampering | cover image URL | medium | mitigate | Same art-file pattern as the renderer, also for placement covers | closed |
| 05-06/T-05-06-04 | Denial of service (UX) | history step | low | mitigate | History state machine; Back browser tests | closed |
| 05-06/T-05-06-05 | Information disclosure | CSP violations with dialog open | low | mitigate | Console and page errors recorded; asserted empty in dialog tests | closed |
| 05-07/T-05-07-01 | Tampering (stored XSS) | card rows | high | mitigate | `createElement` + `textContent` | closed |
| 05-07/T-05-07-02 | Spoofing (bidi) | mixed-direction names | low | mitigate | `dir="auto"` on location, designers and title; isolation in CSS | closed |
| 05-07/T-05-07-03 | Information disclosure | location row | high | mitigate | Row only for a filled location; server gating; invented test locations | closed |
| 05-08/T-05-08-01 | Denial of service (UX) | key handler | low | mitigate | One rect pass per key; linear search; 400-box tests | closed |
| 05-08/T-05-08-02 | Tampering | `data-shelf`, `tabindex` | low | accept | Set from layout numbers and literals only | closed |
| 05-09/T-05-09-01 | Tampering (CSS injection) | chip colour | medium | mitigate | `setProperty` only after a strict hex check, else fallback | closed |
| 05-09/T-05-09-02 | Tampering (stored XSS) | expansion and base titles | high | mitigate | `textContent`; `dir="auto"` on title rows | closed |
| 05-09/T-05-09-03 | Spoofing | swap shows another game | low | mitigate | Swap only to known records; focus and dialog label follow the title | closed |
| 05-10/T-05-10-01 | Tampering (stored XSS) | list entry names | high | mitigate | Strings set with `textContent` | closed |
| 05-10/T-05-10-02 | Denial of service (UX) | 400-entry rebuild | low | mitigate | One rebuild per draw and per card arrival; in-place updates | closed |
| 05-10/T-05-10-03 | Spoofing (bidi) | titles in entry names | low | accept | Focus aid only; screen readers read logical order | closed |
| 05-11/T-05-11-01 | Denial of service (device) | animation cost | medium | mitigate | Transform and opacity only; 3D only on the named snapshot; no blur; tests | closed |
| 05-11/T-05-11-02 | Tampering (CSP) | transition names from script | medium | mitigate | Names live in CSS; scripts only toggle attributes | closed |
| 05-11/T-05-11-03 | Elevation (accessibility harm) | motion for vestibular-sensitive visitors | high | mitigate | Reduced-motion preference read at every open and close; no transition started; CSS backstop; spy tests | closed |
| 05-12/T-05-12-01 | Tampering (data fidelity) | shortened labels | low | mitigate | Only the label is shortened; cards use the full title | closed |
| 05-12/T-05-12-02 | Denial of service | golden re-record | low | accept | Synthetic samples only | closed |
| 05-13/T-05-13-01 | Denial of service (UX) | redraw during reading | low | mitigate | Redraw gated on the close; shared pending redraw; tests | closed |
| 05-13/T-05-13-02 | Denial of service (device) | drag performance | low | mitigate | `will-change` only while moving; translate only | closed |
| 05-13/T-05-13-03 | Tampering (CSP) | drag offset styling | low | mitigate | `style.setProperty` with a custom property only | closed |
| 05-14/T-05-14-01 | Tampering (path traversal) | `ArtFileAudit` file name | medium | mitigate | Prefix, separator, `..` and file-name checks; second check in the art cache; existence check only | closed |
| 05-14/T-05-14-02 | Information disclosure | warning log and health JSON | low | mitigate | Count-only log; `missingArt` only on the loopback-bound ops port | closed |
| 05-14/T-05-14-03 | Denial of service | audit cost per health call | low | accept | Loopback-only, bounded existence checks | closed |
| 05-15/T-05-15-01 | Information disclosure | review screenshots and tables | high | mitigate | Synthetic data only (spot-checked); personal-data pre-commit hook | closed |
| 05-15/T-05-15-02 | Information disclosure | private-info switch | medium | mitigate | Committed setting stays off; documented as local-only against the fake | closed |
| 05-16/T-05-16-01 | Tampering | release artifact | high | mitigate | Digest check, attestation, checksum and `gh attestation verify` in the release workflow; offline verify on the server; workstation verification | closed |
| 05-16/T-05-16-02 | Elevation of privilege | publishing | high | mitigate | `deploy` environment approval by the owner only after the reported draft check | closed |
| 05-16/T-05-16-06 | Elevation of privilege | merge into main and tag push | high | mitigate | Owner approval after three green checks; one tag on the merge commit, noreply tagger | closed |
| 05-16/T-05-16-03 | Information disclosure | evidence in SUMMARY | high | mitigate | Counts, versions, status codes and headers only; env file never opened | closed |
| 05-16/T-05-16-04 | Denial of service | missing ICU on host | medium | mitigate | As 05-04/T-05-04-05; production served `Content-Language: nl` | closed |
| 05-16/T-05-16-05 | Information disclosure | locations on the public card | high | mitigate | As 05-02/T-05-02-04; production shows 0 locations | closed |

*Status: open · closed · open — below high threshold (non-blocking)*
*Severity: critical > high > medium > low — only open threats at or above workflow.security_block_on count toward threats_open*
*Disposition: mitigate (implementation required) · accept (documented risk) · transfer (third-party)*

---

## Accepted Risks Log

| Risk ID | Threat Ref | Rationale | Accepted By | Date |
|---------|------------|-----------|-------------|------|
| AR-05-01 | 05-01/T-05-01-03 | `--with-deps` runs on a throwaway GitHub-hosted runner with no secrets in the job; nothing from it reaches the release | plan register, confirmed by audit | 2026-10-12 |
| AR-05-02 | 05-03/T-05-03-03 | One SHA-256 over a few hundred short lines per sync commit; no per-request cost | plan register, confirmed by audit | 2026-10-12 |
| AR-05-03 | 05-04/T-05-04-04 | Language preference only, `SameSite=Lax`, no state beyond the visitor's own display language; a POST form would need antiforgery and contradicts plain links | plan register, confirmed by audit | 2026-10-12 |
| AR-05-04 | 05-08/T-05-08-02 | `data-shelf` and `tabindex` are set from layout numbers via `dataset`/`setAttribute`; no visitor-supplied value; strict CSP unchanged | plan register, confirmed by audit | 2026-10-12 |
| AR-05-05 | 05-10/T-05-10-03 | A right-to-left title can reorder the visible panel text; screen readers read the logical order; the panel is a focus aid only | plan register, confirmed by audit | 2026-10-12 |
| AR-05-06 | 05-12/T-05-12-02 | Golden re-record uses synthetic samples only; the diff is reviewed to contain label values and the version only | plan register, confirmed by audit | 2026-10-12 |
| AR-05-07 | 05-14/T-05-14-03 | A few hundred `File.Exists` calls on a loopback endpoint polled about every ten minutes | plan register, confirmed by audit | 2026-10-12 |

*Accepted risks do not resurface in future audit runs.*

---

## Advisory Notes (no status change)

1. `detail.js` cover preloading accepts any `/art/` address rather than the full art-file pattern the card uses; same-origin image preload only, never rendered. Sharing one pattern would remove the difference.
2. About a third of the browser tests assert an empty console; nothing in teardown fails every test on a CSP report.
3. Mechanic items and the composed "Expansion for {title}" line carry no per-string `dir="auto"`; mechanics are isolated as a group in CSS. Display only.
4. On phones a second named snapshot (`card`) runs alongside `pull`, animating translate and opacity only; the animation tests assert presence of expected names rather than absence of others (except the cover-turn check).
5. The markup-API lint does not list `DOMParser`, `createContextualFragment`, `setHTML` or `srcdoc`; none appear in the shipped scripts.
6. The language cookie's `Secure` flag depends on the forwarded-proto and known-proxy configuration (review IN-04); low impact, functional cookie.
7. The `v0.6.0` tag push used the owner's admin bypass of the tag-creation rule; the owner approval gate itself was met.
8. Commits made after the audited commit by the review fixer (card-data retry and failure note; per-project test runs in CI) were not part of this audit; a quick diff showed `textContent` kept and no change to workflow pins or permissions.

---

## Security Audit Trail

| Audit Date | Threats Total | Closed | Open | Run By |
|------------|---------------|--------|------|--------|
| 2026-10-12 | 56 | 56 | 0 | gsd-security-auditor (ASVS L2, block_on high), audited commit `73f3aa3` = released `7edc720` outside `.planning/` |

### Security Audit 2026-10-12

| Metric | Count |
|--------|-------|
| Threats found | 56 |
| Closed | 56 (47 mitigate, 2 transfer, 7 accept) |
| Open | 0 |
| Unregistered flags | 0 |

---

## Sign-Off

- [x] All threats have a disposition (mitigate / accept / transfer)
- [x] Accepted risks documented in Accepted Risks Log
- [x] `threats_open: 0` confirmed
- [x] `status: verified` set in frontmatter

**Approval:** verified 2026-10-12
